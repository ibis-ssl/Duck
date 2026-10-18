#!/usr/bin/env python3
"""Capture direct SSL-Vision receive syscalls by both comparison tracker processes."""

from __future__ import annotations

import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import re
import signal
import subprocess
import sys
import time

from wait_aspire_stack_readiness import read_comparison_health, verify_owned_stack


TARGETS = {
    "tracker-tigers": ("java", "TIGERs"),
    "tracker-erforce": ("autoref-cli", "ER-Force"),
}
VISION_SOCKET = re.compile(r"UDP:\[[^]]*:10020\]")
SUCCESSFUL_RECEIVE = re.compile(r"\)\s+=\s+([1-9][0-9]*)\s*$")


def run(command: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(command, capture_output=True, text=True, check=False)


def owned_container(stack_id: str, resource: str) -> str:
    result = run([
        "docker", "ps", "-q", "--no-trunc",
        "--filter", "label=duck.aspire.owner=duck-apphost",
        "--filter", f"label=duck.aspire.stack={stack_id}",
        "--filter", f"label=duck.aspire.resource={resource}",
    ])
    ids = [line.strip() for line in result.stdout.splitlines() if line.strip()]
    if result.returncode != 0 or len(ids) != 1:
        raise RuntimeError(f"Expected exactly one owned {resource} container; found {len(ids)}.")
    return ids[0]


def tracker_pid(container_id: str, process_name: str) -> tuple[int, str]:
    result = run(["docker", "top", container_id, "-eo", "pid,comm,args"])
    if result.returncode != 0:
        raise RuntimeError("Could not inspect tracker process list.")
    for line in result.stdout.splitlines()[1:]:
        fields = line.strip().split(None, 2)
        if len(fields) >= 2 and fields[0].isdigit() and process_name in fields[1]:
            return int(fields[0]), process_name
    raise RuntimeError(f"Could not find expected {process_name} process in owned tracker container.")


def count_vision_receives(trace_path: Path) -> int:
    count = 0
    for line in trace_path.read_text(encoding="utf-8", errors="replace").splitlines():
        if VISION_SOCKET.search(line) and SUCCESSFUL_RECEIVE.search(line):
            count += 1
    return count


def capture(report_path: Path, stack_id: str, window_seconds: float) -> int:
    report: dict[str, object] = {
        "stack_id": stack_id,
        "captured_at_utc": datetime.now(timezone.utc).isoformat(),
        "observation_window_seconds": window_seconds,
        "evidence_method": "strace successful recvfrom/recvmsg/recvmmsg calls on each tracker PID's local UDP port 10020; payloads are not captured",
        "processes": {},
    }
    report_path.parent.mkdir(parents=True, exist_ok=True)
    processes: dict[str, dict[str, object]] = {}
    traces: dict[str, Path] = {}
    tracers: dict[str, subprocess.Popen[str]] = {}
    try:
        valid, reason = verify_owned_stack(stack_id, comparison=True)
        if not valid:
            raise RuntimeError(reason or "Owned stack validation failed.")
        for resource, (process_name, role) in TARGETS.items():
            container_id = owned_container(stack_id, resource)
            pid, executable = tracker_pid(container_id, process_name)
            trace_path = report_path.parent / f"{resource}.vision-receive.strace.txt"
            processes[role] = {"resource": resource, "pid": pid, "executable": executable}
            traces[role] = trace_path

        health_before = read_comparison_health(39107, 2)
        if health_before.get("status") != 200:
            raise RuntimeError("Comparison health endpoint was unavailable at input-capture window start.")

        for role, process in processes.items():
            tracer = subprocess.Popen(
                ["sudo", "-n", "strace", "-f", "-yy", "-s", "0", "-e",
                 "trace=recvfrom,recvmsg,recvmmsg", "-p", str(process["pid"]), "-o", str(traces[role])],
                stdout=subprocess.DEVNULL,
                stderr=subprocess.PIPE,
                text=True,
            )
            tracers[role] = tracer
        time.sleep(0.25)
        for role, tracer in tracers.items():
            if tracer.poll() is not None:
                error = (tracer.stderr.read() if tracer.stderr else "").strip()
                raise RuntimeError(f"Could not attach strace to the {role} process: {error or 'strace exited'}")

        started_at = datetime.now(timezone.utc).isoformat()
        time.sleep(window_seconds)
        ended_at = datetime.now(timezone.utc).isoformat()
        for tracer in tracers.values():
            tracer.send_signal(signal.SIGINT)
        errors = []
        for role, tracer in tracers.items():
            try:
                tracer.wait(timeout=5)
            except subprocess.TimeoutExpired:
                tracer.kill()
                tracer.wait(timeout=2)
            if tracer.returncode not in (0, -signal.SIGINT, 130):
                errors.append(role)
        health_after = read_comparison_health(39107, 2)
        report["window_started_at_utc"] = started_at
        report["window_ended_at_utc"] = ended_at
        report["comparison_health_before"] = health_before
        report["comparison_health_after"] = health_after
        report["processes"] = {
            role: {**process, "successful_vision_receive_calls": count_vision_receives(traces[role]),
                   "trace_file": traces[role].name}
            for role, process in processes.items()
        }
        expected_roles = {"Duck", "TIGERs", "ER-Force"}
        health_body = health_after.get("body")
        roles = {entry.get("LogicalRole") or entry.get("logicalRole")
                 for entry in health_body.get("Sources", health_body.get("sources", []))} if isinstance(health_body, dict) else set()
        passed = (
            health_after.get("status") == 200
            and expected_roles.issubset(roles)
            and not errors
            and all(item["successful_vision_receive_calls"] > 0 for item in report["processes"].values())
        )
        report["status"] = "passed" if passed else "failed"
        if errors:
            report["reason"] = "One or more strace processes exited unsuccessfully."
        elif not expected_roles.issubset(roles):
            report["reason"] = "Comparison health response did not report all three logical roles after the capture window."
        elif not all(item["successful_vision_receive_calls"] > 0 for item in report["processes"].values()):
            report["reason"] = "At least one tracker process had no observed successful receive syscall on UDP/10020."
        return 0 if passed else 1
    except (OSError, RuntimeError, subprocess.SubprocessError) as error:
        report["status"] = "failed"
        report["reason"] = str(error)
        return_code = 1
    finally:
        for tracer in tracers.values():
            if tracer.poll() is None:
                tracer.send_signal(signal.SIGINT)
                try:
                    tracer.wait(timeout=5)
                except subprocess.TimeoutExpired:
                    tracer.kill()
        report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    return return_code


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--stack-id", required=True)
    parser.add_argument("--report-file", type=Path, required=True)
    parser.add_argument("--window-seconds", type=float, default=5)
    args = parser.parse_args()
    if args.window_seconds <= 0:
        parser.error("--window-seconds must be positive")
    return capture(args.report_file, args.stack_id, args.window_seconds)


if __name__ == "__main__":
    raise SystemExit(main())
