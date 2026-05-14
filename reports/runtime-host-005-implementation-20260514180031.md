# RUNTIME-HOST-005 実装レポート

## タスク

`Tracker.DebugHost` が持っていた tracker operation loop を、将来 `Tracker.RuntimeHost` から再利用できる UI 非依存 shared runtime boundary として `Tracker.Core` へ抽出した。

## 対象範囲

- `Tracker.Core/Runtime` に `TrackerCoordinator`、`TrackerRuntimeResolvedOptions`、publisher / snapshot 関連型を追加。
- `TrackerCoordinator` の engine update、profile switch drain、event dispatch、official packet publish、latest snapshot store 更新、observer 通知を Core 側へ移動。
- `ITrackerPacketPublisher`、`TrackerPublisherOptions`、`TrackedSnapshot`、`TrackedSnapshotStore`、`UdpTrackerPacketPublisher` を Core 側へ移動。
- `Tracker.DebugHost` は `VisionReceiverService` から Core coordinator を呼ぶ adapter として維持。
- DebugHost の `TrackerResolvedOptions` は diagnostics 設定を持つ派生 shape とし、Core の loop 用設定は `TrackerRuntimeResolvedOptions` に分離。
- R005 focused contract と既存 coordinator/profile request tests を更新。
- RuntimeHost / Core 設計文書へ R005 の Core shared runtime boundary 方針を追記。

## 対象外

- 新規 `Tracker.RuntimeHost` project scaffold。
- R006 の DebugHost read-side UI 化。
- R007 の diagnostics sample sidecar。
- R008 の RuntimeHost project scaffold。
- 旧 diagnostics log / render snapshot sidecar 互換の維持。
- DebugHost diagnostics logging の新しい保存経路実装。

## 変更概要

- Core runtime source が `Tracker.DebugHost`、Blazor、diagnostics / capture writer / reader、`VisionPacketCaptureSession`、`TrackerRenderSnapshot`、`TrackerPacketSnapshotLog`、`TrackerSnapshotAlignmentLog` を参照しない contract を追加。
- Core `TrackerCoordinator` は committed frame ごとに snapshot 更新、official packet publish、observer 通知を event 順に行う。
- profile switch は pending / in-flight request として扱い、control-only update で drain し、publisher config と snapshot clear を observer 通知前に完了する。
- publisher 例外は coordinator から外へ漏らさず、`TrackedSnapshotStore.PublishFailureCount` を増やす。
- 旧 `TrackerCoordinatorDiagnosticsCaptureTests` は、Core coordinator が DebugHost diagnostics / capture sidecar を直接生成しない契約へ更新した。

## テスト / ビルド結果

- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostSharedOperationLoopBoundaryTests -m:1 /nr:false`
  - 成功。5 tests passed。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerCoordinatorFrameFlowTests|FullyQualifiedName~TrackerCoordinatorResetAndProfileTests|FullyQualifiedName~TrackerProfileRequestServiceTests" -m:1 /nr:false`
  - 成功。8 tests passed。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Core/Tracker.Core.csproj -m:1 /nr:false`
  - 成功。0 warnings / 0 errors。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false`
  - 成功。0 warnings / 0 errors。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`
  - 成功。0 warnings / 0 errors。
- 追加確認: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~TrackerCoordinatorDiagnosticsCaptureTests -m:1 /nr:false`
  - 成功。2 tests passed。
- `git diff --check`
  - 成功。出力なし。

## リスク

- DebugHost の旧 coordinator diagnostics file logging / render snapshot sidecar 生成は Core loop から外したため、旧 diagnostics log 互換を前提にした運用は R005 後の対象外となる。
- DebugHost 固有の新しい diagnostics sample / sidecar 保存経路は R007 側の責務として残る。
- `Tracker.RuntimeHost` project 自体はまだ存在しないため、RuntimeHost 起動経路との DI 結合は R008 scaffold 時に別途確認が必要。
