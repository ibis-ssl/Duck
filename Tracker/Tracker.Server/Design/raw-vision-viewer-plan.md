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

## Issue #10 split / overlay source 設計

Vision 画面の split / overlay で選択できる source 候補は次の 4 種類に固定する。

- `Raw Aggregate`
  - `VisionPacketStore` の aggregate 表示用 snapshot を使う
  - camera ごとの最新 detection を UI 表示用に統合した raw SSL-Vision source として扱う
- `Raw Camera`
  - `VisionPacketStore` の camera ごとの latest detection snapshot を使う
  - camera ID を選択肢の内部 key に含め、表示 label だけで source を識別しない
- `Tracked`
  - ibis tracker の `TrackedSnapshotStore` から得た latest `TrackerFrame` を `TrackedVisionViewState` 相当の field 描画 DTO へ変換して使う
  - raw detection ではなく ibis tracker 出力として扱う
- `3rd party tracker`
  - `MultiTrackerManager<TrackerPacketAdapter>` から受けた external tracker live state を使う
  - UI は `MultiTrackerManager` の mutable state を直接読まず、live UI 用の immutable snapshot store または composer を必ず挟む

live 比較では、厳密な同一 packet timestamp や全 source 共通の同一 receive callback は要求しない。raw SSL-Vision、ibis tracker、3rd party tracker は受信 stream と更新 callback が異なるため、ここを contract にすると通常表示の実装が過剰に結合する。採用方針は、1 回の `UI render tick` で各 source の latest immutable snapshot を固定し、その composite snapshot を split / overlay の Layer A/B に渡すことである。

`UI render tick` の composite snapshot は次を保持する。

- render tick ID または `SampledAt`
- source key と表示 label
- source ごとの receive timestamp / frame timestamp / packet count など、時刻差を説明する metadata
- balls / robots / geometry reference / missing reason を含む immutable source snapshot

3rd party tracker の live 接続では、`MultiTrackerManager<TrackerPacketAdapter>` から external / source label ごとの latest packet を受ける。ただし `TrackerState` や protobuf packet 参照を UI が直接保持しない。`ExternalTrackerSnapshotStore` または `VisionLiveComparisonSnapshot` composer のような境界で clone / DTO 化し、描画中に state が変わらない immutable snapshot として扱う。`TrackerPacketSnapshotLogWriter` や CaptureOn sidecar writer を Vision live store として使う方針は不採用とする。これは CaptureOn session 保存用の仕組みであり、CaptureOff の通常 live Vision 画面では更新 source として成立しないためである。

geometry 基準は raw geometry 優先とする。`Raw Aggregate` または選択中の `Raw Camera` で得られる最新 `SSL_GeometryData` を overlay 全体の field 基準に使い、raw geometry がまだ無い場合のみ `Tracked` の geometry へ fallback する。`3rd party tracker` packet から field geometry を復元する方針は不採用とする。external tracker packet は比較対象の object state であり、field calibration の責任を持たせると source ごとの座標比較の意味が曖昧になる。

split / overlay の UI 挙動は diagnostics に寄せる。

- split mode は Layer A と Layer B を左右に並べる
- overlay mode は 1 つの field に Layer A/B を重ねる
- details は source ごとの summary、timestamp metadata、missing reason、raw/tracked/3rd party の違いを確認できる構成にする
- legend は diagnostics と同じく layer name、source label、visibility toggle、ready / missing state を表示する
- layer visibility は Layer A/B ごとに切り替えられる
- Layer A/B が同じ source を選んだ場合は same-source として 1 layer 表示にまとめ、重複描画で誤差があるように見せない
- 片方の layer が missing でも、ready な layer は残して表示する
- missing layer は field 全体を空にせず、legend / details に missing reason を出す

## Diagnostics time-sync 方針

diagnostics replay / comparison は selected replay timeline tick を同期基準にする。通常経路では、Vision/Input と ibis tracker は selected tick の render frame から得た snapshot を使い、3rd party tracker は同じ `ReplayTimelineIndex` の `saved-session-alignment` record を使う。このため、新規 CaptureOn session で alignment sidecar が存在し、対象 source の alignment record が selected tick にある場合は、Vision、ibis tracker、3rd party tracker を同じ replay timeline tick の比較として扱える。

