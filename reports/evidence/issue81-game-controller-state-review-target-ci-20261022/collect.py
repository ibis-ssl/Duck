import pathlib,subprocess,json,datetime
r=pathlib.Path("/home/donabe/Duck-issue81-aspire-desktop-bridge")
d=pathlib.Path("/tmp/issue81-game-controller-state-review-target-ci-20261022")
gh="/mnt/c/Program Files/GitHub CLI/gh.exe"
def run(name,args):
 cmd=[gh]+args
 (d/(name+".command.json")).write_text(json.dumps({"argv":cmd,"cwd":str(r),"started_utc":datetime.datetime.now(datetime.timezone.utc).isoformat()},indent=2))
 p=subprocess.run(cmd,cwd=r,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
 (d/(name+".stdout.log")).write_bytes(p.stdout);(d/(name+".stderr.log")).write_bytes(p.stderr)
 (d/(name+".exit.json")).write_text(json.dumps({"exit_code":p.returncode,"ended_utc":datetime.datetime.now(datetime.timezone.utc).isoformat()}))
 return p
