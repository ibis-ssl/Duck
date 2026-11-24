#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
workflow="$repo_root/.github/workflows/dotnet-test.yml"
scratch="$(mktemp -d)"
trap 'rm -rf "$scratch"' EXIT

# Run the same two shell blocks used in Actions, with a fresh evidence tree for each case.
extract_run_block() {
  awk -v heading="$1" '
    $0 == "      - name: " heading { found = 1; next }
    found && /^      - / { exit }
    found && /^        run: \|$/ { body = 1; next }
    body && /^          / { print substr($0, 11); next }
    body && /^$/ { print; next }
    body { exit }
    END { if (!body) exit 1 }
  ' "$workflow"
}

extract_run_block 'MATCH-005の終了点と証跡保存を確認' > "$scratch/produce.sh"
extract_run_block 'Require a completed match with in-play referee progress' > "$scratch/require.sh"

run_case() {
  local name="$1" expected_producer="$2" expected_gate="$3"
  local root="$scratch/$name" base match
  base="$root/artifacts/aspire-full-stack"
  match="$base/match"
  mkdir -p "$base/resource-logs" "$match/crane-recording"

  jq -n '{
    status:"passed", fullMatchPassed:true, match005EndpointReached:true, match005EndpointKind:"post_game",
    stageReached:"post_game", terminationKind:"PostGame", configuredMaximumDuration:"00:15:00",
    inPlayProgress:{
      first:{stage:"NORMAL_FIRST_HALF", commandCounter:7, packetTimestamp:100,
        refereeCommand:"NormalStart", gameControllerPhase:"Running", stageTimeLeft:"00:00:42.12"},
      later:{stage:"NORMAL_FIRST_HALF", commandCounter:7, packetTimestamp:101,
        refereeCommand:"NormalStart", gameControllerPhase:"Running", stageTimeLeft:"00:00:41.422"}
    }
  }' > "$match/match-result.json"

  local resource
  for resource in simulator game-controller cm4-sim crane duck tigers-blue autoref-tigers ssl-log-recorder match-controller; do
    printf 'resource=%s\tcontainer_id=fixture\tstatus=captured\n' "$resource"
  done > "$base/resource-logs/live-log-capture-status.tsv"
  printf 'status=archived\nrecorder_shutdown_exit_code=0\ndiscovery_exit_code=0\n' > "$match/crane-recording/capture-status.txt"
  : > "$match/crane-recording/fixture.tar.gz"
  printf '{"eventName":"match_terminated"}\n' > "$match/match-transitions.stdout.log"

  local filter='.'
  case "$name" in
    failed_post_game) filter='.status="failed" | .fullMatchPassed=false' ;;
    missing_progress) filter='del(.inPlayProgress)' ;;
    stopped_clock) filter='.inPlayProgress.later.stageTimeLeft = .inPlayProgress.first.stageTimeLeft' ;;
    watchdog)
      filter='.status="failed" | .fullMatchPassed=false | .match005EndpointKind="maximum_duration_watchdog"
        | .stageReached="maximum_time" | .terminationKind="MaximumTimeReached"
        | .maximumTimeContinuationState={stage:"NORMAL_SECOND_HALF",command:"STOP",stageTimeLeft:"00:00:14"}'
      ;;
    missing_recording) rm "$match/crane-recording/capture-status.txt" ;;
  esac
  jq "$filter" "$match/match-result.json" > "$match/result.tmp"
  mv "$match/result.tmp" "$match/match-result.json"
  jq '{fullMatchPassed}' "$match/match-result.json" > "$match/match-acceptance.json"

  local producer=0 gate=0
  GITHUB_WORKSPACE="$root" GITHUB_STEP_SUMMARY="$root/summary.md" MATCH_ARCHIVE_SUCCESS=true MATCH_SANITIZATION_SUCCESS=true \
    bash "$scratch/produce.sh" > "$root/produce.log" 2>&1 || producer=$?
  GITHUB_WORKSPACE="$root" bash "$scratch/require.sh" > "$root/require.log" 2>&1 || gate=$?
  if [[ "$producer" -ne "$expected_producer" ]] || [[ "$gate" -eq 0 && "$expected_gate" -ne 0 ]] ||
      [[ "$gate" -ne 0 && "$expected_gate" -eq 0 ]]; then
    printf '%s: unexpected producer=%s, strict_gate=%s\n' "$name" "$producer" "$gate" >&2
    cat "$root/produce.log" "$root/require.log" >&2
    exit 1
  fi
  printf '%s: producer=%s strict_gate=%s\n' "$name" "$producer" "$gate"
}

run_case post_game 0 0
run_case failed_post_game 0 1
run_case missing_progress 0 1
run_case stopped_clock 0 1
run_case watchdog 0 1
run_case missing_recording 1 1
