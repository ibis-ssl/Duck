"""実機やDockerを起動しない、受入判定と終了待機の回帰試験。"""
import copy
import datetime
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import verify_desktop_referee_acceptance as acceptance

RUN = "run-fixture"
CID = "a" * 64
OTHER = "b" * 64

def sample(elapsed):
    return {
        "elapsedSeconds": elapsed, "durationSeconds": 0.1, "dockerExit": 0,
        "httpStatus": 200, "error": "",
        "body": {
            "isReady": True, "reason": "Matched",
            "diagnostic": {
                "identity": {"runId": RUN, "resourceId": "driver-resource",
                             "containerId": CID, "generation": 1},
                "inputId": "referee", "receiverEpoch": 2, "sourceKey": "172.18.0.4:54211",
                "successfullyDecodedCount": int(elapsed * 20) + 1, "decodeFailureCount": 0,
                "lastReceivedAt": (datetime.datetime(2026, 10, 26, tzinfo=datetime.timezone.utc)
                                   + datetime.timedelta(seconds=elapsed)).isoformat(),
                "decodeAgeUpperBound": "00:00:00.0200000", "decodeStatus": 1,
                "isReady": True, "reason": "Ready"},
            "api": {"state": {"command": 1, "phase": 1},
                    "endpoint": "ws://game-controller:8082/api/control",
                    "endpointGeneration": 1, "requestSequence": int(elapsed * 10) + 1,
                    "readStartedTimestamp": int(elapsed * 1000) + 1,
                    "readCompletedTimestamp": int(elapsed * 1000) + 2,
                    "ageUpperBound": "00:00:00.0500000"}}}

def stable(start=0):
    return [sample(start + i * 2) for i in range(16)]

class ReadinessTests(unittest.TestCase):
    def evaluate(self, probes):
        return acceptance.evaluate_readiness(probes, RUN, CID)["passed"]

    def test_thirty_seconds_of_fresh_matching_samples_passes(self):
        self.assertTrue(self.evaluate(stable()))

    def test_single_ready_cannot_pass(self):
        self.assertFalse(self.evaluate([sample(0)]))

    def test_two_ready_of_thirty_cannot_pass(self):
        probes = [sample(i * 2) for i in range(30)]
        for index, probe in enumerate(probes):
            if index not in (2, 8):
                probe["httpStatus"] = 503
                probe["body"]["isReady"] = False
        self.assertFalse(self.evaluate(probes))

    def test_startup_is_separate_from_fixed_evaluation_window(self):
        pending = sample(0)
        pending["httpStatus"] = 503
        self.assertTrue(self.evaluate([pending, *stable(10)]))

    def test_first_ready_after_startup_deadline_fails(self):
        self.assertFalse(self.evaluate(stable(46)))

    def test_failure_does_not_restart_evaluation_window(self):
        probes = stable() + stable(32)
        probes[5]["httpStatus"] = 503
        self.assertFalse(self.evaluate(probes))

    def test_evaluation_requires_full_duration_and_sample_count(self):
        self.assertFalse(self.evaluate(stable()[:-1]))
        self.assertFalse(self.evaluate([sample(i * 5) for i in range(7)]))

    def test_large_gap_is_not_continuous_success(self):
        probes = [sample(i) for i in range(10)] + [sample(30)]
        self.assertFalse(self.evaluate(probes))

    def test_evidence_must_be_fresh_at_probe_completion(self):
        for field, age in (("diagnostic", "00:00:04.9500000"), ("api", "00:00:02.9500000")):
            with self.subTest(field=field):
                probes = stable()
                key = "decodeAgeUpperBound" if field == "diagnostic" else "ageUpperBound"
                probes[4]["body"][field][key] = age
                self.assertFalse(self.evaluate(probes))

    def test_owner_epoch_source_and_api_generation_cannot_change(self):
        mutations = [
            lambda b: b["diagnostic"]["identity"].update(runId="other-run"),
            lambda b: b["diagnostic"]["identity"].update(containerId=OTHER),
            lambda b: b["diagnostic"]["identity"].update(resourceId="other-resource"),
            lambda b: b["diagnostic"]["identity"].update(generation=2),
            lambda b: b["diagnostic"].update(receiverEpoch=3),
            lambda b: b["diagnostic"].update(sourceKey="172.18.0.9:1"),
            lambda b: b["api"].update(endpointGeneration=2),
            lambda b: b["api"].update(endpoint="ws://other/api/control"),
        ]
        for index, mutate in enumerate(mutations):
            with self.subTest(mutation=index):
                probes = stable()
                mutate(probes[4]["body"])
                self.assertFalse(self.evaluate(probes))

    def test_receive_utc_is_display_only_when_monotonic_evidence_advances(self):
        probes = stable()
        for probe in probes:
            probe["body"]["diagnostic"]["lastReceivedAt"] = "2026-10-26T00:00:00+00:00"
        self.assertTrue(self.evaluate(probes))

    def test_old_packet_or_api_read_cannot_be_reused(self):
        mutations = [
            lambda b, p: b["diagnostic"].update(successfullyDecodedCount=p["diagnostic"]["successfullyDecodedCount"]),
            lambda b, p: b["diagnostic"].update(lastReceivedAt="invalid-receive-time"),
            lambda b, p: b["api"].update(requestSequence=p["api"]["requestSequence"]),
            lambda b, p: b["api"].update(readStartedTimestamp=p["api"]["readStartedTimestamp"]),
        ]
        for index, mutate in enumerate(mutations):
            with self.subTest(mutation=index):
                probes = stable()
                mutate(probes[4]["body"], probes[3]["body"])
                self.assertFalse(self.evaluate(probes))

    def test_unmatched_decode_failure_and_transport_error_fail(self):
        mutations = [
            lambda p: p["body"].update(reason="UdpContextChanged"),
            lambda p: p["body"]["diagnostic"].update(decodeFailureCount=1),
            lambda p: p["body"]["diagnostic"].update(isReady=False),
            lambda p: p.update(dockerExit=1, error="Access denied"),
            lambda p: p["body"].update(api=None),
            lambda p: p["body"]["api"].update(state=None),
            lambda p: p["body"]["api"].update(ageUpperBound="NaN"),
        ]
        for index, mutate in enumerate(mutations):
            with self.subTest(mutation=index):
                probes = stable()
                mutate(probes[4])
                self.assertFalse(self.evaluate(probes))

