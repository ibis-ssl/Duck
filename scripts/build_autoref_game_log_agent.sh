#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 2 ]]; then
  printf 'Usage: %s <output-jar> <evidence-directory>\n' "$0" >&2
  exit 2
fi

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd -P)"
agent_source="$repo_root/Testing/Duck.Testing.AppHost/AutoRefGameLogAgent/AutoRefGameLogAgent.java"
if [[ ! -f "$agent_source" ]]; then
  printf 'AutoRef game-log agent source was not found: %s\n' "$agent_source" >&2
  exit 1
fi

output_jar="$1"
evidence_dir="$2"
if [[ "$output_jar" != /* ]]; then
  output_jar="$repo_root/$output_jar"
fi
if [[ "$evidence_dir" != /* ]]; then
  evidence_dir="$repo_root/$evidence_dir"
fi

mkdir -p "$(dirname -- "$output_jar")" "$evidence_dir"

fail() {
  printf 'build-autoref-game-log-agent: %s\n' "$1" >&2
  exit 1
}

if ! command -v docker >/dev/null 2>&1; then
  fail 'Docker CLI is required; no host JDK fallback is supported.'
fi

if ! docker info --format '{{.ServerVersion}} {{.OSType}}/{{.Architecture}}' \
  > "$evidence_dir/autoref-game-log-agent-docker-info.stdout.log" \
  2> "$evidence_dir/autoref-game-log-agent-docker-info.stderr.log"; then
  printf 'build-autoref-game-log-agent: Docker CLI is present, but the daemon is unavailable; see %s\n' \
    "$evidence_dir/autoref-game-log-agent-docker-info.stderr.log" >&2
  exit 1
fi

# Docker Official Image, Eclipse Temurin 17.0.21.1+2, Linux amd64 manifest.
# The digest is the platform manifest reported for this tag by Docker Hub.
readonly builder_image='eclipse-temurin:17.0.21.1_2-jdk-jammy@sha256:c2fd820ce6cc82ca15ef89165c4cd89e635d26908b675cc7723dfd1f12ce79e6'
build_root="$(mktemp -d "${TMPDIR:-/tmp}/duck-autoref-game-log-agent-${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-0}.XXXXXX")"
cleanup() {
  rm -rf -- "$build_root"
}
trap cleanup EXIT

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
  exit "$docker_exit_code"
fi

if ! docker image inspect \
  --format '{{.Id}} {{json .RepoDigests}}' \
  "$builder_image" \
  > "$evidence_dir/autoref-game-log-agent-image.txt" 2> "$evidence_dir/autoref-game-log-agent-image.stderr.log"; then
  printf 'build-autoref-game-log-agent: could not record the resolved pinned builder image; see %s\n' \
    "$evidence_dir/autoref-game-log-agent-image.stderr.log" >&2
  exit 1
fi

install -m 0644 "$build_root/autoref-game-log-agent.jar" "$output_jar"
install -m 0644 "$build_root/jdk-version.txt" "$evidence_dir/autoref-game-log-agent-jdk-version.txt"
install -m 0644 "$build_root/contents.txt" "$evidence_dir/autoref-game-log-agent-contents.txt"
printf '%s\n' "$builder_image" > "$evidence_dir/autoref-game-log-agent-builder-image.txt"
sha256sum "$output_jar" > "$evidence_dir/autoref-game-log-agent.sha256"
