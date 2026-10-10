import json
import tempfile
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from capture_aspire_dcp_logs import capture, sanitize
from sanitize_aspire_artifacts import sanitize_tree


class CaptureAspireDcpLogsTests(unittest.TestCase):
    def test_workflow_persists_crane_probe_outside_dcp_logs(self):
        workflow = Path(__file__).resolve().parents[2] / ".github/workflows/dotnet-test.yml"
        text = workflow.read_text(encoding="utf-8-sig")
        self.assertIn('export Testing__Crane__DiagnosticsPath="$results_dir/crane-probe.jsonl"', text)
        self.assertIn('python3 scripts/sanitize_aspire_artifacts.py "$results_dir"', text)
        self.assertIn('path: artifacts/aspire-full-stack', text)

    def test_jsonl_sanitization_preserves_records_and_escaped_secret_boundaries(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "artifacts" / "aspire-full-stack"
            root.mkdir(parents=True)
            path = root / "crane-probe.jsonl"
            records = [
                {"event_name": "duck_crane_probe_progress", "attempt": 1,
                 "Outcome": {"Ready": False, "StandardErrorExcerpt": 'password="JSONL_SENTINEL"\nnext line'}},
                {"event_name": "duck_crane_probe_progress", "attempt": 2,
                 "Outcome": {"Ready": True, "StandardOutputExcerpt": "/session_controller\n"}},
            ]
            path.write_text("\n".join(json.dumps(item) for item in records) + "\n", encoding="utf-8")
            self.assertEqual([], sanitize_tree(root))
            result = path.read_text(encoding="utf-8")
            self.assertNotIn("JSONL_SENTINEL", result)
            decoded = [json.loads(line) for line in result.splitlines()]
            self.assertEqual(2, len(decoded))
            self.assertEqual('password="[REDACTED]"\nnext line', decoded[0]["Outcome"]["StandardErrorExcerpt"])
            self.assertTrue(decoded[1]["Outcome"]["Ready"])
            self.assertEqual(2, decoded[1]["attempt"])

    def test_incomplete_jsonl_prevents_upload_instead_of_becoming_valid_evidence(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            path = root / "crane-probe.jsonl"
            path.write_text('{"event_name":', encoding="utf-8")
            self.assertEqual([path], sanitize_tree(root))

    def test_crane_probe_sanitizer_shared_fixtures(self):
        fixtures_path = Path(__file__).with_name("crane_probe_sanitizer_fixtures.json")
        fixtures = json.loads(fixtures_path.read_text(encoding="utf-8"))
        for fixture in fixtures:
            with self.subTest(input=fixture["input"]):
                self.assertEqual(fixture["expected"], sanitize(fixture["input"]))

    def test_artifact_sanitizer_fails_closed_and_leaves_failed_file_unmodified(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "artifacts"
            root.mkdir()
            protected = root / "unsafe.log"
            protected.write_text("api_token=UPLOAD_SENTINEL", encoding="utf-8")

            def fail_for_sentinel(text):
                if "UPLOAD_SENTINEL" in text:
                    raise RuntimeError("forced sanitizer failure")
                return sanitize(text)

            failures = sanitize_tree(root, fail_for_sentinel)

            self.assertEqual([protected], failures)
            self.assertIn("UPLOAD_SENTINEL", protected.read_text(encoding="utf-8"))

    def test_artifact_sanitizer_redacts_all_files_before_upload(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "artifacts"
            root.mkdir()
            diagnostic = root / "dcp" / "dcp-log-tails.txt"
            diagnostic.parent.mkdir()
            diagnostic.write_text('{"StandardErrorExcerpt":"password=ARTIFACT_SENTINEL"}', encoding="utf-8")

            failures = sanitize_tree(root)

            self.assertEqual([], failures)
            result = diagnostic.read_text(encoding="utf-8")
            self.assertNotIn("ARTIFACT_SENTINEL", result)
            self.assertIn("[REDACTED]", result)

    def test_sanitize_redacts_aspire_dashboard_login_query_token(self):
        token = "DASHBOARD_LOGIN_TOKEN_SENTINEL"

        sanitized = sanitize(f"Login to the dashboard at https://localhost:45611/login?t={token}")

        self.assertNotIn(token, sanitized)
        self.assertIn("?t=[REDACTED]", sanitized)

    def test_capture_redacts_secret_value_forms_and_excludes_sensitive_files(self):
        sentinels = [
            "JSON_PASSWORD_SENTINEL",
            "JSON_TOKEN_SENTINEL",
            "YAML_SINGLE_SENTINEL",
            "YAML_DOUBLE_SENTINEL",
            "UNQUOTED_SENTINEL",
            "BEARER_SENTINEL",
            "KEY_FIELD_SENTINEL",
            "CLIENT_KEY_DATA_SENTINEL",
            "CAMEL_SECRET_KEY_SENTINEL",
            "AWS_ACCESS_KEY_ID_SENTINEL",
            "PEM_BLOCK_SENTINEL",
        ]
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary) / "aspire-dcp-test"
            root.mkdir()
            (root / "dcp.log").write_text(
                '\n'.join(
                    [
                        f'{{"password":"{sentinels[0]}","token":"{sentinels[1]}"}}',
                        f"password: '{sentinels[2]}'",
                        f'token: "{sentinels[3]}"',
                        f"api_token={sentinels[4]}",
                        f"Authorization: Bearer {sentinels[5]}",
                        f"client_key: '{sentinels[6]}'",
                        f"client-key-data: '{sentinels[7]}'",
                        f'"secretKey":"{sentinels[8]}"',
                        f"AWS_ACCESS_KEY_ID={sentinels[9]}",
                        f"-----BEGIN RSA PRIVATE KEY-----\n{sentinels[10]}\n-----END RSA PRIVATE KEY-----",
                    ]
                ),
                encoding="utf-8",
            )
            (root / "private-token.log").write_text("FILE_NAME_SENTINEL", encoding="utf-8")
            (root / "client.pem").write_text("PEM_FILE_SENTINEL", encoding="utf-8")
            output = Path(temporary) / "artifact"
            startup_output = output / "dcp-startup-failure"
            cleanup_output = output / "dcp-cleanup"

            captured = capture(startup_output, [root], "startup-failure")
            cleanup_captured = capture(cleanup_output, [root], "before-apphost-shutdown")

            self.assertEqual(1, captured)
            self.assertEqual(1, cleanup_captured)
            startup_diagnostics = '\n'.join(
                (startup_output / name).read_text(encoding="utf-8")
                for name in ("dcp-log-capture-status.txt", "dcp-log-paths.txt", "dcp-log-tails.txt")
            )
            for artifact_dir in (startup_output, cleanup_output):
                artifact_text = '\n'.join(
                    path.read_text(encoding="utf-8")
                    for path in artifact_dir.iterdir()
                    if path.is_file()
                )
                for sentinel in sentinels + ["FILE_NAME_SENTINEL", "PEM_FILE_SENTINEL"]:
                    self.assertNotIn(sentinel, artifact_text)
                self.assertIn('"password":"[REDACTED]"', artifact_text)
                self.assertNotIn("private-token.log", artifact_text)
                self.assertNotIn("client.pem", artifact_text)
            for sentinel in sentinels + ["FILE_NAME_SENTINEL", "PEM_FILE_SENTINEL"]:
                self.assertNotIn(sentinel, startup_diagnostics)
            self.assertIn("phase=startup-failure", startup_diagnostics)


if __name__ == "__main__":
    unittest.main()