class FakeClock:
    def __init__(self):
        self.now = 0.0
    def clock(self):
        return self.now
    def sleep(self, seconds):
        self.now += seconds

class RemovalTests(unittest.TestCase):
    def wait(self, snapshots, deadline=3):
        clock = FakeClock()
        calls = []
        def listing(remaining):
            calls.append(remaining)
            value = snapshots[min(len(calls) - 1, len(snapshots) - 1)]
            if isinstance(value, Exception):
                raise value
            return value
        result = acceptance.wait_for_owned_removal({CID}, listing, deadline, clock.clock, clock.sleep)
        return result, calls, clock

    def test_immediate_removal_passes(self):
        result, calls, _ = self.wait([[]])
        self.assertEqual("complete", result["status"])
        self.assertEqual(1, len(calls))

    def test_delayed_removal_within_deadline_passes(self):
        result, calls, _ = self.wait([[CID], [CID], []])
        self.assertEqual("complete", result["status"])
        self.assertEqual(3, len(calls))

    def test_residual_owned_id_times_out_with_bounded_wait(self):
        result, calls, clock = self.wait([[CID]])
        self.assertEqual("timeout", result["status"])
        self.assertEqual([CID], result["remainingIds"])
        self.assertGreater(len(calls), 1)
        self.assertLessEqual(clock.now, 3)

    def test_unrelated_container_is_recorded_but_not_owned(self):
        result, _, _ = self.wait([[OTHER]])
        self.assertEqual("complete", result["status"])
        self.assertEqual([OTHER], result["unrelatedIds"])

    def test_list_failure_is_unknown_and_not_retried(self):
        result, calls, _ = self.wait([OSError("Access denied"), []])
        self.assertEqual("list_failed", result["status"])
        self.assertEqual(1, len(calls))

    def test_list_finishing_after_deadline_cannot_pass(self):
        clock = FakeClock()
        def slow(_):
            clock.now += 4
            return []
        result = acceptance.wait_for_owned_removal({CID}, slow, 3, clock.clock, clock.sleep)
        self.assertEqual("timeout", result["status"])

    def test_empty_or_short_owned_id_is_not_cleanup_proof(self):
        for owned in (set(), {"a"}):
            with self.subTest(owned=owned):
                result = acceptance.wait_for_owned_removal(owned, lambda _: [], 3)
                self.assertEqual("ownership_unproven", result["status"])

    def test_abnormal_exit_or_incomplete_ownership_cannot_pass(self):
        good = {"passed": True}
        removed = {"status": "complete"}
        self.assertTrue(acceptance.assess_run(good, removed, 0, acceptance.ROLES, [], True))
        self.assertFalse(acceptance.assess_run(good, removed, 7, acceptance.ROLES, [], True))
        self.assertFalse(acceptance.assess_run(good, removed, 0, {"duck"}, [], True))
        self.assertFalse(acceptance.assess_run(good, removed, 0, acceptance.ROLES, ["denied"], True))
        self.assertFalse(acceptance.assess_run(good, removed, 0, acceptance.ROLES, [], False))


