# Sub-agent実行レポート

## タスク

`/diagnostics` で render snapshot を読み込み、raw / tracked の field 描画を追加した差分をレビューする。

## sub-agentを使う理由

`review-enforcer` に従い、完了判定前のレビューは parent ではなく専用 sub-agent で実施するため。

## 対象範囲

- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs`
- `Tracker/Tracker.Server/Program.cs`
- `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`
- `Tracker/Tracker.Server/README.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`

## 対象外

- `SslProto/src/external/ssl-game-controller` の submodule 状態
- `Tracker/Tracker.Server/appsettings.json` の既存ローカル変更
- ball 分裂アルゴリズム本体の tuning / 修正
- CaptureReplay CLI 本体の再レビュー

## 実行コマンド

- `git status --short`
- `git diff -- Tracker/Tracker.Server/Components/Pages/Diagnostics.razor Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs Tracker/Tracker.Server/Program.cs Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs Tracker/Tracker.Server/README.md Tracker/Tracker.Core/Design/tracker-architecture-plan.md reports/topic-tracker-diagnostics-render-snapshot-review-20260510193226.md`
- `nl -ba reports/topic-tracker-diagnostics-render-snapshot-review-20260510193226.md | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs | sed -n '1,320p'`
- `rg -n "class TrackerDiagnosticsLogReader|record TrackerDiagnosticsLog|TrackedFrame|render-snapshots|TrackerFrame" Tracker/Tracker.Server Tracker/Tracker.Core Tracker/Tracker.Tests -g'*.cs'`
- `rg -n "class TrackedVisionViewState|record TrackedVisionViewState|VisionFieldCanvas|TrackedSnapshot|FromSnapshot|record TrackedSnapshot" Tracker/Tracker.Server -g'*.cs' -g'*.razor'`
- `nl -ba Tracker/Tracker.Server/Components/Vision/TrackedVisionViewState.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs | sed -n '1,140p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor | sed -n '1,340p'`
- `nl -ba Tracker/Tracker.Server/Vision/VisionPacketCaptureFile.cs | sed -n '1,140p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs | sed -n '246,382p;640,690p'`
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter TrackerRenderSnapshotLogReaderTests -m:1 /nr:false`

## 対象ファイル

- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs`
- `Tracker/Tracker.Server/Program.cs`
- `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`
- `Tracker/Tracker.Server/README.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `reports/topic-tracker-diagnostics-render-snapshot-review-20260510193226.md`
- 参照確認: `Tracker/Tracker.Server/Components/Vision/TrackedVisionViewState.cs`
- 参照確認: `Tracker/Tracker.Server/Components/Vision/VisionFieldCanvas.razor`
- 参照確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs`
- 参照確認: `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs`
- 参照確認: `Tracker/Tracker.Server/Vision/VisionPacketCaptureFile.cs`
- 参照確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`

## 指摘事項

- [Medium] `TrackerRenderSnapshotLogReader.ReadFrame` が render snapshot sidecar の読み込み例外を UI 用エラーに変換していないため、sidecar が存在するが gzip/json/schema として読めない場合に `/diagnostics` 全体が例外で落ちる可能性があります。`ReadFrame` は `ReadRecords(renderSnapshotPath)` をそのまま列挙しており（`Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs:49`）、`ReadRecords` は gzip 展開、JSON deserialize、schema version 検証で例外を投げます（`Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs:64`、`Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs:76`、`Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs:80`）。呼び出し側の `Diagnostics.razor` も catch せず `result.Error` を表示する前提です（`Tracker/Tracker.Server/Components/Pages/Diagnostics.razor:270`）。一方で capture writer は gzip sidecar を `FileShare.Read` で開くため、capture 中や不完全な sidecar でも viewer 側から読めます（`Tracker/Tracker.Server/Vision/VisionPacketCaptureFile.cs:33`）。この状態では render snapshot が無い通常ログは使えますが、同 basename の不完全・破損 sidecar がある通常 diagnostics log では、非致命的な alert ではなくページ障害になります。`ReadFrame` 側で `IOException` / `InvalidDataException` / `JsonException` などを捕捉して `TrackerRenderSnapshotLogResult` の `Error` に落とす、または UI 側で snapshot 読み込みだけを隔離する必要があります。

## 結果

指摘 1 件です。`TrackerRenderSnapshotLogReaderTests` は 2 件 pass しましたが、上記の例外隔離ケースは現状テストされていません。

basename 解決は `*.tracker-diagnostics.log` から同じ basename の `*.render-snapshots.jsonl.gz` へ限定され、選択中 diagnostics log が `TrackerDiagnosticsLogReader.ListFiles()` に含まれることも確認しているため、任意パス指定で別ファイルを読みに行く問題は確認できませんでした。

Raw Field / Tracked Field は既存の `VisionFieldCanvas` と `TrackedVisionViewState.FromSnapshot` のデータ形状に沿っており、raw は `SourceDetections`、tracked は `TrackerFrame.Balls` / `TrackerFrame.Robots` から描画されます。実装、README、design 更新の記述に大きな矛盾は確認できませんでした。

## リスク

- ブラウザ上の実描画確認、Playwright/screenshot 検証、全体テストはこの review sub-agent では実行していません。
- 既存ローカル変更と説明されている `Tracker/Tracker.Server/appsettings.json` 起因の全体テスト失敗は対象外として再検証していません。
- render snapshot sidecar が読み込み可能な正常 gzip である前提では動作しますが、capture 中・破損・schema mismatch 時の UI 隔離は未解決です。
