"""Fingerprint existing validation dependencies without changing them."""
from pathlib import Path
import datetime, hashlib, importlib.metadata as metadata, json, subprocess, sys
ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
name = sys.argv[1]
result = OUT / (name + '.json')
if result.exists():
    raise SystemExit('Evidence already exists: ' + str(result))
record = {'time': datetime.datetime.now().astimezone().isoformat(), 'python': sys.version, 'python_executable': sys.executable, 'versions': {}, 'files': {}, 'checker_files': [], 'commands': []}
for package in ['SudachiPy', 'SudachiDict-core', 'ChikkarPy', 'PyYAML']:
    record['versions'][package] = metadata.version(package)
for package in ['cspell', 'textlint', 'textlint-rule-prh', 'yaml']:
    record['versions'][package] = json.loads((ROOT / 'node_modules' / package / 'package.json').read_text())['version']
for file in ['package.json', 'package-lock.json', '.textlintrc.json', '.textlintignore', 'cspell.config.jsonc', 'tools/lint/requirements.txt', 'tools/lint/markdown-targets.json', 'tools/lint/markdown-whitelist.yaml', 'tools/lint/prh.yml']:
    record['files'][file] = hashlib.sha256((ROOT / file).read_bytes()).hexdigest()
manifest = json.loads((ROOT / 'reports/diagnostics/wording-occurrence-audit-20260916/checker-manifest.json').read_text())
record['checker_source_revision'] = manifest['dependency_head']
for file, expected in manifest['files'].items():
    actual = hashlib.sha256((ROOT / '.agents' / file).read_bytes()).hexdigest()
    record['checker_files'].append({'file': file, 'expected_sha256': expected, 'actual_sha256': actual, 'matches': actual == expected})
record['all_checker_files_match'] = all(item['matches'] for item in record['checker_files'])
import yaml
allowlist = yaml.safe_load((ROOT / 'tools/lint/markdown-whitelist.yaml').read_text())
record['allowlist_count'] = len(allowlist['entries'])
record['approved_entries'] = [x for x in allowlist['entries'] if x['term'] in ['単体テスト', '相対パス', 'コミット', 'フィールド', '設定プロファイル', 'キャプチャー', 'キャプチャ']]
for number, args in enumerate([['git', 'rev-parse', 'HEAD'], ['node', '--version'], ['npm', '--version'], [sys.executable, '-m', 'pip', 'freeze'], ['npm', 'ls', '--all', '--json']]):
    process = subprocess.run(args, cwd=ROOT, capture_output=True)
    logs = {}
    for suffix, content in [('stdout', process.stdout), ('stderr', process.stderr), ('exit', (str(process.returncode) + '\n').encode())]:
        path = OUT / (name + '-' + str(number) + '.' + suffix)
        with path.open('xb') as stream:
            stream.write(content)
        logs[suffix] = {'path': path.name, 'sha256': hashlib.sha256(content).hexdigest()}
    record['commands'].append({'command': args, 'exit_code': process.returncode, 'logs': logs})
record['symlink_targets'] = {file: str((ROOT / file).resolve()) for file in ['node_modules', '.agents/skills']}
record['instructions_checked'] = []
for parent in [ROOT, *ROOT.parents]:
    for name in ['AGENTS.md', 'CLAUDE.md']:
        path = parent / name
        record['instructions_checked'].append({'path': str(path), 'exists': path.is_file()})
        if path.is_file():
            print('INSTRUCTION', str(path), path.read_text())
with result.open('x') as stream:
    json.dump(record, stream, ensure_ascii=False, indent=2)
    stream.write('\n')
print(json.dumps({'versions': record['versions'], 'allowlist_count': record['allowlist_count'], 'all_checker_files_match': record['all_checker_files_match'], 'command_exit_codes': [x['exit_code'] for x in record['commands']]}, ensure_ascii=False))
raise SystemExit(0 if record['all_checker_files_match'] else 1)
