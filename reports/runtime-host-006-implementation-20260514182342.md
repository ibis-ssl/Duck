# RUNTIME-HOST-006 実装レポート

## タスク

DebugHost live display を read-side snapshot 境界へ寄せ、UI render tick ごとに raw / tracked / 3rd party tracker の latest immutable snapshot を固定する。

## 対象範囲

- `Home.razor` の live display read path。
- raw / tracked / external tracker を 1 render tick で固定する composite snapshot provider。
- comparison snapshot / view-state 生成の fixed snapshot 化。
- 3rd party tracker live state の read-side snapshot store 化。
- RUNTIME-HOST-006 focused tests と関連設計文書の最小更新。

## 対象外

- RUNTIME-HOST-007 diagnostics sample sidecar fast path。
- RUNTIME-HOST-008 RuntimeHost scaffold。
- 旧 diagnostics log / render snapshot sidecar 互換。
- `MultiTrackerManager` 自体の広範囲改修。

## 変更概要

- `VisionLiveDisplaySnapshotProvider` と `VisionLiveDisplayRenderSnapshot` を追加し、1 render tick で `VisionPacketStore`、`TrackedSnapshotStore`、`ExternalTrackerSnapshotStore` を 1 回ずつ読み、Raw / Tracked / Compare 表示を同じ composite snapshot から派生する構造にした。
- `Home.razor` は `VisionPacketStore` / `TrackedSnapshotStore` / `VisionLiveComparisonSnapshotComposer` を直接 inject せず、live display provider だけを inject する形へ変更した。
- `VisionLiveComparisonSnapshotComposer` は store を直接読まない stateless composer とし、固定済み raw / tracked / external tracker snapshot から `VisionLiveComparisonRenderSnapshot` と view-state を生成する形へ変更した。
- `ExternalTrackerSnapshotStore` を追加し、`MultiTrackerManager<TrackerPacketAdapter>` の update event から packet と metadata を clone 済み read-side DTO として保持するようにした。render path は `MultiTrackerManager.Trackers.Values` を直接読まない。
- `RuntimeHostDebugHostReadSideSnapshotBoundaryTests` を追加し、Home direct injection 禁止、render tick snapshot 固定、operation loop API 非参照を focused contract として固定した。
- `runtime-host-plan.md` と `raw-vision-viewer-plan.md` を R006 の実装済み境界名に合わせて更新した。

## テスト / ビルド結果

- Red 確認:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostDebugHostReadSideSnapshotBoundaryTests" -m:1 /nr:false`
  - 失敗: `ExternalTrackerSnapshotStore`、`VisionLiveDisplaySnapshotProvider` 未実装、および composer が store 依存 constructor だったため compile error。
- Green focused test:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostDebugHostReadSideSnapshotBoundaryTests|FullyQualifiedName~VisionLiveComparisonViewStateTests|FullyQualifiedName~TrackedVisionViewStateTests" -m:1 /nr:false`
  - 結果: 18 passed。
- Build:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false`
  - 結果: success、0 warnings、0 errors。
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`
  - 結果: success、0 warnings、0 errors。
- Diff check:
  - `git diff --check`
  - 結果: success。

## リスク

- `ExternalTrackerSnapshotStore` は manager update event に追従する read-side cache であり、timeout removal の mirror は今回未実装。既存 live comparison と同じく latest source の表示維持を優先し、R006 の blocker にはしていない。
- DebugHost は RUNTIME-HOST-005 時点の adapter として `VisionReceiverService` から Core coordinator を呼ぶ構造を維持している。R006 では Web render tick が operation loop を駆動しないことだけを固定し、RuntimeHost headless normal path は R008/R009 に残す。
