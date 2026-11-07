# Issue #48 normal review

> Latest normal-review result: **pass** for the match-only Crane target-address fix at `7038f2d6cf8d1e4507ca3a001631837277154abe`. `I48-NR-001` and independent finding `I48-IFR-001` are closed by the changes described in the final delta section. Exact-head CI `41640384043` is pending; packet-flow is skipped. Earlier review/fix sections below remain the historical record.

## Task and scope

- Issue: ibis-ssl/Duck #48, ASPIRE-006F5, Crane and Duck match settings.
- PR: #110, branch `task/issue48-aspire-006f5`, base `design/issue18-aspire-test-orchestration`.
- Reviewed implementation candidate: `d2a9d28b961adebb9d752662dc25f8fd122118ef`; diff range `8f8927ae84d24ed1fe0b72b17cf1615b60912bb9..d2a9d28b961adebb9d752662dc25f8fd122118ef`.
- Accepted criteria: match Crane args include `team:=ibis`; match mode omits `cm4-sim`; Duck does not publish UDP 11010; the AppHost model pins required `WaitForStart` and readiness ordering without treating a started notification as service readiness.
- Non-goals: #49 `match-controller`; actual simulator/Docker/UDP runtime acceptance; changes to `base`/`comparison` defaults; #43/#18 merge.

## Dispatch profile (parent-owned)

- schema_version: 4
- selection_source: explicit user override
- task_kind: normal review; bounded AppHost configuration/test diff
- observed decomposability: independent workstreams
- decomposition_policy: forbidden; decomposition_disposition: prohibited_by_review_lifecycle
- requested: `gpt-6.1-sol` / `high`; `fork_turns=none`; `parallelism_mode=single_agent`
- role_plan: default reviewer role; no role override supplied
- planned_runtime_profile: `gpt-6.1-sol` / `high`
- applied: null until observable
- application_status: pending runtime result
- profile_observability: pending

## Review instructions

Read `.agents/skills/review-worker/SKILL.md` and review the complete diff plus direct dependencies against Issue #48 and the approved Aspire simulation design. Do not implement fixes, run unrelated broad tests, stage or commit code, push, approve, or merge. Return concrete required findings separately from held/unverified items and non-blocking observations. Fill the reviewer-owned sections below and preserve parent-owned profile fields.

## Reviewer identity and execution

- Reviewer identity: `/root/issue48_orchestrator/issue48_normal_reviewer`; normal initial review. This reviewer did not implement the candidate or any fix; no subagents were started.
- Runtime profile evidence: requested `gpt-6.1-sol/high` is dispatch intent only. The exact applied model/reasoning profile is not observable in this reviewer runtime and remains unverified. Parent-owned profile fields above are preserved.
- Execution: Windows PowerShell, `C:\Users\donabe\RemoteDesktopWorkspace\Duck-task-split`, repository `ibis-ssl/Duck`, branch `task/issue48-aspire-006f5`. Tracked source was unchanged at review entry and exit; unrelated untracked evidence/reports were left untouched.
- `reviewed_implementation_head`: `d2a9d28b961adebb9d752662dc25f8fd122118ef`.
- Base: `8f8927ae84d24ed1fe0b72b17cf1615b60912bb9`; complete range `8f8927ae84d24ed1fe0b72b17cf1615b60912bb9..d2a9d28b961adebb9d752662dc25f8fd122118ef`.
- Verification capability: `local_execution_available`, based on the supplied local focused validation and retained local test logs. The reviewer did not execute tests or independently qualify the installed test executor.
- Commit state: candidate committed. Push state: pushed, confirmed by PR #110 remote head matching the candidate. CI state: pending at review; no CI rerun or workflow dispatch requested.
- Verdict: **fail**. One required high-severity finding, `I48-NR-001`.
- Next action: implement the finding, add the actual composed match configuration assertion, retain focused red/green evidence, correct the implementation record, and return to this same normal reviewer with a new committed HEAD and the completeness matrix.

## Sources and coverage

The full four-file diff was inspected, including every changed hunk:

- `Testing/Duck.Testing.AppHost/CraneFixtureSettings.cs`: default parameter, configuration precedence, name validation, unchanged yellow-team restriction.
- `Testing/Duck.Testing.AppHost/Program.cs`: mode validation, resolved team defaults, standard match composition, Duck environment, optional bridge settings, comparison defaults, helper container publication.
- `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs`: changed match composition test; exact-count/reference/type wait helpers; argument/environment callback helpers; sibling comparison/default, custom-team, manifest, shared readiness and composition tests.
- `reports/issue-48-implementation-record.md`: completion claims and retained validation evidence.

