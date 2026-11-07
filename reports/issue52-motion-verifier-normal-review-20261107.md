# Issue #52: normal review and fix verification

## Initial review identity

- PR: #119
- Initial implementation HEAD: `a597c981967cd9347360699911f62b080ded4f7c`
- Base: `task/issue50-aspire-006g` at `1660cfb519f373c8263df09599a7e11f0a18e699`
- Requested reviewer profile: `gpt-6.1-sol/high`
- Reviewer: `/root/issue52_normal_review`, spawned as `gpt-6.1-sol/high`; runtime could not independently introspect applied model/effort.

## Initial findings

Initial verdict: **fail**.

- **I52-NR-001 (P2 / medium):** observations were merged across an intervening STOP by reusing a `(counter, command)` key. The first fix isolated contiguous intervals, but follow-up found a delayed lower-counter `NORMAL_START` could still reopen a window after a newer STOP. This remained open at `ced7baf7ec7141825e3999aeaec5d52d3c7493e7`.
- **I52-NR-002 (P2 / medium):** duplicate robot IDs in one vision frame could imply motion. Fix: exclude duplicate IDs in that frame and compare unique observations across records/frames.
- **I52-NR-003 (P2 / medium):** partial header/payload or invalid length discarded decoded summaries. Fix: finalize summaries in `finally` while preserving `status="incomplete"` and exact parse error.

The initial normal review inspected requirements/design, parser correctness and malformed/incomplete input, team/window association, error handling, tests, scope, reports/tracking, workflow coverage, and CI. It reproduced all three findings at the initial HEAD.

## Fix verification at ced7baf7ec7141825e3999aeaec5d52d3c7493e7

- I52-NR-002 closed: duplicate IDs are excluded within a frame; negative single-frame and positive later-frame fixtures pass.
- I52-NR-003 closed: decoded summaries and exact parse errors survive partial header, partial payload, and invalid-length failures while status remains `incomplete`.
- I52-NR-001 remained open: a schema-complete sequence with `NORMAL_START(7)`, `STOP(8)`, delayed `NORMAL_START(7)`, and two later vision frames still passed at this HEAD.
- Reviewer verified immutable committed blobs and reported 10/10 tests passed. CI run `42379644721` later completed successfully for `ced7baf7ec7141825e3999aeaec5d52d3c7493e7`; standard test passed, while NET-002 and full-stack were skipped.

## Current stale-counter fix under review

- `_counter_is_newer` applies uint32 serial-number arithmetic. Stale/regressed packets clear the active window and cannot reopen the most recent command. Equal counters can only continue the same active window; counter `0` after `0xffffffff` is treated as forward wraparound.
- Regression coverage includes the delayed stale-counter false-pass sequence and uint32 wraparound-forward behavior.
- Focused tests currently pass 12/12. `py_compile` and `git diff --check` pass. These local checks will be bound to the new immutable commit after it is created.
- Normal fix verification, exact-head CI, and independent final review remain pending.

## Validation and held items

- Initial exact-head run `42377130591` and follow-up run `42379644721` passed the standard `test` job; NET-002 and full-stack were skipped. The motion tests only run in the skipped full-stack job.
- Docker and live ASPIRE-MATCH-003 acceptance were not run because #43's Docker version remains unverified against a primary record.
- The historical #49 archive is parser smoke evidence only, not current #52 acceptance. SSL log v1 does not contain packet source endpoints; source identity needs separate current-run capture.
