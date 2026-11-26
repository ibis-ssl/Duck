#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 2 ]]; then
  printf 'Usage: %s <output-jar> <evidence-directory>\n' "$0" >&2
  exit 2
fi

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
agent_source="$repo_root/Testing/Duck.Testing.AppHost/AutoRefGameLogAgent/AutoRefGameLogAgent.java"

output_jar="$1"
evidence_dir="$2"
if [[ "$output_jar" != /* ]]; then
  output_jar="$repo_root/$output_jar"
fi
if [[ "$evidence_dir" != /* ]]; then
  evidence_dir="$repo_root/$evidence_dir"
fi

mkdir -p "$(dirname -- "$output_jar")" "$evidence_dir"

readonly evidence_files=(
  autoref-game-log-agent-docker-info.stdout.log
  autoref-game-log-agent-docker-info.stderr.log
  autoref-game-log-agent-docker.stdout.log
  autoref-game-log-agent-docker.stderr.log
  autoref-game-log-agent-image.txt
  autoref-game-log-agent-image.stderr.log
  autoref-game-log-agent-jdk-version.txt
  autoref-game-log-agent-contents.txt
  autoref-game-log-agent-builder-image.txt
  autoref-game-log-agent.sha256
)
for evidence_file in "${evidence_files[@]}"; do
  rm -f -- "$evidence_dir/$evidence_file"
done

status_file="$evidence_dir/autoref-game-log-agent-status.txt"
printf 'status=running\nstage=preflight\n' > "$status_file"
build_root=''
output_tmp=''
output_backup=''
output_had_previous=false
output_published=false
backup_restore_failed=false
failure_stage='preflight'

cleanup() {
  local exit_code=$?
  trap - EXIT
  set +e

  if [[ -n "$build_root" ]]; then
    rm -rf -- "$build_root"
  fi
  if [[ -n "$output_tmp" ]]; then
    rm -f -- "$output_tmp"
  fi

  if [[ "$exit_code" -ne 0 ]]; then
    if [[ "$output_published" == true ]]; then
      if [[ "$output_had_previous" == true && -n "$output_backup" && -f "$output_backup" ]]; then
        if mv -f -- "$output_backup" "$output_jar"; then
          output_backup=''
        else
          printf 'build-autoref-game-log-agent: could not restore the previous JAR; backup retained at %s\n' \
            "$output_backup" >&2
          backup_restore_failed=true
        fi
      else
        rm -f -- "$output_jar"
      fi
    fi
    if [[ -n "$output_backup" && "$backup_restore_failed" != true ]]; then
      rm -f -- "$output_backup"
    fi
    for evidence_file in \
      autoref-game-log-agent-image.txt \
      autoref-game-log-agent-jdk-version.txt \
      autoref-game-log-agent-contents.txt \
      autoref-game-log-agent-builder-image.txt \
      autoref-game-log-agent.sha256; do
      rm -f -- "$evidence_dir/$evidence_file"
    done
    printf 'status=failed\nstage=%s\nexit_code=%s\n' "$failure_stage" "$exit_code" > "$status_file"
  elif [[ -n "$output_backup" ]]; then
    rm -f -- "$output_backup"
  fi
  exit "$exit_code"
}
trap cleanup EXIT

print_log_on_failure() {
  local log_file="$1"
  if [[ -s "$log_file" ]]; then
    printf '\n--- %s ---\n' "$log_file" >&2
    cat -- "$log_file" >&2
  fi
}

fail() {
  printf 'build-autoref-game-log-agent: %s\n' "$1" >&2
  exit 1
}

failure_stage='source-validation'
if [[ ! -f "$agent_source" ]]; then
  fail "AutoRef game-log agent source was not found: $agent_source"
fi

failure_stage='docker-cli'
if ! command -v docker >/dev/null 2>&1; then
  fail 'Docker CLI is required; no host JDK fallback is supported.'
fi

failure_stage='docker-info'
if ! docker info --format '{{.ServerVersion}} {{.OSType}}/{{.Architecture}}' \
  > "$evidence_dir/autoref-game-log-agent-docker-info.stdout.log" \
  2> "$evidence_dir/autoref-game-log-agent-docker-info.stderr.log"; then
  printf 'build-autoref-game-log-agent: Docker CLI is present, but the daemon is unavailable; see %s\n' \
    "$evidence_dir/autoref-game-log-agent-docker-info.stderr.log" >&2
  print_log_on_failure "$evidence_dir/autoref-game-log-agent-docker-info.stdout.log"
  print_log_on_failure "$evidence_dir/autoref-game-log-agent-docker-info.stderr.log"
  exit 1
fi

# Docker Official Image, Eclipse Temurin 17.0.21.1+2, Linux amd64 manifest.
# The digest is the platform manifest reported for this tag by Docker Hub.
readonly builder_image='eclipse-temurin:17.0.21.1_2-jdk-jammy@sha256:c2fd820ce6cc82ca15ef89165c4cd89e635d26908b675cc7723dfd1f12ce79e6'
failure_stage='docker-build'
build_root="$(mktemp -d "${TMPDIR:-/tmp}/duck-autoref-game-log-agent-${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-0}.XXXXXX")"

mkdir -p "$build_root/classes"
printf 'Premain-Class: edu.ibis.duck.autoref.AutoRefGameLogAgent\nCan-Redefine-Classes: false\n\n' \
  > "$build_root/manifest.mf"

set +e
docker run --rm \
  --pull=missing \
  --platform linux/amd64 \
  --network none \
  --user "$(id -u):$(id -g)" \
  --volume "$agent_source:/src/AutoRefGameLogAgent.java:ro" \
  --volume "$build_root:/build:rw" \
  --entrypoint /bin/sh \
  "$builder_image" \
  -euc 'java -version > /build/jdk-version.txt 2>&1
javac -version >> /build/jdk-version.txt 2>&1
javac --release 17 -d /build/classes /src/AutoRefGameLogAgent.java
jar --create --file /build/autoref-game-log-agent.jar --manifest /build/manifest.mf -C /build/classes .
jar --list --file /build/autoref-game-log-agent.jar > /build/contents.txt' \
  > "$evidence_dir/autoref-game-log-agent-docker.stdout.log" \
  2> "$evidence_dir/autoref-game-log-agent-docker.stderr.log"
docker_exit_code=$?
set -e

if [[ "$docker_exit_code" -ne 0 ]]; then
  printf 'build-autoref-game-log-agent: Docker JDK 17 build failed (exit %s); see %s and %s\n' \
    "$docker_exit_code" \
    "$evidence_dir/autoref-game-log-agent-docker.stdout.log" \
    "$evidence_dir/autoref-game-log-agent-docker.stderr.log" >&2
  print_log_on_failure "$evidence_dir/autoref-game-log-agent-docker.stdout.log"
  print_log_on_failure "$evidence_dir/autoref-game-log-agent-docker.stderr.log"
  exit "$docker_exit_code"
fi

failure_stage='image-inspect'
if ! docker image inspect \
  --format '{{.Id}} {{json .RepoDigests}}' \
  "$builder_image" \
  > "$evidence_dir/autoref-game-log-agent-image.txt" 2> "$evidence_dir/autoref-game-log-agent-image.stderr.log"; then
  printf 'build-autoref-game-log-agent: could not record the resolved pinned builder image; see %s\n' \
    "$evidence_dir/autoref-game-log-agent-image.stderr.log" >&2
  print_log_on_failure "$evidence_dir/autoref-game-log-agent-image.stderr.log"
  exit 1
fi

failure_stage='evidence-publish'
output_tmp="$(mktemp "$(dirname -- "$output_jar")/.autoref-game-log-agent.XXXXXX")"
install -m 0644 "$build_root/autoref-game-log-agent.jar" "$output_tmp"
install -m 0644 "$build_root/jdk-version.txt" "$evidence_dir/autoref-game-log-agent-jdk-version.txt"
install -m 0644 "$build_root/contents.txt" "$evidence_dir/autoref-game-log-agent-contents.txt"
printf '%s\n' "$builder_image" > "$evidence_dir/autoref-game-log-agent-builder-image.txt"
output_sha256="$(sha256sum "$output_tmp" | awk '{print $1}')"
printf '%s  %s\n' "$output_sha256" "$output_jar" > "$evidence_dir/autoref-game-log-agent.sha256"

failure_stage='jar-publish'
if [[ -e "$output_jar" ]]; then
  output_had_previous=true
  output_backup="$(mktemp "$(dirname -- "$output_jar")/.autoref-game-log-agent-backup.XXXXXX")"
  cp -p -- "$output_jar" "$output_backup"
fi
mv -f -- "$output_tmp" "$output_jar"
output_tmp=''
output_published=true
failure_stage='complete'
printf 'status=success\nstage=complete\nexit_code=0\noutput_sha256=%s\n' "$output_sha256" > "$status_file"
