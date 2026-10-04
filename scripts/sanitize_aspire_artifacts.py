#!/usr/bin/env python3
"""Sanitize every text artifact before upload; fail closed on read/write errors."""

from __future__ import annotations

import argparse
import os
from pathlib import Path
import tempfile

import json
from capture_aspire_dcp_logs import SECRET_FIELD, sanitize


def sanitize_json_value(value, sanitizer):
    if isinstance(value, str):
        return sanitizer(value)
    if isinstance(value, list):
        return [sanitize_json_value(item, sanitizer) for item in value]
    if isinstance(value, dict):
        return {
            key: "[REDACTED]" if SECRET_FIELD.fullmatch(f'{key}=""')
            else sanitize_json_value(item, sanitizer)
            for key, item in value.items()
        }
    return value


def sanitize_jsonl(content, sanitizer):
    # Decode strings first; regex over escaped JSON can erase record boundaries.
    records = [json.loads(line) for line in content.splitlines() if line.strip()]
    return "".join(json.dumps(sanitize_json_value(record, sanitizer), ensure_ascii=True) + "\n"
                   for record in records)


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
            redacted = sanitize_jsonl(content, sanitizer) if path.suffix.lower() == ".jsonl" else sanitizer(content)
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
