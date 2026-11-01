from pathlib import Path
import subprocess,json,datetime,hashlib
out=Path("/tmp/issue81-independent-spec-tests-worker-20261022/results/additional-v2-fixed-ci-attempt2")
gh="/mnt/c/Program Files/GitHub CLI/gh.exe"
records=[]
def run(name,args):
    command=[gh]+args; start=datetime.datetime.now(datetime.timezone.utc).isoformat()
    r=subprocess.run(command,capture_output=True,text=True)
    (out/(name+".stdout.log")).write_text(r.stdout); (out/(name+".stderr.log")).write_text(r.stderr)
    records.append({"command":command,"started_utc":start,"exit_code":r.returncode,"stdout":str(out/(name+".stdout.log")),"stderr":str(out/(name+".stderr.log"))})
    (out/"commands.json").write_text(json.dumps(records,indent=2)+"\n")
    print(name,"EXIT",r.returncode,flush=True)
    return r
meta_args=["api","repos/ibis-ssl/Duck/actions/runs/39776500808","--jq","{id,head_sha,event,status,conclusion,run_attempt}"]
meta=run("initial-metadata",meta_args)
if meta.returncode: raise SystemExit(meta.returncode)
m=json.loads(meta.stdout)
assert m["head_sha"]=="2ff2b1facaf8c8e4664ca6141f02944e0b74f89b"
assert m["event"]=="pull_request" and m["run_attempt"]==2
print(meta.stdout,flush=True)
view_args=["run","view","39776500808","--attempt","2","--repo","ibis-ssl/Duck","--json","headSha,event,status,conclusion,jobs,url"]
r=run("initial-view",view_args)
if r.returncode: raise SystemExit(r.returncode)
state=json.loads(r.stdout)
print(r.stdout,flush=True)
if state["status"]!="completed":
    watch=run("watch",["run","watch","39776500808","--repo","ibis-ssl/Duck","--interval","30","--exit-status"])
    print(watch.stdout,flush=True); print(watch.stderr,flush=True)
r=run("final-view",view_args)
if r.returncode: raise SystemExit(r.returncode)
state=json.loads(r.stdout)
print(r.stdout,flush=True)
meta=run("final-metadata",meta_args)
m=json.loads(meta.stdout)
assert m["head_sha"]=="2ff2b1facaf8c8e4664ca6141f02944e0b74f89b"
assert m["event"]=="pull_request" and m["run_attempt"]==2 and m["status"]=="completed"
print(meta.stdout,flush=True)
job=next(j for j in state["jobs"] if j["name"]=="test")
log=run("test-job-log",["run","view","39776500808","--attempt","2","--repo","ibis-ssl/Duck","--job",str(job["databaseId"]),"--log"])
keys=["Issue81.IndependentSpecificationTests","ComparisonResolverAdditionalSpecificationTests","VisionStoreAdditionalSpecificationTests","ResourceReadinessSpecificationTests","ServiceReceiveSpecificationTests","TrackerDatagramSpecificationTests","Passed!","Failed!","Test Run","Total tests:"]
matches=[line for line in log.stdout.splitlines() if any(k in line for k in keys)]
(out/"own-test-matches.log").write_text("\n".join(matches)+"\n")
print("\n".join(matches),flush=True)
art=run("artifacts",["api","repos/ibis-ssl/Duck/actions/runs/39776500808/artifacts"])
artifact_data=json.loads(art.stdout)
current=[a for a in artifact_data["artifacts"] if a["name"].endswith("-2")]
print(art.stdout,flush=True)
summary={"stage":"fixed_head_same_run_attempt2","run_id":39776500808,"run_attempt":m["run_attempt"],"head_sha":m["head_sha"],"event":m["event"],"conclusion":m["conclusion"],"test_job":job,"all_artifact_metadata":artifact_data,"attempt2_artifacts":current,"attempt1_history":"fixed own67 Passed individually in attempt1 TRX; suite614Passed+1Failed retained separately","rerun_authority":"parent executed gh run rerun --failed once; this worker only reads","original_failure_cause":"not determined or declared repaired by rerun outcome","product_tests_expectations":"unchanged per parent; product/old test body unread","process_state":"all subprocess calls completed","files":[{"path":str(p),"sha256":hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(out.iterdir()) if p.is_file() and p.name!="ci-evidence.json"]}
(out/"ci-evidence.json").write_text(json.dumps(summary,ensure_ascii=False,indent=2)+"\n")
print("evidence_sha256",hashlib.sha256((out/"ci-evidence.json").read_bytes()).hexdigest(),flush=True)
