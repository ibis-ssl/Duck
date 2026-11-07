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

## Validation evidence

- Filtered test command:
  `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SslLogRecordReaderTests|FullyQualifiedName~ComparisonModelKeepsPinnedSimulatorCraneAndCm4ArgumentsOnDcpContainers|FullyQualifiedName~MatchModePinsSimulatorGameControllerAndFixtureConfiguration|FullyQualifiedName~MatchModeRegistersTigersAutoRefAndLogRecorderOnSharedDcpBridge|FullyQualifiedName~MatchModeComposesLifecycleControllerAfterRequiredMatchResources" --logger "console;verbosity=minimal"`
  Result: **19 passed, 0 failed**. Covers same-window motion, counter overflow/wrap/staleness, duplicate IDs, malformed Vision, absent coordinates, partial record tails, missing footer, trailing bytes plus a copied valid footer, match image configuration, preserved comparison image configuration, and model composition. NU1900 warnings note that the NuGet vulnerability feed was unreachable; tests completed with already available packages.
- `git diff --check`: passed.
- Docker Desktop `4.96.0.243774`, Engine `29.9.1`, Linux `amd64`, context `desktop-linux`.
- `docker manifest inspect --verbose` and digest-specific pulls succeeded for all six match images. Linux/amd64 child digests pinned by match mode: simulator `9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554`; Game Controller `2c0a03394c37541b6d4df8ec5f97207c70b6ddbe553e04bc4c049ffc58b02ef4`; Crane `40435ed83e7a6c4f79a029fe576f1e3c771b38f5606377b4450668ef5dd4392b`; Sumatra `0c05451f26aeb21bffde4c3d6858b6f441bd6bfaf8286f0ecd799241cb1fbc6c`; AutoRef `96dd8417c1fcf4025ee0a20c8210954068440991989587b68ecdea7d27db6b3d`; recorder `d7ee410736512340602baa88f03762bfc3c5b590a3f91097e5b7e0bb45b7207b`. Root/index digests are distinct and recorded in `linux-image-lock.json`; AutoRef latest root is `e3571a2c10218522483d25261426681dd75a734f66c0c85877fec61481a158a3`.
- Exact-head CI for `ac61e08cb9fb444f7c3a30dc58ca0c09aa16a0be` failed at the existing Linux distribution-definition contract because it still expected pre-match image metadata and single-mode defaults. The contract and its fixtures now describe the match pins and preserve comparison defaults. Rerun exact-head CI before release.
- No live match dispatch is currently running.

## Not verified

- No Linux full-match run has completed for this candidate. Current-run packets, same-window motion, source-tuple uniqueness, match result, and finalized recorder archive are not verified.
- Exact-head CI and normal/independent review of the current changed candidate are pending.
- ASPIRE-MATCH-003 therefore remains incomplete. PR #119 remains Draft and must not merge into its parent yet.