selected tick に対象 `3rd party tracker` source の alignment record が無い場合でも、表示と比較を消さない。採用方針は、同じ source の selected tick 以前に存在する最新の `latest-before snapshot` を Field source と comparison に使うことである。UI / comparison は matching rule が `latest-before` であること、source snapshot の実際の `receivedAt`、selected tick との差分 delta、stale / latest-before 状態を明示する。これにより、対象 source が selected tick で未更新でも、ユーザーは直前まで得られていた tracker 状態を raw / ibis tracker と比較できる。

`latest-before snapshot` を使う場合も、replay / comparison の基準 timeline は selected replay timeline tick のまま固定する。source ごとに timeline cursor をずらしたり、表示上の selected time を tracker source 側へスライドしたりしない。Field と comparison は「selected tick に対して、この source は直前 sample を hold している」として表示し、delta は selected tick と hold した source snapshot の差として扱う。これにより、表示が消えることを避けつつ、時間軸が source ごとにずれて異なる時刻のものを同時刻扱いで表示しているように見える状態を避ける。

selected tick 以前に同じ source の snapshot が一切無い場合だけ、Field source は `CandidateMissing`、comparison は `NoCandidateSnapshot` 相当の missing 表示にする。この場合も Field 全体は消さず、ready な layer は残し、legend / details に missing reason を出す。future / later snapshot への fallback は行わない。未来 tick の tracker 状態を現在 tick の比較へ混ぜると、replay timeline の因果関係が崩れ、comparison delta が実際より良く見えるためである。diagnostics-line alignment や nearest timestamp は、selected tick 以前の同一 source snapshot を探すための補助 index として使ってよいが、selected tick より後の snapshot は候補に含めない。この挙動は RAW-VISION-014 の TDD contract と RAW-VISION-015 の修正対象にする。

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
- `Diagnostics.razor` の render snapshot 表示は、Vision Input / Tracker Output の field 表示領域と下部 detail 領域の境界をドラッグで変更できるようにする
- diagnostics の field/detail 比率は viewport 高さに依存した固定上限だけにせず、4K など高解像度環境で field を大きく広げられる上限を持つ
- detail 領域は縮小時も最低高さとスクロールを維持し、Vision Input / Tracker Output の文字列確認を壊さない
- `Diagnostics.razor` の左側 frame timeline は、右側 detail との境界をドラッグして幅を変更できるようにする
- frame timeline は右側 field/detail 表示領域を広げたい場合に小さくでき、最小幅でも frame 選択操作と省略表示を維持する
- `MainLayout.razor.css` と `NavMenu.razor.css` は raw vision / diagnostics の濃色 green UI と同じ配色・密度を使い、default Blazor template 由来の青紫 gradient や浮いた navigation 表現を残さない
- side navigation の active / hover / collapsed / mobile toggle は既存操作を維持しつつ、viewer と同じ border、background、text color の階調で表現する
- `Diagnostics.razor` の timeline scrubber には再生、停止、早送り controls を置き、選択 entry を順方向に進める
- 通常再生は log entry の timestamp 差分を使い、上限 clamp なしで実際の記録速度に合わせて進める
- 再生中は再生ボタンを停止ボタン表示へ切り替え、早送り中は早送りボタンを停止ボタン表示へ切り替える
- 再生/早送りは最後の entry に到達したら停止して先頭 entry に戻し、log 切替や entry 不在時には playback state を停止状態へ戻す

## テスト方針

