# `Tracker.Tests` 保守性改善 詳細設計

## 目的

`TRACKER-035` では、既存テストの意味を変えずに巨大なテストファイルを責務別へ分割し、各テストが何を確認しているかを、日本語の XML documentation comment で明示する。

この詳細設計は `Tracker.Tests` のテスト保守性改善に限定する。追跡処理、サーバー、CLI、UI の製品のソースコードの分割方針は別の詳細設計で扱う。

## 現状

### 巨大なテストファイル

- `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
  - 2,281 行、60 個の `[Fact]` を 1 クラスに保持している。
  - event time 順の入力保持、形状変更に伴う追跡状態の初期化、設定プロファイルの切り替え、ロボット追跡、ボール追跡、キックと接触、ボールがフィールド外へ出たことの判定が同居している。
  - `TRACKER-003` 由来の時系列契約テストから、`TRACKER-031` までの回帰検証が同じクラスに追加され続けているため、変更箇所を探す負荷が高い。
- `Tracker/Tracker.Tests/TrackerCoordinatorTests.cs`
  - 613 行、10 個の `[Fact]` と補助クラス `RecordingTrackerPacketPublisher` / `RecordingTrackerObserver` が同居している。
  - `TrackerCoordinator` のスナップショットの更新、送信、イベント、設定プロファイル、キャプチャー時の診断のテストが同じクラスに並び、補助クラスの責務境界がテスト本体から見えにくい。
- `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`
  - 291 行、読み取りのテストと gzip / JSONL 用の補助処理が同居している。
  - 巨大ではないが、render snapshot を用意する処理を他の診断ログ読み取りテストと共有できる形に分離すると、今後の追加テストが読みやすくなる。
- `Tracker/Tracker.Tests/TrackedVisionViewStateTests.cs`
  - 195 行、1 つの変換テストで多くの条件を検証している。
  - 分割必須ではないが、コメント追加とテスト用データの生成処理の分離を行う対象にする。
- `Tracker/Tracker.Tests/VisionPacketCaptureTests.cs`
  - 226 行、キャプチャーの書き込み、再生、実行中のキャプチャーの有効・無効切り替えが同居している。
  - `VisionPacketCaptureSession` を作成する補助処理は残してよいが、コメント追加対象にする。
- その他の `Tracker/Tracker.Tests/*Tests.cs`
  - 多くは 50 から 223 行であり、`TRACKER-035` ではクラス分割よりコメント追加と小さな補助処理の整理を優先する。

### 既存の補助処理

- `Tracker/Tracker.Tests/Contracts/TrackerContractFixture.cs`
  - 追跡エンジン、パケット生成器、設定、設定プロファイルの切り替え要求、追跡結果や状態を生成する補助メソッドを持つ。
  - テスト分割後も共通の生成処理として維持し、同種の生成処理を各テストクラスに再作成しない。
- `Tracker/Tracker.Tests/Contracts/TrackerContractTestData.cs`
  - 未加工の SSL-Vision パケットを作成する正本として維持する。
  - `TRACKER-035` ではパケット生成処理の意味を変えない。

## 分割方針

### 基本方針

- テストの検証条件、入力パケット、設定値、時刻、順序の期待値は変更しない。
- 1 つの既存 `[Fact]` は原則 1 つの新しいテストメソッドへそのまま移動する。
- メソッド名は原則維持し、同じメソッド名が別のクラスに存在してもよい。
- 名前空間は既存と同じ `Tracker.Tests` を維持する。
- `TrackerContractFixture` と `TrackerContractTestData` を使い回し、分割のためだけに製品のソースコードへテスト専用 API を追加しない。
- ファイル内だけで使う補助処理は、2 クラス以上で共有する場合だけ `Tracker.Tests/Contracts` または `Tracker.Tests/Support` 配下へ抽出する。

### `TrackerEngineTemporalContractTests.cs` の推奨分割

`TrackerEngineTemporalContractTests.cs` は次のクラスへ分ける。

| 新規ファイル | 主な責務 | 移動するテスト |
| --- | --- | --- |
| `Contracts/TrackerEngineBufferingContractTests.cs` | event time 順の並べ替え、統合する時間幅、0..N 件の追跡フレームの確定、遅延到着パケット、処理時刻 | `Update_FlushesBufferedDetectionsInEventTimeOrder_WhenArrivalOrderDiffers`、`Update_SplitsFrames_WhenObservationsExceedMergeWindow`、`Update_CanReturnZeroFramesWhileBuffering_AndMultipleFramesWhenSeveralGroupsFlush`、`Update_DropsLatePacketsAndDoesNotLetThemContaminateLaterFlushes`、`Update_EmitsWorldFrameCommittedForEachCommittedFrameInFlushOrder`、`Update_UsesSentTimeWhenCaptureTimeIsMissing`、`Update_DropsLatePacketsThatFallInsideAnAlreadyCommittedMergeWindow`、`Update_WaitsForTheOldestGroupMergeWindowToCloseBeforeFlushingIt`、`Update_PopulatesProcessedAtNsFromLocalProcessingTime` |
| `Contracts/TrackerEngineGeometryProfileContractTests.cs` | フィールド形状の保持、形状変更に伴う追跡状態の初期化、設定プロファイルの切り替え | `Update_PreservesDisplayGeometryInGeometrySnapshot`、`Update_EmitsGeometryResetAndDropsPendingFramesFromOldGeometryGeneration`、`Update_EmitsGeometryResetWhenGoalGeometryChanges`、`Update_WithControlOnlyProfileSwitch_EmitsOnlyProfileSwitched`、`Update_OrdersProfileSwitchBeforeWorldFrameCommitted_WhenSwitchAndFrameShareAResult`、`Update_PreservesFrameNumberContinuityAcrossProfileSwitch`、`Update_ProfileSwitchClearsPendingBufferedDetectionsFromOldProfile` |
| `Contracts/TrackerEngineRobotTrackingContractTests.cs` | ロボット追跡の統合、速度、Kalman filter、外れ値、可視性、ロボットの重複抑制 | `Update_MergesSameRobotAcrossCamerasIntoSingleTrackedRobot` から `Update_DoesNotMergeStaleCameraPredictionWhenAnotherCameraHasFreshRobotObservation` まで |
| `Contracts/TrackerEngineBallTrackingContractTests.cs` | ボール追跡の統合、主対象と補助対象、速度、Kalman filter、可視性、実体のない追跡や古い追跡の抑制、同じボールとしての識別、複数カメラの観測を同じボールとしてまとめる処理 | `Update_MergesSameBallAcrossCamerasIntoSingleTrackedBall` から `Update_MergesThreeCameraBallChainIntoSingleCluster` まで |
| `Contracts/TrackerEngineKickContactContractTests.cs` | 接触、最後に触れたロボット、キック、地上キックと浮き球キックの分類 | `Update_PopulatesCurrentBallContactAndMarksContactingRobot` から `Update_UsesConfiguredChipHeightThresholdForChipClassification` まで |
| `Contracts/TrackerEngineBallLeftFieldContractTests.cs` | ボールがフィールド外へ出たことの判定と、ゴール開口部、ゴールライン、角のどこを通ったかの分類 | `Update_EmitsBallLeftFieldWhenPrimaryBallLeavesThroughTouchLine`、`Update_ClassifiesGoalMouthExitAsGoalInterior`、`Update_ClassifiesNonGoalMouthExitAsGoalLine`、`Update_ClassifiesCornerExitByFirstPerimeterCrossing` |

抽出後の旧 `TrackerEngineTemporalContractTests.cs` は削除する。空クラスや互換性を保つためだけの呼び出し用の型は残さない。

### 追跡エンジンの契約テスト用の基底クラス

追跡エンジンの各契約テスト用クラスで重複する `TrackerContractFixture` の受け取りと保持をまとめるため、次の基底クラスを追加してよい。

- ファイル: `Tracker/Tracker.Tests/Contracts/TrackerEngineContractTestBase.cs`
- 名前空間: `Tracker.Tests`
- 公開範囲と継承関係: `public abstract class TrackerEngineContractTestBase : IClassFixture<TrackerContractFixture>`
- 内容:
  - `protected TrackerEngineContractTestBase(TrackerContractFixture fixture)`
  - `protected TrackerContractFixture Fixture { get; }`

各具象クラスは `TrackerEngineContractTestBase` を継承し、コンストラクターで基底クラスへ `TrackerContractFixture` を渡す。xUnit による `TrackerContractFixture` の受け渡しを明示するため、具象クラス側にも `IClassFixture<TrackerContractFixture>` を付ける。

### `TrackerCoordinatorTests.cs` の推奨分割

`TrackerCoordinatorTests.cs` は次のクラスへ分ける。

| 新規ファイル | 主な責務 | 移動するテスト |
| --- | --- | --- |
| `TrackerCoordinatorFrameFlowTests.cs` | 確定済みの追跡フレーム、スナップショットの更新、パケット送信、派生イベントの順序 | `ProcessPacket_WithCommittedFrame_UpdatesTrackedSnapshotAndPublishesTrackerPacket`、`ProcessPacket_WhenDerivedEventsExist_NotifiesObserverInEmittedOrder` |
| `TrackerCoordinatorResetAndProfileTests.cs` | 形状変更に伴う追跡状態の初期化、設定プロファイルの切り替え、実行時の調整 | `ProcessPacket_WhenGeometryResetOccurs_ClearsTrackedSnapshotBeforeNotifyingObserver`、`RequestProfileSwitch_WithoutPacket_DrainsControlOnlyUpdateAndClearsSnapshotBeforeObserverNotification`、`ProcessPacket_WithPendingProfileSwitch_PublishesCommittedFrameAfterApplyingNewProfileContext`、`RequestProfileSwitch_WithSameProfileButDifferentRuntimeTuning_AppliesNewEngineSettings` |
| `TrackerCoordinatorDiagnosticsCaptureTests.cs` | キャプチャーの保存単位、診断用の補助ファイル、設定で指定した診断ファイル | `ProcessPacket_WithPacketCaptureSession_WritesDiagnosticsLogSidecar`、`ProcessPacket_WhenCaptureIsReenabled_WritesDiagnosticsToNewSidecar`、`ProcessPacket_WithCaptureDisabled_WritesDefaultDiagnosticsLogUnderCaptureDirectory`、`ProcessPacket_WithPacketCaptureSessionAndConfiguredDiagnosticsFile_WritesBothLogs` |

共有する補助処理は次へ抽出する。

- `Tracker/Tracker.Tests/Support/TrackerCoordinatorTestFactory.cs`
  - `TrackerCoordinator` を作成する複数のオーバーロードを持つ。
  - `VisionPacketCaptureSession` を作成する補助メソッドを持つ。
  - `TrackerContractFixture` をコンストラクターで受ける。
- `Tracker/Tracker.Tests/Support/RecordingTrackerPacketPublisher.cs`
  - `ITrackerPacketPublisher` 実装を移動する。
- `Tracker/Tracker.Tests/Support/RecordingTrackerObserver.cs`
  - `ITrackerObserver` 実装を移動する。
  - `TrackedSnapshotStore` 参照を使った保存状態の消去済み判定は現状のまま維持する。

### 診断・キャプチャーのテストの扱い

- `TrackerRenderSnapshotLogReaderTests.cs`
  - `TRACKER-035` でクラス分割は必須にしない。
  - gzip / JSONL 書き込みの補助処理と `CreateFrame` は非公開の静的メソッドのままでもよい。
  - 今後 `TrackerDiagnosticsLogReaderTests` と共有する必要が出た場合だけ `TrackerDiagnosticsTestFiles` へ抽出する。
- `VisionPacketCaptureTests.cs`
  - クラス分割は必須にしない。
  - `CreateCaptureSession` は非公開の補助メソッドのまま維持してよい。
  - 再生テストの検証条件と、capture metadata の検証条件を補助処理に隠しすぎない。
- `TrackedVisionViewStateTests.cs`
  - 1 つ目の変換テストは、テスト用データの作成部にコメントを足し、検証箇所をフィールド形状、診断情報、イベントの付随情報の順で空行により整理する。
  - 検証箇所を複数のテストへ分ける場合は、1 つの表示状態の変換から複数の公開契約を確認していることを保つため、重複するテスト用データの作成を補助処理へ分離してから行う。

## 日本語コメント追加基準

### 必須コメント

各 `[Fact]` / `[Theory]` の直前に、XML の `summary` 要素で「何を確認しているか」を日本語で 1 から 2 行にまとめる。通常コメント `// 何を確認しているか:` を必須形式とはしない。

```csharp
/// <summary>
/// 何を確認しているか: パケットの到着順と event time の順序が異なる場合でも、追跡フレームが event time の昇順で確定されることを確認する。
/// </summary>
[Fact]
```

XML の `summary` 要素は次を満たす。

- テスト名を日本語へ直訳するだけにしない。
- 「入力条件」「守りたい契約」「壊れると起きる問題」のうち最低 1 つを含める。
- 数値の閾値がテストの本質なら、`ReorderWindow`、`MergeWindow`、`ContactMarginMm` などの設定名を含める。
- 過去の不具合の回帰検証では、現象を短く書く。
  - 例: 別のカメラの正常な観測がある場合、遠方の外れ値で同一ロボット ID の追跡位置が瞬間移動しないことを確認する。

### 任意コメント

テストメソッド内では、次の場合に該当する処理の直前へ短い日本語の通常コメントを置いてよい。

- 複数のパケットを順に投入し、どのパケットが追跡結果の確定のきっかけになるか分かりにくい。
- 設定プロファイルの切り替えや形状変更に伴う追跡状態の初期化のように、イベント順序と内部状態の消去の両方を同時に確認している。
- 繰り返し処理で、観測の揺れ、可視性の減衰、secondary ball を複数回継続して観測した状態などの状態を作っている。

### 避けるコメント

- 検証条件と同じ内容だけを繰り返すコメント。
- 製品のソースコードの内部実装手順を固定しすぎるコメント。
- `Arrange`、`Act`、`Assert` だけの見出しコメント。
- `[Fact]` / `[Theory]` の説明を通常コメントだけで済ませること。
- 英語だけのコメント。識別子や通信規約の名前は英語のままでよい。

## `TRACKER-035` 実行順序

`TRACKER-035` の担当者は次の順に進める。

1. `git status --short` で他の担当者の変更を確認し、自分の対象外のファイルを編集しない。
2. `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-build --filter FullyQualifiedName~Tracker.Tests.TrackerEngineTemporalContractTests` を実行できる状態なら、分割前の対象テスト数と成功状態を確認する。`--no-build` が使えない場合はプロジェクト専用の `DOTNET_CLI_HOME` と NuGet キャッシュを使う。
3. `TrackerEngineTemporalContractTests.cs` を上記 6 クラスへ機械的に移動する。最初は検証条件を変えず、コメント以外の中身を編集しない。
4. 追跡エンジンの契約テストに絞って実行し、失敗があれば移動漏れ、名前空間、`using`、`TrackerContractFixture` の宣言だけを直す。
5. `TrackerCoordinatorTests.cs` を 3 クラスと補助処理へ分割する。補助処理の抽出時も外部から観測できる記録内容を変えない。
6. `TrackerCoordinator` のテストに絞って実行し、失敗があれば補助処理の移動に伴う状態共有や外部資源の解放漏れを直す。
7. `TrackerRenderSnapshotLogReaderTests.cs`、`TrackedVisionViewStateTests.cs`、`VisionPacketCaptureTests.cs`、その他 `Tracker/Tracker.Tests/*Tests.cs` に、必須コメント基準を満たす日本語の XML `summary` 要素を追加する。
8. `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj` を実行し、全テストの結果を報告書に記録する。
9. 差分を確認し、テストメソッドの検証条件、入力値、期待順序の変更が混ざっていないことを確認する。
10. `TRACKER-035` のレビュー用報告書を作成し、専用レビューが完了するまで `tasks-status.md` を完了扱いにしない。

## 意味を変えないための注意点

- `TrackerContractFixture.CreateSettings` の既定値を変更しない。
- `TrackerContractTestData.CreateDetectionPacket` / `CreateGeometryPacket` の呼び出し順と引数を変更しない。
- `CommittedFrames` と `EmittedEvents` の期待順序を読みやすさ目的で並べ替えない。
- `Assert.Single` を `First` や `SingleOrDefault` に置き換えない。
- `Assert.InRange` の範囲、`precision`、閾値を変更しない。
- `DateTimeOffset.UtcNow` を使う処理時刻のテストは、移動以外の変更をしない。
- 一時ディレクトリや一時ファイルを使うテストでは、既存の後片付けを保持する。
- 共通の補助処理を抽出した後も、各テストが追跡エンジン、状態の保存用オブジェクト、送信処理、通知先のインスタンスを個別に作る独立性を維持する。
- 補助処理に、変更可能な静的状態を持たせない。
- XML の `summary` 要素を追加する際に、テストの準備、実行、検証の順序を変えない。

## 検証観点

`TRACKER-035` の検証は次を最低限にする。

- 分割前後で `Tracker.Tests` のテスト数が減っていない。
- 追跡エンジンの契約テストがすべて通る。
- `TrackerCoordinator` のテストがすべて通る。
- `Tracker.Tests` の全テストが通る。
- `rg -n "何を確認しているか" Tracker/Tracker.Tests` と周辺の差分で、追加対象の `[Fact]` / `[Theory]` 直前に XML の `summary` 要素があることを確認できる。
- `git diff --stat` と `git diff --name-status` で、製品のソースコードの変更が混ざっていない。
- レビューでは「移動のみのはずのテストで検証条件が変わっていないか」を重点的に見る。
