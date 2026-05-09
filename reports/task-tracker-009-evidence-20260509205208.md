# Sub-agent実行レポート

## タスク

- 目的: `TRACKER-009` の verification evidence を取得し、ball tracking / camera 間 merge / primary ball 選定 / secondary stable sort の実装が temporal contract suite を通過することを記録する
- タスク種別: evidence

## sub-agentを使う理由

- 理由: `tdd-executor` と `codex-delegation-executor` により test execution を証跡として残す verification は sub-agent 経由で実行する必要があるため

## 対象範囲

- 対象: `TRACKER-009` の task-scope 差分、および `TrackerEngineTemporalContractTests` の ball tracking 関連ケース

## 対象外

- 対象外: `.gitignore` の既存変更、`TRACKER-010` 以降の未実装 task、legacy report 未追跡ファイル

## 実行コマンド

- 実行コマンド: `git diff -- Tracker/Tracker.Core/TrackerExecutionContracts.cs Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs Tracker/Tracker.Core/Design/tasks-status.md Tracker/Tracker.Core/Design/phases-status.md`
- 実行コマンド: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerEngineTemporalContractTests"`
- 実行コマンド: `rg -n "Update_MergesSameBallAcrossCamerasIntoSingleTrackedBall|Update_SelectsPrimaryBallByVisibilityAndStableSortsSecondaryBalls|Update_TracksBallVelocityAcrossFrames|Update_KeepsBallTrackAliveAcrossOneMissingFrameWithDecayedVisibility|PrimaryBallTrackId|TrackedBallComparer|CollectMergedBallStates|UpdateCameraBallTrackStates" Tracker/Tracker.Core/TrackerExecutionContracts.cs Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`

## 対象ファイル

- 変更または確認したファイル: `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- 変更または確認したファイル: `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
- 変更または確認したファイル: `Tracker/Tracker.Core/Design/tasks-status.md`
- 変更または確認したファイル: `Tracker/Tracker.Core/Design/phases-status.md`
- 変更または確認したファイル: `reports/task-tracker-009-evidence-20260509205208.md`

## 指摘事項

- 指摘なし: `TrackerEngineTemporalContractTests` の対象 24 件はすべて成功し、ball tracking / camera 間 merge / primary ball 選定 / secondary stable sort / 速度更新 / 1 フレーム欠測時の visibility 減衰に関する退行は確認されなかった。

## 結果

- 結果: `git diff` で `TrackerExecutionContracts.cs` に camera-local ball track state 更新、`CollectMergedBallStates` による camera 間 merge、`TrackedBallComparer` による visibility 優先の stable sort、`PrimaryBallTrackId = balls[0].InternalTrackId` の primary ball 選定が入っていることを確認した。
- 結果: `TrackerEngineTemporalContractTests.cs` では既存の weighted merge 検証に加え、cross-camera merge の単一ボール化、primary/secondary ball 順序、ball velocity、1 フレーム欠測時の track keep-alive を直接検証するケースが追加されていることを確認した。
- 結果: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerEngineTemporalContractTests"` は `Passed: 24, Failed: 0, Skipped: 0` で完了し、対象 temporal contract suite は通過した。
- 結果: `tasks-status.md` と `phases-status.md` は current task を `TRACKER-009` に進める差分のみで、今回の evidence scope と整合していた。

## リスク

- 未解決のリスクまたは後続対応: 今回の証跡は `TrackerEngineTemporalContractTests` に限定されるため、`TRACKER-010` 以降の機能、より広い integration 経路、長時間連続入力での ball tracking 安定性は別 task / 別 suite で継続確認が必要。
