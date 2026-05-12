# Sub-agent実行レポート

## タスク

`TRACKER-048` として `Tracker.CaptureReplay` の比較表示・出力へ tracker snapshot を接続する。

## sub-agentを使う理由

親エージェントは実装・調査・テスト実行を直接行わず、サブエージェントのレポートを読んで判断するため。

## 対象範囲

- `Tracker.CaptureReplay` で metadata relative path から tracker snapshot sidecar を読み、比較情報を出力できるようにする
- source role / label、tracked timestamp、ball / robot count、raw payload restored、nearest timestamp summary を確認できる focused test を先に追加する
- 既存 capture / diagnostics / render snapshot 表示を壊さない
- focused / related / 必要な full test を実行し、結果を記録する

## 対象外

- diagnostics playback UI の本格接続
- socket abstraction / DI startup test / invalid raw payload direct append hardening
- `TRACKER-049` の README / 運用ドキュメント
- `TRACKER-050` の PR ready 化
- 固定一覧の変更
- commit / push

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/implementation-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/review-enforcer/references/source-documentation-policy.md`
- `sed -n '1,220p' reports/tracker-048-capture-replay-comparison-implementation-20260512155553.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tasks-status.md`
- `sed -n '1,220p' Tracker/Tracker.Core/Design/phases-status.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `sed -n '1,220p' reports/tracker-047-review-fix-implementation-20260512152742.md`
- `sed -n '1,220p' reports/tracker-047-review-r2-20260512153751.md`
- `git status --short`
- `rg --files Tracker/Tracker.CaptureReplay Tracker/Tracker.Tests | rg 'CaptureReplay|TrackerReplayIntegration|Snapshot|Diagnostics'`
- `rg -n "TrackerSnapshotReplayReader|SnapshotReplay|tracker snapshot|render snapshot|diagnostics|metadata|relative|Replay" Tracker/Tracker.CaptureReplay Tracker/Tracker.Tests -g '*.cs'`
- `sed -n '1,260p' Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs`
- `sed -n '1,260p' Tracker/Tracker.CaptureReplay/ReplaySummary.cs`
- `sed -n '1,340p' Tracker/Tracker.CaptureReplay/Program.cs`
- `sed -n '1,340p' Tracker/Tracker.CaptureReplay/ReplayOptions.cs`
- `sed -n '1,280p' Tracker/Tracker.Tests/CaptureReplayTests.cs`
- `sed -n '1,360p' Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter CaptureReplayTests --no-restore -m:1 /nr:false`
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" NUGET_HTTP_CACHE_PATH="$PWD/.codex-nuget-http-cache" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter CaptureReplayTests --no-restore -m:1 /nr:false`
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" NUGET_HTTP_CACHE_PATH="$PWD/.codex-nuget-http-cache" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~CaptureReplayTests|FullyQualifiedName~TrackerReplayIntegrationTddTests|FullyQualifiedName~TrackerCaptureOnSessionSnapshotContractTests|FullyQualifiedName~TrackerComparisonSourceTddTests|FullyQualifiedName~TrackerDiagnosticsLogReaderTests|FullyQualifiedName~TrackerRenderSnapshotLogReaderTests|FullyQualifiedName~DiagnosticsPlaybackStateTests" -m:1 /nr:false`
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" NUGET_HTTP_CACHE_PATH="$PWD/.codex-nuget-http-cache" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 /nr:false`
- `git diff --check`
- `git diff --name-status`
- `git diff --stat`
- `git status --short`

## 対象ファイル

- 変更:
  - `Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs`
  - `Tracker/Tracker.CaptureReplay/Program.cs`
  - `Tracker/Tracker.CaptureReplay/ReplaySummary.cs`
  - `Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj`
  - `Tracker/Tracker.Tests/CaptureReplayTests.cs`
  - `reports/tracker-048-capture-replay-comparison-implementation-20260512155553.md`
