# RUNTIME-HOST-007 実装レポート

## タスク

DebugHost diagnostics sample sidecar fast path を実装し、diagnostics sample tick を replay timeline / Vision Input 復元の主経路にする。

## 対象範囲

- `diagnostics-samples.jsonl` の schema / reader / writer。
- CaptureOn metadata の `DiagnosticsSampleSidecarPath` / `DiagnosticsSampleLog`。
- DebugHost Home refresh tick で `VisionLiveDisplaySnapshotProvider` の fixed live display snapshot を sample sidecar へ保存する経路。
- `TrackerDiagnosticsComparisonViewStateReader` の diagnostics sample sidecar fast path。
- R003 Red contract と writer/metadata focused test の green 化。
- R007 実装に合わせた RuntimeHost / DebugHost 設計文書の具体名追記。

## 対象外

- RUNTIME-HOST-008 の `Tracker.RuntimeHost` headless project scaffold。
- 旧 render snapshot sidecar だけを持つ session の互換復活。
- tracker operation loop から diagnostics logging を直接駆動する変更。

## 変更概要

- `DiagnosticsSampleRecord` / `DiagnosticsSampleLogReader` / `DiagnosticsSampleLogWriter` を追加し、sample tick ごとに latest raw / tracked snapshot summary を JSONL に保存するようにした。
- `VisionPacketCaptureFile.BuildCapturePaths` と `VisionPacketCaptureSession` に diagnostics sample sidecar path / metadata 集計を追加した。
- `Home.razor` の refresh tick で fixed `VisionLiveDisplayRenderSnapshot` を capture enabled 時だけ diagnostics sample sidecar に追記するようにした。
- `TrackerDiagnosticsComparisonViewStateReader` は diagnostics sample sidecar が存在する session では sample timeline を `LoadReplayTimeline` の主経路にし、`Vision Input` / `ibis tracker` field source を sample sidecar から復元する。
- diagnostics sample sidecar がなく旧 render snapshot sidecar だけの session は `unsupported degraded legacy` error と空 replay timeline を返すようにした。
- `VisionPacketCaptureTests` に diagnostics sample sidecar metadata / record 増加の focused test を追加した。
- `raw-vision-viewer-plan.md` と `runtime-host-plan.md` に R007 で固定した sidecar 名と metadata 名を追記した。

## テスト / ビルド結果

- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostDiagnosticsSampleBoundaryContractTests|FullyQualifiedName~DiagnosticsSample" -m:1 /nr:false`
  - 成功。4 passed。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests|FullyQualifiedName~VisionPacketCaptureTests|FullyQualifiedName~DiagnosticsFieldViewFactoryTests|FullyQualifiedName~DiagnosticsPlaybackStateTests" -m:1 /nr:false`
  - 成功。82 passed。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false`
  - 成功。0 warnings / 0 errors。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`
  - 成功。0 warnings / 0 errors。
- `git diff --check`
  - 成功。

## リスク

- diagnostics sample sidecar の raw / tracked semantic summary は R007 の fast path と field 復元に必要な最小 DTO として追加した。詳細 payload 互換や旧 render snapshot sidecar 互換は意図的に対象外。
- Home refresh tick で capture enabled 時に 100ms cadence の sample record を保存するため、長時間 capture では sample sidecar が増える。R007 では performance 優先の JSONL 追記のみとし、保持期間や圧縮は後続判断とする。
- `Tracker/Design/tasks-status.md` は親 agent 側で既に in-progress 更新済みの未コミット差分として残しており、本実装 agent では final sync していない。
