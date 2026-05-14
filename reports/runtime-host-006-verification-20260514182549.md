# RUNTIME-HOST-006 検証レポート

## タスク

DebugHost live display を read-side snapshot 境界へ寄せ、Raw / Tracked / Compare 表示が同一 UI render tick の fixed snapshot から派生することを確認した。

## 検証対象

- `Tracker/Tracker.DebugHost/Vision/VisionLiveDisplaySnapshotProvider.cs`
- `Tracker/Tracker.DebugHost/Tracking/ExternalTrackerSnapshotStore.cs`
- `Tracker/Tracker.DebugHost/Vision/VisionLiveComparisonViewState.cs`
- `Tracker/Tracker.DebugHost/Components/Pages/Home.razor`
- `Tracker/Tracker.Tests/RuntimeHostDebugHostReadSideSnapshotBoundaryTests.cs`
- `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`

## 実行コマンド

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostDebugHostReadSideSnapshotBoundaryTests|FullyQualifiedName~VisionLiveComparisonViewStateTests|FullyQualifiedName~TrackedVisionViewStateTests" -m:1 /nr:false
```

結果: 成功。18 passed / 0 failed / 0 skipped。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false
```

結果: 成功。0 warnings / 0 errors。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false
```

結果: 成功。0 warnings / 0 errors。

```bash
git diff --check
```

結果: 成功。出力なし。

## 確認結果

- `Home.razor` は `VisionPacketStore` / `TrackedSnapshotStore` を直接 inject せず、`VisionLiveDisplaySnapshotProvider` から live display snapshot を取得する。
- `VisionLiveDisplaySnapshotProvider` は 1 render tick で raw / tracked / external tracker read-side snapshot を固定し、comparison snapshot を同じ tick id で生成する。
- `VisionLiveComparisonSnapshotComposer` は store を再読取せず、固定済み snapshot から comparison source / layer / details を生成する。
- `ExternalTrackerSnapshotStore` は `MultiTrackerManager` の update event から packet と metadata を clone 済み DTO として保持し、render path が mutable manager state を直接読まない。
- live display / comparison source は `TrackerCoordinator`、`ITrackerEngine`、`TrackerPacketGenerator`、`ITrackerPacketPublisher`、`ProcessPacket(` を参照しない。

## リスク

- `ExternalTrackerSnapshotStore` は timeout removal を mirror しない。既存 live comparison と同じく latest source 表示維持を優先しており、RUNTIME-HOST-006 の blocker ではない。
- diagnostics sample sidecar は未実装であり、RUNTIME-HOST-007 の対象として残る。
