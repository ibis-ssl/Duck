#!/usr/bin/env python3
"""Wait for and validate the existing AppHost match-mode acceptance run."""

from __future__ import annotations

import argparse
import hashlib
import ipaddress
from datetime import datetime
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import time
from typing import Any
import gzip
from tempfile import gettempdir


MATCH_RESOURCES = {
    "simulator", "game-controller", "cm4-sim", "crane", "duck",
    "tigers-blue", "autoref-tigers", "ssl-log-recorder",
}
MATCH_PARTICIPANT_RESOURCES = MATCH_RESOURCES - {"cm4-sim"}
EXCLUDED_RESOURCES = {"tracker-tigers", "tracker-erforce", "debug-host"}
MATCH_CONTROLLER = "match-controller"


def _load_json(path: Path) -> dict[str, Any] | None:
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
        return value if isinstance(value, dict) else None
    except (OSError, json.JSONDecodeError):
        return None


def _container_inventory(stack_id: str) -> list[dict[str, Any]]:
    listed = subprocess.run(
        ["docker", "ps", "-aq", "--no-trunc", "--filter", "label=duck.aspire.owner=duck-apphost",
         "--filter", f"label=duck.aspire.stack={stack_id}"],
        capture_output=True, text=True, check=False,
    )
    if listed.returncode:
        raise RuntimeError("Docker could not list containers owned by this run.")
    ids = [item.strip() for item in listed.stdout.splitlines() if item.strip()]
    if not ids:
        return []
    inspected = subprocess.run(["docker", "inspect", *ids], capture_output=True, text=True, check=False)
    if inspected.returncode:
        raise RuntimeError("Docker could not inspect containers owned by this run.")
    value = json.loads(inspected.stdout)
    return value if isinstance(value, list) else []


def _preserve_partial_evidence(evidence_dir: Path, output_dir: Path, stack_id: str) -> None:
    output_dir.mkdir(parents=True, exist_ok=True)
    for name in ("participant-status.json", "match-result.json", "referee-source-capture.json"):
        source = evidence_dir / name
        if source.is_file():
            shutil.copy2(source, output_dir / name)
    report_path = output_dir / "match-acceptance.json"
    if not report_path.exists():
        report_path.write_text(json.dumps({
            "status": "incomplete",
            "stackId": stack_id,
            "issue51AcceptanceComplete": False,
            "unverifiedCriteria": ["Runtime evidence collection did not complete."],
        }, indent=2) + "\n", encoding="utf-8")


