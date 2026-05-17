# トラッカー中核処理 詳細設計

## 目的

TRACKER-033 で `Tracker.Core` の巨大な文書を責務別に分割し、主要な型、プロパティ、メソッドに日本語コメントを追加できるように、中核処理側の分割境界、実行順序、挙動維持の確認観点を固定する。

この設計は保守性改善の詳細設計であり、TRACKER-033 では トラッカー の追跡挙動、公開契約、プロトコル出力、設定値の意味を変更しない。

## 対象範囲

- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Core/TrackerModelContracts.cs`
- `Tracker/Tracker.Core/TrackerPacketGenerator.cs`

対象外:

- Tracker.DebugHost / CLI / UI 側の詳細設計
- テスト文書の分割設計
- 追跡アルゴリズム、設定値、プロトコル出力の仕様変更

## 現状の巨大ファイルと責務

### `TrackerExecutionContracts.cs`

現状は約 2200 行を超え、次の責務が 1 ファイルに混在している。

- 中核処理の公開契約
  - `ITrackerEngine`
  - `TrackerUpdateResult`
  - `TrackerEngineDiagnostics`
  - `TrackerEvent`
  - `TrackerEventKind`
  - `ITrackerObserver`
- `TrackerEngine` 本体
  - `Update` によるプロファイル切り替え、フィールド形状更新、検出バッファー追加、確定出力
  - 確定待ち検出バッファーとイベント時刻順の並べ替え、結合窓の管理
  - フィールド形状の大変更時リセットと最新状態の消去
  - ワールドフレーム確定とイベント通知
- ボール追跡
  - カメラ内ボール追跡の観測更新、予測、可視性の減衰
  - 複数カメラのボール集合と結合済みボール同一性の維持
  - 主ボールの安定化と副ボールの出力順序
- ロボット追跡
  - カメラ内ロボット観測の収集
  - 同一ロボット ID の遠方外れ値除去
  - ロボット追跡の観測更新、予測、複数カメラ結合
  - 向きの折り返し補正と正規化
  - 向きフィルターのラジアン単位の共分散と角速度制限
- AutoRef 向けメタイベント
  - ボール接触
  - キック検出と蹴られたボール状態の継続
  - ボール退場と境界交差の投影
- フィールド形状変換
  - `SSL_GeometryData` から `TrackerGeometrySnapshot` への変換
  - 直線と円弧のスナップショット化
- カルマン と数値補助処理
  - 軸状態の初期化、予測、更新
  - 観測ノイズ、過程ノイズ、可視性しきい値の設定解決
  - 距離、時刻変換、速度計算
- 非公開状態レコードと比較器
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
- 中核処理設定と実行時上書き契約
  - `TrackerEngineSettings`
  - `TrackerRuntimeOverrides`
  - `TrackerPublishOverrides`
  - `TrackerRobotTrackerOverrides`
  - `TrackerBallTrackerOverrides`
  - `TrackerKickDetectorOverrides`
  - `TrackerProfileSwitchRequest`

### `TrackerModelContracts.cs`

現状は約 230 行で、内部ワールドモデル、フィールド形状スナップショット、追跡状態、メタ状態、入力元検出、チーム列挙型が 1 ファイルにまとまっている。行数は `TrackerExecutionContracts.cs` より小さいが、公開 DTO が多く、TRACKER-033 の日本語コメント追加時に責務単位で分けた方が読みやすい。

主な責務:

- フレーム全体: `TrackerFrame`, `TrackerFrameMetadata`
- フィールド形状: `TrackerGeometrySnapshot`, `TrackerGeometryLineSegment`, `TrackerGeometryCircularArc`
- 追跡対象: `TrackedBallState`, `TrackedRobotState`, `TrackerTeam`
- AutoRef メタ情報: `KickEventState`, `BallContactState`, `BallLeftFieldState`
- 診断と再生の入力元: `TrackerSourceDetectionFrame`

### `TrackerPacketGenerator.cs`

現状は約 190 行で、`TrackerFrame` から公式の `TrackerWrapperPacket` への変換を担っている。巨大ではないが、`Tracker.Core` の公開境界として日本語コメント追加対象に含める。

主な責務:

- ラッパーのメタデータ設定
- `TrackedFrame` のフレーム番号と時刻設定
- 主ボールの先頭化と副ボールの安定順序
- ボール、ロボット、蹴られたボールのプロトコル変換
- `TrackerTeam` から公式の `Team` への変換
- `mm` / `mm/s` / `ns` から公式単位への変換
- 能力一覧の固定順出力

## 分割後の推奨ファイル構成

TRACKER-033 では名前空間を `Tracker.Core` のまま維持し、同一アセンブリ内のソース文書分割だけを行う。公開型名、メンバー名、アクセス範囲、値なしを許す形は変更しない。

### ファイル命名と部分型配置

ドット区切りファイル名はフレームワークやツールチェーンの慣習に限って許容する。例: `.csproj`、`.sln`、`.razor.cs`、`.razor.css`、`.g.cs`、`.Designer.cs`、`.AssemblyInfo.cs`、生成物、ビルド出力。

手書き C# の責務目印として `TypeName.Responsibility.cs` を使わない。部分クラスを責務別に分ける場合は型ごとのフォルダを作り、`TypeName/Responsibility.cs` 形式を基本にする。フォルダが型名、文書が責務名を表すため、名前空間と公開契約を維持したまま責務境界をパスで読める。

公開または内部のトップレベル型 1 つにつき 1 ファイルを基本にする。複数のトップレベル型を同居させるのは、親子 DTO、密結合した小さな列挙型や拡張、同じ外部スキーマの一部で単独参照されない場合に限る。

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

### 中核処理本体

- `Tracker/Tracker.Core/Engine/TrackerEngine/TrackerEngine.cs`
  - `TrackerEngine` のフィールド、コンストラクターなし状態、`Update`
  - プロファイル切り替え、フィールド形状更新、検出受付、確定出力呼び出しの最上位制御
- `Tracker/Tracker.Core/Engine/TrackerEngine/FrameCommit.cs`
  - `FlushCommittedFrames`
  - `ClearPendingStateAndAdvanceLateCutoff`
  - `CommitGroup`
  - フレームとイベント通知の組み立て
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
  - ボール追跡関連の非公開レコード
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
  - ロボットキー、観測、追跡関連の非公開レコード
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
  - 向きフィルター用の速度分散、過程分散、速度制限指定
- `Tracker/Tracker.Core/Engine/TrackerEngine/Settings.cs`
  - 非公開設定の解決補助処理
  - 可視性と品質減衰の補助処理
  - 単位と時刻の補助処理
  - 角度と距離の補助処理
- `Tracker/Tracker.Core/Engine/TrackedStateComparers.cs`
  - `TrackedBallComparer`
  - `TrackedRobotComparer`

`TrackerEngine` は部分クラスに変更してよい。ただし分割対象の補助処理は非公開のまま残し、内部公開にしてテストから直接触る形にはしない。テストは公開契約経由で挙動を固定する。

### 設定と上書き契約

- `Tracker/Tracker.Core/Configuration/TrackerEngineSettings.cs`
  - `TrackerEngineSettings`
- `Tracker/Tracker.Core/Configuration/TrackerRuntimeOverrides.cs`
  - `TrackerRuntimeOverrides`
  - `TrackerPublishOverrides`
  - `TrackerRobotTrackerOverrides`
  - `TrackerBallTrackerOverrides`
  - `TrackerKickDetectorOverrides`

既存の `Tracker.DebugHost` と `Tracker.CaptureReplay` が参照している型名は維持する。`Tracker.Core` 内のフォルダ移動によってこれらの参照名前空間は変えない。

### モデル契約

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

`TrackerPacketGenerator` は現状の 1 ファイル維持でよい。将来さらに肥大化した場合のみ、`Tracker/Tracker.Core/Proto/TrackerPacketGenerator/Balls.cs`、`Robots.cs`、`KickedBall.cs` のような型ごとのフォルダによる部分型分割を検討する。

## 日本語コメント追加基準

TRACKER-033 では XML ドキュメントコメントを日本語で追加する。固有名詞、型名、設定キー、プロトコル名、単位記号は英字のままでよい。

型、プロパティ、メソッドの説明は原則として XML ドキュメントコメントに寄せる。通常コメント `//` はメソッド内の複雑な処理、不変条件、順序制約の直前に限定し、型やメンバーの契約説明には使わない。

