# 通常レビュー報告

## 審査対象

- Repository: ibis-ssl/Duck
- Issue / child PR: #51 / #117 (draft)
- Parent issue / aggregate PR: #50 / #118 (draft)
- Branch: `task/issue51-aspire-006g1`
- Base: `task/issue50-aspire-006g`
- Review range: `6f919a66003757dc7c780c5d9d0d7cd63c75d232...06e00602fb42448c0d29c981f4a5131bfe2a8c7f`
- Review mode: initial normal review

## 審査根拠

- #51 requires Linux MATCH-001/002 evidence, resource list, team mapping and uniqueness of the actual 11003 source tuple. It does not require POST_GAME or full-match success.
- Reviewed PR diff includes the existing #117 implementation as well as this task's bind-access fix, raw UDP capture, verifier updates, tests and reports. The reviewer must inspect the complete range, direct dependencies, prior PR discussion and task design.
- Exact changed implementation hashes and paths are in `reports/issue51-linux-bind-permissions-source-capture-implementation-20261107.md`.

## Validation evidence

- Independent native Python focused suite: 10 passed, exit 0.
- Independent native Linux .NET suite: 113 passed, 2 Windows-only tests skipped, 0 failed, exit 0.
- Test execution occurred on exact source/test hashes at implementation commit `7d6feb8` before a parent-report-only merge commit. All source/test hashes remained unchanged through the merge; see Green report.
- CI run `42357169977` failed on older HEAD `b2fd92ec7dae7e913c22714920001066e72c789a`; it is stale and not evidence for this review target. No current-head Linux Docker acceptance run exists yet.
- `run-42356804056-artifacts/` is pre-existing and is not part of this change.

## Dispatch profile

- selection inputs: review; judgment-heavy technical; medium uncertainty; cross-system/cross-module change radius; high criticality; single repetition; observed independent workstreams; decomposition forbidden by review lifecycle; fresh.
- selection source: automatic with explicit task-context profile.
- observed decomposability: independent_workstreams.
- decomposition policy / disposition: forbidden / prohibited_by_review_lifecycle; one exhaustive reviewer.
- proposed profile: none.
- approval status / evidence: not required for Sol high.
- requested profile: gpt-6.1-sol / high (specified task profile).
- reviewer role / default-role plan: default role; no explicit role override.
- role config evidence / profile effect: collaboration dispatch exposes model/reasoning only; exact final profile may be unobservable.
- planned runtime profile: gpt-6.1-sol / high.
- applied profile: null until observable.
- application status: pending dispatch.
- runtime profile observability: pending.
- reviewer continuity: this is the initial normal reviewer; reuse this reviewer for any fix verification.
- fork policy: none.
- reasons / constraints: review the immutable target, do not edit code, do not merge, distinguish normal-path blockers from held items and capability gaps.

## Findings

### I51-NR-001 - P1: Allow the completed controller after G1 evidence is frozen

- Latest disposition: closed for the four-file dirty-source snapshot recorded below; original P1 severity preserved.

- Origin: initial normal review of the complete PR #117 implementation range.
- Location: `.github/workflows/dotnet-test.yml:804` (the new match resource array), with the unconditional running-state check at lines 832-834.
- Description: after `verify_aspire_match_acceptance.py` successfully validates frozen G1 evidence, the workflow includes `match-controller` among resources that must still be running. The verifier deliberately permits that owned receiver to have exited; `MatchControllerWorker.ExecuteAsync` throws after persisting any failed full-match result, including `maximum_time`, so its default hosted-service behavior stops the process. A valid G1 run can consequently pass the verifier and then fail this later CI check solely because POST_GAME was not reached.
- Impact: the added Linux acceptance route imposes a full-match lifetime condition on #51, contradicting its accepted MATCH-001/002 scope and blocking the ordinary maximum-time evidence path. The seven participant resources must remain running; the completed controller is a separate evidence producer.
- Evidence: `Testing/Duck.Testing.RefereeDriver.Host/MatchControllerWorker.cs:283` and lines 354-369; `scripts/verify_aspire_match_acceptance.py:180`; `scripts/tests/test_match_referee_source_capture.py:134` explicitly covers completed receiver exit. The workflow string assertions do not exercise this composed post-verifier branch.
- Required actions: keep ownership/presence checks and diagnostic collection for the controller, but accept its completed state only after the current stack/run/container/epoch capture and result have passed G1 verification. Continue requiring all seven participants running. Add a workflow-level composition fixture for verifier-success plus exited-controller/maximum-time, and negative sibling cases for a stopped participant or invalid controller evidence. Retain focused results and exact-source identity.

### I51-NR-002 - P1: Supply the Docker publish contexts in the new match acceptance job

