#!/usr/bin/env python3
"""Verify same-active-referee-window robot motion in an SSL log archive."""

from __future__ import annotations

import argparse
import json
import math
from pathlib import Path
import struct
import sys
from typing import Any
import zlib


HEADER = b"SSL_LOG_FILE"
ACTIVE_COMMANDS = {
    2: "NORMAL_START",
    3: "FORCE_START",
    8: "DIRECT_FREE_YELLOW",
    9: "DIRECT_FREE_BLUE",
    10: "INDIRECT_FREE_YELLOW",
    11: "INDIRECT_FREE_BLUE",
}
REFEREE_RECORD = 3
VISION_RECORD = 4
MAXIMUM_PAYLOAD_BYTES = 16 * 1024 * 1024
COUNTER_MODULUS = 1 << 32
COUNTER_HALF_RANGE = 1 << 31
COUNTER_MAX = COUNTER_MODULUS - 1


class EvidenceError(ValueError):
    pass


def _read_varint(data: bytes, offset: int) -> tuple[int, int]:
    value = 0
    shift = 0
    while offset < len(data) and shift < 70:
        byte = data[offset]
        offset += 1
        value |= (byte & 0x7f) << shift
        if byte < 0x80:
            return value, offset
        shift += 7
    raise EvidenceError("Malformed protobuf varint.")


def _protobuf_fields(data: bytes):
    offset = 0
    while offset < len(data):
        key, offset = _read_varint(data, offset)
        number, wire_type = key >> 3, key & 7
        if number == 0:
            raise EvidenceError("Protobuf field number zero is invalid.")
        if wire_type == 0:
            value, offset = _read_varint(data, offset)
        elif wire_type == 1:
            if offset + 8 > len(data):
                raise EvidenceError("Truncated fixed64 protobuf field.")
            value, offset = data[offset:offset + 8], offset + 8
        elif wire_type == 2:
            length, offset = _read_varint(data, offset)
            if length > MAXIMUM_PAYLOAD_BYTES or offset + length > len(data):
                raise EvidenceError("Truncated or oversized length-delimited protobuf field.")
            value, offset = data[offset:offset + length], offset + length
        elif wire_type == 5:
            if offset + 4 > len(data):
                raise EvidenceError("Truncated fixed32 protobuf field.")
            value, offset = data[offset:offset + 4], offset + 4
        else:
            raise EvidenceError(f"Unsupported protobuf wire type {wire_type}.")
        yield number, wire_type, value


def _referee_command(payload: bytes) -> tuple[int, int]:
    command = counter = None
    for number, wire_type, value in _protobuf_fields(payload):
        if wire_type == 0 and number == 4:
            command = value
        elif wire_type == 0 and number == 5:
            counter = value
    if command is None or counter is None:
        raise EvidenceError("Referee packet is missing command or command counter.")
    if counter > COUNTER_MAX:
        raise EvidenceError("Referee command counter is outside uint32 range.")
    return command, counter


def _counter_is_newer(counter: int, previous: int) -> bool:
    """Compare uint32 command counters using serial-number arithmetic."""
    distance = (counter - previous) % COUNTER_MODULUS
    return 0 < distance < COUNTER_HALF_RANGE


def _robot_position(payload: bytes) -> tuple[int, float, float] | None:
    robot_id = x = y = None
    for number, wire_type, value in _protobuf_fields(payload):
        if number == 2 and wire_type == 0:
            robot_id = value
        elif number == 3 and wire_type == 5:
            x = struct.unpack("<f", value)[0]
        elif number == 4 and wire_type == 5:
            y = struct.unpack("<f", value)[0]
    if robot_id is None or x is None or y is None or not math.isfinite(x) or not math.isfinite(y):
        return None
    return robot_id, x, y


def _vision_positions(payload: bytes) -> tuple[list[tuple[int, float, float]], list[tuple[int, float, float]]]:
    frame = None
    for number, wire_type, value in _protobuf_fields(payload):
        if number == 1 and wire_type == 2:
            if frame is not None:
                raise EvidenceError("Vision wrapper packet has multiple detection frames.")
            frame = value
    if frame is None:
        raise EvidenceError("Vision wrapper packet has no detection frame.")

    yellow, blue = [], []
    for number, wire_type, value in _protobuf_fields(frame):
        if number in (6, 7) and wire_type == 2:
            position = _robot_position(value)
            if position is not None:
                (yellow if number == 6 else blue).append(position)
    return yellow, blue