### 型、インターフェイス、列挙型

次の型には必ず `/// <summary>` を付ける。

- 公開または内部のクラス、インターフェイス、レコード、レコード構造体、列挙型
- `TrackerEngine` の非公開入れ子レコードと比較器のうち、分割後も非公開補助型として残す型

要約には「何を表すか」「どの境界で使うか」を 1 から 2 文で書く。実装手順や履歴は書かない。

例:

```csharp
/// <summary>
/// 生の vision packet から確定済み tracker frame と tracker event を生成する中核処理契約。
/// </summary>
```

### プロパティ

次のプロパティには必ず `/// <summary>` を付ける。

- 公開または内部 DTO のプロパティ
- 設定値プロパティ
- 単位、時刻、プロファイル、同一性、出力順序に関わるプロパティ
- 値なしを許す意味が領域上重要なプロパティ

要約では単位、値なし、0、空値の意味を明記する。特に次は省略しない。

- `Mm`, `MmPerS`, `Rad`, `RadPerS`, `Ns` の単位
- `DataTimestampNs` と `ProcessedAtNs` の違い
- `PrimaryBallTrackId` が値なしになる条件
- `KickedBall` が値なし、または `IsStillMoving == false` の扱い
- `RuntimeOverrides` がプロファイルの状態写しに対する一時上書きであること

