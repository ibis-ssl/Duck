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
export npm_config_cache="$CODEX_CLOUD_TOOLS_HOME/npm-cache"
export PIP_CACHE_DIR="$CODEX_CLOUD_TOOLS_HOME/pip-cache"

fail() {
  printf 'duck-cloud-prepare: %s\n' "$1" >&2
  exit 1
}

expected_version="$(python3 - "$repo_root/tools/cloud/toolchain.json" <<'PY'
import json
import sys
with open(sys.argv[1], encoding="utf-8") as source:
    print(json.load(source)["dotnet_sdk"]["version"])
PY
)" || fail 'could not read the configured SDK version'
expected_dotnet="$DOTNET_ROOT/dotnet"
resolved_dotnet="$(command -v dotnet || true)"
[[ "$resolved_dotnet" == "$expected_dotnet" ]] \
  || fail "PATH resolves dotnet to '$resolved_dotnet', expected '$expected_dotnet'"
actual_version="$(dotnet --version)" || fail 'dotnet --version failed'
[[ "$actual_version" == "$expected_version" ]] \
  || fail "dotnet reports '$actual_version', expected '$expected_version'"

git submodule update --init --recursive
dotnet restore Duck.slnx --disable-parallel
dotnet restore Tracker/Tracker.Tests/Tracker.Tests.csproj --disable-parallel
dotnet build Duck.slnx --no-restore -m:1 -p:BuildInParallel=false /nr:false
dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 -p:BuildInParallel=false /nr:false
npm run lint:md:setup
