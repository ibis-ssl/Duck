"""Windowsの四資源受入。閾値は固定し、通常停止と所有ID消失を別々に記録する。"""
import argparse
import datetime
import hashlib
import json
import math
import os
from pathlib import Path
import re
import signal
import subprocess
import sys
import time
import uuid

ROLES = frozenset(("simulator", "duck", "game-controller", "referee-driver"))
RUN_LABEL = "com.ibis-ssl.duck.run-id"
ROLE_LABEL = "com.ibis-ssl.duck.role"
FULL_ID = re.compile(r"[0-9a-f]{64}\Z")
POLICY = {"startupSeconds": 45, "evaluationSeconds": 30, "minimumSamples": 10,
          "allowedFailures": 0, "maximumSampleGapSeconds": 5,
          "udpFreshnessSeconds": 5, "apiFreshnessSeconds": 3,
          "shutdownSeconds": 15, "overallSeconds": 120}


def require(condition, reason):
    if not condition:
        raise ValueError(reason)


def finite_number(value):
    require(type(value) in (int, float) and math.isfinite(value), "InvalidNumber")
    return value


def age_seconds(value):
    match = re.fullmatch(r"(?:(\d+)\.)?(\d{2}):([0-5]\d):([0-5]\d(?:\.\d{1,7})?)", str(value))
    require(match is not None, "InvalidAge")
    days, hours, minutes, seconds = match.groups()
    return int(days or 0) * 86400 + int(hours) * 3600 + int(minutes) * 60 + float(seconds)


def sample_evidence(probe, run_id, container_id):
    """HTTPの一致に加え、返却された帰属・鮮度と進行を検査する。"""
    require(probe.get("dockerExit") == 0 and not probe.get("error"), "TransportFailure")
    require(probe.get("httpStatus") == 200, "HttpNotReady")
    body = probe["body"]
    diagnostic, api = body["diagnostic"], body["api"]
    require(body.get("isReady") is True and body.get("reason") == "Matched", "NotMatched")
    require(diagnostic.get("isReady") is True and diagnostic.get("decodeStatus") == 1
            and diagnostic.get("inputId") == "referee", "InputNotReady")
    identity = diagnostic["identity"]
    require(identity["runId"] == run_id and identity["containerId"] == container_id
            and FULL_ID.fullmatch(container_id), "OwnerMismatch")
    require(isinstance(identity["resourceId"], str) and bool(identity["resourceId"]), "MissingResource")
    for value in (identity["generation"], diagnostic["receiverEpoch"], api["endpointGeneration"],
                  diagnostic["successfullyDecodedCount"], api["requestSequence"], api["readStartedTimestamp"]):
        require(type(value) is int and value > 0, "MissingProgress")
    require(diagnostic["decodeFailureCount"] == 0, "DecodeFailure")
    require(bool(diagnostic["sourceKey"]), "MissingSource")
    require(api["endpoint"] == "ws://game-controller:8082/api/control", "ApiEndpointMismatch")
    require(type(api["readCompletedTimestamp"]) is int
            and api["readCompletedTimestamp"] >= api["readStartedTimestamp"], "InvalidApiInterval")
    require(all(type(api["state"][key]) is int and api["state"][key] >= 0
                for key in ("command", "phase")), "MissingApiState")
    duration = finite_number(probe["durationSeconds"])
    require(duration >= 0, "InvalidDuration")
    require(age_seconds(diagnostic["decodeAgeUpperBound"]) + duration <= POLICY["udpFreshnessSeconds"],
            "UdpStaleAtCompletion")
    require(age_seconds(api["ageUpperBound"]) + duration <= POLICY["apiFreshnessSeconds"],
            "ApiStaleAtCompletion")
    received = datetime.datetime.fromisoformat(diagnostic["lastReceivedAt"])
    require(received.tzinfo is not None, "MissingReceiveTimezone")
    context = (identity["resourceId"], identity["generation"], diagnostic["receiverEpoch"],
               diagnostic["sourceKey"], api["endpoint"], api["endpointGeneration"])
    # 受信UTCは表示・形式確認用。進行と鮮度を壁時計の補正に依存させない。
    progress = (diagnostic["successfullyDecodedCount"],
                api["requestSequence"], api["readStartedTimestamp"])
    return context, progress


