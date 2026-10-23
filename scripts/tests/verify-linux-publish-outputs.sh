#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
publish_root="$(mktemp -d "${TMPDIR:-/tmp}/duck-issue81-linux-publish.XXXXXX")"
evidence_root="$repo_root/artifacts/linux-publish"
mkdir -p "$evidence_root"
trap 'rm -rf -- "$publish_root"' EXIT

# 既存プロジェクトを発行し、生成された配布出力を固定契約と照合する。
publish_project() {
  local project_path="$1"
  local profile_name="$2"
  local output_name="$3"
  local framework_name="$4"
  shift 4
  local project_dir="$repo_root/$project_path"
  local profile_path="$project_dir/Properties/PublishProfiles/$profile_name.pubxml"
  local publish_dir="$publish_root/$output_name"
  local stdout_log="$evidence_root/$output_name.publish.stdout.log"
  local stderr_log="$evidence_root/$output_name.publish.stderr.log"
  local exit_code_file="$evidence_root/$output_name.publish.exit-code"
  local check_stdout="$evidence_root/$output_name.check.stdout.log"
  local check_stderr="$evidence_root/$output_name.check.stderr.log"
  local check_exit="$evidence_root/$output_name.check.exit-code"

  : > "$stdout_log"
  : > "$stderr_log"
  : > "$check_stdout"
  : > "$check_stderr"
  printf 'not-run\n' > "$exit_code_file"
  printf 'not-run\n' > "$check_exit"

  if [[ ! -f "$profile_path" ]]; then
    printf 'Required Linux publish profile is missing: %s\n' "$profile_path" | tee "$check_stderr" >&2
    printf '1\n' > "$check_exit"
    return 1
  fi

  printf 'Publishing %s with profile %s into %s\n' "$project_path" "$profile_name" "$publish_dir"
  # portable な framework-dependent 出力なので RID を付けず、既存の復元結果を再利用する。
  if dotnet publish "$project_dir" \
    --configuration Release \
    --no-restore \
    --output "$publish_dir" \
    -p:PublishProfile="$profile_name" \
    >"$stdout_log" 2>"$stderr_log"; then
    printf '0\n' > "$exit_code_file"
  else
    local publish_exit_code=$?
    printf '%s\n' "$publish_exit_code" > "$exit_code_file"
    cat "$stdout_log"
    cat "$stderr_log" >&2
    return "$publish_exit_code"
  fi

  cat "$stdout_log"
  cat "$stderr_log" >&2

  local required_path
  for required_path in "$@"; do
    if [[ ! -f "$publish_dir/$required_path" ]]; then
      printf 'Required published file is missing: %s\n' "$publish_dir/$required_path" | tee -a "$check_stderr" >&2
      printf '1\n' > "$check_exit"
      return 1
    fi
  done
  if [[ -e "$publish_dir/$output_name" ]]; then
    printf 'Unexpected native apphost is present: %s\n' "$publish_dir/$output_name" | tee -a "$check_stderr" >&2
    printf '1\n' > "$check_exit"
    return 1
  fi

  if python3 - "$publish_dir/$output_name.runtimeconfig.json" "$framework_name" >"$check_stdout" 2>"$check_stderr" <<'PY'
import json
import pathlib
import sys

runtime_config_path = pathlib.Path(sys.argv[1])
required_framework = sys.argv[2]
runtime_config = json.loads(runtime_config_path.read_text(encoding="utf-8"))
options = runtime_config.get("runtimeOptions", {})
frameworks = options.get("frameworks", [])
if not frameworks and "framework" in options:
    frameworks = [options["framework"]]

matching = [
    item for item in frameworks
    if item.get("name") == required_framework
    and item.get("version", "").startswith("10.0.")
]
if not matching or "includedFrameworks" in options:
    print(
        f"Expected framework-dependent {required_framework} 10.0.x in {runtime_config_path}; "
        f"found {frameworks}",
        file=sys.stderr,
    )
    raise SystemExit(1)
PY
  then
    printf '0\n' > "$check_exit"
  else
    local check_status=$?
    printf '%s\n' "$check_status" > "$check_exit"
    cat "$check_stdout"
    cat "$check_stderr" >&2
    return "$check_status"
  fi
}

publish_project \
  "Tracker/Tracker.RuntimeHost" \
  "LinuxContainer" \
  "Tracker.RuntimeHost" \
  "Microsoft.NETCore.App" \
  "Tracker.RuntimeHost.dll" \
  "Tracker.RuntimeHost.deps.json" \
  "Tracker.RuntimeHost.runtimeconfig.json" \
  "appsettings.json"

publish_project \
  "Tracker/Tracker.DebugHost" \
  "LinuxContainer" \
  "Tracker.DebugHost" \
  "Microsoft.AspNetCore.App" \
  "Tracker.DebugHost.dll" \
  "Tracker.DebugHost.deps.json" \
  "Tracker.DebugHost.runtimeconfig.json" \
  "appsettings.json" \
  "wwwroot/app.css" \
  "wwwroot/favicon.png"

publish_project \
  "Testing/Duck.Testing.RefereeDriver.Host" \
  "LinuxContainer" \
  "Duck.Testing.RefereeDriver.Host" \
  "Microsoft.AspNetCore.App" \
  "Duck.Testing.RefereeDriver.Host.dll" \
  "Duck.Testing.RefereeDriver.Host.deps.json" \
  "Duck.Testing.RefereeDriver.Host.runtimeconfig.json"

python3 "$repo_root/scripts/tests/test-debughost-portable-pdb.py"

python3 "$repo_root/scripts/tests/verify-debughost-portable-pdb.py" \
  --publish-directory "$publish_root/Tracker.DebugHost" \
  --expected-document "/src/Tracker/Tracker.DebugHost/Program.cs"

python3 "$repo_root/scripts/tests/test-linux-distribution-definitions.py"

python3 "$repo_root/scripts/tests/verify-linux-distribution-definitions.py" \
  --repo-root "$repo_root" \
  --publish-root "$publish_root"

printf 'Linux publish output contract passed for all three managed applications.\n'
