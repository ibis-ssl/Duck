# Aspire 005 Crane probe diagnostics design

## Context

At PR #61 HEAD `2094d035dabd11c58ec2175af8cc3a0548d86cad`, exact-head run `37206501213` passed .NET but failed the full-stack readiness gate after 300 seconds. Simulator, Game Controller, and cm4-sim returned ready; Crane returned 503. The artifact showed the pinned current-stack Crane container running with its ROS launch and `crane_session_coordinator_node` processes. It did not record bounded probe exit codes, output, durations, or attempts, so it cannot distinguish setup failure/delay, ROS graph query timeout, or unexpected graph output. Motion and Tracker acceptance were not reached.

## Goal and non-goals

Add only enough restricted evidence to classify the next Crane readiness failure. Preserve the 10-second inner command timeout, 2-second kill-after, outer 300-second readiness deadline, exact coordinator graph predicate, resource ownership checks, readiness semantics, and scoped cleanup.

Do not change ports, team/color, referee behavior, image pin, dependency graph, readiness predicate, motion acceptance, or any timeout. Do not collect environment variables, credentials, tokens, setup file contents, unrelated command lines, or unrestricted container logs as part of this probe diagnostic.

## Probe behavior

Keep the existing command timeout and process-group behavior, adding a fixed phase marker after successful setup:

```text
docker exec <container-id> timeout --signal=TERM --kill-after=2s 10s bash -lc \
  'source /root/ibis_ws/install/setup.bash && printf "DUCK_CRANE_ROS_SETUP_OK\\n" && exec ros2 node list'
```

The marker is emitted only after setup returns successfully and immediately before the ROS graph query. Match it as an exact complete line; a substring is not a marker. It is diagnostic only and never readiness. Evaluate the coordinator predicate against the full output segment after that exact marker and before diagnostic excerpt truncation. Coordinator text before the marker cannot set readiness; a coordinator line beyond the excerpt cap still can.

For each completed attempt, measure monotonic elapsed time and update an in-memory attempt count and latest outcome:

- fixed resource-scoped event name and 1-based attempt number;
- elapsed milliseconds from a monotonic clock;
- Docker exec exit code, explicitly classifying timeout status 124;
- exact-line setup-marker presence;
- exact coordinator match after the marker;
- stdout and stderr excerpts capped at 512 characters each, with truncation flags.

Raw stdout/stderr may exist only transiently in the process result. Before retaining or logging diagnostic excerpts, sanitize the complete text and only then cap it; never retain the uncapped raw copy in diagnostic state. Implement a small in-process C# sanitizer matching existing rules in `scripts/capture_aspire_dcp_logs.py:sanitize`; do not launch Python from the wrapper. Put sanitizer input/expected-output cases in one checked-in fixture consumed by both C# tests and existing Python sanitizer tests. Cover secret field forms, bearer tokens, private-key blocks, and dashboard query tokens. Serialize each diagnostic as one `System.Text.Json` JSON line so embedded newline/control characters cannot create extra records. Emit only the sanitized JSON record to the redirected AppHost stderr file; never log raw excerpts or echo unsanitized data to Actions console. Keep workflow sanitization of the AppHost log before artifact upload as defense in depth. Because the workflow cleanup runs with error-tolerant shell handling, a sanitizer error must set an explicit sanitization-failed status and prevent uploading the affected unsanitized file; it must not silently proceed to upload. Do not include container ID, environment, credentials, setup file contents, non-fixed command lines, or output beyond sanitized capped excerpts.

After every completed attempt, update count and latest outcome. Emit progress when classification changes or at least two monotonic seconds have elapsed since the previous progress event; do not promise a maximum age while an attempt is still running, since a single probe may consume its 10-second timeout plus 2-second kill-after and process overhead. Always emit an unconditional final summary with total attempts and latest bounded outcome on the inner 360-second startup deadline or non-canceling wrapper exit. The outer 300-second gate cancels AppHost before that inner deadline. Its diagnostic path uses the latest completed-attempt progress record available at gate failure; do not promise a fixed age or claim a later cancellation summary is artifact-visible. At that failure step, the workflow captures DCP logs before stopping AppHost. The named artifact path for the latest progress record is `artifacts/aspire-full-stack/dcp-startup-failure/dcp-log-tails.txt`, produced by `scripts/capture_aspire_dcp_logs.py` and uploaded only after sanitizer success. Formatting or logging failure must not mark ready, stop retries, or prevent exact-owner cleanup.

Readiness stays true only when exit code is zero and the exact coordinator graph entry appears after the exact marker in the full output. Exit 124, other nonzero, missing marker, or exit zero without that post-marker node remains not-ready and retries as today.

## Tests

Add deterministic tests for:

1. Exit 124 with no marker and supported secret-sentinel output: timeout classification, marker false, not-ready, sanitized bounded latest outcome, and no sentinel in shared sanitizer fixture output.
2. Exit 0 with marker but no coordinator: graph mismatch and not-ready.
3. Exit 0 with coordinator before the marker only: not-ready.
4. Exit 0 with exact coordinator after the marker: ready even when the coordinator is beyond the 512-character diagnostic excerpt cap.
5. Marker substring embedded in an error line without the exact complete marker: not-ready.
6. Overlong stdout/stderr: capped excerpts and truncation flags, with no uncapped copy in diagnostic state.
7. Failed/timeout probes: latest completed-attempt state is preserved, retry and exact-owner cleanup still complete; inner 360-second deadline produces its unconditional final summary, while outer 300-second timeout artifact contains the latest completed-attempt progress record available at the named DCP artifact path, without assuming it was emitted during an in-flight probe.
8. Multiple attempts: monotonic nonnegative duration and accurate total count; completed identical outcomes obey the two-second progress emission rule, while tests explicitly allow a bounded probe to remain in flight across the outer deadline without manufacturing an exit result.
9. C# and Python sanitizer implementations agree on every shared fixture; JSON-line fields safely escape embedded newlines and controls. A forced workflow-sanitizer failure marks sanitization failed and excludes the affected file from upload.

Reuse existing infrastructure and add no third-party dependency. Linux lifecycle tests cover subprocess behavior; platform-neutral tests cover marker/predicate parsing, bounded outcomes, and classification. A Linux failure lifecycle test asserts that progress diagnostics are logged at the defined cadence, and workflow/artifact tests verify the latest progress appears in the sanitized DCP capture taken before AppHost shutdown.

## Acceptance evidence

After design review, implement the smallest diagnostic-only change, run focused tests and the full .NET suite, obtain normal implementation review, push to the existing PR branch, and inspect the hosted full-stack artifact. The diagnostic should classify the failure as setup not completed, timeout/nonzero during ROS graph query, or successful graph query without the exact node. Report startup readiness, motion/Tracker, and cleanup stages separately. Claim packet or motion success only if those acceptance steps execute and pass.

## Review disposition

Pending normal design review and parent acceptance. This proposal authorizes no implementation by itself.