- Latest disposition: code/path fix resolved for the four-file dirty-source snapshot below; only the original final runtime acceptance action remains under E51-001. Original P1 severity preserved.

- Origin: initial normal review; an inherited build prerequisite is newly exercised by PR #117's added match dispatch/PR route. This is not attributed to the later bind-access/source-capture changes.
- Location: `.github/workflows/dotnet-test.yml:686` (new match route), dependent on the build-only preparation at lines 635-659.
- Description: `aspire-full-stack` starts from a fresh checkout and only runs `dotnet build` for AppHost and tests. It never publishes or stages the `bin/Release/net10.0/publish` directories consumed by `AddPublishedContainer` for Duck and the match controller. The prerequisite `test` job is another runner; its publisher uses a temporary directory deleted on EXIT, and its uploaded runtime bundle contains RuntimeHost plus the CM4 helper, without RefereeDriver.Host. The full-stack job downloads no bundle.
- Impact: the newly added #51 CI route cannot build its two local Docker images from the required contexts on a clean hosted runner. Model-only permission tests and the retained focused Green do not establish those contexts.
- Evidence: `Testing/Duck.Testing.AppHost/Program.cs:23` resolves fixed publish directories and uses them directly as Docker build contexts; its calls are at lines 525 and 639. AppHost/test project files contain no build-time publish target. `scripts/tests/verify-linux-publish-outputs.sh:5` creates a temporary publish root and its final bundle construction exports RuntimeHost/helper only. No download/publish/staging step exists anywhere in `aspire-full-stack`.
- Required actions: prepare exact-source portable Release publish outputs for RuntimeHost and RefereeDriver.Host in this job before AppHost launch, or explicitly download, verify and stage a matching bundle containing both. Assert required entry DLL/deps/runtimeconfig/assets at the paths consumed by AppHost. Cover clean-run preparation and missing/stale input rejection with a composition fixture, then retain matching native Linux runtime evidence. No dependency installation or image-user change is implied.

### Acceptance evidence still required (E51-001)

- The current-head Linux Docker MATCH-001/002 acceptance is `unexplored`, not passing. No matching run was returned for `06e00602fb42448c0d29c981f4a5131bfe2a8c7f`. The supplied older run `42357169977` failed at readiness/finalization and belongs to `b2fd92ec7dae7e913c22714920001066e72c789a`; it cannot close this gate.
- Required owner/action: parent arranges an authorized native Linux AppHost/Docker execution on the final fixed source, retains seven-participant/resource inventory, excluded-resource check, team/initial-STOP evidence, current receiver source tuple/count/window/epoch evidence, environment/network/Docker version, stdout/stderr, failure diagnostics and finalized archive evidence produced by this workflow. Attach exact source identity and matching CI when used. #51 and PR #117 remain incomplete/draft until that evidence exists.
- This is an unfulfilled acceptance gate, separately identified from the two code/path findings. A POST_GAME result or MATCH-003/004/005 pass is not required for G1.

## Coverage / verdict

### Reviewed identity and independence

- Review mode: initial normal review. Reviewer: `/root/issue51_normal_review`; this reviewer did not implement, repair, run tests, build, launch Docker, or publish this change.
- Reviewed implementation HEAD: `06e00602fb42448c0d29c981f4a5131bfe2a8c7f`; branch `task/issue51-aspire-006g1`.
- Base: `task/issue50-aspire-006g` at `6f919a66003757dc7c780c5d9d0d7cd63c75d232`; observed merge-base equals that full SHA. Complete reviewed range: `6f919a66003757dc7c780c5d9d0d7cd63c75d232...06e00602fb42448c0d29c981f4a5131bfe2a8c7f`.
- Execution environment: Windows PowerShell via approved `exec_command`, read-only repository inspection at `C:\Users\donabe\RemoteDesktopWorkspace\Duck-issue51-aspire-006g1`. Worktree ownership: read-only reviewer, with one reserved report write. Initial and final inspection showed only this pre-created untracked report and pre-existing `run-42356804056-artifacts/`; no implementation diff.
- Governing repository skills read: `.agents/skills/work-context-manager/SKILL.md` and `.agents/skills/review-worker/SKILL.md`. No root AGENTS.md was found; prior implementation discovery likewise records none.
- Exact applied model/reasoning profile is unobservable. Requested profile remains the parent-owned Dispatch profile; it was not edited. No nested delegation occurred.
- Initial independent reviewed HEAD / closure reviewed HEAD: not applicable; this is the normal reviewer. Reuse this same reviewer for fix verification.
- Reserved report path: `reports/issue51-normal-review-20261107.md`. `report_attestation_allowed: false`; this initial normal review does not authorize an independent-final-review attestation or transfer its verdict to later implementation heads.

