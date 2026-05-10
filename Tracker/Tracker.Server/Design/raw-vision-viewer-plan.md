# SSL_WrapperPacket Raw Vision Viewer 設計

## 目的

`Tracker.Server` が `SslProto` の生成型を使って SSL-Vision の `SSL_WrapperPacket` datagram を直接受信し、最新の detection / geometry 情報を Blazor UI 上で可視化できるようにする。

## スコープ

- 設定された vision endpoint に bind する UDP hosted service を追加する
- multicast address が設定されている場合は multicast join を行う
- `SSL_WrapperPacket.Parser.ParseFrom` で packet を decode する
- 最新 packet、最新 detection、最新 geometry、受信メタデータ、packet count、error count を singleton store に保持する
- `/` に field SVG、detection 情報、geometry calibration、raw packet JSON を表示する
- navigation は raw vision viewer 中心に保つ
- aggregate view と camera 別 latest-frame view の両方を raw vision UI で扱う

## 非スコープ

- `TrackerConnectionLib` は使わない
- packet の永続化はしない
- raw detection を超える tracking / filtering / world-model 解釈は入れない

## 設定

`appsettings.json` の `VisionReceiver` section を使う。

- `MulticastAddress`: 既定値 `224.5.23.2`
- `Port`: 受信 port
- `InterfaceAddress`: multicast join に使う local IPv4 address。未設定時は候補 interface を自動解決する
- `Profiles.<name>`: profile ごとの receiver override。`MulticastAddress` / `Port` / `InterfaceAddress` を同名 tracker profile に追従させたい場合に使う

resolver 規則:

- 起動時は `Tracker:ActiveProfileName` と同名の `VisionReceiver:Profiles.<name>` を優先する
- 同名 profile が無い場合は top-level `VisionReceiver` 値をそのまま使う
- runtime の tracker profile switch 完了後は、receiver も同じ profile 名で再解決し、必要なら socket を reopen する

## 受信設計

`VisionReceiverService` は hosted background service として動作する。

- IPv4 UDP socket を作成する
- address reuse を有効にする
- `IPAddress.Any` と設定 port に bind する
- cancellation まで datagram を継続受信する
- receiver 設定が切り替わったら現在の receive loop を cancel し、新しい設定で socket を開き直す

設定された address が multicast の場合、group membership は次の規則で解決する。

- `InterfaceAddress` が設定されている場合、その IPv4 address のみを使う
- 未設定の場合、利用可能な local IPv4 interface を列挙して順に join を試行する
- 少なくとも 1 つ成功すれば受信開始を継続する
- 一部 interface の失敗は warning log に残す

decode 成功時は `VisionPacketStore` を更新し、失敗時は error count を増やし、直前の正常 state を保持する。

## Store 設計

`VisionPacketStore` は UI 参照用の thread-safe state を保持する。

- 最新 wrapper packet
- 最新 detection frame
- camera ごとの最新 detection snapshot
- aggregate 表示用に統合した balls / yellow robots / blue robots
- 最新 geometry data
- packet count
- error count
- remote endpoint
- receive timestamp
- 最新 parse error message

UI には immutable snapshot を返し、描画中に lock を保持しない。

## Proto 入力

raw vision viewer が直接使う主な proto 入力は次の通り。

- `SSL_WrapperPacket`
  - 受信 datagram 全体
  - `Detection` と `Geometry` を内包する最上位 packet
- `SSL_DetectionFrame`
  - camera 単位の raw detection
  - `FrameNumber`, `CameraId`, `Balls`, `RobotsYellow`, `RobotsBlue` を使う
- `SSL_DetectionBall`
  - ball 描画と詳細表示に使う
  - 主に `X`, `Y`, `Z`, `PixelX`, `PixelY`, `Confidence`
- `SSL_DetectionRobot`
  - robot 描画と詳細表示に使う
  - 主に `RobotId`, `X`, `Y`, `Orientation`, `PixelX`, `PixelY`, `Confidence`
- `SSL_GeometryData`
  - field geometry 全体
  - `Field` と `Calib` を使う
- `SSL_GeometryFieldSize`
  - field の寸法と line / arc 情報
  - 主に `FieldLength`, `FieldWidth`, `GoalWidth`, `GoalDepth`, `BoundaryWidth`, `BoundaryWidthGoalLine`, `PenaltyAreaDepth`, `PenaltyAreaWidth`, `CenterCircleRadius`, `LineThickness`, `FieldLines`, `FieldArcs`
- `SSL_GeometryCameraCalibration`
  - calibration table 表示に使う
  - 主に `CameraId`, `FocalLength`, `PrincipalPointX`, `PrincipalPointY`, `PixelImageWidth`, `PixelImageHeight`

## Field 投影

`VisionFieldProjection` は field の millimeter 座標を SVG viewport に写像する。

- geometry がある場合はその field dimensions を使う
- geometry がまだない場合は default の競技場寸法を使う
- `(0, 0)` は viewport center に対応する
- field 本体だけでなく、boundary と goal depth が見切れないよう outer margin を加味する

## コンポーネント構成

