# トラッカー test 保守性改善 詳細設計

## 目的

TRACKER-035 では、既存 test の意味を変えずに巨大 test file を責務別へ分割し、各 test が何を確認しているかを日本語 XML コメントで明示する。

この詳細設計は `Tracker.Tests` の test 保守性改善に限定する。Core engine、Server、CLI、UI の production code 分割方針は別の詳細設計で扱う。

## 現状

### 巨大 test file

- `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
  - 2,281 行、60 個の `[Fact]` を 1 class に保持している。
  - event-time buffer、geometry reset、profile switch、robot tracking、ball tracking、kick/contact、ball left field が同居している。
  - TRACKER-003 由来の時系列契約 test から、TRACKER-031 までの回帰 test が同じ class に追加され続けているため、変更箇所を探す負荷が高い。
- `Tracker/Tracker.Tests/TrackerCoordinatorTests.cs`
  - 613 行、10 個の `[Fact]` と `RecordingTrackerPacketPublisher` / `RecordingTrackerObserver` helper が同居している。
  - coordinator の snapshot/publish/event/profile/capture diagnostics が同じ class に並び、helper の責務境界が test 本体から見えにくい。
- `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`
  - 291 行、reader test と gzip/jsonl helper が同居している。
  - 巨大ではないが、render snapshot fixture を他の diagnostics reader test と共有できる形に分離すると、今後の追加 test が読みやすくなる。
- `Tracker/Tracker.Tests/TrackedVisionViewStateTests.cs`
  - 195 行、1 つの mapping test が多くの assertion を持つ。
  - 分割必須ではないが、comment 追加と fixture builder 化の対象にする。
- `Tracker/Tracker.Tests/VisionPacketCaptureTests.cs`
  - 226 行、capture 書き込み、replay、runtime toggle が同居している。
  - `VisionPacketCaptureSession` 作成 helper は残してよいが、comment 追加対象にする。
- その他の `Tracker/Tracker.Tests/*Tests.cs`
  - 多くは 50 から 223 行であり、TRACKER-035 では class 分割より comment 追加と小さな helper 整理を優先する。

### 既存 helper

- `Tracker/Tracker.Tests/Contracts/TrackerContractFixture.cs`
  - engine、packet generator、settings、profile switch request、frame/state 作成 helper を持つ。
  - test 分割後も正本の fixture として維持し、同種の factory を各 test class に再作成しない。
- `Tracker/Tracker.Tests/Contracts/TrackerContractTestData.cs`
  - raw SSL-Vision packet 作成の正本として維持する。
  - TRACKER-035 では packet 生成処理の意味を変えない。

## 分割方針

### 基本方針

- test の assertion、入力 packet、設定値、時刻、順序期待値は変更しない。
- 1 つの既存 `[Fact]` は原則 1 つの新 test method へそのまま移動する。
- method 名は原則維持し、同じ method 名が別 class に存在してもよい。
- namespace は既存と同じ `Tracker.Tests` を維持する。
- `TrackerContractFixture` と `TrackerContractTestData` を使い回し、分割のためだけに production code へ test 専用 API を追加しない。
- file-scoped helper は、2 class 以上で共有する場合だけ `Tracker.Tests/Contracts` または `Tracker.Tests/Support` 配下へ抽出する。

### `TrackerEngineTemporalContractTests.cs` の推奨分割

`TrackerEngineTemporalContractTests.cs` は次の class へ分ける。

| 新規 file | 主な責務 | 移動する test |
| --- | --- | --- |
| `Contracts/TrackerEngineBufferingContractTests.cs` | event-time reorder、merge window、0..N frame flush、late packet、processed time | `Update_FlushesBufferedDetectionsInEventTimeOrder_WhenArrivalOrderDiffers`、`Update_SplitsFrames_WhenObservationsExceedMergeWindow`、`Update_CanReturnZeroFramesWhileBuffering_AndMultipleFramesWhenSeveralGroupsFlush`、`Update_DropsLatePacketsAndDoesNotLetThemContaminateLaterFlushes`、`Update_EmitsWorldFrameCommittedForEachCommittedFrameInFlushOrder`、`Update_UsesSentTimeWhenCaptureTimeIsMissing`、`Update_DropsLatePacketsThatFallInsideAnAlreadyCommittedMergeWindow`、`Update_WaitsForTheOldestGroupMergeWindowToCloseBeforeFlushingIt`、`Update_PopulatesProcessedAtNsFromLocalProcessingTime` |
| `Contracts/TrackerEngineGeometryProfileContractTests.cs` | geometry snapshot、geometry reset、profile switch | `Update_PreservesDisplayGeometryInGeometrySnapshot`、`Update_EmitsGeometryResetAndDropsPendingFramesFromOldGeometryGeneration`、`Update_EmitsGeometryResetWhenGoalGeometryChanges`、`Update_WithControlOnlyProfileSwitch_EmitsOnlyProfileSwitched`、`Update_OrdersProfileSwitchBeforeWorldFrameCommitted_WhenSwitchAndFrameShareAResult`、`Update_PreservesFrameNumberContinuityAcrossProfileSwitch`、`Update_ProfileSwitchClearsPendingBufferedDetectionsFromOldProfile` |
| `Contracts/TrackerEngineRobotTrackingContractTests.cs` | robot merge、速度、カルマン、outlier、visibility、duplicate robot 抑制 | `Update_MergesSameRobotAcrossCamerasIntoSingleTrackedRobot` から `Update_DoesNotMergeStaleCameraPredictionWhenAnotherCameraHasFreshRobotObservation` まで |
| `Contracts/TrackerEngineBallTrackingContractTests.cs` | ball merge、primary/secondary、速度、カルマン、visibility、ghost/stale 抑制、identity、multi-camera cluster | `Update_MergesSameBallAcrossCamerasIntoSingleTrackedBall` から `Update_MergesThreeCameraBallChainIntoSingleCluster` まで |
| `Contracts/TrackerEngineKickContactContractTests.cs` | contact、last toucher、kick、flat/chip 分類 | `Update_PopulatesCurrentBallContactAndMarksContactingRobot` から `Update_UsesConfiguredChipHeightThresholdForChipClassification` まで |
| `Contracts/TrackerEngineBallLeftFieldContractTests.cs` | field 外退出、goal mouth / goal line / corner 分類 | `Update_EmitsBallLeftFieldWhenPrimaryBallLeavesThroughTouchLine`、`Update_ClassifiesGoalMouthExitAsGoalInterior`、`Update_ClassifiesNonGoalMouthExitAsGoalLine`、`Update_ClassifiesCornerExitByFirstPerimeterCrossing` |

抽出後の旧 `TrackerEngineTemporalContractTests.cs` は削除する。空 class や互換用 wrapper は残さない。

### engine contract test 用 base class

各 engine contract test class で constructor と fixture field の重複が増えるため、次の helper を追加してよい。

- file: `Tracker/Tracker.Tests/Contracts/TrackerEngineContractTestBase.cs`
- namespace: `Tracker.Tests`
- visibility: `public abstract class TrackerEngineContractTestBase : IClassFixture<TrackerContractFixture>`
- 内容:
  - `protected TrackerEngineContractTestBase(TrackerContractFixture fixture)`
  - `protected TrackerContractFixture Fixture { get; }`

各 concrete class は `TrackerEngineContractTestBase` を継承し、constructor で base へ fixture を渡す。xUnit の fixture 解決を明示するため、concrete class 側にも `IClassFixture<TrackerContractFixture>` を付ける。

### `TrackerCoordinatorTests.cs` の推奨分割

`TrackerCoordinatorTests.cs` は次の class へ分ける。

| 新規 file | 主な責務 | 移動する test |
| --- | --- | --- |
| `TrackerCoordinatorFrameFlowTests.cs` | committed frame、snapshot 更新、packet publish、derived event 順 | `ProcessPacket_WithCommittedFrame_UpdatesTrackedSnapshotAndPublishesTrackerPacket`、`ProcessPacket_WhenDerivedEventsExist_NotifiesObserverInEmittedOrder` |
| `TrackerCoordinatorResetAndProfileTests.cs` | geometry reset、profile switch、runtime tuning | `ProcessPacket_WhenGeometryResetOccurs_ClearsTrackedSnapshotBeforeNotifyingObserver`、`RequestProfileSwitch_WithoutPacket_DrainsControlOnlyUpdateAndClearsSnapshotBeforeObserverNotification`、`ProcessPacket_WithPendingProfileSwitch_PublishesCommittedFrameAfterApplyingNewProfileContext`、`RequestProfileSwitch_WithSameProfileButDifferentRuntimeTuning_AppliesNewEngineSettings` |
| `TrackerCoordinatorDiagnosticsCaptureTests.cs` | packet capture session、diagnostics sidecar、configured diagnostics file | `ProcessPacket_WithPacketCaptureSession_WritesDiagnosticsLogSidecar`、`ProcessPacket_WhenCaptureIsReenabled_WritesDiagnosticsToNewSidecar`、`ProcessPacket_WithCaptureDisabled_WritesDefaultDiagnosticsLogUnderCaptureDirectory`、`ProcessPacket_WithPacketCaptureSessionAndConfiguredDiagnosticsFile_WritesBothLogs` |

共有 helper は次へ抽出する。

- `Tracker/Tracker.Tests/Support/TrackerCoordinatorTestFactory.cs`
  - `TrackerCoordinator` 作成 overload 群を持つ。
  - `VisionPacketCaptureSession` 作成 helper を持つ。
  - `TrackerContractFixture` を constructor で受ける。
- `Tracker/Tracker.Tests/Support/RecordingTrackerPacketPublisher.cs`
  - `ITrackerPacketPublisher` 実装を移動する。
- `Tracker/Tracker.Tests/Support/RecordingTrackerObserver.cs`
  - `ITrackerObserver` 実装を移動する。
  - `TrackedSnapshotStore` 参照を使った clear 済み判定は現状のまま維持する。

### diagnostics / capture 系 test の扱い

- `TrackerRenderSnapshotLogReaderTests.cs`
  - TRACKER-035 で class 分割は必須にしない。
  - gzip/jsonl 書き込み helper と `CreateFrame` は private static のままでもよい。
  - 今後 `TrackerDiagnosticsLogReaderTests` と共有する必要が出た場合だけ `TrackerDiagnosticsTestFiles` へ抽出する。
- `VisionPacketCaptureTests.cs`
  - class 分割は必須にしない。
  - `CreateCaptureSession` は private helper のまま維持してよい。
  - replay test の assertion と metadata assertion を helper に隠しすぎない。
- `TrackedVisionViewStateTests.cs`
  - 1 つ目の mapping test は、fixture 作成部にコメントを足し、assertion group を `geometry`、`diagnostics`、`event metadata` の順で空行により整理する。
  - assertion を複数 test へ分ける場合は、1 つの view state 変換から複数の public contract を確認していることを保つため、重複 fixture 作成を helper 化してから行う。

## 日本語コメント追加基準

### 必須コメント

各 `[Fact]` / `[Theory]` の直前に、日本語 XML summary で「何を確認しているか」を 1 から 2 行で書く。通常コメント `// 何を確認しているか:` を必須形式とはしない。

```csharp
/// <summary>
/// 何を確認しているか: event time が到着順と異なる場合でも、確定 frame が event time 昇順で flush されることを確認する。
/// </summary>
[Fact]
```

XML summary は次を満たす。

- test 名を日本語へ直訳するだけにしない。
- 「入力条件」「守りたい契約」「壊れると起きる問題」のうち最低 1 つを含める。
- 数値 threshold が test の本質なら、`ReorderWindow`、`MergeWindow`、`ContactMarginMm` などの設定名を含める。
- 過去の不具合回帰 test では、現象を短く書く。
  - 例: `別 camera の正常観測がある場合、遠方 outlier で同一 robot ID track が瞬間移動しないことを確認する。`

### 任意コメント

test method 内では、次の場合に該当 block の直前へ短い日本語通常コメントを置いてよい。

- 複数 packet を順に投入し、どの packet が flush trigger か分かりにくい。
- profile switch や geometry reset のように、event 順序と local state clear の両方を同時に確認している。
- loop で jitter、visibility decay、secondary ball growth などの状態を作っている。

### 避けるコメント

- assertion と同じ内容だけを繰り返すコメント。
- production code の内部実装手順を固定しすぎるコメント。
- `Arrange`、`Act`、`Assert` だけの見出しコメント。
- `[Fact]` / `[Theory]` の説明を通常コメントだけで済ませること。
- 英語だけのコメント。識別子や protocol 名は英語のままでよい。

## TRACKER-035 実行順序

TRACKER-035 worker は次の順に進める。

1. `git status --short` で他 worker の変更を確認し、自分の対象外 file を編集しない。
2. `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-build --filter FullyQualifiedName~Tracker.Tests.TrackerEngineTemporalContractTests` を実行できる状態なら、分割前の対象 test 数と成功状態を確認する。`--no-build` が使えない場合は project-local dotnet home / NuGet cache を使う。
3. `TrackerEngineTemporalContractTests.cs` を上記 6 class へ機械的に移動する。最初は assertion を変えず、comment 以外の中身を編集しない。
4. engine contract test の focused test を実行し、失敗があれば移動漏れ、namespace、using、fixture 宣言だけを直す。
5. `TrackerCoordinatorTests.cs` を 3 class と support helper へ分割する。helper 抽出時も observable な記録内容を変えない。
6. coordinator focused test を実行し、失敗があれば helper 移動に伴う state 共有や disposal 漏れを直す。
7. `TrackerRenderSnapshotLogReaderTests.cs`、`TrackedVisionViewStateTests.cs`、`VisionPacketCaptureTests.cs`、その他 `Tracker/Tracker.Tests/*Tests.cs` に、必須コメント基準を満たす日本語 XML summary を追加する。
8. `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj` を実行し、full test の結果を report に記録する。
9. 差分を確認し、test method の assertion 変更、入力値変更、期待順序変更が混ざっていないことを確認する。
10. TRACKER-035 review 用 report を作成し、専用 review gate が閉じるまで `tasks-status.md` を done にしない。

## 意味を変えないための注意点

- `TrackerContractFixture.CreateSettings` の default 値を変更しない。
- `TrackerContractTestData.CreateDetectionPacket` / `CreateGeometryPacket` の呼び出し順と引数を変更しない。
- `CommittedFrames` と `EmittedEvents` の期待順序を読みやすさ目的で並べ替えない。
- `Assert.Single` を `First` や `SingleOrDefault` に置き換えない。
- `Assert.InRange` の範囲、`precision`、閾値を変更しない。
- `DateTimeOffset.UtcNow` を使う processed time test は、移動以外の変更をしない。
- temp directory / temp file を使う test では、既存の cleanup を保持する。
- shared helper 抽出後も、各 test が新しい engine / store / publisher / observer を作る独立性を維持する。
- support helper に static mutable state を持たせない。
- XML summary 追加時に test の Arrange / Act / Assert の順序を変えない。

## 検証観点

TRACKER-035 の検証は次を最低限にする。

- 分割前後で `Tracker.Tests` の test 数が減っていない。
- engine contract focused test がすべて通る。
- coordinator focused test がすべて通る。
- full `Tracker.Tests` が通る。
- `rg -n "何を確認しているか" Tracker/Tracker.Tests` と周辺 diff で、追加対象の `[Fact]` / `[Theory]` 直前に XML summary があることを確認できる。
- `git diff --stat` と `git diff --name-status` で、production code 変更が混ざっていない。
- review では「移動のみのはずの test が assertion を変えていないか」を重点的に見る。