### Scope and authoritative context

- Read Issue #51, PR #117's existing discussion and historical partial Windows report, task-status G1 row, match design/history, all 25 changed-file diffs and their affected dependency paths.
- The GitHub connector's PR snapshot still describes the older published `b2fd92e...` head/base; it was used only as historical discussion/publication context. The explicitly assigned local range above controls this review; no remote snapshot was silently substituted.
- Accepted scope: Linux MATCH-001/002, participant inventory/exclusions, blue TIGERs/yellow ibis mapping, initial STOP and actual observed 11003 source-tuple uniqueness. The parent explicitly accepts bounded observed tuples without process/container attribution.
- Non-goals: POST_GAME/full-match success; MATCH-003/004/005; networking redesign; image-user or environment changes; unrelated cleanup; PR/issue mutation; merge.

### Required criterion dispositions

| Required criterion | Disposition | Evidence / result |
|---|---|---|
| Requirement and design conformance | checked_finding | I51-NR-001 adds a controller-lifetime condition beyond G1; E51-001 remains unexecuted. Capture/report semantics otherwise distinguish G1 from fullMatchPassed. |
| Correctness and edge cases | checked_finding | I51-NR-001 and I51-NR-002. Reviewed malformed UDP, same-IP/different-port tuples, stale identity/epoch/boot, empty/incomplete/truncated capture, count reconciliation, freeze/abort, Linux path/group modes. |
| Scope discipline and unrelated changes | checked_no_finding | Diff is match acceptance/workflow/evidence work. Shared owner-label and gzip-sanitizer changes have relevant acceptance consumers; no unrelated cleanup found. |
| Complete changed files and direct dependency impact | checked_finding | All changed files covered below; I51-NR-002 concerns actual Docker context dependencies. |
| API, data and compatibility effects | checked_no_finding | Capture is opt-in/internal; result fields additive; no-capture callers retain existing behavior. Linux-only GID/mode preparation preserves image users. |
| Configuration and workflow behavior | checked_finding | Both required findings are in the new match acceptance route and its preparation/composition. |
| Error handling and failure diagnostics | checked_no_finding | Incomplete artifacts and failures are retained; source capture writes atomically and fails closed; archive failure prevents a clean job result. Runtime shutdown/recorder finalization remain E51-001. |
| Security and secret handling | checked_no_finding | Targeted modes 0660/0640/2770, supplemental GID, link rejection, bounded tuple memory, owner/stack/run selection and existing text sanitization inspected. Gzip bytes intentionally preserved. No credential exposure found in this diff. |
| Tests and validation adequacy | checked_finding | Existing focused Green is supported, but composed CI exited-controller and clean publish-context scenarios are missing; required with I51-NR-001/002. |
| Current-head CI evidence | unexplored | No matching commit workflow runs returned; older b2fd run is stale. E51-001 blocks acceptance. |
| Report, tracking and documentation accuracy | checked_no_finding | Historical reports retain their source/environment/phase limits; Windows partial evidence does not claim Linux completion. Parent metadata may describe subsequent commit association; Green itself is tied to dirty-source hashes. |
| Regression and maintainability | checked_finding | I51-NR-001/002 plus held limits below; new capture work reuses the existing socket and identity handshake. |
| Real Linux MATCH-001 resources/readiness | unexplored | Model/test inspection complete; no current-head Docker observation. E51-001. |
| Real Linux MATCH-002 team/STOP/source tuples | unexplored | Synthetic verifier and bounded UDP evidence reviewed; no current-head actual match capture. E51-001. |
| POST_GAME, full match, MATCH-003/004/005 | not_applicable | Outside #51 G1 acceptance; absence is not a required finding. |
| Publishing-process/container attribution | not_applicable | Accepted observation is source-tuple uniqueness within the recorded window, with attribution explicitly unverified. |
| Windows-only file-sharing execution | held | Two explicit Windows-only cases were skipped in native Linux Green; no Windows execution was requested here. Parent owns any later platform-specific validation. |

### Changed-file coverage

