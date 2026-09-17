from pathlib import Path
import datetime, hashlib, json, subprocess, sys
ROOT = Path.cwd()
OUT = ROOT / 'reports/diagnostics/lint-guide-clarity-20260916-0826'
STAGE = sys.argv[1]
OUT.mkdir(parents=True, exist_ok=True)
def manifest():
    names = subprocess.check_output(['git', 'ls-files'], text=True).splitlines()
    paths = [ROOT / n for n in names if not n.startswith('reports/')]
    paths += list((ROOT / '.agents/skills/review-enforcer').rglob('*'))
    return {str(p.relative_to(ROOT)): hashlib.sha256(p.read_bytes()).hexdigest()
            for p in sorted(set(paths)) if p.is_file() and '__pycache__' not in p.parts}
def run(name, command):
    before = manifest()
    result = subprocess.run(command, capture_output=True)
    prefix = STAGE + '-' + name
    (OUT / (prefix + '.stdout.log')).write_bytes(result.stdout)
    (OUT / (prefix + '.stderr.log')).write_bytes(result.stderr)
    record = dict(name=name, command=command, exit_code=result.returncode,
                  head_sha=subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
                  source_fingerprint=hashlib.sha256(json.dumps(before, sort_keys=True).encode()).hexdigest(),
                  unchanged_during_run=before == manifest(), cwd=str(ROOT),
                  checked_at=datetime.datetime.now().astimezone().isoformat(),
                  stdout=prefix + '.stdout.log', stderr=prefix + '.stderr.log')
    (OUT / (prefix + '.manifest.json')).write_text(json.dumps(before, indent=2) + '\n')
    print(name, result.returncode, 'unchanged', record['unchanged_during_run'], flush=True)
    return record
scripts = '.agents/skills/review-enforcer/scripts/'
commands = [('full-md', ['npm', 'run', 'lint:md'])]
if STAGE != 'before':
    commands += [
        ('focused-text', ['node_modules/.bin/textlint', '--config', '.textlintrc.json', '--rulesdir', scripts + 'textlint-rules', 'tools/lint/README.md']),
        ('focused-spell', ['node', scripts + 'run-cspell-markdown.js', 'tools/lint/README.md']),
        ('focused-whitelist', ['python3', scripts + 'check-markdown-whitelist-sudachi.py', '--files', 'tools/lint/README.md']),
        ('diff-check', ['git', 'diff', '--check']),
    ]
records = [run(name, command) for name, command in commands]
(OUT / (STAGE + '-results.json')).write_text(json.dumps(records, ensure_ascii=False, indent=2) + '\n')
print(datetime.datetime.now().astimezone().isoformat())