def evaluate_readiness(probes, run_id, container_id):
    """最初の有効な一致で評価を一度だけ開始し、途中の失敗を保持する。"""
    result = {"passed": False, "evaluationStartedSeconds": None, "evaluationComplete": False,
              "samples": 0, "readySamples": 0, "failures": []}
    context = previous = previous_at = None
    for index, probe in enumerate(probes):
        try:
            at = finite_number(probe["elapsedSeconds"])
            require(at >= 0, "InvalidElapsedTime")
            evidence = sample_evidence(probe, run_id, container_id)
            problem = None
        except (ValueError, KeyError, TypeError, AttributeError) as ex:
            at = probe.get("elapsedSeconds")
            evidence, problem = None, str(ex)
        if result["evaluationStartedSeconds"] is None:
            if evidence is None:
                continue
            if at > POLICY["startupSeconds"]:
                result["failures"].append({"sample": index, "reason": "StartupDeadlineExceeded"})
                break
            result["evaluationStartedSeconds"] = at
            context = evidence[0]
        result["samples"] += 1
        if previous_at is not None:
            if type(at) not in (int, float) or not math.isfinite(at) or not 0 < at - previous_at <= POLICY["maximumSampleGapSeconds"]:
                problem = problem or "SampleGapOrOrder"
        if evidence is not None:
            if evidence[0] != context:
                problem = problem or "EvidenceContextChanged"
            if previous is not None and not all(new > old for new, old in zip(evidence[1], previous)):
                problem = problem or "EvidenceDidNotAdvance"
            previous = evidence[1]
        if problem:
            result["failures"].append({"sample": index, "reason": problem})
        else:
            result["readySamples"] += 1
        previous_at = at
        if type(at) in (int, float) and math.isfinite(at) and at >= result["evaluationStartedSeconds"] + POLICY["evaluationSeconds"]:
            result["evaluationComplete"] = True
            break
    if result["evaluationStartedSeconds"] is None:
        result["failures"].append({"reason": "NoReadyWithinStartup"})
    result["passed"] = (result["evaluationComplete"] and result["samples"] >= POLICY["minimumSamples"]
                        and len(result["failures"]) <= POLICY["allowedFailures"])
    return result


def wait_for_owned_removal(owned_ids, list_ids, deadline, clock=time.monotonic, sleep=time.sleep):
    """記録済みの完全IDだけを期限内で確認する。失敗した一覧取得は再試行しない。"""
    owned = set(owned_ids)
    result = {"status": "timeout", "remainingIds": sorted(owned), "unrelatedIds": [], "polls": []}
    if not owned or not all(FULL_ID.fullmatch(cid) for cid in owned):
        result["status"] = "ownership_unproven"
        return result
    started = clock()
    while clock() < deadline:
        try:
            current = set(list_ids(deadline - clock()))
            require(all(FULL_ID.fullmatch(cid) for cid in current), "InvalidFullContainerId")
        except Exception as ex:
            result.update(status="list_failed", error=type(ex).__name__ + ": " + str(ex))
            return result
        result["remainingIds"] = sorted(owned & current)
        result["unrelatedIds"] = sorted(current - owned)
        result["polls"].append({"elapsedSeconds": clock() - started,
                                "remainingIds": result["remainingIds"],
                                "unrelatedIds": result["unrelatedIds"]})
        if clock() >= deadline:
            break
        if not result["remainingIds"]:
            result["status"] = "complete"
            return result
        sleep(min(0.5, deadline - clock()))
    return result


def assess_run(readiness, removal, apphost_exit, roles, errors, ctrl_break_sent):
    return (readiness["passed"] and removal["status"] == "complete" and
            apphost_exit == 0 and set(roles) == ROLES and not errors and ctrl_break_sent)