| Changed files / group | Inspection and dependencies |
|---|---|
| `.github/workflows/dotnet-test.yml` | Entire diff and complete full-stack job; prerequisite test/publish job, triggers, mode environment, polling, diagnostics, cleanup, sanitization, finalization/upload order. Findings I51-NR-001/002. |
| `Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs`, `LinuxMatchBindAccess.cs`, `Program.cs`, `MatchParticipantStatusProjection.cs` | All changed paths plus fixed Docker contexts, state/config copies, mount/runtime args, team fixtures/settings/Crane probe and overlay, participant identity/health/recorder projection, owner-label consumers and DockerContainerSpec. |
| `Testing/Duck.Testing.RefereeDriver.Host/MatchControllerWorker.cs`, `RefereeDriverApplication.cs` | Changed result/capture composition and surrounding worker lifecycle, readiness probes, host/identity composition, Dockerfile/csproj, driver STOP/start path and API matcher. |
| `Testing/Duck.Testing.RefereeDriver/RefereeReceiveService.cs`, `RefereeSourceCapture.cs` | Complete receiver/capture implementations, UDP adapter/factory, diagnostics/epoch and command/identity interactions; malformed input, epoch replacement, pending receive, freeze and abort contracts. |
| `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs`, `MatchControllerWorkerTests.cs` | Complete diffs and affected match composition/result assertions; owner-label and STOP serialization contracts. |
| `Tracker/Tracker.Tests/MatchLinuxBindPermissionsTests.cs`, `RefereeSenderTupleCaptureTests.cs`, `RefereeSourceCaptureLifecycleTests.cs` | Complete new fixtures; model-only mode/membership/preservation checks, real ephemeral UDP endpoints, raw-before-decode and identity/epoch lifecycle. |
| `scripts/verify_aspire_match_acceptance.py` | Entire file: evidence preservation, current-owned inventory, team/publisher/STOP/recorder checks, capture validation, report generation, CLI polling and gzip finalization. |
| `scripts/sanitize_aspire_artifacts.py`, `scripts/tests/test_capture_aspire_dcp_logs.py`, `test_match_referee_source_capture.py`, `test_verify_aspire_match_acceptance.py`, `test_wait_aspire_stack_readiness.py` | All diffs/new tests; sanitizer dependency and related workflow assertions. String-presence checks do not cover the two composed failures. |
| Five changed `reports/issue51-*.md` reports | Historical Windows partial acceptance; test-first authoring/Red, implementation, independent Green; retained source identity, native execution and unexecuted acceptance limits checked. |

### Validation assessment and closure readiness

- No test, build, Docker, workflow dispatch, issue/PR/comment, commit, push or merge command was executed by this reviewer.
- The 13 hashes in retained `issue51-green-verification-evidence/source-manifest.sha256` were compared read-only against the reviewed worktree: all 13 matched. Result summary and Green report agree on Python 10 passed/exit 0 and .NET 115 total, 113 passed, 2 explicit Windows skips, 0 failed/exit 0. These are reused exact-content focused results, not a new current-HEAD full-stack execution.
- `verification_capability: local_execution_available` is supported by retained approved WSL Python/.NET results, separately from the current read-only PowerShell review tool. Required live Docker/image-user/write behavior is not established by that capability. No dependency/SDK install or Docker configuration change was attempted.
- Commit state: reviewed implementation committed. Push/publication: not performed by reviewer; connector exposes an older published PR snapshot. CI wait: not performed; no matching result. Full local equivalence gate: not started in supplied evidence.
- Closure is not ready. No severity reclassification or prior finding closure is recorded.

| Finding / required action | Production path | Actual composition fixture | Focused/exact-source evidence | Readiness |
|---|---|---|---|---|
| I51-NR-001: allow verified completed controller while retaining seven running participants and ownership checks | Workflow branch currently unconditional | Exited-receiver Python unit case exists; composed workflow case absent | Current Green covers helper only | incomplete |
| I51-NR-001: reject stopped participants and invalid/mismatched controller evidence in the same path | Existing helper checks partly present | Required sibling workflow fixtures absent | New focused evidence absent | incomplete |
| I51-NR-002: prepare and validate exact-source publish inputs before AppHost launch | Job-local publish/staging absent | Clean-job/missing/stale publish fixture absent | Native runtime proof absent | incomplete |
| E51-001: run native Linux G1 acceptance and retain current identity/evidence | Present route requires fixes above | Actual final-head Linux Docker run absent | Matching run/artifacts absent | incomplete |

### Initial review verdict

- **fail** for reviewed implementation HEAD `06e00602fb42448c0d29c981f4a5131bfe2a8c7f`: two required P1 acceptance-path findings.
- Acceptance coverage is additionally incomplete because E51-001 remains unexplored. Focused Green does not establish Issue #51 completion.
- Parent next action: fix the two workflow composition/preparation defects, retain meaningful focused evidence and source identity, commit the fixes, and reuse this same normal reviewer for fix verification with a completeness matrix. Arrange the separately authorized final-head Linux Docker acceptance. No merge or publication permission is granted by this review.


### Normal-review fix verification - latest source snapshot

This section supersedes the initial review's open-code-finding assessment for the source snapshot below. The initial findings, P1 severities, evidence and fail verdict above remain historical records. There is no severity reclassification.

