# `Tracker.Core` 追跡エンジンの詳細設計

本書で raw vision は SSL-Vision の検出情報を指す。カメラの画像や動画そのものではない。

## 目的

`TRACKER-033` で `Tracker.Core` の巨大ファイルを責務別に分割し、主要なクラス、プロパティ、メソッドに日本語コメントを追加できるように、追跡エンジンの分割境界、実行順序、挙動維持の確認観点を固定する。

この設計は保守性改善の詳細設計であり、`TRACKER-033` ではトラッカーの追跡挙動、公開契約、通信形式での出力、設定値の意味を変更しない。

## 対象範囲

- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Core/TrackerModelContracts.cs`
- `Tracker/Tracker.Core/TrackerPacketGenerator.cs`

対象外:

- DebugHost / CLI / UI 側の詳細設計
- テストファイルの分割設計
- 追跡アルゴリズム、設定値、通信形式での出力の仕様変更

## 現状の巨大ファイルと責務

### `TrackerExecutionContracts.cs`

現状は約 2200 行を超え、次の責務が 1 ファイルに混在している。

- 追跡エンジンの公開契約
  - `ITrackerEngine`
  - `TrackerUpdateResult`
  - `TrackerEngineDiagnostics`
  - `TrackerEvent`
  - `TrackerEventKind`
  - `ITrackerObserver`
- `TrackerEngine` 本体
  - `Update` による設定プロファイルの切り替え、フィールド形状の更新、バッファへの検出情報追加、追跡結果の確定の実行
  - 未確定の検出情報の保持、event time 順の並べ替え、観測を統合する時間幅の管理
  - フィールド形状の大幅変更に伴う追跡状態の初期化と最新状態の消去
  - world frame（フィールド全体の追跡フレーム）の確定とイベント発行
- ボール追跡
  - camera-local ball track の観測更新、予測、可視性の減衰
  - 複数カメラの観測を同じボールとしてまとめる処理と統合後のボールの識別情報の維持
  - primary ball の安定化と secondary ball の出力順序
- ロボット追跡
  - カメラごとのロボット観測の収集
  - 同一ロボット ID の遠方の外れ値の除去
  - ロボット追跡状態の観測更新、予測、複数カメラの追跡結果の統合
  - 向きの角度の連続化と正規化
  - 向きを推定する Kalman filter で使う、rad 単位に対応する共分散と角速度の範囲制限
- AutoRef 向けの派生イベント
  - ボール接触
  - キック検出と kicked ball の状態の継続
  - ball left field（ボールのフィールド外判定）と、境界を横切る位置の推定
- フィールド形状の変換
  - `SSL_GeometryData` から `TrackerGeometrySnapshot` への変換
  - 線分と円弧の情報をスナップショットとして保持
- Kalman filter と数値計算の補助処理
  - 軸ごとの状態の初期化、予測、更新
  - measurement noise、process noise、可視性の閾値に関する設定の解決
  - 距離の計算、時刻の変換、速度の計算
- 非公開の状態保持用の `record` と比較処理
  - `BufferedDetection`
  - `BallObservation`
  - `KalmanAxisState`
  - `BallTrackState`
  - `MergedBallState`
  - `MergedBallIdentityState`
  - `RobotKey`
  - `CameraRobotKey`
  - `RobotObservation`
  - `RobotTrackState`
  - `BufferedDetectionGroup`
  - `TrackedBallComparer`
  - `TrackedRobotComparer`
- 追跡エンジンの設定と実行時の上書き設定の契約
  - `TrackerEngineSettings`
  - `TrackerRuntimeOverrides`
  - `TrackerPublishOverrides`
  - `TrackerRobotTrackerOverrides`
  - `TrackerBallTrackerOverrides`
  - `TrackerKickDetectorOverrides`
  - `TrackerProfileSwitchRequest`

### `TrackerModelContracts.cs`

現状は約 230 行で、内部で使うフィールド全体の状態表現、フィールド形状のスナップショット、追跡状態、接触・キック・フィールド外判定などの派生状態、source detection、チームの列挙型が 1 ファイルにまとまっている。行数は `TrackerExecutionContracts.cs` より小さいが、公開 DTO が多く、`TRACKER-033` の日本語コメント追加時に責務単位で分けた方が読みやすい。

主な責務:

- 追跡フレームとその付随情報: `TrackerFrame`, `TrackerFrameMetadata`
- フィールド形状: `TrackerGeometrySnapshot`, `TrackerGeometryLineSegment`, `TrackerGeometryCircularArc`
- 追跡対象: `TrackedBallState`, `TrackedRobotState`, `TrackerTeam`
- AutoRef 向けの派生状態: `KickEventState`, `BallContactState`, `BallLeftFieldState`
- 診断・再生の元情報: `TrackerSourceDetectionFrame`

### `TrackerPacketGenerator.cs`

現状は約 190 行で、`TrackerFrame` から公式形式の `TrackerWrapperPacket` への変換を担っている。巨大ではないが、`Tracker.Core` の公開境界として日本語コメント追加対象に含める。

主な責務:

- パケット全体に付随する情報の設定
- `TrackedFrame` の tracked frame number と tracked frame timestamp の設定
- primary ball を先頭に置く処理と、secondary ball の安定した並び順
- ボール、ロボット、kicked ball の情報を通信形式へ変換
- `TrackerTeam` から公式形式の `Team` への変換
- `mm` / `mm/s` / `ns` から公式の単位への変換
- 対応機能を固定した順序で出力

## 分割後の推奨ファイル構成

`TRACKER-033` では名前空間を `Tracker.Core` のまま維持し、同一アセンブリ内のソースファイルの分割だけを行う。公開する型名、型のメンバーの名前、公開範囲、`null` を許可する箇所は変更しない。

### ファイル命名と `partial` 配置

`.` 区切りのファイル名は、フレームワークや開発ツールの慣習に限って許容する。例: `.csproj`、`.sln`、`.razor.cs`、`.razor.css`、`.g.cs`、`.Designer.cs`、`.AssemblyInfo.cs`、自動生成物やビルド出力。

手書き C# の責務を示すために `TypeName.Responsibility.cs` を使わない。partial class を責務別に分ける場合は type-owned folder を作り、`TypeName/Responsibility.cs` 形式を基本にする。フォルダが型名、ファイルが責務名を表すため、名前空間と公開契約を維持したまま責務境界をファイルパスで読める。

`public` / `internal` の最上位の型 1 つにつき、1 ファイルを基本にする。複数の最上位の型を同居させるのは、親子 DTO、密結合した小さな列挙型や拡張処理、同じ外部データ形式の一部で単独参照されない型の場合に限る。

### 公開契約

- `Tracker/Tracker.Core/Engine/ITrackerEngine.cs`
  - `ITrackerEngine`
- `Tracker/Tracker.Core/Engine/TrackerUpdateResult.cs`
  - `TrackerUpdateResult`
  - `TrackerEngineDiagnostics`
  - `TrackerEvent`
  - `TrackerEventKind`
- `Tracker/Tracker.Core/Engine/ITrackerObserver.cs`
  - `ITrackerObserver`
- `Tracker/Tracker.Core/Engine/TrackerProfileSwitchRequest.cs`
  - `TrackerProfileSwitchRequest`

### 追跡エンジンの本体

- `Tracker/Tracker.Core/Engine/TrackerEngine/TrackerEngine.cs`
  - `TrackerEngine` の状態を保持する変数、明示的なコンストラクターを持たない構成、`Update`
  - 設定プロファイルの切り替え、フィールド形状の更新、検出情報の受け付け、追跡結果の確定の呼び出しをまとめる最上位の制御処理
- `Tracker/Tracker.Core/Engine/TrackerEngine/FrameCommit.cs`
  - `FlushCommittedFrames`
  - `ClearPendingStateAndAdvanceLateCutoff`
  - `CommitGroup`
  - 追跡結果とイベントを出力する処理の組み立て
- `Tracker/Tracker.Core/Engine/TrackerEngine/DetectionBuffer.cs`
  - `CreateBufferedDetection`
  - `CreateSourceDetectionFrames`
  - `SelectEventTimeSeconds`
  - `BuildDetectionGroups`
  - `BufferedDetection`
  - `BufferedDetectionGroup`
- `Tracker/Tracker.Core/Engine/TrackerEngine/Geometry.cs`
  - `ShouldResetForGeometryChange`
  - `CreateGeometrySnapshot`
  - `CreateGeometryLineSegment`
  - `CreateGeometryCircularArc`
- `Tracker/Tracker.Core/Engine/TrackerEngine/BallTracking.cs`
  - `UpdateCameraBallTrackStates`
  - `CreateObservedBallTrackState`
  - `CreatePredictedBallTrackState`
  - `PredictBallTrackState`
  - `CollectMergedBallStates`
  - `BuildBallClusters`
  - `CanAttachBallTrackToCluster`
  - `AssignMergedBallIdentity`
  - `CreateTrackedBall`
  - `IsFreshPreviousPrimaryBall`
  - ボール追跡に関する非公開の `record`
- `Tracker/Tracker.Core/Engine/TrackerEngine/RobotTracking.cs`
  - `UpdateCameraRobotTrackStates`
  - `CollectCameraRobotObservations`
  - `DropFarRobotOutliersWhenSameRobotHasNearObservation`
  - `IsNearExistingRobotTrack`
  - `AddRobotObservations`
  - `HasCloseRobotObservationWithDifferentId`
  - `AddRobotObservation`
  - `CreateObservedRobotTrackState`
  - `CreatePredictedRobotTrackState`
  - `PredictRobotTrackState`
  - `CollectMergedRobotStates`
  - `CreateTrackedRobot`
  - ロボットの識別子・観測・追跡に関する非公開の `record`
- `Tracker/Tracker.Core/Engine/TrackerEngine/Contact.cs`
  - `CreateBallContactState`
  - `ApplyBallContactFlags`
  - `UpdateLatestBallContactState`
  - `PruneLatestBallContactStates`
  - `DidBallContactChange`
- `Tracker/Tracker.Core/Engine/TrackerEngine/Kick.cs`
  - `UpdateKickState`
  - `TryCreateKickEventState`
  - `SelectRecentContact`
  - `GetPlanarSpeedMmPerS`
  - `IsChipKick`
- `Tracker/Tracker.Core/Engine/TrackerEngine/BallLeftField.cs`
  - `CreateBallLeftFieldState`
  - `UpdateLatestBallLeftFieldState`
  - `PruneLatestBallLeftFieldStates`
  - `DidBallLeaveField`
  - `IsBallOutOfField`
  - `ProjectBallCrossing`
  - `ClassifyBoundaryNameFromCurrentPosition`
  - `TryProjectFirstPerimeterCrossing`
  - `ProjectTouchLineCrossing`
  - `ProjectGoalLineCrossing`
  - `InterpolateTimestamp`
- `Tracker/Tracker.Core/Engine/TrackerEngine/Kalman.cs`
  - `KalmanAxisState`
  - `CreateInitialKalmanAxis`
  - `PredictKalmanAxis`
  - `UpdateKalmanAxis`
  - 向きを推定する Kalman filter の速度分散、予測に伴う分散、速度の範囲制限の指定
- `Tracker/Tracker.Core/Engine/TrackerEngine/Settings.cs`
  - 設定値を決定する非公開の補助処理
  - 可視性と観測品質の減衰処理
  - 単位と時刻の変換処理
  - 角度と距離の計算処理
- `Tracker/Tracker.Core/Engine/TrackedStateComparers.cs`
  - `TrackedBallComparer`
  - `TrackedRobotComparer`

`TrackerEngine` は `partial sealed class` に変更してよい。ただし分割対象の補助処理は `private` のまま残し、`internal` にしてテストから直接触る形にはしない。テストは公開契約経由で挙動を固定する。

### 設定と上書き設定の契約

- `Tracker/Tracker.Core/Configuration/TrackerEngineSettings.cs`
  - `TrackerEngineSettings`
- `Tracker/Tracker.Core/Configuration/TrackerRuntimeOverrides.cs`
  - `TrackerRuntimeOverrides`
  - `TrackerPublishOverrides`
  - `TrackerRobotTrackerOverrides`
  - `TrackerBallTrackerOverrides`
  - `TrackerKickDetectorOverrides`

既存の `Tracker.DebugHost` と `Tracker.CaptureReplay` が参照している型名は維持する。`Tracker.Core` 内のフォルダ移動によって、これらが参照する名前空間は変えない。

### 状態表現の契約

- `Tracker/Tracker.Core/Model/TrackerFrame.cs`
  - `TrackerFrame`
  - `TrackerFrameMetadata`
- `Tracker/Tracker.Core/Model/TrackerGeometrySnapshot.cs`
  - `TrackerGeometrySnapshot`
  - `TrackerGeometryLineSegment`
  - `TrackerGeometryCircularArc`
- `Tracker/Tracker.Core/Model/TrackedBallState.cs`
  - `TrackedBallState`
- `Tracker/Tracker.Core/Model/TrackedRobotState.cs`
  - `TrackedRobotState`
  - `TrackerTeam`
- `Tracker/Tracker.Core/Model/TrackerMetaStates.cs`
  - `KickEventState`
  - `BallContactState`
  - `BallLeftFieldState`
- `Tracker/Tracker.Core/Model/TrackerSourceDetectionFrame.cs`
  - `TrackerSourceDetectionFrame`

### パケット生成器

- `Tracker/Tracker.Core/Proto/TrackerPacketGenerator.cs`
  - `TrackerPacketGenerator`

`TrackerPacketGenerator` は現状の 1 ファイル維持でよい。将来さらに肥大化した場合のみ、`Tracker/Tracker.Core/Proto/TrackerPacketGenerator/Balls.cs`、`Robots.cs`、`KickedBall.cs` のような type-owned folder による partial class の分割を検討する。

## 日本語コメント追加基準

`TRACKER-033` では日本語の XML documentation comment を追加する。固有名詞、型名、設定キー、通信形式の名前、単位記号は英字のままでよい。

クラス、プロパティ、メソッドの説明は、原則として XML documentation comment に記述する。通常コメント `//` はメソッド内の複雑な処理、不変条件、順序制約の直前に限定し、型や型のメンバーの契約説明には使わない。

