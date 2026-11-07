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

### Historical failed-run findings - 42367919270

#### I51-NR-003 - P1: Recorder-created private archives are unreadable by the host consumers

- Latest disposition: closed at technical HEAD 3239d266 by successful native run 42370027080; see the final closure/evidence section. Original P1 severity preserved.

- Origin: newly available Linux runtime evidence for the existing bind-access implementation; not a regression attributed to diagnostic commit 709d1b4.
- Location: `Testing/Duck.Testing.AppHost/LinuxMatchBindAccess.cs:20` and recorder mount setup at `Program.cs:613`-618, with affected host reads at `MatchParticipantStatusProjection.cs:455` and `scripts/verify_aspire_match_acceptance.py:313`.
- Description/evidence: `startup-diagnostics.txt:230`-236 records runner UID/GID 1001:1001, result/recorder directories 2770 owned 1001:1001, but the producer-created `2026-11-07_11-27-11.log.gz` is 0600 owned 1000:1001. `ssl-log-recorder.inspect.txt` records image user 1000 and supplemental group 1001. The host participant reader reports access denied to this exact file, and `match/match-archive-verification-failure.txt` reports Python Errno 13 on the same path.
- Impact: directory setgid establishes the shared owning group but does not grant group-read permission to a file created 0600. The real host reader cannot establish recorder service readiness and the host finalizer cannot read/hash/CRC-check/copy the archive. This is an observed failure, not merely an inferred UID mismatch.
- Required actions: provide a scoped production route that lets the actual host reader and finalizer read output produced by the pinned recorder while preserving the intended identity/permission boundary; demonstrate the route during recording and after stop. Add a composed native regression with different host/producer UIDs and a producer-created private file, then retain focused results and final-source Linux acceptance/archive evidence. Directory mode/group-membership assertions alone do not exercise this contract.
- Status at the failed-run review: open; original finding severity P1. Root-cause confidence: high for the missing host-read contract and the observed mode/ownership explanation; no claim that this explains the independent Game Controller failure.

#### I51-NR-004 - P1: Game Controller still fails to open the prepared Linux state store

- Latest disposition: closed at technical HEAD 3239d266 by successful native run 42370027080; see the final closure/evidence section. Original P1 severity preserved.

- Origin: newly available Linux runtime evidence, same implementation/run identity as NR-003.
- Location: `Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs:59` and `LinuxMatchBindAccess.ApplyTo` at line 179; observed failure in `game-controller.startup.log:13` and `game-controller.inspect.txt`.
- Description/evidence: the Game Controller logs `Could not open state store: open /tmp/state-store.json.stream: permission denied` at 11:27:11 and exits with code 2. Docker inspection records the exact run-owned RW bind, configured user 1000 and `GroupAdd=["1001"]`. At 11:41:48, host diagnostics record the matching source file as 0660 owned 1001:1001.
- Impact: required Game Controller readiness never establishes; the participant snapshot also has Crane and recorder not ready, and no match result or source-capture artifact is retained. The run cannot satisfy MATCH-001/002.
- Required actions: establish the actual failing access contract, then correct and verify it. Capture effective process UID/GID/supplementary groups and the in-container target metadata/access at the relevant startup boundary, together with exact image/run/mount identity; demonstrate successful state-store open/read/write and Game Controller readiness on the corrected final source. Preserve the present failing evidence and add an appropriate composed regression after the mechanism is identified.
- Root-cause limit: Docker user/group-add configuration is not a captured effective process credential set. Host `stat` was taken about 14 minutes after the failed open; no contemporaneous in-container stat, effective-group snapshot, ACL or denial attribution is retained. The artifact proves the open failure but does not prove that Docker ignored group-add, that UID inequality alone caused it, or which additional permission mechanism was responsible. Under ordinary group access, effective membership in GID 1001 plus the recorded 0660 mode would be sufficient; the missing runtime observation must be resolved before prescribing a fix.
- Status at the failed-run review: open; severity P1 for the observed required-service startup blocker. Exact low-level cause remains unexplored.


### Prior review findings and continuity

### I51-NR-001 - P1: Allow the completed controller after G1 evidence is frozen

- Latest disposition: closed at technical HEAD 3239d266; prior snapshot closure and final exact-source runtime evidence are recorded below. Original P1 severity preserved.

