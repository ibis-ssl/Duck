# AutoRef 向け Tracker 設計

## 目的

`Tracker.Server` を唯一の実行体として維持しつつ、`Tracker.Core` に AutoRef 向けの高品質な追跡エンジンを分離実装する。

初期目標は次の 3 点に置く。

- `SSL-Vision` の raw detection / geometry から決定的に追跡結果を生成できる
- official `TrackerWrapperPacket / TrackedFrame` を multicast 配信できる
- official proto より豊富な内部メタ情報を保持し、将来の AutoRef 判定に再利用できる
- 将来の AutoRef 判定は world snapshot と高レベル event から記述しやすい構造にする

## 対象範囲

- `Tracker.Core` に tracker の内部モデル、エンジン契約、proto 変換器を実装する
- `Tracker.Server` から raw vision の流れを `Tracker.Core` に流し、最新の tracked snapshot と official tracker packet を生成できるようにする
- UI は raw viewer に加えて tracked viewer を持ち、button で切り替えられるようにする
- v1 では primary ball を先頭にしつつ、複数 ball を同時に維持して出力できるようにする
- v1 では決定性とルール上重要な品質を優先し、過剰な機械学習や非決定的要素は入れない

## 対象外

- feedback packet や robot telemetry を tracker 入力に使うこと
- Tigers と完全な挙動一致を取ること
- 独立した別 executable として tracker daemon を増やすこと
- 永続化や replay database を v1 で持つこと

## 基本方針

### 実行形態

- 実行体は `Tracker.Server` の 1 プロセスのみ
- 追跡アルゴリズム本体は `Tracker.Core` に置く
- `Tracker.Server` は host / UDP publish / UI / config の責務に限定する

### 品質優先順位

1. 決定的であること
2. ルール上重要な情報を落とさないこと
3. official tracker proto と互換であること
4. raw / tracked の観察性が高いこと

### 参考実装の扱い

`Tracker/Tracker.Core/Design/Ref/AutoReferee` は構成参考として使う。

- 採用する
  - raw vision と tracked world の責務分離
  - tracker proto 出力と内部 world model の分離
  - kicked ball / contact / ball left field のような AutoRef 向けメタ情報を内部で保持する考え方
- 採用しない
  - Java 実装構造そのもの
  - Tigers 固有の module 分割や naming への追従
  - 完全一致を前提とした複雑な最適化

### 調査結果の参照先

Tigers および official proto の調査結果は次を参照する。

- [TRACKER-000-tigers-investigation-20260501115618.md](/home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md:1)

この設計書では要点のみを書く。クラス名ごとの根拠や読み取り結果は調査メモ側に寄せる。

## Proto 入力

tracker が直接扱う proto 入力は次の通り。

- `SSL_WrapperPacket`
  - raw vision の datagram 全体
  - `Detection` と `Geometry` を内包する最上位 packet
- `SSL_DetectionFrame`
  - camera 単位の detection
  - 主に `FrameNumber`, `TCapture`, `TSent`, `CameraId`, `Balls`, `RobotsYellow`, `RobotsBlue`
- `SSL_DetectionBall`
  - ball 観測
  - 主に `X`, `Y`, `Z`, `Confidence`, `Area`
- `SSL_DetectionRobot`
  - robot 観測
  - 主に `RobotId`, `X`, `Y`, `Orientation`, `Confidence`, `Height`
- `SSL_GeometryData`
  - field geometry 全体
  - `Field` を保持し、必要に応じて calibration は診断表示へ回す
- `SSL_GeometryFieldSize`
  - field 寸法と line / arc 情報
  - 主に `FieldLength`, `FieldWidth`, `GoalWidth`, `GoalDepth`, `BoundaryWidth`, `BoundaryWidthGoalLine`, `LineThickness`, `FieldLines`, `FieldArcs`

## 外部出力

### Official 出力

v1 の外部配信は official tracker proto に限定する。

- `TrackerWrapperPacket`
  - `uuid`
  - `source_name`
  - `tracked_frame`
- `TrackedFrame`
  - `frame_number`
  - `timestamp`
  - `balls`
  - `robots`
  - `kicked_ball`
  - `capabilities`

初期 capability は次を出す。

- `CAPABILITY_DETECT_KICKED_BALLS`
- `CAPABILITY_DETECT_FLYING_BALLS`
- `CAPABILITY_DETECT_MULTIPLE_BALLS`

### 内部出力

official proto だけでは AutoRef に必要な情報が不足するため、`Tracker.Core` はより豊かな内部 frame を持つ。

- `TrackerFrame`
- `TrackedBallState`
- `TrackedRobotState`
- `KickEventState`
- `BallContactState`
- `BallLeftFieldState`
- `TrackerFrameMetadata`

## 内部モデル方針

### 単位

内部単位は次で統一する。

- 位置: `mm`
- 速度: `mm/s`
- 角度: `rad`
- 時刻: `ns`

proto 変換境界でのみ official 単位へ変換する。

- `mm` -> `m`
- `mm/s` -> `m/s`
- `ns` -> `s`

### `TrackerFrame`

`TrackerFrame` は UI と packet generator の両方が参照する内部参照モデルとする。

最低限の内容:

- 単調増加する `frame_number`
- data timestamp
- diagnostics 用の処理完了時刻
- geometry snapshot
- tracked ball の状態一覧
- primary ball の位置または参照
- tracked robot の状態一覧
- kicked ball state
- 最新 contact / 最終接触者
- ball の field 内外状態 / field 外退出状態
- source metadata

時刻の意味は次で固定する。

- `TrackerFrame.data_timestamp_ns`
  - world を構成した観測の基準時刻
  - detection を含む packet では `SSL_DetectionFrame.TCapture` を unix time とみなして `ns` 化した値を使う
  - `TCapture` が欠落または 0 以下なら `TSent` を使う
  - receive time / processing time は data timestamp には使わない
- `TrackerFrame.processed_at_ns`
  - engine がその frame を確定したローカル処理時刻
  - diagnostics 用であり official proto には出さない

`TrackerPacketGenerator` は `TrackerFrame.data_timestamp_ns` を `TrackedFrame.timestamp` に変換する。

### `TrackedBallState`

最低限の内容:

- 現在位置 / 速度 / 高さ
- visibility
- 参照元 camera 範囲
- 浮遊中かどうか
- 最終観測時刻
- 品質値

### `TrackedRobotState`

最低限の内容:

