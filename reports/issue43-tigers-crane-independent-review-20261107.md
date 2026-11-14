# Issue #43: TIGERs versus Crane independent final review

## Review identity

- Mode: independent-final, one exhaustive pass of the accepted #43 aggregate scope.
- Reviewed implementation HEAD: `c4c3301f0e825d198acd2949f781b0f3978381da`.
- Initial independent reviewed HEAD: same; no closure review.
- Scoped range: `ee1eb7c4a024134ddf2fadf8f029528ee6d609f8..c4c3301f0e825d198acd2949f781b0f3978381da`.
- Scope: #43 aggregate ASPIRE-006F1–006F6 plus required composition dependencies in PR #28. This is not a PR-wide review or merge approval.
- Worktree: `C:\wpr28normal`, clean at freeze and unchanged by the reviewer.
- Independent reviewer: separate fresh agent; did not implement the work, perform normal review, or rely on normal review before inspecting production paths.
- Requested runtime profile: `gpt-6.1-sol/high`; exact applied profile unverified.
- `c4c3301` adds only the normal-review report relative to `c8daa93`.

## Findings

**NR43-001 · Medium · held/non-blocking**

Location: `Testing/Duck.Testing.RefereeDriver/RefereeDriverNetworkAdapters.cs:237–251`, `ReceiveTextMessageAsync`.

The helper accumulates WebSocket fragments in a `MemoryStream` through `EndOfMessage` without an accumulated-byte limit; it serves control/config operations. The separate `GameControllerStateClient` reader enforces its configured logical-message limit. Oversized or continuing control output could cause excess memory consumption. No occurrence or population-frequency evidence was available. No explicit applicable 006F1–006F6 criterion requires a size bound on this helper. The evidence neither establishes the user’s repair threshold nor establishes frequency below it. Preserve Medium; record and leave under the user’s policy. Reassess with a realistic reproduction, frequency evidence, or an applicable explicit requirement.

No other finding requiring a change to the frozen candidate was established. Verdict: `pass_with_held` for this bounded #43 scope.

## Coverage

- Requirements/design and tracking rows 200–206: checked, no finding. 006G and 007 acceptance remain separate tasks.
- 006F1 fixtures: initial STOP, FRIENDLY, Blue TIGERs Mannheim / Yellow ibis, match timing, protocol and engine configuration, and copied assets checked, no finding.
- 006F2 topology: match participants, controller, comparison-only exclusions, and absence of cm4-sim checked, no finding.
- 006F3 simulator/Game Controller: arguments, mounts/state copies, control endpoint, GC 11003 publication, readiness composition checked, no finding.
- 006F4 participants: TIGERs Blue-only control, referee, AutoRef, recorder arguments, pinned images, networking and shutdown checked, no finding.
- 006F5 Crane/Duck: ibis default, direct-simulator composition, no cm4 dependency, Duck publication behavior and WaitFor/WaitForStart ordering checked, no finding.
- 006F6 lifecycle: readiness, HALT/STOP/PREPARE/NORMAL_START, continuation, half progression, POST_GAME and maximum-time distinction checked, no finding.
- Correctness, edge cases, dependencies, API/configuration, error handling/diagnostics, security, source-level regression tests and maintainability: checked in the scoped production/composition paths, no additional required finding.
- PR-wide changes outside 006F and legacy evidence: unexplored, explicitly out of scope. PR #28 contains about 6,836 changed files; this report does not attest to all of them.

## Validation and evidence

- PR #28 CI at `c8daa93`: .NET tests SUCCESS; ASPIRE-NET-002 Linux packet-flow and ASPIRE full-stack Linux acceptance SKIPPED. Skips remain unrun, not passed.
- Exact `c4c3301` CI was not available or claimed by the reviewer. The normal-review commit is report-only.
- PR #116 committed reports describe a real match reaching POST_GAME, passed persisted result, complete final readiness, nine placements, recorder CRC/EOF and cleanup. These were report-read, not independently reproduced; raw runtime evidence was not inspected.
- The Windows full-project result remains 1296 passed, 1 failed, 2 not run. The isolated passing rerun does not erase the failed run. Cause and frequency remain unknown.
- No tests or Docker were run during this independent review.

## Held and unexplored items

Held: NR43-001 (frequency unknown); unresolved recorded Windows full-project failure; exact published completion-HEAD CI and final repository/protection checks.

Outside scope: 006G and 007A/B/C acceptance; unrelated comparison/base/native/tooling changes and historical evidence across PR #28; third-party image internals and behavior beyond inspected contracts and reported evidence.

## Administrative attestation

This report attests the independent review of implementation HEAD `c4c3301f0e825d198acd2949f781b0f3978381da`. The report commit is administrative attestation only and does not change or extend the reviewed implementation. Reserved by `review-enforcer` before freeze as metadata-only under reservation identity `duck-issue43-independent-review-c4c3301f0e825d198acd2949f781b0f3978381da-20261107`.