- `VisionPacketStore` が detection-only packet を保持できる
- `VisionPacketStore` が geometry-only packet を保持できる
- `VisionPacketStore` が camera ごとの latest state と aggregate state を返せる
- `VisionPacketStore` が decode failure 時に error count を増やす
- `VisionReceiverService.ResolveMulticastJoinAddresses` が configured address と auto discovery を正しく処理する
- `VisionFieldProjection` が `(0, 0)` を center に写像する
- `VisionFieldProjection` が field / boundary / goal depth を含めても viewport 内に収める
- diagnostics render snapshot の field/detail 可変高さは、最小値・最大値・drag delta の clamp を単体テストで確認する
- diagnostics frame timeline の可変幅は、最小値・最大値・drag delta の clamp を単体テストで確認する
- diagnostics timeline playback は、次 index 計算、最後での停止と先頭復帰、通常再生と早送り step、timestamp 差分に基づく実速度 interval を単体テストで確認する
- Vision split / overlay contract は、source 候補、split / overlay mode、Layer A/B source selection、layer visibility、same-source 1 layer 化、missing layer でも ready layer を残す挙動、diagnostics 寄せ legend / details を単体テストで先に固定する
- Vision live comparison contract は、1 回の `UI render tick` で `Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` の latest immutable snapshot を固定し、後続 store 更新で描画中 snapshot が変化しないことを単体テストで先に固定する
- 3rd party tracker live source contract は、`MultiTrackerManager<TrackerPacketAdapter>` の mutable state を UI が直接読まず、immutable snapshot store / composer を通して source option と field DTO を作ることを単体テストで先に固定する
- diagnostics time-sync regression は、selected `ReplayTimelineIndex` に対象 3rd party source の alignment record が無い場合でも、selected replay timeline tick 自体は動かさず、同じ source の selected tick 以前の `latest-before snapshot` を Field source と comparison に使い、matching rule、source snapshot の実際の `receivedAt`、selected tick との差分 delta、stale / latest-before 状態を表示することを単体テストで先に固定する
- diagnostics missing regression は、selected tick 以前に同じ source の snapshot が一切無い場合だけ `CandidateMissing` / `NoCandidateSnapshot` 相当になり、future / later snapshot へ fallback せず、ready layer は残ることを単体テストで先に固定する

## 前提

- raw vision information は `vision/ssl_vision_wrapper.proto` の `SSL_WrapperPacket` を指す
- default の SSL-Vision multicast endpoint は一般には `224.5.23.2:10006` だが、runtime 設定で変更できる
- 既存の unrelated worktree change は保護する

## 用語

- `Raw Aggregate`
  - raw SSL-Vision の camera ごとの latest detection を UI 表示用に統合した source。
- `Raw Camera`
  - 特定 camera ID の raw SSL-Vision latest detection を表示する source。
- `Tracked`
  - ibis tracker が生成した `TrackerFrame` を Vision 表示用 DTO に変換した source。
- `3rd party tracker`
  - ibis tracker 以外の外部 tracker から受けた tracker packet source。
- `UI render tick`
  - Blazor UI が 1 回の表示更新を行う単位。この tick 内で各 source の latest snapshot を固定し、split / overlay へ渡す。
- `immutable snapshot`
  - 描画中に内容が変わらないよう clone / DTO 化された読み取り専用 snapshot。
- `MultiTrackerManager`
  - `TrackerConnectionLib` の tracker state 管理コンポーネント。own / external / unknown tracker の latest state を保持する。
- `TrackerPacketAdapter`
  - 3rd party tracker packet を `MultiTrackerManager` で扱うための adapter。
- `ReplayTimelineIndex`
  - diagnostics replay の selected timeline tick を識別する index。
- `saved-session-alignment`
  - CaptureOn session に保存された、replay timeline tick と tracker source snapshot の対応 record。
- `latest-before snapshot`
  - selected replay timeline tick に対象 source の alignment record が無い場合に使う、同じ source で selected tick 以前に存在する最新 snapshot。future / later snapshot は含めない。timeline cursor は selected tick のまま固定し、この snapshot は直前 sample の hold として扱う。
- `CandidateMissing`
  - Field source に選択 source の候補 snapshot が無いことを示す missing 状態。
- `NoCandidateSnapshot`
  - comparison に選択 source の候補 snapshot が無いことを示す missing 状態。
- `VisionFieldCanvas`
  - Vision 画面の field SVG 親コンポーネント。geometry、balls、robots、zoom / pan、axis / cursor overlay を担当する。
