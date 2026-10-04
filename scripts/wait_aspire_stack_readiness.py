#!/usr/bin/env python3
"""Wait until every required wrapper in the full-stack AppHost reports ready."""

from __future__ import annotations

import argparse
from dataclasses import asdict, dataclass
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import socket
import subprocess
import sys
import time
from typing import Callable
from urllib.error import HTTPError, URLError
from urllib.request import urlopen


REQUIRED_RESOURCES = {
    "simulator": 39101,
    "game-controller": 39102,
    "cm4-sim": 39103,
    "crane": 39104,
}


@dataclass(frozen=True)
class ReadinessResult:
    status: str
    failure_category: str | None
    reason: str | None
    polls: list[dict[str, object]]


def probe_health(port: int, timeout_seconds: float) -> int | str:
    """Return the HTTP status only; health response bodies are not needed."""
    try:
        with urlopen(f"http://127.0.0.1:{port}/health/ready", timeout=timeout_seconds) as response:
            return response.status
    except HTTPError as error:
        return error.code
    except (URLError, TimeoutError, socket.timeout, OSError):
        return "unreachable"


def process_is_alive(pid: int) -> bool:
    try:
        os.kill(pid, 0)
        return True
    except OSError:
        return False


def verify_owned_stack(stack_id: str, run=subprocess.run) -> tuple[bool, str | None]:
    """Require exactly one live, correctly labelled host-network container per wrapper."""
    for resource in REQUIRED_RESOURCES:
        listed = run(
            [
                "docker", "ps", "-aq", "--no-trunc",
                "--filter", "label=duck.aspire.owner=duck-apphost",
                "--filter", f"label=duck.aspire.stack={stack_id}",
                "--filter", f"label=duck.aspire.resource={resource}",
            ],
            capture_output=True,
            text=True,
            check=False,
        )
        if listed.returncode != 0:
            return False, f"Could not list owned {resource} containers."
        container_ids = [line.strip() for line in listed.stdout.splitlines() if line.strip()]
        if len(container_ids) != 1:
            return False, f"Expected one owned {resource} container; found {len(container_ids)}."

        inspected = run(
            ["docker", "inspect", container_ids[0]],
            capture_output=True,
            text=True,
            check=False,
        )
        if inspected.returncode != 0:
            return False, f"Could not inspect owned {resource} container."
        try:
            container = json.loads(inspected.stdout)[0]
            labels = container["Config"]["Labels"] or {}
            correct = (
                labels.get("duck.aspire.owner") == "duck-apphost"
                and labels.get("duck.aspire.stack") == stack_id
                and labels.get("duck.aspire.resource") == resource
                and bool(labels.get("duck.aspire.run"))
                and container["Name"] == f"/duck-{stack_id}-{resource}"
                and container["HostConfig"]["NetworkMode"] == "host"
            )
            running = container["State"]["Running"] is True
        except (IndexError, KeyError, TypeError, json.JSONDecodeError):
            return False, f"Could not validate owned {resource} container metadata."
        if not correct:
            return False, f"Owned {resource} container metadata did not match this stack."
        if not running:
            return False, f"Owned {resource} container is not running."
    return True, None


def wait_for_readiness(
    apphost_pid: int,
    *,
    timeout_seconds: float = 300,
    poll_interval_seconds: float = 2,
    request_timeout_seconds: float = 2,
    probe: Callable[[int, float], int | str] = probe_health,
    is_alive: Callable[[int], bool] = process_is_alive,
    verify_stack: Callable[[str], tuple[bool, str | None]] | None = None,
    stack_id: str | None = None,
    clock: Callable[[], float] = time.monotonic,
    sleep: Callable[[float], None] = time.sleep,
) -> ReadinessResult:
    """Require every readiness endpoint to return HTTP 200 in one poll cycle."""
    if apphost_pid <= 0:
        raise ValueError("AppHost PID must be positive.")
    if timeout_seconds <= 0 or poll_interval_seconds <= 0 or request_timeout_seconds <= 0:
        raise ValueError("Readiness timeouts must be positive.")

    deadline = clock() + timeout_seconds
    polls: list[dict[str, object]] = []
    while True:
        apphost_alive = is_alive(apphost_pid)
        statuses = {
            name: probe(port, request_timeout_seconds)
            for name, port in REQUIRED_RESOURCES.items()
        }
        polls.append(
            {
                "observed_at_utc": datetime.now(timezone.utc).isoformat(),
                "apphost_alive": apphost_alive,
                "resources": statuses,
            }
        )

        if not apphost_alive:
            return ReadinessResult(
                "failed",
                "apphost_exited",
                "AppHost exited before all required wrapper readiness endpoints returned HTTP 200.",
                polls,
            )
        if all(status == 200 for status in statuses.values()):
            if verify_stack is not None:
                if not stack_id:
                    raise ValueError("Stack ID is required when verifying owned containers.")
                stack_valid, stack_reason = verify_stack(stack_id)
                polls[-1]["owned_stack_valid"] = stack_valid
                if not stack_valid:
                    return ReadinessResult(
                        "failed",
                        "owned_stack_mismatch",
                        stack_reason or "Current AppHost stack ownership could not be verified.",
                        polls,
                    )
            return ReadinessResult("ready", None, None, polls)

        now = clock()
        if now >= deadline:
            pending = [
                f"{name}:{statuses[name]}"
                for name in REQUIRED_RESOURCES
                if statuses[name] != 200
            ]
            return ReadinessResult(
                "failed",
                "wrapper_readiness_timeout",
                "Timed out waiting for required wrapper readiness: " + ", ".join(pending),
                polls,
            )
        sleep(min(poll_interval_seconds, deadline - now))


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apphost-pid", type=int, required=True)
    parser.add_argument("--report-file", type=Path, required=True)
    parser.add_argument("--stack-id", required=True)
    parser.add_argument("--timeout-seconds", type=float, default=300)
    parser.add_argument("--poll-interval-seconds", type=float, default=2)
    args = parser.parse_args()

    result = wait_for_readiness(
        args.apphost_pid,
        verify_stack=verify_owned_stack,
        stack_id=args.stack_id,
        timeout_seconds=args.timeout_seconds,
        poll_interval_seconds=args.poll_interval_seconds,
    )
    report = {
        "required_resources": REQUIRED_RESOURCES,
        **asdict(result),
    }
    args.report_file.parent.mkdir(parents=True, exist_ok=True)
    args.report_file.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    for number, poll in enumerate(result.polls, start=1):
        resources = poll["resources"]
        details = " ".join(f"{name}={resources[name]}" for name in REQUIRED_RESOURCES)
        print(
            f"readiness_poll={number} apphost_alive={str(poll['apphost_alive']).lower()} {details}",
            flush=True,
        )
    if result.status != "ready":
        print(
            f"STARTUP_READINESS_FAILURE category={result.failure_category} reason={result.reason}",
            file=sys.stderr,
            flush=True,
        )
        return 1

    print("Full-stack wrapper readiness confirmed.", flush=True)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
