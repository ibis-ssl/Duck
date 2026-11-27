# Sub-agent's report

## Task

- Purpose: implement the verified Linux match bind permission and observed UDP source tuple capture gaps for issue #51, then stop before tests, Docker, publication, review, or merge.
- Task type: bounded cross-module implementation, TDD; tests and independent Red evidence already exist.

## Why the sub-agent is used

- The work spans AppHost resource creation/runtime arguments, the existing referee receive path, acceptance verifier and tests; one implementation owner keeps these contracts aligned.

## Scope

- Make only scoped Linux permission/source-capture changes needed for #51 MATCH-001/002 and tests.
- Preserve Windows behavior and image-configured users. Use group access only where specific bind mounts require it; no world-wide chmod/chown or shared-temp mode changes.
- Preserve truthfulness: unique observed UDP source tuple within a bound capture window; no process/container attribution inferred from host networking; full match success remains separate.

## Out of scope

- Running tests/builds, Docker, installing dependencies, environment settings, issue/PR edits, commits, pushes, comments, CI reruns, reviews, and merges.
- Full-match success or MATCH-003–005 scope.

## Dispatch profile

- selection inputs: implementation; bounded technical with cross-system boundary; medium uncertainty; cross-module change radius; high criticality for acceptance evidence; single repetition; sequential dependencies; fresh.
- selection source: automatic.
- observed decomposability: sequential_dependencies.
- decomposition policy / disposition: forbidden for sequential test-first lifecycle; one implementation owner.
- proposed profile: none.
- approval status / evidence: not required.
- requested profile: gpt-6.1-sol / high (task context specified profile).
- agent role / default-role plan: default role; explicit role override none.
- role config evidence / profile effect: collaboration dispatch exposes model/reasoning only; final runtime profile may be unobservable.
- planned runtime profile: gpt-6.1-sol / high.
- applied profile: null until observable.
- application status: pending dispatch.
- runtime profile observability: pending.
- fork policy: none.
- reasons / constraints: independent Red is available; implementation must not claim source process identity or alter unapproved environment state.

## Commands

- No tests, builds, Red/Green commands, Docker commands, workflow/CI actions, commits, pushes, comments, reviews, or merges were run in this implementation phase. Reads, read-only Git inspection, one read-only GitHub issue fetch, and scoped source/report writes were performed.
- Governing test-first flow: parent-authorized independent Red is complete in `reports/issue51-linux-permissions-source-capture-verification-20261107.md`; this phase implements afterward and stops for an independent Green owner.
- Suggested bounded Green commands, not executed here, from the exact changed source:

```bash
timeout 180s python3 -B -m unittest discover -s scripts/tests -p 'test_*match*.py' -v
timeout 600s dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter 'FullyQualifiedName~Tracker.Tests.MatchLinuxBindPermissionsTests|FullyQualifiedName~Tracker.Tests.RefereeSenderTupleCaptureTests|FullyQualifiedName~Tracker.Tests.RefereeSourceCaptureLifecycleTests|FullyQualifiedName~Tracker.Tests.RefereeReceive|FullyQualifiedName~Tracker.Tests.RefereeDriverHost|FullyQualifiedName~Tracker.Tests.MatchControllerWorkerTests|FullyQualifiedName~Tracker.Tests.MatchParticipantStatusProjectionTests' --artifacts-path artifacts/issue51-green-verification-20261107 --logger 'trx;LogFileName=issue51-linux-bind-source-capture-green.trx' --results-directory /tmp/issue51-green-verification-20261107/results
```

- Native Linux permission verification is required. Use the ignored **in-repository** artifacts path above so AppHost repository-root discovery works; retain command, exit, stdout, stderr, TRX and source manifest. The earlier independent Red report preserves the failed out-of-repository `/tmp` build-layout attempt separately from the corrected assertion Red.

## Files

Implementation changes in this phase:

- `Testing/Duck.Testing.AppHost/LinuxMatchBindAccess.cs` (new): Linux-only targeted group/mode setup, numeric supplementary host GID, unchanged owner UID/image user, and link rejection. State store gets 0660, input engine config 0640, output directories 2770. Setgid on the two output directories lets files from host and container share the access group. No recursive operations or shared temp-root permission changes.
- `Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs`: prepare only the two run-specific copied Game Controller files; add the host group to Game Controller and recorder in match mode.
- `Testing/Duck.Testing.AppHost/Program.cs`: prepare only the result/recorder directories, add match-controller's host group, and opt into source capture at `/match-results/referee-source-capture.json` with current stack ID. Existing identity-control wiring is reused.
- `Testing/Duck.Testing.AppHost/MatchParticipantStatusProjection.cs`: retain the AppHost OS in participant status so G1 Linux-host completion comes from the actual AppHost projection.
- `Testing/Duck.Testing.RefereeDriver/RefereeSourceCapture.cs` (new): collect raw address/port counts in memory for one receiver identity/epoch, with run/container/generation, explicit observation bounds, total reconciliation and a bounded source set. Epoch changes reset the window. Abort retains incomplete evidence. Finalization atomically replaces the artifact; no file write occurs per datagram. Linux capture files are group-readable 0640.
- `Testing/Duck.Testing.RefereeDriver/RefereeReceiveService.cs`: feed the collector from actual received `RemoteEndpoint` values before decoding, including rejected payloads; reset with identity/epoch, abort on pump exit, and expose explicit capture finalization. Existing receive socket, decoding/command ownership, and diagnostic lifecycle remain in use.
- `Testing/Duck.Testing.RefereeDriver.Host/RefereeDriverApplication.cs`: opt-in collector construction before acquiring the transport lease. Full container/run/generation identity comes from the existing `RefereeIdentityControlHostedService` / `ContainerIdentityExecHandoff` handshake, not a guessed hostname or additional Docker discovery.
- `Testing/Duck.Testing.RefereeDriver.Host/MatchControllerWorker.cs`: atomically finalize the source artifact before the existing match-result write; add the frozen receive epoch to the result. Match outcome and maximum-duration logic are unchanged.
- `scripts/verify_aspire_match_acceptance.py`: preserve source artifacts, fail closed on missing/stale/incomplete/malformed captures, require current owned receiver/run/container/start evidence and result epoch binding, validate generation, destination and packet-count sums, and require one distinct observed address/port tuple. Accept both `-publishAddress` (actual AppHost spelling) and `--publishAddress`. Report observed-tuple verification separately from unproven process/container attribution. Permit the owned receiver to have exited after freezing its evidence while retaining Running/healthy requirements for all seven participants.

Regression changes in this phase:

- `Tracker/Tracker.Tests/RefereeSourceCaptureLifecycleTests.cs` (new): real UDP packets through the existing service prove malformed protobuf senders are still counted, two source ports remain distinct, counters reconcile, and finalization freezes the window; a separate case checks identity/epoch reset and incomplete abort persistence. A controlled pending-receive regression replaces identity before delivery and proves the stale packet is excluded from the new epoch before a fresh packet is counted.
- `scripts/tests/test_match_referee_source_capture.py`: strengthen the existing fixture with current receiver start/generation and explicit result epoch, stale-epoch/boot cases; add actual single-dash publisher spelling, non-Linux host, and completed-receiver-exit cases. The Linux G1 fixture now expects issue acceptance complete even with maximum_time, while fullMatchPassed remains false.

Unchanged test inputs from the Red phase: `Tracker/Tracker.Tests/MatchLinuxBindPermissionsTests.cs` and `Tracker/Tracker.Tests/RefereeSenderTupleCaptureTests.cs`. The new report is `reports/issue51-linux-bind-permissions-source-capture-implementation-20261107.md`; only its child-owned sections changed. Parent-owned Dispatch profile and all prior reports/evidence are preserved.

## Handoff