### メソッド

次のメソッドには `/// <summary>` を付ける。

- 公開または内部メソッド
- `TrackerEngine.Update`
- 分割後に部分型ファイルの入口になる非公開メソッド
- 並べ替え、結合、カルマン、フィールド形状リセット、同一性割り当て、イベント通知、プロトコル変換の境界メソッド

単純な取得補助、数式そのものが明らかな非公開補助処理、1 行の包み込み処理には無理に付けない。ただし「なぜこの順序か」「どの挙動を固定するか」が読み手に伝わりにくい場合は、非公開メソッドでも要約または短い通常コメントを追加する。

### 通常コメント

通常コメントは複雑な処理の前にだけ置く。既存コードをなぞるだけのコメントは追加しない。

追加対象の例:

- プロファイル切り替えイベントを `WorldFrameCommitted` より前に通知する箇所
- `ReorderWindow` と `MergeWindow` による確定出力対象決定
- フィールド形状の大変更時リセットで確定待ち検出を捨てる箇所
- 主ボール継続を副ボール整列より優先する箇所
- カルマン更新で予測状態と直前位置を併用する箇所
- ロボットの遠方外れ値を同一ロボット ID の近傍観測で落とす箇所

## TRACKER-033 実行順序

1. 作業前に `Tracker/Design/Core/tracker-core-engine-detail-design.md` と `Tracker/Design/Core/tracker-architecture-plan.md` を読み、設計上の挙動固定点を確認する。
2. `TrackerExecutionContracts.cs` から公開契約を先に分離する。`ITrackerEngine`、結果、イベント、観測通知、プロファイル切り替え要求の型名と名前空間を変えない。
3. `TrackerEngine` を部分クラスにして、最上位 `Update` とフィールドを `Engine/TrackerEngine/TrackerEngine.cs` に残す。
4. 検出バッファーとフレーム確定を分離する。ここで `CommittedFrames` と `EmittedEvents` の順序が変わらないことを重点テストで確認する。
5. フィールド形状変換とフィールド形状リセット判定を分離する。確定待ち検出の消去、フレーム番号維持、遅延破棄境界の扱いを変えない。
6. ボール追跡を分離する。カメラ内追跡 ID、結合済み内部追跡 ID、主ボール継続、副ボール成長条件を変えない。
7. カルマン補助処理を分離する。`UpdateKalmanAxis` の引数と、予測状態と直前位置を使う基準を変えない。
8. ロボット追跡を分離する。同一ロボット ID の複数カメラ結合、遠方外れ値除去、向き折り返し補正の順序を変えない。
9. 接触、キック、ボール退場を分離する。イベント発火条件、直近接触窓、境界名を変えない。
10. 設定と実行時上書き契約を `Configuration` 配下へ分離し、`Tracker.RuntimeHost`、`Tracker.DebugHost`、`Tracker.CaptureReplay`、テストの参照がソース文書パスに依存していないことを確認する。
11. モデル契約を `Model` 配下へ分離し、公開プロパティの形を変えずに日本語 XML コメントを追加する。
12. `TrackerPacketGenerator` を `Proto` 配下へ移動し、主ボール先頭化、ロボット整列、能力一覧順、単位変換のコメントを追加する。
13. 全分割後に `TrackerExecutionContracts.cs` と `TrackerModelContracts.cs` が残る場合は、空の互換文書を残さず削除する。
14. 重点テストを実行し、`Tracker.Core` 契約、時系列契約、パケット生成器、コーディネーターのプロファイル切り替えの正常系が通ることを確認する。
15. 全テストを実行する。失敗した場合は、分割による参照漏れか、挙動差分かを切り分けてから修正する。

## 挙動を変えないための注意点

