# Sub-agent実行レポート

## タスク

- 目的: PR #19 の `Tracker.CaptureReplay` 遅延分析 tooling を code review する。
- タスク種別: review

## sub-agentを使う理由

- 理由: `CAPTURE-REPLAY-001` の dedicated review gate として、親実装者とは別視点で正常系・回帰・test 妥当性を確認するため。

## 対象範囲

- 対象: `Tracker/Tracker.CaptureReplay` の session folder 解決、latency analysis、CLI option、summary 出力、`CaptureReplayTests` の該当差分、`reports/capture-replay-001-latency-investigation-20260516185833.md`。

## 対象外

- 対象外: `Tracker.RuntimeHost` の CLI profile 指定、`Tracker.RuntimeHost/appsettings.json`、PR 作成操作、レビュー結果に基づく修正実装。

## 実行コマンド

- 実行コマンド:
  - `git diff --unified=80 main...HEAD -- Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs Tracker/Tracker.CaptureReplay/Program.cs Tracker/Tracker.CaptureReplay/ReplayInputPathResolver.cs Tracker/Tracker.CaptureReplay/ReplayLatencyAnalyzer.cs Tracker/Tracker.CaptureReplay/ReplayOptions.cs Tracker/Tracker.CaptureReplay/ReplaySummary.cs Tracker/Tracker.CaptureReplay/README.md Tracker/Tracker.Tests/CaptureReplayTests.cs reports/capture-replay-001-latency-investigation-20260516185833.md`
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~CaptureReplayTests -m:1 /nr:false`
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- --help`
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- --capture /home/ibis/ssl/IbisDuck/Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260514T132706328Z-4a19e61b92c443929f91eccdff3512c9 --analyze-latency --max-latency-frames 8 --skip-tracker-snapshots --max-details 0`
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- --capture /home/ibis/ssl/IbisDuck/Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260514T132706328Z-4a19e61b92c443929f91eccdff3512c9 --analyze-latency --max-latency-frames 4 --skip-tracker-snapshots --max-details 0 --reorder-window-ns 0`

## 対象ファイル

- 変更または確認したファイル:
  - `Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs`
  - `Tracker/Tracker.CaptureReplay/Program.cs`
  - `Tracker/Tracker.CaptureReplay/ReplayInputPathResolver.cs`
  - `Tracker/Tracker.CaptureReplay/ReplayLatencyAnalyzer.cs`
  - `Tracker/Tracker.CaptureReplay/ReplayOptions.cs`
  - `Tracker/Tracker.CaptureReplay/ReplaySummary.cs`
  - `Tracker/Tracker.CaptureReplay/README.md`
  - `Tracker/Tracker.Tests/CaptureReplayTests.cs`
  - `reports/capture-replay-001-latency-investigation-20260516185833.md`
  - `Tracker/Tracker.Core/Model/TrackerFrame.cs`
  - `Tracker/Tracker.Core/Engine/TrackerEngine/DetectionBuffer.cs`
  - `Tracker/Tracker.Core/Engine/TrackerEngine/FrameCommit.cs`
  - `Tracker/Tracker.DebugHost/Tracking/TrackerSnapshotReplayReader.cs`
  - `Tracker/Tracker.DebugHost/Components/Pages/DiagnosticsRawGeometryLoader.cs`

## 指摘事項

- 指摘要約または「指摘なし」:
  - `non-blocking concern`: `Tracker/Tracker.CaptureReplay/README.md:25-26,71` と `reports/capture-replay-001-latency-investigation-20260516185833.md:15-16` は `latencySummary` / `commit lag` を capture-time ベースの指標として説明していますが、実装側の `Tracker/Tracker.CaptureReplay/ReplayLatencyAnalyzer.cs:29-45,50-92,114-138` が実際に使っているのは `record.ReceivedAt` と `commitReceivedAt` です。今回の CLI は「raw vision がいつ見え、tracker commit がいつ出るか」の wall-clock 差分を見る用途には合っていますが、説明文のままだと event timestamp 差分を測っているように読めるため、後続の調査や report 引用で誤解を招きます。
  - `blocking normal-path problem`: なし。`ReplayInputPathResolver` の session folder 解決は実 capture でも動作し、`--skip-tracker-snapshots` / `--max-latency-frames` / CLI help / `CaptureReplayTests` の reviewed scope に blocker は見当たりませんでした。
  - `user-confirmation-required capability gap`: なし。

## 結果

- 結果:
  - CaptureReplay scope の dedicated review を実施し、blocking finding はありませんでした。release 可否の観点では通せますが、latency metric の説明文だけは実装の時刻軸に合わせて補正した方が安全です。

## リスク

- 未解決のリスクまたは後続対応:
  - README と調査レポートが現状のままだと、`avgCommitLagMs` / `maxCommitLagMs` を capture-time ベースの delay と誤読したまま議論が進むリスクがあります。
  - focused test は 11 件すべて通過し、実 capture でも report 記載の数値を再現できましたが、レビューでは duplicate raw detection packet を含む capture までは追加検証していません。