#### Identity and review boundary

- Same reviewer: `/root/issue51_normal_review`. Mode: normal-review fix verification and per-action closure-readiness check. No new exhaustive review or nested delegation.
- Git baseline HEAD remains `06e00602fb42448c0d29c981f4a5131bfe2a8c7f` on `task/issue51-aspire-006g1`. These fixes are dirty/uncommitted; the baseline SHA alone does not identify the fixes.
- Reviewed source fingerprint: SHA-256 `0c0a6e12c461ed9687e7c7af8f9c37ca8ae81c5548914882b84a9e3ad19b6168`. It hashes a UTF-8 manifest of the four rows below, ordered by path, each encoded as `<sha256>  <path>\n`. Coverage includes both tracked modifications and both untracked production helpers, not the report or unrelated historical artifacts.
- Parent authorized review of this exact dirty worktree. Any later content change requires affected-delta review; a later commit must be checked to contain these exact reviewed bytes before associating the source conclusions with that commit.
- Rechecked the complete four-file fix scope, actual CI sequencing, helper implementations, fixtures and sibling rejection cases. No implementation, test, build, Docker, dependency-install or publication operation was performed by this reviewer.

| Reviewed source input | SHA-256 |
|---|---|
| `.github/workflows/dotnet-test.yml` | `4d9f3f557e8dd750060b99aa101bf3b0113006fed16327bf63dbcb0c7ab584af` |
| `scripts/tests/test_match_referee_source_capture.py` | `ceae7d72424f0a7098a12938eaba407a23c7ba965a3123016fc9d8297549d22d` |
| `scripts/verify_aspire_match_publish_contexts.py` | `a9cd4199b5e62e934f4fcee6b80943863861437875b719236fba28f62ae0877c` |
| `scripts/verify_aspire_match_resource_state.py` | `96794eeecd8a4f51701d638beb534582b3681b954c249dcb89a9ef86a7d8a22c` |

#### Finding dispositions and completeness matrix

- **I51-NR-001 / P1: closed for the reviewed source snapshot.** The current-run acceptance verifier still runs first and failure exits the readiness step. Only a match-mode `match-controller` in `exited` state gets a passed Linux G1 report passed to the production state gate. Other stopped resources and other terminal statuses are rejected. Ownership/presence checks and diagnostic collection remain. No POST_GAME requirement has been added.
- **I51-NR-002 / P1: implementation resolved; final runtime-evidence action pending.** The CI match route now publishes both current-checkout projects into the exact AppHost Docker contexts before start and invokes the production context gate. DLL, deps, runtimeconfig, RuntimeHost appsettings and both Dockerfile inputs are required and nonempty. The original action to retain matching native Linux runtime evidence is still open as E51-001; it is not silently marked satisfied or removed from the finding.
- Both identified code defects are addressed; no additional required source change was found in this fix delta. Formal closure of both findings together is not yet complete because the runtime-evidence cell below remains absent. NR-002's remaining work is evidence collection on the final source, not another inferred code defect.

| Finding / original required action | Production path | Actual fixture / composition evidence | Validation and disposition |
|---|---|---|---|
| I51-NR-001: permit the completed controller only after current G1 verification, keep ownership/presence and diagnostics | Workflow acceptance verifier precedes loop; `verify_aspire_match_resource_state.py:7` is invoked at workflow line 857 for nonrunning resources | `MatchTerminalStateGateTests.test_accepts_only_running_resources_or_verified_exited_match_controller`; existing real acceptance-helper fixture covers frozen capture plus completed controller; workflow-order assertion binds helper invocation to CI | 18 focused / 109 full Python passed; complete for snapshot |
| I51-NR-001: continue rejecting stopped participants, wrong modes/statuses and incomplete evidence | Same exact CI-invoked state gate; report is required and checked for passed/native-Linux/G1-complete flags | Helper fixtures exercise exited simulator, base-mode exited controller, dead controller, partial/non-Linux report, incomplete G1 report and no report; acceptance-helper fixtures retain stale/mismatched source/epoch/boot rejection | Complete; I51-NR-001 closes for snapshot |
| I51-NR-002: create exact-source portable Release publish inputs before AppHost starts | Workflow lines 661-678 publish RuntimeHost and RefereeDriver.Host from current checkout, then invoke `verify_aspire_match_publish_contexts.py`; step condition matches the match-mode dispatch/PR route | `test_match_ci_publishes_and_checks_both_docker_contexts_before_start` binds both publish commands and production checker to the pre-start step; `MatchPublishContextGateTests.test_accepts_complete_duck_and_match_controller_docker_contexts` exercises the exact checker against both paths | Implementation/fixture complete; actual dotnet publish and container execution await E51-001 |
| I51-NR-002: assert DLL/deps/runtimeconfig/assets and Dockerfiles; reject absent or wrong-context inputs | `REQUIRED_OUTPUTS` and `verify_contexts` check the fixed Release/net10.0/publish paths; Dockerfiles are checked at project roots | Helper fixtures cover missing RuntimeHost deps/appsettings/Dockerfile, RefereeDriver DLL/deps/Dockerfile, empty RefereeDriver runtimeconfig and misplaced Debug DLL | Complete for checker contract. Current-checkout publish in a fresh CI job is the source-provenance route; no reusable external bundle is accepted |
| I51-NR-002: retain matching native Linux runtime evidence | Corrected workflow is present but not executed on final committed fixes | No final-source Docker composition run/artifact yet | Pending under E51-001; NR-002 formal evidence closure remains incomplete |
| E51-001: final-source Linux MATCH-001/002, current-run identity, environment, resource/team/source and finalization evidence; matching CI when used | Existing Linux match workflow plus the reviewed fixes | No matching committed-fix CI/live acceptance yet | Unexplored; required before #51 completion |