class DockerAdapterTests(unittest.TestCase):
    def runner(self):
        from types import SimpleNamespace
        runner = object.__new__(acceptance.DesktopRun)
        runner.args = SimpleNamespace(engine_id="expected-engine")
        runner.deadline = acceptance.time.monotonic() + 100
        return runner

    def test_engine_check_and_inventory_share_the_shutdown_deadline(self):
        from unittest.mock import patch
        runner, clock, calls = self.runner(), FakeClock(), []
        def docker(*args, timeout=5):
            calls.append((args, timeout))
            clock.sleep(0.05)
            return "expected-engine" if args[0] == "info" else ""
        runner.docker = docker
        with patch.object(acceptance.time, "monotonic", clock.clock):
            self.assertEqual([], runner.list_ids(0.3))
        self.assertTrue(all(0 < timeout <= 0.3 for _, timeout in calls))
        self.assertEqual("info", calls[0][0][0])
        self.assertEqual("info", calls[-1][0][0])

    def test_engine_mismatch_prevents_inventory_lookup(self):
        runner, calls = self.runner(), []
        def docker(*args, **kwargs):
            calls.append(args)
            return "other-engine"
        runner.docker = docker
        with self.assertRaisesRegex(ValueError, "DockerEngineChanged"):
            runner.list_ids()
        self.assertEqual(1, len(calls))

    def test_full_id_and_both_owner_labels_are_required(self):
        import json
        for wrong in (False, True):
            with self.subTest(wrong=wrong):
                runner = self.runner()
                def docker(*args, **kwargs):
                    if args[0] == "ps":
                        return CID
                    return json.dumps({acceptance.RUN_LABEL: "other" if wrong else RUN,
                                       acceptance.ROLE_LABEL: "referee-driver"})
                runner.docker = docker
                if wrong:
                    with self.assertRaisesRegex(ValueError, "OwnerLabelMismatch"):
                        runner.own_resources(RUN)
                else:
                    self.assertEqual({CID: "referee-driver"}, runner.own_resources(RUN))


