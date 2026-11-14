#!/usr/bin/env python3
"""DebugHostの発行済みアセンブリとportable PDBの対応を検査する。"""

from __future__ import annotations

import argparse
import pathlib
import struct
import sys


class ContractError(Exception):
    """配布出力が固定契約を満たさない場合の診断。"""


def read_compressed_uint(data: bytes, offset: int) -> tuple[int, int]:
    """ECMA-335形式の圧縮整数を読み、値と次の位置を返す。"""
    if offset >= len(data):
        raise ContractError("portable PDBの圧縮整数が途中で終わっています。")
    first = data[offset]
    if first & 0x80 == 0:
        return first, offset + 1
    if first & 0xC0 == 0x80:
        if offset + 2 > len(data):
            raise ContractError("portable PDBの2 byte圧縮整数が不完全です。")
        return ((first & 0x3F) << 8) | data[offset + 1], offset + 2
    if first & 0xE0 == 0xC0:
        if offset + 4 > len(data):
            raise ContractError("portable PDBの4 byte圧縮整数が不完全です。")
        value = ((first & 0x1F) << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]
        return value, offset + 4
    raise ContractError("portable PDBに不正な圧縮整数があります。")


def read_metadata_streams(data: bytes) -> dict[str, bytes]:
    """Portable PDBのmetadata rootから名前付きstreamを読み出す。"""
    if len(data) < 20 or data[:4] != b"BSJB":
        raise ContractError("PDBが有効なportable PDB metadata rootではありません。")
    version_length = struct.unpack_from("<I", data, 12)[0]
    cursor = (16 + version_length + 3) & ~3
    if cursor + 4 > len(data):
        raise ContractError("portable PDBのmetadata root headerが不完全です。")
    stream_count = struct.unpack_from("<H", data, cursor + 2)[0]
    cursor += 4
    streams: dict[str, bytes] = {}
    for _ in range(stream_count):
        if cursor + 8 > len(data):
            raise ContractError("portable PDBのstream headerが不完全です。")
        stream_offset, stream_size = struct.unpack_from("<II", data, cursor)
        cursor += 8
        name_end = data.find(b"\0", cursor)
        if name_end < 0:
            raise ContractError("portable PDBのstream名が終端されていません。")
        try:
            name = data[cursor:name_end].decode("ascii")
        except UnicodeDecodeError as error:
            raise ContractError("portable PDBのstream名がASCIIではありません。") from error
        cursor = (name_end + 4) & ~3
        stream_end = stream_offset + stream_size
        if stream_offset > len(data) or stream_end > len(data):
            raise ContractError(f"portable PDBの{name} streamがファイル範囲外です。")
        if name in streams:
            raise ContractError(f"portable PDBに{name} streamが重複しています。")
        streams[name] = data[stream_offset:stream_end]
    return streams


def decode_document_name(blob: bytes, blob_heap: bytes) -> str:
    """Document.Name blob内の区切り文字と各path要素を復号する。"""
    if not blob:
        raise ContractError("portable PDBのDocument.Nameが空です。")
    separator = chr(blob[0])
    pieces: list[str] = []
    cursor = 1
    while cursor < len(blob):
        segment_index, cursor = read_compressed_uint(blob, cursor)
        length, content_offset = read_compressed_uint(blob_heap, segment_index)
        content_end = content_offset + length
        if content_end > len(blob_heap):
            raise ContractError("portable PDBのDocument.Name segmentがblob heap範囲外です。")
        try:
            pieces.append(blob_heap[content_offset:content_end].decode("utf-8"))
        except UnicodeDecodeError as error:
            raise ContractError("portable PDBのDocument.Nameに不正なUTF-8があります。") from error
    if not pieces:
        raise ContractError("portable PDBのDocument.Nameにsource pathがありません。")
    return separator.join(pieces)


