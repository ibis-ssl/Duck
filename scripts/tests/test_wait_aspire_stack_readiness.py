import sys
import unittest
from pathlib import Path
from unittest.mock import MagicMock, patch
from urllib.error import HTTPError

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from wait_aspire_stack_readiness import (
    REQUIRED_RESOURCES,
    probe_health,
    verify_owned_stack,
    wait_for_readiness,
)


class FakeClock:
    def __init__(self):
        self.now = 0.0

    def __call__(self):
        return self.now

    def sleep(self, seconds):
        self.now += seconds


class AspireStackReadinessTests(unittest.TestCase):
    def test_required_readiness_ports_match_the_default_apphost_resources(self):
        repository = Path(__file__).resolve().parents[2]
        apphost = (repository / "Testing" / "Duck.Testing.AppHost" / "Program.cs").read_text(encoding="utf-8")

        for resource, port in REQUIRED_RESOURCES.items():
            self.assertRegex(apphost, rf"\b{port},\s+\"[^\"]+\",\s+\"{resource}\"")
        self.assertIn('?? "visibility_graph"', apphost)

    def test_workflow_gates_motion_acceptance_on_the_full_stack_readiness_probe(self):
        repository = Path(__file__).resolve().parents[2]
        workflow = (repository / ".github" / "workflows" / "dotnet-test.yml").read_text(encoding="utf-8-sig")
        readiness_marker = "id: aspire_full_stack_ready"
        self.assertIn(readiness_marker, workflow)
        readiness_start = workflow.index(readiness_marker)
        acceptance_start = workflow.index("- name: Run full-stack packet and active-motion acceptance")
        readiness_step = workflow[readiness_start:acceptance_start]
        acceptance_step = workflow[acceptance_start:]

        self.assertIn("scripts/wait_aspire_stack_readiness.py", readiness_step)
        self.assertIn('--stack-id "$stack_id"', readiness_step)
        self.assertIn("--timeout-seconds 300", readiness_step)
        self.assertIn("steps.aspire_full_stack_ready.outcome == 'success'", acceptance_step)
        self.assertNotIn("ancestor=robocupssl/ssl-game-controller", readiness_step)

    def test_workflow_diagnostics_and_cleanup_are_limited_to_the_run_owned_stack(self):
        repository = Path(__file__).resolve().parents[2]
        workflow = (repository / ".github" / "workflows" / "dotnet-test.yml").read_text(encoding="utf-8-sig")
        cleanup_start = workflow.index("- name: Collect logs and clean up this run's containers")
        self.assertIn("if: ${{ always() }}", workflow[cleanup_start:])
        cleanup_step = workflow[cleanup_start:]

        self.assertIn('stack_id="gha-$GITHUB_RUN_ID-$GITHUB_RUN_ATTEMPT"', workflow)
        self.assertIn("--filter 'label=duck.aspire.owner=duck-apphost'", cleanup_step)
        self.assertIn('--filter "label=duck.aspire.stack=$stack_id"', cleanup_step)
        self.assertIn("from scripts.capture_aspire_dcp_logs import sanitize", cleanup_step)
        self.assertNotIn('docker inspect "$container_id"', cleanup_step)
        self.assertNotIn("docker image ls", cleanup_step)

    def test_workflow_skips_full_stack_artifact_upload_when_sanitization_fails(self):
        repository = Path(__file__).resolve().parents[2]
        workflow = (repository / ".github" / "workflows" / "dotnet-test.yml").read_text(encoding="utf-8-sig")
        cleanup_start = workflow.index("- name: Collect logs and clean up this run's containers")
        upload_start = workflow.index("- name: Upload full-stack acceptance evidence")
        cleanup_step = workflow[cleanup_start:upload_start]
        upload_step = workflow[upload_start:]

        self.assertIn("id: cleanup_full_stack", cleanup_step)
        self.assertIn("scripts/sanitize_aspire_artifacts.py", cleanup_step)
        self.assertIn('echo \"sanitization_success=$sanitization_success\" >> \"$GITHUB_OUTPUT\"', cleanup_step)
        self.assertIn("steps.cleanup_full_stack.outputs.sanitization_success == 'true'", upload_step)

    def test_probe_returns_status_without_reading_response_body(self):
        response = MagicMock()
        response.status = 200
        response.__enter__.return_value = response

        with patch("wait_aspire_stack_readiness.urlopen", return_value=response) as urlopen:
            self.assertEqual(200, probe_health(39104, 0.5))

        urlopen.assert_called_once_with("http://127.0.0.1:39104/health/ready", timeout=0.5)
        response.read.assert_not_called()

    def test_probe_preserves_http_failure_code_and_sanitizes_connection_failure(self):
        failure = HTTPError("http://127.0.0.1:39104/health/ready", 503, "Unavailable", None, None)
        with patch("wait_aspire_stack_readiness.urlopen", side_effect=failure):
            self.assertEqual(503, probe_health(39104, 0.5))
        with patch("wait_aspire_stack_readiness.urlopen", side_effect=OSError("private detail")):
            self.assertEqual("unreachable", probe_health(39104, 0.5))

    def test_waits_until_every_resource_is_ready_in_the_same_poll_cycle(self):
        clock = FakeClock()
        cycles = [
            {"simulator": 200, "game-controller": 200, "cm4-sim": 503, "crane": 200},
            {"simulator": 200, "game-controller": 200, "cm4-sim": 200, "crane": 200},
        ]
        cycle_index = 0

        def probe(port, _timeout):
            nonlocal cycle_index
            name = {39101: "simulator", 39102: "game-controller", 39103: "cm4-sim", 39104: "crane"}[port]
            result = cycles[cycle_index][name]
            if name == "crane":
                cycle_index += 1
            return result

        result = wait_for_readiness(
            42,
            timeout_seconds=5,
            poll_interval_seconds=1,
            probe=probe,
            is_alive=lambda _pid: True,
            clock=clock,
            sleep=clock.sleep,
        )

        self.assertEqual("ready", result.status)
        self.assertEqual(2, len(result.polls))
        self.assertEqual(1.0, clock.now)

    def test_reports_startup_readiness_timeout_for_missing_wrapper(self):
        clock = FakeClock()

        def probe(port, _timeout):
            return 503 if port == 39104 else 200

        result = wait_for_readiness(
            42,
            timeout_seconds=2,
            poll_interval_seconds=1,
            probe=probe,
            is_alive=lambda _pid: True,
            clock=clock,
            sleep=clock.sleep,
        )

        self.assertEqual("failed", result.status)
        self.assertEqual("wrapper_readiness_timeout", result.failure_category)
        self.assertIn("crane:503", result.reason)
        self.assertEqual(3, len(result.polls))

    def test_fails_as_startup_error_when_apphost_exits(self):
        result = wait_for_readiness(
            42,
            probe=lambda _port, _timeout: "unreachable",
            is_alive=lambda _pid: False,
        )

        self.assertEqual("failed", result.status)
        self.assertEqual("apphost_exited", result.failure_category)
        self.assertEqual(1, len(result.polls))

    def test_stale_health_200_without_this_runs_owned_stack_blocks_readiness(self):
        calls = []

        def docker_run(command, **_kwargs):
            calls.append(command)
            from subprocess import CompletedProcess
            return CompletedProcess(command, 0, "", "")

        stack_valid, reason = verify_owned_stack("gha-123-1", run=docker_run)
        self.assertFalse(stack_valid)
        self.assertIn("simulator", reason)

        result = wait_for_readiness(
            42,
            probe=lambda _port, _timeout: 200,
            is_alive=lambda _pid: True,
            verify_stack=lambda stack: verify_owned_stack(stack, run=docker_run),
            stack_id="gha-123-1",
        )
        self.assertEqual("failed", result.status)
        self.assertEqual("owned_stack_mismatch", result.failure_category)
        self.assertFalse(result.polls[0]["owned_stack_valid"])
        self.assertEqual(2, len(calls))

    def test_current_stack_all_resources_must_be_unique_running_and_host_networked(self):
        inspections = {
            resource: {
                "Name": f"/duck-gha-123-1-{resource}",
                "Config": {"Labels": {
                    "duck.aspire.owner": "duck-apphost",
                    "duck.aspire.stack": "gha-123-1",
                    "duck.aspire.resource": resource,
                    "duck.aspire.run": f"run-{resource}",
                }},
                "State": {"Running": True},
                "HostConfig": {"NetworkMode": "host"},
            }
            for resource in REQUIRED_RESOURCES
        }

        def make_docker_runner(*, duplicate=None, stopped=None, wrong_stack=None):
            def docker_run(command, **_kwargs):
                if command[1] == "ps":
                    resource = command[-1].split("=")[-1]
                    if duplicate == resource:
                        return type("Result", (), {"returncode": 0, "stdout": "id-a\nid-b\n"})()
                    return type("Result", (), {"returncode": 0, "stdout": "id-" + resource + "\n"})()
                resource = command[-1].removeprefix("id-")
                container = inspections[resource]
                if stopped == resource:
                    container["State"]["Running"] = False
                if wrong_stack == resource:
                    container["Config"]["Labels"]["duck.aspire.stack"] = "old-stack"
                return type("Result", (), {"returncode": 0, "stdout": __import__("json").dumps([container])})()
            return docker_run

        # Restore shared fixture mutations between each case.
        import copy
        pristine = copy.deepcopy(inspections)
        valid_runner = make_docker_runner()
        self.assertEqual((True, None), verify_owned_stack("gha-123-1", run=valid_runner))
        valid_result = wait_for_readiness(
            42,
            probe=lambda _port, _timeout: 200,
            is_alive=lambda _pid: True,
            verify_stack=lambda stack: verify_owned_stack(stack, run=valid_runner),
            stack_id="gha-123-1",
        )
        self.assertEqual("ready", valid_result.status)
        self.assertTrue(valid_result.polls[0]["owned_stack_valid"])

        for case in ("duplicate", "stopped", "wrong_stack"):
            inspections.clear()
            inspections.update(copy.deepcopy(pristine))
            kwargs = {case: "cm4-sim"}
            invalid_runner = make_docker_runner(**kwargs)
            accepted, reason = verify_owned_stack("gha-123-1", run=invalid_runner)
            self.assertFalse(accepted)
            self.assertIn("cm4-sim", reason)
            result = wait_for_readiness(
                42,
                probe=lambda _port, _timeout: 200,
                is_alive=lambda _pid: True,
                verify_stack=lambda stack: verify_owned_stack(stack, run=invalid_runner),
                stack_id="gha-123-1",
            )
            self.assertEqual("failed", result.status)
            self.assertEqual("owned_stack_mismatch", result.failure_category)
            self.assertFalse(result.polls[0]["owned_stack_valid"])


if __name__ == "__main__":
    unittest.main()