- Origin: initial normal review of the complete PR #117 implementation range.
- Location: `.github/workflows/dotnet-test.yml:804` (the new match resource array), with the unconditional running-state check at lines 832-834.
- Description: after `verify_aspire_match_acceptance.py` successfully validates frozen G1 evidence, the workflow includes `match-controller` among resources that must still be running. The verifier deliberately permits that owned receiver to have exited; `MatchControllerWorker.ExecuteAsync` throws after persisting any failed full-match result, including `maximum_time`, so its default hosted-service behavior stops the process. A valid G1 run can consequently pass the verifier and then fail this later CI check solely because POST_GAME was not reached.
- Impact: the added Linux acceptance route imposes a full-match lifetime condition on #51, contradicting its accepted MATCH-001/002 scope and blocking the ordinary maximum-time evidence path. The seven participant resources must remain running; the completed controller is a separate evidence producer.
- Evidence: `Testing/Duck.Testing.RefereeDriver.Host/MatchControllerWorker.cs:283` and lines 354-369; `scripts/verify_aspire_match_acceptance.py:180`; `scripts/tests/test_match_referee_source_capture.py:134` explicitly covers completed receiver exit. The workflow string assertions do not exercise this composed post-verifier branch.
- Required actions: keep ownership/presence checks and diagnostic collection for the controller, but accept its completed state only after the current stack/run/container/epoch capture and result have passed G1 verification. Continue requiring all seven participants running. Add a workflow-level composition fixture for verifier-success plus exited-controller/maximum-time, and negative sibling cases for a stopped participant or invalid controller evidence. Retain focused results and exact-source identity.

### I51-NR-002 - P1: Supply the Docker publish contexts in the new match acceptance job

- Latest disposition: closed at technical HEAD 3239d266; the code/path correction and final native runtime action are satisfied by run 42370027080. Original P1 severity preserved.

- Origin: initial normal review; an inherited build prerequisite is newly exercised by PR #117's added match dispatch/PR route. This is not attributed to the later bind-access/source-capture changes.
- Location: `.github/workflows/dotnet-test.yml:686` (new match route), dependent on the build-only preparation at lines 635-659.
- Description: `aspire-full-stack` starts from a fresh checkout and only runs `dotnet build` for AppHost and tests. It never publishes or stages the `bin/Release/net10.0/publish` directories consumed by `AddPublishedContainer` for Duck and the match controller. The prerequisite `test` job is another runner; its publisher uses a temporary directory deleted on EXIT, and its uploaded runtime bundle contains RuntimeHost plus the CM4 helper, without RefereeDriver.Host. The full-stack job downloads no bundle.
- Impact: the newly added #51 CI route cannot build its two local Docker images from the required contexts on a clean hosted runner. Model-only permission tests and the retained focused Green do not establish those contexts.
- Evidence: `Testing/Duck.Testing.AppHost/Program.cs:23` resolves fixed publish directories and uses them directly as Docker build contexts; its calls are at lines 525 and 639. AppHost/test project files contain no build-time publish target. `scripts/tests/verify-linux-publish-outputs.sh:5` creates a temporary publish root and its final bundle construction exports RuntimeHost/helper only. No download/publish/staging step exists anywhere in `aspire-full-stack`.
- Required actions: prepare exact-source portable Release publish outputs for RuntimeHost and RefereeDriver.Host in this job before AppHost launch, or explicitly download, verify and stage a matching bundle containing both. Assert required entry DLL/deps/runtimeconfig/assets at the paths consumed by AppHost. Cover clean-run preparation and missing/stale input rejection with a composition fixture, then retain matching native Linux runtime evidence. No dependency installation or image-user change is implied.

### Acceptance evidence continuity (E51-001)

- Latest disposition: satisfied at technical HEAD 3239d266 by run 42370027080; the earlier pending observations below are historical.

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



### Completed Linux-run evidence review - 42367919270

#### Identity and provenance

- Reviewed artifact root: `C:\Users\donabe\Documents\Codex\2026-11-07\task\issue51-ci-42367919270\aspire-full-stack-42367919270-1`. This is the user/parent-supplied completed-run artifact; it was inspected read-only.
- At the start of artifact review, local HEAD was `afea30143ad08219664929140ae81fc5f2b12e12`, a report-only successor of technical HEAD `709d1b4ad4ab11083062a96005b0f211284b5c1f`; those technical trees matched. Before report persistence, the parent advanced HEAD to implementation commit `4e5c90412a602f515e11c1e2f06cadeff0b0f404`. The write guard stopped before changing bytes. That new identity fix is outside this completed-run evidence review and is not given a technical verdict here.
- Run 42367919270 is returned by the workflow query for PR head 709d1b4. Its `environment-before.txt` correctly records actual checkout `473c278ca55529cbb17f5ba609cfd78d76cd51a9` at `refs/pull/117/merge`, not a literal checkout of 709d1b4.
- GitHub commit metadata identifies 473c278 as the test merge of 709d1b4 into base `6f919a66003757dc7c780c5d9d0d7cd63c75d232`. Connector comparison 709d1b4..473c278 reports one merge commit and **files=[]**, binding the actual execution tree to the reviewed technical source. Local checkout does not contain that temporary merge object; no fetch or source mutation was performed.
- Runtime: Linux, Ubuntu 24.04.5 LTS, kernel `7.0.0-1017-azure`, Docker Engine 28.0.4; actual run and stack `gha-42367919270-1`.
- Same normal reviewer; this is runtime-evidence follow-up, not a new exhaustive or independent-final-review pass. No tests/build/Docker/CI rerun, source edit, privilege change or publication was performed by this reviewer.

#### Completed CI and artifact observations

