# トラッカーテスト保守性改善 詳細設計

## 目的

TRACKER-035 では、既存テストの意味を変えずに巨大なテスト文書を責務別へ分割し、各テストが何を確認しているかを日本語 XML コメントで明示する。

この詳細設計は `Tracker.Tests` のテスト保守性改善に限定する。`Tracker.Core` の中核処理、`Tracker.Server`、CLI、UI の製品コード分割方針は別の詳細設計で扱う。

## 現状

### 巨大テスト文書

- `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
  - 2,281 行、60 個の `[Fact]` を 1 クラスに保持している。
  - イベント時刻バッファー、フィールド形状リセット、プロファイル切り替え、ロボット追跡、ボール追跡、キック接触、ボール退場が同居している。
  - TRACKER-003 由来の時系列契約テストから、TRACKER-031 までの回帰テストが同じクラスに追加され続けているため、変更箇所を探す負荷が高い。
- `Tracker/Tracker.Tests/TrackerCoordinatorTests.cs`
  - 613 行、10 個の `[Fact]` と `RecordingTrackerPacketPublisher` / `RecordingTrackerObserver` 補助処理が同居している。
  - コーディネーターのスナップショット、公開、イベント、プロファイル、診断取得が同じクラスに並び、補助処理の責務境界がテスト本体から見えにくい。
- `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`
  - 291 行、読み取りテストと圧縮 JSON 行形式の補助処理が同居している。
  - 巨大ではないが、描画スナップショットの前提データを他の診断読み取りテストと共有できる形に分離すると、今後の追加テストが読みやすくなる。
- `Tracker/Tracker.Tests/TrackedVisionViewStateTests.cs`
  - 195 行、1 つの写像テストが多くの検証を持つ。
  - 分割必須ではないが、注釈追加と前提データ作成器化の対象にする。
- `Tracker/Tracker.Tests/VisionPacketCaptureTests.cs`
  - 226 行、取得書き込み、再生、実行時切り替えが同居している。
  - `VisionPacketCaptureSession` 作成補助処理は残してよいが、注釈追加対象にする。
- その他の `Tracker/Tracker.Tests/*Tests.cs`
  - 多くは 50 から 223 行であり、TRACKER-035 ではクラス分割より注釈追加と小さな補助処理整理を優先する。

### 既存補助処理

- `Tracker/Tracker.Tests/Contracts/TrackerContractFixture.cs`
  - 中核処理、パケット生成器、設定、プロファイル切り替え要求、フレームと状態の作成補助処理を持つ。
  - テスト分割後も正本の前提データとして維持し、同種の生成処理を各テストクラスに再作成しない。
- `Tracker/Tracker.Tests/Contracts/TrackerContractTestData.cs`
  - 生の SSL-Vision パケット作成の正本として維持する。
  - TRACKER-035 ではパケット生成処理の意味を変えない。

## 分割方針

### 基本方針

- テストの検証、入力パケット、設定値、時刻、順序期待値は変更しない。
- 1 つの既存 `[Fact]` は原則 1 つの新テストメソッドへそのまま移動する。
- メソッド名は原則維持し、同じメソッド名が別クラスに存在してもよい。
- 名前空間は既存と同じ `Tracker.Tests` を維持する。
- `TrackerContractFixture` と `TrackerContractTestData` を使い回し、分割のためだけに製品コードへテスト専用 API を追加しない。
- 文書内限定補助処理は、2 クラス以上で共有する場合だけ `Tracker.Tests/Contracts` または `Tracker.Tests/Support` 配下へ抽出する。

### `TrackerEngineTemporalContractTests.cs` の推奨分割

`TrackerEngineTemporalContractTests.cs` は次のクラスへ分ける。

| 新規文書 | 主な責務 | 移動するテスト |
| --- | --- | --- |
| `Contracts/TrackerEngineBufferingContractTests.cs` | イベント時刻順の並べ替え、結合窓、0 から N フレームの確定出力、遅延パケット、処理時刻 | `Update_FlushesBufferedDetectionsInEventTimeOrder_WhenArrivalOrderDiffers`、`Update_SplitsFrames_WhenObservationsExceedMergeWindow`、`Update_CanReturnZeroFramesWhileBuffering_AndMultipleFramesWhenSeveralGroupsFlush`、`Update_DropsLatePacketsAndDoesNotLetThemContaminateLaterFlushes`、`Update_EmitsWorldFrameCommittedForEachCommittedFrameInFlushOrder`、`Update_UsesSentTimeWhenCaptureTimeIsMissing`、`Update_DropsLatePacketsThatFallInsideAnAlreadyCommittedMergeWindow`、`Update_WaitsForTheOldestGroupMergeWindowToCloseBeforeFlushingIt`、`Update_PopulatesProcessedAtNsFromLocalProcessingTime` |
| `Contracts/TrackerEngineGeometryProfileContractTests.cs` | フィールド形状スナップショット、フィールド形状リセット、プロファイル切り替え | `Update_PreservesDisplayGeometryInGeometrySnapshot`、`Update_EmitsGeometryResetAndDropsPendingFramesFromOldGeometryGeneration`、`Update_EmitsGeometryResetWhenGoalGeometryChanges`、`Update_WithControlOnlyProfileSwitch_EmitsOnlyProfileSwitched`、`Update_OrdersProfileSwitchBeforeWorldFrameCommitted_WhenSwitchAndFrameShareAResult`、`Update_PreservesFrameNumberContinuityAcrossProfileSwitch`、`Update_ProfileSwitchClearsPendingBufferedDetectionsFromOldProfile` |
| `Contracts/TrackerEngineRobotTrackingContractTests.cs` | ロボット結合、速度、カルマン、外れ値、可視性、重複ロボット抑制 | `Update_MergesSameRobotAcrossCamerasIntoSingleTrackedRobot` から `Update_DoesNotMergeStaleCameraPredictionWhenAnotherCameraHasFreshRobotObservation` まで |
| `Contracts/TrackerEngineBallTrackingContractTests.cs` | ボール結合、主副の区別、速度、カルマン、可視性、幽霊状態と古い状態の抑制、同一性、複数カメラ集合 | `Update_MergesSameBallAcrossCamerasIntoSingleTrackedBall` から `Update_MergesThreeCameraBallChainIntoSingleCluster` まで |
| `Contracts/TrackerEngineKickContactContractTests.cs` | 接触、最後に触れた対象、キック、平面キックと浮き球キックの分類 | `Update_PopulatesCurrentBallContactAndMarksContactingRobot` から `Update_UsesConfiguredChipHeightThresholdForChipClassification` まで |
| `Contracts/TrackerEngineBallLeftFieldContractTests.cs` | フィールド外退出、ゴール開口部、ゴール線、角の分類 | `Update_EmitsBallLeftFieldWhenPrimaryBallLeavesThroughTouchLine`、`Update_ClassifiesGoalMouthExitAsGoalInterior`、`Update_ClassifiesNonGoalMouthExitAsGoalLine`、`Update_ClassifiesCornerExitByFirstPerimeterCrossing` |

抽出後の旧 `TrackerEngineTemporalContractTests.cs` は削除する。空クラスや互換用の包み込みは残さない。

### 中核処理契約テスト用の基底クラス

各中核処理契約テストクラスでコンストラクターと前提データフィールドの重複が増えるため、次の補助処理を追加してよい。

- 文書: `Tracker/Tracker.Tests/Contracts/TrackerEngineContractTestBase.cs`
- 名前空間: `Tracker.Tests`
- 可視性: `public abstract class TrackerEngineContractTestBase : IClassFixture<TrackerContractFixture>`
- 内容:
  - `protected TrackerEngineContractTestBase(TrackerContractFixture fixture)`
  - `protected TrackerContractFixture Fixture { get; }`

各具象クラスは `TrackerEngineContractTestBase` を継承し、コンストラクターで基底クラスへ前提データを渡す。テストフレームワークの前提データ解決を明示するため、具象クラス側にも `IClassFixture<TrackerContractFixture>` を付ける。

### `TrackerCoordinatorTests.cs` の推奨分割

`TrackerCoordinatorTests.cs` は次のクラスへ分ける。

| 新規文書 | 主な責務 | 移動するテスト |
| --- | --- | --- |
| `TrackerCoordinatorFrameFlowTests.cs` | 確定済みフレーム、スナップショット更新、パケット公開、派生イベント順 | `ProcessPacket_WithCommittedFrame_UpdatesTrackedSnapshotAndPublishesTrackerPacket`、`ProcessPacket_WhenDerivedEventsExist_NotifiesObserverInEmittedOrder` |
| `TrackerCoordinatorResetAndProfileTests.cs` | フィールド形状リセット、プロファイル切り替え、実行時調整 | `ProcessPacket_WhenGeometryResetOccurs_ClearsTrackedSnapshotBeforeNotifyingObserver`、`RequestProfileSwitch_WithoutPacket_DrainsControlOnlyUpdateAndClearsSnapshotBeforeObserverNotification`、`ProcessPacket_WithPendingProfileSwitch_PublishesCommittedFrameAfterApplyingNewProfileContext`、`RequestProfileSwitch_WithSameProfileButDifferentRuntimeTuning_AppliesNewEngineSettings` |
| `TrackerCoordinatorDiagnosticsCaptureTests.cs` | パケット取得セッション、診断付随文書、設定済み診断文書 | `ProcessPacket_WithPacketCaptureSession_WritesDiagnosticsLogSidecar`、`ProcessPacket_WhenCaptureIsReenabled_WritesDiagnosticsToNewSidecar`、`ProcessPacket_WithCaptureDisabled_WritesDefaultDiagnosticsLogUnderCaptureDirectory`、`ProcessPacket_WithPacketCaptureSessionAndConfiguredDiagnosticsFile_WritesBothLogs` |

共有補助処理は次へ抽出する。

- `Tracker/Tracker.Tests/Support/TrackerCoordinatorTestFactory.cs`
  - `TrackerCoordinator` 作成オーバーロード群を持つ。
  - `VisionPacketCaptureSession` 作成補助処理を持つ。
  - `TrackerContractFixture` をコンストラクターで受ける。
- `Tracker/Tracker.Tests/Support/RecordingTrackerPacketPublisher.cs`
  - `ITrackerPacketPublisher` 実装を移動する。
- `Tracker/Tracker.Tests/Support/RecordingTrackerObserver.cs`
  - `ITrackerObserver` 実装を移動する。
  - `TrackedSnapshotStore` 参照を使った消去済み判定は現状のまま維持する。

### 診断と取得系テストの扱い

- `TrackerRenderSnapshotLogReaderTests.cs`
  - TRACKER-035 でクラス分割は必須にしない。
  - 圧縮 JSON 行形式の書き込み補助処理と `CreateFrame` は非公開静的のままでもよい。
  - 今後 `TrackerDiagnosticsLogReaderTests` と共有する必要が出た場合だけ `TrackerDiagnosticsTestFiles` へ抽出する。
- `VisionPacketCaptureTests.cs`
  - クラス分割は必須にしない。
  - `CreateCaptureSession` は非公開補助処理のまま維持してよい。
  - 再生テストの検証とメタデータ検証を補助処理に隠しすぎない。
- `TrackedVisionViewStateTests.cs`
  - 1 つ目の写像テストは、前提データ作成部にコメントを足し、検証群を `geometry`、`diagnostics`、`event metadata` の順で空行により整理する。
  - 検証を複数テストへ分ける場合は、1 つの表示状態変換から複数の公開契約を確認していることを保つため、重複する前提データ作成を補助処理化してから行う。

## 日本語コメント追加基準

### 必須コメント

各 `[Fact]` / `[Theory]` の直前に、日本語 XML 要約で「何を確認しているか」を 1 から 2 行で書く。通常コメント `// 何を確認しているか:` を必須形式とはしない。

```csharp
/// <summary>
/// 何を確認しているか: イベント時刻が到着順と異なる場合でも、確定フレームがイベント時刻昇順で出力されることを確認する。
/// </summary>
[Fact]
```

XML 要約は次を満たす。

- テスト名を日本語へ直訳するだけにしない。
- 「入力条件」「守りたい契約」「壊れると起きる問題」のうち最低 1 つを含める。
- 数値しきい値がテストの本質なら、`ReorderWindow`、`MergeWindow`、`ContactMarginMm` などの設定名を含める。
- 過去の不具合回帰テストでは、現象を短く書く。
  - 例: `別カメラの正常観測がある場合、遠方外れ値で同一ロボット ID 追跡が瞬間移動しないことを確認する。`

### 任意コメント

テストメソッド内では、次の場合に該当処理の直前へ短い日本語通常コメントを置いてよい。

- 複数パケットを順に投入し、どのパケットが確定出力の契機か分かりにくい。
- プロファイル切り替えやフィールド形状リセットのように、イベント順序と局所状態消去の両方を同時に確認している。
- 反復処理で揺れ、可視性減衰、副ボール成長などの状態を作っている。

### 避けるコメント

- 検証と同じ内容だけを繰り返すコメント。
- 製品コードの内部実装手順を固定しすぎるコメント。
- `Arrange`、`Act`、`Assert` だけの見出しコメント。
- `[Fact]` / `[Theory]` の説明を通常コメントだけで済ませること。
- 英語だけのコメント。識別子やプロトコル名は英語のままでよい。

## TRACKER-035 実行順序

TRACKER-035 の作業担当は次の順に進める。

1. `git status --short` で他作業担当の変更を確認し、自分の対象外文書を編集しない。
2. `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-build --filter FullyQualifiedName~Tracker.Tests.TrackerEngineTemporalContractTests` を実行できる状態なら、分割前の対象テスト数と成功状態を確認する。`--no-build` が使えない場合は作業場所内の .NET 用ホーム領域と NuGet キャッシュを使う。
3. `TrackerEngineTemporalContractTests.cs` を上記 6 クラスへ機械的に移動する。最初は検証を変えず、コメント以外の中身を編集しない。
4. 中核処理契約テストの重点テストを実行し、失敗があれば移動漏れ、名前空間、参照宣言、前提データ宣言だけを直す。
5. `TrackerCoordinatorTests.cs` を 3 クラスと支援補助処理へ分割する。補助処理抽出時も観測可能な記録内容を変えない。
6. コーディネーター重点テストを実行し、失敗があれば補助処理移動に伴う状態共有や破棄漏れを直す。
7. `TrackerRenderSnapshotLogReaderTests.cs`、`TrackedVisionViewStateTests.cs`、`VisionPacketCaptureTests.cs`、その他 `Tracker/Tracker.Tests/*Tests.cs` に、必須コメント基準を満たす日本語 XML 要約を追加する。
8. `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj` を実行し、全テストの結果をレポートに記録する。
9. 差分を確認し、テストメソッドの検証変更、入力値変更、期待順序変更が混ざっていないことを確認する。
10. TRACKER-035 レビュー用レポートを作成し、専用レビューゲートが閉じるまで `tasks-status.md` を完了にしない。

## 意味を変えないための注意点

- `TrackerContractFixture.CreateSettings` の既定値を変更しない。
- `TrackerContractTestData.CreateDetectionPacket` / `CreateGeometryPacket` の呼び出し順と引数を変更しない。
- `CommittedFrames` と `EmittedEvents` の期待順序を読みやすさ目的で並べ替えない。
- `Assert.Single` を `First` や `SingleOrDefault` に置き換えない。
- `Assert.InRange` の範囲、`precision`、閾値を変更しない。
- `DateTimeOffset.UtcNow` を使う処理時刻テストは、移動以外の変更をしない。
- 一時ディレクトリや一時文書を使うテストでは、既存の後片付けを保持する。
- 共有補助処理抽出後も、各テストが新しい中核処理、保管先、公開器、観測通知を作る独立性を維持する。
- 支援補助処理に静的な可変状態を持たせない。
- XML 要約追加時にテストの準備、実行、検証の順序を変えない。

## 検証観点

TRACKER-035 の検証は次を最低限にする。

- 分割前後で `Tracker.Tests` のテスト数が減っていない。
- 中核処理契約の重点テストがすべて通る。
- コーディネーター重点テストがすべて通る。
- `Tracker.Tests` 全体が通る。
- `rg -n "何を確認しているか" Tracker/Tracker.Tests` と周辺差分で、追加対象の `[Fact]` / `[Theory]` 直前に XML 要約があることを確認できる。
- `git diff --stat` と `git diff --name-status` で、製品コード変更が混ざっていない。
- レビューでは「移動のみのはずのテストが検証を変えていないか」を重点的に見る。
