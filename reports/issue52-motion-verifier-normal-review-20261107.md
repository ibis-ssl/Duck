# Issue #52: normal review — motion verifier

## Review identity

- PR: #119
- Reviewed implementation HEAD: `a597c981967cd9347360699911f62b080ded4f7c`
- Base: `task/issue50-aspire-006g` at `1660cfb519f373c8263df09599a7e11f0a18e699`
- Requested reviewer profile: `gpt-6.1-sol/high`
- Applied profile and reviewer identity: `/root/issue52_normal_review`, spawned as `gpt-6.1-sol/high`; reviewer runtime could not independently introspect the applied model or effort.

## Scope

Review the bounded #52 offline SSL-log motion verifier, tests, implementation report, and phase-status update against Issue #52 / ASPIRE-MATCH-003. The review target is the committed PR head above; this untracked report template is not part of that target.

## Coverage

Initial review inspected requirements/design, parser correctness and malformed/incomplete input behavior, team/window association, error handling, tests, scope, report/tracking accuracy, and current-head CI. The verifier fixes below are in progress; closure verification remains pending.

## Findings

Initial verdict: **fail**.

- **I52-NR-001 (P2 / medium):** the prior `(counter, command)` keyed collection merged a delayed repeated command across an intervening STOP. Fix: store contiguous command intervals separately and create a new baseline after any different, inactive, or malformed referee command. Regression test: STOP followed by repeated prior command identity cannot combine movement.
- **I52-NR-002 (P2 / medium):** two detections for the same robot ID in one vision frame could count as movement. Fix: exclude duplicate IDs in that frame and compare unique observations across records/frames. Regression tests cover one-frame duplicate IDs and legitimate movement in later frames.
- **I52-NR-003 (P2 / medium):** a partial header/payload or invalid length discarded summaries decoded before the error. Fix: finalize summaries in `finally` while retaining `status="incomplete"` and the parse error. Regression tests cover partial header, partial payload, and invalid record length.

## Validation and evidence

- Focused Python tests: 5/5 passed on the reviewed source.
- Exact-head GitHub Actions run: `42377130591` passed the standard `test` job; `ASPIRE-NET-002` and `ASPIRE full-stack Linux acceptance` were skipped.
- Docker and live ASPIRE-MATCH-003 acceptance: not run; the #43 version condition remains unverified.
- The initial review confirmed that this CI run did not execute the motion tests: the workflow discovers them only in the skipped full-stack job.

## Verdict

Initial review failed with the three findings above. TDD regressions and fixes are present locally and pass 11 focused tests; the reviewer must verify fixes against a new immutable commit before this report can declare closure. Current-head CI and independent final review are still pending for that later commit.

## Held and unexplored items

The historical #49 archive smoke test is not current #52 acceptance. Source endpoint attribution is not present in SSL log v1 and requires separate evidence. Live acceptance remains outside this source-review scope.