### クラス・`interface`・列挙型

次の型には必ず `/// <summary>` を付ける。

- `public` / `internal` のクラス、`interface`、`record`、`record struct`、列挙型
- `TrackerEngine` の非公開の入れ子の `record` と比較処理のうち、分割後も非公開の補助処理として残す型

`summary` 要素の説明文には「何を表すか」「どの境界で使うか」を 1 から 2 文で書く。実装手順や履歴は書かない。

例:

```csharp
/// <summary>
/// raw vision のパケットから確定済みの追跡フレームとイベントを生成する、追跡エンジンの契約。
/// </summary>
```

### プロパティ

次のプロパティには必ず `/// <summary>` を付ける。

- `public` / `internal` DTO のプロパティ
- 設定値を表すプロパティ
- 単位、時刻、設定プロファイル、識別情報、出力順序に関わるプロパティ
- `null` が何を意味するかが、対象の仕様上重要なプロパティ

`summary` 要素の説明文では、単位と `null` / 0 / 空の値の意味を明記する。特に次は省略しない。

- `Mm`, `MmPerS`, `Rad`, `RadPerS`, `Ns` の単位
- `DataTimestampNs` と `ProcessedAtNs` の違い
- `PrimaryBallTrackId` が `null` の条件
- `KickedBall` が `null` または `IsStillMoving == false` の扱い
- `RuntimeOverrides` が設定プロファイルのスナップショットに対する一時上書きであること

