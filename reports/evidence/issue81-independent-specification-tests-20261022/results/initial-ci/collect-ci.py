from pathlib import Path
import subprocess,json,datetime
out=Path("/tmp/issue81-independent-spec-tests-worker-20261022/results/initial-ci")
gh="/mnt/c/Program Files/GitHub CLI/gh.exe"
records=[]
def run(name,args):
    started=datetime.datetime.now(datetime.timezone.utc).isoformat()
    command=[gh]+args
    result=subprocess.run(command,capture_output=True,text=True)
    (out/(name+".stdout.log")).write_text(result.stdout)
    (out/(name+".stderr.log")).write_text(result.stderr)
    records.append({"command":command,"started_utc":started,"exit_code":result.returncode,"stdout":str(out/(name+".stdout.log")),"stderr":str(out/(name+".stderr.log"))})
    (out/"commands.json").write_text(json.dumps(records,indent=2)+"\n")
    print(name,"EXIT",result.returncode,flush=True)
    return result
args=["run","view","39770570031","--repo","ibis-ssl/Duck","--json","headSha,event,status,conclusion,jobs,url"]
first=run("initial-view",args)
if first.returncode: raise SystemExit(first.returncode)
state=json.loads(first.stdout)
assert state["headSha"]=="83588ac908c9169e48f64917572a8ca2a4379e02"
assert state["event"]=="pull_request"
print(first.stdout,flush=True)
if state["status"]!="completed":
    watch=run("watch",["run","watch","39770570031","--repo","ibis-ssl/Duck","--interval","30","--exit-status"])
    print(watch.stdout,flush=True)
    print(watch.stderr,flush=True)
final=run("final-view",args)
print(final.stdout,flush=True)
print(final.stderr,flush=True)
