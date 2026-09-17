from pathlib import Path
import subprocess, json, hashlib, re, os, sys, yaml
root=Path('/home/ibis/.local/share/duck-pr20-review-followup-20260916'); os.chdir(root)
out=Path('/home/ibis/.local/share/duck-pr20-review-followup-evidence-20260916/resume')
base='53796bcfa789ced28f1034f445c2d24dff6d175a'
audit=json.loads(Path('reports/markdown-wording-audit-20260916.json').read_text()); docs=audit['markdown_files']
def fingerprint():
 files=sorted(set(subprocess.check_output(['git','ls-files'],text=True).splitlines()+[str(p) for p in Path('reports/history').glob('*')]))
 manifest={p:hashlib.sha256(Path(p).read_bytes()).hexdigest() for p in files if Path(p).is_file()}
 return manifest,hashlib.sha256(json.dumps(manifest,sort_keys=True).encode()).hexdigest()
manifest,fp=fingerprint(); results=[]
def run(name,args):
 r=subprocess.run(args,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
 (out/(name+'.stdout.log')).write_bytes(r.stdout);(out/(name+'.stderr.log')).write_bytes(r.stderr)
 results.append({'name':name,'command':args,'exit_code':r.returncode,'source_fingerprint':fp})
 print(name,'exit',r.returncode,flush=True);return r
run('full-md',['npm','run','lint:md'])
focused=[p for p in docs if p!='feedback-points/feedback-points.md']
run('focused-whitelist',['python3','.agents/skills/review-enforcer/scripts/check-markdown-whitelist-sudachi.py','--files',*focused])
run('whitelist-definitions',['python3','.agents/skills/review-enforcer/scripts/check-markdown-whitelist-sudachi.py','--files','tools/lint/markdown-whitelist.yaml'])
run('diff-check',['git','diff','--check'])
checks=[]
def check(name,value):checks.append({'name':name,'passed':bool(value)})
text='\n'.join(Path(p).read_text() for p in docs); wl=yaml.safe_load(Path('tools/lint/markdown-whitelist.yaml').read_text())
old=yaml.safe_load(subprocess.check_output(['git','show',base+':tools/lint/markdown-whitelist.yaml'],text=True))
vals=lambda w:{v for e in w['entries'] for v in [e['term'],*e.get('aliases',[])]}
check('existing allowed spellings preserved',vals(old)<=vals(wl))
check('only three approved new spellings',vals(wl)-vals(old)=={'フィールド','設定プロファイル','キャプチャー'})
for bad in ['競技場','設定組','未加工映像','入力元の追跡結果の番号','傍受','約約','カルマン状態推定','プロセス側の揺らぎ','観測側の揺らぎ','利用可能な利用可能な','規則に基づくの']:
 check('removed misleading wording: '+bad,bad not in text)
for term in ['source frame number','Kalman filter','process noise','measurement noise','render snapshot','timeline scrubber','replay timeline','diagnostics sample tick','Field source']:
 check('named concept present: '+term,term in text)
check('original direct quotation retained','次回からレビューレポートの編集許可をサブエージェントに渡すようにしてください' in Path('feedback-points/feedback-points.md').read_text())
check('observer and event named', all(s in Path('Tracker/Design/Core/tracker-history-000-038.md').read_text() for s in ['イベントと通知先の契約','`TrackerEvent`','`ITrackerObserver`']))
check('actual UI labels preserved','`Vision Input` と `Tracker Output` の文字列' in Path('Tracker/Design/DebugHost/raw-vision-viewer-plan.md').read_text())
for record in json.loads(Path('reports/history/terminology-baseline-manifest.json').read_text()):
 data=Path(record['snapshot']).read_bytes(); original=subprocess.check_output(['git','show',record['base_sha']+':'+record['source']])
 check('exact historical content: '+record['source'],data==original)
 check('history source link: '+record['source'],record['snapshot'] in Path(record['source']).read_text())
check('no product change',not any(re.search(r'\.(cs|razor|csproj|proto|sln)$',p) for p in subprocess.check_output(['git','diff','--name-only',base],text=True).splitlines()))
for label,body,expected in [('allowed','フィールドに表示する。\n設定プロファイルを選ぶ。\n通信内容をキャプチャーする。\n',0),('rejected-profile','プロファイル\n',1),('rejected-capture','キャプチャ\n',1),('rejected-datagram','データグラム\n',1)]:
 r=subprocess.run(['python3','.agents/skills/review-enforcer/scripts/check-markdown-whitelist-sudachi.py','--stdin','term-validation.md'],input=body.encode(),stdout=subprocess.PIPE,stderr=subprocess.PIPE)
 (out/(label+'.stdout.log')).write_bytes(r.stdout);(out/(label+'.stderr.log')).write_bytes(r.stderr)
 style=subprocess.run(['node_modules/.bin/textlint','--stdin','--stdin-filename','term-validation.md','--config','.textlintrc.json','--rulesdir','.agents/skills/review-enforcer/scripts/textlint-rules'],input=body.encode(),capture_output=True)
 (out/(label+'.style.stdout.log')).write_bytes(style.stdout);(out/(label+'.style.stderr.log')).write_bytes(style.stderr)
 actual=style.returncode or r.returncode
 checks.append({'name':'full policy: '+label,'passed':actual==expected,'whitelist_exit':r.returncode,'style_exit':style.returncode,'actual_exit':actual,'expected_exit':expected})
check('source unchanged during validation',fingerprint()[1]==fp)
payload={'baseline_head':base,'validated_head':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'source_fingerprint':fp,'manifest':manifest,'commands':results,'checks':checks,'note':'Document assertions and implementation self-check, not an independent review or proof of full occurrence-level closure.'}
(out/'validation.json').write_text(json.dumps(payload,ensure_ascii=False,indent=2)+'\n')
print('Checks',sum(c['passed'] for c in checks),'/',len(checks),flush=True)
print(json.dumps([c for c in checks if not c['passed']],ensure_ascii=False))
print((out/'full-md.stdout.log').read_text()[-1800:])
