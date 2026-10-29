"""Crane起動時だけ宛先を反映するDuck所有の補助処理。"""

from __future__ import annotations

import ipaddress
import json
import os
import re
import shutil
import socket
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Callable, Iterable


EXPECTED_SIM_TARGET = "127.0.0.1"
RESOURCE_NAMES = frozenset(("cm4-sim", "simulator"))
DNS_TIMEOUT_SECONDS = 3.0


def _is_allowed_ipv4(value: str) -> bool:
    try:
        address = ipaddress.IPv4Address(value)
    except ipaddress.AddressValueError:
        return False
    return (
        str(address) == value
        and not address.is_unspecified
        and not address.is_multicast
        and int(address) != 0xFFFFFFFF
    )


def _is_unicast_ipv4(value: str) -> bool:
    try:
        address = ipaddress.IPv4Address(value)
    except ipaddress.AddressValueError:
        return False
    return _is_allowed_ipv4(value) and not address.is_loopback


def _get_ipv4_addresses(resource_name: str) -> list[str]:
    return sorted(
        {
            entry[4][0]
            for entry in socket.getaddrinfo(resource_name, None, socket.AF_INET, socket.SOCK_STREAM)
        }
    )


def _run_bounded_command(command: list[str], timeout_seconds: float) -> subprocess.CompletedProcess[str]:
    try:
        return subprocess.run(
            command,
            capture_output=True,
            text=True,
            timeout=timeout_seconds,
            check=False,
        )
    except subprocess.TimeoutExpired as error:
        raise TimeoutError("期限付きの名前解決処理が制限時間を超えました") from error


def _resolve_with_deadline(resource_name: str) -> list[str]:
    command = [sys.executable, str(Path(__file__).resolve()), "--resolve-name-worker", resource_name]
    result = _run_bounded_command(command, DNS_TIMEOUT_SECONDS)
    if result.returncode != 0:
        raise RuntimeError("資源名のIPv4解決用子プロセスが失敗しました")
    try:
        addresses = json.loads(result.stdout)
    except json.JSONDecodeError as error:
        raise RuntimeError("資源名のIPv4解決結果を読み取れません") from error
    if not isinstance(addresses, list) or any(not isinstance(value, str) for value in addresses):
        raise RuntimeError("資源名のIPv4解決結果の形式が不正です")
    return addresses


def resolve_target_address(
    target: str,
    expected_resource: str,
    resolver: Callable[[str], list[str]] | None = None,
) -> str:
    if expected_resource not in RESOURCE_NAMES:
        raise ValueError("選択された宛先資源名が許可一覧にありません")
    if target in RESOURCE_NAMES:
        if target != expected_resource:
            raise ValueError("宛先資源名が現在のplanner選択と一致しません")
        addresses = _resolve_with_deadline(target) if resolver is None else resolver(target)
        unique = sorted(set(addresses))
        if len(unique) != 1 or not _is_unicast_ipv4(unique[0]):
            raise ValueError("選択資源のIPv4アドレスが一意でないか許可されていません")
        return unique[0]
    if target in RESOURCE_NAMES:
        raise ValueError("宛先資源名が不正です")
    if target != "127.0.0.1":
        raise ValueError("宛先は選択資源名またはホストネットワーク互換の127.0.0.1である必要があります")
    return target


def patch_simulator_target(launch_bytes: bytes, address: str) -> bytes:
    if not _is_allowed_ipv4(address):
        raise ValueError("起動ファイルへ反映するIPv4アドレスが許可されていません")
    had_bom = launch_bytes.startswith(b"\xef\xbb\xbf")
    text = launch_bytes.decode("utf-8-sig")
    candidates: list[tuple[re.Match[str], ET.Element]] = []
    for match in re.finditer(r"<let\b[^>]*?/?>", text, flags=re.DOTALL):
        try:
            element = ET.fromstring(match.group(0))
        except ET.ParseError:
            continue
        if element.attrib.get("name") == "ibis_target_address" and element.attrib.get("if") == "$(var sim)":
            candidates.append((match, element))
    if len(candidates) != 1 or candidates[0][1].attrib.get("value") != EXPECTED_SIM_TARGET:
        raise ValueError("sim=trueの宛先letが想定した形で一つだけ存在する必要があります")

    match, _ = candidates[0]
    tag = match.group(0)
    value_matches = list(re.finditer(r"(\bvalue\s*=\s*)(['\"])127\.0\.0\.1\2", tag))
    if len(value_matches) != 1:
        raise ValueError("宛先letのvalue属性が一意でありません")
    value_match = value_matches[0]
    replacement = value_match.group(1) + value_match.group(2) + address + value_match.group(2)
    updated_tag = tag[: value_match.start()] + replacement + tag[value_match.end() :]
    updated = text[: match.start()] + updated_tag + text[match.end() :]
    ET.fromstring(updated)
    encoded = updated.encode("utf-8")
    return b"\xef\xbb\xbf" + encoded if had_bom else encoded