def utc_now():
    return datetime.datetime.now(datetime.timezone.utc).isoformat()


def save(path, value):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


class DesktopRun:
    """既存のWindows環境だけを使う。Docker設定変更、pull、rm、強制停止は行わない。"""
    def __init__(self, args, folder):
        self.args, self.folder = args, folder
        self.repo = Path(__file__).resolve().parents[1]
        self.deadline = time.monotonic() + POLICY["overallSeconds"]
        self.command_number = 0

    def call(self, argv, timeout=5):
        remaining = min(timeout, self.deadline - time.monotonic())
        require(remaining > 0, "OverallDeadlineExceeded")
        self.command_number += 1
        prefix = self.folder / ("command-%03d" % self.command_number)
        save(prefix.with_suffix(".json"), {"argv": [str(a) for a in argv], "timeoutSeconds": remaining})
        try:
            completed = subprocess.run([str(a) for a in argv], cwd=self.repo, capture_output=True,
                                       timeout=remaining, stdin=subprocess.DEVNULL)
            prefix.with_suffix(".stdout.log").write_bytes(completed.stdout)
            prefix.with_suffix(".stderr.log").write_bytes(completed.stderr)
            prefix.with_suffix(".exit.txt").write_text(str(completed.returncode))
            require(completed.returncode == 0,
                    "CommandFailed: " + completed.stderr.decode("utf-8", errors="replace")[:300])
            return completed.stdout.decode("utf-8", errors="replace").strip()
        except subprocess.TimeoutExpired as ex:
            prefix.with_suffix(".stdout.log").write_bytes(ex.stdout or b"")
            prefix.with_suffix(".stderr.log").write_bytes(ex.stderr or b"")
            prefix.with_suffix(".exit.txt").write_text("timeout")
            raise

    def docker(self, *args, timeout=5):
        return self.call([self.args.docker, "--context", self.args.context, *args], timeout)

    def check_engine(self, timeout=5):
        require(self.docker("info", "--format", "{{.ID}}", timeout=timeout) == self.args.engine_id,
                "DockerEngineChanged")

    def list_ids(self, remaining=5):
        # 一覧の前後の接続先照合も同じ停止期限に含める。
        deadline = min(self.deadline, time.monotonic() + remaining)
        def budget():
            available = deadline - time.monotonic()
            require(available > 0, "InventoryDeadlineExceeded")
            return available
        self.check_engine(timeout=budget())
        ids = self.docker("ps", "-a", "--no-trunc", "--format", "{{.ID}}",
                          timeout=budget()).splitlines()
        require(all(FULL_ID.fullmatch(cid) for cid in ids), "InvalidContainerInventory")
        self.check_engine(timeout=budget())
        return ids

    def own_resources(self, run_id):
        ids = self.docker("ps", "-a", "--no-trunc", "--filter", "label=" + RUN_LABEL + "=" + run_id,
                          "--format", "{{.ID}}").splitlines()
        result = {}
        for cid in ids:
            require(FULL_ID.fullmatch(cid), "IncompleteContainerId")
            labels = json.loads(self.docker("inspect", cid, "--format", "{{json .Config.Labels}}"))
            require(labels.get(RUN_LABEL) == run_id and labels.get(ROLE_LABEL) in ROLES, "OwnerLabelMismatch")
            require(labels[ROLE_LABEL] not in result.values(), "DuplicateOwnedRole")
            result[cid] = labels[ROLE_LABEL]
        return result

    def preflight(self):
        require(os.name == "nt", "WindowsRequired")
        require(self.call(["git", "rev-parse", "HEAD"]) == self.args.expected_head, "HeadMismatch")
        require(not self.call(["git", "status", "--porcelain"]), "WorktreeNotClean")
        require(not self.list_ids(), "ExistingContainersRequireSeparateReview")
        # ローカルに揃った依存だけを使い、欠落時は起動しない。
        for image in (
            "ghcr.io/ibis-ssl/framework-simulatorcli@sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79",
            "robocupssl/ssl-game-controller@sha256:d62d772de063c2951f600a0b7898803f97822f4f46a412a731e42f7d82a99dc7",
            "mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3",
            "mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca"):
            self.docker("image", "inspect", image, "--format", "{{.Id}}")
        files = ("Testing/Duck.Testing.AppHost/bin/Debug/net10.0/Duck.Testing.AppHost.dll",
                 "Testing/Duck.Testing.RefereeDriver.Host/bin/Release/net10.0/publish/Duck.Testing.RefereeDriver.Host.dll",
                 "Tracker/Tracker.RuntimeHost/bin/Release/net10.0/publish/Tracker.RuntimeHost.dll")
        return {name: hashlib.sha256((self.repo / name).read_bytes()).hexdigest() for name in files}

    def probe(self, cid, begun):
        started = time.monotonic()
        request = 'exec 3<>/dev/tcp/127.0.0.1/39110; printf "GET /health/referee HTTP/1.0\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n" >&3; cat <&3'
        raw = self.docker("exec", cid, "/bin/bash", "-c", request, timeout=4.5)
        status = re.search(r"HTTP/1\.[01]\s+(\d{3})", raw)
        parts = re.split(r"\r?\n\r?\n", raw, maxsplit=1)
        return {"utc": utc_now(), "elapsedSeconds": time.monotonic() - begun,
                "durationSeconds": time.monotonic() - started, "dockerExit": 0, "error": "",
                "httpStatus": int(status.group(1)) if status else None,
                "body": json.loads(parts[1]) if len(parts) == 2 else None}

    def execute(self, metadata):
        run_id = metadata["runId"]
        env = os.environ.copy()
        env["PATH"] = str(Path(self.args.docker).parent) + os.pathsep + env.get("PATH", "")
        env["DOCKER_CONTEXT"] = self.args.context
        env.update({
            "Testing__GameControllerVisionOnly": "true",
            "Testing__IdentityControl__DesktopBridgeEnabled": "true",
            "Testing__IdentityControl__DockerContext": self.args.context,
            "Testing__IdentityControl__ExpectedEngineId": self.args.engine_id,
            "Testing__IdentityControl__DockerExecutable": self.args.docker,
            "Testing__IdentityControl__SafeTracePath": str(self.folder / "identity.safe.jsonl"),
            "Testing__Diagnostics__RunId": run_id})
        # AppHost既定の共通所有ロックを使う。実行ごとの別ロックへ置き換えない。
        env.pop("Testing__StackOwnership__LockPath", None)
        info = {"head": self.args.expected_head, "runId": run_id, "errors": [], "ctrlBreakSent": False,
                "apphostPid": None, "apphostExit": None, "ownedResources": {}, "probes": [],
                "removal": {"status": "not_started"}, "startedUtc": utc_now()}
        process, begun, cid = None, time.monotonic(), None
        with (self.folder / "apphost.stdout.log").open("wb") as stdout, (self.folder / "apphost.stderr.log").open("wb") as stderr:
            try:
                apphost = self.repo / "Testing/Duck.Testing.AppHost/bin/Debug/net10.0/Duck.Testing.AppHost.dll"
                process = subprocess.Popen([self.args.dotnet, str(apphost)], cwd=self.repo, env=env,
                                           creationflags=subprocess.CREATE_NEW_PROCESS_GROUP,
                                           stdin=subprocess.DEVNULL, stdout=stdout, stderr=stderr)
                info["apphostPid"] = process.pid
                while time.monotonic() < self.deadline - POLICY["shutdownSeconds"]:
                    require(process.poll() is None, "AppHostExitedDuringEvaluation")
                    current = self.own_resources(run_id)
                    info["ownedResources"].update(current)
                    require(len(set(info["ownedResources"].values())) == len(info["ownedResources"]),
                            "OwnedResourceReplaced")
                    cid = next((key for key, role in current.items() if role == "referee-driver"), None)
                    if cid:
                        probe = self.probe(cid, begun)
                        info["probes"].append(probe)
                        with (self.folder / "health-probes.jsonl").open("a", encoding="utf-8") as log:
                            log.write(json.dumps(probe, ensure_ascii=False) + "\n")
                    assessment = evaluate_readiness(info["probes"], run_id, cid or "")
                    if assessment["evaluationComplete"]:
                        break
                    if assessment["evaluationStartedSeconds"] is None and time.monotonic() - begun >= POLICY["startupSeconds"]:
                        break
                    time.sleep(min(2, max(0, self.deadline - POLICY["shutdownSeconds"] - time.monotonic())))
            except Exception as ex:
                info["errors"].append(type(ex).__name__ + ": " + str(ex))
            finally:
                stop_at = time.monotonic()
                stop_deadline = min(stop_at + POLICY["shutdownSeconds"], self.deadline)
                info["stopRequestedUtc"] = utc_now()
                if process is not None:
                    if process.poll() is None:
                        try:
                            process.send_signal(signal.CTRL_BREAK_EVENT)
                            info["ctrlBreakSent"] = True
                            process.wait(timeout=max(0.001, stop_deadline - time.monotonic()))
                        except Exception as ex:
                            info["errors"].append("NormalStop: " + type(ex).__name__ + ": " + str(ex))
                    info["apphostExit"] = process.poll()
                    info["apphostExitObservedSeconds"] = time.monotonic() - stop_at
                info["removal"] = wait_for_owned_removal(info["ownedResources"], self.list_ids, stop_deadline)
                if info["removal"]["status"] == "complete":
                    try:
                        self.check_engine()
                        info["lateOwnedResources"] = self.own_resources(run_id)
                        require(not info["lateOwnedResources"], "UnrecordedOwnedResourcesRemain")
                    except Exception as ex:
                        info["errors"].append("FinalOwnershipCheck: " + str(ex))
                info["readiness"] = evaluate_readiness(info["probes"], run_id, cid or "")
                info["passed"] = assess_run(info["readiness"], info["removal"], info["apphostExit"],
                                            info["ownedResources"].values(), info["errors"], info["ctrlBreakSent"])
                info["endedUtc"] = utc_now()
                save(self.folder / "result.safe.json", info)
        print(json.dumps({key: info[key] for key in ("passed", "apphostPid", "apphostExit", "readiness", "removal", "errors")},
                         ensure_ascii=False), flush=True)
        return 0 if info["passed"] else 2


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument("--preflight-only", action="store_true")
    mode.add_argument("--run-once", action="store_true")
    parser.add_argument("--expected-head", required=True)
    parser.add_argument("--engine-id", required=True)
    parser.add_argument("--docker", required=True)
    parser.add_argument("--dotnet", default=r"C:\Program Files\dotnet\dotnet.exe")
    parser.add_argument("--context", default="desktop-linux")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    args.output = args.output.resolve()
    args.output.mkdir(parents=True, exist_ok=False)
    metadata = {"head": args.expected_head, "runId": uuid.uuid4().hex, "policy": POLICY,
                "context": args.context, "engineId": args.engine_id, "startedUtc": utc_now(),
                "mode": "run-once" if args.run_once else "preflight-only"}
    save(args.output / "run-meta.safe.json", metadata)
    try:
        runner = DesktopRun(args, args.output)
        metadata["preflight"] = runner.preflight()
        save(args.output / "run-meta.safe.json", metadata)
        if args.preflight_only:
            print(json.dumps(metadata, ensure_ascii=False))
            return 0
        return runner.execute(metadata)
    except Exception as ex:
        save(args.output / "preflight-failure.json", {"error": type(ex).__name__ + ": " + str(ex)})
        print(type(ex).__name__ + ": " + str(ex), file=sys.stderr)
        return 3


if __name__ == "__main__":
    sys.exit(main())
