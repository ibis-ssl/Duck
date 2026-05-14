# SSL_WrapperPacket Raw Vision Viewer 設計

## 目的

`Tracker.DebugHost`[^tracker-debug-host] が `SslProto` の生成型を使って SSL-Vision の `SSL_WrapperPacket` datagram を直接受信し、最新の detection / geometry 情報を Blazor UI 上で可視化できるようにする。実装移行前の project 名は `Tracker.Server` だが、本設計上の責務名は `Tracker.DebugHost` とする。

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
- 本番寄りの tracker operation と将来 AutoRef mode は `Tracker.RuntimeHost` の責務とし、DebugHost の Web rendering / diagnostics logging から切り離す

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

Vision 画面の split / overlay で選択できる source[^source-term] 候補は次の 4 種類に固定する。

- `Raw Aggregate`[^raw-aggregate]
  - `VisionPacketStore` の aggregate 表示用 snapshot を使う
  - camera ごとの最新 detection を UI 表示用に統合した raw SSL-Vision source として扱う
- `Raw Camera`[^raw-camera]
  - `VisionPacketStore` の camera ごとの latest detection snapshot を使う
  - camera ID を選択肢の内部 key に含め、表示 label だけで source を識別しない
- `Tracked`[^tracked-source]
  - ibis tracker の `TrackedSnapshotStore` から得た latest `TrackerFrame` を `TrackedVisionViewState` 相当の field 描画 DTO へ変換して使う
  - raw detection ではなく ibis tracker 出力として扱う
- `3rd party tracker`[^third-party-tracker]
  - `MultiTrackerManager<TrackerPacketAdapter>`[^multi-tracker-manager] から受けた external tracker live state[^live-state] を使う
  - UI は `MultiTrackerManager` の mutable state[^mutable-state] を直接読まず、live UI[^live-ui] 用の immutable snapshot store[^immutable-snapshot-store] または composer を必ず挟む

live 比較では、厳密な同一 packet timestamp[^packet-timestamp] や全 source 共通の同一 receive callback[^receive-callback] は要求しない。raw SSL-Vision、ibis tracker、3rd party tracker は受信 stream と更新 callback が異なるため、ここを contract にすると通常表示の実装が過剰に結合する。採用方針は、1 回の `UI render tick`[^ui-render-tick] で各 source の latest immutable snapshot[^immutable-snapshot] を固定し、その composite snapshot を split / overlay の Layer A/B に渡すことである。

`UI render tick` の composite snapshot は次を保持する。

- render tick ID[^render-tick-id] または `SampledAt`
- source key[^source-key] と display label[^display-label]
- source ごとの receive timestamp[^receive-timestamp] / frame timestamp[^frame-timestamp] / packet count など、時刻差を説明する metadata
- balls / robots / geometry reference[^geometry-reference] / missing reason を含む immutable source snapshot

3rd party tracker の live 接続では、`MultiTrackerManager<TrackerPacketAdapter>` から external tracker packet を受け、source identity は UUID を優先して集約する。同じ `uuid` の tracker は remote endpoint が異なっても 1 つの source として扱い、同じ uuid group 内で最新 `ReceivedAt` の snapshot を代表として描画する。balls / robots は複数 endpoint 間で union merge しない。`uuid` が空または不明な場合だけ、source name / remote endpoint fallback で識別する。`uuid` が異なる tracker は source name が同じでも別 source とし、同じ display label が複数残る場合は短い uuid または endpoint を補助表示して UI 上で区別できる label にする。ただし `TrackerState` や protobuf packet 参照を UI が直接保持しない。`ExternalTrackerSnapshotStore` または `VisionLiveComparisonSnapshot` composer のような境界で clone / DTO 化し、描画中に state が変わらない immutable snapshot として扱う。`TrackerPacketSnapshotLogWriter` や CaptureOn sidecar writer を Vision live store[^live-store] として使う方針は不採用とする。これは CaptureOn session 保存用の仕組みであり、CaptureOff の通常 live Vision 画面では更新 source として成立しないためである。