def _decompress(data: bytes) -> tuple[bytes, bool, str | None]:
    decompressor = zlib.decompressobj(16 + zlib.MAX_WBITS)
    output = bytearray()
    footer_offset = max(0, len(data) - 8)
    try:
        output.extend(decompressor.decompress(data[:footer_offset]))
        output.extend(decompressor.decompress(data[footer_offset:]))
        if decompressor.unused_data:
            return bytes(output), False, "Unexpected data follows the gzip member."
        if not decompressor.eof:
            return bytes(output), False, "Gzip member ended before its complete checksum footer."
        return bytes(output), True, None
    except zlib.error as error:
        return bytes(output), False, str(error)


def _empty_window(counter: int, command: str) -> dict[str, Any]:
    return {
        "commandCounter": counter,
        "command": command,
        "visionPackets": 0,
        "robots": {"crane": {}, "tigers": {}},
    }


def _observe_positions(window: dict[str, Any], team: str, robots: list[tuple[int, float, float]]) -> None:
    counts: dict[int, int] = {}
    positions: dict[int, list[float]] = {}
    for robot_id, x, y in robots:
        counts[robot_id] = counts.get(robot_id, 0) + 1
        positions[robot_id] = [x, y]

    observed = window["robots"][team]
    for robot_id, position in positions.items():
        # Conflicting duplicate IDs make this frame ambiguous for that robot.
        # Ignore the ID for the whole frame so duplicates can never imply motion.
        if counts[robot_id] != 1:
            continue
        entry = observed.get(robot_id)
        if entry is None:
            observed[robot_id] = {"last": position, "moved": False}
        elif entry["last"] != position:
            entry["moved"] = True
            entry["last"] = position


def _window_summaries(windows: list[dict[str, Any]]) -> list[dict[str, Any]]:
    summaries = []
    for window in windows:
        crane_moving = any(robot["moved"] for robot in window["robots"]["crane"].values())
        tigers_moving = any(robot["moved"] for robot in window["robots"]["tigers"].values())
        summaries.append({
            "commandCounter": window["commandCounter"],
            "command": window["command"],
            "visionPackets": window["visionPackets"],
            "craneMovingRobotCount": sum(robot["moved"] for robot in window["robots"]["crane"].values()),
            "tigersMovingRobotCount": sum(robot["moved"] for robot in window["robots"]["tigers"].values()),
            "craneMoved": crane_moving,
            "tigersMoved": tigers_moving,
        })
    return summaries


