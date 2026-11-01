from pathlib import Path
import subprocess,json,hashlib,datetime
root=Path("/tmp/issue81-independent-spec-tests-worker-20261022")
h=root/"execution-harness"
out=root/"results"/"initial-bcl"
out.mkdir(parents=True,exist_ok=False)
artifacts=h/"artifacts"
common=["-p:UseArtifactsOutput=true","-p:ArtifactsPath="+str(artifacts),"-p:EnableSourceControlManagerQueries=false","-p:UseSharedCompilation=false","-m:1","--nodeReuse:false"]
commands=[
    ["dotnet","--version"],
    ["dotnet","restore",str(h/"bcl.csproj"),"--source","/home/donabe/.nuget/packages","-p:NuGetAudit=false"]+common,
    ["dotnet","test",str(h/"bcl.csproj"),"--no-restore","--configuration","Debug","--logger","trx;LogFileName=initial-bcl.trx","--results-directory",str(out)]+common,
]
results=[]
for index,command in enumerate(commands):
    started=datetime.datetime.now(datetime.timezone.utc).isoformat()
    result=subprocess.run(command,cwd=h,capture_output=True,text=True)
    (out/(str(index)+".stdout.log")).write_text(result.stdout)
    (out/(str(index)+".stderr.log")).write_text(result.stderr)
    record={"command":command,"cwd":str(h),"started_utc":started,"exit_code":result.returncode,"stdout":str(out/(str(index)+".stdout.log")),"stderr":str(out/(str(index)+".stderr.log"))}
    results.append(record)
    (out/"execution.json").write_text(json.dumps({"baseline_parent_supplied":"6bb88d00338120226d024fe2ac2e717ba7412985","frozen_manifest_sha256":"c790c3d76002a14797a066f4d07989641f1fc5ed80237ba080b8318d99238a14","runs":results},indent=2)+"\n")
    print("COMMAND",index,"EXIT",result.returncode,flush=True)
    print(result.stdout,flush=True)
    print(result.stderr,flush=True)
    if result.returncode:
        print("STOPPED_ON_FAILURE; no test, expectation, or product changes.",flush=True)
        break