- team / robot id
- 位置 / 向き
- 並進速度 / 角速度
- visibility
- 品質値
- 直近 ball 接触フラグ

### `KickEventState`

最低限の内容:

- 開始位置
- 初速度
- 開始時刻
- 追跡対象 ball の内部 track id
- still moving 判定用の最新速度 / 最新更新時刻
- 任意の停止予測
- 任意の kicker robot id
- kick 種別候補

### `BallLeftFieldState`

最低限の内容:

- 内外状態
- 横切った line 種別
- 横切り位置
- 横切り時刻

## 契約詳細

### `ITrackerEngine`

役割:

- raw vision 入力を 1 件受け取り、event time で再順序化したうえで内部追跡状態を進める
- 確定した world frame 群と tracker event 群を publish 順で返す
- geometry 更新だけの packet でも内部状態を壊さない

最低限の入力:

- `SSL_WrapperPacket?`
  - detection / geometry を含む通常入力では必須
  - control-only reconfigure `Update` では省略可
- 現在有効な設定セット
- 必要に応じて設定セット切替要求
  - `TrackerProfileSwitchRequest`
    - `RequestVersion`
    - 適用対象 profile 名
    - その時点の immutable な resolved base settings snapshot
    - その時点の `RuntimeOverrides` snapshot

最低限の出力:

- `TrackerUpdateResult`
  - `CommittedFrames`
    - この入力処理で確定した `TrackerFrame` の列
    - 0 件以上を許可する
    - publish 順に並ぶ
  - `EmittedEvents`
    - `ProfileSwitched`、`GeometryReset`、`WorldFrameCommitted`、`KickDetected`、`ContactChanged`、`BallLeftField` の列
    - publish 順に並ぶ
    - event は必要に応じて対象 `frame_number` を参照する

最低限の保持状態:

- event time 順の pending detection buffer
- camera ごとの最新 packet timestamp
- camera ごとの robot track 群
- camera ごとの ball track 群
- 直近に確定した world snapshot
- 最新 geometry
- frame counter
- 現在の設定セット
- active な kick / contact / field metadata

`ITrackerEngine` の v1 契約は「packet を受けるたびに直ちに 1 frame 出す」ではなく、buffer に積んだうえで確定可能な event time 群だけを順に flush する方式とする。

`TrackerUpdateResult` の返却規則:

- detection を含まない入力では `CommittedFrames` が 0 件でもよい
- `ReorderWindow` をまたいで複数 group が確定した入力では `CommittedFrames` が複数件でもよい
- `TrackerCoordinator` は `CommittedFrames` を先頭から順に処理し、中間 frame を捨てない
- `TrackerCoordinator` は `CommittedFrames` が 0 件で `ProfileSwitched` / `GeometryReset` も無い入力では packet 配信、frame 表示更新、`WorldFrameCommitted` 通知を行わない
- `ProfileSwitched` / `GeometryReset` を含む 0-frame 入力では、対応する state clear と UI / store の状態更新だけを行ってよい
- control-only の入力でも reconfigure request を処理でき、その場合 `CommittedFrames` は 0 件でも `ProfileSwitched` などの event だけを返してよい
- 設定セット切替要求を受けたときの profile 適用、clear、設定差し替え、`ProfileSwitched` emit は `ITrackerEngine` の責務とする
- `TrackerCoordinator` は engine state を直接 clear せず、切替要求を次の `Update` 呼び出しへ渡すだけにする
- `ITrackerEngine` は `Update` の先頭で切替要求を消費し、以後の geometry / detection 処理を新 profile で実行する
- `ProfileSwitched` は新設定セットの反映と state clear が完了した直後に emit し、同じ `TrackerUpdateResult` に `WorldFrameCommitted` がある場合はそれより前に並べる
- `ProfileSwitched` と `GeometryReset` が同じ `TrackerUpdateResult` に共存する場合も、`EmittedEvents` の順序を正とし、coordinator はその順に local state 遷移を適用する
- `TrackerCoordinator` は `Update` 呼び出しごとに `pending request` を最大 1 件だけ `in-flight request` へ昇格させ、その request を result 処理完了まで immutable として扱う

### `TrackerPacketGenerator`

役割:

- `TrackerFrame` を official `TrackerWrapperPacket` に変換する

最低限の責務:

- `uuid` / `source_name` の設定
- 内部単位から official 単位への変換
- 複数 ball 出力
- primary ball 先頭化
- `kicked_ball` の組み立て
- capabilities の設定

### `TrackerCoordinator`

`Tracker.Server` 側の責務として置く。

役割:

- raw vision packet を `ITrackerEngine` へ渡す
- `TrackerUpdateResult` に含まれる `CommittedFrames` を順に store と observer へ反映する
- 設定セット変更時に engine へ切替要求を渡す
- 必要に応じて UDP 配信を行う
- publisher の配信先切替や UI 表示用の active profile 名更新など、engine 外 state の反映を行う

処理規則:

- `CommittedFrames` が複数件ある場合、古い順に全件を処理する
- UI 用 `TrackedSnapshotStore` には最後の `CommittedFrame` を残す
- official tracker packet は各 `CommittedFrame` ごとに生成する
- observer 通知は `EmittedEvents` の順序に従う
- coordinator は同一 `TrackerUpdateResult` の dispatch 中、まず `ProfileSwitched` / `GeometryReset` の local state 遷移を `EmittedEvents` 順に適用し、その完了後に `WorldFrameCommitted` と対応する `CommittedFrame` / official packet を処理する
- profile 切替要求を受けたら、coordinator は要求内容を保持したまま次の `Update` に 1 回だけ渡す
- raw packet が来ていなくても pending request がある場合は、coordinator は control-only `Update` を即時呼び出して request を drain しなければならない
- profile 切替要求に伴う engine state clear や `ProfileSwitched` の発火順制御は coordinator 側で再実装しない
- coordinator は profile 要求受付、`Update` 呼び出し、`TrackerUpdateResult` 処理を同じ直列化区間で扱い、1 回の `Update` 処理中に `in-flight request` を上書きしない
- coordinator は profile 切替要求を受け取った時点では publisher 配信先や UI 表示中 profile 名を即時反映しない
- `ProfileSwitched` を受け取った時点で、その `in-flight request` に対応する `現在適用済み snapshot`、publisher 配信先、active profile 表示、`TrackedSnapshotStore` の現在設定セット名を先に更新し、その後に `TrackedSnapshotStore` の最新 frame と受信時刻を clear する
- `ProfileSwitched` の observer 通知は、上記 local state 更新と `in-flight request` 解放が完了した後に行う
- `GeometryReset` を受け取った時点でも `TrackedSnapshotStore` の最新 frame と受信時刻を clear し、その clear 完了後に `OnGeometryReset` を通知する
- 任意の `Update` 呼び出しの result 処理が完了した直後に pending request がまだ残っていれば、coordinator はその場で次の control-only `Update` を直ちに再実行して `desired target snapshot` まで drain し続ける
- coordinator は `desired target snapshot`、`pending request`、`in-flight request`、`現在適用済み snapshot` を別に持ち、切替完了前の old state 出力と new state 表示を混在させない
- これにより、profile 切替後の最初の official packet / `WorldFrameCommitted` は必ず新 publisher 配信先と新 active profile 文脈の下で処理される

