# Sub-agent実行レポート

## タスク

- 目的: diagnostics replay / comparison が Vision、ibis tracker、3rd party tracker を同じ replay timeline tick で同期できているか監査し、ずれている場合に今回修正すべき範囲を特定する。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: ユーザー指示により、調査は gpt-5.5 high の sub-agent に委譲し、親はマネージャーとして report を裁定する。

## 対象範囲

- 対象: diagnostics replay timeline、alignment sidecar、comparison reader / view-state、Field source / overlay、Vision / ibis tracker / 3rd party tracker の選択ロジック、関連テスト。

## 対象外

- 対象外: コード変更、テスト作成、設計書編集、PR本文更新、仕様決定。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `sed -n '1,220p' reports/issue-10-diagnostics-time-sync-audit-20260514081730.md`
  - `sed -n '1,260p' reports/issue-10-live-same-tick-investigation-20260514081135.md`
  - `rg -n "RAW-VISION-013|RAW-VISION-014|RAW-VISION-015|time-sync|time sync|diagnostics replay|comparison" Tracker/Tracker.Server/Design Tracker/Tracker.Core/Design reports -S`
  - `git status --short`
  - `rg -n "selectedReplayTimelineTick|SelectedReplayTimelineTick|ReplayTimeline|replayTimeline|alignment|Alignment|FieldSource|TrackerDiagnosticsFieldSource|Comparison|comparison|nearest|Nearest|saved-session-alignment|legacy-nearest|latest-before|LatestBefore|SourceKey" Tracker/Tracker.Server/Tracking Tracker/Tracker.Server/Components/Pages Tracker/Tracker.Tests -S`
  - `rg --files Tracker/Tracker.Server/Tracking Tracker/Tracker.Server/Components/Pages Tracker/Tracker.Tests | rg "(Diagnostics|Comparison|ReplayTimeline|Alignment|FieldSource|Playback|CaptureReplay|Snapshot)"`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '260,620p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '620,1040p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '1040,1220p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsReplayTimelineIndex.cs | sed -n '1,280p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '255,560p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '560,760p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '760,980p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '980,1015p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs | sed -n '1,280p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs | sed -n '280,620p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor | sed -n '1,360p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor | sed -n '360,520p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs | sed -n '1,280p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogReader.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs | sed -n '180,380p'`
  - `nl -ba Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs | sed -n '380,620p'`
  - `nl -ba Tracker/Tracker.Tests/TrackerDiagnosticsReplayTimelineIndexTests.cs | sed -n '1,280p'`
  - `nl -ba Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs | sed -n '1,260p'`
  - `rg -n "SelectedReplayTimeline|selectedReplayTimeline|ReplayTimelineSelection|LoadReplayTimeline|SelectTimeline|ToReplayTimelineSelection|RenderFrameNumber|GetPlaybackTimelineTimestamps|GetNextIndex" Tracker/Tracker.Tests Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs Tracker/Tracker.Server/Tracking -S`
  - `nl -ba Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs | sed -n '260,360p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs | sed -n '258,360p'`
  - `nl -ba Tracker/Tracker.Tests/TrackerCaptureOnSessionSnapshotContractTests.cs | sed -n '200,340p'`
  - `nl -ba Tracker/Tracker.Tests/TrackerCaptureOnSessionSnapshotContractTests.cs | sed -n '322,420p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '120,210p'`
  - `nl -ba Tracker/Tracker.Server/Design/tasks-status.md | sed -n '1,110p'`
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests|FullyQualifiedName~TrackerDiagnosticsReplayTimelineIndexTests|FullyQualifiedName~DiagnosticsPlaybackStateTests|FullyQualifiedName~TrackerCaptureOnSessionSnapshotContractTests" --no-restore -m:1 /nr:false`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/issue-10-diagnostics-time-sync-audit-20260514081730.md`
  - 確認: `reports/issue-10-live-same-tick-investigation-20260514081135.md`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsReplayTimelineIndex.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogReader.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs`
  - 確認: `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
  - 確認: `Tracker/Tracker.Tests/TrackerDiagnosticsReplayTimelineIndexTests.cs`
  - 確認: `Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
  - 確認: `Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs`
  - 確認: `Tracker/Tracker.Tests/TrackerCaptureOnSessionSnapshotContractTests.cs`
  - 確認: `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - 確認: `Tracker/Tracker.Server/Design/tasks-status.md`
  - 非対象維持: `Tracker/Tracker.Server/appsettings.json` の既存差分は触っていない。