geometry 基準は raw geometry 優先とする。`Raw Aggregate` または選択中の `Raw Camera` で得られる最新 `SSL_GeometryData` を overlay 全体の field 基準に使い、raw geometry がまだ無い場合のみ `Tracked` の geometry へ fallback する。`3rd party tracker` packet から field geometry を復元する方針は不採用とする。external tracker packet は比較対象の object state であり、field calibration の責任を持たせると source ごとの座標比較の意味が曖昧になる。

split / overlay の UI 挙動は diagnostics に寄せる。

- split mode は Layer A と Layer B を左右に並べる
- overlay mode は 1 つの field に Layer A/B を重ねる
- overlay と split を相互に共通化するのではなく、それぞれの mode に必要な画面構造は分けて保つ。そのうえで、Vision live と diagnostics の field 描画部[^field-rendering-part]は同じ責務境界に揃える
- split 用 field コンポーネント[^split-field-component]と overlay 用 field コンポーネント[^overlay-field-component]は別物として切り出す。overlay と split を 1 つのコンポーネントへ統合する意味ではない
- Vision live と diagnostics は、split では同じ split 用 field コンポーネントを使い、overlay では同じ overlay 用 field コンポーネントを使う
- field / boundary / geometry / marker の描画責務は split 用 / overlay 用の field コンポーネントへ置き、source selector、timestamp metadata、missing reason、legend、layout wrapper は Vision live と diagnostics の page / wrapper / 付加 component 側に持たせる
- overlay mode では layer ごとに独立した `VisionFieldCanvas` を重ねる方針は不採用とする。overlay 用 field コンポーネントは field / geometry を 1 回だけ描き、Layer A/B の balls / robots を layer group[^layer-group] として同じ viewport state[^viewport-state] 配下に描く
- split mode では左右 field は独立 viewport[^split-independent-viewport] として扱う。左右の pan / zoom 同期は要件にしないが、Vision live split と diagnostics split は同じ split 用 field コンポーネント、同じ marker 描画方針、同じ geometry fallback 方針を使う
- details は source ごとの summary、timestamp metadata、missing reason、raw/tracked/3rd party の違いを確認できる構成にする
- legend は diagnostics と同じく layer name、source label、visibility toggle、ready / missing state を表示する
- layer visibility は Layer A/B ごとに切り替えられる
- overlay mode では diagnostics overlay と同じく Layer A/B を別の accent color[^accent-color] で表示し、field marker と legend swatch の両方で layer を識別できるようにする
- Layer A/B が同じ source を選んだ場合は same-source[^same-source] として 1 layer 表示にまとめ、重複描画で誤差があるように見せない
- 片方の layer が missing でも、ready な layer は残して表示する
- missing layer は field 全体を空にせず、legend / details に missing reason を出す

## Diagnostics time-sync 方針

diagnostics replay / comparison は selected replay timeline tick[^selected-replay-timeline-tick] を同期基準にする。旧形式 / current limitation[^old-format-current-limitation] では、Vision/Input と ibis tracker は selected tick の render frame から得た snapshot を使い、3rd party tracker は同じ `ReplayTimelineIndex`[^replay-timeline-index] の `saved-session-alignment`[^saved-session-alignment] record を使う。この render snapshot[^legacy-render-snapshot-sidecar] 経路は `WorldFrameCommitted`[^world-frame-committed] に従うため tracker committed frame cadence[^tracker-committed-frame-cadence] に制限され、新規 capture[^new-capture] の目標経路としては扱わない。

新規 capture の diagnostics replay / comparison は diagnostics sample tick[^diagnostics-sample-tick] を保存単位にする。Diagnostics の `Vision Input` は selected tick の render frame ではなく、diagnostics sample tick に保存された latest raw snapshot[^latest-raw-snapshot] から復元する。ibis tracker と 3rd party tracker の比較対象は同じ diagnostics sample tick に保存された latest tracker snapshot[^latest-tracker-snapshot]、または同 tick 以前の `latest-before snapshot` を使う。このため、新規 capture では Vision、ibis tracker、3rd party tracker を tracker committed frame cadence ではなく diagnostics sample timeline[^diagnostics-sample-timeline] 上の比較として扱う。

