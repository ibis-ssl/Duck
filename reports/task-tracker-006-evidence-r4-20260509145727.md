# Sub-agent実行レポート

## タスク

- 目的: `TRACKER-006` final follow-up 後の verification evidence を取得し、group-close flush と late-packet cutoff 修正を含む reorder buffer / flush pipeline 実装が task-scope の contract tests を通過することを記録する
- タスク種別: evidence

## sub-agentを使う理由

- 理由: `tdd-executor` により test execution を証跡として残す verification は sub-agent 経由で実行する必要があるため

## 対象範囲

- 対象: `TRACKER-006` final follow-up 差分、および `TrackerEngineTemporalContractTests` の対象 9 ケース

## 対象外

- 対象外: `TRACKER-007` 以降の未実装 contract、未関連の `.gitignore` 変更、legacy review reports の未追跡ファイル

## 実行コマンド

- 実行コマンド: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerEngineTemporalContractTests.Update_FlushesBufferedDetectionsInEventTimeOrder_WhenArrivalOrderDiffers|FullyQualifiedName~TrackerEngineTemporalContractTests.Update_SplitsFrames_WhenObservationsExceedMergeWindow|FullyQualifiedName~TrackerEngineTemporalContractTests.Update_CanReturnZeroFramesWhileBuffering_AndMultipleFramesWhenSeveralGroupsFlush|FullyQualifiedName~TrackerEngineTemporalContractTests.Update_DropsLatePacketsAndDoesNotLetThemContaminateLaterFlushes|FullyQualifiedName~TrackerEngineTemporalContractTests.Update_EmitsWorldFrameCommittedForEachCommittedFrameInFlushOrder|FullyQualifiedName~TrackerEngineTemporalContractTests.Update_UsesSentTimeWhenCaptureTimeIsMissing|FullyQualifiedName~TrackerEngineTemporalContractTests.Update_DropsLatePacketsThatFallInsideAnAlreadyCommittedMergeWindow|FullyQualifiedName~TrackerEngineTemporalContractTests.Update_WaitsForTheOldestGroupMergeWindowToCloseBeforeFlushingIt|FullyQualifiedName~TrackerEngineTemporalContractTests.Update_PopulatesProcessedAtNsFromLocalProcessingTime"`

## 対象ファイル

- 変更または確認したファイル: `Tracker/Tracker.Core/TrackerExecutionContracts.cs`, `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`, `reports/task-tracker-006-evidence-r4-20260509145727.md`

## 指摘事項

- 指摘要約または「指摘なし」: 指摘なし。`TrackerExecutionContracts.cs` では reorder buffer / merge-window group flush / late-packet cutoff / `WorldFrameCommitted` event emission / `TCapture` 欠落時の `TSent` fallback / `ProcessedAtNs` 採番を実装していることを確認した。`TrackerEngineTemporalContractTests.cs` では対象 9 ケースがその挙動を直接検証していることを確認した。

## 結果

- 結果: 対象 9 テストを実行し、`Failed: 0, Passed: 9, Skipped: 0, Total: 9` で全件成功した。確認した final follow-up の範囲では、group-close flush と late-packet cutoff 修正を含む temporal contract は成立している。

## リスク

- 未解決のリスクまたは後続対応: 今回の証跡は指定された 9 件の contract tests に限定されるため、geometry reset・profile switch・より広い統合経路との相互作用は未再検証である。また `ProcessedAtNs` はローカル時刻由来で、実装上はミリ秒精度をナノ秒換算しているため、より厳密な時刻精度要件が将来必要になった場合は追加検証が必要。
