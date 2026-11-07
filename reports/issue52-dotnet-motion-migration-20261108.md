# Issue #52: .NET motion verifier and latest match images

## Scope

- Issue: [#52](https://github.com/ibis-ssl/Duck/issues/52)
- Child PR: [#119](https://github.com/ibis-ssl/Duck/pull/119), Draft
- Branch: `task/issue52-aspire-006g2`, base `task/issue50-aspire-006g`
- Replaces the Python-only motion archive verifier with the existing .NET SSL log reader and protobuf types, and uses current match images in `Testing:Mode=match`.

## Implementation

`SslLogRecordReader` now exposes typed record observations while retaining its prior summary and append-stream APIs. `SslLogMotionAcceptanceVerifier` checks that Crane Yellow and TIGERs Blue detections move inside one contiguous active referee command window. It rejects inactive commands, malformed or out-of-range referee counters, duplicate/stale counters, duplicate robot IDs within one frame, missing/non-finite coordinates, incomplete record tails, and gzip members with an invalid CRC32 or uncompressed-size footer. Raw counter validation is required because protobuf's uint32 parser truncates oversized varints. Previously decoded motion remains visible in a partial report but cannot pass an incomplete archive.

`Duck.Testing.AppHost --verify-motion-archive <archive> [report.json]` is called by match-workflow cleanup after the recorder archive is finalized. Match mode uses the latest simulator, Game Controller, Crane, Sumatra, AutoRef, and recorder images. Runtime references pin the resolved Linux/amd64 child manifests; `linux-image-lock.json` records each registry root/index and platform child separately. Other modes keep their existing selections. No package was added. SSL_LOG_FILE v1 has no UDP source endpoint fields, so this verifier does not establish source-tuple uniqueness.

The existing Linux distribution-definition gate now records the updated match-mode root/platform image metadata and checks that each Linux/amd64 lock digest agrees with the AppHost pin. It also retains the comparison-mode defaults. The motion archive implementation and acceptance itself are .NET; the existing publish-contract gate is still used by the repository's CI.

## Latest Linux match attempt and diagnosis

- Exact-head CI run `42388488492` for `f86e7facdafd4a1cd520e616f49544017bca98a1` passed: 1,317 .NET tests passed, 4 skipped; the Linux distribution contract passed all 25 tests; Linux publish artifact validation passed. The full-stack job was not part of this PR-triggered run.
- Normal review and independent final review both completed `pass_with_held` on `f86e7fa`; they found no remaining source-code findings, but live acceptance remains held.
- Workflow run `42388811953`, attempt 2, used the pinned Linux/amd64 images and ran for 15 minutes. Runtime readiness failed because the Crane container exited with code 2. Its startup log reports that the current `sim=true` launch did not define the expected `ibis_target_address` `<let>` in the conditional form.
- The downloaded recorder archive is complete (237,446 protobuf records; gzip/footer verified); the .NET motion verifier reports `Passed=false`, `ActiveWindows=[]`, reason `No active referee command window contained position changes for both teams.` The readiness failure prevented the separate final match-result/source-capture acceptance from completing. `match-acceptance.json` records `status=incomplete`; no acceptance pass or merge is claimed.
- The public `scenario-develop` launch XML now declares the unique loopback target unconditionally (`<let name="ibis_target_address" value="127.0.0.1"/>`), while the existing overlay accepted only `if="$(var sim)"`. A small compatibility fix now accepts either that unique unconditional loopback target or the previous exact simulation-conditional form. The regression test was added to the existing overlay test suite. This fix has not yet been exercised by a new full-stack run.
- The Linux distribution-definition tests remain Python-based. Porting this existing 25-case publish-contract gate to .NET would require replacing its lock/catalog validation, Dockerfile parser, AppHost source wiring assertions, publish-output/runtimeconfig checks, CM4 helper byte check, and mutation fixtures together. Simply removing the Python invocations would skip existing protection. The new motion verifier does not depend on Python; this broader gate migration remains outstanding under the user's .NET-first preference.
- The local Docker CLI candidate path is inaccessible in this delegated environment, so no local container/image inspection or acceptance rerun was performed here. Exact-head CI remains the available verification evidence until the fix is pushed and rerun.

## Validation evidence

- Filtered test command:
  `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SslLogRecordReaderTests|FullyQualifiedName~ComparisonModelKeepsPinnedSimulatorCraneAndCm4ArgumentsOnDcpContainers|FullyQualifiedName~MatchModePinsSimulatorGameControllerAndFixtureConfiguration|FullyQualifiedName~MatchModeRegistersTigersAutoRefAndLogRecorderOnSharedDcpBridge|FullyQualifiedName~MatchModeComposesLifecycleControllerAfterRequiredMatchResources" --logger "console;verbosity=minimal"`
  Result: **19 passed, 0 failed**. Covers same-window motion, counter overflow/wrap/staleness, duplicate IDs, malformed Vision, absent coordinates, partial record tails, missing footer, trailing bytes plus a copied valid footer, match image configuration, preserved comparison image configuration, and model composition. NU1900 warnings note that the NuGet vulnerability feed was unreachable; tests completed with already available packages.
- `git diff --check`: passed.
- Docker Desktop `4.96.0.243774`, Engine `29.9.1`, Linux `amd64`, context `desktop-linux`.
- `docker manifest inspect --verbose` and digest-specific pulls succeeded for all six match images. Linux/amd64 child digests pinned by match mode: simulator `9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554`; Game Controller `2c0a03394c37541b6d4df8ec5f97207c70b6ddbe553e04bc4c049ffc58b02ef4`; Crane `40435ed83e7a6c4f79a029fe576f1e3c771b38f5606377b4450668ef5dd4392b`; Sumatra `0c05451f26aeb21bffde4c3d6858b6f441bd6bfaf8286f0ecd799241cb1fbc6c`; AutoRef `96dd8417c1fcf4025ee0a20c8210954068440991989587b68ecdea7d27db6b3d`; recorder `d7ee410736512340602baa88f03762bfc3c5b590a3f91097e5b7e0bb45b7207b`. Root/index digests are distinct and recorded in `linux-image-lock.json`; AutoRef latest root is `e3571a2c10218522483d25261426681dd75a734f66c0c85877fec61481a158a3`.
- Exact-head CI `42391765626` for `e55b591ee572ad8c30c09c005359005cd35b327a` passed, including the Crane overlay test, full .NET test job, and Linux publish validation. This CI still ran the existing Python Crane overlay test and distribution-definition contract; those changes do not meet the latest Python-free implementation direction.
- Workflow dispatch `42392063603` ran the full-stack match on `e55b591`. The Crane compatibility change let all Crane ROS nodes start. All seven participants reached initial readiness and the initial referee command was STOP, counter 0, next command KICKOFF(YELLOW), but MatchController timed out waiting for the initial STOP/HALT/PREPARE/NORMAL_START sequence. `match-result.json` has `status=failed`, `stageReached=start`, and `finalReadiness=null`.
- Recorder archive `2026-11-07_16-39-24.log.gz` finalized completely (11,119,001 compressed bytes, SHA256 `d90c3538d1d686617c8626b4f6820fb0303b9eb0974e1e41cb0440038a856ea4`, 83,751,743 decompressed bytes). The .NET verifier processed 74,331 records but returned `Passed=false`, `ActiveWindows=[]`; no active referee window showed position changes for both teams. The referee source capture completed with 4,641 datagrams and one observed source tuple, `172.18.0.2:54311`. This does not establish a passing match.
- The artifact acceptance script then crashed while reading the failed result because `finalReadiness` was null (`AttributeError: 'NoneType' object has no attribute 'get'`). This is a secondary diagnostic failure; MatchController's initial sequence timeout is the underlying acceptance blocker.
- Exact-head CI and reviews for `e55b591` are not complete as a release set: the successful CI is known, but normal/independent reviews applied only to `f86e7fa`; the Python additions in `e55b591` conflict with the latest instruction and must be moved to .NET or another approved non-Python implementation before merge.

## Not verified

- No Linux full-match run has completed for this candidate. Current-run packets, same-window motion, source-tuple uniqueness, match result, and finalized recorder archive are not verified.
- The Crane compatibility change is pushed and its unit test passed in CI, but the subsequent full-stack acceptance failed as recorded above. A conforming non-Python implementation is still needed, followed by the required start-sequence and active-motion acceptance.
- ASPIRE-MATCH-003 therefore remains incomplete. PR #119 remains Draft and must not merge into its parent yet.