### `TrackedSnapshotStore`

`Tracker.Server` 側の UI 用読み取り模型とする。

最低限の内容:

- 最新 `TrackerFrame`
- 受信時刻
- 現在の設定セット名
- publish 成功回数 / 失敗回数
- profile 切替直後に frame 未確定であることを表す empty 状態

runtime profile control の UI 規則:

- tracked 側の detail panel は active profile 表示と profile 切替要求 UI を持つ
- active profile 表示の source of truth は `TrackedSnapshotStore.ActiveProfileName` とする
- profile 候補一覧は `TrackerOptions.Profiles` から作り、空なら current active profile 1 件だけを disabled 表示する
- profile 切替要求 UI は `ProfileSwitched` 直後に latest frame が clear されても操作不能にならないよう、frame の有無とは独立して描画する

## 入出力詳細

### 入力 packet の扱い

`SSL_WrapperPacket` の扱いは 3 種に分ける。

1. detection のみを含む packet
2. geometry のみを含む packet
3. detection と geometry の両方を含む packet

これに加えて、coordinator から engine へ reconfigure request だけを渡す control-only `Update` 呼び出しを許可する。

処理規則:

- geometry があれば、まず geometry snapshot を更新する
- detection があれば、`TCapture` を第 1 優先、`TSent` を第 2 優先の event time として pending buffer に積む
- pending buffer は `(event time, camera id, frame number)` の安定順で処理する
- 最新に見えた event time から `ReorderWindow` を越えた detection group を flush 対象にする
- detection の flush 時は、その時点の geometry snapshot を参照しつつ追跡処理を進める
- detection がない packet では `frame_number` を無理に進めない
- すでに flush 済みの event time より古い late packet は diagnostics に記録し、状態更新には使わない
- geometry 大変更 reset が発生した場合、pending buffer に残っている旧 geometry 世代の detection は flush せず破棄する
- control-only `Update` では detection / geometry を追加せず、pending request の消費と event 生成だけを行う

### multi-camera の時系列契約

既存 `VisionReceiverService` は UDP 到着順で packet を渡すが、tracker は arrival order に依存しないよう次を守る。

- `ReorderWindow`
  - packet 再順序化のための待ち時間窓
  - 設定値で外出しする
- `MergeWindow`
  - 同一 world frame に統合してよい camera 間時刻差の上限
  - 設定値で外出しする
- 1 つの world frame は「anchor event time から `MergeWindow` 以内の camera-local state」のみを使って構成する
- world frame 確定順は event time 昇順とし、同時刻 tie は `camera id` 昇順で安定化する
- `frame_number` は flush された world frame ごとに 1 ずつ進める
- geometry-only packet は frame を進めないが、次に flush される frame から新 geometry を参照できる
- event time の基準は detection data の unix time であり、receive time は統合順序決定に使わない

### 出力 packet の並び順

`TrackedFrame` の出力規則は次とする。

- `Balls[0]` は primary ball
- `Balls[1..]` は secondary ball
- `Robots` は team と id で安定順を持たせる
- `Capabilities` は毎回同じ順で出す

secondary ball の安定順は次で固定する。

- primary を除いた残りを `visibility desc`、`last_visible_timestamp_ns desc`、`internal_track_id asc` の順で整列する
- `internal_track_id` は engine 内で単調増加の採番とし、state reset 時のみ採番を初期化してよい

### geometry の扱い

geometry は次の 2 つの用途を持つ。

1. 追跡時の field 内外判定
2. UI 表示と AutoRef メタ計算

geometry 更新規則:

- 新しい geometry を受信したら snapshot を置き換える
- 既存 track は geometry 更新で捨てない
- ただし field length / width / goal geometry が設定閾値以上に変化した場合は camera-local track、kick/contact state、world snapshot を reset する
- geometry 大変更 reset 時は pending buffer も同時に clear し、旧 geometry 世代の未確定 detection を次 frame へ持ち越さない
- geometry 起因 reset でも `frame_number` と runtime identity は維持する

## 構成

### `Tracker.Core`

- `ITrackerEngine`
  - raw vision 入力から tracker の状態を進める中核契約
- `TrackerEngine`
  - v1 の決定的 tracker 実装
- `TrackerPacketGenerator`
  - `TrackerFrame` から `TrackerWrapperPacket` を生成する
- `TrackerFrame` と各 state 型
  - 内部状態モデル

### `Tracker.Server`

- raw vision receiver
  - 既存 `VisionReceiverService` を入力源として再利用する
- tracker coordinator hosted service
  - raw vision packet を `Tracker.Core` に流し、最新 tracked frame を更新する
- tracked snapshot store
  - UI 用の最新 tracked world state を保持する
- tracker packet publisher
  - official tracker multicast を配信する
- viewer page
  - `Raw / Tracked` button 切替を提供する

### 層ごとの責務境界

- `VisionReceiverService`
  - UDP 受信と proto decode
  - 問題再現用に、必要な調査時だけ着信 UDP datagram を圧縮 capture として保存する
- `VisionPacketStore`
  - raw snapshot 保持
- `TrackerCoordinator`
  - raw から tracked への橋渡し
- `Tracker.Core`
  - 追跡アルゴリズム本体
- `TrackerPacketGenerator`
  - official proto 変換
- `Tracker.CaptureReplay`
  - 保存済み packet capture を再生し、summary metric と条件式で regression check / 調査を行う CLI
- `TrackedSnapshotStore`
  - tracked UI 読み取り用状態
- viewer
  - 可視化のみ