The first, string-only fix attempt was not accepted as closure evidence. The later exact production helpers and positive/negative fixtures satisfy the corresponding source/fixture cells above. The native acceptance cell is still explicitly absent, so no complete two-finding closure is asserted.

#### Retained validation evidence

- Parent supplied the native WSL execution commands below and exit 0 for each. Reviewer read the retained logs and confirmed their test names, counts and final `OK`; reviewer did not rerun them.
- Evidence directory: `C:\Users\donabe\Documents\Codex\2026-11-07\task\issue51-normal-review-fix-verification-20261107-01`.
- Focused command: `python3 -B -m unittest discover -s scripts/tests -p 'test_*match*.py' -v`: **18 passed**, log `focused-match.log`, SHA-256 `5b116d1866de08a47f340edbb896234e565b6698c8b1a070012a7fb47b85f329`.
- Full Python command: `python3 -B -m unittest discover -s scripts/tests -p 'test_*.py' -v`: **109 passed**, log `full-python.log`, SHA-256 `ad39e4945a895ba7677677d9573e4b19b6135f169e943f02b5a279dd9fad5602`.
- These two retained files contain unittest output. Wrapper exit 0 and WSL location are supplied parent execution evidence; separate command/exit/environment manifests were not retained in this new directory. This limitation is stated rather than inventing missing files.
- Read-only `git diff --check` returned exit 0; only existing LF/CRLF normalization warnings appeared. No .NET source changed in this follow-up; prior native .NET Green remains relevant to its unchanged C# inputs.
- No dirty-source CI result exists by definition. The earlier baseline/older published runs are not used as validation of these four dirty fix files. No live Docker acceptance or independent-final-review verdict is established.

#### Updated criterion dispositions

| Required criterion | Latest disposition | Result |
|---|---|---|
| Requirement/design conformance | checked_no_finding | Code now permits bounded G1 success with a completed controller; full-match outcome remains separate |
| Correctness/edge cases | checked_no_finding | Reviewed actual state/context gates and negative sibling fixtures; no new code defect found |
| Scope discipline | checked_no_finding | Four fix inputs address the two existing findings |
| Changed-file/direct dependency impact | checked_no_finding | Entire fix diff and both new helpers reviewed against AppHost fixed paths and image entrypoints |
| API/data/configuration/compatibility effects | checked_no_finding | CI-only helper CLIs; match-only publishing; base/comparison stopped resources continue to fail |
| Workflow/error handling/diagnostics | checked_no_finding | Native context preparation and gates fail before ordinary acceptance proceeds; terminal diagnostics remain |
| Security/secret handling | checked_no_finding | No new credentials, Docker privilege, destructive operation or configuration change |
| Test/source evidence adequacy | checked_no_finding | Exact production helpers exercised; actual workflow binding/order assertions and 18/109 logs inspected |
| Current final-source CI | unexplored | Fixes are not committed/published; fresh PR CI still pending |
| Real Linux MATCH-001/002 | unexplored | E51-001 still absent; no substitute from synthetic fixtures or prior Windows run |
| Report/tracking accuracy | checked_no_finding | Dirty fingerprint, supplied exits, retained logs and remaining gates explicitly separated |
| Regression/maintainability | checked_no_finding | Small CI helpers share tested production predicates; full scripts/tests output is Green |
| POST_GAME/full match/MATCH-003/004/005 | not_applicable | Not required for G1 |
| Process/container attribution | not_applicable | Bounded observed tuple uniqueness remains the accepted contract |
| Windows-specific sharing and broader .NET/full repository gates | held | Prior limits remain; no such execution was requested/performed in this follow-up |

