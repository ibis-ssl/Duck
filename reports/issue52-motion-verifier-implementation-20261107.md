# Issue #52: same active-window motion verifier

## Target and scope

- Issue: [#52](https://github.com/ibis-ssl/Duck/issues/52)
- Draft child PR: [#119](https://github.com/ibis-ssl/Duck/pull/119)
- Branch: `task/issue52-aspire-006g2`
- Base: `task/issue50-aspire-006g` at `1660cfb519f373c8263df09599a7e11f0a18e699`
- Implementation commit: `40550b237cb04c24019c0fb2de29a6e6e78fc40a`
- Scope: an offline verifier for the existing `ASPIRE-MATCH-003` criterion and focused tests. No product behavior or external dependency was added.

## Implementation

Added `scripts/verify_aspire_motion_acceptance.py`. It reads SSL log format v1 records, counts referee and vision packets, associates each vision frame with the most recent referee command in the recorded stream, and compares each robot's first and later decoded positions within the same command-counter/command window. Crane is read from Yellow and TIGERs from Blue, matching the accepted match configuration. No minimum displacement threshold was added.

`NORMAL_START`, `FORCE_START`, `DIRECT_FREE_*`, and `INDIRECT_FREE_*` are treated as active commands. Preparatory, stopped, timeout, goal-notification, and ball-placement commands do not open an active-motion window. The output retains packet counts, active-window results, a successful window if found, and partial counts for incomplete archives. SSL log v1 does not store source endpoints; the JSON explicitly marks sender identity as unavailable in this archive and calls for separate current-run source-capture evidence.

## Red/Green and local verification

- Red: `python scripts/tests/test_verify_aspire_motion_acceptance.py` against the initial verifier stub at `0381212` — the positive same-window case failed; the incomplete report contract was also absent.
- Green: the same focused suite against implementation commit `40550b2` — 5/5 passed.
- Syntax: `python -m py_compile scripts/verify_aspire_motion_acceptance.py scripts/tests/test_verify_aspire_motion_acceptance.py` — passed with Python 3.14.7.
- `git diff --check` — passed.
- `python -m ruff check ...` — unavailable; this environment has no `ruff` module. No dependency was installed.

A read-only parser smoke test against the historical #49 recorder archive `issue49-blue-only-match-20261106-l / 2026-11-06_13-03-51.log.gz` returned a clean gzip EOF and parsed 40,238 records (6,151 referee and 9,707 vision). It found both teams moving in the same recorded `NORMAL_START` window (command counter 2; 342 vision packets). The archive SHA-256 was `F9876EF60100FE07C2970197EE625F7F951CBABDB817110824946EB8953C8DB2`.

This historical #49 archive predates the #51 merge and is only a real-format parser smoke test. It does not establish #52 acceptance on the current parent HEAD and is not a substitute for a current `ASPIRE-MATCH-003` run.

## Blocked and unverified

- No Docker command was run. The #49 runtime report omits its Docker Desktop/Engine version. The current #51 report records Desktop 4.96.0 / Engine 29.9.1, but that alone does not prove equality with #43 at the time of its runtime run. The earlier equality assertion could not be traced to an artifact in the inspected repository/history.
- Live #52 acceptance, current-run environment/network/source identity, and full stdout/stderr evidence remain unverified. Sender identity must come from separate current-run capture evidence; it is not inferred from the recorder archive.
- Normal and independent review have not been performed. PR #119 remains Draft. No merge was attempted.
- The initial intake-head PR check `42375826023` passed the standard `test` job and skipped `ASPIRE-NET-002` and full-stack acceptance; it does not validate implementation commit `40550b2`.

## Next action

Resolve the approved Docker-version comparison for the #43 runtime. Then, if the condition is satisfied, run `ASPIRE-MATCH-003` on the current child branch and preserve current-run environment and source-capture evidence. If the condition remains unverified, keep the live acceptance and Issue #52 incomplete.