class ImagePreflightTests(unittest.TestCase):
    """固定イメージとビルドキャッシュをDockerなしで検証する。"""

    REFS = (
        "mcr.microsoft.com/dotnet/runtime@sha256:0cf09aaccf6a9f68900aed4c73fb28fe25bb59ec85de4dea08ba02240a8635f3",
        "mcr.microsoft.com/dotnet/aspnet@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca",
    )

    def setUp(self):
        import tempfile
        from types import SimpleNamespace
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.path = Path(self.temp.name)
        for name in (
            "Testing/Duck.Testing.AppHost/bin/Debug/net10.0/Duck.Testing.AppHost.dll",
            "Testing/Duck.Testing.RefereeDriver.Host/bin/Release/net10.0/publish/Duck.Testing.RefereeDriver.Host.dll",
            "Tracker/Tracker.RuntimeHost/bin/Release/net10.0/publish/Tracker.RuntimeHost.dll",
        ):
            path = self.path / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(b"test")
        self.run = object.__new__(acceptance.DesktopRun)
        self.run.repo = self.run.folder = self.path
        self.run.args = SimpleNamespace(expected_head="test-head", context="desktop-linux")
        self.run.call = lambda args: "test-head" if args[-1] == "HEAD" else ""
        self.run.list_ids = lambda: []
        self.calls = []
        # 製品モジュールの参照だけを置換し、pathlibが使う実OSは変更しない。
        from unittest.mock import patch
        platform = patch.object(acceptance, "os", SimpleNamespace(name="nt"))
        platform.start()
        self.addCleanup(platform.stop)

    def mock_docker(self, cache=None, registered=False, direct_missing=False, base_error=False):
        import json
        if cache is None:
            cache = [{"Description": "pulled from " + ref, "Mutable": False, "Shared": True}
                     for ref in self.REFS]
        def docker(*args, **kwargs):
            self.calls.append(args)
            if args[:2] == ("image", "inspect"):
                ref = args[2]
                if direct_missing and ref.startswith("ghcr.io"):
                    raise ValueError("CommandFailed: No such image: " + ref)
                if base_error and ref in self.REFS:
                    raise ValueError("CommandFailed: access denied")
                if ref in self.REFS and not registered:
                    raise ValueError("CommandFailed: No such image: " + ref)
                return "sha256:" + "a" * 64
            if args[:2] == ("buildx", "du"):
                if isinstance(cache, Exception):
                    raise cache
                return cache if isinstance(cache, str) else json.dumps(cache)
            raise AssertionError(str(args))
        self.run.docker = docker

    def evidence(self):
        import json
        return json.loads((self.path / "preflight-images.json").read_text(encoding="utf-8"))

    def test_cache_candidate_is_unverified_and_recorded_separately(self):
        self.mock_docker()
        result = self.run.preflight()
        self.assertEqual("cache_candidates_unverified", result["status"])
        self.assertFalse(result["buildVerified"])
        self.assertEqual(4, acceptance.preflight_exit_code(result))
        self.assertTrue(all(item["state"] == "registered" for item in self.evidence()["directImages"]))
        self.assertTrue(all(item["state"] == "cache_candidate" for item in self.evidence()["buildBaseImages"]))
        self.assertIn(("buildx", "du", "--builder", "desktop-linux", "--format=json"), self.calls)

    def test_registered_bases_do_not_require_cache_lookup(self):
        self.mock_docker(registered=True)
        result = self.run.preflight()
        self.assertEqual("images_registered", result["status"])
        self.assertFalse(result["buildVerified"])
        self.assertEqual(0, acceptance.preflight_exit_code(result))
        self.assertFalse(any(args[:2] == ("buildx", "du") for args in self.calls))

    def test_missing_cache_is_blocked(self):
        self.mock_docker(cache=[])
        with self.assertRaisesRegex(ValueError, "BuildBaseCacheEvidenceMissing"):
            self.run.preflight()
        self.assertTrue(all(item["state"] == "cache_missing" for item in self.evidence()["buildBaseImages"]))

    def test_unparseable_cache_is_unknown(self):
        self.mock_docker(cache='{"unexpected":"shape"}')
        with self.assertRaisesRegex(ValueError, "BuildCacheEvidenceUnknown"):
            self.run.preflight()
        self.assertTrue(all(item["state"] == "cache_unknown" for item in self.evidence()["buildBaseImages"]))

    def test_cache_command_error_is_distinct(self):
        self.mock_docker(cache=OSError("cannot query builder"))
        with self.assertRaisesRegex(ValueError, "BuildCacheQueryFailed"):
            self.run.preflight()
        self.assertTrue(all(item["state"] == "cache_command_failed" for item in self.evidence()["buildBaseImages"]))

    def test_direct_image_failure_is_not_masked_by_cache(self):
        self.mock_docker(direct_missing=True)
        with self.assertRaisesRegex(ValueError, "DirectImageInspectFailed"):
            self.run.preflight()
        self.assertEqual("inspect_failed", self.evidence()["directImages"][0]["state"])
        self.assertFalse(any(args[:2] == ("buildx", "du") for args in self.calls))

    def test_unexpected_base_inspect_error_stays_error(self):
        self.mock_docker(base_error=True)
        with self.assertRaisesRegex(ValueError, "BuildBaseInspectFailed"):
            self.run.preflight()
        self.assertEqual("inspect_failed", self.evidence()["buildBaseImages"][0]["state"])

    def test_mutable_cache_and_wrong_digest_are_not_candidates(self):
        self.mock_docker(cache=[
            {"Description": "pulled from " + self.REFS[0].replace("0cf09a", "ffffff"),
             "Mutable": False, "Shared": True},
            {"Description": "pulled from " + self.REFS[1], "Mutable": True, "Shared": True},
        ])
        with self.assertRaisesRegex(ValueError, "BuildBaseCacheEvidenceMissing"):
            self.run.preflight()

    def test_json_lines_cache_evidence_is_supported(self):
        import json
        records = "\n".join(json.dumps({"Description": "pulled from " + ref,
                                          "Mutable": False, "Shared": True})
                            for ref in self.REFS)
        self.mock_docker(cache=records)
        self.assertEqual("cache_candidates_unverified", self.run.preflight()["status"])


class PreflightPlatformTests(unittest.TestCase):
    """実行OSの制限と、OS非依存で動く模擬試験を別々に確認する。"""

    def test_non_windows_is_rejected_before_external_commands(self):
        from types import SimpleNamespace
        from unittest.mock import Mock, patch
        runner = object.__new__(acceptance.DesktopRun)
        runner.call = Mock(side_effect=AssertionError("Unexpected command"))
        runner.list_ids = Mock(side_effect=AssertionError("Unexpected Docker inventory"))
        with patch.object(acceptance, "os", SimpleNamespace(name="posix")):
            with self.assertRaisesRegex(ValueError, "^WindowsRequired$"):
                runner.preflight()
        runner.call.assert_not_called()
        runner.list_ids.assert_not_called()

    def test_image_fixtures_do_not_depend_on_host_os(self):
        from types import SimpleNamespace
        from unittest.mock import patch
        suite = unittest.defaultTestLoader.loadTestsFromTestCase(ImagePreflightTests)
        result = unittest.TestResult()
        with patch.object(acceptance, "os", SimpleNamespace(name="posix")):
            suite.run(result)
        self.assertEqual(9, result.testsRun)
        self.assertTrue(result.wasSuccessful(), str(result.failures + result.errors))
        self.assertEqual([], result.skipped)


if __name__ == "__main__":
    unittest.main(verbosity=2)