### メソッド

次のメソッドには `/// <summary>` を付ける。

- `public` / `internal` のメソッド
- `TrackerEngine.Update`
- 分割後に各ファイルの入口になる非公開のメソッド
- 並べ替え、統合、Kalman filter、形状変更に伴う追跡状態の初期化、識別情報の割り当て、イベント発行、通信形式への変換の境界となるメソッド

単純な値の取得処理、数式そのものが明らかな非公開の補助処理、1 行で別の処理を呼び出すだけのメソッドには無理に付けない。ただし「なぜこの順序か」「どの挙動を固定するか」が読み手に伝わりにくい場合は、非公開のメソッドでも説明文または短い通常コメントを追加する。

### 通常コメント

通常コメントは複雑な処理のまとまりの前にだけ置く。既存のソースコードをなぞるだけのコメントは追加しない。

追加対象の例:

- 設定プロファイルの切り替えイベントを `WorldFrameCommitted` より前に発行する箇所
- `ReorderWindow` と `MergeWindow` によって、確定する追跡結果を決める箇所
- フィールド形状の大幅変更に伴う追跡状態の初期化で未処理の検出情報を捨てる箇所
- primary ball の継続を、secondary ball の並べ替えより優先する箇所
- Kalman filter の更新で予測状態と前回の位置を併用する箇所
- ロボットの遠方外れ値を同一ロボット ID の近傍観測で落とす箇所

