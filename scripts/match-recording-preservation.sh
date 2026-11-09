#!/usr/bin/env bash

capture_crane_recordings() {
  local crane_id="$1" bag_paths="$2" crane_capture_dir="$3" raw_recovery_root="$4"
  local apphost_dll="$5" recorder_shutdown_exit_code="$6" discovery_exit_code="$7"
  local bag_count=0 recovery_archive_count=0 raw_recovery_count=0 copy_failed=false
  mkdir -p "$raw_recovery_root"

  while IFS= read -r metadata_path; do
    [[ -n "$metadata_path" ]] || continue
    local bag_dir bag_name target_dir archive_path recovery_archive
    bag_dir="$(dirname "$metadata_path")"
    bag_name="$(basename "$bag_dir")"
    if [[ ! "$bag_name" =~ ^[A-Za-z0-9._-]+$ || "$bag_name" == "." || "$bag_name" == ".." ]]; then
      copy_failed=true
      continue
    fi

    target_dir="$raw_recovery_root/$bag_name"
    if docker cp "$crane_id:$bag_dir" "$target_dir" \
      > "$crane_capture_dir/docker-cp-$bag_name.log" 2>&1; then
      archive_path="$crane_capture_dir/$bag_name.tar.gz"
      if tar -C "$raw_recovery_root" -czf "$archive_path" "$bag_name" \
        && gzip -t "$archive_path" \
        && sha256sum "$archive_path" > "$archive_path.sha256"; then
        bag_count=$((bag_count + 1))
        rm -rf "$target_dir"
      else
        copy_failed=true
        recovery_archive="$crane_capture_dir/$bag_name-recovery.zip.gz"
        if dotnet "$apphost_dll" --preserve-crane-recording "$target_dir" "$recovery_archive" \
          > "$crane_capture_dir/recovery-$bag_name.log" 2>&1; then
          recovery_archive_count=$((recovery_archive_count + 1))
          rm -rf "$target_dir"
        else
          raw_recovery_count=$((raw_recovery_count + 1))
          printf 'raw_recording_preserved=true\narchive_status=failed\n' \
            > "$target_dir/preservation-status.txt"
        fi
      fi
    else
      copy_failed=true
      if [[ -d "$target_dir" ]]; then
        raw_recovery_count=$((raw_recovery_count + 1))
        printf 'raw_recording_preserved=true\narchive_status=docker-copy-failed\n' \
          > "$target_dir/preservation-status.txt"
      fi
    fi
  done < "$bag_paths"

  if [[ "$bag_count" -gt 0 && "$copy_failed" == false && \
    "$recorder_shutdown_exit_code" -eq 0 && "$discovery_exit_code" -eq 0 ]]; then
    printf 'status=archived\nbag_count=%s\nrecorder_shutdown_exit_code=%s\ndiscovery_exit_code=%s\narchive_validation=gzip-crc-and-sha256\n' \
      "$bag_count" "$recorder_shutdown_exit_code" "$discovery_exit_code" \
      > "$crane_capture_dir/capture-status.txt"
  elif [[ "$copy_failed" == true ]]; then
    printf 'status=copy-failed\nbag_count=%s\nrecovery_archive_count=%s\nraw_recovery_count=%s\nrecorder_shutdown_exit_code=%s\ndiscovery_exit_code=%s\n' \
      "$bag_count" "$recovery_archive_count" "$raw_recovery_count" "$recorder_shutdown_exit_code" "$discovery_exit_code" \
      > "$crane_capture_dir/capture-status.txt"
  elif [[ "$recorder_shutdown_exit_code" -ne 0 ]]; then
    printf 'status=recorder-shutdown-failed\nbag_count=%s\nrecorder_shutdown_exit_code=%s\ndiscovery_exit_code=%s\n' \
      "$bag_count" "$recorder_shutdown_exit_code" "$discovery_exit_code" \
      > "$crane_capture_dir/capture-status.txt"
  elif [[ "$discovery_exit_code" -ne 0 ]]; then
    printf 'status=discovery-failed\nbag_count=%s\nrecorder_shutdown_exit_code=%s\ndiscovery_exit_code=%s\n' \
      "$bag_count" "$recorder_shutdown_exit_code" "$discovery_exit_code" \
      > "$crane_capture_dir/capture-status.txt"
  else
    printf 'status=missing\nbag_count=0\nrecorder_shutdown_exit_code=%s\ndiscovery_exit_code=%s\n' \
      "$recorder_shutdown_exit_code" "$discovery_exit_code" \
      > "$crane_capture_dir/capture-status.txt"
  fi
}

preserve_ssl_before_finalization() {
  local recorder_source="$1" raw_copy="$2" match_results_dir="$3" apphost_dll="$4" stack_id="$5"
  local finalizer_command="${6:-python3}"
  if ! dotnet "$apphost_dll" --preserve-ssl-archives "$recorder_source" "$raw_copy" \
    > "$match_results_dir/ssl-archive-preservation.log" 2>&1; then
    printf 'status=copy-failed-or-no-archives\n' \
      > "$match_results_dir/ssl-archive-preservation-status.txt"
    return 1
  fi

  printf 'status=copied-before-validation\n' \
    > "$match_results_dir/ssl-archive-preservation-status.txt"
  "$finalizer_command" scripts/verify_aspire_match_acceptance.py \
    --stack-id "$stack_id" --results-dir "$match_results_dir" --finalize
}