- Connector now reports run **completed/failure**. The normal `test` job succeeded. Full-stack scoped Python/sanitizer tests, AppHost/harness builds, match Docker-context publication and AppHost start succeeded.
- The readiness step failed after waiting for a Match Controller result. Startup diagnostics were captured at `2026-11-07T11:41:48Z`. Cleanup and artifact upload succeeded. The final archive gate failed, with the retained archive-read permission error.
- Seven participant rows were retained at `11:41:48.6209583+00:00`: simulator, Duck, TIGERs and AutoRef were ready; Game Controller, Crane and recorder were not ready. This is **four of seven ready**, not a complete MATCH-001 pass.
- Game Controller container `a313a9d699ce247c23fd408b0da1cdb094fd211e8db38b42a483417424d5773f` is owned by the expected stack/run, uses image tag `robocupssl/ssl-game-controller:3.20.3`, and exited code 2. The mount source/path and startup panic correspond to the copied state store. A content digest for this tagged GC image is not retained in this short inspect output.
- Recorder container `c7622b9baf5acfc9750473994bd81dff530130401bb46364f4fc1613c0399adf` is run-owned, running at diagnostic time and pinned to the declared recorder image digest. Its directory write succeeded: a roughly 6 MB archive exists in the host status metadata. The recorder's own last retained log counts are vision **54,210**, vision-tracker **138,521**, referee **0**.
- Those producer log counts are distinct from host-decoded evidence. The participant projection reports decoded counts zero because its reader was denied access, so those zeros do not prove absence of Vision/tracker traffic. No unique 11003 source observation can be inferred from the GC publisher log or recorder counters.
- `match/match-acceptance.json` explicitly remains `status=incomplete`, `issue51AcceptanceComplete=false`. Neither `match-result.json` nor `referee-source-capture.json` is present. No gzip archive is present in the downloaded artifact; finalizer read failure prevents establishing its CRC/EOF/hash/completeness.
- Recorder group ownership is observed to have inherited GID 1001; group read permission is absent. These facts support NR-003 directly. In contrast, GC effective credentials and target access at its failed-open boundary are not observed, so NR-004's precise mechanism remains unresolved. The two failures must not be collapsed into one proven UID/GID root cause.

#### Evidence hashes

Paths below are relative to the artifact root.

| Evidence file | SHA-256 |
|---|---|
| `startup-diagnostics.txt` | `4fb9f4731fc3d37f0cc80c0772f8bc8f15dafc625d059b056e45241875d457e2` |
| `game-controller.startup.log` | `dedc3bbfee71c87ec2fef8419f0ded23cdfe1847e318a008efe0b673a6905b2d` |
| `game-controller.inspect.txt` | `70938ab72c2319e451f11bdedf54b5652fc7c5cc27c2ef178834ee38c5733890` |
| `ssl-log-recorder.inspect.txt` | `f9b721d570908a1470954aeb9e7aee7727143420c21234924e40ca1b6fe7cb03` |
| `match/participant-status.json` | `1e6b06b746082df8e34c80b0fa249fa45ccc60ed67ff374accd62f2a205e06d4` |
| `match/match-acceptance.json` | `332f6ffec243d00ca8e39ecb61180baeeaee5722972138043e0985a70a78f4e2` |
| `match/match-archive-verification-failure.txt` | `181d3a6dbee1b35bb58ba059dfb5ae55a40ace173122e5d02e3bd6cb1034d989` |
| `environment-before.txt` | `fa95679d384b0d87f973bac7fb862f67d4e026ef89a007ab98ee95f0d529d2ac` |

#### Latest dispositions, readiness and verdict

| Criterion / item | Disposition | Evidence-based result |
|---|---|---|
| Linux bind/output correctness | checked_finding | NR-003 host archive access denied; NR-004 GC state-store open failed |
| Production composition / validation adequacy | checked_finding | Earlier mode/group model fixtures did not exercise the actual producer-private-file/host-reader contract or establish effective GC access |
| Failure diagnostics / source provenance | checked_no_finding | Failing run, actual PR merge checkout, tree equivalence and concrete access errors are retained |
| Current matching CI | checked_finding | Completed failure; successful preparation is not successful acceptance |
| MATCH-001 resource readiness | checked_finding | Only four of seven participant rows ready |
| MATCH-002 / unique observed source tuples | unexplored | No match result or receiver source capture artifact |
| Archive finalization evidence | checked_finding | Finalizer Errno 13; CRC/EOF/hash and complete archive remain unverified |
| Low-level GC denial cause | unexplored | Effective credentials and contemporaneous target-access evidence absent |
| NR-001 | checked_no_finding | Previous code closure preserved; this run does not demonstrate a regression in its corrected state gate |
| NR-002 code/path correction | checked_no_finding | Context-publish step succeeded; original final acceptance requirement still represented by E51-001 |
| E51-001 | checked_finding | Acceptance was attempted and failed; no longer described as merely awaiting the run |
| POST_GAME/full-match result | not_applicable | Still outside G1; its absence is not the reason for these findings |

