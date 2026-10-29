import importlib.util
import shutil
import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace


REPOSITORY_ROOT = Path(__file__).resolve().parents[2]
MODULE_PATH = REPOSITORY_ROOT / "Testing" / "Duck.Testing.AppHost" / "CraneLaunchOverlay.py"
SPEC = importlib.util.spec_from_file_location("crane_launch_overlay", MODULE_PATH)
assert SPEC is not None and SPEC.loader is not None
overlay = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(overlay)


class CraneLaunchOverlayTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="duck-crane-overlay-test-")
        self.root = Path(self.temporary.name)
        self.install_prefix = self.root / "install"
        self.package_share = self.install_prefix / "share" / "crane_bringup"
        self.launch_directory = self.package_share / "launch"
        self.launch_directory.mkdir(parents=True)
        (self.launch_directory / "crane.launch.xml").write_bytes(self.launch_xml())
        (self.launch_directory / "foxglove_bridge.launch.xml").write_text("<launch/>", encoding="utf-8")
        (self.package_share / "config").mkdir()
        (self.package_share / "config" / "settings.yaml").write_text("mode: fixed\n", encoding="utf-8")
        self.marker = self.install_prefix / "share" / "ament_index" / "resource_index" / "packages" / "crane_bringup"
        self.marker.parent.mkdir(parents=True)
        self.marker.write_text("", encoding="utf-8")

    def tearDown(self):
        self.temporary.cleanup()

    @staticmethod
    def launch_xml():
        return (
            b'<launch>\n'
            b'  <let name="ibis_target_address" value="127.0.0.1" if="$(var sim)" />\n'
            b'  <let name="ibis_target_address" value="192.168.20.255" if="$(var sim) == false" />\n'
            b'  <let name="unrelated" value="preserve" />\n'
            b'  <include file="$(find-pkg-share crane_bringup)/launch/foxglove_bridge.launch.xml" />\n'
            b'</launch>\n'
        )

    @staticmethod
    def copy_link(source, destination):
        if source.is_dir():
            shutil.copytree(source, destination)
        else:
            shutil.copy2(source, destination)

    def test_target_name_must_match_planner_and_resolve_to_one_safe_ipv4(self):
        self.assertEqual(
            "192.0.2.8",
            overlay.resolve_target_address("cm4-sim", "cm4-sim", resolver=lambda name: ["192.0.2.8"]),
        )
        self.assertEqual("127.0.0.1", overlay.resolve_target_address("127.0.0.1", "cm4-sim"))
        with self.assertRaises(ValueError):
            overlay.resolve_target_address("192.0.2.8", "cm4-sim")
        with self.assertRaises(ValueError):
            overlay.resolve_target_address("simulator", "cm4-sim", resolver=lambda name: ["192.0.2.8"])
        with self.assertRaises(ValueError):
            overlay.resolve_target_address("cm4-sim", "cm4-sim", resolver=lambda name: ["192.0.2.8", "192.0.2.9"])
        for address in ("0.0.0.0", "255.255.255.255", "224.0.0.1", "::1", "198.51.100.3.example", "127.0.0.2"):
            with self.subTest(address=address), self.assertRaises(ValueError):
                overlay.resolve_target_address(address, "cm4-sim")

    def test_simulator_target_patch_preserves_false_branch_and_unrelated_bytes(self):
        original = self.launch_xml()
        updated = overlay.patch_simulator_target(original, "192.0.2.31")
        self.assertEqual(original, (self.launch_directory / "crane.launch.xml").read_bytes())
        self.assertIn(b'value="192.0.2.31" if="$(var sim)"', updated)
        self.assertIn(b'value="192.168.20.255" if="$(var sim) == false"', updated)
        self.assertIn(b'name="unrelated" value="preserve"', updated)
        self.assertIn(b'file="$(find-pkg-share crane_bringup)/launch/foxglove_bridge.launch.xml"', updated)

    def test_simulator_target_patch_rejects_missing_or_ambiguous_target_let(self):
        original = self.launch_xml()
        missing = original.replace(b'name="ibis_target_address" value="127.0.0.1" if="$(var sim)"', b'name="other" value="127.0.0.1" if="$(var sim)"')
        duplicate = original.replace(b'</launch>', b'  <let name="ibis_target_address" value="127.0.0.1" if="$(var sim)" />\n</launch>')
        for value in (missing, duplicate):
            with self.subTest(launch=value), self.assertRaises(ValueError):
                overlay.patch_simulator_target(value, "192.0.2.31")

    def test_overlay_keeps_other_package_files_and_removes_only_its_temporary_prefix(self):
        original_launch = (self.launch_directory / "crane.launch.xml").read_bytes()
        temporary, prefix = overlay.create_overlay(
            self.package_share,
            "192.0.2.41",
            temporary_directory=str(self.root),
            link_original=self.copy_link,
        )
        try:
            generated = prefix / "share" / "crane_bringup"
            launch = (generated / "launch" / "crane.launch.xml").read_bytes()
            self.assertIn(b'value="192.0.2.41" if="$(var sim)"', launch)
            self.assertEqual(b"<launch/>", (generated / "launch" / "foxglove_bridge.launch.xml").read_bytes())
            self.assertEqual("mode: fixed\n", (generated / "config" / "settings.yaml").read_text(encoding="utf-8"))
            self.assertTrue((prefix / "share" / "ament_index" / "resource_index" / "packages" / "crane_bringup").is_file())
            self.assertEqual(original_launch, (self.launch_directory / "crane.launch.xml").read_bytes())
        finally:
            temporary.cleanup()
        self.assertFalse(prefix.exists())

    def test_runner_uses_package_name_command_child_path_and_cleans_overlay(self):
        source_environment = {"AMENT_PREFIX_PATH": "/original/prefix", "PLANNER": "visibility_graph"}
        observed = {}

        def runner(arguments, *, env, check):
            observed["arguments"] = arguments
            observed["environment"] = env
            observed["check"] = check
            observed["overlay_launch"] = next(
                Path(part) / "share" / "crane_bringup" / "launch" / "crane.launch.xml"
                for part in env["AMENT_PREFIX_PATH"].split(overlay.os.pathsep)
                if part.startswith(str(self.root)) and (Path(part) / "share" / "crane_bringup" / "launch" / "crane.launch.xml").exists()
            ).read_bytes()
            return SimpleNamespace(returncode=7)

        result = overlay.run_with_overlay(
            "cm4-sim",
            "cm4-sim",
            ["speak:=false", "planner:=visibility_graph"],
            environment=source_environment,
            package_share=self.package_share,
            resolver=lambda name: ["192.0.2.51"],
            runner=runner,
            temporary_directory=str(self.root),
        )

        self.assertEqual(7, result)
        self.assertEqual(["ros2", "launch", "crane_bringup", "crane.launch.xml", "speak:=false", "planner:=visibility_graph"], observed["arguments"])
        self.assertFalse(observed["check"])
        self.assertEqual("/original/prefix", source_environment["AMENT_PREFIX_PATH"])
        self.assertTrue(observed["environment"]["AMENT_PREFIX_PATH"].endswith(os_pathsep_join("/original/prefix")))
        self.assertIn(b'value="192.0.2.51" if="$(var sim)"', observed["overlay_launch"])
        self.assertFalse(any(path.name.startswith("duck-crane-ament-") for path in self.root.iterdir()))

    def test_deadline_bounds_and_reaps_its_lookup_child(self):
        command = [sys.executable, "-c", "import sys,time; print('child-ready', flush=True); time.sleep(5)"]
        with self.assertRaises(TimeoutError):
            overlay._run_bounded_command(command, 0.1)

def os_pathsep_join(value):
    return overlay.os.pathsep + value


if __name__ == "__main__":
    unittest.main()
