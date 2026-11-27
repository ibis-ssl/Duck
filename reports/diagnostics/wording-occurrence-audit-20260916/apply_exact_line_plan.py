"""Apply explicitly reviewed line-scoped edits, including repeated identical lines.

No translation or semantic approval is inferred. Every exact line and its full
paragraph before/after is recorded. All plans are checked before any file write.
"""
from __future__ import annotations
import hashlib, json, sys
from pathlib import Path
if len(sys.argv) != 3:
    raise SystemExit('Usage: apply_exact_line_plan.py <batch> <reviewed-plan.json>')
batch, source = sys.argv[1:]
root = Path.cwd().resolve()
out = root/'reports/diagnostics/wording-occurrence-audit-20260916'
plan = json.loads(Path(source).read_text())
original, modified, reasons, source_bytes = {}, {}, {}, {}
for rule in plan:
    file = rule['file']
    path = (root/file).resolve()
    if not path.is_relative_to(root) or path.suffix != '.md':
        raise ValueError('Only current-worktree Markdown files are permitted')
    if file not in original:
        source_bytes[file] = path.read_bytes()
        original[file] = source_bytes[file].decode().splitlines(keepends=True)
        modified[file] = original[file].copy()
    for number in rule['lines']:
        index = number-1
        if not 0 <= index < len(modified[file]):
            raise ValueError((file, number, 'outside file'))
        if '\n' in rule['old'] or '\n' in rule['new']:
            raise ValueError('Line-scoped edits cannot insert/remove line breaks')
        line = modified[file][index]
        if rule['old'] not in line:
            raise ValueError((file, number, rule['old'], 'not found'))
        modified[file][index] = line.replace(rule['old'], rule['new'])
        reasons.setdefault((file, number), []).append(rule['reason'])
evidence=[]
for file in original:
    after = ''.join(modified[file]).encode()
    for number, (old, new) in enumerate(zip(original[file], modified[file]), 1):
        if old != new:
            evidence.append({'file':file,'line':number,'old':old.rstrip('\n'),
                'new':new.rstrip('\n'),'reason':' '.join(reasons[(file,number)]),
                'before_sha256':hashlib.sha256(source_bytes[file]).hexdigest(),
                'after_sha256':hashlib.sha256(after).hexdigest()})
for file in original:
    if (root/file).read_bytes()!=source_bytes[file]:
        raise ValueError('Concurrent modification detected; no writes performed')
for file in original:
    (root/file).write_bytes(''.join(modified[file]).encode())
(out/f'line-plan-{batch}.json').write_text(json.dumps(plan,ensure_ascii=False,indent=2)+'\n')
(out/f'edits-{batch}.json').write_text(json.dumps(evidence,ensure_ascii=False,indent=2)+'\n')
print(f'Applied {len(evidence)} explicitly reviewed line-scoped paragraph edits')