## データフロー

1. `VisionReceiverService` が `SSL_WrapperPacket` を受信する
2. packet capture が有効な場合は、decode 前の UDP payload bytes と受信時刻を `jsonl.gz` に保存する
3. raw packet を `VisionPacketStore` に反映する
4. 同じ raw packet を tracker coordinator が `TrackerEngine` に流す
5. `TrackerEngine` が `TrackerFrame` を更新する
6. `TrackerPacketGenerator` が official `TrackerWrapperPacket` を生成する
7. publisher が UDP multicast へ送信する
8. UI は raw snapshot または tracked snapshot を button で切り替えて描画する

## 設定

`Tracker.Server` 側に `Tracker` section を追加する前提とする。

- `Enabled`
- `PublishUdp`
- `MulticastAddress`
- `Port`
- `SourceName`
- `Uuid`
- `Diagnostics`
- `RobotTracker`
- `BallTracker`
- `KickDetector`
- `RuntimeOverrides`
- `Profiles`

設定の大枠は次の形を想定する。

- `ActiveProfileName`
- `Profiles`
  - `<profile-name>`
    - `Publish`
    - `RobotTracker`
    - `BallTracker`
    - `KickDetector`
- `RuntimeOverrides`
  - `Publish`
  - `RobotTracker`
  - `BallTracker`
  - `KickDetector`
- `Diagnostics`
  - `FilePath`

`VisionReceiver` 側は replay 用の packet capture 設定を持つ。

- `PacketCapture`
  - `Enabled`
  - `DirectoryPath`
  - `FilePrefix`
  - `FlushEachPacket`

packet capture は protobuf decode 前の UDP payload bytes を `jsonl.gz` に保存し、`receivedAt` と remote endpoint を同じ record に持つ。保存された capture は順序通りに読み戻し、`SSL_WrapperPacket` へ復元して tracker へ再投入できるようにする。

packet capture の metadata には active profile 名だけでなく、`TrackerOptions` 全体の `Profiles` 設定値と、runtime override 適用後の resolved settings を保存する。profile 名だけでは replay 時に当時の tuning を復元できないため、capture と同時点の profile 設定値を同封する。

`Tracker.CaptureReplay` は、保存済み capture を `TrackerEngine` へ再投入する汎用 CLI とする。特定の不具合専用にせず、`packets`、`committed-frames`、`max-balls`、`max-robots`、`max-raw-balls` などの summary metric と、frame detail filter の条件式で自動テストや調査に使えるようにする。`--settings` で `Tracker.Server/appsettings.json` を読む場合は active profile の設定に `Tracker:RuntimeOverrides` を適用した engine settings を使う。

raw / tracked 診断で比較する raw detection は、現在着信した packet ではなく、commit 済み `TrackerFrame` を生成した source detection 群に紐づける。これにより reorder / merge window で遅延 commit された tracked frame と raw count / raw frame / raw camera の対応がずれない。

`Tracker.Server` の diagnostics viewer は、diagnostics log と同じ basename の `*.render-snapshots.jsonl.gz` がある場合に、選択した tracked frame の raw source detection と tracked frame を field 上に並べて描画する。描画 snapshot は調査用の UI データであり、tracker engine の replay 入力や内部状態保持には使わない。viewer は timeline scrubber のドラッグで frame を連続切替でき、field 描画時はページ全体をスクロールさせず、field の zoom / pan と画面スクロールが干渉しない layout とする。

既定配信先は official tracker の慣例値に合わせる。

- `224.5.23.2:10010`

ただし既定値は埋め込み固定せず、すべて設定から注入する。

- multicast address / port / source name / uuid は設定外出しする
- tracking parameter は設定外出しする
- raw / tracked 診断ログの明示出力先は `Tracker:Diagnostics:FilePath` で設定できるようにする
- packet capture は `VisionReceiver:PacketCapture:Enabled` を起動時初期値として持ち、起動後は UI から On / Off を切り替えられるようにする
- v1 標準であるカルマン filter の process noise / measurement noise / gating threshold も設定外出しする
- 近傍判定、visibility decay、kick speed threshold、chip 判定 threshold も設定外出しする

要望として、これらの設定は最終的に UI から動的変更できる構成にする。

v1 では次の 2 段階で進める。

1. `appsettings` と設定束縛で全設定を外出しする
2. 実行時設定保存領域を追加し、UI から変更した値を tracker coordinator が再読込できるようにする

### 設定セット切替

設定値は個別項目だけでなく、まとまりで切り替えられるようにする。

`Profiles` は 2 個以上の設定セットを保持できるようにする。

初期例:

- `Profiles.Simulation`
- `Profiles.RealHardware`
- `Profiles.RealHardwareB`

各設定セットには少なくとも次を含める。

- raw vision 受信元
  - `MulticastAddress`
  - `Port`
  - `InterfaceAddress`
- 配信先
  - `MulticastAddress`
  - `Port`
- `RobotTracker`
  - process noise
  - measurement noise
  - gate
  - 外れ値上限
- `BallTracker`
  - process noise
  - measurement noise
  - gate
  - 外れ値上限
  - 追跡寿命
- `KickDetector`
  - kick 判定閾値
  - chip 判定閾値
  - 接触余白

`RuntimeOverrides` の意図:

- 選択中設定セットの上に一時上書きをかける
- UI からの微調整はまずここへ入れる
- 設定セットそのものの保存は別操作に分ける
- v1 では UI の微調整はまず draft override として coordinator 側に保持し、engine へは明示 apply 時の snapshot だけを渡す
- pending または in-flight の request に入った override snapshot は immutable とし、その後の UI 編集は次の request 候補にだけ反映する

切替要件:

- 起動時に任意の設定セットを 1 つ選べる
- UI から登録済み設定セットの一覧を選択できる
- UI からの切替後は tracker coordinator が新しい設定セットへの切替要求を engine へ渡す
- 同名の `VisionReceiver` profile が存在する場合、起動時と profile switch 完了後にその受信元設定へ追従できる
- 個別値の微調整は選択中の設定セットに対する上書きとして扱えるようにする
- 設定セットは将来的に追加できる前提にする
- coordinator は最新のユーザー意図を `desired target snapshot` として保持し、profile 選択や override apply のたびにそれを最新値で置き換える
- 未適用の切替要求がある間にさらに profile 選択が来た場合、coordinator は pending request を最新要求で上書きし、queue は積まない
- `ProfileSwitched` は engine へ実際に渡されて適用された `RequestVersion` に対してのみ 1 回 emit される
- override の明示 apply 要求も v1 では同じ reconfigure request 経路で扱い、profile 名と draft override snapshot を組にして pending request を置き換える
- すでに `in-flight request` がある間の override 編集はその request を書き換えず、次の pending request 候補だけを更新する
- 新しいユーザー操作が現在の `desired target snapshot` と同値な場合だけ duplicate とみなし、新たな request を作らない
- `desired target snapshot` が `現在適用済み snapshot` と同値でも、pending または in-flight が別 snapshot を指しているなら、その差分を打ち消すための request を残す

