"""Verify both historic quotations and unchanged ledger identity fields."""
import hashlib, json, re, subprocess
from pathlib import Path
root=Path.cwd()
out=root/'reports/diagnostics/wording-occurrence-audit-20260916'
file='feedback-points/feedback-points.md'
base='f5482ab85d49832a21ef6029d6aa3354f6c7c4f4'
start='a2e63f05345bb4301d6033273910a26ccfb4a1d2'
read=lambda sha:subprocess.check_output(['git','show',sha+':'+file],text=True)
original, prior, current=read(base),read(start),(root/file).read_text()
quotes=lambda text:re.findall(r'「[^」]*」',text)
rows=lambda text:{line.split(' | ')[0][2:]:line.strip('| ').split(' | ') for line in text.splitlines() if line.startswith('| FP-')}
a,b=rows(prior),rows(current)
assert set(a)==set(b)=={'FP-001','FP-002'}
unchanged_fields=[0,1,3,4,5,6,7,8,9,10,11]
assert all(a[key][i]==b[key][i] for key in a for i in unchanged_fields)
assert quotes(original)==quotes(current) and len(quotes(current))==2
record={'baseline_sha':base,'previous_check_baseline':start,'file':file,
 'original_quotes':quotes(original),'previous_head_quotes':quotes(prior),
 'current_quotes':quotes(current),'both_quotes_exactly_preserved':True,
 'historic_identity_origin_category_count_state_dates_unchanged':True,
 'current_sha256':hashlib.sha256(current.encode()).hexdigest(),
 'prior_limitation':'開始時点とバイト一致することは、用語整理前の原文との一致を証明しない。FP-002の引用改変を今回復元した。'}
(root/'reports/diagnostics/wording-verification-20260917-0758/quotation-recheck.json').write_text(json.dumps(record,ensure_ascii=False,indent=2)+'\n')
print(json.dumps(record,ensure_ascii=False,indent=2))