Direct dependencies and authorities inspected:

- GitHub Issue #48 and PR #110 via GitHub connector; PR head/base match the supplied immutable range.
- `Tracker/Design/tasks-status.md`, row `ASPIRE-006F5`, plus adjacent dependency rows; `phases-status.md` context.
- `Tracker/Design/Testing/aspire-simulation-test-environment.md`, match-mode scope and readiness/start ordering; retained historical ASPIRE-006F5 contract.
- `BaseSimulationResourceRegistration.cs`: CM4 exclusion, manifest propagation, launch arguments, Duck simulator startup wait, Crane Duck startup/Game Controller healthy waits, standard readiness registration.
- `DcpBaseResourceReadiness.cs`: service-specific health checks and health annotation registration; `CraneFixtureManifest.cs` and `CraneFixtureSettingsTests.cs`.
- `Tracker.RuntimeHost/Program.cs`, `RuntimeHostCommandLine.cs`, `RuntimeHostServiceCollectionExtensions.cs`, `RuntimeTrackerOptions.cs`, `RuntimeTrackerConfigurationResolver.cs`, `appsettings.json`, `Dockerfile`, and `Tracker.Core/Runtime/UdpTrackerPacketPublisher.cs`: actual outgoing UDP configuration path.
- `.github/workflows/dotnet-test.yml`; retained Issue #48 red, green, and combined focused output logs.
- Repository skills: `review-worker`, `work-context-manager`, relevant `development-orchestrator` and review-wrapper policy, and `report-writer`.

| Criterion | Disposition | Evidence / outcome |
| --- | --- | --- |
| Match Crane defaults to team:=ibis | checked_no_finding | Mode-specific fallback reaches validated settings, manifest and launch arguments; comparison/base retain crane fallback. |
| Match has no cm4-sim or Crane dependency on it | checked_no_finding | IncludeCm4Simulator is false in match; exact Crane wait-count assertion allows only Duck and Game Controller. |
| Duck does not publish UDP 11010 | checked_finding | I48-NR-001: outbound publisher remains enabled; endpoint absence cannot prove send suppression. |
| Required startup/readiness model graph | checked_no_finding | Exact waits pin Duck to simulator Started, Crane to Duck Started and Game Controller Healthy. Game Controller has a service health check. Started is not claimed as runtime readiness. |
| Correctness and edge cases | checked_finding | I48-NR-001; optional fallback preserves explicit configuration precedence and existing validation. |
| Scope and direct dependency impact | checked_no_finding | Four-file bounded diff; base/comparison team default stays crane. No unrelated executable changes. |
| API/configuration/compatibility | checked_no_finding | Optional parameter preserves existing source callers; accepted match-only default change. |
| Error handling and diagnostics | checked_no_finding | Existing invalid-team/color rejection preserved; no changed failure/cleanup path. |
| Security and secrets | checked_no_finding | Both fallback values pass the existing safe-name validation; no added external input or secret handling. |
| Tests and validation adequacy | checked_finding | I48-NR-001: current model test checks host UDP exposure rather than the effective outgoing publisher setting. |
| Current-HEAD CI | held | Run 41637729431 is in_progress at exact reviewed SHA; parent owns final CI read. |
| Reports, tracking and documentation | checked_finding | Same finding: implementation record implies disabled Duck UDP publication without effective config evidence; tracking remains open. |
| Regression/maintainability | checked_no_finding | Existing comparison default and custom settings remain intact by inspection; no additional required issue. |
| Actual Docker/simulator/UDP runtime acceptance | not_applicable | Explicitly outside Issue #48 review scope; no runtime acceptance claim made. |

## Required finding I48-NR-001

