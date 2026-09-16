"""Apply only explicitly supplied, context-reviewed sentence edits."""
from __future__ import annotations
import json
import sys
from pathlib import Path
from review_support import apply_edits
if len(sys.argv) != 3:
    raise SystemExit('Usage: python apply_batch.py <batch-name> <reviewed-edits.json>')
batch, source = sys.argv[1:]
edits = json.loads(Path(source).read_text())
if not isinstance(edits, list) or not all(isinstance(e, dict) for e in edits):
    raise SystemExit('Expected a list of explicit edits.')
apply_edits(batch, edits)
print(f'Applied {len(edits)} context-reviewed edits; evidence saved for {batch}.')