## `TRACKER-033` 実行順序

1. 作業前に `Tracker/Design/Core/tracker-core-engine-detail-design.md` と `Tracker/Design/Core/tracker-architecture-plan.md` を読み、設計上の挙動固定点を確認する。
2. `TrackerExecutionContracts.cs` から公開契約を先に分離する。`ITrackerEngine`、結果とイベント、通知先、設定プロファイルの切り替え要求の型名と名前空間を変えない。
3. `TrackerEngine` を `partial sealed class` にして、最上位の `Update` と状態を保持する変数を `Engine/TrackerEngine/TrackerEngine.cs` に残す。
4. 検出情報を保持するバッファと追跡結果の確定処理を分離する。ここで `CommittedFrames` と `EmittedEvents` の順序が変わらないことを、対象を絞ったテストで確認する。
5. フィールド形状の変換と、形状変更に伴う追跡状態の初期化判定を分離する。未確定の検出情報の消去、追跡フレームの番号の維持、遅延到着の判定境界の扱いを変えない。
6. ボール追跡を分離する。camera-local track の ID、統合後の内部追跡 ID、primary ball の継続、secondary ball の追跡が確立する条件を変えない。
7. Kalman filter の補助処理を分離する。`UpdateKalmanAxis` の引数と、予測状態・前回の位置を使う計算の基準を変えない。
8. ロボット追跡を分離する。同一ロボット ID の複数カメラの追跡結果を統合する処理、遠方の外れ値の除去、向きの角度を連続化する処理の順序を変えない。
9. 接触、キック、ball left field を分離する。イベントの発火条件、直近の接触とみなす時間範囲、境界名を変えない。
10. 設定と実行時の上書き設定の契約を `Configuration` 配下へ分離し、`Tracker.RuntimeHost`、`Tracker.DebugHost`、`Tracker.CaptureReplay`、テストの参照がソースファイルのファイルパスに依存していないことを確認する。
11. 状態表現の契約を `Model` 配下へ分離し、公開プロパティの構造を変えずに日本語の XML documentation comment を追加する。
12. `TrackerPacketGenerator` を `Proto` 配下へ移動し、primary ball を先頭に置く処理、ロボットの並べ替え、対応機能の順序、単位変換のコメントを追加する。
13. 全分割後に `TrackerExecutionContracts.cs` と `TrackerModelContracts.cs` が残る場合は、空の互換用ファイルを残さず削除する。
14. 対象を絞ったテストを実行し、`Tracker.Core` の契約、時系列の契約、`TrackerPacketGenerator`、`TrackerCoordinator` での設定プロファイルの切り替えの正常系が通ることを確認する。
15. 全テストを実行する。失敗した場合は、分割による参照漏れか、挙動差分かを切り分けてから修正する。

