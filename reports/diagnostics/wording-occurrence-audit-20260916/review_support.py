"""Record explicit contextual decisions and precise sentence edits; no automatic translation."""
from __future__ import annotations
import datetime, hashlib, json
from pathlib import Path
ROOT = Path.cwd()
OUT = ROOT / 'reports/diagnostics/wording-occurrence-audit-20260916'
BLOCKS = json.loads((OUT / 'changed-blocks-start.json').read_text())
def apply_edits(batch: str, edits: list[dict]) -> None:
    evidence = []
    for edit in edits:
        path = ROOT / edit['file']
        before = path.read_text()
        old, new = edit['old'], edit['new']
        if before.count(old) != 1:
            raise ValueError(f"Expected one exact occurrence in {edit['file']}: {old!r}")
        line = before[:before.index(old)].count('\n') + 1
        after = before.replace(old, new, 1)
        path.write_text(after)
        evidence.append({**edit, 'line_before_edit': line,
            'before_sha256': hashlib.sha256(before.encode()).hexdigest(),
            'after_sha256': hashlib.sha256(after.encode()).hexdigest()})
    (OUT / f'edits-{batch}.json').write_text(json.dumps(evidence, ensure_ascii=False, indent=2) + '\n')
def record_reviews(doc_index: int, decisions: dict[int, tuple[str,str]]) -> None:
    available = {b['id']: b for b in BLOCKS if b['id'].startswith(f'D{doc_index:02d}-')}
    records = []
    for number, (disposition, reason) in decisions.items():
        bid = f'D{doc_index:02d}-B{int(number):03d}'
        if bid not in available:
            raise ValueError(bid)
        records.append({'block': bid, 'file': available[bid]['file'],
            'disposition': disposition, 'reason': reason,
            'method': '変更前後の文章と前後各2行以上を読んだ文脈確認。用語検索だけによる自動採否ではない。',
            'reviewed_at': datetime.datetime.now().astimezone().isoformat()})
    (OUT / f'decisions-doc-{doc_index:02d}.json').write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n')
