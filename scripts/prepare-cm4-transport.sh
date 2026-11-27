#!/usr/bin/env bash
# Build the Linux x64 helper and bind the output to its exact source inputs.
set -euo pipefail
root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
if [[ $# != 1 || $(uname -s) != Linux || $(uname -m) != x86_64 ]]; then
  echo 'Usage: prepare-cm4-transport.sh <output-directory> (Linux x64 with cc, static libc, readelf, sha256sum)' >&2
  exit 1
fi
mkdir -p -- "$1"
out="$(cd -- "$1" && pwd -P)"
tmp="$(mktemp -d "$out/.cm4-prepare.XXXXXX")"
trap 'rm -rf -- "$tmp"' EXIT
cd "$root/native/cm4-transport"
inputs=(control_main_posix.c transport_control_frontend.c transport_control_frontend.h transport_core.c transport_core.h transport_path_policy.c transport_path_policy.h transport_server_posix.c transport_server_posix.h)
sha256sum "${inputs[@]}" > "$tmp/cm4-transport-control.sha256"
cc -std=c11 -O2 -Wall -Wextra -Werror -pedantic -static -I . \
  transport_core.c transport_control_frontend.c transport_path_policy.c transport_server_posix.c control_main_posix.c \
  -o "$tmp/cm4-transport-control"
readelf -h "$tmp/cm4-transport-control" | grep -q 'Advanced Micro Devices X86-64'
if readelf -lW "$tmp/cm4-transport-control" | grep -E 'INTERP|DYNAMIC'; then
  echo 'The CM4 helper must be statically linked.' >&2
  exit 1
fi
# Refuse a source change during compilation.
sha256sum -c "$tmp/cm4-transport-control.sha256"
(cd "$tmp" && sha256sum cm4-transport-control >> cm4-transport-control.sha256)
install -m 0755 "$tmp/cm4-transport-control" "$out/cm4-transport-control"
install -m 0644 "$tmp/cm4-transport-control.sha256" "$out/cm4-transport-control.sha256"