- Identity: `I48-NR-001`.
- Severity: **high** (required); origin: normal initial source review against Issue #48 / ASPIRE-006F5.
- Primary changed location: `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs:1149` (new absence-of-UDP-endpoint assertion).
- Production location: `Testing/Duck.Testing.AppHost/Program.cs:519` (composed Duck registration), with runtime evidence below.
- Description: the new assertion proves that Docker does not expose a UDP endpoint whose target port is 11010. It does not disable the application sending UDP to that destination. The match Duck registration sets the sim profile and Testing__Mode=match but never sets Tracker__PublishUdp=false. RuntimeHost does not use Testing:Mode to disable publishing.
- Evidence: `Tracker/Tracker.RuntimeHost/appsettings.json:9` sets PublishUdp=true, and the selected sim profile uses multicast 224.5.23.2 port 11010 at line 59. `RuntimeHostServiceCollectionExtensions.cs:39` binds the Tracker options; `RuntimeTrackerConfigurationResolver.cs:41` copies PublishUdp and the profile destination into the publisher options. `Tracker/Tracker.Core/Runtime/UdpTrackerPacketPublisher.cs:45` sends the packet whenever that flag is true. A host-published UDP endpoint is unnecessary for this outbound send.
- Impact: a normal match Duck still emits tracker packets to 11010, violating the accepted no-send requirement and introducing the Duck tracking source into the match stream. The current focused test passes while this required behavior is absent.
- Required action: disable the effective Duck UDP publisher for match mode only (for example, set Tracker__PublishUdp=false on that composed resource), assert that effective setting in the actual match AppHost model fixture, and protect base/comparison publication behavior. Retain or supplement the no-host-endpoint assertion if useful. Update the implementation record to describe the verified behavior accurately.
- Required closure evidence: a focused regression that fails on the reviewed candidate because publication remains enabled, passes after the fix, and demonstrates base/comparison behavior is preserved; production path, actual composition fixture, and exact source/HEAD identity must be recorded.
- Severity continuity: source severity high, unchanged; no reclassification or erratum.

### Finding completeness matrix for a future fix-verification request

| Finding | Required action | Production path | Actual composition fixture | Focused evidence | Disposition |
| --- | --- | --- | --- | --- | --- |
| I48-NR-001 | Match-only publisher suppression | Program.cs composed Duck registration; effective Tracker publisher options | MatchModeRegistersMatchResourcesWithoutCm4OrComparisonTrackers (or equivalent actual AppHost model fixture) | Red/green effective-setting assertion, matched to new source/HEAD | incomplete; fix not implemented |
| I48-NR-001 | Preserve base/comparison publishing behavior | Same mode-specific configuration path | Base and comparison AppHost compositions | Focused mode regression evidence | incomplete |
| I48-NR-001 | Correct completion/evidence claims | reports/issue-48-implementation-record.md | Not applicable to prose | Updated report consistent with focused evidence | incomplete |

## Validation and CI assessment

The reviewer read the retained logs without rerunning tests:

- `reports/evidence/issue48-red-20261103/output.log`: changed match test failed with expected team:=ibis substring absent, at line 1148; 1 failed.
- `reports/evidence/issue48-green-20261103/output.log`: 1 passed, 119 ms.
- `reports/evidence/issue48-focused-20261103/output.log`: 2 passed, 125 ms, matching the supplied combined match and comparison-default validation description.
- These logs substantiate the reported outcomes. They do not contain a self-contained SHA/command manifest, so exact candidate attribution relies on the supplied execution record and source agreement. They also do not exercise the missing publisher-setting requirement.
- GitHub connector read of `https://github.com/ibis-ssl/Duck/actions/runs/41637729431`: pull_request run, attempt 1, head_sha `d2a9d28b961adebb9d752662dc25f8fd122118ef`, status in_progress, conclusion null. Final CI is pending; no success or merge readiness inferred.
- An initial terminal gh read could not reach GitHub due socket policy; the installed GitHub connector successfully supplied Issue, PR, and run evidence. No dependent remote-read blocker remains.

## Held items, risks and handoff

- Held CI item `I48-NR-H1`: parent must read the exact-head run outcome before accepting final CI evidence. Pending CI does not change the source finding or authorize acceptance.
- Exact runtime model/reasoning profile remains unverified.
- Actual service/UDP runtime acceptance is outside scope; model waits are startup configuration evidence only.
- No required source area remains unexplored for this bounded initial review. No additional findings, fixes, implementation changes, commits, pushes, PR comments, approvals or merges were performed.
- Normal-review continuity: use this reviewer for I48-NR-001 fix verification; completeness matrix required before closure.
- Independent final review has not occurred. `initial_independent_reviewed_head: null`; closure reviewed heads: none.
- Persistence mode: repository_file; reserved path: `reports/issue-48-normal-review-20261103.md`.
- `report_attestation_allowed: false` for this normal review. No attestation head exists. The caller must use its independent-final lifecycle and validate all reserved-path/first-parent/only-report-path conditions before accepting any future administrative attestation.


## Normal fix verification — 2026-11-03

This section supersedes the initial-review technical verdict for the new HEAD only; the initial finding and its **high** severity above remain historical evidence.