def read_portable_pdb(pdb_path: pathlib.Path) -> tuple[bytes, list[str]]:
    """Portable PDBの識別子とDocument tableを読み出す。"""
    try:
        data = pdb_path.read_bytes()
    except OSError as error:
        raise ContractError(f"PDBを読み取れません: {pdb_path}: {error}") from error
    streams = read_metadata_streams(data)
    pdb_stream = streams.get("#Pdb")
    tables = streams.get("#~") or streams.get("#-")
    blob_heap = streams.get("#Blob")
    if pdb_stream is None or len(pdb_stream) < 32:
        raise ContractError("PDBに有効な#Pdb streamがありません。")
    if tables is None or blob_heap is None or len(tables) < 24:
        raise ContractError("PDBに必要なmetadata tableまたは#Blob streamがありません。")
    pdb_id = pdb_stream[:20]
    heap_sizes = tables[6]
    valid_mask = struct.unpack_from("<Q", tables, 8)[0]
    cursor = 24
    row_counts: dict[int, int] = {}
    for table_id in range(64):
        if valid_mask & (1 << table_id):
            if cursor + 4 > len(tables):
                raise ContractError("portable PDBのmetadata table row countが不完全です。")
            row_counts[table_id] = struct.unpack_from("<I", tables, cursor)[0]
            cursor += 4
    document_count = row_counts.get(0x30, 0)
    if document_count == 0:
        raise ContractError("portable PDBにDocument tableがありません。")
    blob_index_size = 4 if heap_sizes & 0x04 else 2
    guid_index_size = 4 if heap_sizes & 0x02 else 2
    row_size = blob_index_size + guid_index_size + blob_index_size + guid_index_size
    if cursor + document_count * row_size > len(tables):
        raise ContractError("portable PDBのDocument tableが不完全です。")
    documents: list[str] = []
    for row in range(document_count):
        offset = cursor + row * row_size
        name_index = int.from_bytes(tables[offset:offset + blob_index_size], "little")
        length, blob_offset = read_compressed_uint(blob_heap, name_index)
        end = blob_offset + length
        if end > len(blob_heap):
            raise ContractError("portable PDBのDocument.Name blobが範囲外です。")
        documents.append(decode_document_name(blob_heap[blob_offset:end], blob_heap))
    return pdb_id, documents


