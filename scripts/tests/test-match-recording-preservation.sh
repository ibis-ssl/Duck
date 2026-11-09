#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT
fake_bin="$scratch/bin"
mkdir -p "$fake_bin"

cat > "$fake_bin/docker" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
[[ "$1" == cp ]]
target="${@: -1}"
mkdir -p "$target/data"
printf '\x00\xffcrane-recording\x80' > "$target/data/chunk.db3"
SH
cat > "$fake_bin/tar" <<'SH'
#!/usr/bin/env bash
exit 23
SH
cat > "$fake_bin/dotnet" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
case "$2" in
  --preserve-crane-recording) exit 24 ;;
  --preserve-ssl-archives)
    mkdir -p "$4"
    cp "$3"/*.log.gz "$4/"
    printf 'preserved_ssl_archive_count=1\n'
    ;;
  *) exit 64 ;;
esac
SH
cat > "$fake_bin/python3" <<'SH'
#!/usr/bin/env bash
exit 25
SH
chmod +x "$fake_bin"/*
PATH="$fake_bin:$PATH"
source "$repo_root/scripts/match-recording-preservation.sh"

# Compose docker copy, archive failure, recovery failure, and artifact-boundary retention.
crane_results="$scratch/artifacts/aspire-full-stack/match/crane-recording"
crane_raw="$scratch/artifacts/aspire-full-stack-raw/crane-recording"
mkdir -p "$crane_results"
printf '/root/bag-one/metadata.yaml\n' > "$scratch/bag-paths.txt"
if [[ "$crane_raw/" == "$crane_results/"* ]]; then
  echo 'raw Crane evidence must remain outside the sanitizer input tree' >&2
  exit 1
fi
capture_crane_recordings crane-container "$scratch/bag-paths.txt" "$crane_results" "$crane_raw" \
  "$scratch/AppHost.dll" 0 0
[[ -f "$crane_raw/bag-one/preservation-status.txt" ]]
grep -Fxq 'status=copy-failed' "$crane_results/capture-status.txt"
grep -Fxq 'raw_recovery_count=1' "$crane_results/capture-status.txt"
printf '\x00\xffcrane-recording\x80' > "$scratch/expected.db3"
cmp "$scratch/expected.db3" "$crane_raw/bag-one/data/chunk.db3"

# Compose raw SSL preservation followed by a failing finalizer; failed status and bytes remain.
ssl_source="$scratch/recorder"
ssl_results="$scratch/artifacts/aspire-full-stack/match"
ssl_raw="$ssl_results/ssl-archive-raw"
mkdir -p "$ssl_source" "$ssl_results"
printf '\x1f\x8b\x08\x00\xfftruncated' > "$ssl_source/partial.log.gz"
if preserve_ssl_before_finalization "$ssl_source" "$ssl_raw" "$ssl_results" "$scratch/AppHost.dll" stack-1; then
  echo 'failing finalizer unexpectedly succeeded' >&2
  exit 1
fi
grep -Fxq 'status=copied-before-validation' "$ssl_results/ssl-archive-preservation-status.txt"
cmp "$ssl_source/partial.log.gz" "$ssl_raw/partial.log.gz"

echo 'match recording preservation composition fixtures passed'
