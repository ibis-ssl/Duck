"""Run every required Markdown check and retain both output streams, including failures."""
from __future__ import annotations
import datetime, hashlib, json, os, subprocess, sys, tarfile
from pathlib import Path
D = Path('reports/diagnostics/wording-occurrence-audit-20260916')
if len(sys.argv) < 3:
    raise SystemExit('Usage: python validate_batch.py <batch> <markdown-file> ...')
batch, files = sys.argv[1], sys.argv[2:]
scripts = '.agents/skills/review-enforcer/scripts/'
targets = subprocess.check_output(['node', scripts+'list-markdown-targets.js'], text=True).splitlines()
if not set(files).issubset(targets):
    raise SystemExit('The focused scope must be within the configured Markdown targets.')
head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip()
hashes = lambda: {f: hashlib.sha256(Path(f).read_bytes()).hexdigest() for f in targets}
before = hashes()
commands = [('focused-textlint', ['./node_modules/.bin/textlint','--config','.textlintrc.json','--rulesdir',scripts+'textlint-rules',*files]),
    ('focused-cspell', ['node',scripts+'run-cspell-markdown.js',*files]),
    ('focused-whitelist', [sys.executable,scripts+'check-markdown-whitelist-sudachi.py','--files',*files]),
    ('diff-check', ['git','diff','--check']), ('full-lint', ['npm','run','lint:md'])]
env = os.environ.copy()
env['PATH'] = str(Path(sys.executable).parent) + os.pathsep + env['PATH']
results = []
for name, args in commands:
    p = subprocess.run(args, capture_output=True, env=env)
    for suffix, data in [('stdout',p.stdout),('stderr',p.stderr),('exit',f'{p.returncode}\n'.encode())]:
        (D/'logs'/f'{batch}-{name}.{suffix}').write_bytes(data)
    results.append({'name':name,'command':args,'exit_code':p.returncode,'stdout':f'logs/{batch}-{name}.stdout','stderr':f'logs/{batch}-{name}.stderr'})
    print(name, p.returncode, p.stderr.decode()[-600:], flush=True)
after = hashes()
unchanged = before == after and head == subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip()
record = {'time':datetime.datetime.now().astimezone().isoformat(),'technical_parent':head,
    'focused_files':files,'all_target_files':targets,'file_hashes':before,
    'inputs_unchanged_during_validation':unchanged,'results':results}
(D/f'validation-{batch}.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
paths = sorted((D/'logs').glob(f'{batch}-*'))
with tarfile.open(D/f'{batch}-validation-logs.tar.gz','w:gz') as archive:
    for path in paths:
        archive.add(path,arcname=path.name)
manifest = [{'name':p.name,'bytes':p.stat().st_size,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in paths]
(D/f'{batch}-validation-logs-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
if not unchanged:
    raise SystemExit('Validation inputs changed during execution; results cannot be accepted.')
raise SystemExit(1 if any(r['exit_code'] for r in results) else 0)
