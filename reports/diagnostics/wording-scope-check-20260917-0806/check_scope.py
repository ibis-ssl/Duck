"""Record scope evidence without inferring semantic approval from counts."""
from pathlib import Path
import collections, datetime, hashlib, json, subprocess, sys
ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
AUDIT = ROOT / 'reports/diagnostics/wording-occurrence-audit-20260916'
CHECKER = Path('/home/ibis/.local/share/duck-wording-audit-tools-20260916-1855/skills/review-enforcer/scripts/list-markdown-targets.js')
BASE = 'f5482ab85d49832a21ef6029d6aa3354f6c7c4f4'
if len(sys.argv) != 2:
    raise SystemExit('Usage: python check_scope.py <unique-batch-name>')
BATCH = sys.argv[1]
RESULT = OUT / (BATCH + '.json')
if RESULT.exists():
    raise SystemExit('Refusing to overwrite prior evidence: ' + str(RESULT))
def sha(data):
    return hashlib.sha256(data).hexdigest()
def load(path):
    return json.loads(path.read_text())
def run(name, args):
    p = subprocess.run(args, cwd=ROOT, capture_output=True)
    files = {}
    for suffix, data in [('stdout', p.stdout), ('stderr', p.stderr), ('exit', str(p.returncode).encode() + b'\n')]:
        f = OUT / (BATCH + '-' + name + '.' + suffix)
        with f.open('xb') as stream:
            stream.write(data)
        files[suffix] = {'file': f.name, 'sha256': sha(data)}
    return {'command': args, 'exit_code': p.returncode, 'logs': files}, p.stdout
record = {'time': datetime.datetime.now().astimezone().isoformat(), 'commands': [], 'semantic_completion_claim': False}
for name, args in [('head', ['git', 'rev-parse', 'HEAD']), ('status', ['git', 'status', '--short', '--untracked-files=all']), ('markdown-diff', ['git', 'diff', '--name-status', BASE + '...HEAD', '--', '*.md']), ('targets', ['node', str(CHECKER)])]:
    evidence, output = run(name, args)
    record['commands'].append(evidence)
    if evidence['exit_code'] != 0:
        raise SystemExit('Scope collection failed: ' + name)
    record[name] = output.decode().splitlines()
config = load(ROOT / 'tools/lint/markdown-targets.json')
baselines = load(AUDIT / 'baseline-manifest.json')
targets = set(record['targets'])
record['changed_markdown'] = []
for item in record['markdown-diff']:
    status, path = item.split('\t', 1)
    reasons = ['ignoreDirectories:' + part for part in Path(path).parts if part in config['ignoreDirectories']]
    reasons += ['ignoredPrefixes:' + p for p in config['ignoredPrefixes'] if path.startswith(p)]
    record['changed_markdown'].append({'path': path, 'git_status': status, 'lint_target': path in targets, 'exclusion_rules': reasons, 'sha256': sha((ROOT / path).read_bytes())})
record['baseline_checks'] = []
for entry in baselines:
    evidence, output = run('baseline-' + str(entry['index']), ['git', 'show', entry['baseline_sha'] + ':' + entry['file']])
    record['commands'].append(evidence)
    record['baseline_checks'].append({'file': entry['file'], 'baseline_sha': entry['baseline_sha'], 'expected_sha256': entry['baseline_sha256'], 'actual_sha256': sha(output), 'matches': evidence['exit_code'] == 0 and sha(output) == entry['baseline_sha256']})
record['target_files_without_baseline'] = sorted(targets - {x['file'] for x in baselines})
record['baseline_files_outside_targets'] = sorted({x['file'] for x in baselines} - targets)
record['changed_markdown_without_scope'] = [x['path'] for x in record['changed_markdown'] if not x['lint_target'] and not x['exclusion_rules']]
occurrences = load(AUDIT / 'occurrences-start.json')
blocks = load(AUDIT / 'changed-blocks-start.json')
record['occurrence_inventory'] = {'count': len(occurrences), 'unique_ids': len({x['id'] for x in occurrences}), 'by_file': dict(collections.Counter(x['file'] for x in occurrences)), 'outside_baselines': sorted({x['file'] for x in occurrences} - {x['file'] for x in baselines})}
record['block_inventory'] = {'count': len(blocks), 'unique_ids': len({x['id'] for x in blocks}), 'by_file': dict(collections.Counter(x['file'] for x in blocks))}
record['decision_inventory'] = []
for entry in baselines:
    path = AUDIT / ('decisions-doc-' + str(entry['index']).zfill(2) + '.json')
    decisions = load(path) if path.is_file() else []
    expected = {x['id'] for x in blocks if x['file'] == entry['file']}
    actual = [x['block'] for x in decisions]
    record['decision_inventory'].append({'file': entry['file'], 'decision_file': str(path.relative_to(ROOT)), 'expected_blocks': len(expected), 'decision_rows': len(actual), 'unique_blocks': len(set(actual)), 'missing': sorted(expected - set(actual)), 'unexpected': sorted(set(actual) - expected), 'empty_reasons': [x['block'] for x in decisions if not x.get('reason', '').strip()], 'judgment': 'Structural coverage only; inherited semantic decisions are not automatically approved.'})
guide = ROOT / 'tools/lint/README.md'
example = next(line for line in guide.read_text().splitlines() if 'list-markdown-targets.js --files README.md ' in line)
requested = example.split('--files ', 1)[1].split()
evidence, output = run('documented-example', ['node', str(CHECKER), '--files', *requested])
record['commands'].append(evidence)
record['example_check'] = {'source_file': str(guide.relative_to(ROOT)), 'source_sha256': sha(guide.read_bytes()), 'example': example, 'requested_files': requested, 'actual_files': output.decode().splitlines(), 'missing_files': sorted(set(requested) - set(output.decode().splitlines())), 'passed': evidence['exit_code'] == 0 and set(requested) == set(output.decode().splitlines())}
record['summary'] = {'changed_markdown': len(record['changed_markdown']), 'lint_targets': len(targets), 'excluded_changed_markdown': sum(not x['lint_target'] for x in record['changed_markdown']), 'baseline_hashes_match': all(x['matches'] for x in record['baseline_checks']), 'example_passed': record['example_check']['passed']}
record['scope_passed'] = not (record['target_files_without_baseline'] or record['baseline_files_outside_targets'] or record['changed_markdown_without_scope']) and record['summary']['baseline_hashes_match']
with RESULT.open('x') as stream:
    json.dump(record, stream, ensure_ascii=False, indent=2)
    stream.write('\n')
print(json.dumps(record['summary'], ensure_ascii=False))
print(json.dumps(record['example_check'], ensure_ascii=False))
raise SystemExit(0 if record['scope_passed'] and record['example_check']['passed'] else 1)