#### Latest verdict and next action

- **incomplete** overall. I51-NR-001 closes for the exact dirty-source fingerprint; I51-NR-002's production correction is verified, while its already-required native-runtime-evidence action remains pending with E51-001. No further required code fix was identified.
- The historical fail verdict remains attached to the original committed implementation HEAD. The new source conclusions apply to baseline HEAD plus the four-file fingerprint; they do not claim that unchanged Git HEAD contains the fixes.
- Parent may commit the reviewed fixes and bind the resulting commit tree to the reviewed fingerprint, then obtain fresh PR CI and native Linux G1 acceptance on that final source. Reuse this reviewer for bounded remaining-finding/CI-evidence verification. Issue #51 completion, an independent final review and merge are not authorized or established here.
- `report_attestation_allowed: false`. This normal-review report is not an independent-final-review attestation.



### Committed-head diagnostic follow-up - 709d1b4

#### Identity, scope and continuity

- Review mode: continued normal review, limited to commit/source association, the diagnostic delta, affected test/wiring and current CI evidence. Same reviewer `/root/issue51_normal_review`; no new exhaustive review, implementation or delegation.
- Reviewed implementation HEAD: `709d1b4ad4ab11083062a96005b0f211284b5c1f`, branch `task/issue51-aspire-006g1`.
- Diagnostic delta: `decd6958242ad38321df99fea53e0dd56358641d..709d1b4ad4ab11083062a96005b0f211284b5c1f`; only `.github/workflows/dotnet-test.yml` and `scripts/tests/test_match_referee_source_capture.py` changed.
- Commit `decd6958242ad38321df99fea53e0dd56358641d` was checked against all four recorded source hashes from the preceding dirty-source review. Its Git blob bytes match all four rows exactly, binding reviewed fingerprint `0c0a6e12c461ed9687e7c7af8f9c37ca8ae81c5548914882b84a9e3ad19b6168` to the committed fixes. Both production gate helpers remain unchanged at 709d1b4.
- Before the report update, the tracked worktree was clean; only the pre-existing untracked `run-42356804056-artifacts/` remained. Read-only `git diff --exit-code 709d1b4 -- .github/workflows/dotnet-test.yml scripts` passed.
- Current changed source SHA-256: workflow `b9a0e37d5814af37009c76aba24f9c02367616c2440dbcbdf90e412a86260345`; match fixture `5662cda8bdeaea916bcbbf0c80110e7bb5e06cba4f247aac410c2aabaa92ea9c`.
- Reviewer did not run tests, builds or Docker, dispatch/rerun CI, or alter source/PR/issues/commits. Only reviewer-owned report sections are updated.

#### Diagnostic-delta findings and coverage

- **No new finding.** The change records runner identity, mode/UID/GID metadata for run-owned match paths, and the owned containers' image user, supplemental groups and mounts in startup-failure diagnostics. It does not alter permissions, users, groups, mount configuration or acceptance decisions.
- State-copy paths use the SHA-256 of the current stack ID; the reviewed CI start path sets diagnostics RunId to that same stack ID. Result/recorder paths are scoped to the same run. `TMPDIR` is already propagated from the start step through `GITHUB_ENV`.
- Diagnostic `stat` failures remain nonfatal and are retained; the surrounding readiness step already omits `set -e`. Container discovery retains owner/stack scoping, and the new inspect fields pass through the existing sanitizer before writing evidence. The final artifact tree still passes the existing sanitization/upload gate.
- The new fixture asserts that this exact failure-diagnostic block contains the host identity/path-mode and container user/group/mount fields. It is structural regression coverage; no actual collected bind-mode artifact is claimed while readiness is still running.

| Affected criterion | Disposition | Result |
|---|---|---|
| Diagnostic scope, correctness and compatibility | checked_no_finding | Read-only metadata added to the existing failure path; acceptance behavior unchanged |
| Changed files/direct dependencies | checked_no_finding | Entire 709d1b4 delta, CI mode/run/path variables, state-copy naming, sanitizer and cleanup/upload wiring inspected |
| Error handling and failure evidence | checked_no_finding | Missing files produce retained diagnostic errors rather than mask the original acceptance failure |
| Security and ownership | checked_no_finding | Run-scoped paths/owned containers; metadata only; existing sanitization remains |
| Tests/workflow regression | checked_no_finding | New fixture inspected; exact-commit Python/sanitizer workflow step and normal test job passed |
| Exact-HEAD Linux acceptance and archive | unexplored | Readiness is still executing; final evidence and archive are not established |
| Unchanged criteria from prior exhaustive review | held | Earlier dispositions carried forward; source association verified, no unrelated criteria reopened |

