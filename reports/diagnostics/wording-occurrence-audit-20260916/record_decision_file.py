"""Record a complete list of explicitly reviewed block decisions for one document."""
import json, sys
from pathlib import Path
from review_support import record_reviews, BLOCKS
if len(sys.argv)!=3:
    raise SystemExit('Usage: record_decision_file.py <document-index> <decisions.json>')
index=int(sys.argv[1])
decisions=json.loads(Path(sys.argv[2]).read_text())
blocks=[b for b in BLOCKS if b['id'].startswith(f'D{index:02d}-')]
if not isinstance(decisions,list) or len(decisions)!=len(blocks):
    raise ValueError((index,len(decisions),len(blocks)))
if not all(len(row)==2 and row[0] in {'accepted','fixed','held'} and row[1].strip() for row in decisions):
    raise ValueError('Every decision needs an explicit disposition and contextual reason')
record_reviews(index,{i:decision for i,decision in enumerate(decisions,1)})
print(f'Recorded {len(decisions)} contextual decisions for document {index:02d}')