selected tick[^selected-tick] に対象 `3rd party tracker` source の alignment record が無い場合でも、表示と比較を消さない。採用方針は、同じ source の selected tick 以前に存在する最新の `latest-before snapshot`[^latest-before-snapshot] を Field source と comparison に使うことである。UI / comparison は matching rule が `latest-before` であること、source snapshot の実際の `receivedAt`、selected tick との差分 delta、stale / latest-before 状態を明示する。これにより、対象 source が selected tick で未更新でも、ユーザーは直前まで得られていた tracker 状態を raw / ibis tracker と比較できる。

`latest-before snapshot` を使う場合も、replay / comparison の基準 timeline は selected replay timeline tick のまま固定する。source ごとに timeline cursor[^timeline-cursor] をずらしたり、表示上の selected time を tracker source 側へスライドしたりしない。Field と comparison は「selected tick に対して、この source は直前 sample を hold している」として表示し、delta は selected tick と hold した source snapshot の差として扱う。これにより、表示が消えることを避けつつ、時間軸が source ごとにずれて異なる時刻のものを同時刻扱いで表示しているように見える状態を避ける。

selected tick 以前に同じ source の snapshot が一切無い場合だけ、Field source は `CandidateMissing`[^candidate-missing]、comparison は `NoCandidateSnapshot`[^no-candidate-snapshot] 相当の missing 表示にする。この場合も Field 全体は消さず、ready な layer は残し、legend / details に missing reason を出す。future / later snapshot[^future-later-snapshot] への fallback は行わない。未来 tick の tracker 状態を現在 tick の比較へ混ぜると、replay timeline の因果関係が崩れ、comparison delta が実際より良く見えるためである。diagnostics-line alignment[^diagnostics-line-alignment] や nearest timestamp[^nearest-timestamp] は、selected tick 以前の同一 source snapshot を探すための補助 index として使ってよいが、selected tick より後の snapshot は候補に含めない。この挙動は既存 diagnostics time-sync regression contract として維持し、RuntimeHost / DebugHost 分離 scope では新しい `RAW-VISION-*` タスクを追加しない。

## Diagnostics loop isolation 方針

loop isolation[^loop-isolation] の中心目的は、tracker operation loop[^tracker-operation-loop] を web server live display processing[^web-server-live-display-processing] と diagnostics logging / replay processing[^diagnostics-logging-replay-processing] の両方から隔離することである。修正は UI-only display correction[^ui-only-display-correction] ではなく、保存と replay の入力 cadence を tracker committed frame cadence から切り離す設計として扱う。

3 つの loop の責務は次の通り分ける。

- tracker operation loop は raw packet と profile / control input を tracker engine へ渡し、tracker state 更新、publish、latest tracker snapshot の公開までを担当する。diagnostics sidecar への frame 保存をこの loop の `WorldFrameCommitted` callback へ直接結合しない。
- server live display processing は `UI render tick` ごとに raw / tracked / 3rd party tracker の latest immutable snapshot を固定し、通常 Vision 画面の split / overlay を描画する。これは表示用 loop であり、diagnostics logging の sample cadence を決めない。
- diagnostics logging / replay processing は tracker operation loop から直接書き込まれた render frame を読むのではなく、独立した diagnostics sample tick で latest raw snapshot と latest tracker snapshot を固定し、diagnostics sample sidecar[^diagnostics-sample-sidecar] に保存する。replay はこの sample timeline から `Vision Input` と比較対象を復元する。

logging 互換性はこの loop isolation の必須要件にしない。新規 capture の性能と cadence 維持を優先し、旧 render snapshot sidecar に対する高コストな互換 layer は設計しない。旧形式の render snapshot sidecar しか持たない session は、この新機能では unsupported / degraded legacy session[^degraded-legacy-session] として扱ってよい。旧形式を読む場合も、旧経路が tracker committed frame cadence に制限されることを UI / details で説明できれば足りる。