切替責務の境界:

- `TrackerCoordinator`
  - UI や設定保存領域から新 profile 選択を受け取る
  - `desired target snapshot` を保持し、後続の profile 選択または override apply が来たら最新意図で置き換える
  - pending request を 1 件だけ保持し、後続の profile 選択または override apply が来たら `desired target snapshot` へ収束する内容に上書きする
  - 新しいユーザー操作が現在の `desired target snapshot` と同値な場合だけ no-op として破棄する
  - `Update` 呼び出し直前に pending request を `in-flight request` へ昇格させ、result 処理完了まで固定する
  - `ProfileSwitched` を受けるまでは publisher 配信先や active profile 表示を切り替えない
  - `ProfileSwitched` を受けた時点で、その `in-flight request` に対応する `現在適用済み snapshot`、publisher 配信先、active profile 表示、`TrackedSnapshotStore` の現在設定セット名を原子的に切り替える
  - receiver profile の切替は `ProfileSwitched` 後の observer 側で行い、tracker 側の active profile と受信元設定の観測可能な切替点を揃える
  - 上記 local state 遷移と store clear を完了してから `OnProfileSwitched` を通知する
  - 任意の `Update` の result 処理後に pending request が残る場合は、その場で `desired target snapshot` に一致するまで control-only `Update` を繰り返す
  - `TrackerProfileSwitchRequest` を次の `ITrackerEngine.Update` へ 1 回だけ渡す
- `ITrackerEngine`
  - `TrackerProfileSwitchRequest` を受けたら `Update` の先頭で新 profile と `RuntimeOverrides` を確定する
  - immutable な resolved base settings snapshot と override snapshot を request そのものから読む
  - coordinator が duplicate request を除外する前提とし、engine は受け取った request を実変更として扱う
  - camera-local track、kick/contact state、pending buffer、world snapshot を clear する
  - clear 完了後に `ProfileSwitched` を `EmittedEvents` へ積む
  - 同じ `Update` 呼び出し内で後続 packet を処理する場合、その flush 結果は新 profile の state だけを使う

runtime identity は設定セットとは分離する。

- `Uuid` は process 起動中に一定とし、profile 切替では変更しない
- `SourceName` も v1 では起動時固定とし、profile 切替では変更しない
- `MulticastAddress` / `Port` は profile 切替で変えてよい
- raw vision 受信元 `MulticastAddress` / `Port` / `InterfaceAddress` も profile 切替で変えてよい

profile 切替時の state 規則:

- `ITrackerEngine` は新 profile 適用時に camera-local track、kick/contact state、pending buffer、world snapshot を clear する
- 最新 geometry snapshot と runtime identity は維持する
- `frame_number` は単調増加を保つため継続する
- これにより old profile の filter state を new profile に持ち越さない
- profile 切替要求を受けた `Update` 呼び出しでは、clear 前に pending buffer を flush しない
- その入力が detection を含む場合、clear 後に新 profile の空 state へ積み直して処理する
- これにより `ProfileSwitched` より後に emit される `WorldFrameCommitted` は必ず new profile の state だけから生成される

初期実装では、設定セット切替は `appsettings` と実行時設定保存領域で扱う。

## アルゴリズム設計

v1 は決定的な古典的追跡を採用する。設計時点では particle filter や learned model は使わない。

この方針は、Tigers の次の実装を参考に寄せる。

- `VisionFilterImpl`
  - camera ごとの処理、統合、品質評価、公開周期の分離
- `BallFilterPreprocessor`
  - ball tracker 群の統合、kick 検出、kick 推定の前処理分離
- `BallTracker`
  - 個別 ball ごとの Kalman filter、health、成長判定、外れ値除外
- `RobotTracker`
  - 個別 robot ごとの位置・角度の別 filter、向きの巻き戻し補正、外れ値除外
- `TrackerPacketGenerator`
  - world model から official tracker proto への専用変換
  - rule 層が直接 raw packet に触れずに済む境界を保つ

寄せる対象は「考え方」と「責務分離」であり、Java の構造そのものを複製することではない。

### Tigers との対応関係

- `VisionFilterImpl`
  - 本設計では `TrackerCoordinator` と `TrackerEngine` の分担に相当
- `RobotTracker`
  - 本設計の camera 単位 robot track に相当
- `BallTracker`
  - 本設計の camera 単位 ball track に相当
- `BallFilterPreprocessor`
  - 本設計の ball 統合、kick 検出、kick 推定の前処理段に相当
- `TrackerPacketGenerator`
  - 本設計の `TrackerPacketGenerator` にそのまま相当
- `BotBallContactAutoRefCalc`
  - 本設計の `BallContactState` と最終接触者計算に相当
- `BallLeftFieldAutoRefCalc`
  - 本設計の `BallLeftFieldState` に相当

### 全体方針

- camera ごとの raw 観測を時系列順に処理する
- camera ごとにいったん局所的に追跡し、その結果を統合して world を作る
- 対象の識別は ball / team / robot id で分けて管理する
- 対応付けは明示的な規則で決める
- 状態推定は設定可能な filter で行う
- filter 実装は差し替え可能にするが、v1 は直線運動前提の Kalman filter を標準とする
- ball については「追跡本体」と「kick / 追加メタ推定」を分離する
- world 側の永続 filter は v1 では持たず、camera-local track を uncertainty-weighted に統合した結果をその frame の world snapshot とする

v1 実装契約:

