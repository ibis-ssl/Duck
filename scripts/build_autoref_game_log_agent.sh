#!/usr/bin/env bash
set -euo pipefail

if [[ "$#" -ne 2 ]]; then
  printf 'Usage: %s <output-jar> <evidence-directory>\n' "$0" >&2
  exit 2
fi

output_jar="$1"
evidence_dir="$2"
agent_source="Testing/Duck.Testing.AppHost/AutoRefGameLogAgent/AutoRefGameLogAgent.java"
build_root="$(mktemp -d "${TMPDIR:-/tmp}/duck-autoref-game-log-agent-${GITHUB_RUN_ID:-local}-${GITHUB_RUN_ATTEMPT:-0}.XXXXXX")"
classes_dir="$build_root/classes"

mkdir -p "$classes_dir" "$(dirname "$output_jar")" "$evidence_dir"
javac --release 17 -d "$classes_dir" "$agent_source" \
  > "$evidence_dir/autoref-game-log-agent-javac.stdout.log" \
  2> "$evidence_dir/autoref-game-log-agent-javac.stderr.log"
printf 'Premain-Class: edu.ibis.duck.autoref.AutoRefGameLogAgent\nCan-Redefine-Classes: false\n\n' \
  > "$build_root/manifest.mf"
jar --create --file "$output_jar" --manifest "$build_root/manifest.mf" -C "$classes_dir" . \
  > "$evidence_dir/autoref-game-log-agent-jar.stdout.log" \
  2> "$evidence_dir/autoref-game-log-agent-jar.stderr.log"
jar --list --file "$output_jar" > "$evidence_dir/autoref-game-log-agent-contents.txt"
sha256sum "$output_jar" > "$evidence_dir/autoref-game-log-agent.sha256"
