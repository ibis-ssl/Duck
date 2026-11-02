# Issue #42 — Red / Green verification

## Task and scope

- Issue: https://github.com/ibis-ssl/Duck/issues/42
- Draft PR: https://github.com/ibis-ssl/Duck/pull/105
- Branch: `task/issue42-aspire-006e-captureon-same-tick`
- Acceptance contract: use the saved alignment for the same diagnostics sample tick when present; otherwise use the latest tracker snapshot at or before the diagnostics data timestamp; never use a future snapshot.
- Scope: `TrackerSnapshotReplayReader` fallback selection and diagnostics-entry alignment selection, with focused replay tests. No design change was needed because the existing design already states the no-future rule.
- No Sub-issue was created: the issue is one cohesive resolver contract.

## Red evidence

| Contract | Baseline | Result | Evidence |
| --- | --- | --- | --- |
| Fallback excludes future snapshots | `92a53d305867c4cbc313a491c9d293b526a2947a` plus test diff SHA-256 `5108064990150801D09492D5B633722B281839AA8ABFA47F8BE74B7A6A5622A2` | Build succeeded; 7 tests ran, 6 passed and 1 failed. The fallback selected `future-nearest` at `12,202,000,000` instead of `latest-before` at `12,199,000,000` for diagnostics time `12,201,000,000`. | `reports/evidence/issue42-red-20261103-20261102T225737Z/` |
| Saved alignment is scoped to the diagnostics sample tick | `4fb678bd9b24fa16ecd38ba88b9734f1d9307aee` plus working diff SHA-256 `CE2CB7019F8E6AA0FB2977241FBD5E32CC8926AA74281B29D7A90E6F9701F94D` | Build succeeded; 1 test ran and failed as expected. A render-snapshot row sharing the diagnostics line sorted before the diagnostics-entry row and was selected (`render-snapshot` instead of `saved-alignment`). | `reports/evidence/issue42-red-tick-kind-20261102T230155Z/` |

| Future saved alignment falls back to latest-before | `44e0ef03d91a5a6833edea0724ec2644b986ab69` plus working-tree source diff SHA-256 `991178A422983C85D382849A7A56618E4C65887DFDFF24A429D9383D0323FD1E` | Build succeeded; the single test failed as expected, selecting `saved-future` at `12,202,000,000` instead of `latest-before` at `12,199,000,000` for diagnostics sample `12,201,000,000`. Exit code 1. | `reports/evidence/issue42-red-futuresaved-20261102T230952Z/` |

Exact command:

`dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~TrackerReplayIntegrationTddTests.ReadSession_FutureSavedAlignmentFallsBackToLatestBeforeSnapshot --no-restore -m:1 /nr:false --logger "trx;LogFileName=issue42-red.trx" --results-directory reports/evidence/issue42-red-futuresaved-20261102T230952Z`

The evidence folder contains `command.txt`, `stdout.log`, `stderr.log`, `issue42-red.trx`, and `source.diff`. Build emitted analyzer warnings, preserved in the logs. The fixture omits the render-snapshot row and points the ready diagnostics-entry saved alignment to a future timestamp.
## Implementation

- Filter saved alignment records to ready `DiagnosticsEntryTimelineKind` rows before grouping by diagnostics line.
- Precompute the external fallback candidates once, preserving the timestamp order established by `ReadSession`.
- Use binary search to select the last candidate whose tracked timestamp is at or before the diagnostics data timestamp. If no eligible candidate exists, emit no comparison for that entry instead of substituting a future snapshot.
- Report the matching rule as `latest-before-timestamp`.
- The saved-alignment preference and latest-before fallback are covered by the replay integration tests. The tests use the existing capture sidecars and `TrackerSnapshotReplayReader`; no Docker/Aspire replay environment was started.

## Green verification

- Focused command: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~TrackerReplayIntegrationTddTests --no-restore -m:1 /nr:false`
- Focused result: build succeeded; 7 passed, 0 failed, 0 skipped.
- Full project command: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore`
- Full result: 1,157 passed, 0 failed, 2 skipped. Console output ended with `Passed!`, and the TRX records the same counts. The command runner did not expose a separate native exit-code field; success is supported by the test summary and TRX. Build warnings are preserved in the logs.
- Green evidence: `reports/evidence/issue42-green-full-20261102T230311Z/` (commands, stdout/stderr, TRX, source diff, and source identity).
- Green baseline: `4037612894249c74e1c4037691c915f19d7833d8` plus the implementation diff.
- Green diff SHA-256: `C95B3165AC9AC3C674E945413BFF20F1F706FA1900426B61F8D1A0C6B797C05A`.
- `git diff --check` passed before the implementation commit.

## Execution profile

- Requested test-runner profile: `gpt-6-luna`, reasoning `high`.
- Applied runtime profile: unverified; runtime metadata is not visible to the parent.
- Implementation profile requested by the user: `gpt-6-luna`, reasoning `high`.
- Normal and independent review profiles requested by the user: `gpt-6.1-sol`, reasoning `high`; reviewer identities must be Issue #42-specific and continuous within #42.

## Remaining verification

- Normal and independent review of the final implementation HEAD are pending.
- GitHub Actions / external CI status is pending and is not represented as passed here.
- No merge, approval, `main` integration, or deployment was performed.