- 名前空間は `Tracker.Core` のまま維持する。
- 公開または内部の型名、メンバー名、プロパティ型、値なし許容、既定値を変えない。
- `ITrackerEngine.Update` の引数順、既定引数、値なし許容を変えない。
- `ProfileSwitched` は状態消去後、同じ結果内の `WorldFrameCommitted` より前に通知する。
- `GeometryReset` はフィールド形状の大変更時リセット時だけ通知し、フレーム番号と実行時同一性は維持する。
- 検出のイベント時刻は `TCapture > 0` を優先し、代替値は `TSent` のままにする。
- 確定待ち検出の確定出力順はイベント時刻、カメラ ID、入力フレーム番号の安定順を維持する。
- `ReorderWindow` と `MergeWindow` の意味を入れ替えない。
- 遅延パケット破棄は `lastCommittedGroupCloseTimestampNs` 以下のイベント時刻を状態更新に使わない。
- フィールド形状リセットやプロファイル切り替え時に、確定待ちバッファー、カメラ内追跡群、結合済みボール同一性、接触状態、退場状態、動作中キック状態、主ボールを消去する範囲を変えない。
- `nextCommittedFrameNumber` は状態消去で戻さない。
- ボールの主ボール継続判定を副ボール整列より優先する。
- 副ボールは可視性降順、最終可視時刻降順、内部追跡 ID 昇順の安定順を維持する。
- 副ボールの出力は新鮮な観測と成長済み観測数の条件を維持する。
- ボールとロボットのカルマン更新は予測状態を基準にし、観測速度算出に直前位置を使う。
- ロボット向き軸は位置 mm 用共分散を流用せず、ラジアン単位の観測分散、過程分散、角速度制限を使う。プロファイルのカルマン倍率系設定は既定値比でラジアン用基準値へ反映する。
- ロボット観測収集では、結合窓内の同一カメラ、チーム、ロボット ID 候補を既存同一 ID 追跡への近さで優先し、さらに既存別 ID 追跡近傍への突然の ID 変更候補を `RobotTracker.IdentitySwitchDistanceMm` で抑制する。ID が急に入れ替わることは小さな位置ずれより起きづらいという前提を関連付けに反映する。
- 設定上書き補助処理は値なしの意味と既定値を変えない。
- `TrackerPacketGenerator` の単位変換は `mm -> m`、`mm/s -> m/s`、`ns -> s` のままにする。
- `TrackerPacketGenerator` は `KickedBall` が `IsStillMoving == true` の場合だけ公式 `kicked_ball` を出す。
- `Capabilities` の内容と順序を変えない。

## 検証観点

TRACKER-033 の重点検証は、少なくとも次を含める。

- `Tracker.Core` 契約面
  - 公開型とプロパティが引き続き参照できること
  - `TrackerRuntimeOverrides` と `TrackerProfileSwitchRequest` のスナップショット形状が変わらないこと
- 時系列中核処理
  - イベント時刻順の並べ替え
  - 結合窓分割
  - 遅延パケット破棄
  - 0 フレーム結果
  - `WorldFrameCommitted` のイベント順序
- プロファイル切り替えとフィールド形状リセット
  - `ProfileSwitched` の通知順序
  - 制御だけの更新
  - フィールド形状の大変更時リセット
  - 状態消去後もフレーム番号が維持されること
- ボール追跡
  - 主ボール継続
  - 複数カメラのボール結合
  - 副ボールの安定順
  - カルマン基準が予測状態を使うこと
- ロボット追跡
  - 複数カメラのロボット結合
  - 同一ロボット ID の遠方外れ値で結合済みロボットが瞬間移動しないこと
  - 向きの折り返し補正と正規化
- AutoRef メタ情報
  - 接触変化
  - キック検出と蹴られたボール継続
  - ボール退場と境界交差
- パケット生成
  - 入力元名と UUID
  - 時刻変換
  - 主ボール先頭化
  - ロボット整列
  - 蹴られたボールの出力条件
  - 能力一覧

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

- 非公開補助処理の分割だけでも、部分型化の際に非公開入れ子レコードの参照順やファイル配置を誤るとコンパイルエラーになりやすい。
- コメント追加量が多いため、実装移動とコメント追加を同時に広く行うとレビューが難しくなる。TRACKER-033 では責務単位で分割し、各単位ごとに重点テストを挟む。
- `TrackerPacketGenerator` は行数が小さいため、過剰分割すると可読性が下がる。TRACKER-033 では移動とコメント追加を優先し、部分型分割は必要になった場合だけ行う。
