import difflib, hashlib, json, pathlib, subprocess, sys
repo=pathlib.Path('/home/donabe/Duck-issue81-aspire-desktop-bridge')
out=pathlib.Path('/mnt/c/Users/donabe/AppData/Local/Temp/issue81-tracking-lint-5f280e3-20261022')
base='4e3099e75c4afab8ba1f11c67500ebe29f17fed3'
head='5f280e33da9f0e93d7396373c9573b0f21424582'
paths=['Tracker/Design/tasks-status.md','Tracker/Design/phases-status.md']
result={'baseline':base,'current':head,'files':[]}
for rel in paths:
    versions={}
    for name,ref in [('baseline',base),('current',head)]:
        p=subprocess.run(['git','-C',str(repo),'show',f'{ref}:{rel}'],check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
        versions[name]=p.stdout
    old=versions['baseline'].decode('utf-8').splitlines()
    new=versions['current'].decode('utf-8').splitlines()
    matcher=difflib.SequenceMatcher(a=old,b=new,autojunk=False)
    changed=[]
    for tag,i1,i2,j1,j2 in matcher.get_opcodes():
        if tag in ('replace','insert'):
            changed.extend({'line':n+1,'text':new[n],'sha256':hashlib.sha256(new[n].encode('utf-8')).hexdigest()} for n in range(j1,j2) if new[n].strip())
    target=out/(pathlib.Path(rel).name+'-changed-lines.md')
    target.write_text('\n'*(changed[0]['line']-1)+'\n'.join(x['text'] for x in changed)+'\n',encoding='utf-8')
    result['files'].append({'path':rel,'baselineBytes':len(versions['baseline']),'baselineSha256':hashlib.sha256(versions['baseline']).hexdigest(),'currentBytes':len(versions['current']),'currentSha256':hashlib.sha256(versions['current']).hexdigest(),'changedLines':changed,'fragment':target.name,'fragmentBytes':target.stat().st_size,'fragmentSha256':hashlib.sha256(target.read_bytes()).hexdigest()})
(out/'tracking-source-diff.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(result,ensure_ascii=False,indent=2))


