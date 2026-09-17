"""Run all full-lint stages even after failure and preserve source quotations."""
from pathlib import Path
import datetime, hashlib, json, os, re, subprocess, sys
ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
batch = sys.argv[1]
result_path = OUT / (batch + '.json')
if result_path.exists():
    raise SystemExit('Refusing to overwrite prior evidence')
env = dict(os.environ, PATH=str(Path(sys.executable).parent) + os.pathsep + os.environ['PATH'], PYTHONDONTWRITEBYTECODE='1')
targets = subprocess.check_output(['node', '.agents/skills/review-enforcer/scripts/list-markdown-targets.js'], cwd=ROOT, text=True).splitlines()
def fingerprint():
    return {file: hashlib.sha256((ROOT / file).read_bytes()).hexdigest() for file in targets}
record = {'time': datetime.datetime.now().astimezone().isoformat(), 'head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(), 'file_hashes': fingerprint(), 'results': []}
for name, args in [('textlint', ['npm', 'run', 'lint:md:text']), ('cspell', ['npm', 'run', 'lint:md:spell']), ('whitelist', ['npm', 'run', 'lint:md:whitelist']), ('diff-check', ['git', 'diff', '--check'])]:
    p = subprocess.run(args, cwd=ROOT, env=env, capture_output=True)
    logs = {}
    for suffix, data in [('stdout', p.stdout), ('stderr', p.stderr), ('exit', (str(p.returncode) + '\n').encode())]:
        path = OUT / (batch + '-' + name + '.' + suffix)
        with path.open('xb') as stream:
            stream.write(data)
        logs[suffix] = {'file': path.name, 'sha256': hashlib.sha256(data).hexdigest()}
    record['results'].append({'name': name, 'command': args, 'exit_code': p.returncode, 'logs': logs})
    print(name, p.returncode, p.stdout.decode()[-1500:], p.stderr.decode()[-400:], flush=True)
file = 'feedback-points/feedback-points.md'
base = 'f5482ab85d49832a21ef6029d6aa3354f6c7c4f4'
args = ['git', 'show', base + ':' + file]
p = subprocess.run(args, cwd=ROOT, capture_output=True)
for suffix, data in [('stdout', p.stdout), ('stderr', p.stderr), ('exit', (str(p.returncode) + '\n').encode())]:
    with (OUT / (batch + '-quotation-baseline.' + suffix)).open('xb') as stream:
        stream.write(data)
original = re.findall(r'「[^」]*」', p.stdout.decode())
current = re.findall(r'「[^」]*」', (ROOT / file).read_text())
record['quotation_check'] = {'command': args, 'exit_code': p.returncode, 'baseline_sha': base, 'file': file, 'original_quotes': original, 'current_quotes': current, 'baseline_sha256': hashlib.sha256(p.stdout).hexdigest(), 'current_sha256': hashlib.sha256((ROOT / file).read_bytes()).hexdigest(), 'passed': p.returncode == 0 and len(current) == 2 and original == current}
record['inputs_unchanged'] = record['file_hashes'] == fingerprint() and record['head'] == subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
with result_path.open('x') as stream:
    json.dump(record, stream, ensure_ascii=False, indent=2)
    stream.write('\n')
print('quotation_check', record['quotation_check']['passed'], 'inputs_unchanged', record['inputs_unchanged'])
raise SystemExit(0 if all(x['exit_code'] == 0 for x in record['results']) and record['inputs_unchanged'] and record['quotation_check']['passed'] else 1)