- camera-local ball / robot track は、観測値をそのまま上書きする簡易追跡ではなく、predict-update を持つ線形 Kalman filter で更新する
- 各 track は少なくとも state estimate と covariance 相当の不確かさを保持する
- `ProcessNoise` は予測時の process covariance へ、`MeasurementNoise` は観測 covariance へ、`Gate` は対応付け時の innovation / 距離 gate へ使う
- `VisibilityHalfLifeSeconds` は観測欠測時の liveliness 管理に使う値であり、Kalman の covariance 更新を省略する理由にはならない
- world 統合で使う uncertainty は camera-local Kalman filter の事後不確かさから導く
- 単純な等速外挿 + 観測値上書き + 手動 uncertainty 加算だけで済ませる実装は、この v1 契約を満たさない

段階分割:

1. raw vision 正規化
2. camera 単位 track 更新
3. camera 横断統合
4. kick / contact / field 外退出計算
5. official proto 変換
6. rule 消費向け event 通知

### robot 追跡

robot は `team + robot id` が既知なので、対応付け問題は ball より小さい。

Tigers の `RobotTracker` に合わせ、位置系と向き系を別 filter で扱う。

処理段階:

1. camera 単位の観測を正規化する
2. camera ごとに `team + robot id` の robot track を維持する
3. 同一 `team + robot id` の複数 camera track を束ねて統合する
4. 既存 track と id で直接対応付ける
5. `position / velocity` と `orientation / angular velocity` を別 filter で更新する
6. 向きは unwrap して多回転補正する
7. 欠測時は予測のみ行い visibility を減衰する
8. 外れ値は gate で除外する

Tigers 由来で重視する点:

- 位置と向きの filter 分離
- 速度上限、角速度上限による外れ値除外
- health と更新頻度から visibility / quality を作る
- camera ごとの track と統合後の robot を分けて扱う

robot ごとの可視性:

- 直近 1 秒程度の更新履歴を保持する
- 更新頻度と平均 frame 間隔から `visibility` を作る
- 長時間欠測した robot は出力から外す

robot 状態モデル:

- 状態量
  - 位置 filter: `x, y, vx, vy`
  - 向き filter: `theta, omega`
- 観測量
  - 位置 filter: `x, y`
  - 向き filter: `theta`
- 推定
  - 位置 filter: 等速移動
  - 向き filter: 一定角速度

robot v1 filter 要件:

- `team + robot id` ごとに camera-local track を維持し、位置系と向き系を独立した線形 Kalman filter として更新する
- 同一 camera / team の raw detection に、既に採用済み robot と近すぎる別 ID robot が含まれる場合は、Tigers の `Geometry.getBotRadius() * 1.5` 相当の距離を基準に後続候補を採用しない
- 近接重複 robot の採用順は deterministic にし、confidence が高い候補を優先し、同 confidence では robot id の小さい候補を優先する
- 向き観測は update 前に unwrap して、`-pi` / `pi` 境界の不連続を filter 外へ漏らさない
- gate 判定は生観測との差分ではなく、予測状態に対する対応付け規則として使う
- 欠測 frame では predict のみを行い、visibility 減衰と track 削除判定は別責務として扱う
- merge に使う uncertainty は最新観測 confidence のみでなく、filter 後の position uncertainty を基準にする
- 欠測により visibility が十分低下した stale track は内部状態として短時間残せるが、tracked frame / viewer / official packet へ出し続けてはならない
- 外部出力可否は `OutputVisibilityThreshold` で判定し、Tigers の robot quality gate 初期値 `0.05` を設定値の基準とする

orientation は unwrap して連続化する。`-pi` / `pi` 境界での跳びは state 層で吸収する。

### ball 追跡

ball は id がないため、対応付けを明示設計する。

Tigers の `BallTracker` と `BallFilterPreprocessor` に合わせ、ball は「個別 track 群」と「primary ball 決定および kick 推定」の 2 段に分ける。

処理段階:

1. camera ごとの raw ball 観測を正規化する
2. camera ごとに ball track 群を維持する
3. track ごとに予測位置との距離と最大速度上限で外れ値を除外する
4. 更新できた track は health を上げ、育成前の track と成長済み track を分ける
5. camera をまたいで ball track 群を統合する
6. 直前の filtered ball 近傍を優先する探索半径で primary 候補を絞る
7. 古くなった track は visibility を減衰し、閾値以下で除外する
8. primary ball をルール上重要な優先度で 1 つ選ぶ

Tigers 由来で重視する点:

- `BallTracker` 単位の Kalman filter
- health と成長判定
- 最大速度による外れ値除外
- 直前の ball 位置や空中 ball 投影位置を基準にした探索半径
- camera ごとに 1 つまでの代表 track を選んで統合する考え方

ball ごとの生存管理:

- 生成直後の track は育成前として扱う
- 一定回数の更新後に成長済みとみなす
- 成長前 track は primary 候補の優先度を下げる
- v1 では Tigers の `grownUpAge = 3` に合わせ、primary 以外の secondary ball は 3 回以上観測された track だけを外部出力する
- 1 frame だけ raw detection に入った secondary ball ghost は camera-local track として短時間残せるが、tracked frame / viewer / official packet へは出さない
- 長時間更新されない track は削除する

ball 状態モデル:

- 状態量
  - `x, y, z, vx, vy, vz`
- 観測量
  - `x, y, z`
- 推定
  - 等速移動

ball v1 filter 要件:

- camera-local ball track は各 track ごとに線形 Kalman filter を持ち、観測 update と欠測時 predict を分ける
- `ProcessNoise` と `MeasurementNoise` は ball filter の covariance 更新に直接使う
- `Gate` は新規観測を既存 ball track へ結び付ける可否判定に使い、対応付け失敗時だけ新規 track を生成する
- track の uncertainty は観測 confidence の単純逆数ではなく、filter 事後 covariance から導く
- camera 横断統合の weighted merge は、この ball filter の事後 uncertainty を重みとする
- health / 育成 / visibility の管理は filter 更新とは別責務だが、少なくとも Kalman ベースの状態推定を置き換えてはならない
- 欠測により visibility が十分低下した stale track は内部状態として短時間残せるが、tracked frame / viewer / official packet へ出し続けてはならない
- 外部出力可否は `OutputVisibilityThreshold` で判定可能とし、Tigers の ball 不可視 lifetime 初期値 `1.0s` は `TrackLifetimeNs` の基準とする

複数 ball 対応:

- 内部では `TrackedBallState` を複数保持する
- 外部 `TrackedFrame.Balls` には primary ball と、成長済み secondary ball だけを出す
- `Balls[0]` は primary ball に固定する
- primary 選定は直前 primary track を優先し、その後 visibility、経過時間、field 上の重要度、直近 contact との整合を使う
- secondary ball は出力規則節の stable sort に従うが、single-frame ghost 抑制のため育成前 track は出力しない

