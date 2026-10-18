#!/usr/bin/env python3
"""Capture bounded, sanitized DCP logs from Aspire's temporary log directories."""

from __future__ import annotations

import argparse
from collections import deque
from datetime import datetime, timezone
import os
from pathlib import Path
import re
import sys

MAX_BYTES = 512 * 1024
TAIL_LINES = 500
SECRET_FIELD = re.compile(
    r"(?ix)"
    r"(?P<key>['\"]?[A-Za-z0-9_.-]*(?:token|password|passwd|secret|credential|private[_-]?key|client[_-]?key|api[_-]?key|access[_-]?key|authorization)[A-Za-z0-9_.-]*['\"]?)"
    r"(?P<separator>\s*[:=]\s*)"
    r"(?P<value>\"(?:\\.|[^\"\\])*\"|'(?:''|\\.|[^'])*'|[^\s,;}\]]+)",
    re.S,
)
BEARER = re.compile(r"(?i)\bBearer\s+[A-Za-z0-9._~+/-]+=*")
PRIVATE_KEY_BLOCK = re.compile(
    r"-----BEGIN [^-]*PRIVATE KEY-----.*?-----END [^-]*PRIVATE KEY-----",
    re.S,
)
DASHBOARD_LOGIN_TOKEN = re.compile(r"(?i)([?&]t=)[A-Za-z0-9._~+/-]+={0,2}")


def _excluded(path: Path) -> bool:
    name = path.name.lower()
    if any(term in name for term in ("kubeconfig", "token", "secret", "credential")):
        return True
    return path.suffix.lower() in {".key", ".pem", ".p12", ".pfx", ".crt", ".cert"}


def _redact_value(match: re.Match[str]) -> str:
    value = match.group("value")
    if value.startswith('"'):
        replacement = '"[REDACTED]"'
    elif value.startswith("'"):
        replacement = "'[REDACTED]'"
    else:
        replacement = "[REDACTED]"
    return f"{match.group('key')}{match.group('separator')}{replacement}"


def sanitize(text: str) -> str:
    text = PRIVATE_KEY_BLOCK.sub("[REDACTED PRIVATE KEY BLOCK]", text)
    text = BEARER.sub("Bearer [REDACTED]", text)
    text = DASHBOARD_LOGIN_TOKEN.sub(r"\1[REDACTED]", text)
    return SECRET_FIELD.sub(_redact_value, text)


def _tail(path: Path) -> str:
    lines: deque[bytes] = deque(maxlen=TAIL_LINES)
    with path.open("rb") as stream:
        lines.extend(stream)
    content = b"".join(lines)[-MAX_BYTES:]
    return sanitize(content.decode("utf-8", errors="replace"))


def capture(output_dir: Path, roots: list[Path], phase: str) -> int:
    output_dir.mkdir(parents=True, exist_ok=True)
    manifest = output_dir / "dcp-log-paths.txt"
    tails = output_dir / "dcp-log-tails.txt"
    status = output_dir / "dcp-log-capture-status.txt"
    captured: list[Path] = []

    for root in roots:
        if not root.is_dir() or root.is_symlink():
            continue
        for directory, subdirectories, filenames in os.walk(root, topdown=True, followlinks=False):
            current = Path(directory)
            depth = len(current.relative_to(root).parts)
            subdirectories[:] = [
                name for name in subdirectories if not (current / name).is_symlink()
            ]
            if depth >= 8:
                subdirectories.clear()
            for filename in filenames:
                path = current / filename
                if path.is_symlink() or path.suffix.lower() != ".log" or _excluded(path):
                    continue
                captured.append(path)

    captured.sort(key=lambda item: str(item))
    with manifest.open("w", encoding="utf-8") as manifest_stream, tails.open(
        "w", encoding="utf-8"
    ) as tail_stream:
        for path in captured:
            stat = path.stat()
            manifest_stream.write(
                f"{path} {stat.st_size} bytes "
                f"{datetime.fromtimestamp(stat.st_mtime, timezone.utc).isoformat()}\n"
            )
            tail_stream.write(f"\n### {path} (last {TAIL_LINES} lines, capped at {MAX_BYTES} bytes)\n")
            try:
                tail_stream.write(_tail(path))
            except OSError as error:
                tail_stream.write(f"[Could not read eligible log: {error.__class__.__name__}]\n")
            tail_stream.write("\n")

    status.write_text(
        f"captured_log_files={len(captured)}\n"
        f"captured_at_utc={datetime.now(timezone.utc).isoformat()}\n"
        f"phase={phase}\n"
        f"sensitive_files_excluded=true\n"
        f"log_tails_sanitized=true\n",
        encoding="utf-8",
    )
    return len(captured)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("output_dir", type=Path)
    parser.add_argument("--phase", default="unspecified")
    parser.add_argument("roots", nargs="*", type=Path)
    args = parser.parse_args()

    roots = args.roots or sorted(Path("/tmp").glob("aspire-dcp*"))
    count = capture(args.output_dir, roots, args.phase)
    if count == 0:
        print("No eligible DCP .log files found.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