## 指摘事項

- 指摘要約または「指摘なし」:
  - Blocking time-sync gap: 限定的にあり。`selectedReplayTimeline` が渡されているにもかかわらず、選択 tick に対象 3rd party source の alignment record がない場合、`TrackerDiagnosticsComparisonViewStateReader.CreateSelectedEntryComparison` は timeline alignment miss の後に selected diagnostics entry / diagnostics line alignment / nearest timestamp fallback へ進む。Field source も `CreateFieldSourceFrame` で同じ fallback へ進む。これにより、selected replay timeline tick では存在しない future/later の 3rd party snapshot を同じ diagnostics line または nearest timestamp から表示できる。
  - Normal-path evidence: 新規 capture で alignment sidecar があり、選択 tick に対象 source の alignment record がある場合は、comparison と Field source は同じ `selectedReplayTimeline.ReplayTimelineIndex` から `saved-session-alignment` を選ぶ。Vision/Input と ibis tracker は同じ selected tick の `RenderFrameNumber` で `selectedRenderSnapshot` を引き、3rd party tracker は同じ tick の alignment record を引く。
  - 3rd party nearest timestamp scope: `nearest-timestamp` は legacy/no-alignment または timeline alignment miss の fallback として残っている。設計では alignment sidecar ready の新規 capture は selected replay timeline tick + Field source key で保存済み alignment を使うため、timeline selection 中の miss を nearest に落とす挙動は RAW-VISION-014/015 で固定・修正対象にするべき。
  - ユーザー確認が必要な点: 「source がまだ存在しない tick」で External/source label を選んだ場合、Field/comparison を `CandidateMissing` / `NoCandidateSnapshot` として空表示にする方針でよいか。時間同期を優先するならこれが妥当で、future snapshot を出すより安全。

## 結果