def read_codeview_identity(dll_path: pathlib.Path) -> tuple[bytes, int]:
    """PE debug directoryからCodeViewのGUIDとageを取得する。"""
    try:
        data = dll_path.read_bytes()
    except OSError as error:
        raise ContractError(f"アセンブリを読み取れません: {dll_path}: {error}") from error
    if len(data) < 64 or data[:2] != b"MZ":
        raise ContractError(f"有効なPEアセンブリではありません: {dll_path}")
    pe_offset = struct.unpack_from("<I", data, 0x3C)[0]
    if pe_offset + 24 > len(data) or data[pe_offset:pe_offset + 4] != b"PE\0\0":
        raise ContractError(f"PE headerが破損しています: {dll_path}")
    section_count = struct.unpack_from("<H", data, pe_offset + 6)[0]
    optional_size = struct.unpack_from("<H", data, pe_offset + 20)[0]
    optional_offset = pe_offset + 24
    if optional_offset + optional_size > len(data):
        raise ContractError(f"PE optional headerが不完全です: {dll_path}")
    magic = struct.unpack_from("<H", data, optional_offset)[0]
    if magic == 0x20B:
        directory_offset = optional_offset + 112
    elif magic == 0x10B:
        directory_offset = optional_offset + 96
    else:
        raise ContractError(f"未対応のPE optional headerです: 0x{magic:04x}")
    if directory_offset + 7 * 8 > optional_offset + optional_size:
        raise ContractError("PEにdebug data directoryがありません。")
    debug_rva, debug_size = struct.unpack_from("<II", data, directory_offset + 6 * 8)
    if not debug_rva or debug_size < 28:
        raise ContractError("アセンブリにCodeView debug directoryがありません。")
    section_offset = optional_offset + optional_size
    sections: list[tuple[int, int, int]] = []
    for index in range(section_count):
        current = section_offset + index * 40
        if current + 40 > len(data):
            raise ContractError("PE section tableが不完全です。")
        virtual_size, virtual_address, raw_size, raw_offset = struct.unpack_from("<IIII", data, current + 8)
        sections.append((virtual_address, max(virtual_size, raw_size), raw_offset))

    def rva_to_offset(rva: int) -> int:
        """PE RVAをファイルoffsetへ変換する。"""
        for virtual_address, span, raw_offset in sections:
            if virtual_address <= rva < virtual_address + span:
                result = raw_offset + (rva - virtual_address)
                if result < len(data):
                    return result
        raise ContractError(f"PE RVAをファイル位置へ変換できません: 0x{rva:x}")

    debug_offset = rva_to_offset(debug_rva)
    if debug_offset + debug_size > len(data):
        raise ContractError("PE debug directoryがファイル範囲外です。")
    for entry_offset in range(debug_offset, debug_offset + debug_size - 27, 28):
        entry = struct.unpack_from("<IIHHIIII", data, entry_offset)
        entry_type, payload_size, payload_rva, payload_offset = entry[4], entry[5], entry[6], entry[7]
        if entry_type != 2:
            continue
        if payload_offset == 0 and payload_rva:
            payload_offset = rva_to_offset(payload_rva)
        if payload_offset + payload_size > len(data) or payload_size < 24:
            raise ContractError("CodeView debug recordが不完全です。")
        payload = data[payload_offset:payload_offset + payload_size]
        if payload[:4] != b"RSDS":
            raise ContractError("CodeView debug recordがRSDS形式ではありません。")
        return payload[4:20], struct.unpack_from("<I", payload, 20)[0]
    raise ContractError("アセンブリにCodeView RSDS recordがありません。")


def verify_contract(publish_directory: pathlib.Path, expected_document: str) -> int:
    """実発行物のportable PDB、DLL対応、固定source pathを検査する。"""
    dll_path = publish_directory / "Tracker.DebugHost.dll"
    pdb_path = publish_directory / "Tracker.DebugHost.pdb"
    for output_path in (dll_path, pdb_path):
        if not output_path.is_file():
            raise ContractError(f"必要なDebugHost発行物がありません: {output_path}")
    codeview_guid, codeview_age = read_codeview_identity(dll_path)
    if codeview_age != 1:
        raise ContractError(f"portable PDBのCodeView ageは1でなければなりません: {codeview_age}")
    pdb_id, documents = read_portable_pdb(pdb_path)
    if codeview_guid != pdb_id[:16]:
        raise ContractError("Tracker.DebugHost.dllのCodeView GUIDとTracker.DebugHost.pdbの識別子が一致しません。")
    normalized = {document.replace("\\", "/") for document in documents}
    expected = expected_document.replace("\\", "/")
    if expected not in normalized:
        examples = ", ".join(sorted(normalized)[:3])
        raise ContractError(f"portable PDBに固定source documentがありません: {expected}; examples={examples}")
    outside = sorted(document for document in normalized if not document.startswith("/src/"))
    if outside:
        raise ContractError(f"portable PDBのsource documentが /src/ 外にあります: {', '.join(outside[:3])}")
    return len(normalized)


def main() -> int:
    """CLI引数を検査し、契約不一致を非0終了で報告する。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--publish-directory", required=True, type=pathlib.Path)
    parser.add_argument("--expected-document", required=True)
    args = parser.parse_args()
    try:
        document_count = verify_contract(args.publish_directory, args.expected_document)
    except (ContractError, struct.error, IndexError, OverflowError) as error:
        print(f"DebugHost portable PDB contract failed: {error}", file=sys.stderr)
        return 1
    print(f"DebugHost portable PDB contract passed: {document_count} documents are under /src/ and DLL identity matches.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