- Review mode: bounded normal fix verification of `I48-NR-001`, with the same reviewer `/root/issue48_orchestrator/issue48_normal_reviewer`.
- Initial reviewed HEAD: `d2a9d28b961adebb9d752662dc25f8fd122118ef`.
- Current `reviewed_implementation_head`: `acb4b358fe6d65985a431d2a8c3159e483a44905`.
- Base remains `8f8927ae84d24ed1fe0b72b17cf1615b60912bb9`; complete implementation range is that base through acb4. Bounded fix range inspected in full: `d2a9d28b961adebb9d752662dc25f8fd122118ef..acb4b358fe6d65985a431d2a8c3159e483a44905`.
- Current verdict: **pass_with_held**. `I48-NR-001` is **closed**; source severity remains **high**, with no reclassification. No additional required finding arose in the changed area.
- Scope: the three-file fix delta, effective publisher configuration path, composed match/comparison fixtures, base preservation by source/default inspection, corrected implementation record, focused validation logs and exact-head CI.
- Execution: same Windows PowerShell worktree and branch. Git HEAD matched acb4 at entry and immediately before report persistence; tracked source had no working-tree delta. The reviewer made no implementation edit, test execution, Git mutation, push, CI rerun, PR comment, approval or merge.
- Profile observability: exact applied runtime model/reasoning remains unverified; parent-owned dispatch fields are unchanged.

### Completeness matrix and finding closure

The parent supplied the fix/evidence mapping before verification. The matrix is complete under the accepted preservation scope: parent explicitly confirmed source/default plus comparison composition evidence is sufficient for base/comparison preservation; a new base-only publication test is not required for this match-only task. This clarification changes no finding identity or severity.

| Finding / required action | Production path | Actual composition fixture / checked contract | Focused evidence | Disposition |
| --- | --- | --- | --- | --- |
| I48-NR-001: suppress effective Duck publisher in match only | Program.cs:531-533 sets Tracker__PublishUdp=false only inside testingMode == match. RuntimeHost binds Tracker options; resolver copies PublishUdp; UDP publisher returns before send when false. | MatchModeRegistersMatchResourcesWithoutCm4OrComparisonTrackers creates the actual match AppHost and asserts duckEnvironment[Tracker__PublishUdp] equals false at line 1150. The no-UDP-endpoint assertion is retained as separate exposure coverage. | Valid Red log fails at the new effective-key assertion; Green match + comparison pass 2/2, 134 ms. | complete; checked_no_finding |
| I48-NR-001: preserve comparison publication/default team | Same common Duck registration; match-only guard is false for comparison, leaving the key unset and RuntimeHost PublishUdp=true default intact. | CraneLaunchUsesCraneTeamNameAndSeparateYellowColor creates comparison AppHost, asserts no Tracker__PublishUdp override at line 1285 and retains team:=crane / team_color:=YELLOW. | Included in supplied Green 2/2 focused run. | complete; checked_no_finding |
| I48-NR-001: preserve base publication/default team | The same guard is false for base; unchanged RuntimeHost appsettings PublishUdp=true remains the fallback. Mode-specific team fallback remains crane outside match. | Existing shared AppHost production path and base composition were inspected; preservation is established by source/default proof plus comparison fixture coverage of the common non-match path. No newly run base publication test is claimed. | Source inspection at acb4 and unchanged defaults; focused comparison composition evidence. Explicit parent scope clarification accepts this proof. | complete; checked_no_finding |
| I48-NR-001: correct implementation record | reports/issue-48-implementation-record.md now states effective match suppression and distinguishes endpoint exposure from outgoing publication. | Report accurately describes the new model setting assertion, valid Red/Green, prior exact-head CI and matching-CI requirement for new commit. | Three-file fix diff and retained evidence logs match the report. | complete; checked_no_finding |
| I48-NR-001: retain source-bound focused regression evidence | Production and test fix are committed at acb4; supplied parent execution record attributes final focused run to the candidate content. | Actual match and comparison model fixtures; no synthetic substitute. | Red reviewfix2: missing Tracker__PublishUdp key, 1 failed; Green reviewfix: 2 passed, 134 ms. Raw logs have no self-contained SHA/command manifest; source identity attribution relies on supplied execution record, as in initial review. | complete; checked_no_finding |

The new environment setting reaches the same RuntimeTrackerOptions configuration binding, RuntimeTrackerConfigurationResolver publisher options and UdpTrackerPacketPublisher gate identified in the initial finding. It disables outgoing UDP publication in match mode. It does not rely on Docker endpoint exposure to stop sending.

### Coverage delta dispositions

