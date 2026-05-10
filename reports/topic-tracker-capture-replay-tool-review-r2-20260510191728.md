# Sub-agent実行レポート

## タスク

PR #5 `feat/tracker-capture-replay-tool` のレビュー指摘対応後差分を再レビューする。

## sub-agentを使う理由

`review-enforcer` に従い、完了判定前のレビューは parent ではなく専用 sub-agent で実施するため。

## 対象範囲

- 前回レビュー `reports/topic-tracker-capture-replay-tool-review-20260510185110.md` の 3 指摘への対応差分
- `Tracker/Tracker.CaptureReplay/Program.cs`
- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Core/TrackerModelContracts.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `Tracker/Tracker.Tests/TrackerDiagnosticsLogReaderTests.cs`
- 関連 README / design document 更新

## 対象外

- `SslProto/src/external/ssl-game-controller` の submodule 状態
- ball 分裂アルゴリズム本体の tuning / 修正
- PR #5 全体の初回レビューや unrelated files の網羅レビュー

## 実行コマンド

- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `git status --short`
- `sed -n '1,260p' reports/topic-tracker-capture-replay-tool-review-r2-20260510191728.md`
- `sed -n '1,260p' reports/topic-tracker-capture-replay-tool-review-20260510185110.md`
- `git diff -- Tracker/Tracker.CaptureReplay/Program.cs Tracker/Tracker.Core/TrackerExecutionContracts.cs Tracker/Tracker.Core/TrackerModelContracts.cs Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs Tracker/Tracker.Server/Components/Pages/Diagnostics.razor Tracker/Tracker.Tests/TrackerDiagnosticsLogReaderTests.cs Tracker/Tracker.Core/Design/tracker-architecture-plan.md Tracker/Tracker.Server/README.md reports/topic-tracker-capture-replay-tool-review-r2-20260510191728.md`
- `nl -ba Tracker/Tracker.CaptureReplay/Program.cs | sed -n '1,240p'`
- `nl -ba Tracker/Tracker.CaptureReplay/Program.cs | sed -n '300,580p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs | sed -n '230,410p'`
- `rg -n "TrackerDiagnosticsLogReader|TrackerDiagnosticsOptions|Configure<TrackerDiagnosticsOptions>|AddSingleton.*Diagnostics|RuntimeOverrides|ResolvedTrackerOptions|appsettings" Tracker/Tracker.Server Tracker/Tracker.CaptureReplay Tracker/Tracker.Core Tracker/Tracker.Tests`
- `nl -ba Tracker/Tracker.Core/TrackerExecutionContracts.cs | sed -n '160,330p'`
- `nl -ba Tracker/Tracker.Core/TrackerExecutionContracts.cs | sed -n '760,850p'`
- `nl -ba Tracker/Tracker.Core/TrackerExecutionContracts.cs | sed -n '2000,2058p'`
- `nl -ba Tracker/Tracker.Server/Program.cs | sed -n '1,70p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerOptions.cs | sed -n '1,80p'`
- `nl -ba Tracker/Tracker.Tests/TrackerDiagnosticsLogReaderTests.cs | sed -n '1,130p'`
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerDiagnosticsLogReaderTests"`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor | sed -n '1,190p'`
- `rg -n "MapRazorComponents|UseAuthentication|UseAuthorization|Authorize|/diagnostics|@page" Tracker/Tracker.Server`
- `rg -n "ReadFile\\(|ListFiles\\(" Tracker`

## 対象ファイル

- `Tracker/Tracker.CaptureReplay/Program.cs`
- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Core/TrackerModelContracts.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `Tracker/Tracker.Tests/TrackerDiagnosticsLogReaderTests.cs`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Server/README.md`
- `reports/topic-tracker-capture-replay-tool-review-r2-20260510191728.md`

## 指摘事項

1. [Medium] `/diagnostics` のログ選択値を改変すると、許可済みログ一覧外の任意パスを `ReadFile` に読ませられます。
   - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor:150`
   - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor:176`
   - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs:50`
   - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs:52`
   - 今回の修正で `<option value>` が `FileName` から `FullPath` に変わり、`ReadFile` も `Path.GetFileName` による capture directory 固定をやめて root path をそのまま受け付けるようになっています。`LoadFiles()` は現在選択値が `ListFiles()` に含まれるかを検証しますが、`OnLogFileChanged` はイベント値をそのまま `selectedLogPath` に設定して `LoadSelectedFile()` を呼ぶため、ブラウザ側で select 値を改変できる利用者は `AppContext.BaseDirectory` 相対または absolute path の任意ファイルを読み取り対象にできます。ログ parser に一致する行しか表示されないものの、未認可のファイル存在確認や parse 可能なログ内容の漏えいになり得ます。`ReadFile` 側で `ListFiles()` の `FullPath` と照合する、または UI で選択値を一覧内の値に正規化してから読む必要があります。

前回レビューの 3 指摘については、確認範囲では再発する問題は見つかりませんでした。committed frame の `SourceDetections` を使った raw count / raw frame / raw camera 表示、`Tracker:RuntimeOverrides` の profile 設定への merge、capture sidecar / default `tracker-diagnostics-*.log` / configured `Tracker:Diagnostics:FilePath` の列挙は実装と README / design 更新が整合しています。

## 結果

前回レビュー指摘 3 件への対応差分は概ね妥当ですが、`/diagnostics` の full path 読み取り経路に新しい修正必須の指摘があります。

検証:
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerDiagnosticsLogReaderTests"` passed: 2 tests

## リスク

未解決リスク:
- `/diagnostics` の file selection / reader 境界で allowlist 検証がないため、修正前に PR を完了扱いにすると diagnostics page 利用者が一覧外パスを読ませられる可能性が残ります。
- CaptureReplay の `Tracker:RuntimeOverrides` 反映については parent 実行の end-to-end replay 結果を確認済み前提でレビューしました。この sub-agent では CaptureReplay 全体の再実行はしていません。
