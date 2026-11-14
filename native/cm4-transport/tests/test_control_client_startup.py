#!/usr/bin/env python3
"""一回限りの制御クライアントでUnixソケット起動待ちを確認する。"""

import select
import socket
import subprocess
import sys
import threading
import time
from pathlib import Path


helper = Path(sys.argv[1])
socket_directory = Path(sys.argv[2])
request = Path(sys.argv[3]).read_bytes().rstrip(b"\n")
acknowledgement = Path(sys.argv[4]).read_bytes().rstrip(b"\n")
socket_path = socket_directory / "control.sock"


def remove_socket_directory():
    if socket_path.exists() or socket_path.is_socket():
        socket_path.unlink()
    if socket_directory.exists():
        socket_directory.rmdir()


def run_client(timeout=5):
    return subprocess.run(
        [str(helper), "--protocol", "jsonl-v1"],
        input=request + b"\n",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        timeout=timeout,
        check=False,
    )


def prepare_directory():
    socket_directory.mkdir(mode=0o700)
    socket_directory.chmod(0o700)


def serve_once(listener, expected_request, reply):
    connection, _ = listener.accept()
    with connection:
        received = bytearray()
        while True:
            chunk = connection.recv(4096)
            if not chunk:
                break
            received.extend(chunk)
        if bytes(received) != expected_request + b"\n":
            raise AssertionError("control client sent an unexpected request")
        if reply is not None:
            connection.sendall(reply + b"\n")


def test_delayed_socket_creation():
    remove_socket_directory()
    listener = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    errors = []

    def create_and_serve():
        try:
            time.sleep(0.12)
            prepare_directory()
            listener.bind(str(socket_path))
            socket_path.chmod(0o600)
            listener.listen(1)
            serve_once(listener, request, acknowledgement)
        except BaseException as error:  # surfaced in the main test thread
            errors.append(error)

    thread = threading.Thread(target=create_and_serve)
    thread.start()
    try:
        result = run_client()
        thread.join(timeout=4)
        if thread.is_alive():
            raise AssertionError("delayed socket server did not finish")
        if errors:
            raise errors[0]
        if result.returncode != 0 or result.stdout.rstrip(b"\n") != acknowledgement:
            raise AssertionError(
                f"client did not connect after socket creation: "
                f"exit={result.returncode}, stderr={result.stderr!r}"
            )
    finally:
        listener.close()
        thread.join(timeout=1)
        remove_socket_directory()


def test_refused_connect_before_listen():
    remove_socket_directory()
    prepare_directory()
    listener = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    listener.bind(str(socket_path))
    socket_path.chmod(0o600)
    errors = []

    def start_listening():
        try:
            time.sleep(0.12)
            listener.listen(1)
            serve_once(listener, request, acknowledgement)
        except BaseException as error:
            errors.append(error)

    thread = threading.Thread(target=start_listening)
    thread.start()
    try:
        result = run_client()
        thread.join(timeout=4)
        if thread.is_alive():
            raise AssertionError("listener did not finish")
        if errors:
            raise errors[0]
        if result.returncode != 0 or result.stdout.rstrip(b"\n") != acknowledgement:
            raise AssertionError(
                f"client did not retry a pre-send refused connection: "
                f"exit={result.returncode}, stderr={result.stderr!r}"
            )
    finally:
        listener.close()
        thread.join(timeout=1)
        remove_socket_directory()


def test_missing_socket_uses_one_total_deadline():
    remove_socket_directory()
    started = time.monotonic()
    result = run_client(timeout=4.5)
    elapsed = time.monotonic() - started
    if result.returncode == 0 or result.stdout:
        raise AssertionError("client accepted a missing control socket")
    if elapsed < 2.0:
        raise AssertionError(f"client failed before using its bounded startup wait: {elapsed:.3f}s")
    if elapsed > 4.5:
        raise AssertionError(f"client exceeded its bounded wait: {elapsed:.3f}s")


def test_no_reconnect_after_request_send():
    remove_socket_directory()
    prepare_directory()
    listener = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    listener.bind(str(socket_path))
    socket_path.chmod(0o600)
    listener.listen(2)
    accepted = []
    errors = []

    def receive_and_close():
        try:
            connection, _ = listener.accept()
            accepted.append(1)
            with connection:
                received = bytearray()
                while True:
                    chunk = connection.recv(4096)
                    if not chunk:
                        break
                    received.extend(chunk)
            if bytes(received) != request + b"\n":
                raise AssertionError("control client sent an unexpected request")
            readable, _, _ = select.select([listener], [], [], 0.2)
            if readable:
                connection, _ = listener.accept()
                accepted.append(1)
                connection.close()
        except BaseException as error:
            errors.append(error)

    thread = threading.Thread(target=receive_and_close)
    thread.start()
    try:
        result = run_client()
        thread.join(timeout=2)
        if thread.is_alive():
            raise AssertionError("server thread did not finish")
        if errors:
            raise errors[0]
        if result.returncode == 0 or len(accepted) != 1:
            raise AssertionError(
                f"client retried or accepted an unconfirmed request: "
                f"exit={result.returncode}, connections={len(accepted)}"
            )
    finally:
        listener.close()
        thread.join(timeout=1)
        remove_socket_directory()


def test_unsafe_directory_fails_closed():
    remove_socket_directory()
    socket_directory.mkdir(mode=0o755)
    socket_directory.chmod(0o755)
    try:
        result = run_client(timeout=1)
        if result.returncode == 0 or result.stdout:
            raise AssertionError("client accepted an unsafe control directory")
    finally:
        remove_socket_directory()


tests = (
    test_delayed_socket_creation,
    test_refused_connect_before_listen,
    test_missing_socket_uses_one_total_deadline,
    test_no_reconnect_after_request_send,
    test_unsafe_directory_fails_closed,
)

for test in tests:
    test()
    print(f"PASS {test.__name__}")