def _check_source_capture(
    evidence_dir: Path, stack_id: str, receiver: dict[str, Any], result: dict[str, Any]
) -> dict[str, Any]:
    capture = _load_json(evidence_dir / "referee-source-capture.json")
    if capture is None:
        raise RuntimeError("No readable referee source capture was retained.")
    labels = (receiver.get("Config") or {}).get("Labels") or {}
    if (type(capture.get("schemaVersion")) is not int or capture["schemaVersion"] != 1
            or capture.get("stackId") != stack_id
            or capture.get("runId") != labels.get("duck.aspire.run")
            or capture.get("receiverResourceId") != MATCH_CONTROLLER
            or capture.get("receiverContainerId") != receiver.get("Id")):
        raise RuntimeError("Referee source capture does not match the current stack/run/receiver.")
    epoch = capture.get("receiverEpoch")
    if type(epoch) is not int or epoch <= 0:
        raise RuntimeError("Referee source capture has no valid receiver epoch.")
    # The result is published after capture finalization and binds that exact epoch.
    result_epoch = result.get("refereeSourceCaptureEpoch")
    if type(result_epoch) is not int or result_epoch != epoch:
        raise RuntimeError("Referee source capture receiver epoch differs from the match result.")
    generation = capture.get("receiverGeneration")
    if type(generation) is not int or generation <= 0:
        raise RuntimeError("Referee source capture has no valid receiver generation.")
    if capture.get("captureComplete") is not True or capture.get("captureTruncated", False) is not False:
        raise RuntimeError("Referee source capture is incomplete or truncated.")
    try:
        started = datetime.fromisoformat(capture["captureStartedAtUtc"].replace("Z", "+00:00"))
        ended = datetime.fromisoformat(capture["captureEndedAtUtc"].replace("Z", "+00:00"))
        if started.utcoffset() is None or ended.utcoffset() is None or ended < started:
            raise ValueError("invalid observation window")
    except (KeyError, TypeError, ValueError, AttributeError) as error:
        raise RuntimeError("Referee source capture lacks a valid observation window.") from error
    receiver_started = (receiver.get("State") or {}).get("StartedAt")
    try:
        boot_started = datetime.fromisoformat(receiver_started.replace("Z", "+00:00"))
        if boot_started.utcoffset() is None or started < boot_started:
            raise ValueError("capture predates the receiver process")
    except (TypeError, ValueError, AttributeError) as error:
        raise RuntimeError("Referee source capture lacks current receiver start evidence or predates it.") from error
    destination = capture.get("destination")
    if (not isinstance(destination, dict) or destination.get("address") != "224.5.23.1"
            or type(destination.get("port")) is not int or destination["port"] != 11003):
        raise RuntimeError("Referee source capture destination differs from 224.5.23.1:11003.")
    total = capture.get("totalDatagramCount")
    sources = capture.get("sources")
    if type(total) is not int or total <= 0 or not isinstance(sources, list) or not sources:
        raise RuntimeError("Referee source capture contains no observed datagrams.")
    counted = 0
    tuples = set()
    for source in sources:
        if not isinstance(source, dict):
            raise RuntimeError("Referee source capture has a malformed sender tuple.")
        try:
            if not isinstance(source.get("address"), str):
                raise ValueError("invalid sender address")
            address = ipaddress.ip_address(source["address"])
            if address.version != 4 or address.is_multicast or address.is_unspecified:
                raise ValueError("invalid sender address")
        except (ValueError, TypeError) as error:
            raise RuntimeError("Referee source capture has an invalid sender address.") from error
        port, count = source.get("port"), source.get("datagramCount")
        if type(port) is not int or not 1 <= port <= 65535 or type(count) is not int or count <= 0:
            raise RuntimeError("Referee source capture has an invalid sender port or datagram count.")
        endpoint = (str(address), port)
        if endpoint in tuples:
            raise RuntimeError("Referee source capture duplicated a sender tuple.")
        tuples.add(endpoint)
        counted += count
    if counted != total:
        raise RuntimeError("Referee source capture datagram totals do not reconcile.")
    if len(tuples) != 1:
        raise RuntimeError(f"Expected one unique observed referee source tuple; found {len(tuples)}.")
    # No process/container attribution follows from a host-network source IP.
    return capture


