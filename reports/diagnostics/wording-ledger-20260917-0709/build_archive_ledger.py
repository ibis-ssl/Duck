"""Join explicit human archival decisions to original occurrences; never infer acceptance."""
from __future__ import annotations
import argparse
import collections
import datetime
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path.cwd()
EVIDENCE = Path(__file__).resolve().parent
ORIGINAL = ROOT / 'reports/diagnostics/wording-occurrence-audit-20260916'

def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

def baseline_text(sha: str, file: str) -> list[str]:
    return subprocess.check_output(['git', 'show', f'{sha}:{file}'], text=True).splitlines()

def build(output: Path) -> dict:
    """Validate frozen inputs and emit traceable records, refusing stale or missing decisions."""
    summary_path = output.with_suffix('.summary.json')
    if output.exists() or summary_path.exists():
        raise ValueError('Existing evidence must not be overwritten; choose a new output name.')
    all_occurrences = json.loads((ORIGINAL / 'occurrences-start.json').read_text())
    inputs = json.loads((EVIDENCE / 'archive-line-correspondence.json').read_text())
    correspondences = inputs['correspondences']
    mapping = {(c['source_file'], c['source_line']): c for c in correspondences}
    if len(mapping) != len(correspondences):
        raise ValueError('Duplicate original-line correspondence.')
    selected = [o for o in all_occurrences if '/Archive/' in o['file']]
    original_files = {}
    current_files = {}
    records = []
    for occurrence in selected:
        key = (occurrence['baseline_sha'], occurrence['file'])
        if key not in original_files:
            original_files[key] = baseline_text(*key)
        text = original_files[key][occurrence['before_line'] - 1]
        column = occurrence['before_column'] - 1
        surface = occurrence['surface']
        if text != occurrence['before_text'] or text[column:column + len(surface)] != surface:
            raise ValueError(f"Original location mismatch: {occurrence['id']}")
        context = mapping[(occurrence['file'], occurrence['before_line'])]
        if context['decision'] != 'accepted_context_preservation' or not context['reason']:
            raise ValueError(f"No explicit contextual decision: {occurrence['id']}")
        if context['source_text'] != text:
            raise ValueError(f"Context source mismatch: {occurrence['id']}")
        locations = []
        for span in context['final_spans']:
            file = span['file']
            if file not in current_files:
                path = ROOT / file
                current_files[file] = (path.read_text().splitlines(), sha256(path))
            lines, digest = current_files[file]
            if digest != span['sha256'] or lines[span['start']-1:span['end']] != span['text']:
                raise ValueError(f"Final context is stale: {context['id']} in {file}")
            locations.append({k: span[k] for k in ['file', 'start', 'end', 'sha256']})
        records.append({
            'id': occurrence['id'],
            'whitelist_entry_index': occurrence['entry_index'],
            'original_term': occurrence['term'], 'original_surface': surface,
            'source': {'sha': occurrence['baseline_sha'], 'file': occurrence['file'],
                       'line': occurrence['before_line'], 'column': occurrence['before_column']},
            'source_text_and_final_expressions': f"archive-line-correspondence.json#{context['id']}",
            'final_locations': locations,
            'adoption': '原文の対象・条件を保持した表現として採用',
            'decision_reference': context['decision_reference'],
            'reason': context['reason'],
            'scope': '作業一覧・同じ作業IDの履歴本文、または対応する工程条件',
            'original_audit_state': occurrence['status'],
            'original_changed_block': occurrence.get('block'),
            'review_role': 'implementation_worker_self_check',
        })
    if len({r['id'] for r in records}) != len(records):
        raise ValueError('Duplicate occurrence ID.')
    summary = {
        'created_at': datetime.datetime.now().astimezone().isoformat(),
        'technical_parent': subprocess.check_output(['git', 'rev-parse', 'HEAD'], text=True).strip(),
        'all_original_occurrences': len(all_occurrences),
        'archive_occurrences': len(records),
        'other_occurrences_not_covered_by_this_file': len(all_occurrences) - len(records),
        'documents': dict(collections.Counter(r['source']['file'] for r in records)),
        'source_contexts': len(mapping),
        'current_file_sha256': {f: d for f, (_, d) in current_files.items()},
        'input_sha256': {p.name: sha256(p) for p in [
            ORIGINAL / 'occurrences-start.json', EVIDENCE / 'archive-line-correspondence.json',
            EVIDENCE / 'archive-semantic-review.json', Path(__file__).resolve()]},
        'full_pr_completion': False, 'independent_final_review': False,
        'limitation': 'Acceptance comes from explicitly recorded contextual decisions, not matching words. '
                      'This file covers only four archival documents; it does not claim all 4336 occurrences.',
    }
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open('x') as stream:
        for record in records:
            stream.write(json.dumps(record, ensure_ascii=False) + '\n')
    summary['output_sha256'] = sha256(output)
    with summary_path.open('x') as stream:
        json.dump(summary, stream, ensure_ascii=False, indent=2)
        stream.write('\n')
    return summary

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', required=True, type=Path)
    arguments = parser.parse_args()
    try:
        result = build(arguments.output)
    except (OSError, ValueError, KeyError, IndexError, subprocess.CalledProcessError) as error:
        print(f'Archive ledger generation failed: {error}', file=sys.stderr)
        return 1
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0

if __name__ == '__main__':
    raise SystemExit(main())