def _link_original(source: Path, destination: Path) -> None:
    os.symlink(source, destination, target_is_directory=source.is_dir())


def create_overlay(
    package_share: Path,
    address: str,
    *,
    temporary_directory: str | None = None,
    link_original: Callable[[Path, Path], None] = _link_original,
) -> tuple[tempfile.TemporaryDirectory[str], Path]:
    package_share = package_share.resolve(strict=True)
    launch_source = package_share / "launch" / "crane.launch.xml"
    marker_source = package_share.parent.parent / "share" / "ament_index" / "resource_index" / "packages" / "crane_bringup"
    if not launch_source.is_file() or not marker_source.is_file():
        raise ValueError("固定crane_bringup配布物に起動ファイルまたはament登録印がありません")

    patched_launch = patch_simulator_target(launch_source.read_bytes(), address)
    temporary = tempfile.TemporaryDirectory(prefix="duck-crane-ament-", dir=temporary_directory)
    try:
        prefix = Path(temporary.name)
        package_destination = prefix / "share" / "crane_bringup"
        launch_destination = package_destination / "launch"
        launch_destination.mkdir(parents=True)
        marker_destination = prefix / "share" / "ament_index" / "resource_index" / "packages" / "crane_bringup"
        marker_destination.parent.mkdir(parents=True)
        shutil.copyfile(marker_source, marker_destination)

        for child in package_share.iterdir():
            destination = package_destination / child.name
            if child.name != "launch":
                link_original(child, destination)
                continue
            for launch_child in child.iterdir():
                launch_child_destination = launch_destination / launch_child.name
                if launch_child.name == "crane.launch.xml":
                    launch_child_destination.write_bytes(patched_launch)
                else:
                    link_original(launch_child, launch_child_destination)
        return temporary, prefix
    except Exception:
        temporary.cleanup()
        raise


def run_with_overlay(
    target: str,
    expected_resource: str,
    launch_arguments: Iterable[str],
    *,
    environment: dict[str, str],
    package_share: Path,
    resolver: Callable[[str], list[str]] | None = None,
    runner: Callable[..., object] = subprocess.run,
    temporary_directory: str | None = None,
) -> int:
    address = resolve_target_address(target, expected_resource, resolver)
    temporary, prefix = create_overlay(
        package_share,
        address,
        temporary_directory=temporary_directory,
    )
    child_environment = environment.copy()
    existing_prefix = child_environment.get("AMENT_PREFIX_PATH", "")
    child_environment["AMENT_PREFIX_PATH"] = os.pathsep.join(
        value for value in (str(prefix), existing_prefix) if value
    )
    try:
        completed = runner(
            ["ros2", "launch", "crane_bringup", "crane.launch.xml", *launch_arguments],
            env=child_environment,
            check=False,
        )
        return int(completed.returncode)
    finally:
        temporary.cleanup()


def main(arguments: list[str] | None = None) -> int:
    arguments = sys.argv[1:] if arguments is None else arguments
    if arguments and arguments[0] == "--resolve-name-worker":
        if len(arguments) != 2 or arguments[1] not in RESOURCE_NAMES:
            return 3
        print(json.dumps(_get_ipv4_addresses(arguments[1])))
        return 0

    target = os.environ.get("DUCK_CRANE_TARGET_ADDRESS")
    expected_resource = os.environ.get("DUCK_CRANE_EXPECTED_RESOURCE")
    planner = os.environ.get("PLANNER")
    expected_for_planner = "cm4-sim" if planner == "visibility_graph" else "simulator"
    if not target or expected_resource not in RESOURCE_NAMES or expected_resource != expected_for_planner:
        print("Crane宛先設定が不足しているかplanner選択と一致しません。", file=sys.stderr)
        return 2

    try:
        from ament_index_python.packages import get_package_share_directory

        return run_with_overlay(
            target,
            expected_resource,
            arguments,
            environment=os.environ.copy(),
            package_share=Path(get_package_share_directory("crane_bringup")),
        )
    except Exception as error:
        print(f"Crane起動時宛先の準備に失敗しました: {type(error).__name__}: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