def assess_archive_bytes(archive: bytes) -> dict[str, Any]:
    """Return a machine-readable verdict while preserving evidence on failure."""
    content, decompressed_eof, gzip_error = _decompress(archive)
    report: dict[str, Any] = {
        "schemaVersion": 1,
        "status": "incomplete",
        "criterion": "Crane and TIGERs move in the same active referee command window.",
        "archive": {"gzipEof": decompressed_eof, "recordStreamComplete": False},
        "packetCounts": {"records": 0, "referee": 0, "vision": 0, "validReferee": 0, "validVision": 0},
        "sameWindowMotionObserved": False,
        "successfulWindow": None,
        "activeWindows": [],
        "partialEvidencePreserved": False,
        "senderIdentity": {
            "capturedBySslLogFormatV1": False,
            "reason": "SSL_LOG_FILE v1 stores receive time, message type, and packet payload, not a network source endpoint.",
        },
        "unverifiedDetails": ["Packet sender identity must be recorded from a separate current-run source capture."],
    }

    windows: list[dict[str, Any]] = []
    current_window_key = None
    current_window = None
    last_referee_counter = None
    try:
        if len(content) < len(HEADER) + 4 or content[:len(HEADER)] != HEADER:
            raise EvidenceError("SSL log header is missing or truncated.")
        version = struct.unpack_from(">i", content, len(HEADER))[0]
        if version != 1:
            raise EvidenceError(f"Unsupported SSL log format version {version}.")

        offset = len(HEADER) + 4
        records = report["packetCounts"]
        while offset < len(content):
            if len(content) - offset < 16:
                raise EvidenceError("SSL log ends with a partial record header.")
            _timestamp_ns, message_type, payload_length = struct.unpack_from(">qii", content, offset)
            offset += 16
            if payload_length <= 0 or payload_length > MAXIMUM_PAYLOAD_BYTES:
                raise EvidenceError(f"Invalid SSL log payload length {payload_length}.")
            if offset + payload_length > len(content):
                raise EvidenceError("SSL log ends with a partial packet payload.")
            payload = content[offset:offset + payload_length]
            offset += payload_length
            records["records"] += 1

            if message_type == REFEREE_RECORD:
                records["referee"] += 1
                try:
                    command_id, counter = _referee_command(payload)
                    records["validReferee"] += 1
                except EvidenceError:
                    current_window_key = None
                    current_window = None
                    continue
                if last_referee_counter is None:
                    last_referee_counter = counter
                elif counter == last_referee_counter:
                    # A duplicate packet may continue only the exact current command.
                    # Never reopen a command after STOP/malformed input at the same counter.
                    if current_window_key != (counter, command_id) or current_window is None:
                        current_window_key = None
                        current_window = None
                    continue
                elif not _counter_is_newer(counter, last_referee_counter):
                    # Stale/regressed UDP packets cannot reopen old acceptance windows.
                    current_window_key = None
                    current_window = None
                    continue
                else:
                    last_referee_counter = counter
                command_name = ACTIVE_COMMANDS.get(command_id)
                if command_name is None:
                    current_window_key = None
                    current_window = None
                    continue
                next_window_key = (counter, command_id)
                if next_window_key != current_window_key:
                    current_window_key = next_window_key
                    current_window = _empty_window(counter, command_name)
                    windows.append(current_window)
            elif message_type == VISION_RECORD:
                records["vision"] += 1
                try:
                    yellow, blue = _vision_positions(payload)
                    records["validVision"] += 1
                except EvidenceError:
                    continue
                if current_window is not None:
                    current_window["visionPackets"] += 1
                    _observe_positions(current_window, "crane", yellow)
                    _observe_positions(current_window, "tigers", blue)

        report["archive"]["recordStreamComplete"] = True
        report["archive"]["formatVersion"] = version
        report["archive"]["gzipError"] = gzip_error
        report["partialEvidencePreserved"] = records["records"] > 0
        report["archive"]["gzipError"] = gzip_error
    except (EvidenceError, struct.error) as error:
        report["reason"] = str(error)
        report["partialEvidencePreserved"] = report["packetCounts"]["records"] > 0
        if gzip_error:
            report["archive"]["gzipError"] = gzip_error
    finally:
        summaries = _window_summaries(windows)
        report["activeWindows"] = summaries
        report["successfulWindow"] = next(
            (summary for summary in summaries if summary["craneMoved"] and summary["tigersMoved"]),
            None,
        )
        report["sameWindowMotionObserved"] = report["successfulWindow"] is not None
        report["partialEvidencePreserved"] = report["packetCounts"]["records"] > 0
        if report["archive"]["recordStreamComplete"] and decompressed_eof:
            if report["sameWindowMotionObserved"]:
                report["status"] = "passed"
                report["unverifiedDetails"] = ["Packet sender identity is not stored in SSL_LOG_FILE v1 and must be read from the separate source-capture evidence."]
            else:
                report["status"] = "failed"
                report["reason"] = "No active referee command window contained position changes for both teams."
        else:
            report["status"] = "incomplete"
            report.setdefault("reason", gzip_error or "SSL packet record stream is incomplete.")
    return report


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--archive", required=True, type=Path, help="Current-run SSL log recorder .log.gz archive")
    parser.add_argument("--output", type=Path, help="Write the JSON result here; defaults to stdout")
    arguments = parser.parse_args(argv)
    try:
        archive = arguments.archive.read_bytes()
        report = assess_archive_bytes(archive)
    except OSError as error:
        report = {
            "schemaVersion": 1,
            "status": "incomplete",
            "reason": f"Could not read the SSL log archive: {error}",
            "partialEvidencePreserved": False,
        }
    serialized = json.dumps(report, indent=2) + "\n"
    if arguments.output:
        arguments.output.parent.mkdir(parents=True, exist_ok=True)
        arguments.output.write_text(serialized, encoding="utf-8")
    else:
        sys.stdout.write(serialized)
    return 0 if report.get("status") == "passed" else 1


if __name__ == "__main__":
    raise SystemExit(main())