### camera 統合

複数 camera から同一対象が見えるときは、単純平均ではなく「規則ベースの候補選別 + uncertainty-weighted merge」を行う。

- まず spatial gate と id 規則で同一候補を束ねる
- ball は直前の filtered ball 近傍または chip 投影近傍にある camera-local track だけを primary 候補に残す
- ball は camera ごとに代表 track を 1 つ選んでから統合する
- robot は同一 `team + robot id` の camera-local track だけを束ねる
- merge 自体は camera-local filter state の uncertainty を重みとして使う
- confidence や camera 固有品質は uncertainty 補正係数として将来拡張できるようにする
- 視線角や camera 固有品質を後で入れられるよう拡張点を持つ
- v1 では統合後の world 側に別 filter をもう 1 段かけない
- `TrackedBallState` / `TrackedRobotState` は camera-local track 群からその frame ごとに合成した world snapshot とする

統合時の安定性要件:

- 同時刻近傍の観測のみを統合対象にする
- 明らかに古い camera track は統合対象から外す
- 統合順序で結果がぶれないよう、安定した並び順を持つ
- merge 前に候補列を `(camera id, local track id)` で安定整列する
- 同一 uncertainty の tie は `camera id` と local track id で決める

### kick 検出

kick は AutoRef に重要なので v1 から入れる。Tigers の `BallFilterPreprocessor` のように、ball の主追跡から分離した前処理段で扱う。

候補条件:

- ball 速度が短時間で閾値以上に増加した
- 増加直前に近傍 robot の接触候補がある
- ball 進行方向と robot 前方がある程度整合する

処理方針:

- 早期検出系と安定検出系の 2 系統を持てる構造にする
- 推定結果がある場合はそちらを優先する
- kick 検出後は flat / chip の推定器へ流す

v1 の最小実装:

- 1 本の判定器から開始してよい
- ただし構造は 2 系統へ増やせる形にしておく

出力:

- kicker robot id
- kick 開始時刻
- 開始位置 / 初速度
- flat / chip 候補
- stop 予測があれば停止時刻 / 停止位置

`kicked_ball` の寿命規則:

- official proto には「kick 済みかつ still moving の間だけ」出力する
- active kick は、対応 ball の平面速度が `KickStillMovingSpeedThreshold` を下回る状態が `KickStillMovingGraceFrames` 続いたら clear する
- 対応 ball track が削除された、または `BallInvisibleTimeout` を超えて不可視になった場合も clear する
- 別の kick が確定した場合は古い kick を置き換える

flat と chip は次で近似判定する。

- `vz` または `z` 上昇が閾値以上なら chip 候補
- それ以外は flat 候補

### ball 接触

contact は専用 state として保持する。Tigers の `BotBallContactAutoRefCalc` と同様に、「現在接触中」と「最終接触者」を分ける。

- robot 半径 + ball 半径 + margin に入ったら接触候補
- 方向整合や相対速度で誤判定を減らす
- 現在接触中と最終接触者を分けて保持する

出力規則:

- 現在接触中がいなければ、直前の kick 情報も使って最終接触者を維持する
- 複数候補がある場合は距離と進行方向で優先順位を付ける

### ball の field 外退出

geometry の line 群または field size を使って判定する。Tigers の `BallLeftFieldAutoRefCalc` と同じく、world から外退出位置と field 内外状態を作る。

- ball center が field interior を出た時刻を記録する
- 横切った line 種別を持つ
  - touch line
  - goal line
  - goal interior
- 複数 ball がある場合も各 ball track ごとに判定する

goal 判定:

- goal mouth を通って goal interior に入ったか
- 単に goal line を横切っただけか

は分けて保持する。

### rule 連携

AutoRef などの rule 側は raw packet や camera-local track を直接読むのではなく、確定済み world snapshot と高レベル event を読む前提とする。

rule 側へ渡す基本要素:

- 最新 `TrackerFrame`
- 必要に応じた直近数 frame の履歴
- 高レベル event
  - `WorldFrameCommitted`
  - `KickDetected`
  - `ContactChanged`
  - `BallLeftField`
  - `ProfileSwitched`
  - `GeometryReset`

設計方針:

- rule ごとに observer を持てる構造にする
- observer は raw vision packet を直接 subscribe しない
- observer は `TrackerFrame` と domain event を入力にする
- kick / contact / ball left field の計算は tracker 側で責務を持ち、rule 側で同じ前提計算を重複させない
- rule 順序依存を避けるため、event は tracker で確定した順に publish する
- rule が追加されても tracking core の数値処理へ影響しない境界を保つ

publish 順は次で固定する。

1. state clear や意味の切替を伴う event
   - `ProfileSwitched`
   - `GeometryReset`
2. frame 本体
   - `WorldFrameCommitted`
3. その frame に従属する派生 event
   - `KickDetected`
   - `ContactChanged`
   - `BallLeftField`

同一 phase 内の並びは `TrackerUpdateResult.EmittedEvents` に格納された順を正とする。

最小インターフェースの考え方:

- `ITrackerObserver`
  - `OnProfileSwitched(string profileName)`
  - `OnGeometryReset()`
  - `OnWorldFrameCommitted(TrackerFrame frame)`
  - `OnKickDetected(KickEventState kick, TrackerFrame frame)`
  - `OnContactChanged(TrackerFrame frame)`
  - `OnBallLeftField(BallLeftFieldState state, TrackerFrame frame)`

v1 ではまず同期 observer でよい。将来、非同期配信や event bus へ差し替えられるよう、`TrackerEngine` 本体から publish 実装を分離できる余地を残す。

### filter 設定

filter と gate の主要設定は外出し前提にする。

- robot process noise
- robot measurement noise
- robot gating distance
- ball process noise
- ball measurement noise
- ball gating distance
- stale timeout
- visibility decay
- kick thresholds
- chip thresholds

設定源は固定しない。`Tracker.Core` は設定オブジェクトを受け取り、`Tracker.Server` が最終的な設定供給責務を持つ。

## UI 方針

- 現在の raw vision viewer は維持する
- viewer 上部または detail panel に `Raw / Tracked` の button 切替を置く
- tracked 表示では filtered ball / robots / kick 情報 / contact 情報を確認できるようにする
- raw と tracked で field 表示の見た目は揃え、比較しやすくする

