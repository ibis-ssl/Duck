# Issue #49 — implementation and verification

## State

Pre-implementation context record for ASPIRE-006F6. No product or test code has been changed, and no tests have been run. The dedicated branch is `task/issue49-aspire-006f6`, created from integration HEAD `fdef8f22249e6bdaab1f5dc998a1045f42d8dcb1` after fetching `origin`.

## Accepted scope

Issue #49, `[ASPIRE-006F6] match-controllerで試合の開始・継続・終了を管理する`, is OPEN. Dependencies #46, #47, and #48 are complete. The formal issue acceptance condition is focused coverage for Game Controller API/referee/vision readiness, STOP/HALT continuation, POST_GAME/max-time termination, and result persistence.

`Tracker/Design/tasks-status.md` row 206 further specifies the detailed task contract to preserve:

- Verify Game Controller API, referee packets, SSL-Vision input, and participant-resource readiness.
- On initial HALT, send API `RESUME_FROM_HALT`; observe a newer packet with STOP; then send `NEXT_COMMAND`; observe a newer PREPARE_KICKOFF_BLUE/YELLOW packet before NORMAL_START; prove start only from a newer referee packet, not a successful send, a fixed delay, or FORCE_START acceptance.
- Distinguish POST_GAME from maximum-time termination. The time limit stops execution but is not success. Missing fresh API/referee/vision/participant evidence at final result must produce failure while retaining partial evidence.
- Keep readiness during valid command transitions after initial STOP; after restart, verify initial STOP again.

Approved design: `Tracker/Design/Testing/aspire-simulation-test-environment.md`, especially sections around lines 42–46 and 296–298. Real two-team behavior remains separate ASPIRE-MATCH-003 / #52 runtime acceptance; this issue's focused tests must not claim real match communication.

## Current implementation and test surfaces (read-only discovery)

- Match AppHost composition is exercised by `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs` (`MatchModePinsSimulatorGameControllerAndFixtureConfiguration`, `MatchModeRegistersMatchResourcesWithoutCm4OrComparisonTrackers`, and match fixture checks).
- `Tracker/Tracker.Tests/AspireFullStackAcceptanceTests.cs` contains existing referee/readiness helpers and runtime acceptance paths. Existing tests are not evidence of #49 acceptance until the relevant scenarios are checked against the complete requirements above.
- Candidate implementation surfaces include `Testing/Duck.Testing.AppHost/GameControllerReadinessMonitor.cs`, match configuration/fixture paths, and existing referee/vision readiness components. Exact product changes remain unselected pending post-link focused gap analysis.

## Validation and workflow

The Issue requires test-first focused failure then the minimal implementation. Before any test or product edit, create the Draft PR and read back the formal closing reference to #49 and Project 16 Linked PR field. Then add or adjust only coverage needed for the accepted scope, run focused tests, preserve diagnostics and exact commands, and record skipped/unverified runtime acceptance explicitly.

## Non-goals and write boundary

- No implementation of the separate ASPIRE-MATCH-* real-network acceptance work; no live Game Controller, container, or match execution unless the Issue's accepted environment and authorization explicitly permit it.
- No unrelated refactor, duplicated full-stack suite, or review-only optional enhancement.
- Parent #43 and upper #18 remain unmerged. Only this child PR may be merged into `design/issue18-aspire-test-orchestration` after exact-head CI and both reviews pass.
- Preserve existing shared untracked `reports/evidence` and other reports.