def _check_evidence(stack_id: str, evidence_dir: Path, output_dir: Path) -> dict[str, Any]:
    _preserve_partial_evidence(evidence_dir, output_dir, stack_id)
    status_path = evidence_dir / "participant-status.json"
    result_path = evidence_dir / "match-result.json"
    status = _load_json(status_path)
    result = _load_json(result_path)
    if status is None or result is None:
        raise RuntimeError("Match Controller did not leave both participant-status.json and match-result.json.")

    resource_rows = status.get("resources")
    if not isinstance(resource_rows, list):
        raise RuntimeError("Participant status has no resource list.")
    by_name = {row.get("resourceId"): row for row in resource_rows if isinstance(row, dict)}
    if len(resource_rows) != len(MATCH_PARTICIPANT_RESOURCES) or set(by_name) != MATCH_PARTICIPANT_RESOURCES:
        raise RuntimeError(f"Match participant set differed from the expected seven resources: {sorted(by_name)}")
    for name, row in by_name.items():
        if row.get("state") != "Running" or row.get("healthStatus") != "Healthy" or row.get("isReady") is not True:
            raise RuntimeError(f"Match resource {name} was not running, healthy, and ready in the final status snapshot.")

    final_readiness = result.get("finalReadiness")
    result_ready = (
        final_readiness.get("participantResourcesReady")
        if isinstance(final_readiness, dict)
        else None
    )
    if result_ready is not True:
        raise RuntimeError("Match Controller final readiness was not complete.")

    containers = _container_inventory(stack_id)
    labels_by_resource: dict[str, list[dict[str, Any]]] = {}
    for item in containers:
        labels = (item.get("Config") or {}).get("Labels") or {}
        name = labels.get("duck.aspire.resource")
        if name:
            labels_by_resource.setdefault(name, []).append(item)
    for name in MATCH_RESOURCES | {MATCH_CONTROLLER}:
        rows = labels_by_resource.get(name, [])
        if len(rows) != 1 or (name != MATCH_CONTROLLER and rows[0].get("State", {}).get("Running") is not True):
            raise RuntimeError(f"Expected exactly one owned {name} container, running for participants; found {len(rows)}.")
        labels = (rows[0].get("Config") or {}).get("Labels") or {}
        if labels.get("duck.aspire.owner") != "duck-apphost" or labels.get("duck.aspire.stack") != stack_id:
            raise RuntimeError(f"Owned {name} container does not match the current AppHost stack.")
        if labels.get("duck.aspire.run") != stack_id:
            raise RuntimeError(f"Owned {name} container does not match the current run generation.")
    if EXCLUDED_RESOURCES.intersection(labels_by_resource):
        raise RuntimeError("An excluded base/comparison resource appeared in the match stack.")

    for name, row in by_name.items():
        item = labels_by_resource[name][0]
        labels = (item.get("Config") or {}).get("Labels") or {}
        if row.get("containerId") != item.get("Id") or row.get("runId") != stack_id:
            raise RuntimeError(f"Participant evidence for {name} did not match the current container/run generation.")
        if row.get("currentResourceId") in (None, "") or not labels.get("duck.aspire.resource") == name:
            raise RuntimeError(f"Participant evidence for {name} has no current resource identity.")

    def command(resource: str) -> str:
        item = labels_by_resource[resource][0]
        config = item.get("Config") or {}
        return " ".join(str(value) for value in (config.get("Entrypoint") or []) + (config.get("Cmd") or []))

    if "--ibis-team-color yellow" not in command("simulator"):
        raise RuntimeError("Simulator did not use the expected yellow ibis team color.")
    if "team:=ibis" not in command("crane"):
        raise RuntimeError("Crane did not use the expected ibis team.")
    crane_environment = (labels_by_resource["crane"][0].get("Config") or {}).get("Env") or []
    if "DUCK_CRANE_IBIS_PACKET_ADAPTER=true" not in crane_environment or \
        "DUCK_CRANE_EXPECTED_RESOURCE=cm4-sim" not in crane_environment:
        raise RuntimeError("Crane was not configured to route Ibis packets through cm4-sim.")
    cm4_command = command("cm4-sim")
    if "--in-port 12345" not in cm4_command or "--out-port 12346" not in cm4_command:
        raise RuntimeError("CM4 simulator did not use the expected Crane and Simulator Ibis ports.")
    if "--aiBlue" not in command("tigers-blue"):
        raise RuntimeError("Sumatra did not use the expected --aiBlue option.")

    publishers = []
    for resource, rows in labels_by_resource.items():
        for item in rows:
            config = item.get("Config") or {}
            argv = (config.get("Entrypoint") or []) + (config.get("Cmd") or [])
            for index, value in enumerate(argv):
                if value in ("-publishAddress", "--publishAddress") and index + 1 < len(argv) and argv[index + 1] == "224.5.23.1:11003":
                    publishers.append(resource)
    if publishers != ["game-controller"]:
        raise RuntimeError(f"Expected one configured Game Controller publisher for 224.5.23.1:11003; found {publishers}.")

    gc_id = labels_by_resource["game-controller"][0]["Id"]
    gc = subprocess.run(["docker", "logs", "--timestamps", gc_id], capture_output=True, text=True, check=False)
    if gc.returncode:
        raise RuntimeError("Could not read the run-owned Game Controller log.")
    gc_log = gc.stdout + gc.stderr
    initial_stop = result.get("initialStopCommand") == "STOP"
    if not initial_stop:
        raise RuntimeError("Match Controller did not retain evidence of its initial STOP command.")
    required_log_evidence = {
        "tigers_connected": "Team TIGERs Mannheim connected" in gc_log,
        "blue_tigers_client_connected": "Team Client BLUE-TIGERs Mannheim connected" in gc_log,
        "stop_command_observed": bool(re.search(r'"type"\s*:\s*"STOP"', gc_log)),
    }
    if not all(required_log_evidence.values()):
        raise RuntimeError(f"Game Controller log lacked expected team/STOP evidence: {required_log_evidence}.")

    recorder_reason = by_name["ssl-log-recorder"].get("serviceEvidenceReason", "")
    counts = re.search(r"referee=(\d+),\s*vision-detection=(\d+),\s*tracker=(\d+)", recorder_reason)
    if counts is None or any(int(value) <= 0 for value in counts.groups()):
        raise RuntimeError("Recorder status did not contain positive packet counts for referee, detection, and tracker.")

    capture = _check_source_capture(evidence_dir, stack_id, labels_by_resource[MATCH_CONTROLLER][0], result)
    linux_host_verified = status.get("appHostOperatingSystem") == "linux"

    report = {
        "status": "passed" if linux_host_verified else "partial",
        "stackId": stack_id,
        "acceptanceScope": "ASPIRE-006G1 / ASPIRE-MATCH-001 and ASPIRE-MATCH-002",
        "appHostOperatingSystem": status.get("appHostOperatingSystem"),
        "linuxHostVerified": linux_host_verified,
        "issue51AcceptanceComplete": linux_host_verified,
        "resourceIds": sorted(MATCH_RESOURCES),
        "excludedResourcesAbsent": True,
        "allFinalParticipantsReady": True,
        "craneTeam": "ibis",
        "simulatorIbisColor": "yellow",
        "sumatraTeamOption": "--aiBlue",
        "configured11003Publishers": publishers,
        "initialStopObservedByMatchController": initial_stop,
        "uniqueObservedSenderTupleVerified": True,
        "observedSenderTupleScope": "Datagrams observed by the current receiver epoch within the retained capture window.",
        "refereeSourceCapture": "referee-source-capture.json",
        "refereeSourceCaptureEpoch": capture["receiverEpoch"],
        "refereeSourceCaptureStartedAtUtc": capture["captureStartedAtUtc"],
        "refereeSourceCaptureEndedAtUtc": capture["captureEndedAtUtc"],
        "observed11003SourceTuples": capture["sources"],
        "uniqueOnWireSenderAttributionVerified": False,
        "unverifiedCriteria": [] if linux_host_verified else ["Native Linux AppHost acceptance."],
        "limitations": ["Unique observed tuples cover only the retained receiver window and do not identify the publishing process/container."],
        "gameControllerLogEvidence": required_log_evidence,
        "recorderPacketCounts": {
            "referee": int(counts.group(1)),
            "visionDetection": int(counts.group(2)),
            "tracker": int(counts.group(3)),
        },
        "matchControllerOutcome": {
            "status": result.get("status"),
            "stageReached": result.get("stageReached"),
            "terminationKind": result.get("terminationKind"),
        },
        "fullMatchPassed": result.get("status") == "passed",
    }
    _write_report(output_dir / "match-acceptance.json", report)
    # Save only the specific sanitized log lines used as evidence.
    from capture_aspire_dcp_logs import sanitize
    matched_lines = [line for line in gc_log.splitlines() if any(
        marker in line for marker in (
            '"type":"STOP"', '"type": "STOP"',
            "Team TIGERs Mannheim connected", "Team Client BLUE-TIGERs Mannheim connected",
        )
    )]
    (output_dir / "game-controller-evidence.log").write_text(sanitize("\n".join(matched_lines) + "\n"), encoding="utf-8")
    return report


