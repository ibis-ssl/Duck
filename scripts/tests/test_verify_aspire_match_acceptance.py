import contextlib
import importlib.util
import io
from pathlib import Path
import tempfile
import unittest
from unittest import mock


SCRIPT = Path(__file__).resolve().parents[1] / "verify_aspire_match_acceptance.py"
SPEC = importlib.util.spec_from_file_location("verify_aspire_match_acceptance", SCRIPT)
MODULE = importlib.util.module_from_spec(SPEC)
assert SPEC and SPEC.loader
SPEC.loader.exec_module(MODULE)


class MatchAcceptanceFailureTests(unittest.TestCase):
    def test_apphost_exit_preserves_incomplete_result_and_failure_reason(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            evidence = root / "duck-aspire-match-results" / "stack"
            output = root / "output"
            evidence.mkdir(parents=True)
            (evidence / "participant-status.json").write_text('{"resources": []}\n', encoding="utf-8")
            with mock.patch.object(MODULE, "gettempdir", return_value=str(root)), \
                 mock.patch.object(MODULE.os, "kill", side_effect=OSError("exited")), \
                 mock.patch("sys.argv", [str(SCRIPT), "--apphost-pid", "42", "--stack-id", "stack",
                                          "--results-dir", str(output)]), \
                 contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(1, MODULE.main())

            self.assertEqual('{"resources": []}\n', (output / "participant-status.json").read_text(encoding="utf-8"))
            report = MODULE._load_json(output / "match-acceptance.json")
            self.assertEqual("incomplete", report["status"])
            self.assertIn("exited before writing", (output / "match-acceptance-failure.txt").read_text(encoding="utf-8"))

    def test_finalize_failure_creates_output_directory_and_writes_diagnostic(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            evidence = root / "evidence"
            evidence.mkdir()
            output = root / "not-created-yet"
            with mock.patch.object(MODULE, "gettempdir", return_value=str(root)), \
                 mock.patch("sys.argv", [str(SCRIPT), "--stack-id", "stack", "--results-dir", str(output), "--finalize"]), \
                 contextlib.redirect_stderr(io.StringIO()):
                self.assertEqual(1, MODULE.main())

            self.assertIn("verification failed", (output / "match-archive-verification-failure.txt").read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
