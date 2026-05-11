# Tracker Core engine 詳細設計

## 目的

TRACKER-033 で `Tracker.Core` の巨大ファイルを責務別に分割し、主要な class / property / method に日本語コメントを追加できるように、Core engine 側の分割境界、実行順序、挙動維持の確認観点を固定する。

この設計は保守性改善の詳細設計であり、TRACKER-033 では tracker の追跡挙動、公開 contract、proto 出力、設定値の意味を変更しない。

## 対象範囲

- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Core/TrackerModelContracts.cs`
- `Tracker/Tracker.Core/TrackerPacketGenerator.cs`

対象外:

- Server / CLI / UI 側の詳細設計
- test file の分割設計
- 追跡アルゴリズム、設定値、proto 出力の仕様変更

## 現状の巨大ファイルと責務

### `TrackerExecutionContracts.cs`

現状は約 2200 行を超え、次の責務が 1 ファイルに混在している。

- engine の公開契約
  - `ITrackerEngine`
  - `TrackerUpdateResult`
  - `TrackerEngineDiagnostics`
  - `TrackerEvent`
  - `TrackerEventKind`
  - `ITrackerObserver`
- `TrackerEngine` 本体
  - `Update` による profile switch、geometry 更新、detection buffer 追加、flush 実行
  - pending detection buffer と event-time reorder / merge window の管理
  - geometry 大変更 reset と latest state clear
  - world frame commit と event emit
- ball tracking
  - camera-local ball track の観測更新、予測、visibility decay
  - multi-camera ball cluster と merged ball identity の維持
  - primary ball の安定化と secondary ball の出力順序
- robot tracking
  - camera-local robot observation の収集
  - 同一 robot id の遠方外れ値除去
  - robot track の観測更新、予測、multi-camera merge
  - orientation unwrap / normalize
- AutoRef 向け meta event
  - ball contact
  - kick detection / kicked ball state 継続
  - ball left field / boundary crossing projection
- geometry 変換
  - `SSL_GeometryData` から `TrackerGeometrySnapshot` への変換
  - line / arc の snapshot 化
- Kalman と数値 helper
  - axis state の初期化、predict、update
  - measurement noise / process noise / visibility threshold の settings 解決
  - distance、timestamp 変換、速度計算
- private state record / comparer
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
- engine settings / runtime override contract
  - `TrackerEngineSettings`
  - `TrackerRuntimeOverrides`
  - `TrackerPublishOverrides`
  - `TrackerRobotTrackerOverrides`
  - `TrackerBallTrackerOverrides`
  - `TrackerKickDetectorOverrides`
  - `TrackerProfileSwitchRequest`

### `TrackerModelContracts.cs`

現状は約 230 行で、内部 world model、geometry snapshot、tracked state、meta state、source detection、team enum が 1 ファイルにまとまっている。行数は `TrackerExecutionContracts.cs` より小さいが、public DTO が多く、TRACKER-033 の日本語コメント追加時に責務単位で分けた方が読みやすい。

主な責務:

- frame 全体: `TrackerFrame`, `TrackerFrameMetadata`
- geometry: `TrackerGeometrySnapshot`, `TrackerGeometryLineSegment`, `TrackerGeometryCircularArc`
- tracked object: `TrackedBallState`, `TrackedRobotState`, `TrackerTeam`
- AutoRef meta: `KickEventState`, `BallContactState`, `BallLeftFieldState`
- diagnostics / replay source: `TrackerSourceDetectionFrame`

### `TrackerPacketGenerator.cs`

現状は約 190 行で、`TrackerFrame` から official `TrackerWrapperPacket` への変換を担っている。巨大ではないが、Core の公開境界として日本語コメント追加対象に含める。

主な責務:

- wrapper metadata の設定
- `TrackedFrame` の frame number / timestamp 設定
- primary ball 先頭化と secondary ball の安定順序
- ball / robot / kicked ball の proto 変換
- `TrackerTeam` から official `Team` への変換
- `mm` / `mm/s` / `ns` から official 単位への変換
- capability の固定順出力

## 分割後の推奨ファイル構成

TRACKER-033 では namespace を `Tracker.Core` のまま維持し、同一 assembly 内の source file 分割だけを行う。public type 名、member 名、accessibility、nullable shape は変更しない。

### ファイル命名と partial 配置

dot 区切りファイル名は framework / toolchain 慣習に限って許容する。例: `.csproj`、`.sln`、`.razor.cs`、`.razor.css`、`.g.cs`、`.Designer.cs`、`.AssemblyInfo.cs`、generated / build output。

手書き C# の責務 marker として `TypeName.Responsibility.cs` を使わない。partial class を責務別に分ける場合は type-owned folder を作り、`TypeName/Responsibility.cs` 形式を基本にする。folder が型名、file が責務名を表すため、namespace と public contract を維持したまま責務境界を path で読める。

1 public / internal top-level type 1 file を基本にする。複数 top-level type を同居させるのは、親子 DTO、密結合した small enum / extension、同じ external schema の一部で単独参照されない場合に限る。

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

### engine 本体

- `Tracker/Tracker.Core/Engine/TrackerEngine/TrackerEngine.cs`
  - `TrackerEngine` の field、constructor なし状態、`Update`
  - profile switch、geometry 更新、detection 受付、flush 呼び出しの最上位 orchestration
- `Tracker/Tracker.Core/Engine/TrackerEngine/FrameCommit.cs`
  - `FlushCommittedFrames`
  - `ClearPendingStateAndAdvanceLateCutoff`
  - `CommitGroup`
  - frame / event emit の組み立て
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
  - ball track 関連 private record
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
  - robot key / observation / track 関連 private record
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
- `Tracker/Tracker.Core/Engine/TrackerEngine/Settings.cs`
  - private settings 解決 helper
  - visibility / quality decay helper
  - unit / timestamp helper
  - angle / distance helper
- `Tracker/Tracker.Core/Engine/TrackedStateComparers.cs`
  - `TrackedBallComparer`
  - `TrackedRobotComparer`

`TrackerEngine` は `partial sealed class` に変更してよい。ただし分割対象の helper は private のまま残し、internal 化して test から直接触る形にはしない。test は public contract 経由で挙動を固定する。

### settings / override contract

- `Tracker/Tracker.Core/Configuration/TrackerEngineSettings.cs`
  - `TrackerEngineSettings`
- `Tracker/Tracker.Core/Configuration/TrackerRuntimeOverrides.cs`
  - `TrackerRuntimeOverrides`
  - `TrackerPublishOverrides`
  - `TrackerRobotTrackerOverrides`
  - `TrackerBallTrackerOverrides`
  - `TrackerKickDetectorOverrides`

既存の `Tracker.Server` と `Tracker.CaptureReplay` が参照している型名は維持する。フォルダ移動によって namespace は変えない。

### model contract

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

### packet generator

- `Tracker/Tracker.Core/Proto/TrackerPacketGenerator.cs`
  - `TrackerPacketGenerator`

`TrackerPacketGenerator` は現状の 1 ファイル維持でよい。将来さらに肥大化した場合のみ、`Tracker/Tracker.Core/Proto/TrackerPacketGenerator/Balls.cs`、`Robots.cs`、`KickedBall.cs` のような type-owned folder による partial 分割を検討する。

## 日本語コメント追加基準

TRACKER-033 では XML documentation comment を日本語で追加する。proper noun、型名、設定 key、proto 名、単位記号は英字のままでよい。

class / property / method の説明は原則として XML documentation comment に寄せる。通常コメント `//` は method 内の複雑な block、不変条件、順序制約の直前に限定し、type や member の契約説明には使わない。