diagnostics sample tick の cadence は tracker committed frame cadence と同義にしない。raw SSL-Vision の latest snapshot が tracker commit より高頻度に更新される場合、new logging は raw snapshot cadence[^raw-snapshot-cadence] を失わない保存境界を持つ。tracker snapshot は sample tick 時点の latest を読むが、tracker operation 自体を sample tick から駆動しない。これにより、tracker operation、server live display、diagnostics logging / replay のいずれかの負荷や周期が、他の loop の user-visible 表示や保存 cadence を支配しない。

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
- Vision overlay color contract は、Layer A/B の field marker と legend が diagnostics と同じ考え方の別 accent color を持ち、same-source collapse 時は 1 layer の色へまとまることを単体テストで先に固定する
- Vision / diagnostics overlay field contract は、overlay mode が layer ごとに独立した field canvas を重ねず、Vision live overlay と diagnostics overlay が同じ overlay 用 field コンポーネントの単一 viewport state 配下で Layer A/B を描くことを単体テストで先に固定する
- Vision / diagnostics split field contract は、左右 field の viewport は独立のまま、Vision live split と diagnostics split が同じ split 用 field コンポーネントと同じ marker / geometry 描画方針を使うことを単体テストで先に固定する
- Vision live comparison contract は、1 回の `UI render tick` で `Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` の latest immutable snapshot を固定し、後続 store 更新で描画中 snapshot が変化しないことを単体テストで先に固定する
- 3rd party tracker live source contract は、`MultiTrackerManager<TrackerPacketAdapter>` の mutable state を UI が直接読まず、immutable snapshot store / composer を通して source option と field DTO を作ることを単体テストで先に固定する
- diagnostics time-sync regression は、selected `ReplayTimelineIndex` に対象 3rd party source の alignment record が無い場合でも、selected replay timeline tick 自体は動かさず、同じ source の selected tick 以前の `latest-before snapshot` を Field source と comparison に使い、matching rule、source snapshot の実際の `receivedAt`、selected tick との差分 delta、stale / latest-before 状態を表示することを単体テストで先に固定する
- diagnostics missing regression は、selected tick 以前に同じ source の snapshot が一切無い場合だけ `CandidateMissing` / `NoCandidateSnapshot` 相当になり、future / later snapshot へ fallback せず、ready layer は残ることを単体テストで先に固定する
- RUNTIME-HOST-002 の TDD contract は、RuntimeHost / DebugHost の project dependency boundary と DebugHost read-side responsibility を固定する
- RUNTIME-HOST-003 の TDD contract は、diagnostics sample boundary と legacy degraded contract を固定する。diagnostics sample tick が tracker committed frame cadence に依存せず latest raw snapshot と latest tracker snapshot を保存すること、Diagnostics `Vision Input` が render snapshot sidecar ではなく diagnostics sample sidecar から復元されること、DebugHost の diagnostics logging / replay processing が RuntimeHost の tracker operation loop や server live display processing の `UI render tick` snapshot contract を壊さないこと、旧 render snapshot sidecar だけを持つ session は unsupported / degraded legacy session として扱われ高コストな互換保証を持たないことを単体テストで先に固定する

## 前提

- raw vision information は `vision/ssl_vision_wrapper.proto` の `SSL_WrapperPacket` を指す
- default の SSL-Vision multicast endpoint は一般には `224.5.23.2:10006` だが、runtime 設定で変更できる
- 既存の unrelated worktree change は保護する

