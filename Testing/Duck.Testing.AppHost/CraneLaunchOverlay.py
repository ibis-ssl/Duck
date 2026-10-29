"""Crane起動時だけ宛先を反映するDuck所有の補助処理。"""

from __future__ import annotations

import ipaddress
import json
import os
import re
import signal
import shutil
import socket
import subprocess
import sys
import tempfile
import threading
import time
import xml.etree.ElementTree as ET
from contextlib import contextmanager
from pathlib import Path
from typing import Callable, Iterable
from xml.parsers import expat


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


class _StopState:
    def __init__(self) -> None:
        self.requested = threading.Event()
        self.signal_number: int | None = None


@contextmanager
def _capture_stop_signals(state: _StopState):
    previous_handlers: dict[int, object] = {}

    def request_stop(signum: int, _frame: object) -> None:
        if state.signal_number is None:
            state.signal_number = signum
        state.requested.set()

    if threading.current_thread() is threading.main_thread():
        for signum in (signal.SIGTERM, signal.SIGINT):
            previous_handlers[signum] = signal.signal(signum, request_stop)
    try:
        yield state
    finally:
        for signum, previous in previous_handlers.items():
            signal.signal(signum, previous)


def _run_bounded_command(
    command: list[str],
    timeout_seconds: float,
    stop_state: _StopState | None = None,
) -> subprocess.CompletedProcess[str]:
    state = stop_state or _StopState()
    if stop_state is None:
        with _capture_stop_signals(state):
            return _run_bounded_command(command, timeout_seconds, state)
    try:
        return _run_owned_command(
            command,
            os.environ.copy(),
            stop_state=state,
            timeout_seconds=timeout_seconds,
            capture_output=True,
            text=True,
        )
    except subprocess.TimeoutExpired as error:
        raise TimeoutError("\u63a2\u7d22\u7528\u306e\u5b50\u30d7\u30ed\u30bb\u30b9\u304c\u898f\u5b9a\u6642\u9593\u3092\u8d85\u3048\u307e\u3057\u305f") from error


def _resolve_with_deadline(resource_name: str, stop_state: _StopState | None = None) -> list[str]:
    command = [sys.executable, str(Path(__file__).resolve()), "--resolve-name-worker", resource_name]
    result = _run_bounded_command(command, DNS_TIMEOUT_SECONDS, stop_state)
    if result.returncode != 0:
        raise RuntimeError("\u540d\u524d\u89e3\u6c7a\u7528\u306eIPv4\u5b50\u30d7\u30ed\u30bb\u30b9\u304c\u5931\u6557\u3057\u307e\u3057\u305f")
    try:
        addresses = json.loads(result.stdout)
    except json.JSONDecodeError as error:
        raise RuntimeError("\u540d\u524d\u89e3\u6c7a\u7528\u306eIPv4\u7d50\u679c\u3092\u8aad\u307f\u53d6\u308c\u307e\u305b\u3093") from error
    if not isinstance(addresses, list) or any(not isinstance(value, str) for value in addresses):
        raise RuntimeError("\u540d\u524d\u89e3\u6c7a\u7528\u306eIPv4\u7d50\u679c\u306e\u5f62\u5f0f\u304c\u4e0d\u6b63\u3067\u3059")
    return addresses


