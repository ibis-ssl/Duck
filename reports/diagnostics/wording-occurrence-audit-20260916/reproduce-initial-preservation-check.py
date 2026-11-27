import json,pathlib
p=pathlib.Path('reports/diagnostics/wording-occurrence-audit-20260916/history-preservation-initial.json')
r=json.loads(p.read_text())
print(json.dumps(r,ensure_ascii=False,indent=2))
assert all(c['all_report_occurrences_preserved'] and c['existing_quotations_preserved'] and c['task_id_counts_preserved'] for c in r)
