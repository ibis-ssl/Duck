"""Validate the source index; this is not a final wording correspondence ledger."""
from pathlib import Path
import datetime, hashlib, json, subprocess, sys
ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
AUDIT = ROOT / 'reports/diagnostics/wording-occurrence-audit-20260916'
result_path = OUT / (sys.argv[1] + '.json')
if result_path.exists():
    raise SystemExit('Refusing to overwrite prior evidence')
occurrences = json.loads((AUDIT / 'occurrences-start.json').read_text())
record = {'time': datetime.datetime.now().astimezone().isoformat(), 'head': subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip(), 'semantic_completion_claim': False, 'source_blobs': [], 'occurrences': [], 'history_snapshots': []}
cache = {}
for o in occurrences:
    key = o['baseline_sha'] + ':' + o['file']
    if key not in cache:
        args = ['git', 'show', key]
        p = subprocess.run(args, cwd=ROOT, capture_output=True)
        if p.returncode != 0:
            raise SystemExit(p.stderr.decode())
        cache[key] = p.stdout.decode().splitlines()
        record['source_blobs'].append({'command': args, 'exit_code': p.returncode, 'stderr': p.stderr.decode(), 'sha256': hashlib.sha256(p.stdout).hexdigest()})
    line = cache[key][o['before_line'] - 1]
    surface = line[o['before_column'] - 1:o['before_column'] - 1 + len(o['surface'])]
    record['occurrences'].append({'id': o['id'], 'source': key, 'line': o['before_line'], 'column': o['before_column'], 'expected_surface': o['surface'], 'actual_surface': surface, 'line_matches': line == o['before_text'], 'surface_matches': surface == o['surface']})
for entry in json.loads((ROOT / 'reports/history/terminology-baseline-manifest.json').read_text()):
    args = ['git', 'show', entry['base_sha'] + ':' + entry['source']]
    p = subprocess.run(args, cwd=ROOT, capture_output=True)
    snapshot = (ROOT / entry['snapshot']).read_bytes()
    record['history_snapshots'].append({'source': entry['source'], 'snapshot': entry['snapshot'], 'baseline_sha': entry['base_sha'], 'command': args, 'exit_code': p.returncode, 'stderr': p.stderr.decode(), 'original_sha256': hashlib.sha256(p.stdout).hexdigest(), 'snapshot_sha256': hashlib.sha256(snapshot).hexdigest(), 'expected_sha256': entry['sha256'], 'passed': p.returncode == 0 and p.stdout == snapshot and hashlib.sha256(snapshot).hexdigest() == entry['sha256']})
record['source_index_errors'] = [x['id'] for x in record['occurrences'] if not x['line_matches'] or not x['surface_matches']]
record['guide_example_original_occurrences'] = [o['id'] for o in occurrences if o['file'] == 'tools/lint/README.md' and 'Tracker/README.appsettings.md' in o['before_text']]
record['summary'] = {'occurrences': len(occurrences), 'unique_ids': len({o['id'] for o in occurrences}), 'source_index_errors': len(record['source_index_errors']), 'history_snapshots': len(record['history_snapshots']), 'history_errors': sum(not x['passed'] for x in record['history_snapshots']), 'guide_example_original_occurrences': record['guide_example_original_occurrences']}
with result_path.open('x') as stream:
    json.dump(record, stream, ensure_ascii=False, indent=2)
    stream.write('\n')
print(json.dumps(record['summary'], ensure_ascii=False))
raise SystemExit(1 if record['source_index_errors'] or record['summary']['history_errors'] else 0)