def resolve_target_address(
    target: str,
    expected_resource: str,
    resolver: Callable[[str], list[str]] | None = None,
    _stop_state: _StopState | None = None,
) -> str:
    if expected_resource not in RESOURCE_NAMES:
        raise ValueError("選択された宛先資源名が許可一覧にありません")
    if target in RESOURCE_NAMES:
        if target != expected_resource:
            raise ValueError("宛先資源名が現在のplanner選択と一致しません")
        addresses = _resolve_with_deadline(target, _stop_state) if resolver is None else resolver(target)
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
        raise ValueError("\u30af\u30e9\u30f3\u30d5\u30a1\u30a4\u30eb\u3078\u53cd\u6620\u3059\u308bIPv4\u30a2\u30c9\u30ec\u30b9\u304c\u8981\u6c42\u3055\u308c\u3066\u3044\u307e\u3059")

    # XML解析で実要素の位置を特定し、コメントを対象外にします。
    candidates: list[tuple[int, int, dict[str, str]]] = []
    parser = expat.ParserCreate()

    def visit_element(name: str, attributes: dict[str, str]) -> None:
        if name != "let" or attributes.get("name") != "ibis_target_address" or attributes.get("if") != "$(var sim)":
            return
        start = parser.CurrentByteIndex
        quote: int | None = None
        end = start
        while end < len(launch_bytes):
            current = launch_bytes[end]
            if quote is not None:
                if current == quote:
                    quote = None
            elif current in (ord('"'), ord("'")):
                quote = current
            elif current == ord(">"):
                end += 1
                break
            end += 1
        if end > len(launch_bytes) or launch_bytes[end - 1 : end] != b">":
            raise ValueError("sim=true\u306e\u5b9f\u884clet\u958b\u59cb\u4f4d\u7f6e\u3092\u7279\u5b9a\u3067\u304d\u307e\u305b\u3093")
        candidates.append((start, end, dict(attributes)))

    parser.StartElementHandler = visit_element
    parser.Parse(launch_bytes, True)
    if len(candidates) != 1 or candidates[0][2].get("value") != EXPECTED_SIM_TARGET:
        raise ValueError("sim=true\u306e\u5b9b\u5148let\u304c\u4e00\u610f\u3057\u305f\u5f62\u3067\u8a2d\u5b9a\u3055\u308c\u308b\u5fc5\u8981\u304c\u3042\u308a\u307e\u3059")

    start, end, _ = candidates[0]
    tag = launch_bytes[start:end]
    value_matches = list(re.finditer(rb"(\bvalue\s*=\s*)(['\"])127\.0\.0\.1\2", tag))
    if len(value_matches) != 1:
        raise ValueError("\u5b9b\u5148let\u306evalue\u304c\u4e00\u610f\u3057\u3066\u3044\u307e\u305b\u3093")
    value_match = value_matches[0]
    replacement = value_match.group(1) + value_match.group(2) + address.encode("ascii") + value_match.group(2)
    updated = launch_bytes[:start] + tag[:value_match.start()] + replacement + tag[value_match.end():] + launch_bytes[end:]
    ET.fromstring(updated)
    return updated


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


class OwnedChildNotReapedError(TimeoutError):
    """\u6240\u6709\u5b50\u30d7\u30ed\u30bb\u30b9\u306e\u56de\u53ce\u304c\u671f\u9650\u5185\u306b\u5b8c\u4e86\u3057\u306a\u304b\u3063\u305f\u72b6\u614b\u3092\u793a\u3057\u307e\u3059\u3002"""


OWNED_CHILD_STOP_SECONDS = 2.35
OWNED_CHILD_FORCE_REAP_SECONDS = 0.4


def _owned_process_group_exists(process_group_id: int) -> bool:
    if os.name != "posix":
        return False
    try:
        os.killpg(process_group_id, 0)
        return True
    except ProcessLookupError:
        return False


def _signal_owned_child(process: subprocess.Popen[object], signum: int) -> None:
    try:
        if os.name == "posix":
            os.killpg(process.pid, signum)
        elif process.poll() is None:
            process.send_signal(signum)
    except ProcessLookupError:
        pass


def _force_stop_owned_child(process: subprocess.Popen[object]) -> None:
    try:
        if os.name == "posix":
            os.killpg(process.pid, signal.SIGKILL)
        elif process.poll() is None:
            process.kill()
    except ProcessLookupError:
        pass


def _wait_owned_child(
    process: subprocess.Popen[object], timeout: float, capture_output: bool
) -> tuple[bool, tuple[object, object] | None]:
    try:
        if capture_output:
            stdout, stderr = process.communicate(timeout=timeout)
            return True, (stdout, stderr)
        process.wait(timeout=timeout)
        return True, None
    except subprocess.TimeoutExpired:
        return False, None


def _wait_owned_tree(
    process: subprocess.Popen[object], deadline: float, capture_output: bool
) -> tuple[bool, tuple[object, object] | None]:
    output: tuple[object, object] | None = None
    while True:
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            return False, output
        if process.poll() is None or (capture_output and output is None):
            complete, observed = _wait_owned_child(process, min(0.05, remaining), capture_output)
            if complete:
                output = observed
        if process.poll() is not None and not _owned_process_group_exists(process.pid):
            return True, output
        time.sleep(min(0.02, max(0.0, deadline - time.monotonic())))


def _stop_and_reap_owned_child(
    process: subprocess.Popen[object], signum: int, capture_output: bool
) -> tuple[object, object] | None:
    _signal_owned_child(process, signum)
    complete, output = _wait_owned_tree(
        process, time.monotonic() + OWNED_CHILD_STOP_SECONDS, capture_output
    )
    if complete:
        return output
    _force_stop_owned_child(process)
    complete, output = _wait_owned_tree(
        process, time.monotonic() + OWNED_CHILD_FORCE_REAP_SECONDS, capture_output
    )
    if complete:
        return output
    raise OwnedChildNotReapedError(
        f"\u6240\u6709\u5b50\u30d7\u30ed\u30bb\u30b9\u3092\u56de\u53ce\u3067\u304d\u307e\u305b\u3093 (pid={process.pid})"
    )


