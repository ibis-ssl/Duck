"""Issue 51: acceptance needs observed UDP tuples, beyond publisher configuration.

The capture artifact contract is deliberately scoped to its recorded observation
window. These fixtures are synthetic verifier inputs, not Linux acceptance evidence.
Real source endpoint extraction is covered by RefereeSenderTupleCaptureTests.cs.
"""

import contextlib
import copy
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest import mock


SCRIPT = Path(__file__).resolve().parents[1] / "verify_aspire_match_acceptance.py"
SPEC = importlib.util.spec_from_file_location("match_referee_source_verifier", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(MODULE)
CAPTURE_NAME = "referee-source-capture.json"


class MatchRefereeSourceCaptureTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.evidence = self.root / "evidence"
        self.output = self.root / "output"
        self.evidence.mkdir()
        self.stack_id = "issue51-source-test"
        self.containers = []
        resources = sorted(MODULE.MATCH_RESOURCES | {MODULE.MATCH_CONTROLLER})
        for index, resource in enumerate(resources, 1):
            command = {
                "simulator": ["--ibis-team-color", "yellow"],
                "crane": ["team:=ibis"],
                "tigers-blue": ["--aiBlue"],
                "game-controller": ["--publishAddress", "224.5.23.1:11003"],
            }.get(resource, [])
            self.containers.append({
                "Id": format(index, "x") * 64,
                "State": {"Running": True, "StartedAt": "2026-11-07T09:59:00Z"},
                "Config": {"Labels": {
                    "duck.aspire.owner": "duck-apphost",
                    "duck.aspire.stack": self.stack_id,
                    "duck.aspire.run": self.stack_id,
                    "duck.aspire.resource": resource,
                }, "Entrypoint": [], "Cmd": command},
                "NetworkSettings": {"Networks": {"aspire-test": {
                    "NetworkID": "a" * 64, "IPAddress": f"172.30.0.{index + 1}",
                }}},
            })
        self.by_resource = {item["Config"]["Labels"]["duck.aspire.resource"]: item
                            for item in self.containers}
        rows = [{
            "resourceId": name, "currentResourceId": name + "-current",
            "containerId": self.by_resource[name]["Id"], "runId": self.stack_id,
            "state": "Running", "healthStatus": "Healthy", "isReady": True,
            "serviceEvidenceReason": "referee=3, vision-detection=4, tracker=5",
        } for name in sorted(MODULE.MATCH_RESOURCES)]
        self._write(self.evidence / "participant-status.json", {"appHostOperatingSystem": "linux", "resources": rows})
        self._write(self.evidence / "match-result.json", {
            "initialStopCommand": "STOP", "refereeSourceCaptureEpoch": 1,
            "finalReadiness": {"participantResourcesReady": True},
            "status": "failed", "terminationKind": "maximum_time",
        })
        gc = self.by_resource["game-controller"]
        # Host networking provides no container-specific source IP. A received
        # host address cannot identify the publishing process or container.
        gc["HostConfig"] = {"NetworkMode": "host"}
        gc["NetworkSettings"] = {"Networks": {"host": {"IPAddress": ""}}}
        self.source_address = "192.0.2.51"
        self.capture = {
            "schemaVersion": 1, "stackId": self.stack_id, "runId": self.stack_id,
            "receiverResourceId": "match-controller",
            "receiverContainerId": self.by_resource["match-controller"]["Id"],
            "receiverEpoch": 1, "receiverGeneration": 1, "captureComplete": True,
            "captureStartedAtUtc": "2026-11-07T10:00:00Z",
            "captureEndedAtUtc": "2026-11-07T10:00:10Z",
            "destination": {"address": "224.5.23.1", "port": 11003},
            "totalDatagramCount": 3,
            "sources": [{"address": self.source_address, "port": 42317, "datagramCount": 3}],
        }

    @staticmethod
    def _write(path, value):
        path.write_text(json.dumps(value) + "\n", encoding="utf-8")

    def _check(self, capture):
        if capture is not None:
            self._write(self.evidence / CAPTURE_NAME, capture)

        def logs_only(command, **kwargs):
            self.assertEqual(["docker", "logs", "--timestamps",
                              self.by_resource["game-controller"]["Id"]], command)
            return subprocess.CompletedProcess(command, 0, stdout=(
                'Team TIGERs Mannheim connected\nTeam Client BLUE-TIGERs Mannheim connected\n'
                '{"type":"STOP"}\n'), stderr="")

        # These are verifier unit tests: every Docker boundary is replaced. No
        # process is launched and no current stack/container can be touched.
        with contextlib.ExitStack() as stack:
            stack.enter_context(mock.patch.object(MODULE, "_container_inventory", return_value=self.containers))
            stack.enter_context(mock.patch.object(MODULE.subprocess, "run", side_effect=logs_only))
            stack.enter_context(mock.patch.object(sys, "path", [str(SCRIPT.parent), *sys.path]))
            return MODULE._check_evidence(self.stack_id, self.evidence, self.output)

    def test_observed_unique_tuple_is_retained_for_current_receiver_epoch(self):
        report = self._check(self.capture)
        self.assertTrue(report.get("uniqueObservedSenderTupleVerified", False))
        # A unique host tuple proves no process/container attribution by itself.
        self.assertFalse(report.get("uniqueOnWireSenderAttributionVerified", False))
        self.assertEqual(["game-controller"], report["configured11003Publishers"])
        self.assertEqual(self.capture, MODULE._load_json(self.output / CAPTURE_NAME))
        # The source port comes from receive evidence; it is not the destination port.
        self.assertEqual(42317, MODULE._load_json(self.output / CAPTURE_NAME)["sources"][0]["port"])
        self.assertTrue(report["issue51AcceptanceComplete"])
        self.assertFalse(report["fullMatchPassed"])

    def test_maximum_time_does_not_block_g1_but_non_linux_host_does(self):
        status = MODULE._load_json(self.evidence / "participant-status.json")
        status["appHostOperatingSystem"] = "windows"
        self._write(self.evidence / "participant-status.json", status)
        report = self._check(self.capture)
        self.assertFalse(report["issue51AcceptanceComplete"])
        self.assertFalse(report["linuxHostVerified"])
        self.assertFalse(report["fullMatchPassed"])

    def test_completed_receiver_may_exit_after_freezing_g1_evidence(self):
        self.by_resource["match-controller"]["State"]["Running"] = False
        report = self._check(self.capture)
        self.assertTrue(report["issue51AcceptanceComplete"])
        self.assertFalse(report["fullMatchPassed"])

    def test_single_dash_publish_flag_matches_actual_apphost_command(self):
        self.by_resource["game-controller"]["Config"]["Cmd"][0] = "-publishAddress"
        report = self._check(self.capture)
        self.assertEqual(["game-controller"], report["configured11003Publishers"])
        self.assertTrue(report["uniqueObservedSenderTupleVerified"])

    def test_configured_single_publisher_cannot_replace_missing_capture(self):
        with self.assertRaisesRegex(RuntimeError, "(?i)(source|sender|capture|datagram)"):
            self._check(None)

    def test_second_source_port_on_same_ip_fails_uniqueness(self):
        capture = copy.deepcopy(self.capture)
        capture["sources"][0]["datagramCount"] = 2
        capture["sources"].append({"address": self.source_address, "port": 42318, "datagramCount": 1})
        with self.assertRaisesRegex(RuntimeError, "(?i)(source|sender|unique)"):
            self._check(capture)
        self.assertEqual(capture, MODULE._load_json(self.output / CAPTURE_NAME))

    def test_untrusted_empty_incomplete_or_wrong_destination_capture_fails_closed(self):
        mutations = {
            "other_stack": {"stackId": "another-stack"},
            "previous_run": {"runId": "previous-run"},
            "previous_epoch": {"receiverEpoch": 2},
            "previous_boot": {"captureStartedAtUtc": "2026-11-07T09:50:00Z",
                              "captureEndedAtUtc": "2026-11-07T09:51:00Z"},
            "previous_receiver": {"receiverContainerId": "f" * 64},
            "wrong_receiver": {"receiverResourceId": "unowned-observer"},
            "not_complete": {"captureComplete": False},
            "zero_packets": {"totalDatagramCount": 0, "sources": []},
            "wrong_group": {"destination": {"address": "224.5.23.2", "port": 11003}},
            "wrong_port": {"destination": {"address": "224.5.23.1", "port": 10003}},
            "invalid_source_address": {"sources": [{"address": "not-an-ip", "port": 42317, "datagramCount": 3}]},
            "invalid_source_port": {"sources": [{"address": self.source_address, "port": 0, "datagramCount": 3}]},
            "lost_packet_accounting": {"totalDatagramCount": 4},
        }
        for reason, changes in mutations.items():
            with self.subTest(reason=reason):
                capture = copy.deepcopy(self.capture)
                capture.update(changes)
                with self.assertRaisesRegex(RuntimeError, "(?i)(source|sender|capture|datagram|destination|receiver|run|stack|port)"):
                    self._check(capture)

    def test_capture_requires_positive_current_receiver_epoch(self):
        capture = copy.deepcopy(self.capture)
        capture["receiverEpoch"] = 0
        with self.assertRaisesRegex(RuntimeError, "(?i)(receiver|epoch|capture)"):
            self._check(capture)


if __name__ == "__main__":
    unittest.main()
