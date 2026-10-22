from pathlib import Path
import subprocess,json,datetime,hashlib
out=Path("/tmp/issue81-independent-spec-tests-worker-20261022/results/additional-v2-fixed-ci")
gh="/mnt/c/Program Files/GitHub CLI/gh.exe"
records=[]
def run(name,args):
    command=[gh]+args
    start=datetime.datetime.now(datetime.timezone.utc).isoformat()
    result=subprocess.run(command,capture_output=True,text=True)
    (out/(name+".stdout.log")).write_text(result.stdout)
    (out/(name+".stderr.log")).write_text(result.stderr)
    records.append({"command":command,"started_utc":start,"exit_code":result.returncode,"stdout":str(out/(name+".stdout.log")),"stderr":str(out/(name+".stderr.log"))})
    (out/"commands.json").write_text(json.dumps(records,indent=2)+"\n")
    print(name,"EXIT",result.returncode,flush=True)
    return result
args=["run","view","39776500808","--repo","ibis-ssl/Duck","--json","headSha,event,status,conclusion,jobs,url"]
r=run("initial-view",args)
if r.returncode: raise SystemExit(r.returncode)
state=json.loads(r.stdout)
print(r.stdout,flush=True)
assert state["headSha"]=="2ff2b1facaf8c8e4664ca6141f02944e0b74f89b"
assert state["event"]=="pull_request"
if state["status"]!="completed":
    w=run("watch",["run","watch","39776500808","--repo","ibis-ssl/Duck","--interval","30","--exit-status"])
    print(w.stdout,flush=True)
    print(w.stderr,flush=True)
r=run("final-view",args)
print(r.stdout,flush=True)
if r.returncode: raise SystemExit(r.returncode)
state=json.loads(r.stdout)
assert state["headSha"]=="2ff2b1facaf8c8e4664ca6141f02944e0b74f89b"
assert state["event"]=="pull_request" and state["status"]=="completed"
job=next(j for j in state["jobs"] if j["name"]=="test")
log=run("test-job-log",["run","view","39776500808","--repo","ibis-ssl/Duck","--job",str(job["databaseId"]),"--log"])
keys=["Issue81.IndependentSpecificationTests","ComparisonResolverAdditionalSpecificationTests","VisionStoreAdditionalSpecificationTests","ResourceReadinessSpecificationTests","ServiceReceiveSpecificationTests","TrackerDatagramSpecificationTests","Passed!","Test Run","Total tests:","Passed:","Failed:"]
matches=[line for line in log.stdout.splitlines() if any(k in line for k in keys)]
(out/"own-test-matches.log").write_text("\n".join(matches)+"\n")
print("\n".join(matches),flush=True)
a=run("artifacts",["api","repos/ibis-ssl/Duck/actions/runs/39776500808/artifacts"])
print(a.stdout,flush=True)
summary={"stage":"additional_v2_same_frozen_tests_after_implementation_fix","head_sha":state["headSha"],"run_id":39776500808,"event":state["event"],"status":state["status"],"conclusion":state["conclusion"],"url":state["url"],"test_job":job,"artifacts":json.loads(a.stdout) if a.returncode==0 else None,"processes":"all subprocess.run calls completed","product_tests_expectations_delta":"none per parent; no product inspection","initial_history":"v1 BCL28/28 and initial/published-head CI history retained separately; additional-v2 initial CI39775288708 four failures retained; this is fixed-head rerun","individual_case_limit":"success logs may contain aggregate only; no individual own-case claim without names/TRX","additional_delta_retry":"not_performed","repo_writes":"none","files":[{"path":str(p),"sha256":hashlib.sha256(p.read_bytes()).hexdigest()} for p in sorted(out.iterdir()) if p.is_file() and p.name!="ci-evidence.json"]}
(out/"ci-evidence.json").write_text(json.dumps(summary,ensure_ascii=False,indent=2)+"\n")
print("evidence_sha256",hashlib.sha256((out/"ci-evidence.json").read_bytes()).hexdigest(),flush=True)