tracked 表示の最低限:

- primary ball
- secondary ball
- tracked robots
- 現在の設定セット名
- kicked ball の有無
- 最終接触者
- ball の field 内外

## テスト方針

TDD の最初の対象は `Tracker.Core` の中核契約に限定する。

### 最初に失敗テストを作る対象

- `TrackerPacketGenerator` が内部単位から official proto 単位へ正しく変換する
- `TrackerPacketGenerator` が `kicked_ball` と capabilities を正しく埋める
- `TrackerPacketGenerator` が複数 ball を `TrackedFrame.Balls` に出し、primary ball を先頭に置く
- `TrackerPacketGenerator` が `TrackerFrame.data_timestamp_ns` を `TrackedFrame.timestamp` に使う
- `TrackerEngine` が 1 frame の raw vision から primary ball と robots を持つ `TrackerFrame` を返す
- `TrackerEngine` が複数 ball 観測を別 track として保持できる
- `TrackerEngine` が同一 robot の 2 frame から velocity を推定する
- `TrackerEngine` の threshold / noise parameter が設定オブジェクトから供給される
- `TrackerEngine` が複数の設定セットから選択された 1 つを受け取れる
- `TrackerEngine` が arrival order の異なる同一入力でも同じ event time 順で frame を確定する
- `TrackerEngine` が `MergeWindow` 外の camera 観測を同一 frame に混ぜない
- `TrackerEngine` が確定した world frame に対して高レベル event を安定順で通知する
- `TrackerEngine` が 1 入力から `0..N` 件の `CommittedFrames` を返せる
- `TrackerEngine` が geometry reset 時に pending buffer を clear する

### 具体的な最初のテスト候補

1. `TrackerPacketGenerator` に 2 個の ball を与えると、primary 指定 ball が `Balls[0]` になる
2. `TrackerPacketGenerator` が `mm` を `m` に、`ns` を `s` に変換する
3. `TrackerPacketGenerator` が `CAPABILITY_DETECT_MULTIPLE_BALLS` を含める
4. `TrackerPacketGenerator` が `TrackerFrame.data_timestamp_ns` を `TrackedFrame.timestamp` に使う
5. `TrackerEngine` が geometry のみ packet を受けても例外なく geometry snapshot を更新する
6. `TrackerEngine` が 2 frame の同一 robot 観測から非 0 の速度を出す
7. `TrackerEngine` が離れた 2 ball 観測を別 track として保持する
8. `TrackerEngine` が設定セット名変更で新しい設定を参照する
9. `TrackerEngine` が arrival order の異なる同一入力でも同じ frame 順を返す
10. `TrackerEngine` が `MergeWindow` を超えた camera 観測を別 frame に分ける
11. `TrackerPacketGenerator` が secondary ball を stable sort で出力する
12. `TrackerPacketGenerator` が still moving でない kick を `kicked_ball` に出さない
13. `TrackerCoordinator` が profile 切替時に track state を reset しても `frame_number` を巻き戻さない
14. `TrackerObserver` が raw packet ではなく確定済み `TrackerFrame` と domain event を受け取る
15. `TrackerCoordinator` が 1 入力で複数 `CommittedFrames` を受けたとき中間 frame を落とさない
16. `TrackerObserver` が `ProfileSwitched` / `GeometryReset` / `WorldFrameCommitted` / 派生 event を固定順で受け取る
17. geometry 大変更時に旧 geometry 世代の pending detection が破棄される

### 後続で追加する対象

- ball visibility decay
- recent contact / last toucher
- ball left field 判定
- geometry 大変更時 reset
- late packet diagnostics
- raw/tracked viewer 切替の統合確認

## タスク分割方針

- `TRACKER-000`: 設計書と進捗管理ファイル作成
- `TRACKER-001`: `Tracker.Tests` から `Tracker.Core` を参照可能にし契約テスト基盤を作る
- `TRACKER-002`: packet generator の契約テストを追加する
- `TRACKER-003`: engine の時系列契約テストを追加する
- `TRACKER-004`: `TrackerFrame` / state 型 / `TrackerUpdateResult` / observer-event 契約を実装する
- `TRACKER-005`: `TrackerPacketGenerator` を実装する
- `TRACKER-006`: `TrackerEngine` の reorder buffer と flush pipeline を実装する
- `TRACKER-007`: `TrackerEngine` の profile switch / geometry reset / event publish 順を実装する
- `TRACKER-008`: robot tracking と robot merge を実装する
- `TRACKER-009`: ball tracking と primary/secondary ball 選定を実装する
- `TRACKER-010`: kick と contact metadata を実装する
- `TRACKER-011`: ball left field metadata を実装する
- `TRACKER-012`: `Tracker.Server` へ engine と packet 配信を統合する
- `TRACKER-013`: tracker/network 設定束縛を統合する
- `TRACKER-014`: profile 切替要求経路を統合する
- `TRACKER-015`: tracked viewer と raw/tracked toggle を追加する
- `TRACKER-016`: tracked diagnostics 表示を追加する
- `TRACKER-017`: runtime profile 表示・操作 UI を追加する
- `TRACKER-018`: Tracker v1 の build/test 証跡を取得する
- `TRACKER-019`: Tracker v1 の integration 観点検証を行う
- `TRACKER-020`: Tracker v1 の最終レビューと追跡ファイル同期を行う
- `TRACKER-027`: Tigers 由来の近接重複 robot / 短命 ball 抑制を追加する

contracts フェーズの着手順:

1. `TRACKER-001` で `Tracker.Tests` から `Tracker.Core` を参照し、shared fixture と test data 基盤を整える
2. `TRACKER-002` で packet generator の失敗契約テストを固定する
3. `TRACKER-003` で engine の時系列契約テストを固定する
4. `TRACKER-004` で内部モデル・state 型・observer/event 契約を固定する
5. `TRACKER-005` で packet generator 実装へ進む

## 承認ゲート

`TRACKER-000` の設計承認は完了済みであり、以後はこの設計書を正本として contracts フェーズ以降を進める。

- 仕様変更や task 再分割があれば先にこの設計書と tracking files を同期する
- contracts フェーズでは failing test と契約 surface を先に固定する

## 前提

- source first で進める
- 初期入力は vision only
- 配信は library + UDP
- viewer は `ssl-vision-client` のように raw / tracked を button で切り替える
- 無関係な worktree 変更は保護する
