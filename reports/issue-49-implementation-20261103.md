# Issue #49 implementation and verification record

## State

Work is on `task/issue49-aspire-006f6`, created from integration HEAD `fdef8f22249e6bdaab1f5dc998a1045f42d8dcb1` after fetching `origin`. Draft PR #111 is open against `design/issue18-aspire-test-orchestration`; the PR has a formal closing reference to #49 and Project 16 lists PR #111 under Linked pull requests. The corrected implementation is published at `bb86892dd8c510f8586af6d5b6cd66788ab8438f` on branch `task/issue49-aspire-006f6`.

## Accepted scope

Issue #49 acceptance requires focused checks for Game Controller API/referee/vision readiness, STOP/HALT continuation, POST_GAME versus maximum-time termination, and result persistence. `Tracker/Design/tasks-status.md` row 206 further requires RESUME_FROM_HALT from initial HALT, a newer STOP packet, NEXT_COMMAND, a newer PREPARE_KICKOFF_BLUE/YELLOW packet, NORMAL_START, and a still newer packet confirming NORMAL_START. It also requires maximum time to remain a failure and final results to fail when fresh API/referee/SSL-Vision/participant-resource evidence is incomplete.

## Changes

- `RefereeCommandSnapshot` now carries the referee Stage and packet timestamp. The UDP receive adapter, hosted receive service, and HTTP health source preserve those fields.
- The focused driver now performs HALT -> RESUME_FROM_HALT -> STOP -> NEXT_COMMAND -> PREPARE_KICKOFF_BLUE/YELLOW -> NORMAL_START -> newer NORMAL_START. It does not use FORCE_START to declare the match active.
- The WebSocket adapter serializes RESUME_FROM_HALT. The existing full-stack acceptance path now uses this same evidence-driven driver sequence.
- The test result writer records termination kind and last referee command/stage/counter. POST_GAME is successful only when fresh final evidence for all four required inputs is present; maximum-time expiry and missing final evidence are failures that retain partial evidence.
- Review corrections map Game Controller API `KICKOFF`/`STOP` to UDP `PREPARE_KICKOFF_BLUE/YELLOW`, preserve POST_GAME even when its command counter is unchanged, reject stale/regressing referee evidence, retain the initial HALT in partial results, and prevent final-stage success without termination evidence.

## Verification

Red: `DriveToActiveAsyncResumesFromHaltThenAdvancesFromStop` failed on the original code with `Game Controller did not reach an active referee command; observed PrepareKickoffBlue.` This showed that the previous driver sent NEXT_COMMAND and FORCE_START without observing the required STOP/PREPARE transitions.

Green: initial focused Windows validation passed 22 tests. Review Red checks reproduced missing API KICKOFF mapping and loss of a same-counter POST_GAME update. After the corrections, `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --verbosity minimal -p:RunAspireFullStackAcceptance=true --filter "FullyQualifiedName~RefereeDriverHostAutoDriveTests|FullyQualifiedName~GameControllerStateMappingTests|FullyQualifiedName~RefereeDriverNetworkAdapterTests|FullyQualifiedName~RefereeDriverFixtureTests|FullyQualifiedName~MatchTerminationAcceptanceTests|FullyQualifiedName~HttpRefereeSourceCarriesPostGameStageIntoTheTerminationSnapshot" -m:1 /nr:false` passed 66 tests, 0 failed, 0 skipped (1 second). `git diff --check` passed. Existing NU1902 MessagePack and ASPIRE010 warnings remain. The full-stack Docker acceptance was not run.

The initial-slice CI run [41646168452](https://github.com/ibis-ssl/Duck/actions/runs/41646168452) succeeded for `8e0bfc5`, before the review fixes. Exact-head CI [41648274253](https://github.com/ibis-ssl/Duck/actions/runs/41648274253) succeeded for corrected HEAD `bb86892`. The same normal reviewer re-reviewed the fixes and closed NR001–NR005 with no new findings (`pass_with_held` while exact-head CI was pending). An independent reviewer then passed the complete 13-file diff with no required findings.

The container-backed full-stack acceptance test was not run; this local result does not claim live Game Controller, Docker, or match communication. Requested model profiles are gpt-6-luna/high for implementation and gpt-6.1-sol/high for review; actual applied runtime profiles have not been independently verified.

## Remaining workflow

The Japanese six-section PR comment is posted and read back at https://github.com/ibis-ssl/Duck/pull/111#issuecomment-6605669046. A report-only follow-up commit updates this record after review; after its automatic CI completes, squash merge PR #111 only into `design/issue18-aspire-test-orchestration`. Do not merge #43 or #18.