def _write_report(path: Path, report: dict[str, Any]) -> None:
    from capture_aspire_dcp_logs import sanitize
    content = json.dumps(report, indent=2) + "\n"
    path.write_text(sanitize(content), encoding="utf-8")


def _finalize_archive(evidence_dir: Path, output_dir: Path) -> dict[str, Any]:
    report_path = output_dir / "match-acceptance.json"
    report = _load_json(report_path)
    if report is None:
        raise RuntimeError("No match acceptance evidence report was preserved before cleanup.")
    archives = sorted((evidence_dir / "recorder").glob("*.log.gz"))
    if len(archives) != 1:
        raise RuntimeError(f"Expected one finalized recorder archive after cleanup; found {len(archives)}.")
    archive = archives[0]
    digest = hashlib.sha256()
    with archive.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    decompressed_size = 0
    with gzip.open(archive, "rb") as source:
        while True:
            block = source.read(1024 * 1024)
            if not block:
                break
            decompressed_size += len(block)

    output_dir.mkdir(parents=True, exist_ok=True)
    copied = output_dir / archive.name
    shutil.copy2(archive, copied)
    report.update({
        "recorderArchive": copied.name,
        "recorderArchiveBytes": copied.stat().st_size,
        "recorderArchiveSha256": digest.hexdigest(),
        "recorderArchiveDecompressedBytes": decompressed_size,
        "recorderArchiveFinalizedAfterRecorderStop": True,
    })
    _write_report(report_path, report)
    return report


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--apphost-pid", type=int)
    parser.add_argument("--stack-id", required=True)
    parser.add_argument("--results-dir", type=Path, required=True)
    parser.add_argument("--finalize", action="store_true", help="Validate and preserve the recorder archive after cleanup.")
    parser.add_argument("--timeout-seconds", type=int, default=900)
    parser.add_argument("--poll-interval-seconds", type=float, default=2)
    args = parser.parse_args()
    if args.timeout_seconds <= 0 or args.poll_interval_seconds <= 0:
        parser.error("PID and timeout values must be positive")

    evidence_dir = Path(gettempdir()) / "duck-aspire-match-results" / args.stack_id
    if args.finalize:
        args.results_dir.mkdir(parents=True, exist_ok=True)
        try:
            report = _finalize_archive(evidence_dir, args.results_dir)
        except (RuntimeError, OSError, EOFError, gzip.BadGzipFile, json.JSONDecodeError) as error:
            failure = args.results_dir / "match-archive-verification-failure.txt"
            failure.write_text(f"Recorder archive verification failed: {error}\n", encoding="utf-8")
            print(f"MATCH_ARCHIVE_VERIFICATION_FAILURE: {error}", file=sys.stderr)
            return 1
        print(json.dumps(report, separators=(",", ":")), flush=True)
        return 0
    if args.apphost_pid is None or args.apphost_pid <= 0:
        parser.error("--apphost-pid is required unless --finalize is selected")

    result_path = evidence_dir / "match-result.json"
    deadline = time.monotonic() + args.timeout_seconds
    stable_since: float | None = None
    previous_size = None
    while time.monotonic() < deadline:
        try:
            os.kill(args.apphost_pid, 0)
        except OSError:
            _preserve_partial_evidence(evidence_dir, args.results_dir, args.stack_id)
            (args.results_dir / "match-acceptance-failure.txt").write_text(
                "Match AppHost exited before writing match-result.json.\n", encoding="utf-8")
            print("Match AppHost exited before writing match-result.json.", file=sys.stderr)
            return 1
        result = _load_json(result_path)
        size = result_path.stat().st_size if result is not None else None
        if result is not None and size == previous_size:
            stable_since = stable_since or time.monotonic()
            if time.monotonic() - stable_since >= 1:
                break
        else:
            stable_since = None
        previous_size = size
        time.sleep(args.poll_interval_seconds)
    if stable_since is None or time.monotonic() - stable_since < 1:
        _preserve_partial_evidence(evidence_dir, args.results_dir, args.stack_id)
        (args.results_dir / "match-acceptance-failure.txt").write_text(
            "Timed out or could not read a stable Match Controller result.\n", encoding="utf-8")
        print("Timed out waiting for Match Controller result.", file=sys.stderr)
        return 1
    try:
        report = _check_evidence(args.stack_id, evidence_dir, args.results_dir)
    except (RuntimeError, OSError, json.JSONDecodeError, KeyError, TypeError, EOFError, gzip.BadGzipFile) as error:
        _preserve_partial_evidence(evidence_dir, args.results_dir, args.stack_id)
        (args.results_dir / "match-acceptance-failure.txt").write_text(
            f"Match-mode runtime checks were incomplete: {error}\n", encoding="utf-8")
        print(f"MATCH_ACCEPTANCE_FAILURE: {error}", file=sys.stderr)
        return 1
    print(json.dumps(report, separators=(",", ":")), flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