- 結果:
  - 判定: 新規 capture の通常経路は概ね同期できているが、selected replay timeline tick に対象 source record がない場合の fallback が残っているため、「全 tick / 全 source selector で必ず同じ replay timeline tick」とはまだ言い切れない。
  - 根拠:
    - `Diagnostics.razor.cs` は `selectedReplayTimelineTick` を保持し、scrub / wheel / playback で `SelectReplayTimelineTick` を通して `selectedEntry`、`selectedRenderSnapshot`、comparison state を同期する。`ToReplayTimelineSelection()` が `TrackerDiagnosticsComparisonUiState.Load` と source selector 変更へ渡される。
    - `LoadSelectedRenderSnapshot()` は `selectedReplayTimelineTick?.RenderFrameNumber` を優先して render snapshot を引くため、Vision Input と ibis tracker Field は selected replay timeline tick の render frame に揃う。
    - `TrackerDiagnosticsComparisonUiState.RefreshFieldSourceFrames()` は comparison と左右 Field source frame の両方へ同じ `selectedReplayTimeline` を渡す。
    - `TrackerDiagnosticsComparisonViewStateReader.CreateSelectedEntryComparison()` と `CreateFieldSourceFrame()` は、`selectedReplayTimeline` がある場合にまず `FindAlignedTimelineCandidate` / `FindAlignedTimelineFieldSourceCandidate` を使い、成功時は `saved-session-alignment` を返す。
    - `TrackerDiagnosticsReplayTimelineIndex.Build()` は alignment v2 record を `ReplayTimelineIndex` でまとめ、`ReceivedAt` 順 timeline と render latest-before fallback を作る。3rd party の `TrackedFrame.timestamp` ではなく capture-time `ReceivedAt` が timeline 軸になっている。
    - `TrackerSnapshotAlignmentLogWriter` は render snapshot、diagnostics entry、tracker snapshot tick ごとに `GetLatestSnapshotsBySource()` から source ごとの latest snapshot を保存し、alignment v2 record に `ReplayTimelineIndex`、`ReplayTimelineReceivedAt`、`RenderFrameNumber`、`TrackerSnapshotRecordIndex`、`ReceivedAtDeltaTicks` を記録する。
    - focused test は 81 件 pass。`TrackerDiagnosticsComparisonViewStateTests` は selected replay timeline から comparison と Field source が同じ timeline record を使うこと、fast tracker tick で source ごとの latest-before record を使うこと、cache 再利用を確認している。`TrackerDiagnosticsReplayTimelineIndexTests` は fast tracker cadence と render latest-before hold を確認している。`DiagnosticsPlaybackStateTests` は FastForward が replay timeline tick を間引かないことを確認している。`TrackerCaptureOnSessionSnapshotContractTests` は alignment writer が fastest cadence と source ごとの latest snapshot を保存することを確認している。
  - 修正対象:
    - `TrackerDiagnosticsComparisonViewStateReader.CreateSelectedEntryComparison`: `selectedReplayTimeline` が非 null で timeline alignment candidate がない場合、diagnostics-line / nearest fallback に落とさず `NoCandidateSnapshot` 相当を返す。
    - `TrackerDiagnosticsComparisonViewStateReader.CreateFieldSourceFrame`: `selectedReplayTimeline` が非 null で selected source の timeline alignment candidate がない場合、diagnostics-line / nearest fallback に落とさず `CandidateMissing` 相当を返す。
    - 必要なら message / matching rule に `unsupported-alignment-missing` または `saved-session-alignment-missing` 相当を追加し、alignment ready だが selected source がその tick に存在しない状態を UI で区別する。
  - TDD 候補:
    - `TrackerDiagnosticsComparisonViewStateTests.Load_WithSelectedReplayTimeline_WhenSelectedSourceHasNoTimelineRecord_DoesNotFallbackToDiagnosticsLineOrNearest`: 同じ diagnostics line の later tick に external alignment record がある fixture で、earlier selected timeline tick の External/source label comparison が later external snapshot を返さないことを確認する。
    - `TrackerDiagnosticsComparisonViewStateTests.LoadFieldSourceFrame_WithSelectedReplayTimeline_WhenSelectedSourceHasNoTimelineRecord_ReturnsCandidateMissing`: 同条件で Field source が `CandidateMissing` になり、`MatchingRule` / `TrackedFrameNumber` / `SemanticSummary` を持たないことを確認する。
    - 追加で `TrackerDiagnosticsComparisonUiState` 経由の test を 1 件置き、source selector 変更時にも `selectedReplayTimeline` が維持され、fallback で別 tick を表示しないことを確認する。

## リスク

- 未解決のリスクまたは後続対応:
  - alignment sidecar ready の session でも、selected tick に存在しない source を選んだ場合の UI 表示仕様が未固定。future/later snapshot を表示しない方針を RAW-VISION-014 の failing test で先に固定する必要がある。
  - `TrackerDiagnosticsComparisonViewStateReader` の fallback は legacy capture を壊さないために必要だが、alignment ready + selected timeline selection の経路では適用範囲を狭めないと、time-sync 監査で見つけた gap が残る。
  - Vision/Input と ibis tracker は selected tick の render frame へ揃うが、先頭 tick で prior render がない場合は nearest-after fallback が設計上許容されている。これは UI に match rule を出すか、manual evidence で確認しないと「同時」と誤解される可能性がある。
  - 今回はコード変更・テスト追加・設計書編集をしていない。RAW-VISION-014/015 で上記 TDD と修正を入れるまで、全 source selector の厳密同期は未完了として扱うべき。