### class / interface / enum

次の型には必ず `/// <summary>` を付ける。

- public / internal の class、interface、record、record struct、enum
- `TrackerEngine` の private nested record / comparer のうち、分割後も private helper として残す型

summary には「何を表すか」「どの境界で使うか」を 1 から 2 文で書く。実装手順や履歴は書かない。

例:

```csharp
/// <summary>
/// raw vision packet から確定済み tracker frame と tracker event を生成する Core engine 契約。
/// </summary>
```

### property

次の property には必ず `/// <summary>` を付ける。

- public / internal DTO の property
- 設定値 property
- unit、timestamp、profile、identity、出力順序に関わる property
- nullable の意味が domain 上重要な property

summary では単位と null / 0 / empty の意味を明記する。特に次は省略しない。

- `Mm`, `MmPerS`, `Rad`, `RadPerS`, `Ns` の単位
- `DataTimestampNs` と `ProcessedAtNs` の違い
- `PrimaryBallTrackId` が null の条件
- `KickedBall` が null または `IsStillMoving == false` の扱い
- `RuntimeOverrides` が profile snapshot に対する一時上書きであること

### method

次の method には `/// <summary>` を付ける。

- public / internal method
- `TrackerEngine.Update`
- 分割後に partial file の入口になる private method
- reorder / merge / Kalman / geometry reset / identity assignment / event emit / proto 変換の境界 method

