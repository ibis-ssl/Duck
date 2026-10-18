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
COMPARISON_RESOURCES = {
    "tracker-tigers": 39105,
    "tracker-erforce": 39106,
}
COMPARISON_HTTP_ENDPOINTS = {
    "debug-host-comparison": (39107, "/health/comparison"),
    "runtimehost-vision": (39108, "/health/vision"),
    "debughost-vision": (39109, "/health/vision"),
}


@dataclass(frozen=True)
class ReadinessResult:
    status: str
    failure_category: str | None
    reason: str | None
    polls: list[dict[str, object]]
    comparison_vision_count_window: dict[str, int | str] | None = None


def probe_health(port: int, timeout_seconds: float, path: str = "/health/ready") -> int | str:
    """Return the HTTP status only; health response bodies are not needed."""
    try:
        with urlopen(f"http://127.0.0.1:{port}{path}", timeout=timeout_seconds) as response:
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


def verify_owned_stack(stack_id: str, run=subprocess.run, comparison: bool = False) -> tuple[bool, str | None]:
    """Require exactly one live, correctly labelled host-network container per wrapper."""
    resources = {**REQUIRED_RESOURCES, **(COMPARISON_RESOURCES if comparison else {})}
    for resource in resources:
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
    probe: Callable[..., int | str] = probe_health,
    is_alive: Callable[[int], bool] = process_is_alive,
    verify_stack: Callable[..., tuple[bool, str | None]] | None = None,
    comparison: bool = False,
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
    required_resources = {**REQUIRED_RESOURCES, **(COMPARISON_RESOURCES if comparison else {})}
    http_endpoints = COMPARISON_HTTP_ENDPOINTS if comparison else {}
    polls: list[dict[str, object]] = []
    while True:
        apphost_alive = is_alive(apphost_pid)
        statuses = {name: probe(port, request_timeout_seconds) for name, port in required_resources.items()}
        statuses.update({
            name: probe(port, request_timeout_seconds, path)
            for name, (port, path) in http_endpoints.items()
        })
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
                if comparison:
                    stack_valid, stack_reason = verify_stack(stack_id, True)
                else:
                    stack_valid, stack_reason = verify_stack(stack_id)
                polls[-1]["owned_stack_valid"] = stack_valid
                if not stack_valid:
                    return ReadinessResult(
                        "failed",
                        "owned_stack_mismatch",
                        stack_reason or "Current AppHost stack ownership could not be verified.",
                        polls,
                    )
            comparison_counts = None
            if comparison:
                before = {key: _read_vision_count(port, request_timeout_seconds) for key, (port, _) in http_endpoints.items() if key != "debug-host-comparison"}
                sleep(5)
                after = {key: _read_vision_count(port, request_timeout_seconds) for key, (port, _) in http_endpoints.items() if key != "debug-host-comparison"}
                comparison_counts = {}
                for name in before:
                    comparison_counts[f"{name}_before"] = before[name]  # type: ignore[assignment]
                    comparison_counts[f"{name}_after"] = after[name]  # type: ignore[assignment]
                final_apphost_alive = is_alive(apphost_pid)
                final_statuses = {
                    name: probe(port, request_timeout_seconds)
                    for name, port in required_resources.items()
                }
                final_statuses.update({
                    name: probe(port, request_timeout_seconds, path)
                    for name, (port, path) in http_endpoints.items()
                })
                final_poll: dict[str, object] = {
                    "observed_at_utc": datetime.now(timezone.utc).isoformat(),
                    "phase": "comparison_window_end",
                    "apphost_alive": final_apphost_alive,
                    "resources": final_statuses,
                }
                polls.append(final_poll)
                stack_valid = True
                stack_reason = None
                if verify_stack is not None:
                    stack_valid, stack_reason = verify_stack(stack_id, True) if stack_id else (False, "Stack ID is required.")
                    final_poll["owned_stack_valid"] = stack_valid
                if not final_apphost_alive:
                    return ReadinessResult(
                        "failed",
                        "apphost_exited",
                        "AppHost exited during the comparison Vision receive-count window.",
                        polls,
                        comparison_counts,
                    )
                unavailable = [f"{name}:{status}" for name, status in final_statuses.items() if status != 200]
                if unavailable:
                    return ReadinessResult(
                        "failed",
                        "comparison_readiness_interrupted",
                        "Comparison readiness endpoints stopped reporting HTTP 200 during the Vision receive-count window: "
                        + ", ".join(unavailable),
                        polls,
                        comparison_counts,
                    )
                if not stack_valid:
                    return ReadinessResult(
                        "failed",
                        "owned_stack_mismatch",
                        stack_reason or "Current AppHost stack ownership could not be verified after the comparison window.",
                        polls,
                        comparison_counts,
                    )
                if any(not isinstance(before[name], int) or not isinstance(after[name], int) or after[name] <= before[name] for name in before):
                    return ReadinessResult(
                        "failed", "comparison_vision_count_stalled",
                        "RuntimeHost and DebugHost SSL-Vision receive counts did not both increase in the same five-second window.",
                        polls,
                        comparison_counts,
                    )
            return ReadinessResult("ready", None, None, polls, comparison_counts)

        now = clock()
        if now >= deadline:
            pending = [
                f"{name}:{statuses[name]}"
                for name in statuses
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
    parser.add_argument("--comparison", action="store_true", help="Require comparison resources and a shared-window Vision receive-count increase.")
    args = parser.parse_args()

    result = wait_for_readiness(
        args.apphost_pid,
        verify_stack=lambda stack, comparison=args.comparison: verify_owned_stack(stack, comparison=comparison),
        stack_id=args.stack_id,
        comparison=args.comparison,
        timeout_seconds=args.timeout_seconds,
        poll_interval_seconds=args.poll_interval_seconds,
    )
    report = {
        "required_resources": {**REQUIRED_RESOURCES, **(COMPARISON_RESOURCES if args.comparison else {})},
        "comparison_http_endpoints": COMPARISON_HTTP_ENDPOINTS if args.comparison else {},
        **asdict(result),
    }
    args.report_file.parent.mkdir(parents=True, exist_ok=True)
    args.report_file.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    for number, poll in enumerate(result.polls, start=1):
        resources = poll["resources"]
        details = " ".join(f"{name}={status}" for name, status in resources.items())
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

    print("Required AppHost resources and runtime packet readiness confirmed.", flush=True)
    return 0


def _read_vision_count(port: int, timeout_seconds: float) -> int | str:
    try:
        with urlopen(f"http://127.0.0.1:{port}/health/vision", timeout=timeout_seconds) as response:
            payload = json.loads(response.read().decode("utf-8"))
        value = payload.get("VisionPacketsReceived", payload.get("visionPacketsReceived"))
        return value if isinstance(value, int) else "invalid"
    except (HTTPError, URLError, TimeoutError, socket.timeout, OSError, ValueError, json.JSONDecodeError):
        return "unreachable"


if __name__ == "__main__":
    raise SystemExit(main())
