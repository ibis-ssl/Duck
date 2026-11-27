#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd -P)"
cd "$repo_root"

export CODEX_CLOUD_TOOLS_HOME="${CODEX_CLOUD_TOOLS_HOME:-/workspace/.duck-env}"
export DOTNET_ROOT="$CODEX_CLOUD_TOOLS_HOME/dotnet"
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME="$CODEX_CLOUD_TOOLS_HOME/dotnet-home"
export NUGET_PACKAGES="$CODEX_CLOUD_TOOLS_HOME/nuget-packages"
export NUGET_HTTP_CACHE_PATH="$CODEX_CLOUD_TOOLS_HOME/nuget-http-cache"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false

expected_version="$(python3 - "$repo_root/tools/cloud/toolchain.json" <<'PY'
import json
import sys
with open(sys.argv[1], encoding="utf-8") as source:
    print(json.load(source)["dotnet_sdk"]["version"])
PY
)"
expected_dotnet="$DOTNET_ROOT/dotnet"
resolved_dotnet="$(command -v dotnet || true)"
if [[ "$resolved_dotnet" != "$expected_dotnet" ]]; then
  printf 'duck-cloud-debughost: PATH resolves dotnet to %s, expected %s\n' \
    "${resolved_dotnet:-<missing>}" "$expected_dotnet" >&2
  exit 1
fi
actual_version="$(dotnet --version)"
if [[ "$actual_version" != "$expected_version" ]]; then
  printf 'duck-cloud-debughost: dotnet reports %s, expected %s\n' \
    "$actual_version" "$expected_version" >&2
  exit 1
fi

export ASPNETCORE_ENVIRONMENT=Development
export Tracker__Enabled=true
export Tracker__PublishUdp=false
export VisionReceiver__MulticastAddress=127.0.0.1
export VisionReceiver__Port=12020
export VisionReceiver__Profiles__sim__MulticastAddress=127.0.0.1
export VisionReceiver__Profiles__sim__Port=12020
export VisionReceiver__Profiles__fast__MulticastAddress=127.0.0.1
export VisionReceiver__Profiles__fast__Port=12020
export VisionReceiver__Profiles__default__MulticastAddress=127.0.0.1
export VisionReceiver__Profiles__default__Port=12020
export VisionReceiver__PacketCapture__DirectoryPath=/workspace/.duck-env/packet-captures

exec dotnet run --project Tracker/Tracker.DebugHost --no-build --no-restore \
  --no-launch-profile -- --urls http://127.0.0.1:5289
