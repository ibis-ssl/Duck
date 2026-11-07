import json,pathlib,subprocess,sys
root=pathlib.Path('/mnt/c/Users/donabe/AppData/Local/Temp/issue81-04a-lint-20261022')
out=pathlib.Path('/mnt/c/Users/donabe/AppData/Local/Temp/issue81-tracking-lint-5f280e3-20261022')
py='/home/donabe/venvs/duck-pr73-r04-ff50682/bin/python'
checker='/home/donabe/Duck-issue81-aspire-desktop-bridge/tools/lint/scripts/check-markdown-whitelist-sudachi.py'
args=[py,checker,'--files','tracking-evidence-dirty/tasks-status.md','tracking-evidence-dirty/phases-status.md']
(out/'sudachi-dirty-raw.command.json').write_text(json.dumps({'cwd':str(root),'argv':args,'stdin':'none','inputFiles':2},ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
p=subprocess.run(args,cwd=root,stdout=subprocess.PIPE,stderr=subprocess.PIPE,check=False)
(out/'sudachi-dirty-raw.stdout.log').write_bytes(p.stdout)
(out/'sudachi-dirty-raw.stderr.log').write_bytes(p.stderr)
(out/'sudachi-dirty-raw.exit-code').write_text(str(p.returncode)+'\n',encoding='ascii')
print(json.dumps({'exit':p.returncode,'stdoutBytes':len(p.stdout),'stderrBytes':len(p.stderr)},indent=2))