## 挙動を変えないための注意点

- 名前空間は `Tracker.Core` のまま維持する。
- `public` / `internal` の型名、型のメンバーの名前、プロパティの型、`null` を許可するかどうか、既定値を変えない。
- `ITrackerEngine.Update` の引数順、引数の既定値、`null` を許す条件を変えない。
- `ProfileSwitched` は状態の消去後、同じ結果内の `WorldFrameCommitted` より前に発行する。
- `GeometryReset` はフィールド形状の大幅変更に伴う追跡状態の初期化時だけ発行し、追跡フレームの番号と実行時の識別情報は維持する。
- 検出情報の event time は `TCapture > 0` を優先し、これを使えない場合の代用は `TSent` のままにする。
- 未処理の検出情報から追跡結果を確定する順序は、event time、カメラ ID、source frame number（入力の観測フレームの番号）の安定した順序を維持する。
- `ReorderWindow` と `MergeWindow` の意味を入れ替えない。
- 遅延到着パケットの破棄では、event time が `lastCommittedGroupCloseTimestampNs` 以下の入力を状態更新に使わない。
- 形状変更に伴う追跡状態の初期化や設定プロファイルの切り替え時に、未処理の入力バッファ、camera-local track、統合後のボールの識別情報、接触と ball left field の状態、継続中のキック状態、primary ball を消去する範囲を変えない。
- `nextCommittedFrameNumber` は状態の消去で戻さない。
- primary ball の継続判定を、secondary ball の並べ替えより優先する。
- secondary ball は、可視性の降順、最後に観測できた時刻の降順、内部追跡 ID の昇順という安定した順序を維持する。
- secondary ball の出力は、新しい観測と、追跡の確立に必要な観測回数の条件を維持する。
- ボールとロボットの Kalman filter の更新は予測状態を基準にし、観測値から速度を算出する際には前回の位置を使う。
- Kalman filter の向きの軸では、位置（mm）用の共分散を流用しない。角度（rad）用の measurement noise と process noise に対応する分散と、角速度の範囲制限を使う。設定プロファイルの Kalman filter の倍率設定は、既定値との比で rad 用の基準値へ反映する。
- ロボットの観測収集では、統合する時間範囲内でカメラ、チーム、ロボット ID が同じ候補について、既存の同一 ID の追跡位置への近さを優先する。さらに、既存の別 ID の追跡位置の近傍への突然の ID 変更候補を `RobotTracker.IdentitySwitchDistanceMm` で抑制する。ID が急に入れ替わることは小さな位置ずれより起きづらいという前提を、対応付けに反映する。
- 上書き設定の解決処理は `null` の意味と既定値を変えない。
- `TrackerPacketGenerator` の単位変換は `mm -> m`、`mm/s -> m/s`、`ns -> s` のままにする。
- `TrackerPacketGenerator` は `KickedBall` が `IsStillMoving == true` の場合だけ公式形式の `kicked_ball` を出す。
- `Capabilities` の内容と順序を変えない。

