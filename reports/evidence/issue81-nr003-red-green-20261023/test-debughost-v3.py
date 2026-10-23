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

    @staticmethod
    def _write_binary_fixture(directory: pathlib.Path, *, codeview_stamp: int, pdb_stamp: int, age: int = 1) -> None:
        """最小PEとportable PDBを組み立て、実reader経路へ渡す。"""
        import struct

        guid = bytes(range(16))
        pdb_id = guid + struct.pack("<I", pdb_stamp)
        document = b"/" + bytes((0, 1, 5))
        blob_heap = b"\0" + bytes((3,)) + b"src" + bytes((10,)) + b"Tracker.cs"
        name_blob = document
        name_index = len(blob_heap)
        blob_heap += bytes((len(name_blob),)) + name_blob

        tables = bytearray(24)
        tables[4:8] = bytes((2, 0, 0, 1))
        tables[8:16] = (1 << 0x30).to_bytes(8, "little")
        tables[16:24] = (1 << 0x30).to_bytes(8, "little")
        tables += (1).to_bytes(4, "little")
        tables += name_index.to_bytes(2, "little") + bytes(6)

        stream_specs = [("#Pdb", pdb_id + bytes(12)), ("#~", bytes(tables)), ("#Blob", blob_heap)]
        version = b"v1\0\0"
        root_prefix = b"BSJB" + struct.pack("<HHII", 1, 1, 0, len(version)) + version + struct.pack("<HH", 0, len(stream_specs))
        header_size = sum(8 + ((len(name.encode("ascii")) + 1 + 3) & ~3) for name, _ in stream_specs)
        data_offset = len(root_prefix) + header_size
        stream_headers = bytearray()
        stream_data = bytearray()
        for name, payload in stream_specs:
            encoded_name = name.encode("ascii") + b"\0"
            padded_name = encoded_name + bytes((-len(encoded_name)) % 4)
            stream_headers += struct.pack("<II", data_offset, len(payload)) + padded_name
            stream_data += payload
            data_offset += len(payload)
        (directory / "Tracker.DebugHost.pdb").write_bytes(root_prefix + stream_headers + stream_data)

        pe_offset = 0x80
        optional_size = 240
        section_offset = pe_offset + 24 + optional_size
        raw_offset = 0x200
        raw_size = 0x200
        pe = bytearray(raw_offset + raw_size)
        pe[:2] = b"MZ"
        struct.pack_into("<I", pe, 0x3C, pe_offset)
        pe[pe_offset:pe_offset + 4] = b"PE\0\0"
        struct.pack_into("<HHIIIHH", pe, pe_offset + 4, 0x8664, 1, 0, 0, 0, optional_size, 0x2022)
        optional = pe_offset + 24
        struct.pack_into("<H", pe, optional, 0x20B)
        struct.pack_into("<I", pe, optional + 108, 16)
        struct.pack_into("<II", pe, optional + 112 + 6 * 8, 0x1000, 28)
        struct.pack_into("<8sIIIIIIHHI", pe, section_offset, b".rdata\0\0", raw_size, 0x1000, raw_size, raw_offset, 0, 0, 0, 0, 0x40000040)
        payload_offset = raw_offset + 28
        payload = b"RSDS" + guid + struct.pack("<I", age) + b"fixture.pdb\0"
        struct.pack_into("<IIHHIIII", pe, raw_offset, 0, codeview_stamp, 0, 0, 2, len(payload), 0x101C, payload_offset)
        pe[payload_offset:payload_offset + len(payload)] = payload
        (directory / "Tracker.DebugHost.dll").write_bytes(pe)

    def test_real_binary_fixture_accepts_matching_twenty_byte_identity(self) -> None:
        """GUIDとStampが一致する実reader fixtureを受理する。"""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = pathlib.Path(temporary_directory)
            self._write_binary_fixture(directory, codeview_stamp=0x12345678, pdb_stamp=0x12345678)
            self.assertEqual(CONTRACT.read_codeview_identity(directory / "Tracker.DebugHost.dll"), (bytes(range(16)), 1))
            self.assertEqual(CONTRACT.read_portable_pdb(directory / "Tracker.DebugHost.pdb"), (bytes(range(16)) + bytes.fromhex("78563412"), ["/src/Tracker.cs"]))
            self.assertEqual(CONTRACT.verify_contract(directory, "/src/Tracker.cs"), 1)

    def test_real_binary_fixture_rejects_stamp_only_mismatch(self) -> None:
        """同じGUID、age 1、有効な/src文書でもStamp不一致を拒否する。"""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = pathlib.Path(temporary_directory)
            self._write_binary_fixture(directory, codeview_stamp=0x12345679, pdb_stamp=0x12345678)
            self.assertEqual(CONTRACT.read_codeview_identity(directory / "Tracker.DebugHost.dll"), (bytes(range(16)), 1))
            self.assertEqual(CONTRACT.read_portable_pdb(directory / "Tracker.DebugHost.pdb"), (bytes(range(16)) + bytes.fromhex("78563412"), ["/src/Tracker.cs"]))
            with self.assertRaisesRegex(CONTRACT.ContractError, "識別子が一致しません"):
                CONTRACT.verify_contract(directory, "/src/Tracker.cs")

    def test_real_binary_fixture_rejects_nonportable_codeview_age(self) -> None:
        """portable PDBに許可されないCodeView ageを実reader経路で拒否する。"""
        with tempfile.TemporaryDirectory() as temporary_directory:
            directory = pathlib.Path(temporary_directory)
            self._write_binary_fixture(directory, codeview_stamp=0x12345678, pdb_stamp=0x12345678, age=2)
            with self.assertRaisesRegex(CONTRACT.ContractError, "CodeView ageは1"):
                CONTRACT.verify_contract(directory, "/src/Tracker.cs")


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