単純な getter helper、数式そのものが明らかな private helper、1 行 wrapper には無理に付けない。ただし「なぜこの順序か」「どの挙動を固定するか」が読み手に伝わりにくい場合は、private method でも summary または短い通常コメントを追加する。

### 通常コメント

通常コメントは複雑な処理 block の前にだけ置く。既存コードをなぞるだけのコメントは追加しない。

追加対象の例:

- profile switch event を `WorldFrameCommitted` より前に emit する箇所
- `ReorderWindow` と `MergeWindow` による flush 対象決定
- geometry 大変更 reset で pending detection を捨てる箇所
- ball primary 継続を secondary sort より優先する箇所
- Kalman update で predicted state と previous position を併用する箇所
- robot の遠方外れ値を同一 robot id の近傍観測で落とす箇所

## TRACKER-033 実行順序

1. 作業前に `Tracker/Tracker.Core/Design/tracker-core-engine-detail-design.md` と `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` を読み、設計上の挙動固定点を確認する。
2. `TrackerExecutionContracts.cs` から公開契約を先に分離する。`ITrackerEngine`、result / event、observer、profile switch request の型名と namespace を変えない。
3. `TrackerEngine` を `partial sealed class` にして、最上位 `Update` と field を `Engine/TrackerEngine/TrackerEngine.cs` に残す。
4. detection buffer と frame commit を分離する。ここで `CommittedFrames` と `EmittedEvents` の順序が変わらないことを focused test で確認する。
5. geometry 変換と geometry reset 判定を分離する。pending detection clear、frame number 維持、late cutoff の扱いを変えない。
6. ball tracking を分離する。camera-local track id、merged internal track id、primary ball 継続、secondary ball 成長条件を変えない。
7. Kalman helper を分離する。`UpdateKalmanAxis` の引数と、predicted state / previous position を使う baseline を変えない。
8. robot tracking を分離する。same robot id の multi-camera merge、遠方外れ値除去、orientation unwrap の順序を変えない。
9. contact、kick、ball left field を分離する。event 発火条件、recent contact window、boundary 名を変えない。
10. settings / runtime override contract を `Configuration` 配下へ分離し、`Tracker.Server`、`Tracker.CaptureReplay`、tests の参照が source file path に依存していないことを確認する。
11. model contract を `Model` 配下へ分離し、public property shape を変えずに日本語 XML コメントを追加する。
12. `TrackerPacketGenerator` を `Proto` 配下へ移動し、primary ball 先頭化、robot sort、capability 順、単位変換のコメントを追加する。
13. 全分割後に `TrackerExecutionContracts.cs` と `TrackerModelContracts.cs` が残る場合は、空の compatibility file を残さず削除する。
14. focused tests を実行し、Core contract、temporal contract、packet generator、coordinator profile switch の正常系が通ることを確認する。
15. full test を実行する。失敗した場合は、分割による参照漏れか、挙動差分かを切り分けてから修正する。

