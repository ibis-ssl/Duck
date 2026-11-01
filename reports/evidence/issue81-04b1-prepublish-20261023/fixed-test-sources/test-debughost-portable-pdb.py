#!/usr/bin/env python3
"""DebugHost portable PDB契約の失敗診断を検査する。"""

from __future__ import annotations

import importlib.util
import pathlib
import tempfile
import unittest
from unittest import mock


MODULE_PATH = pathlib.Path(__file__).with_name("verify-debughost-portable-pdb.py")
SPEC = importlib.util.spec_from_file_location("debughost_pdb_contract", MODULE_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"検査対象を読み込めません: {MODULE_PATH}")
CONTRACT = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(CONTRACT)


class DebugHostPortablePdbTests(unittest.TestCase):
    """欠落、破損、不一致、source path違反の診断を固定する。"""

    def test_absolute_document_name_allows_empty_first_segment(self) -> None:
        """絶対pathの先頭空要素をportable PDB仕様どおり復号する。"""
        blob_heap = bytes((0, 3)) + b"src" + bytes((3,)) + b"foo"
        document_name = b"/" + bytes((0, 1, 5))
        self.assertEqual(CONTRACT.decode_document_name(document_name, blob_heap), "/src/foo")

    def test_missing_pdb_is_reported(self) -> None:
        """PDB未発行を明示的な契約違反として報告する。"""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = pathlib.Path(temporary_directory)
            (directory / "Tracker.DebugHost.dll").write_bytes(b"assembly")
            with self.assertRaisesRegex(CONTRACT.ContractError, "必要なDebugHost発行物がありません"):
                CONTRACT.verify_contract(directory, "/src/Tracker/Tracker.DebugHost/Program.cs")

    def test_corrupt_pe_is_reported(self) -> None:
        """破損した実行体をPE解析時に拒否する。"""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = pathlib.Path(temporary_directory)
            (directory / "Tracker.DebugHost.dll").write_bytes(b"not a PE")
            (directory / "Tracker.DebugHost.pdb").write_bytes(b"not a PDB")
            with self.assertRaisesRegex(CONTRACT.ContractError, "有効なPEアセンブリではありません"):
                CONTRACT.verify_contract(directory, "/src/Tracker/Tracker.DebugHost/Program.cs")

    def test_corrupt_pdb_is_reported(self) -> None:
        """portable PDB metadata rootの破損を拒否する。"""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = pathlib.Path(temporary_directory)
            (directory / "Tracker.DebugHost.dll").touch()
            (directory / "Tracker.DebugHost.pdb").write_bytes(b"not a PDB")
            with (
                mock.patch.object(CONTRACT, "read_codeview_identity", return_value=(b"A" * 16, 1)),
                self.assertRaisesRegex(CONTRACT.ContractError, "有効なportable PDB metadata rootではありません"),
            ):
                CONTRACT.verify_contract(directory, "/src/Tracker/Tracker.DebugHost/Program.cs")

    def test_assembly_and_pdb_identity_must_match(self) -> None:
        """同じ発行から作られていないPDBをGUID比較で拒否する。"""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = pathlib.Path(temporary_directory)
            (directory / "Tracker.DebugHost.dll").touch()
            (directory / "Tracker.DebugHost.pdb").touch()
            with (
                mock.patch.object(CONTRACT, "read_codeview_identity", return_value=(b"A" * 16, 1)),
                mock.patch.object(CONTRACT, "read_portable_pdb", return_value=(b"B" * 16 + b"C" * 4, ["/src/Tracker/Tracker.DebugHost/Program.cs"])),
                self.assertRaisesRegex(CONTRACT.ContractError, "識別子が一致しません"),
            ):
                CONTRACT.verify_contract(directory, "/src/Tracker/Tracker.DebugHost/Program.cs")

    def test_expected_document_must_be_present_under_src(self) -> None:
        """固定documentがない場合を検出する。"""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = pathlib.Path(temporary_directory)
            (directory / "Tracker.DebugHost.dll").touch()
            (directory / "Tracker.DebugHost.pdb").touch()
            with (
                mock.patch.object(CONTRACT, "read_codeview_identity", return_value=(b"A" * 16, 1)),
                mock.patch.object(CONTRACT, "read_portable_pdb", return_value=(b"A" * 16 + b"C" * 4, ["/src/Tracker/Tracker.DebugHost/Other.cs"])),
                self.assertRaisesRegex(CONTRACT.ContractError, "固定source documentがありません"),
            ):
                CONTRACT.verify_contract(directory, "/src/Tracker/Tracker.DebugHost/Program.cs")

    def test_documents_outside_src_are_rejected(self) -> None:
        """固定documentがあってもPDB内に/src外のdocumentがあれば拒否する。"""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = pathlib.Path(temporary_directory)
            (directory / "Tracker.DebugHost.dll").touch()
            (directory / "Tracker.DebugHost.pdb").touch()
            with (
                mock.patch.object(CONTRACT, "read_codeview_identity", return_value=(b"A" * 16, 1)),
                mock.patch.object(CONTRACT, "read_portable_pdb", return_value=(b"A" * 16 + b"C" * 4, ["/src/Tracker/Tracker.DebugHost/Program.cs", "/agent/_work/Duck/Tracker/Tracker.DebugHost/Other.cs"])),
                self.assertRaisesRegex(CONTRACT.ContractError, "source documentが /src/ 外にあります"),
            ):
                CONTRACT.verify_contract(directory, "/src/Tracker/Tracker.DebugHost/Program.cs")


if __name__ == "__main__":
    unittest.main(verbosity=2)