## 検証観点

`TRACKER-033` で対象を絞って検証する際は、少なくとも次を含める。

- `Tracker.Core` の公開契約
  - 公開する型とプロパティが引き続き参照できること
  - `TrackerRuntimeOverrides` と `TrackerProfileSwitchRequest` のスナップショットの構造が変わらないこと
- 追跡エンジンの時系列処理
  - event time 順の並べ替え
  - 統合する時間幅に応じた追跡フレームの分割
  - 遅延到着パケットの破棄
  - 確定済みの追跡フレームを含まない結果
  - `WorldFrameCommitted` イベントの順序
- 設定プロファイルの切り替え / 形状変更に伴う追跡状態の初期化
  - `ProfileSwitched` の発行順序
  - 観測データを伴わない制御要求の処理
  - フィールド形状の大幅変更に伴う追跡状態の初期化
  - 状態の消去後も追跡フレームの番号が維持されること
- ボール追跡
  - primary ball の継続
  - 複数カメラのボール追跡の統合
  - secondary ball の安定順
  - Kalman filter の更新の基準として予測状態を使うこと
- ロボット追跡
  - 複数カメラのロボット追跡の統合
  - 同一ロボット ID の遠方の外れ値で、統合したロボットの追跡位置が瞬間移動しないこと
  - 向きの角度の連続化と正規化
- AutoRef 向けの派生状態
  - 接触の変化
  - キック検出と kicked ball の状態の継続
  - ball left field と境界を横切る位置の判定
- パケット生成
  - source name と UUID
  - 時刻変換
  - primary ball を先頭に配置
  - ロボットの並べ替え
  - kicked ball の出力条件
  - 対応機能

推奨コマンド:

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj \
  --filter "FullyQualifiedName~TrackerCoreContractSurfaceTests|FullyQualifiedName~TrackerEngineTemporalContractTests|FullyQualifiedName~TrackerPacketGeneratorContractTests|FullyQualifiedName~TrackerCoordinatorTests" \
  -m:1 /nr:false
```

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false
```

## 残るリスク

- 非公開の補助処理の分割だけでも、`partial` 化の際に非公開の入れ子の `record` の参照順やファイル配置を誤るとコンパイルエラーになりやすい。
- コメント追加量が多いため、実装移動とコメント追加を同時に広く行うとレビューが難しくなる。`TRACKER-033` では責務単位で分割し、各単位ごとに対象を絞ったテストを挟む。
- `TrackerPacketGenerator` は行数が小さいため、過剰分割すると可読性が下がる。`TRACKER-033` では移動とコメント追加を優先し、partial class の分割は必要になった場合だけ行う。
