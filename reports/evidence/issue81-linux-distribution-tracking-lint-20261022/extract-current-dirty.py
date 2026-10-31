import difflib,hashlib,json,pathlib,subprocess
repo=pathlib.Path('/home/donabe/Duck-issue81-aspire-desktop-bridge')
out=pathlib.Path('/mnt/c/Users/donabe/AppData/Local/Temp/issue81-tracking-lint-5f280e3-20261022')
base='4e3099e75c4afab8ba1f11c67500ebe29f17fed3'
paths=['Tracker/Design/tasks-status.md','Tracker/Design/phases-status.md']
obj={'baseline':base,'current':'working-tree at extraction time; parent-owned prose edits only','files':[]}
diffs=[]
for rel in paths:
    old_bytes=subprocess.run(['git','-C',str(repo),'show',f'{base}:{rel}'],check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE).stdout
    new_bytes=(repo/rel).read_bytes()
    old=old_bytes.decode('utf-8').splitlines()
    new=new_bytes.decode('utf-8').splitlines()
    changed=[]
    for tag,i1,i2,j1,j2 in difflib.SequenceMatcher(a=old,b=new,autojunk=False).get_opcodes():
        if tag in ('replace','insert'):
            changed.extend({'line':n+1,'text':new[n],'sha256':hashlib.sha256(new[n].encode('utf-8')).hexdigest()} for n in range(j1,j2) if new[n].strip())
    fragment=out/(pathlib.Path(rel).name+'-dirty-changed-lines.md')
    fragment.write_text('\n'*(changed[0]['line']-1)+'\n'.join(row['text'] for row in changed)+'\n',encoding='utf-8')
    obj['files'].append({'path':rel,'baselineBytes':len(old_bytes),'baselineSha256':hashlib.sha256(old_bytes).hexdigest(),'currentBytes':len(new_bytes),'currentSha256':hashlib.sha256(new_bytes).hexdigest(),'changedLines':changed,'fragment':fragment.name,'fragmentBytes':fragment.stat().st_size,'fragmentSha256':hashlib.sha256(fragment.read_bytes()).hexdigest()})
    d=subprocess.run(['git','-C',str(repo),'diff','--no-ext-diff','--unified=0',base,'--',rel],check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
    diffs.append(d.stdout.decode('utf-8'))
(out/'tracking-current-dirty.diff').write_text('\n'.join(diffs),encoding='utf-8')
(out/'tracking-source-diff-current-dirty.json').write_text(json.dumps(obj,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(obj,ensure_ascii=False,indent=2))