| Area | Current disposition | Result |
| --- | --- | --- |
| Accepted no-send requirement and regression coverage | checked_no_finding | I48-NR-001 closed by actual composed effective setting assertion and production guard. |
| Match-only scope, sibling modes and compatibility | checked_no_finding | Base/comparison publication defaults and crane team fallback preserved by the evidence above. |
| Reports and validation accuracy | checked_no_finding | Record corrected; unsuccessful compile attempt is not counted as a behavioral Red. |
| New error/security/diagnostic impact | checked_no_finding | Constant boolean config value; no added external input or failure/cleanup path. |
| Current-HEAD CI | checked_no_finding | Run 41638797927 completed successfully at exact HEAD acb4; the packet-flow and full-stack acceptance jobs were skipped. |
| Prior team, CM4 and startup/readiness findings coverage | checked_no_finding | No change to those reviewed contracts in this bounded fix. No exhaustive rereview was performed. |
| Actual container/UDP runtime acceptance | not_applicable | Remains outside accepted Issue #48 scope. |

### Validation and current-head CI

- `reports/evidence/issue48-red-reviewfix-20261103/output.log`: compiler CS1061 on accessing Keys from an unawaited environment Task; test did not execute. Retained as a failed attempt, not a valid behavioral Red.
- `reports/evidence/issue48-red-reviewfix2-20261103/output.log`: valid match-model execution failed with KeyNotFoundException for Tracker__PublishUdp at test line 1150; 1 failed.
- `reports/evidence/issue48-green-reviewfix-20261103/output.log`: match and comparison focused fixtures passed 2/2, 134 ms, as supplied and corroborated by the log.
- Reviewer performed no test run or rerun.
- Parent subsequently read back run `41638797927`, pull_request attempt 1, `head_sha=acb4b358fe6d65985a431d2a8c3159e483a44905`, `status=completed`, `conclusion=success`. `.NET tests`, desktop acceptance policy and Linux publish steps succeeded. `ASPIRE-NET-002 Linux packet flow` and full-stack Linux acceptance were skipped. URL: https://github.com/ibis-ssl/Duck/actions/runs/41638797927.
- Prior run `41637729431` belongs to d2a9 and is stale for the changed candidate, regardless of its reported success. It is not used as current-head validation.
- Commit/push: candidate is committed and pushed; its exact-head CI succeeded. Runtime integration remains explicitly unverified.

### Remaining risks and next action

- No required finding remains for this bounded normal-review fix. No verdict-blocking source area remains unexplored within the accepted closure scope.
- Held CI item `I48-NR-H1` is resolved by the successful exact-head run; the two runtime acceptance jobs remain skipped/out of scope.
- Applied runtime profile remains unverified. Actual simulator/container/UDP acceptance is outside scope, and base preservation has source/default plus comparison model evidence rather than a newly executed base-specific publication test.
- Parent should persist this normal review record before independent-final review and continue the established lifecycle. This is not an independent-final verdict or merge authorization.
- `report_attestation_allowed: false`; normal review report only. Independent reviewed HEAD and report-attestation HEAD remain null.

## Follow-up review: independent finding I48-IFR-001

- Reviewer identity: `/root/issue48_orchestrator/issue48_normal_reviewer`, same normal reviewer as the earlier review. Requested profile `gpt-6.1-sol/high`; actual runtime profile unverified.
- Reviewed tree: source delta committed as `7038f2d6cf8d1e4507ca3a001631837277154abe`, based on `a069ef26ecc961ed28d7f417487a88844add97c8`.
- Finding addressed: independent reviewer found that default match mode still resolved `CraneTargetAddressSettings` to `cm4-sim` and invoked the Crane overlay despite the resource being excluded. The normal reviewer confirmed the launch behavior was operationally relevant and required no extra changes beyond the user-approved #48 scope.
- Change: `Program.cs` now sets target-address settings to `null` only in `Testing:Mode=match`. The match model test asserts absence of both target environment keys, the overlay mount, and the overlay launch argument. Base/comparison still call the original settings resolver.
- Red/Green: the new assertion failed before the fix because `DUCK_CRANE_TARGET_ADDRESS` was present. After the fix, the match model test passed 1/1 (115 ms); the existing comparison model test passed 1/1 (117 ms). The reviewer ran no tests.
- Follow-up verdict: **pass**, no additional required finding. `I48-IFR-001` resolved. This review covers only the delta and relies on the earlier full review for unchanged areas; it is not a fresh independent-final review.
- Exact-head CI run `41640384043` is pending at `7038f2d6cf8d1e4507ca3a001631837277154abe`; the ASPIRE-NET-002 packet-flow job is skipped. No run is treated as success until terminal readback.
- Overall runtime acceptance remains unverified: no simulator/container/UDP end-to-end test was run.
- Reviewer identity and execution: same normal reviewer; no source/report/Git mutations, test execution, approval, or merge by reviewer. No new security, error-handling, readiness, or sibling-mode findings.
