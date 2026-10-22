import hashlib, importlib.metadata, json, pathlib, subprocess
repo=pathlib.Path('/home/donabe/Duck-issue81-aspire-desktop-bridge')
mirror=pathlib.Path('/mnt/c/Users/donabe/AppData/Local/Temp/issue81-04a-lint-20261022')
ref='5f280e33da9f0e93d7396373c9573b0f21424582'
paths=['.textlintrc.json','cspell.config.jsonc','package.json','tools/lint/markdown-targets.json','tools/lint/markdown-whitelist.yaml','tools/lint/scripts/check-markdown-whitelist-sudachi.py','tools/lint/scripts/run-markdown-targets.js','tools/lint/scripts/list-markdown-targets.js','tools/lint/scripts/run-cspell-markdown.js']
result={'ref':ref,'configAndCheckerFiles':[]}
for rel in paths:
    b=subprocess.run(['git','-C',str(repo),'show',f'{ref}:{rel}'],check=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE).stdout
    candidate=hashlib.sha256(b).hexdigest()
    q=mirror/rel
    mirrored=hashlib.sha256(q.read_bytes()).hexdigest() if q.is_file() else None
    result['configAndCheckerFiles'].append({'path':rel,'candidateSha256':candidate,'mirrorSha256':mirrored,'matches':candidate==mirrored})
dic=pathlib.Path('/home/donabe/venvs/duck-pr73-r04-ff50682/lib/python3.14/site-packages/sudachidict_core/resources/system.dic')
result['runtime']={'SudachiPy':importlib.metadata.version('SudachiPy'),'SudachiDictCore':importlib.metadata.version('SudachiDict-core'),'systemDicBytes':dic.stat().st_size,'systemDicSha256':hashlib.sha256(dic.read_bytes()).hexdigest()}
out=pathlib.Path('/mnt/c/Users/donabe/AppData/Local/Temp/issue81-tracking-lint-5f280e3-20261022/lint-source-context.json')
out.write_text(json.dumps(result,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(json.dumps(result,indent=2,ensure_ascii=False))
