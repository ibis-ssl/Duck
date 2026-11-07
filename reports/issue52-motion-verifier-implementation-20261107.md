# Issue #52: same active-window motion verifier

## Target and scope

- Issue: [#52](https://github.com/ibis-ssl/Duck/issues/52)
- Draft child PR: [#119](https://github.com/ibis-ssl/Duck/pull/119)
- Branch: `task/issue52-aspire-006g2`
- Base: `task/issue50-aspire-006g` at `1660cfb519f373c8263df09599a7e11f0a18e699`
- Source HEAD: `a95ad91c8e45718c56219110063739a111301034`
- Scope: an offline verifier and focused regression tests for existing `ASPIRE-MATCH-003`; no product behavior or new external dependency.

## Implementation

`scripts/verify_aspire_motion_acceptance.py` parses SSL log v1 records, associates vision frames with contiguous referee command intervals, and checks movement for Crane (Yellow) and TIGERs (Blue) in one active interval. STOP, malformed referee packets, different commands, and stale/regressed uint32 command counters close an interval; serial arithmetic handles forward wraparound. Duplicate robot IDs are excluded within a frame. The parser fully validates each Vision wrapper, rejects multiple detection-frame fields, and still accepts well-formed unknown protobuf fields. Previously decoded summaries survive record failures while the result remains `incomplete`.

SSL log v1 does not retain source endpoints. Sender identity remains explicitly unverified and requires separate current-run packet-capture evidence.

## Tests and validation

- `python scripts/tests/test_verify_aspire_motion_acceptance.py`: **16/16 passed** at source HEAD `a95ad91c8e45718c56219110063739a111301034`, including CLI partial-output retention.
- `python -m py_compile scripts/verify_aspire_motion_acceptance.py scripts/tests/test_verify_aspire_motion_acceptance.py`: passed.
- `git diff --check`: passed.
- Exact-head GitHub Actions run [42381288692](https://github.com/ibis-ssl/Duck/actions/runs/42381288692): standard `test` job succeeded; NET-002 and full-stack acceptance were skipped. CI did not execute these Python motion tests.
- Regression tests caught the stale-counter, duplicate-ID, partial-record, and malformed Vision-wrapper false passes before their respective fixes. Malformed wrapper tails (varint, truncated fixed32/length-delimited, unsupported wire type, zero field) no longer count as valid Vision packets; well-formed unknown fields remain accepted.

A read-only parser smoke test on historical #49 archive `issue49-blue-only-match-20261106-l / 2026-11-06_13-03-51.log.gz` parsed 40,238 records (6,151 referee, 9,707 vision) with clean gzip EOF and found both teams moving in one NORMAL_START window (counter 2, 342 vision packets). SHA-256: `F9876EF60100FE07C2970197EE625F7F951CBABDB817110824946EB8953C8DB2`. This archive predates #51 and is parser evidence only, not current #52 acceptance.

## Reviews

- Normal review at `ff1ce32b8d9245f3ee0a46b575d035f785839e56`: `pass_with_held`; I52-NR-001/002/003 closed.
- Independent final review at `81b79124ec3a5e9702e8c63b7c30c8adb99f7910`: `fail`; I52-IFR-001 identified incomplete validation of trailing Vision protobuf fields. Fixed at source HEAD `a95ad91c8e45718c56219110063739a111301034` and normal fix verification closed it, severity P2/medium preserved.
- Fresh independent final review of `a95ad91c8e45718c56219110063739a111301034`: pending.

## Blocked and unverified

- Docker was not run. #43 has no dedicated PR; its recorded CI skips Linux packet flow and full-stack acceptance. Child PR #116 reports a Docker-backed #49 match but omits the Docker Desktop/Engine version. The available source and user-message history contain an earlier assistant claim of equality, then a correction that the #43 version was not recorded. Docker Desktop 4.96.0.243774 / Engine 29.9.1 is measured at other times, not tied to #43's runtime; the user's same-version condition remains unresolved.
- Current live `ASPIRE-MATCH-003`, current-run environment/network/source identity, and corresponding stdout/stderr remain unverified. Issue #52 is not complete. PR #119 remains Draft; no merge was attempted.
- Reviewer runtime could not independently introspect the applied model/effort; caller spawn metadata specified `gpt-6.1-sol/high` for normal and independent reviews.

## Next action

Resolve the #43 Docker-version comparison from a primary record. If it is established, run current live `ASPIRE-MATCH-003` and capture environment/network/source evidence. Otherwise keep the live acceptance and Issue #52 open.