- **Completed-run verdict: fail** on technical source 709d1b4, with new required P1 findings NR-003 and NR-004. These are continuity-bearing findings against the observed run; applicability/closure on the newly created 4e5c904 identity fix requires separate same-reviewer fix verification. The completed failure supersedes the earlier in-progress/incomplete CI observation; prior source-findings/history remain preserved.
- Closure readiness for NR-003: production route, cross-identity composition fixture and passing final-source host-reader/finalizer evidence are missing. Closure readiness for NR-004: exact access mechanism, correction, corresponding fixture and passing startup evidence are missing. Do not begin finding-limited closure until those cells are supplied.
- Parent next action: preserve these artifacts, identify the GC denied-open mechanism and repair both actual access contracts within the accepted scope; provide source/fixture/validation matrices to this same reviewer. Then obtain final-source native Linux MATCH-001/002 and archive evidence. No change to the native Linux criterion or automatic image-user/privilege override is justified by this report.
- `report_attestation_allowed: false`; no independent final-review or merge verdict is issued.



### Bounded identity-fix verification - committed 4e5c904

#### Immutable target and inspected delta

- Same normal reviewer; assigned technical target `4e5c90412a602f515e11c1e2f06cadeff0b0f404`. Reviewed `709d1b4..4e5c904`, including the intervening report-only commit and the complete Linux bind identity/structural-test delta. Immutable source was read using `git show`, not the concurrently edited working files.
- Committed source hashes: `Testing/Duck.Testing.AppHost/LinuxMatchBindAccess.cs` SHA-256 `58aab96013941ce727393b5e5cdec10e2f062b1e9b6a9ef04d0f355b466e5c32`; `scripts/tests/test_match_referee_source_capture.py` SHA-256 `c24d3dd290b97ffdce6f3b0f426b71b2d649591948b569b5b822205e55441c40`.
- During this pass, parent-owned root-UID-guard edits were committed as `3239d266df5b888f941189f9c8ff4521894ac333`. That successor is outside this assigned 4e5c904 review and is not given a verdict here. No source/fixture changes were made by the reviewer.
- The earlier artifact review's guarded report-write attempt stopped before writing on the first HEAD movement. The subsequent report write completed, then its postcheck detected the parent-owned dirty guard edits. The persisted artifact findings/evidence were verified and retained; no source operation was retried or performed.

#### Source assessment and finding readiness

- On Linux only, `ApplyTo` now emits `--user <host effective UID>:<host effective GID>` instead of supplemental `--group-add`. Numeric values use invariant formatting and the new `geteuid` P/Invoke is confined to the existing Linux branch. Existing wiring applies this to Game Controller, recorder and match controller; other resource/mode paths are unchanged.
- This is an intentional image-user override. Earlier historical statements about preserving image-configured users no longer describe the candidate fix. The parent explicitly requested review of this approach; no implicit authorization for unrelated identity/network/privilege changes is assumed.
- For NR-003, matching the recorder's UID to the host owner provides a plausible production route for a newly created 0600 archive to be host-readable. For NR-004, matching the GC UID to the copied file owner reduces reliance on the unproven supplemental-group behavior. These are source-level expectations, not observations of either image running successfully under the new identity.
- No additional required source defect was identified in the committed 4e5c904 delta. Image filesystem/runtime compatibility and successful cross-identity access require actual composition evidence.
- The added Python case checks source text and call-site wiring. It does not create a private recorder file under the pinned image, exercise the host reader/finalizer, capture effective GC credentials or demonstrate a successful GC state-store open. Existing AppHost composition tests accept primary/supplemental host-group membership but do not alone prove the new UID/producer-output contract.
- A new working composition may establish that the access contract is repaired without claiming a forensic explanation for why the older configured group-add route failed. The historical root-cause limit remains explicit.

| Existing finding / required cell | Current 4e5c904 evidence | Disposition |
|---|---|---|
| NR-003 production access route | Linux writer UID:GID aligned with the host owner | Candidate correction inspected |
| NR-003 actual producer-private-file -> host reader -> finalizer composition | New structural assertion only; no completed current-source runtime artifact | Missing; do not close |
| NR-004 actual configured/effective identity and successful state access/startup | Numeric UID:GID override inspected; old group's failed-open mechanism remains unproven | Candidate correction; runtime proof missing |
| NR-003/004 focused validation and final-source Linux acceptance | Supplied local results plus successful CI test job; Linux acceptance still running | Partial; closure incomplete |
| NR-001 / NR-002 earlier code corrections | Not changed by this identity delta | Prior source dispositions preserved |
| E51-001 | No completed successful current-source MATCH-001/002 + archive evidence | Pending |

Formal NR-003/004 closure is not started: the original action matrices still lack the actual native producer/consumer/startup and archive evidence. This is a bounded source/readiness assessment, not a passing runtime verdict.

#### Local validation provenance

