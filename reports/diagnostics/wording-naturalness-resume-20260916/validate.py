from pathlib import Path
from collections import Counter
import subprocess, re, json, hashlib, datetime
root = Path.cwd()
out = root / 'reports/diagnostics/wording-naturalness-resume-20260916'
def git(*args):
    return subprocess.check_output(['git', *args], text=True)
head = git('rev-parse', 'HEAD').strip()
seed = '9e48670560a17feb4fd65f126a6c7eac7116f7dc'
paths = git('diff', '--name-only', seed, '84dbf6c235be9e89e775bc0020fb891d56131ee5', '--', '*.md', ':(exclude)reports/**').splitlines()
checks = []
def check(name, passed):
    checks.append({'name': name, 'passed': bool(passed)})
check('eleven reviewed Markdown files', len(paths) == 11)
for path in paths:
    old = git('show', seed + ':' + path)
    new = Path(path).read_text()
    fenced = r'^```.*?^```'
    check(path + ': fenced code retained', re.findall(fenced, old, re.M | re.S) == re.findall(fenced, new, re.M | re.S))
    inline = r'`[^`\n]*`'
    check(path + ': inline code retained', not (Counter(re.findall(inline, old)) - Counter(re.findall(inline, new))))
    refs = r'\]\(([^)]*)\)|\[\^([^\]]*)\]'
    check(path + ': references retained', Counter(re.findall(refs, old)) == Counter(re.findall(refs, new)))
    numbers = r'\d+(?:\.\d+)?'
    check(path + ': numbers retained', Counter(re.findall(numbers, old)) == Counter(re.findall(numbers, new)))
    lists = r'^\s*[-*+] '
    check(path + ': list indentation retained', re.findall(lists, old, re.M) == re.findall(lists, new, re.M))
check('whitelist unchanged', git('diff', seed, '--', 'tools/lint/markdown-whitelist.yaml') == '')
quote_path = 'feedback-points/feedback-points.md'
check('quotation unchanged', Path(quote_path).read_text() == git('show', seed + ':' + quote_path))
files = [p for p in git('ls-files', '*.md').splitlines() if not p.startswith('reports/') and p != quote_path]
commands = []
for name, args in [('focused-whitelist', ['/home/ibis/ssl/IbisDuck/.venv/bin/python3', '.agents/skills/review-enforcer/scripts/check-markdown-whitelist-sudachi.py', '--files', *files]), ('diff-check', ['git', 'diff', '--check'])]:
    cp = subprocess.run(args, capture_output=True)
    (out / (name + '.stdout.log')).write_bytes(cp.stdout)
    (out / (name + '.stderr.log')).write_bytes(cp.stderr)
    commands.append({'name': name, 'command': args, 'exit_code': cp.returncode})
    print(name, cp.returncode, cp.stderr.decode()[-1000:])
manifest = {p: hashlib.sha256(Path(p).read_bytes()).hexdigest() for p in files + [quote_path, 'tools/lint/markdown-whitelist.yaml']}
result = {'seed_head': seed, 'tested_head': head, 'checked_at': datetime.datetime.now().astimezone().isoformat(), 'changed_files': paths, 'manifest': manifest, 'commands': commands, 'checks': checks, 'full_lint_exit_code': int((out / 'lint-integrated.exit').read_text()), 'note': 'Implementation self-check; not independent review or full occurrence-level closure.'}
(out / 'validation-current.json').write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n')
print('CHECKS', sum(c['passed'] for c in checks), '/', len(checks))
print([c for c in checks if not c['passed']])
print(result['checked_at'])
raise SystemExit(0 if all(c['passed'] for c in checks) and all(c['exit_code'] == 0 for c in commands) else 1)