- Implementation is ready for independent Green verification against the exact working-tree content. No Green or independent review verdict is claimed.
- Pre-Green correctness correction from parent read-only review: the raw collector receives the captured local receiveEpoch selected before ReceiveAsync. It never relabels that awaited receive with mutable currentEpoch after identity replacement; the collector's ReferenceEquals guard rejects the stale epoch. The new controlled regression checks zero new-epoch sources after stale delivery and exactly one fresh source after the next receive.
- Authoritative scope rechecked using the read-only GitHub issue tool: [issue #51](https://github.com/ibis-ssl/Duck/issues/51) requires Linux MATCH-001/002, resource/team mapping and real 11003 uniqueness. It does not require POST_GAME or full-match success. Repository design history likewise separates ASPIRE-006G1 from G2/G3/G4.
- `issue51AcceptanceComplete` now reflects successful G1 checks plus AppHost-projected Linux OS; `fullMatchPassed` still depends only on the actual match result status. A maximum_time failure can therefore coexist with G1 acceptance. The existing worker behavior for a failed full-match result is preserved; the verifier can read a completed receiver's frozen evidence after that receiver exits.
- `uniqueObservedSenderTupleVerified` means exactly one tuple among raw datagrams observed by the configured 224.5.23.1:11003 receiver during the retained window. `uniqueOnWireSenderAttributionVerified` remains false: host networking does not identify a publishing process/container. Scope and this limitation are explicit in the report output.
- Full receiver identity is available on the currently authorized CI path: CI already supplies Docker context/engine/executable, which enables the match-controller identity handoff. If identity never arrives, capture remains incomplete and acceptance fails closed. No workflow change was needed.
- No dependencies, environment settings, image-configured users or existing containers were changed. No existing artifacts or `run-42356804056-artifacts/` were touched. Do not discard the independent Red report's drvfs and first-attempt layout limitations.

## Results

- Mode: initial bounded implementation following independently established Red; governing scope and stopping point supplied by parent.
- Repository/issue: `ibis-ssl/Duck` #51, PR #117, branch `task/issue51-aspire-006g1`; starting/final technical HEAD `b2fd92ec7dae7e913c22714920001066e72c789a`. Base ref not independently re-resolved. Source is dirty/current-task-owned, with inherited untracked tests/reports/artifacts preserved. Current source hashes follow below for independent verification.
- Execution environment: Windows PowerShell via `exec_command`, working directory `C:\Users\donabe\RemoteDesktopWorkspace\Duck-issue51-aspire-006g1`. Verification capability supplied by independent Red is `local_execution_available` through approved WSL/.NET and Windows Python; this implementation owner did not execute them. Native Linux build/test behavior remains unverified after these edits.
- Supplied Red: Windows Python 5 methods / 15 assertion failures; corrected native WSL .NET 9 total / 7 target assertion failures / 2 supporting passes. The first .NET attempt's repository-root discovery errors are harness failures only. Tests and fixture metadata added/strengthened in this phase have not been run.
- Inspected required skills: work-context-manager, implementation-executor, implementation-worker, tdd-executor; used report-output-manager and report-writer for normal persistence. Parent retains TDD execution ownership. No subdelegation or orchestrator re-entry occurred.
- Commit: not performed/unauthorized. Push: not performed/unauthorized. CI wait: not performed. Full local equivalence gate: not started. Current-head CI success: not obtained. Existing evidence is historical/Red only.
- This bounded correctness follow-up changed only RefereeReceiveService.cs, RefereeSourceCaptureLifecycleTests.cs and this report. No test/build/Green/Docker command was run. No issue/PR mutation or publication occurred. Only the exact source/tests listed above and this report were edited in this implementation phase.

## Risks

- Compilation, discovery, collector lifecycle regressions and Green are unverified. The existing receive pipeline's identity/epoch races and shutdown behavior require the suggested related tests and independent review; no author-issued verdict is provided.
- Native Linux permission Red ran in WSL2 with source fixtures on drvfs `/mnt/c`; copied file bits there differed from ordinary Linux CI. Real image-user group membership, state-store append, recorder output and setgid inheritance still need the authorized Linux runtime acceptance.
- Engine configuration is treated as input (0640/group-read) per the accepted scoped contract; no image-specific behavior requiring mutation of that input has been verified. Existing users are preserved, and no numeric UID override is introduced.
- The capture destination is the actual existing socket's configured multicast subscription; raw source tuples come from received UDP endpoints. Per-packet destination ancillary metadata is not added. UDP loss/kernel drops and traffic outside the observation window are not measured, and uniqueness does not establish publishing-process/container identity.
- Incomplete capture is persisted at epoch creation and graceful pump failure; an abrupt kill can leave only the earlier incomplete snapshot. A complete artifact is never emitted without positive traffic, a current identity/epoch, a valid window and bounded source accounting.
- More than 1024 distinct source tuples marks the capture truncated/incomplete; no unbounded source dictionary is allowed. Counter overflow or persistence errors fail the receive/finalization path rather than claiming success.
- Without the existing identity handshake (for example, manually disabling the bridge), source acceptance remains incomplete; this is intentional fail-closed behavior, not a guessed identity fallback.
- G1 completion in generated report data does not claim the unrun implementation passed, that MATCH-003/004/005 succeeded, or that the full match passed. Exact-head Linux acceptance and independent review still belong to subsequent phases.
- No denied or pending operation remains. Existing certificates, SDK settings, containers and user evidence were not altered.

### Source manifest for the independent verifier

- Testing/Duck.Testing.AppHost/LinuxMatchBindAccess.cs: SHA-256 5e37c575e66c0bf18c53ca0cf3d0cdd5ae00bf0b0fff0b7f3a608cb88ea62e9e
- Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs: SHA-256 dfdac8f85d7e00cb0dbd769fcbdc185584e67b066f61e5f07bc7d2a022839da8
- Testing/Duck.Testing.AppHost/Program.cs: SHA-256 8ab7e8994929f1679f5fae1f4ce1bda1b5266441ef118796409261494b0cd18f
- Testing/Duck.Testing.AppHost/MatchParticipantStatusProjection.cs: SHA-256 afd0d4eca2bd4d2f81c073349ba9b24b886e077c9b8f5112137d8113a38da09f
- Testing/Duck.Testing.RefereeDriver/RefereeSourceCapture.cs: SHA-256 ba8a74a7b2b0c68db9493d900e3854cd6e3668219c12a5b3b8af66e88586d4db
- Testing/Duck.Testing.RefereeDriver/RefereeReceiveService.cs: SHA-256 012c5dd34843ba8c92082a85a72468cca2040d1aaeea256dc44e5c2b0ab2bd40
- Testing/Duck.Testing.RefereeDriver.Host/RefereeDriverApplication.cs: SHA-256 193068a22ccedbcda4236e216e0c451fee6e623740eda081dee735f8c9bfd901
- Testing/Duck.Testing.RefereeDriver.Host/MatchControllerWorker.cs: SHA-256 34b7b2ffbdb1616bef8f05cb3c6a2cb40f37eda1f9424bdd51ff572afbeb8e26
- scripts/verify_aspire_match_acceptance.py: SHA-256 690287f3a956be75a4386236b320edeaea4ccb93d0ab14ac67d4af629b351ee0
- scripts/tests/test_match_referee_source_capture.py: SHA-256 69f218e5fb4d122c26da6963221d486f8d1a1c22e84819adbcf5a8b0312ba53f
- Tracker/Tracker.Tests/RefereeSourceCaptureLifecycleTests.cs: SHA-256 00a78f2650dd70da93d7ee0a4ae99f76325f5690590d1c7f4cc62d98e3b38582
- Tracker/Tracker.Tests/MatchLinuxBindPermissionsTests.cs: SHA-256 079ffac4ffc0b84e4bf7005ceb9b873de4ee9a8f57be03b408d55ebcb6ae5dc4
- Tracker/Tracker.Tests/RefereeSenderTupleCaptureTests.cs: SHA-256 3b5b99139f7f23c6fa09bf83277179df10f5e91fd0f2bcfefc2b78611f87258e