## 挙動を変えないための注意点

- namespace は `Tracker.Core` のまま維持する。
- public / internal 型名、member 名、property type、nullable、既定値を変えない。
- `ITrackerEngine.Update` の引数順、default parameter、null 許容を変えない。
- `ProfileSwitched` は state clear 後、同じ result 内の `WorldFrameCommitted` より前に emit する。
- `GeometryReset` は geometry 大変更 reset 時だけ emit し、frame number と runtime identity は維持する。
- detection の event time は `TCapture > 0` を優先し、fallback は `TSent` のままにする。
- pending detection の flush 順は event time、camera id、source frame number の安定順を維持する。
- `ReorderWindow` と `MergeWindow` の意味を入れ替えない。
- late packet drop は `lastCommittedGroupCloseTimestampNs` 以下の event time を状態更新に使わない。
- geometry reset / profile switch 時に pending buffer、camera-local tracks、merged ball identity、contact / left-field state、active kick state、primary ball を clear する範囲を変えない。
- `nextCommittedFrameNumber` は state clear で戻さない。
- ball の primary 継続判定を secondary sort より優先する。
- secondary ball は visibility 降順、last visible timestamp 降順、internal track id 昇順の安定順を維持する。
- secondary ball の出力は fresh observation と grown-up observation count の条件を維持する。
- ball / robot の Kalman update は predicted state を基準にし、observed velocity 算出に previous position を使う。
- settings override helper は null の意味と default 値を変えない。
- `TrackerPacketGenerator` の unit conversion は `mm -> m`、`mm/s -> m/s`、`ns -> s` のままにする。
- `TrackerPacketGenerator` は `KickedBall` が `IsStillMoving == true` の場合だけ official `kicked_ball` を出す。
- `Capabilities` の内容と順序を変えない。

## 検証観点

TRACKER-033 の focused verification は、少なくとも次を含める。

- Core contract surface
  - public 型と property が引き続き参照できること
  - `TrackerRuntimeOverrides` と `TrackerProfileSwitchRequest` の snapshot shape が変わらないこと
- temporal engine
  - event-time reorder
  - merge window 分割
  - late packet drop
  - 0-frame result
  - `WorldFrameCommitted` event 順序
- profile switch / geometry reset
  - `ProfileSwitched` の emit 順序
  - control-only update
  - geometry 大変更 reset
  - state clear 後も frame number が維持されること
- ball tracking
  - primary ball 継続
  - multi-camera ball merge
  - secondary ball の安定順
  - Kalman baseline が予測状態を使うこと
- robot tracking
  - multi-camera robot merge
  - same robot id の遠方外れ値で merged robot が瞬間移動しないこと
  - orientation unwrap / normalize
- AutoRef meta
  - contact changed
  - kick detected / kicked ball 継続
  - ball left field と boundary crossing
- packet generation
  - source name / uuid
  - timestamp conversion
  - primary ball first
  - robot sort
  - kicked ball output condition
  - capabilities

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

- private helper の分割だけでも `partial` 化の際に private nested record の参照順や file 配置を誤ると compile error になりやすい。
- コメント追加量が多いため、実装移動とコメント追加を同時に広く行うと review が難しくなる。TRACKER-033 では責務単位で分割し、各単位ごとに focused test を挟む。
- `TrackerPacketGenerator` は行数が小さいため、過剰分割すると可読性が下がる。TRACKER-033 では移動とコメント追加を優先し、partial 分割は必要になった場合だけ行う。