#### Exact-HEAD CI observation

Observed at **2026-11-07 11:30:27 UTC**, using the GitHub connector's workflow-run query for exact commit `709d1b4ad4ab11083062a96005b0f211284b5c1f` and the job list for [run 42367919270](https://github.com/ibis-ssl/Duck/actions/runs/42367919270):

- Run status: `in_progress`, no conclusion.
- `test` job `128389808273`: completed/success, including dotnet tests, desktop acceptance policy and Linux application publish-output verification.
- `ASPIRE full-stack Linux acceptance` job `128390466184`: in progress. Scoped Python/sanitizer tests, AppHost/harness build, **Publish Linux match Docker contexts**, and AppHost start completed successfully.
- `Wait for all full-stack resources to report ready`: in progress.
- Cleanup, evidence upload and archive-finalization gate: pending. No final MATCH-001/002 result or verified recorder archive was available to inspect.
- `ASPIRE-NET-002 Linux packet flow`: skipped; not substituted for match acceptance.

This is fresh exact-commit CI progress, not CI completion or a Linux acceptance pass. The successful match publish-context step supplies runtime route evidence for the NR-002 preparation fix; it does not establish all seven participants, source uniqueness, or recorder finalization.

#### Latest committed-head dispositions and verdict

| Item | Current disposition at 709d1b4 |
|---|---|
| I51-NR-001 / original P1 | Closed source fix carried forward to the committed HEAD; diagnostic delta does not change the gate |
| I51-NR-002 / original P1 | Code/path fix resolved and committed; exact-HEAD CI publish-context step succeeded. Its final native acceptance evidence remains tracked only by E51-001 |
| E51-001 | Pending exact-HEAD Linux MATCH-001/002 and final archive/evidence verification |
| New source findings in 709d1b4 | None |
| Overall verdict | **incomplete** |

- This section supersedes the preceding dirty-source identity as the current reviewed implementation identity; prior observations and severity history remain preserved.
- Parent next action: wait for run 42367919270 to finish, inspect the exact-head resource/team/STOP/source-capture and archive artifacts, and return any failure evidence to the same reviewer. No successful gate or Issue #51 completion may be inferred from readiness being in progress. POST_GAME/full-match success remains outside G1.
- `report_attestation_allowed: false`; this is a normal-review continuation, not an independent final-review attestation.


## Risks

### User-confirmation-required capability gaps

- None was demonstrated by this review. Approved repository reads/writing of the reserved report are available; no denial, pending approval, or uncertain action remains. Linux Docker acceptance was prohibited for this reviewer, rather than attempted and denied. A later need to install dependencies, enable/reconfigure Docker, or weaken the native Linux criterion would require the parent to resolve authorization explicitly; no such change is assumed here.

### Initial-review held concerns and limits

- H51-001 (owner: parent runtime acceptance): native Linux image-user access, state-store append, recorder output readability/setgid inheritance, DCP shutdown and final gzip archive integrity still need E51-001. WSL Green used a Windows-backed v9fs filesystem and no containers.
- H51-002 (owner: parent; non-blocking scoped persistence concern): engine configuration is intentionally prepared as group-read-only input. The locally checked-out Game Controller dependency source attempts persistence after in-memory AutoContinue updates (`internal/app/engine/engine.go:517`) and logs persistence failures. That checkout is not independently proven identical to image 3.20.3, and no failure of the scoped G1 behavior is established. Preserve/inspect runtime diagnostics; broader engine-config persistence is not silently added to G1.
- H51-003 (owner: parent/report consumer): tuple uniqueness covers raw datagrams admitted to the retained current receiver epoch/window. UDP loss, kernel drops, per-packet destination metadata, traffic outside the window and publishing-process identity remain unmeasured; these documented limits do not imply a full-match result.
- H51-004 (owner: parent subsequent validation): broader repository checks, affected AppHost composition tests outside the focused filter, sanitizer/workflow tests outside the focused Python pattern and Windows-only sharing behavior have no new exact-target execution here. Existing build/analyzer/dependency warnings remain in historical Green evidence.
- H51-005 (owner: parent reporting): historical reports include pre-existing garbled text and phase-specific pending statements. This reviewer preserved their bytes and did not reinterpret those historical statements as final-head success.


### Fix-verification risk update

- Full Python workflow/sanitizer helper coverage is now supported by the retained 109-test run. The unchanged C# / Windows-specific and broader repository limits above remain. E51-001 is the only remaining final acceptance gate; no additional source defect was found in this follow-up.

## Next action

- Review the exact committed range. If findings require changes, parent will fix, run applicable local verification, commit, and reuse this normal reviewer for fix verification. Parent PR #118 remains draft and will not merge to main.
