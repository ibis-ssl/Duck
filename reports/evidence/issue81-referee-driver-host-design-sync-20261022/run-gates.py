from pathlib import Path
import subprocess,json,datetime,hashlib
r=Path('/home/donabe/Duck-issue81-aspire-desktop-bridge')
d=Path('/tmp/issue81-referee-driver-host-design-sync-20261022')
mirror=Path('/mnt/c/Users/donabe/AppData/Local/Temp/issue81-referee-driver-host-design-sync-20261022')
win=r'C:\Users\donabe\AppData\Local\Temp\issue81-referee-driver-host-design-sync-20261022'
ps='/mnt/c/Windows/System32/WindowsPowerShell/v1.0/powershell.exe'
docs=['Tracker/Design/Testing/aspire-simulation-test-environment.md','Tracker/Design/Testing/tracker-comparison-debug-design.md']
python='/home/donabe/venvs/duck-pr73-r04-ff50682/bin/python'
yomi='/home/donabe/yomiyasu-v1.1.0-0df4774/scripts/yomiyasu_lint.py'
def run(stage,name,cmd,cwd=None,data=None):
 out=d/stage;out.mkdir(exist_ok=True)
 start=datetime.datetime.now(datetime.timezone.utc).isoformat()
 p=subprocess.run(cmd,cwd=cwd,input=data,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
 (out/(name+'.command.json')).write_text(json.dumps({'command':cmd,'cwd':str(cwd) if cwd else None,'exit_code':p.returncode,'startedUTC':start,'finishedUTC':datetime.datetime.now(datetime.timezone.utc).isoformat()},indent=2))
 (out/(name+'.stdout.log')).write_bytes(p.stdout);(out/(name+'.stderr.log')).write_bytes(p.stderr)
 return p
def stage(name):
 sourceproof=[]
 for f in docs:
  data=(d/'baseline'/f).read_bytes() if name=='baseline' else (r/f).read_bytes()
  p=mirror/f;p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(data)
  sourceproof.append({'path':f,'sourceSHA256':hashlib.sha256(data).hexdigest(),'mirrorSHA256':hashlib.sha256(p.read_bytes()).hexdigest(),'bytes':len(data)})
 (d/name).mkdir(exist_ok=True)
 (d/name/'source-mirror-proof.json').write_text(json.dumps(sourceproof,indent=2))
 for mode in ['targets','text','spell']:
  script='tools/lint/scripts/list-markdown-targets.js' if mode=='targets' else 'tools/lint/scripts/run-markdown-targets.js '+mode
  cmd=[ps,'-NoProfile','-NonInteractive','-Command',f"Set-Location -LiteralPath '{win}'; & 'C:\\Program Files\\nodejs\\node.exe' {script} --files "+' '.join(docs)]
  p=run(name,mode,cmd)
  print(name,mode,p.returncode,flush=True)
 for f in docs:
  short=Path(f).stem
  p=run(name,'sudachi-'+short,[python,str(d/'sudachi-capture.py'),str(mirror/'tools/lint/scripts/check-markdown-whitelist-sudachi.py'),str(d/name/('sudachi-'+short+'.json')),'--stdin',f],cwd=mirror,data=(mirror/f).read_bytes())
  print(name,'sudachi',short,p.returncode,flush=True)
  p=run(name,'yomiyasu-'+short,[python,yomi,str(mirror/f),'--json','--strict'],cwd=mirror)
  print(name,'yomi',short,p.returncode,flush=True)
if __name__=='__main__':
 import sys
 stage(sys.argv[1])