def _run_owned_command(
    command: list[str],
    environment: dict[str, str],
    *,
    stop_state: _StopState | None = None,
    timeout_seconds: float | None = None,
    capture_output: bool = False,
    text: bool = False,
) -> subprocess.CompletedProcess:
    """\u6240\u6709\u3059\u308b\u5b50\u30d7\u30ed\u30bb\u30b9\u306b\u505c\u6b62\u3092\u8ee2\u9001\u3057\u3001\u6709\u754c\u306b\u56de\u53ce\u3057\u307e\u3059\u3002"""
    state = stop_state or _StopState()
    if stop_state is None:
        with _capture_stop_signals(state):
            return _run_owned_command(
                command,
                environment,
                stop_state=state,
                timeout_seconds=timeout_seconds,
                capture_output=capture_output,
                text=text,
            )

    creation_flags = getattr(subprocess, "CREATE_NEW_PROCESS_GROUP", 0) if os.name == "nt" else 0
    process: subprocess.Popen[object] | None = None
    stop_attempted = False
    deadline = None if timeout_seconds is None else time.monotonic() + timeout_seconds
    try:
        process = subprocess.Popen(
            command,
            env=environment,
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE if capture_output else None,
            stderr=subprocess.PIPE if capture_output else None,
            text=text,
            start_new_session=os.name == "posix",
            creationflags=creation_flags,
            close_fds=True,
        )
        while True:
            if state.requested.is_set():
                stop_attempted = True
                output = _stop_and_reap_owned_child(
                    process, state.signal_number or signal.SIGTERM, capture_output
                )
                return subprocess.CompletedProcess(command, process.returncode, *(output or (None, None)))
            remaining = None if deadline is None else deadline - time.monotonic()
            if remaining is not None and remaining <= 0:
                stop_attempted = True
                output = _stop_and_reap_owned_child(process, signal.SIGTERM, capture_output)
                raise subprocess.TimeoutExpired(
                    command,
                    timeout_seconds,
                    output=None if output is None else output[0],
                    stderr=None if output is None else output[1],
                )
            wait_seconds = 0.05 if remaining is None else min(0.05, remaining)
            complete, output = _wait_owned_child(process, wait_seconds, capture_output)
            if complete:
                if _owned_process_group_exists(process.pid):
                    stop_attempted = True
                    output = _stop_and_reap_owned_child(process, signal.SIGTERM, capture_output)
                return subprocess.CompletedProcess(command, process.returncode, *(output or (None, None)))
    except BaseException:
        if process is not None and process.poll() is None and not stop_attempted:
            stop_attempted = True
            _stop_and_reap_owned_child(process, signal.SIGTERM, capture_output)
        raise


def run_with_overlay(
    target: str,
    expected_resource: str,
    launch_arguments: Iterable[str],
    *,
    environment: dict[str, str],
    package_share: Path,
    resolver: Callable[[str], list[str]] | None = None,
    runner: Callable[..., object] | None = None,
    temporary_directory: str | None = None,
    _command_factory: Callable[[list[str]], list[str]] | None = None,
) -> int:
    stop_state = _StopState()
    with _capture_stop_signals(stop_state):
        address = resolve_target_address(target, expected_resource, resolver, stop_state)
        if stop_state.requested.is_set():
            return 128 + (stop_state.signal_number or signal.SIGTERM)
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
        release_overlay = True
        try:
            if stop_state.requested.is_set():
                return 128 + (stop_state.signal_number or signal.SIGTERM)
            command = ["ros2", "launch", "crane_bringup", "crane.launch.xml", *launch_arguments]
            if runner is not None:
                completed = runner(command, env=child_environment, check=False)
                return int(completed.returncode)
            if _command_factory is not None:
                command = _command_factory(command)
            completed = _run_owned_command(command, child_environment, stop_state=stop_state)
            if stop_state.requested.is_set():
                return 128 + (stop_state.signal_number or signal.SIGTERM)
            return int(completed.returncode)
        except OwnedChildNotReapedError:
            # \u30d7\u30ed\u30bb\u30b9\u304c\u751f\u5b58\u3057\u3066\u3044\u308b\u9593\u306f\u3001\u305d\u306e\u5b50\u304c\u53c2\u7167\u3059\u308b\u4e00\u6642\u4fdd\u5b58\u5148\u3092\u6d88\u3057\u307e\u305b\u3093\u3002
            release_overlay = False
            temporary._finalizer.detach()
            raise
        finally:
            if release_overlay:
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
