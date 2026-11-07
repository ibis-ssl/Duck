import gzip
import importlib.util
import json
from pathlib import Path
import struct
import tempfile
import unittest


SCRIPT = Path(__file__).resolve().parents[1] / "verify_aspire_motion_acceptance.py"
SPEC = importlib.util.spec_from_file_location("verify_aspire_motion_acceptance", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(MODULE)


def _varint(value):
    encoded = bytearray()
    while value > 0x7f:
        encoded.append((value & 0x7f) | 0x80)
        value >>= 7
    encoded.append(value)
    return bytes(encoded)


def _integer_field(number, value):
    return _varint((number << 3) | 0) + _varint(value)


def _message_field(number, value):
    return _varint((number << 3) | 2) + _varint(len(value)) + value


def _float_field(number, value):
    return _varint((number << 3) | 5) + struct.pack("<f", value)


def _robot(x, y, robot_id=0):
    return _integer_field(2, robot_id) + _float_field(3, x) + _float_field(4, y)


def _vision_packet(yellow, blue):
    frame = b"".join(_message_field(6, item) for item in yellow)
    frame += b"".join(_message_field(7, item) for item in blue)
    return _message_field(1, frame)


def _referee_packet(command, counter):
    return _integer_field(4, command) + _integer_field(5, counter)


def _record(message_type, seconds, payload):
    timestamp_ns = (1_800_000_000 + seconds) * 1_000_000_000
    return (struct.pack(">qii", timestamp_ns, message_type, len(payload)) + payload)


def _archive(records):
    content = b"SSL_LOG_FILE" + struct.pack(">i", 1) + b"".join(records)
    return gzip.compress(content)


class MotionAcceptanceTests(unittest.TestCase):
    def test_both_teams_move_inside_the_same_normal_start_window(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)])),
        ])

        result = MODULE.assess_archive_bytes(archive)

        self.assertEqual("passed", result["status"])
        self.assertEqual("NORMAL_START", result["successfulWindow"]["command"])
        self.assertEqual(7, result["successfulWindow"]["commandCounter"])
        self.assertTrue(result["successfulWindow"]["craneMoved"])
        self.assertTrue(result["successfulWindow"]["tigersMoved"])

    def test_motion_in_separate_windows_does_not_satisfy_same_window(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.0, 0.0)])),
            _record(3, 3, _referee_packet(3, 8)),
            _record(4, 4, _vision_packet([_robot(0.2, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 5, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)])),
        ])

        result = MODULE.assess_archive_bytes(archive)

        self.assertNotEqual("passed", result["status"])
        self.assertFalse(result["sameWindowMotionObserved"])

    def test_repeated_command_after_stop_does_not_reuse_motion_baseline(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.0, 0.0)])),
            _record(3, 3, _referee_packet(1, 8)),  # STOP
            _record(3, 4, _referee_packet(2, 7)),  # stale/regressed command counter
            _record(4, 5, _vision_packet([_robot(0.2, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 6, _vision_packet([_robot(0.4, 0.0)], [_robot(1.2, 0.0)])),
        ])

        result = MODULE.assess_archive_bytes(archive)

        self.assertNotEqual("passed", result["status"])
        self.assertEqual(1, len(result["activeWindows"]))
        self.assertTrue(result["activeWindows"][0]["craneMoved"])
        self.assertFalse(result["activeWindows"][0]["tigersMoved"])

    def test_duplicate_robot_ids_in_one_frame_are_excluded(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet(
                [_robot(0.0, 0.0), _robot(0.2, 0.0)],
                [_robot(1.0, 0.0), _robot(1.2, 0.0)],
            )),
        ])

        result = MODULE.assess_archive_bytes(archive)

        self.assertNotEqual("passed", result["status"])
        self.assertEqual(1, result["activeWindows"][0]["visionPackets"])
        self.assertFalse(result["activeWindows"][0]["craneMoved"])
        self.assertFalse(result["activeWindows"][0]["tigersMoved"])

    def test_legitimate_movement_across_distinct_frames_still_passes(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)])),
        ])

        result = MODULE.assess_archive_bytes(archive)

        self.assertEqual("passed", result["status"])

    def test_uint32_command_counter_wraparound_is_treated_as_newer(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 0xffffffff)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(3, 2, _referee_packet(2, 0)),
            _record(4, 3, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 4, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)])),
        ])

        result = MODULE.assess_archive_bytes(archive)

        self.assertEqual("passed", result["status"])
        self.assertEqual([0xffffffff, 0], [item["commandCounter"] for item in result["activeWindows"]])

    def test_command_counter_above_uint32_is_malformed(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 0x1_0000_0000)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)])),
        ])

        result = MODULE.assess_archive_bytes(archive)

        self.assertEqual("failed", result["status"])
        self.assertFalse(result["sameWindowMotionObserved"])
        self.assertEqual(0, result["packetCounts"]["validReferee"])

    def test_malformed_vision_wrapper_tail_cannot_pass_motion_acceptance(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)]) + b"\xff"),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)]) + b"\xff"),
        ])

        result = MODULE.assess_archive_bytes(archive)

        self.assertEqual("failed", result["status"])
        self.assertFalse(result["sameWindowMotionObserved"])
        self.assertEqual(0, result["packetCounts"]["validVision"])
        self.assertFalse(result["activeWindows"][0]["craneMoved"])
        self.assertFalse(result["activeWindows"][0]["tigersMoved"])

    def test_vision_wrapper_validates_trailing_protobuf_wire_data(self):
        valid_prefix = _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])
        invalid_tails = [
            (b"\xff", "Malformed protobuf varint."),
            (_varint((2 << 3) | 5) + b"\x00\x00\x00", "Truncated fixed32 protobuf field."),
            (_varint((2 << 3) | 3), "Unsupported protobuf wire type 3."),
            (valid_prefix, "Vision wrapper packet has multiple detection frames."),
        ]
        for tail, message in invalid_tails:
            with self.subTest(message=message), self.assertRaisesRegex(MODULE.EvidenceError, message):
                MODULE._vision_positions(valid_prefix + tail)

    def test_partial_record_header_preserves_decoded_window_summaries(self):
        content = b"SSL_LOG_FILE" + struct.pack(">i", 1) + b"".join([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)])),
        ]) + b"partial-header"

        result = MODULE.assess_archive_bytes(gzip.compress(content))

        self.assertEqual("incomplete", result["status"])
        self.assertEqual(1, len(result["activeWindows"]))
        self.assertTrue(result["activeWindows"][0]["craneMoved"])
        self.assertTrue(result["activeWindows"][0]["tigersMoved"])
        self.assertTrue(result["sameWindowMotionObserved"])
        self.assertEqual("SSL log ends with a partial record header.", result["reason"])

    def test_partial_payload_preserves_decoded_window_summaries(self):
        content = b"SSL_LOG_FILE" + struct.pack(">i", 1) + b"".join([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)])),
        ]) + struct.pack(">qii", 1_800_000_003_000_000_000, 4, 100) + b"partial"

        result = MODULE.assess_archive_bytes(gzip.compress(content))

        self.assertEqual("incomplete", result["status"])
        self.assertEqual(1, len(result["activeWindows"]))
        self.assertTrue(result["sameWindowMotionObserved"])
        self.assertEqual("SSL log ends with a partial packet payload.", result["reason"])

    def test_invalid_record_length_preserves_decoded_window_summaries(self):
        content = b"SSL_LOG_FILE" + struct.pack(">i", 1) + b"".join([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)])),
            struct.pack(">qii", 1_800_000_003_000_000_000, 4, 0),
        ])

        result = MODULE.assess_archive_bytes(gzip.compress(content))

        self.assertEqual("incomplete", result["status"])
        self.assertEqual(1, len(result["activeWindows"]))
        self.assertTrue(result["sameWindowMotionObserved"])
        self.assertEqual("Invalid SSL log payload length 0.", result["reason"])

    def test_static_team_and_preparatory_commands_do_not_pass(self):
        archive = _archive([
            _record(3, 0, _referee_packet(5, 7)),  # PREPARE_KICKOFF_BLUE
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.0, 0.0)])),
        ])

        result = MODULE.assess_archive_bytes(archive)

        self.assertNotEqual("passed", result["status"])
        self.assertFalse(result["sameWindowMotionObserved"])

    def test_incomplete_gzip_never_passes_even_if_motion_precedes_the_truncated_footer(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
            _record(4, 2, _vision_packet([_robot(0.2, 0.0)], [_robot(1.2, 0.0)])),
        ])[:-8]

        result = MODULE.assess_archive_bytes(archive)

        self.assertNotEqual("passed", result["status"])
        self.assertTrue(result["partialEvidencePreserved"])

    def test_cli_writes_partial_json_evidence_when_archive_is_incomplete(self):
        archive = _archive([
            _record(3, 0, _referee_packet(2, 7)),
            _record(4, 1, _vision_packet([_robot(0.0, 0.0)], [_robot(1.0, 0.0)])),
        ])[:-8]
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            archive_path = root / "partial.log.gz"
            output_path = root / "evidence" / "motion-acceptance.json"
            archive_path.write_bytes(archive)

            exit_code = MODULE.main(["--archive", str(archive_path), "--output", str(output_path)])

            self.assertEqual(1, exit_code)
            report = json.loads(output_path.read_text(encoding="utf-8"))
            self.assertEqual("incomplete", report["status"])
            self.assertGreater(report["packetCounts"]["records"], 0)
            self.assertTrue(report["partialEvidencePreserved"])


if __name__ == "__main__":
    unittest.main()
