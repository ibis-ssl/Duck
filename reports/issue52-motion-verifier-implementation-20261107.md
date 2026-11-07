# Issue #52: same active-window motion verifier

## Target and scope

- Issue: [#52](https://github.com/ibis-ssl/Duck/issues/52)
- Draft child PR: [#119](https://github.com/ibis-ssl/Duck/pull/119)
- Branch: `task/issue52-aspire-006g2`
- Base: `task/issue50-aspire-006g` at `1660cfb519f373c8263df09599a7e11f0a18e699`
- Current implementation HEAD: `ff1ce32b8d9245f3ee0a46b575d035f785839e56`
- Scope: offline verifier and focused regression tests for existing `ASPIRE-MATCH-003`; no product behavior or new external dependency.

## Implementation

`scripts/verify_aspire_motion_acceptance.py` parses SSL log v1 records, associates vision frames with contiguous referee command intervals, and checks movement for Crane (Yellow) and TIGERs (Blue) in one active interval. A STOP, malformed referee packet, different command, or stale/regressed uint32 command counter closes the interval; serial arithmetic handles forward wraparound. Per-frame duplicate robot IDs are excluded. Previously decoded summaries survive record failures while the result remains `incomplete`. Preparatory, stopped, timeout, goal-notification, and ball-placement commands do not open active-motion intervals.

SSL log v1 does not retain source endpoints. Sender identity remains explicitly unverified and requires a separate current-run packet capture.

## Tests and validation

- `python scripts/tests/test_verify_aspire_motion_acceptance.py`: **13/13 passed** on HEAD `ff1ce32b8d9245f3ee0a46b575d035f785839e56`.
- `python -m py_compile scripts/verify_aspire_motion_acceptance.py scripts/tests/test_verify_aspire_motion_acceptance.py`: passed.
- `git diff --check`: passed.
- Exact-head GitHub Actions run [42380286902](https://github.com/ibis-ssl/Duck/actions/runs/42380286902): standard `test` job succeeded. NET-002 and full-stack acceptance were skipped; the CI run did not execute these Python motion tests.
- The initial regression tests reproduced the false passes and missing partial summaries before fixes. Normal review verified the counter, duplicate-ID, and partial-evidence fixes.

A read-only parser smoke test on the historical #49 archive `issue49-blue-only-match-20261106-l / 2026-11-06_13-03-51.log.gz` parsed 40,238 records (6,151 referee, 9,707 vision) with clean gzip EOF and found both teams moving in one NORMAL_START window (counter 2, 342 vision packets). SHA-256: `F9876EF60100FE07C2970197EE625F7F951CBABDB817110824946EB8953C8DB2`. This archive predates the #51 merge and is only real-format parser evidence, not current #52 acceptance.

## Reviews

Normal fix-verification review: **pass_with_held** at exact HEAD `ff1ce32b8d9245f3ee0a46b575d035f785839e56`; findings I52-NR-001, I52-NR-002, and I52-NR-003 (all P2/medium) are closed. Independent final review is pending.

## Blocked and unverified

- Docker was not executed. #43 has no dedicated PR; its recorded CI skipped Linux packet flow and full-stack acceptance. #49 PR #116 reports a Docker-backed match but omits the Docker Desktop/Engine version. Available host records establish Docker Desktop 4.96.0.243774 / Engine 29.9.1 at other times, not conclusively for the #43 runtime. Therefore the user's same-version condition is unresolved.
- Current live `ASPIRE-MATCH-003`, current-run environment/network/source identity, and corresponding stdout/stderr evidence remain unverified. Issue #52 is not complete. PR #119 remains Draft; no merge was attempted.
- The normal reviewer could not independently introspect the applied model/effort. Spawn metadata records the requested `gpt-6.1-sol/high` profile.

## Next action

Resolve the #43 Docker-version comparison from a primary record. If the condition is established, run the current live match acceptance and collect source-capture evidence. Otherwise keep the live acceptance and Issue #52 open.
