#!/usr/bin/env python3
"""Validate the exact Release publish directories used as Linux Docker contexts."""
import argparse
from pathlib import Path

REQUIRED_OUTPUTS = {
    "Tracker/Tracker.RuntimeHost": (
        "Tracker.RuntimeHost.dll",
        "Tracker.RuntimeHost.deps.json",
        "Tracker.RuntimeHost.runtimeconfig.json",
        "appsettings.json",
        "Dockerfile",
    ),
    "Testing/Duck.Testing.RefereeDriver.Host": (
        "Duck.Testing.RefereeDriver.Host.dll",
        "Duck.Testing.RefereeDriver.Host.deps.json",
        "Duck.Testing.RefereeDriver.Host.runtimeconfig.json",
        "Dockerfile",
    ),
}


def verify_contexts(repo_root: Path) -> None:
    missing = []
    for project, files in REQUIRED_OUTPUTS.items():
        project_root = repo_root / project
        publish_root = project_root / "bin" / "Release" / "net10.0" / "publish"
        for relative in files:
            path = (project_root / relative) if relative == "Dockerfile" else (publish_root / relative)
            if not path.is_file() or path.stat().st_size == 0:
                missing.append(str(path.relative_to(repo_root)))
    if missing:
        raise RuntimeError("Missing or empty Docker context inputs: " + ", ".join(missing))


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", required=True, type=Path)
    args = parser.parse_args()
    try:
        verify_contexts(args.repo_root.resolve())
    except (OSError, RuntimeError) as error:
        print(f"MATCH_PUBLISH_CONTEXT_FAILURE: {error}")
        return 1
    print("Duck RuntimeHost and RefereeDriver Host Docker publish contexts are complete.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