- 確認:
  - `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogReader.cs`
  - `Tracker/Tracker.Tests/TrackerReplayIntegrationTddTests.cs`
  - `Tracker/Tracker.Core/Design/tasks-status.md`
  - `Tracker/Tracker.Core/Design/phases-status.md`
  - `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `reports/tracker-047-review-fix-implementation-20260512152742.md`
  - `reports/tracker-047-review-r2-20260512153751.md`

## 指摘事項

- TDD failure evidence:
  - `Run_WithMetadataSnapshotSidecar_ReturnsTrackerSnapshotComparisonLines` と `Run_WithLegacyMetadataWithoutSnapshotSidecar_KeepsExistingReplaySummary` を先に追加し、実装前 focused test で compile failure を確認した。`CaptureReplayRunner.Run` に `metadataPath` がなく、`ReplaySummary` に `TrackerSnapshotLines` がないため、metadata relative path から snapshot sidecar 比較情報を返す経路が存在しないことを確認した。
  - 初回実装後、test metadata を settings file として読ませたため `TrackerSettingsFactory` が profile 不在で 2 failed になった。test は tracker settings と metadata path の責務を分け、production は `metadataPath` を独立 optional input として扱う形に修正した。
- 新規 review / blocking findings:
  - なし。後続 review sub-agent で確認する。

## 結果

- 実装修正:
  - `Tracker.CaptureReplay` から既存 `TrackerSnapshotReplayReader` を使えるよう `Tracker.Server` project reference を追加した。
  - `CaptureReplayRunner.Run` に optional `metadataPath` を追加し、既存呼び出しは互換維持したまま、metadata がある場合だけ `TrackerSnapshotReplayReader.ReadSession` の `SnapshotInputs` / `ComparisonSummaries` を `ReplaySummary.TrackerSnapshotLines` に変換するようにした。
  - CLI `Program.cs` は `options.SettingsPath` を metadata 候補として渡し、`trackerSnapshot ...` と `trackerComparison ...` の key=value 行を既存 summary/detail 出力に続けて標準出力へ出す。
  - 出力行には source label / role、tracked frame / timestamp、ball / robot count、raw payload restored、nearest timestamp summary を含めた。timestamp matching は `TRACKER-047` の reader 実装を使うため、`receivedAt` fallback には戻していない。
  - metadata に snapshot sidecar がない既存 capture / diagnostics / render snapshot 形状では `TrackerSnapshotLines` を空にし、既存 replay summary を維持する regression test を追加した。
- 検証:
  - 実装前 focused: compile failed。`Run` の `metadataPath` parameter と `ReplaySummary.TrackerSnapshotLines` が未実装であることを確認済み。
  - 実装後 focused: `CaptureReplayTests` 8 passed / 0 failed / 0 skipped。
  - 関連 focused: `CaptureReplayTests|TrackerReplayIntegrationTddTests|TrackerCaptureOnSessionSnapshotContractTests|TrackerComparisonSourceTddTests|TrackerDiagnosticsLogReaderTests|TrackerRenderSnapshotLogReaderTests|DiagnosticsPlaybackStateTests` 47 passed / 0 failed / 0 skipped。
  - full: `Tracker.Tests` 194 passed / 0 failed / 0 skipped。
  - `git diff --check`: 問題なし。

## リスク

- `Tracker.CaptureReplay` から `Tracker.Server` を参照して既存 `TrackerSnapshotReplayReader` を再利用した。reader を重複実装しないための最小経路だが、CLI project が Server assembly に依存する構成変更は後続 review で妥当性確認が必要。
- `Program.cs` は `--settings` path を metadata 候補にも使う。通常の appsettings では snapshot sidecar がなければ追加行なしになる。Capture metadata に tracker settings が入っていない手書き metadata を `--settings` として渡す場合は、従来どおり settings 解決で失敗する。
- diagnostics playback UI の本格接続、socket abstraction / DI startup test / invalid raw payload direct append hardening、README / PR body / commit / push は対象外として未実施。
- test 実行時に NuGet vulnerability data の read-only cache warning が出た。`DOTNET_CLI_HOME` / `NUGET_PACKAGES` / `NUGET_HTTP_CACHE_PATH` は project-local を指定し、focused / related / full test はすべて成功した。
