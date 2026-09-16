"""Attach positions to retained human judgments; never generate semantic approvals."""
import collections, hashlib, json, re, subprocess
from pathlib import Path
ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
AUDIT = ROOT / 'reports/diagnostics/wording-occurrence-audit-20260916'
judgments = json.loads((OUT / 'engine-line-judgments.json').read_text())
file = judgments['file']
before = subprocess.check_output(['git', 'show', judgments['baseline_sha'] + ':' + file], cwd=ROOT, text=True).splitlines()
final = (ROOT / file).read_text().splitlines()
assert hashlib.sha256((ROOT / file).read_bytes()).hexdigest() == judgments['final_sha256']
reasons = {r['before_line']: r for r in judgments['judgments']}
occurrences = [r for r in json.loads((AUDIT / 'occurrences-start.json').read_text()) if r['file'] == file]
rows = []
for source in occurrences:
    n = source['before_line']; old = before[n - 1]; judgment = reasons[n]
    target_line = judgment['final_line']; text = final[target_line - 1]
    surface = source['surface']; column = source['before_column']
    assert old[column - 1:column - 1 + len(surface)] == surface, source['id']
    expression = {'event-time': 'event time', 'profile switch': '設定プロファイルの切り替え', 'id': 'ID', 'uuid': 'UUID'}.get(surface, surface)
    if surface == 'Kalman' and n != 207: expression = 'Kalman filter'
    if source['id'] == 'O001759': expression = 'XML documentation comment'
    ordinal = sum(1 for o in occurrences if o['before_line'] == n and o['surface'].lower() == surface.lower() and o['before_column'] < column)
    if source['id'] == 'O001703': ordinal = 1  # Original settings clause, not the newly added orientation explanation.
    pattern = re.escape(expression)
    if expression.isascii(): pattern = r'(?<![A-Za-z0-9_])' + pattern + r'(?![A-Za-z0-9_])'
    candidates = list(re.finditer(pattern, text))
    assert len(candidates) > ordinal, (source['id'], expression, ordinal, text)
    match = candidates[ordinal]
    disposition = 'maintained' if expression == surface else 'natural_japanese_or_explicit_technical_name'
    rows.append({
        'id': source['id'], 'source_entry_index': source['entry_index'], 'source_term': source['term'],
        'source_surface': surface, 'baseline_sha': source['baseline_sha'], 'baseline_file': file,
        'before_line': n, 'before_column': column, 'before_text': old,
        'before_context': {'start_line': max(1, n - 2), 'lines': before[max(0, n - 3):n + 2]},
        'final_file': file, 'final_sha256': judgments['final_sha256'], 'final_line': target_line,
        'final_column': match.start() + 1, 'final_end_column': match.end(), 'final_expression': match.group(),
        'final_text': text, 'final_context': {'start_line': max(1, target_line - 2), 'lines': final[max(0, target_line - 3):target_line + 2]},
        'disposition': disposition, 'reason': judgment['reason'],
        'source_change_block': source.get('block'), 'line_text_unchanged': old == text,
        'evidence': ['engine-line-judgments.json', 'engine-summary-correction.json' if n == 314 else 'engine-contract-checks.json'],
        'review_kind': 'implementation_self_check',
    })
assert len({r['id'] for r in rows}) == len(rows) == 133
assert set(reasons) == {r['before_line'] for r in rows}
assert all(r['final_text'][r['final_column'] - 1:r['final_end_column']] == r['final_expression'] for r in rows)
result = {'schema_version': 1, 'document': file, 'scope': 'Only 133 baseline occurrences in this document, not the complete 4336-occurrence PR ledger.',
          'baseline_sha': judgments['baseline_sha'], 'technical_parent': judgments['initial_head'],
          'final_sha256': judgments['final_sha256'], 'line_mapping': 'Manually checked baseline line +2 after the introductory definition; no paragraph moves in this document.',
          'count': len(rows), 'unresolved': 0, 'rows': rows}
(OUT / 'engine-occurrence-correspondence.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
print(json.dumps({'rows': len(rows), 'source_lines': len(reasons), 'unchanged_line_occurrences': sum(r['line_text_unchanged'] for r in rows), 'dispositions': dict(collections.Counter(r['disposition'] for r in rows))}, ensure_ascii=False))
