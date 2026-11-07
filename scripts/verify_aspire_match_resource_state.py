#!/usr/bin/env python3
"""Fail-closed terminal-state check for the Linux match acceptance stack."""
import argparse
import json
from pathlib import Path


def verify_state(mode: str, resource: str, state: str, acceptance_report: Path | None) -> None:
    if state == "running":
        return
    if not (mode == "match" and resource == "match-controller" and state == "exited"):
        raise RuntimeError(f"Owned {resource} container entered state {state}; only a verified match-controller may exit.")
    if acceptance_report is None or not acceptance_report.is_file():
        raise RuntimeError("Exited match-controller has no current acceptance report.")
    report = json.loads(acceptance_report.read_text(encoding="utf-8"))
    if not (
        report.get("status") == "passed"
        and report.get("linuxHostVerified") is True
        and report.get("issue51AcceptanceComplete") is True
    ):
        raise RuntimeError("Exited match-controller lacks passed current-run Linux G1 acceptance evidence.")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--mode", required=True)
    parser.add_argument("--resource", required=True)
    parser.add_argument("--state", required=True)
    parser.add_argument("--acceptance-report", type=Path)
    args = parser.parse_args()
    try:
        verify_state(args.mode, args.resource, args.state, args.acceptance_report)
    except (OSError, ValueError, RuntimeError) as error:
        print(f"MATCH_RESOURCE_STATE_FAILURE: {error}")
        return 1
    print(f"resource={args.resource} state={args.state} accepted=true")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
