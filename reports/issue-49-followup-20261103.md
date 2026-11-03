# Issue #49 integration follow-up

## Scope

This follow-up addresses the fresh aggregate review findings I43-IFR-001 and I43-IFR-002 against the explicit Issue #49 acceptance and approved Aspire testing design. It remains within match-controller lifecycle ownership; it does not begin Issue #50 live packet acceptance.

## Changes in this slice

- `RefereeDriverFixture.DriveToActiveAsync` accepts a seeded STOP and continues directly to NEXT_COMMAND; an initial HALT still sends RESUME_FROM_HALT and waits for a newer STOP.
- Match-mode AppHost exposes TIGERs, AutoRef, and recorder resources, adds a `match-controller` Host container, and gates it on simulator, Game Controller, Duck/vision, Crane, and all three participant resources.
- The controller waits for matched API/referee and vision readiness, drives the match start, waits for POST_GAME or maximum time, captures final API/referee and vision evidence, then writes a JSON result. POST_GAME succeeds only with complete final evidence; maximum time, readiness failures, and incomplete final evidence persist as failures.
- No live or full-stack execution is planned or claimed.

## Verification

Red: `DriveToActiveAsyncContinuesDirectlyFromInitialStop` failed on the original code with `Expected initial referee command HALT, but observed Stop.` The initial match-mode composition test failed because `match-controller` was absent.

Green: the focused suite `MatchControllerWorkerTests|MatchModeComposesLifecycleControllerAfterRequiredMatchResources|RefereeDriverFixtureTests|MatchControllerConfigurationRegistersLifecycleWorkerInsteadOfStartOnlyWorker` passed 11/11, 0 failed, 0 skipped. It covers seeded STOP, retained HALT, resource dependencies/config, worker registration, POST_GAME result persistence, maximum-time failure persistence, and incomplete final-evidence failure persistence. Existing NU1902/ASPIRE010 warnings remain.

## Workflow

Draft PR #112 targets `design/issue18-aspire-test-orchestration` from base `3041d75cbfa42d8b477e3cf910dba562814a822b`. PR #112 has formal closing reference #49, and Project 16 reads Linked pull requests #111/#112 with Status In progress. Issue #49 was reopened after explicit user authorization.

The worktree contains no unrelated tracked edits. Existing untracked reports/evidence are preserved. Do not merge PR #28, #43, or #18; do not start #50.

## Remaining

Complete focused source review, commit/push, exact-head CI, same normal reviewer, fresh independent review, and the Japanese six-section PR comment/readback. Merge PR #112 only to `design/issue18-aspire-test-orchestration`; then read back #49 Closed/Project Done and PR #28 current head/status without merging it.