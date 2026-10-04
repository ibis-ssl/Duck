#!/usr/bin/env python3
"""Sanitize every text artifact before upload; fail closed on read/write errors."""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import tempfile

from capture_aspire_dcp_logs import sanitize


def sanitize_tree(root: Path, sanitizer=sanitize) -> list[Path]:
    failures: list[Path] = []
    if not root.is_dir() or root.is_symlink():
        return [root]

    for path in sorted(root.rglob("*")):
        if path.is_symlink() or not path.is_file():
            continue
        temporary_path: Path | None = None
        try:
            content = path.read_text(encoding="utf-8", errors="replace")
            redacted = sanitizer(content)
            with tempfile.NamedTemporaryFile(
                mode="w", encoding="utf-8", newline="", dir=path.parent, delete=False
            ) as temporary:
                temporary.write(redacted)
                temporary_path = Path(temporary.name)
            os.replace(temporary_path, path)
        except Exception:
            failures.append(path)
            if temporary_path is not None:
                temporary_path.unlink(missing_ok=True)
    return failures


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("artifact_root", type=Path)
    args = parser.parse_args()
    failures = sanitize_tree(args.artifact_root)
    if failures:
        for path in failures:
            print(f"sanitization_failed={path}")
        return 1
    print("sanitization_success=true")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
