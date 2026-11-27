import difflib,hashlib,importlib.metadata,json,pathlib,subprocess
repo=pathlib.Path('/home/donabe/Duck-issue81-aspire-desktop-bridge')
out=pathlib.Path('/mnt/c/Users/donabe/AppData/Local/Temp/issue81-tracking-lint-5f280e3-20261022/final')
base='4e3099e75c4afab8ba1f11c67500ebe29f17fed3'
paths=['Tracker/Design/tasks-status.md','Tracker/Design/phases-status.md']
obj={'baseline':base,'current':'working-tree after parent line-33 edit','targetFiles':2,'expectedChangedLines':4,'files':[]}
for rel in paths:
 old_bytes=subprocess.run(['git','-C',str(repo),'show',f'{base}:{rel}'],check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE).stdout
 new_bytes=(repo/rel).read_bytes()
 old=old_bytes.decode('utf-8').splitlines(); new=new_bytes.decode('utf-8').splitlines(); changed=[]
 for tag,i1,i2,j1,j2 in difflib.SequenceMatcher(a=old,b=new,autojunk=False).get_opcodes():
  if tag in ('replace','insert'):
   changed.extend({'line':n+1,'text':new[n],'sha256':hashlib.sha256(new[n].encode()).hexdigest()} for n in range(j1,j2) if new[n].strip())
 name=pathlib.Path(rel).name
 fragment=out/name
 fragment.write_text('\n'*(changed[0]['line']-1)+'\n'.join(row['text'] for row in changed)+'\n',encoding='utf-8')
 obj['files'].append({'path':rel,'baselineBytes':len(old_bytes),'baselineSha256':hashlib.sha256(old_bytes).hexdigest(),'currentBytes':len(new_bytes),'currentSha256':hashlib.sha256(new_bytes).hexdigest(),'changedLines':changed,'fragment':name,'fragmentBytes':fragment.stat().st_size,'fragmentSha256':hashlib.sha256(fragment.read_bytes()).hexdigest()})
if [r['line'] for r in obj['files'][0]['changedLines']] != [31,32,33] or [r['line'] for r in obj['files'][1]['changedLines']] != [19]:
 raise SystemExit('scope mismatch: expected tasks lines 31-33 and phases line 19')
if '対応するデバッグ情報' not in obj['files'][0]['changedLines'][2]['text']:
 raise SystemExit('parent line 33 edit not found')
ctx=json.loads((out.parent/'lint-source-context.json').read_text(encoding='utf-8'))
configs=[]
for item in ctx['configAndCheckerFiles']:
 rel=item['path']; p=repo/rel
 live=hashlib.sha256(p.read_bytes()).hexdigest()
 if live!=item['candidateSha256']: raise SystemExit('lint config/source changed: '+rel)
 configs.append({'path':rel,'sha256':live,'matchesPrior5fCandidate':True})
dic=pathlib.Path('/home/donabe/venvs/duck-pr73-r04-ff50682/lib/python3.14/site-packages/sudachidict_core/resources/system.dic')
dic_hash=hashlib.sha256(dic.read_bytes()).hexdigest()
if dic_hash!='6c1d5adc8a2389875713056e7b39bbcd0073d6122ffd509866e1d3a196f8608e': raise SystemExit('Sudachi dictionary changed')
obj['configHashes']=configs
obj['dictionary']={'SudachiPy':importlib.metadata.version('SudachiPy'),'SudachiDictCore':importlib.metadata.version('SudachiDict-core'),'systemDicBytes':dic.stat().st_size,'systemDicSha256':dic_hash,'matchesPrior':True}
(out/'preflight.json').write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(obj,ensure_ascii=False,indent=2))