[^source-term]: source: 画面に描画する balls / robots / geometry の由来を表す設計上の概念。Vision split / overlay では Layer A/B で何を選び、何と比較するかを決める単位であり、`Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` が候補になる。
[^raw-aggregate]: Raw Aggregate: raw SSL-Vision の camera ごとの latest detection を UI 表示用に統合した source。複数 camera の balls / robots をまとめて Vision 画面で見るための候補で、camera 単体ではなく集約表示を選ぶときに使う。
[^raw-camera]: Raw Camera: 特定 camera ID の raw SSL-Vision latest detection を表示する source。camera ごとの見え方や検出差を確認するための候補で、選択肢の内部 key には camera ID を含める。
[^tracked-source]: Tracked: ibis tracker が生成した `TrackerFrame` を Vision 表示用 DTO に変換した source。raw detection ではなく ibis tracker 出力を Layer A/B や overlay 比較へ出すために使う。
[^third-party-tracker]: 3rd party tracker: ibis tracker 以外の外部 tracker から受けた tracker packet source。外部 tracker の出力を raw SSL-Vision や ibis tracker と比較するための候補。
[^live-state]: live state: Vision split / overlay の `3rd party tracker` で、実行中に外部 tracker から最後に受けた状態。Layer A/B で `3rd party tracker` を選んだときの描画元になるが、UI はこの状態を直接保持せず、snapshot 化された表示用データを読む。
[^mutable-state]: mutable state: `MultiTrackerManager` 内で後から内容が変わる状態オブジェクト。Vision 画面の split / overlay では、描画中に値が変わることを避けるため、この状態を直接読まず snapshot 化してから比較に使う。
[^live-ui]: live UI: CaptureOn の保存 replay ではなく、CaptureOff の通常 Vision 画面で現在受信している source を比較する画面。raw SSL-Vision、ibis tracker、3rd party tracker の現在値を split / overlay で見比べる。
[^immutable-snapshot-store]: immutable snapshot store: Vision 画面の通常表示で、Layer A/B に渡す前に描画中に変わらない表示用データを保持する境界。外部 tracker の更新と field 描画のタイミングを切り離すために使う。
[^packet-timestamp]: packet timestamp: packet に入っている時刻。Vision split / overlay の通常表示では、raw SSL-Vision、ibis tracker、3rd party tracker の packet timestamp が厳密に同一であることは比較条件にしない。
[^receive-callback]: receive callback: packet 到着時の受信処理呼び出し。通常表示では、すべての source が同じ receive callback で更新されたことを Layer A/B の比較条件にしない。
[^ui-render-tick]: UI render tick: Blazor UI が 1 回の表示更新を行う単位。この tick 内で各 source の latest snapshot を固定し、split / overlay へ渡す。
[^immutable-snapshot]: immutable snapshot: 描画中に内容が変わらないよう clone / DTO 化された読み取り専用 snapshot。後続の store 更新で描画中 snapshot が変化しないことを保証するために使う。
[^render-tick-id]: render tick ID: `UI render tick` ごとの表示更新単位を識別する ID。Layer A/B が同じ表示更新で固定された snapshot を見ていることを説明するために使う。
[^source-key]: source key: Vision 画面の source を識別する内部 key。`Raw Camera` では camera ID を含め、display label だけで source を取り違えないようにする。
[^display-label]: display label: UI に出す表示名。source key と違い内部識別には使わず、details や選択肢でユーザーに source を読ませるために使う。
[^receive-timestamp]: receive timestamp: packet を受け取った時刻。details で source 間の時刻差を確認し、`latest-before snapshot` が selected tick からどれだけ古いかを説明するために使う。
[^frame-timestamp]: frame timestamp: detection frame や tracker frame 側の時刻。receive timestamp と併せて、source 同士の比較がどの時刻情報に基づくかを details で説明する。
[^geometry-reference]: geometry reference: field 描画に使う geometry 参照。overlay では raw geometry を優先し、無い場合だけ `Tracked` の geometry へ fallback するため、source 同士の座標比較の基準を示す。
[^source-label]: source label: legend や details に出す source 名。ユーザーが Layer A/B でどの raw / tracked / 3rd party tracker を選んでいるか確認するために使う。
[^live-store]: live store: 通常 Vision 画面の現在表示を更新するための store。CaptureOn session 保存用の `TrackerPacketSnapshotLogWriter` や sidecar writer はこの役割に使わない。
[^accent-color]: accent color: Layer A/B を見分けるために marker の stroke や legend swatch に使う強調色。Issue #10 の Vision overlay では diagnostics overlay と同じ考え方で、重なった layer を色で判別できるようにする。
[^field-rendering-part]: field 描画部: field の背景、boundary、geometry line、ball marker、robot marker を SVG に描く部品境界。source 選択や詳細 metadata 表示ではなく、field 上に何をどう描くかを担当する部分を指す。
[^split-field-component]: split 用 field コンポーネント: split mode の左右それぞれの field を描くコンポーネント。左右の viewport は独立させるが、Vision live と diagnostics は同じコンポーネント境界を使う。
[^overlay-field-component]: overlay 用 field コンポーネント: overlay mode で 1 つの field 上に Layer A/B を重ねて描くコンポーネント。field と geometry を layer ごとに描き直さず、object marker だけを Layer A/B の group として重ねる。
[^overlay-field-rendering]: overlay field 描画部: overlay 用 field コンポーネントと同じ責務範囲を指す。field と geometry を layer ごとに描き直さず、object marker だけを Layer A/B の group として重ねる。
[^layer-group]: layer group: 同じ field 上で Layer A または Layer B に属する balls / robots をまとめる SVG group。group ごとに visibility や accent color を持たせるが、pan / zoom の基準は overlay field 描画部のものを共有する。
[^viewport-state]: viewport state: field 表示の zoom、pan、drag 中の移動量など、画面上で field をどの位置と倍率で見るかを表す状態。overlay mode では layer ごとに別々に持たず、1 つの viewport state を共有する。
[^split-independent-viewport]: split の独立 viewport: split mode の左右 field が、それぞれ別の表示位置と倍率を持つこと。左右は比較対象を並べる表示なので、片方を drag してももう片方を自動追従させる要件ではない。
[^same-source]: same-source: Layer A/B が同じ source を選んだ状態。Vision overlay では重複描画で誤差があるように見せないため、1 layer 表示にまとめる。
[^multi-tracker-manager]: MultiTrackerManager / TrackerPacketAdapter: `MultiTrackerManager` は `TrackerConnectionLib` の tracker state 管理コンポーネントで、own / external / unknown tracker の latest state を保持する。`TrackerPacketAdapter` は 3rd party tracker packet を `MultiTrackerManager` で扱うための adapter。
[^selected-replay-timeline-tick]: selected replay timeline tick: diagnostics replay でユーザーが現在選択している再生タイムライン上の基準 tick。Vision/Input、ibis tracker、3rd party tracker を比較するとき、この tick は source ごとへ移動させない。
[^replay-timeline-index]: ReplayTimelineIndex: diagnostics replay の selected timeline tick を識別する index。`saved-session-alignment` record と結び付けて、どの tick の比較かを特定する。
[^saved-session-alignment]: saved-session-alignment: CaptureOn session に保存された、replay timeline tick と tracker source snapshot の対応 record 群。diagnostics replay で 3rd party tracker の snapshot を同じ selected replay timeline tick に合わせるために使う。
[^alignment-sidecar]: alignment sidecar: CaptureOn session の主ログとは別に保存される対応付け用の補助ファイル。diagnostics replay で、選択中の replay timeline tick と tracker source snapshot の関係を後から復元するために使う。
[^alignment-record]: alignment record: alignment sidecar 内の 1 件の対応付け。diagnostics replay で特定の replay timeline tick を選んだとき、どの tracker source snapshot を比較に使うかを示す。
[^selected-tick]: selected tick: selected replay timeline tick の短縮表現。本文で「選択 tick」と書く場合も同じ意味を指す。
[^latest-before-snapshot]: latest-before snapshot: selected replay timeline tick に対象 source の alignment record が無い場合に使う、同じ source で selected tick 以前に存在する最新 snapshot。future / later snapshot は含めない。timeline cursor は selected tick のまま固定し、この snapshot は直前 sample の hold として扱う。
[^timeline-cursor]: timeline cursor: diagnostics replay 画面で現在選ばれている再生タイムライン上の位置。`latest-before snapshot` を使う場合でも、timeline cursor は selected tick のまま動かさない。
[^candidate-missing]: CandidateMissing: Field source に選択 source の候補 snapshot が無いことを示す missing 状態。Field 全体は消さず、ready な layer は残し、legend / details に missing reason を出す。
[^no-candidate-snapshot]: NoCandidateSnapshot: comparison に選択 source の候補 snapshot が無いことを示す missing 状態。比較対象が無い理由を UI に出すための状態であり、future / later snapshot を代わりに使う合図ではない。
[^future-later-snapshot]: future / later snapshot: selected replay timeline tick より後に存在する snapshot。Issue #10 の diagnostics では、未来側の tracker 状態を現在 tick の比較に混ぜないため、Field source や comparison の代替候補にしない。
[^diagnostics-line-alignment]: diagnostics-line alignment: diagnostics のログ行と tracker source snapshot の対応付け。selected tick 以前の同じ source の snapshot を探す補助情報としてだけ使い、selected tick より後の snapshot を候補にするためには使わない。
[^nearest-timestamp]: nearest timestamp: selected tick に近い時刻を探す検索方法。Issue #10 では近さだけで future / later snapshot を選ばず、同じ source の selected tick 以前の snapshot だけを候補にする。
[^old-format-current-limitation]: 旧形式 / current limitation: 既存の diagnostics capture が render frame 単位の sidecar に依存している状態。新規 capture の target ではなく、tracker committed frame cadence に制限される既存制約として扱う。
[^legacy-render-snapshot-sidecar]: render snapshot / legacy render snapshot sidecar: 既存 `.render-snapshots.jsonl.gz` のように tracker render frame 単位で保存された sidecar。loop isolation 後の新規 capture では主要な `Vision Input` 復元元にしない。
[^world-frame-committed]: WorldFrameCommitted: ibis tracker が world frame を commit したタイミングを表す dispatch result。既存 render snapshot 保存はこの callback に結合しており、raw Vision の保存 cadence としては遅くなり得る。
[^tracker-committed-frame-cadence]: tracker committed frame cadence: ibis tracker が `WorldFrameCommitted` を出し、`TrackerFrame` を publish する周期。raw Vision Input の新規保存周期として扱わない。
[^new-capture]: new capture: loop isolation 設計後に作る CaptureOn session。旧 render snapshot sidecar 互換より、latest raw / latest tracker snapshot を高頻度に保存できることを優先する。
[^diagnostics-sample-tick]: diagnostics sample tick: diagnostics logging / replay processing が latest raw snapshot と latest tracker snapshot を同じ保存単位として固定する tick。tracker committed frame と同義にしない。
[^latest-raw-snapshot]: latest raw snapshot: `VisionPacketStore` 相当の raw SSL-Vision latest detection / geometry を snapshot 化したもの。Diagnostics の `Vision Input` は新規 capture ではこの snapshot 系から復元する。
[^latest-tracker-snapshot]: latest tracker snapshot: ibis tracker または 3rd party tracker の最新出力を diagnostics sample に含めるために snapshot 化したもの。
[^diagnostics-sample-timeline]: diagnostics sample timeline: diagnostics sample tick を時系列に並べた replay 用 timeline。selected replay timeline tick の考え方を維持しつつ、render frame ではなく diagnostics sample を基準にする。
[^loop-isolation]: loop isolation: tracker operation、server live display、diagnostics logging / replay の周期と責務を分け、片方の cadence や負荷が別 loop の表示や保存を支配しないようにする方針。
[^tracker-operation-loop]: tracker operation loop: raw packet や profile / control input を tracker engine に渡し、tracker state の更新、publish、latest tracker snapshot の公開までを担当する処理 loop。
[^web-server-live-display-processing]: web server live display processing: 通常 Vision 画面が `UI render tick` ごとに latest immutable snapshot を固定して描画する処理。diagnostics logging / replay とは別扱いにする。
[^diagnostics-logging-replay-processing]: diagnostics logging / replay processing: CaptureOn 中に latest raw snapshot と latest tracker snapshot を独立した sample として保存し、Diagnostics 画面でその sample timeline を replay する処理。
[^ui-only-display-correction]: UI-only display correction: 保存済み data の cadence は変えず、描画時の補正だけで遅延を隠そうとする修正方針。RAW-VISION-017 の loop isolation では不採用とする。
[^diagnostics-sample-sidecar]: diagnostics sample sidecar: loop isolation 後に diagnostics logging / replay processing が保存する latest raw / latest tracker snapshot の sidecar。具体的な schema 名は実装 task で固定する。
[^degraded-legacy-session]: unsupported / degraded legacy session: 旧 render snapshot sidecar しか持たない capture session。新しい diagnostics sample path の性能や cadence 保証を受けず、表示できる範囲だけを旧形式として扱う。
[^raw-snapshot-cadence]: raw snapshot cadence: SSL-Vision packet / raw latest snapshot が更新される周期。Diagnostics の `Vision Input` 表示は新規 capture でこの cadence を失わない保存経路を持つ。
[^tracker-debug-host]: Tracker.DebugHost: 現 `Tracker.Server` の後継名。Web UI、raw vision viewer、diagnostics、capture / replay、比較表示を担当する debug 用 host。