The parent supplied these exact commands and results for the 4e5c904 source before adding the root guard. Outputs were displayed in the parent chat but **not retained as log files**, so they are supplied evidence, not independently inspected raw artifacts:

- `python -B -m unittest discover -s scripts/tests -p 'test_*match*.py' -v`: **Ran 20, OK**.
- `python -B -m unittest discover -s scripts/tests -p 'test_*.py' -v`: **Ran 111, OK (skipped=1)**. This is 111 total with one skipped; it is not reported as 111 passes plus a skip.
- `dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj -c Release --no-restore`: succeeded, 0 errors, 2 existing warnings (`ASPIRE010`, MessagePack `NU1902`).
- Located `issue51-bind-diagnostic-verification-20261107-01/full-python.log` says Ran 110, OK; it is older diagnostic-phase evidence and is not mislabelled as the new 111-test result.
- Reviewer ran no tests/builds or containers in this follow-up.

#### Fresh CI observation and status correction

At **2026-11-07 11:53:44 UTC**, connector queries and independent read-only `gh api` queries agree:

- [Run 42369796318](https://github.com/ibis-ssl/Duck/actions/runs/42369796318) is a pull-request run for head `4e5c90412a602f515e11c1e2f06cadeff0b0f404`, attempt 1, **in_progress**, no conclusion.
- Test job `128395365240` completed/success, including dotnet tests and Linux application publish-output verification.
- Linux acceptance job `128395897571` exists and is **in_progress**. The latest step detail showed AppHost/harness build in progress, with match-context publication/start/readiness/cleanup/archive steps still pending at that observation. The scoped Python/sanitizer step had succeeded.
- ASPIRE-NET-002 job `128395366345` was skipped.

The parent's interim statement that this run had completed success and had no Linux acceptance job conflicts with both direct API observations. This report records the observed live status rather than converting a successful test job into a successful workflow or acceptance result. No CI dispatch, cancellation or rerun was performed by this reviewer.

#### Latest target verdict

- **incomplete for fix verification at 4e5c904**. No new code finding in the bounded delta; NR-003/004 retain their original P1 identities and remain open until their composed/runtime evidence cells are satisfied. Historical fail remains attached to 709d1b4 and its failed run.
- E51-001 remains pending completed final-source Linux MATCH-001/002 and archive evidence. Neither a successful build, a source-string test, nor the green normal test job closes it.
- Parent next action: retain and inspect the current mode=match job's terminal result/artifacts, bind any later guard/fix commit to its own source/validation evidence, and return the finding/CI delta to this same reviewer. No merge or independent-final-review attestation is granted.



#### Superseded-run cancellation update

- Parent reported requesting cancellation of 4e5c904 run `42369796318` at 11:53 UTC to avoid duplicating the newer `3239d26` run `42370027080`.
- Subsequent direct read-only GitHub API observation confirms `status=completed`, `conclusion=cancelled`, updated `2026-11-07T11:54:47Z`. The normal test job remains success; Linux acceptance job `128395897571` is completed/cancelled.
- Treat this as a superseded duplicate run, not completed acceptance evidence and not proof that the UID-alignment runtime fix passed or failed. The earlier in-progress observation above remains historical and is superseded by this terminal cancellation.
- Latest pushed successor `3239d266df5b888f941189f9c8ff4521894ac333` and run `42370027080` are the parent's target for future review; their guard/runtime evidence has not been reviewed in this 4e5c904 pass. NR-003/004 and E51-001 remain pending.



### Non-root guard follow-up - 3239d266

- Reviewed immutable technical HEAD `3239d266df5b888f941189f9c8ff4521894ac333`, limited to `4e5c904..3239d266`: `LinuxMatchBindAccess.cs` and its Python structural assertion. Same normal reviewer; no source changes, tests/builds, containers or CI actions were performed by this reviewer.
- **No new finding in this delta.** Non-Linux execution returns before either libc identity call. Linux effective UID 0 throws an explicit configuration error before adding the container user override; nonzero UID retains the previously reviewed invariant numeric UID:GID override. The three match bind-consumer call sites are unchanged.
- The new assertions check the guard and error message in source. They do not execute an effective-UID-zero process; this is structural coverage, and no root-runtime behavior result is invented. The normal Linux branch is supported by current CI compilation/test success.
- Source SHA-256: `Testing/Duck.Testing.AppHost/LinuxMatchBindAccess.cs` = `e1563738939b29465f9158381e9ca3d0e6eda45320925f578de38356e9abbfba`; `scripts/tests/test_match_referee_source_capture.py` = `b9c3b9a1fe17033b878d7e3f0f59ada5056b242179b5b84bca1b54c00fefe13c`.
- At `2026-11-07 11:56:19 UTC`, direct GitHub API evidence for [run 42370027080](https://github.com/ibis-ssl/Duck/actions/runs/42370027080), attempt 1, identifies PR head `3239d266df5b888f941189f9c8ff4521894ac333`. The run is `in_progress`, no conclusion. Test job `128396043175` completed/success; ASPIRE-NET-002 job was skipped. Linux acceptance job `128396533939` is in progress; its scoped Python/sanitizer step and AppHost/harness build completed/success. No terminal native acceptance or archive result has been inspected.

| Affected criterion | Disposition | Result |
|---|---|---|
| Guard correctness / platform compatibility | checked_no_finding | Explicit Linux root rejection; non-Linux short circuit; nonzero Linux UID:GID path unchanged |
| Changed-file / scope / diagnostics impact | checked_no_finding | Complete two-file delta inspected; clear fail-early exception; no new image/network/permission changes beyond the existing candidate |
| Regression evidence | checked_no_finding | Structural guard assertion reviewed; current test job and scoped Python/build steps successful |
| Exact-head MATCH-001/002 and archive | unexplored | Current Linux acceptance still in progress |
| NR-003 / NR-004 | held | Original P1 identities and open closure matrices carried forward; guard does not establish host-reader or GC runtime success |
| E51-001 | held | Pending completed exact-source Linux acceptance/artifact review |

**Latest verdict: incomplete at 3239d266.** The bounded non-root guard delta has no new finding. NR-003, NR-004 and E51-001 remain open until the exact-head native match and archive evidence satisfies their existing action matrices. Historical 709d1b4 failure and 4e5c904 superseded cancellation remain preserved; this section supersedes the earlier statement that 3239d266 had not yet been reviewed.

Parent next action: retain the terminal result and artifacts of run 42370027080 and return only the finding/CI delta to this same reviewer. `report_attestation_allowed: false`; no independent final-review or merge verdict is issued.



### Successful exact-head G1 acceptance and normal-review closure - 42370027080

#### Reviewed source, execution identity and CI

- Reviewed technical HEAD: `3239d266df5b888f941189f9c8ff4521894ac333`; same normal reviewer, continuing only the carried finding/CI-evidence verification. No new exhaustive review or independent final review.
- Direct GitHub API confirms [run 42370027080](https://github.com/ibis-ssl/Duck/actions/runs/42370027080), attempt 1, PR head 3239d266, **completed/success**, last updated `2026-11-07T12:01:56Z`. Normal test job `128396043175` and Linux acceptance job `128396533939` both succeeded.
- The artifact records actual checkout `b14eb2923673ac640dbd47e5693c7e4922e84469` at `refs/pull/117/merge`. Connector comparison 3239d266..b14eb292 reports one test-merge commit with **files=[]**, binding that execution tree to the reviewed technical HEAD.
- Artifact root: `C:\Users\donabe\Documents\Codex\2026-11-07\task\issue51-ci-42370027080\aspire-full-stack-42370027080-1`. All inspection, hashing and gzip decompression was read-only; no tests/builds/Docker/CI mutation or source change was performed by this reviewer.
- Reviewed the acceptance/result/status/capture JSON, team/publisher and all participant inspections, GC evidence log, controller terminal-state record, workflow conditions, archive and final CI step dispositions.

#### Exact G1 requirement dispositions

| G1 requirement | Disposition and retained evidence |
|---|---|
| Native Linux execution | **Pass**: `appHostOperatingSystem=linux`, `linuxHostVerified=true`; Linux job and environment evidence retained |
| Seven participant resources | **Pass**: `autoref-tigers`, `crane`, `duck`, `game-controller`, `simulator`, `ssl-log-recorder`, `tigers-blue`. All seven current container IDs match their saved inspect files and owner/stack/run labels |
| All final participants ready | **Pass**: all seven rows are Running/Healthy/`isReady=true` in participant-status at `12:01:06.767307+00:00` and in match-result final participant evidence; final aggregate readiness has participant/API-referee/Vision all true, `isComplete=true` |
| Excluded resources | **Pass within the retained verifier evidence**: `excludedResourcesAbsent=true` is produced by the successful live inventory check. A raw complete pre-shutdown Docker inventory is not separately retained; the empty before-cleanup listing is a post-AppHost-shutdown view and is not used as proof of startup inventory |
| Crane/team mapping | **Pass**: retained fixture and Crane inspect show `team:=ibis`, `team_color:=YELLOW`; initial referee snapshot also names yellow team `ibis` |
| Simulator/Sumatra mapping | **Pass**: actual inspected commands contain simulator `--ibis-team-color yellow` and Sumatra `--aiBlue`; GC evidence records TIGERs Mannheim and BLUE-TIGERs Mannheim client connections |
| Initial STOP observed | **Pass**: match result retains `initialStopCommand=STOP`, initial snapshot command 1/counter 0/yellowTeamName ibis; this is the controller's retained initial STOP observation, not a claim to have captured the GC's first-ever datagram |
| Configured 11003 publisher | **Pass as configuration evidence**: current-run verifier records `configured11003Publishers=["game-controller"]`; saved GC command uses `-publishAddress 224.5.23.1:11003` |
| Actual observed source-tuple uniqueness | **Pass for the retained window**: capture complete/not truncated; **4,905** total datagrams, all from **172.18.0.2:39916**, counts reconcile, destination `224.5.23.1:11003` |
| Capture identity/window | **Pass**: stack/run `gha-42370027080-1`, match-controller container `943c9ad472835f849996db4e5615f63aaf48d6a838d06f4ef6b90c6eacd98209`, generation 1, receiver epoch 2; result binds epoch 2. Window `11:58:56.1830266+00:00` to `12:01:02.6701829+00:00` lies after controller start and before its recorded exit |
| Recorder input evidence | **Pass**: acceptance/participant-status retained host-decoded counts referee **10,397**, vision detection **16,014**, tracker **41,069**, with recorder ready. Earlier final-result snapshot counts are 10,219 / 15,742 / 40,360; these are different retained times, not a conflicting single snapshot |
| Finalized recorder archive | **Pass**: preserved `2026-11-07_11-56-37.log.gz`, **17,429,925 bytes**, with `recorderArchiveFinalizedAfterRecorderStop=true`. Reviewer independently streamed SHA-256 and gzip decompression to EOF/CRC successfully: **88,811,890 decompressed bytes** |
| G1 generated verdict | **Pass**: `status=passed`, `issue51AcceptanceComplete=true`, `unverifiedCriteria=[]` within the explicitly named ASPIRE-MATCH-001/002 scope |

Archive SHA-256 independently matches both the generated report and the parent's host check:

`470fdfecc97d4c6a6d9d63e794cf5cc39f95255a3590137c2a13bb7ed9d6ade5`

Source-tuple capture and recorder counts cover different observation intervals and are not expected to be equal. Their difference is not used to infer packet loss, additional publishers or complete whole-match coverage.

#### Why step 11 is skipped while G1 passes

- In match mode, step 9 (`Wait for all full-stack resources to report ready`) executes `scripts/verify_aspire_match_acceptance.py`, which writes the G1 acceptance report after checking the live resource/team/capture/result evidence. Step 9 succeeded in this run.
- Step 11 (`Run full-stack packet and active-motion acceptance`) has the explicit condition `steps.aspire_full_stack_ready.outcome == 'success' && steps.aspire_full_stack_start.outputs.apphost_mode != 'match'`. It is intentionally skipped for match mode. This does not skip the G1 verifier, and it does not establish a pass of the separate packet/active-motion suite.
- Step 10 is reported success but its match-mode body writes `status=not-applicable, mode=match`; no separate tracker-input capture from that step is inferred.
- Step 12 cleanup runs the match archive finalizer and succeeded; artifact upload succeeded. Step 14 is an error-only guard (`archive_success == 'false'`) and was skipped because archive finalization did not fail. A skipped error guard is not a skipped finalizer.
- Match Controller terminal-state evidence records exited/accepted after verified G1 evidence. The match result itself is **failed / maximum_time / MaximumTimeReached**, with `MATCH_MAXIMUM_TIME_REACHED: execution stopped without POST_GAME`; `fullMatchPassed=false`. G1 passing does not claim POST_GAME or a successful full match.

#### Carried finding closure matrix

The current native full match-mode composition is the actual regression fixture for the former access failures; it exercises the pinned recorder and registered GC, actual host reader, controller and finalizer together. Its successful behavior supplies the missing runtime cells, without inventing an unexecuted isolated UID fixture.

| Finding / original required action | Final-source production/composition evidence | Closure |
|---|---|---|
| I51-NR-001 / P1: accept verified completed controller while preserving seven live participants and negative gates | Unchanged tested state helper; current match-controller exited, accepted-state record present, all seven participants ready, G1 report and capture identity verified | **Closed at 3239d266** |
| I51-NR-002 / P1: exact-source contexts, required assets and native route evidence | Current run's publish-context step succeeded; actual Duck/controller images ran, helper contracts/negative fixtures carried forward, native G1/archive evidence now inspected | **Closed at 3239d266** |
| I51-NR-003 / P1: actual recorder output readable during recording and after stop | Host reader decoded positive current-run counts and recorder was ready; host finalizer read/hash/decompressed/copied the archive; independent retained-file CRC/EOF/hash checks passed | **Closed at 3239d266** |
| I51-NR-004 / P1: GC successfully accesses state and reaches readiness | Current GC is run-owned, Running/Healthy; current initial STOP/ibis state, API/referee readiness and changing-command result retained; native composition completed G1 instead of state-open failure | **Closed for the observed access/startup contract at 3239d266** |
| E51-001: completed exact-source Linux MATCH-001/002 and archive evidence | Successful matched execution tree and reviewed artifact set above | **Satisfied** |

Original P1 identities/severities and the failed 709d1b4 artifacts are preserved. No severity reclassification is made. The exact low-level reason for the former configured supplemental-group route's GC denial remains unproven; the current observed working owner-UID route closes the behavior finding without asserting why the old route failed.

#### Final normal-review criterion dispositions

| Required criterion | Final disposition |
|---|---|
| Accepted requirement/design conformance | checked_no_finding - G1 scope above passes |
| Correctness and edge cases | checked_no_finding - prior source/helper/negative-fixture review plus successful native composition |
| Scope discipline / unrelated changes | checked_no_finding - prior full-range review carried forward |
| Changed files / direct dependencies | checked_no_finding - full initial and bounded fix coverage complete |
| API/data/configuration/workflow compatibility | checked_no_finding - match-specific gates and Linux identity path exercised |
| Error handling and diagnostics | checked_no_finding - failed and successful runtime evidence retained; source conclusions do not erase historical failures |
| Security / secret handling | checked_no_finding - prior source review and non-root guard carried forward; no new credential exposure found |
| Tests and validation adequacy for G1 | checked_no_finding - current CI test, scoped Python, build/publish and actual Linux G1 composition succeeded |
| Matching current-source CI | checked_no_finding - run 42370027080 completed/success; actual PR merge tree equals technical head |
| Reports / evidence accuracy | checked_no_finding - tuple/process/full-match distinctions, source association and differing snapshot counts explicit |
| Regression / maintainability | checked_no_finding - four finding identities closed with source/fixture/runtime evidence |
| Former GC group-denial mechanism / raw full inventory reconstruction | held - parent owns any further forensic investigation; not a G1 behavior blocker after current successful evidence |
| POST_GAME / full match / MATCH-003/004/005 | not_applicable to G1 |
| Publishing-process/container attribution | not_applicable to accepted source-tuple criterion; remains unverified |

#### Latest normal-review verdict and separate completion requirement

- **pass_with_held** for technical HEAD `3239d266df5b888f941189f9c8ff4521894ac333`. All four required normal-review findings are closed, E51-001 is satisfied, and no required normal-review finding or G1 evidence gate remains.
- Nonblocking held limits, owned by the parent: the old GC supplemental-group denial's exact mechanism is unresolved; the raw complete live inventory is represented by the successful verifier result rather than a separately retained Docker dump; source uniqueness is limited to the recorded current-epoch window and does not identify a publishing process/container. Whole-match success and other OS acceptance are not established.
- `uniqueOnWireSenderAttributionVerified=false` and `fullMatchPassed=false` remain correct. The passing G1 result must not be expanded to either claim.
- **Independent final review remains separately required.** This normal reviewer is not independent-final-review eligible for this task. This verdict does not claim the required independent final review occurred, authorize merge, or supply an independent-final-review report attestation.
- `report_attestation_allowed: false`. The verdict applies to the named technical source. Any later commit/source association belongs to the parent and the final review lifecycle.

#### Final artifact identity hashes

| Relative artifact path | SHA-256 |
|---|---|
| `match/match-acceptance.json` | `7a0d7c0913f36fe8ac687e7076095aeaf27eeb2470e63c8464124b4ba41d0d29` |
| `match/match-result.json` | `5de1d625132a63fff921656207696820d73ad606b210e42edead96bca013c2bc` |
| `match/participant-status.json` | `1913a04ae665093718597a6056683ac03d43167c353a0644866acd846438ec76` |
| `match/referee-source-capture.json` | `f02757b36152fb23e05a63457278c94273d4e9bc5ff6ecddcbfd3d5fd93d9c7d` |
| `match/game-controller-evidence.log` | `a8ab54fc17ef489ae2797c8be8fae5bd04f7d7b5b710f69f0616c66b71901ed7` |
| `environment-before.txt` | `b9e06d55cbb2885ef2d85ed22ab49bd3d943506b70961a275af34ff53ee79db7` |


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


### Completed-run risk update

- The earlier H51-001 runtime-access concern now has concrete failures, tracked as required NR-003 and NR-004 against run source 709d1b4. Recorder host-read failure is established; GC effective-credential/access attribution remains unexplored. No root-cause conclusion about security profiles, UID-only mismatch or group-add effectiveness is supported by the retained artifact. The newly advanced 4e5c904 implementation has not been reviewed in this evidence-only pass. No new permission/connection/installation approval blocker was encountered.


### Identity-fix risk update

- The 4e5c904 candidate changes the image user rather than preserving it; pinned-image behavior under that numeric UID/GID remains unproven until runtime evidence arrives. The separately committed root guard at 3239d266 was not reviewed in this assigned pass. Supplied local result counts have no new retained logs; API initially observed 42369796318 in progress; its later confirmed cancellation is recorded above and supplies no completed acceptance result.


### Final normal-review risk update

- All four normal-review finding identities are closed and E51-001 is satisfied for 3239d266. Earlier open/pending/fail statements remain historical evidence only. The old GC group-denial mechanism, raw live-inventory reconstruction and bounded tuple/process-attribution limits are nonblocking held items with the parent as owner. Full-match success remains false/outside G1. Independent final review is still separately required and was not performed by this reviewer.

## Next action

- Review the exact committed range. If findings require changes, parent will fix, run applicable local verification, commit, and reuse this normal reviewer for fix verification. Parent PR #118 remains draft and will not merge to main.
