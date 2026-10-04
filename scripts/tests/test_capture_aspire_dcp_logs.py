import tempfile
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from capture_aspire_dcp_logs import capture, sanitize


class CaptureAspireDcpLogsTests(unittest.TestCase):
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