raw vision viewer の主要コンポーネントは次の通り。

- `Home.razor`
  - 画面全体の親
  - `VisionPacketStore.GetSnapshot()` の結果を定期取得し、compact header と左右ペインを構成する
- `VisionFieldCanvas.razor`
  - field SVG の親コンポーネント
  - zoom / pan 状態、boundary 背景、field 本体、子 marker の配置、axis overlay、cursor 座標 overlay を担当する
- `VisionFieldLines.razor`
  - field line / arc / goal の描画を担当する
  - `FieldLines` / `FieldArcs` がある場合はそれを優先し、不足時は geometry 寸法から fallback 描画する
- `VisionBallMarker.razor`
  - `SSL_DetectionBall` 1 件を SVG circle として描く
- `VisionRobotMarker.razor`
  - `SSL_DetectionRobot` 1 件を robot shape として描く
  - team 色、前面 gap、nose marker、label を担当する
- `VisionDetailsPanel.razor`
  - JSON / balls / robots / geometry calibration の右ペイン表示を担当する
- `VisionPalette.cs`
  - team color と marker stroke color の定義を一箇所に集約する
- `VisionRenderOptions.cs`
  - robot radius など、将来設定から変更したい描画パラメータの受け口

## コンポーネント入力

各コンポーネントが受ける最小入力は次の通り。

### Home.razor

- `VisionPacketSnapshot`
  - store から取得した UI 用 snapshot
- `selectedViewKey`
  - aggregate / camera 切替状態
- sidebar 折りたたみ状態を前提に field-first の main content を構成する

### VisionFieldCanvas.razor

- `SSL_GeometryData? Geometry`
- `IReadOnlyList<SSL_DetectionBall> Balls`
- `IReadOnlyList<SSL_DetectionRobot> RobotsYellow`
- `IReadOnlyList<SSL_DetectionRobot> RobotsBlue`
- `VisionRenderOptions RenderOptions`
- cursor 座標表示に必要な hover state と canvas size

### VisionFieldLines.razor

- `VisionFieldProjection Projection`
- `SSL_GeometryFieldSize? Field`

### VisionBallMarker.razor

- `VisionFieldProjection Projection`
- `SSL_DetectionBall Ball`

### VisionRobotMarker.razor

- `VisionFieldProjection Projection`
- `SSL_DetectionRobot Robot`
- `string ClassName`
- `VisionRenderOptions RenderOptions`

### VisionDetailsPanel.razor

- `string ViewLabel`
- `string FrameLabel`
- `string CameraLabel`
- `string SourceLabel`
- `string RawJson`
- `IReadOnlyList<VisionCameraSnapshot> Cameras`
- `string SelectedViewKey`
- `EventCallback<string> OnSelectView`
- `SSL_GeometryData? Geometry`
- `IReadOnlyList<SSL_DetectionBall> Balls`
- `IReadOnlyList<SSL_DetectionRobot> RobotsYellow`
- `IReadOnlyList<SSL_DetectionRobot> RobotsBlue`

## UI 方針

root page では次を表示する。

- receiver status と最新 receive metadata
- field-first の SVG 表示
- raw JSON
- balls / robots / geometry calibration の詳細表示

field presentation は `RoboCup-SSL/ssl-vision-client` の方向性を踏襲する。

- field canvas を主表示にする
- boundary-aware の field background
- wheel zoom と drag pan
- field 面積を優先するため、画面 title は省略し、source selector は field 上端から外す
- +X / +Y 方向が分かる axis overlay を field 上に固定表示する
- cursor 座標は cursor の上下で表示位置を切り替え、field 視認性を落とさない
- desktop sidebar は viewer 表示面積確保のため折りたたみ可能にする

## レイアウト追補

- `Home.razor` は大きな title block を持たず、status と main content を優先する
- source selector は `VisionDetailsPanel.razor` 側へ移し、field の縦方向面積を確保する
- `VisionFieldCanvas.razor` は field 本体に加えて axis overlay と cursor coordinate overlay を管理する
- cursor coordinate overlay は proto 由来の field geometry と `VisionFieldProjection` の逆写像から求める
- sidebar 折りたたみは layout レベルで扱い、viewer 専用コンポーネントへ閉じ込めない

## テスト方針

- `VisionPacketStore` が detection-only packet を保持できる
- `VisionPacketStore` が geometry-only packet を保持できる
- `VisionPacketStore` が camera ごとの latest state と aggregate state を返せる
- `VisionPacketStore` が decode failure 時に error count を増やす
- `VisionReceiverService.ResolveMulticastJoinAddresses` が configured address と auto discovery を正しく処理する
- `VisionFieldProjection` が `(0, 0)` を center に写像する
- `VisionFieldProjection` が field / boundary / goal depth を含めても viewport 内に収める

## 前提

- raw vision information は `vision/ssl_vision_wrapper.proto` の `SSL_WrapperPacket` を指す
- default の SSL-Vision multicast endpoint は一般には `224.5.23.2:10006` だが、runtime 設定で変更できる
- 既存の unrelated worktree change は保護する
