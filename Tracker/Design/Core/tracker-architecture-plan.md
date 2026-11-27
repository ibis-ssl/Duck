# 自動レフェリー向けトラッカー設計

本書で raw vision は SSL-Vision の検出情報を指す。カメラの画像や動画そのものではない。

## 目的

`Tracker.Core` に自動レフェリー向けの高品質な追跡エンジンを分離実装し、本番寄りの実行体は `Tracker.RuntimeHost`、デバッグ・診断用の Web UI は `Tracker.DebugHost` として分ける。

初期目標は次の 4 点に置く。

- `SSL-Vision` の未加工の検出情報とフィールド形状から決定的に追跡結果を生成できる
- 公式形式の `TrackerWrapperPacket / TrackedFrame` をマルチキャスト配信できる
- 公式の通信形式より豊富な内部情報を保持し、将来の自動レフェリー判定に再利用できる
- 将来の自動レフェリー判定はフィールド全体の状態のスナップショットとキックや接触などの通知から記述しやすい構造にする

## 対象範囲

- `Tracker.Core` にトラッカーの内部状態表現、追跡エンジンの契約、公式の通信形式への変換処理を実装する
- `Tracker.RuntimeHost` から raw vision を `Tracker.Core` に渡し、最新の追跡スナップショットと official tracker packet を生成できるようにする
- `Tracker.DebugHost` は Web UI、診断、キャプチャー・再生、比較表示に専念し、トラッカーの周期処理を描画やログの保存の周期から切り離す
- UI は raw vision viewer に加えて追跡結果の表示画面を持ち、ボタンで切り替えられるようにする
- 初期版では primary ball（主対象のボール）を先頭にしつつ、複数のボールを同時に維持して出力できるようにする
- 初期版では決定性と競技規則上重要な品質を優先し、過剰な機械学習や非決定的要素は入れない

## 対象外

- ロボットからの応答パケットやロボットの稼働情報をトラッカー入力に使うこと
- TIGERs と完全な挙動一致を取ること
- 自動レフェリーの判定処理を今回の対象範囲で実装すること
- 永続化や再生データベースを初期版で持つこと

## 基本方針

### 実行形態

- 実行体は本番寄りの `Tracker.RuntimeHost` とデバッグ用の `Tracker.DebugHost` に分ける
- 追跡アルゴリズム本体は `Tracker.Core` に置く
- `Tracker.RuntimeHost` はトラッカーの実時間処理、UDP 送信、将来の自動レフェリーモードを同一プロセスで実行する処理を担当する
- `Tracker.DebugHost` は Web UI、診断、キャプチャー・再生、比較、デバッグ設定を担当する
- `Tracker.DebugHost` の Web UI の描画と診断ログの保存は `Tracker.RuntimeHost` のトラッカーの周期処理を直接駆動しない

### 品質優先順位

1. 決定的であること
2. 競技規則上重要な情報を落とさないこと
3. official tracker proto と互換であること
4. 未加工入力・追跡結果の観察性が高いこと

### 参考実装の扱い

`Tracker/Design/Core/Ref/AutoReferee` は構成参考として使う。

- 採用する
  - raw vision とフィールド全体の追跡結果の責務分離
  - 公式形式の追跡出力と、内部で持つフィールド全体の状態表現の分離
  - キックされたボール、接触、ボールの場外退出など、自動レフェリーに必要な情報を内部で保持する考え方
- 採用しない
  - Java 実装構造そのもの
  - TIGERs 固有の構成分割や命名への追従
  - 完全一致を前提とした複雑な最適化

### 調査結果の参照先

TIGERs および公式の通信形式の調査結果は次を参照する。

- [TIGERs と公式通信形式の調査結果](../../../reports/TRACKER-000-tigers-investigation-20260501115618.md)

この設計書では要点のみを書く。クラス名ごとの根拠や読み取り結果は調査メモ側に寄せる。

## 入力する通信データ

トラッカーが直接扱う入力の型は次の通り。

- `SSL_WrapperPacket`
  - raw vision を含む UDP パケットの受信データ全体
  - `Detection` と `Geometry` を内包する最上位パケット
- `SSL_DetectionFrame`
  - カメラ単位の検出情報
  - 主に `FrameNumber`, `TCapture`, `TSent`, `CameraId`, `Balls`, `RobotsYellow`, `RobotsBlue`
- `SSL_DetectionBall`
  - ボール観測
  - 主に `X`, `Y`, `Z`, `Confidence`, `Area`
- `SSL_DetectionRobot`
  - ロボット観測
  - 主に `RobotId`, `X`, `Y`, `Orientation`, `Confidence`, `Height`
- `SSL_GeometryData`
  - フィールド形状全体
  - `Field` を保持し、必要に応じてカメラの校正情報は診断表示へ回す
- `SSL_GeometryFieldSize`
  - フィールド寸法と線分 / 円弧情報
  - 主に `FieldLength`, `FieldWidth`, `GoalWidth`, `GoalDepth`, `BoundaryWidth`, `BoundaryWidthGoalLine`, `PenaltyAreaDepth`, `PenaltyAreaWidth`, `CenterCircleRadius`, `LineThickness`, `FieldLines`, `FieldArcs`

## 外部出力

### 公式形式の出力

初期版の外部配信は official tracker proto に限定する。

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

初期版で出力する対応機能は次の通り。

- `CAPABILITY_DETECT_KICKED_BALLS`
- `CAPABILITY_DETECT_FLYING_BALLS`
- `CAPABILITY_DETECT_MULTIPLE_BALLS`

### tracker packet snapshot 比較ログ

CaptureOn 中に同じ公式トラッカーのマルチキャスト用の通信アドレスと通信ポートで受信した `TrackerWrapperPacket` は、後から自前トラッカーの内部出力、自前トラッカー自身の公式形式のパケット、外部トラッカーのパケットを再生・比較できるように、別系統で保存する。

DebugHost / CLI / UI 側の詳細な機能仕様は `../DebugHost/debug-host-cli-ui-detail-design.md` を正とする。巨大ファイルの分割や追跡処理の軽量化などの保守・運用作業は、この機能仕様に含めない。

責務境界は次の通り。

- `TrackerConnectionLib` を official tracker packet のキャプチャー処理の接続先の第一候補とする。`UdpTrackerReceiver`、`MultiTrackerManager`、`TrackerPacketAdapter` の既存責務を使い、公式形式の `TrackerWrapperPacket` を `uuid` / `sourceName` / 送信元の通信アドレス / 通信ポートごとに識別する。
- `Tracker.DebugHost` は CaptureOn の記録単位と snapshot log を紐付ける統合層とする。同じ CaptureOn の記録単位のキャプチャー、capture metadata、トラッカーの診断ログ、`diagnostics-samples.jsonl`、render snapshot、tracker packet snapshot sidecar JSONL、tracker snapshot alignment sidecar JSONL を一つの session folder 配下にまとめる。新規キャプチャーでは diagnostics sample sidecar を `Vision Input` と自前トラッカーの診断再生の主な入力として扱い、CaptureOn を開始した時点が異なるログは別フォルダに分ける。
- `Tracker.Core` には official tracker packet のキャプチャー、スナップショット保存、比較処理を入れない。`Tracker.Core` は自前トラッカーの内部状態生成と公式形式のパケット生成だけを担当する。

snapshot log は、既存の `.tracker-diagnostics.log` を破壊的に拡張しない。主記録は session folder 配下の tracker packet snapshot sidecar JSONL とする。診断側の追加は、既存の読み取り処理との `key=value` 形式の互換性を保ったまま、capture metadata から解決できる snapshot sidecar への相対パス、表示元の数、source role 別の件数、近傍比較の概要などの参照・集計に限定する。

session folder 名には既存の `<prefix>-<timestamp>-<guid>` という共通名を使う。フォルダ内のファイル名にも同じ共通名を含めるか、用途名を使う。capture metadata には session folder と各ファイルへの相対パスを記録し、新規キャプチャーでは `DiagnosticsSampleSidecarPath` / `DiagnosticsSampleLog`、`TrackerSnapshotSidecarPath` / `TrackerSnapshotLog`、`TrackerSnapshotAlignmentPath` / `TrackerSnapshotAlignmentLog` を別々に保持する。diagnostics sample sidecar、tracker packet snapshot、alignment sidecar の未作成、記録 0 件、読み取り失敗を互いに混同せず、共通名を揃える考え方は session folder 名またはフォルダ内のファイル名で維持する。

補助 JSONL ファイルの各記録は、少なくとも次を保持する。スナップショットは表示用データとして扱ってよいが、それだけでは比較元データとして不十分である。通常経路では、受信パケットの元のバイト列か、そのバイト列を復元できる参照を必ず保持する。round-trip（書き込み後の読み戻し）で、保存済み記録から元のバイト列を復元または再デコードできるようにする。

- `receivedAt`
- 送信元の通信アドレスと通信ポート
- `uuid`
- `sourceName`
- source role、source label、source metadata
- tracked frame number
- tracked frame timestamp
- 受信パケットの元のバイト列、または session folder 内で元のバイト列を復元できる参照情報
- 元データから作れるボール・ロボット数、チーム・ロボット ID、代表位置、追跡情報の生成元の概要など、後から比較・一覧表示するための概要情報
- デコードや保存形式のエラーがある場合に、処理を省略したことやエラーの内容を示す情報

自前トラッカーの `Uuid` / `SourceName` は自前のパケットを保存対象から外す条件ではなく、後続表示・比較用の source role、source label、source metadata を付与するために使う。自前トラッカー自身の公式形式のパケットも snapshot sidecar へ保存してよく、詳細ログや render snapshot との重複保持を仕様として許容する。どちらかが空、重複、または他のトラッカーと衝突する場合も記録は落とさず、送信元の通信アドレスと通信ポートに加え、送信ソケットの自己受信の扱いも診断に記録し、source role を `unknown` や `ambiguous` として扱う。表示元ごとに利用中のトラッカーを取得する API の扱いと、同じ `uuid` が衝突する場合の扱いは、表示元の概要と source role の判定に関する確認事項である。元のパケットと source identity を落とさない限り、保存処理を止める理由にはしない。

自前トラッカーの確定済み追跡フレームと tracker packet snapshot は、送信頻度が一致しない前提で扱う。さらに、外部トラッカーの `TrackedFrame.timestamp` は自前トラッカーと同じ時刻系とは限らない。新規キャプチャーの再生、診断、Field source の表示では、`diagnostics-samples.jsonl` の diagnostics sample tick を選択時点とし、`Vision Input` はその記録の未加工入力の概要、自前トラッカーは同じ記録の追跡結果の概要から復元する。外部トラッカーは、同じ選択時点に保存済みの alignment sidecar の対応記録があればその tracker snapshot を使い、対応記録がなければ選択時点以前の同じ表示元の latest-before snapshot を使う。diagnostics sample sidecar がない旧形式だけは、既存の近傍時刻の選択を推定表示として使ってよい。採用した対応規則、許容する時間幅、`uuid` / `sourceName` / 送信元の通信アドレスと通信ポート / source role、および対応付けの状態は、後から確認できるように保存または表示する。

新規キャプチャーの replay timeline は `diagnostics-samples.jsonl` の `sampleReceivedAt` を時刻軸とし、diagnostics sample tick を選択単位にする。外部トラッカーの高頻度な受信記録は tracker packet snapshot と alignment sidecar に保持するが、それ自体を新規記録の再生位置として追加せず、選択中の diagnostics sample tick に対する保存済みの対応付け、または latest-before snapshot として比較・描画する。`TrackedFrame.timestamp` は表示元間で時刻系が違う場合があるため、再生順序には使わない。

等倍速 `Play` は、replay timeline のすべての時点を逐次描画する契約ではない。再生開始時の wall-clock と、selected replay timeline tick の `ReceivedAt` を基準に目標の収録時刻を計算し、毎秒30回相当の表示更新ごとに `ReceivedAt <= target` を満たす最新時点へ追従する。高頻度トラッカーの更新時点は対応付け・比較データとして保持し、表示だけが中間時点を省略できる。再生位置のドラッグ、Field source 選択、比較、CLI 比較は、任意の replay timeline tick を選べる経路として維持する。通常再生での表示省略によって、保存済みの第2版の対応付けや比較精度を落とさない。診断画面の再生操作部は、`Play` / `Fast Forward` / `Stop` の従来のボタン配置を維持し、速度選択側の小さなタブに `等倍速`、`4x`、`16x`、`64x` を並べる。`等倍速` は通常再生、各倍率は調査用の早送りとし、通常再生専用の実時間追従から分離する。数値の等倍ラベルは使わない。

alignment sidecar は `tracker-packet-snapshots.jsonl` へ埋め込まず、別ファイルとする。snapshot sidecar は受信パケットの主記録、alignment sidecar は診断記録の再生用索引として分けることで、対応付けの欠落や破損を既存スナップショット保存の破損と区別できる。source key は `sourceRole + sourceLabel + sourceUuid + remoteEndpoint` を基本とする。同じ source label / UUID が複数の送信元の通信アドレスと通信ポートに分かれる場合、UI で集約する代表の tracker snapshot は、記録開始からの相対的な `receivedAt` に最も近いものを選ぶ。同条件の候補から選ぶ規則も固定し、選択結果が一意に決まるようにする。

`tracker-snapshot-alignment.jsonl` は外部トラッカーを含む tracker source snapshot と診断側の選択時点を対応付ける補助索引として扱う。新規キャプチャーで diagnostics sample sidecar がある場合、replay timeline の選択軸は diagnostics sample tick とし、alignment sidecar の対応記録は選択中の diagnostics sample tick に対応する tracker snapshot、または選択時点以前の候補を引くために使う。外部トラッカーの高頻度な tracker snapshot を保存しても、`Vision Input` と自前トラッカーの復元元を render snapshot へ戻さない。読み取り処理は補助 JSONL をログ選択時に索引化し、再生位置や Field source の変更ごとに全件を再読込しない。

Capture Off 中は `tracker-packet-snapshots.jsonl`、alignment sidecar、diagnostics sample sidecar へ追記しない。Capture Off から再度記録を開始する際は、新しい session folder と新しい補助ファイルへ切り替え、前のフォルダへ追記しない。他のトラッカーが存在しない場合でも diagnostics sample sidecar は `Vision Input` と自前トラッカーの診断再生に使え、既存のキャプチャーと診断ログの内容上の挙動を壊さない。capture metadata には各補助ファイルが未作成または 0 件である状態を個別に表現できるようにする。

### 内部出力

公式の通信形式だけでは自動レフェリーに必要な情報が不足するため、`Tracker.Core` はより豊かな内部の追跡フレームを持つ。

- `TrackerFrame`
- `TrackedBallState`
- `TrackedRobotState`
- `KickEventState`
- `BallContactState`
- `BallLeftFieldState`
- `TrackerFrameMetadata`

## 内部状態表現の方針

### 単位

内部単位は次で統一する。

- 位置: `mm`
- 速度: `mm/s`
- 角度: `rad`
- 時刻: `ns`

公式の通信形式へ変換する箇所でのみ、内部単位から公式形式の単位へ変換する。

- `mm` -> `m`
- `mm/s` -> `m/s`
- `ns` -> `s`

### `TrackerFrame`

`TrackerFrame` は UI とパケット生成処理の両方が参照する内部参照用の状態表現とする。

最低限の内容:

- 単調増加する `frame_number`
- 観測データの時刻
- 診断用の処理完了時刻
- フィールド形状のスナップショット
- 追跡中のボールの状態一覧
- 主対象のボールの位置または参照
- 追跡中のロボットの状態一覧
- キックされたボールの状態
- 直近の接触状態と最終接触者
- ボールがフィールドの内側・外側のどちらにあるかと、場外退出の状態
- source metadata

時刻の意味は次で固定する。

- `TrackerFrame.data_timestamp_ns`
  - フィールド全体の状態を構成した観測の基準時刻
  - 検出情報を含むパケットでは `SSL_DetectionFrame.TCapture` を Unix 時刻とみなして `ns` 化した値を使う
  - `TCapture` が欠落または 0 以下なら `TSent` を使う
  - 受信時刻・処理時刻は観測データの時刻には使わない
- `TrackerFrame.processed_at_ns`
  - 追跡エンジンがその追跡フレームを確定した、この端末での処理時刻
  - 診断用であり公式の通信形式には出さない

`TrackerPacketGenerator` は `TrackerFrame.data_timestamp_ns` を `TrackedFrame.timestamp` に変換する。

### `TrackedBallState`

最低限の内容:

- 現在位置 / 速度 / 高さ
- 可視性
- 参照元のカメラの範囲
- 浮遊中かどうか
- 最終観測時刻
- 品質値

### `TrackedRobotState`

最低限の内容:

- チーム / ロボット ID
- 位置 / 向き
- 並進速度 / 角速度
- 可視性
- 品質値
- 直近にボールへ接触したかを示すフラグ

### `KickEventState`

最低限の内容:

- 開始位置
- 初速度
- 開始時刻
- 追跡対象ボールの内部追跡 ID
- ボールが動き続けているかを判定するための最新速度 / 最新更新時刻
- 任意の停止予測
- 必要に応じて、キックしたロボットの ID
- キック種別候補

### `BallLeftFieldState`

最低限の内容:

- 内外状態
- 横切った線分種別
- 横切り位置
- 横切り時刻

## 契約詳細

### `ITrackerEngine`

役割:

- raw vision を 1 件受け取り、観測時刻で並べ替えたうえで内部の追跡状態を進める
- 確定したフィールド全体の追跡フレームとトラッカーの通知を、出力順で返す
- フィールド形状更新だけのパケットでも内部状態を壊さない

最低限の入力:

- `SSL_WrapperPacket?`
  - 検出情報 / フィールド形状を含む通常入力では必須
  - 観測入力を伴わない設定変更 `Update` では省略可
- 現在有効な設定プロファイル
- 必要に応じて設定プロファイルの切り替え要求
  - `TrackerProfileSwitchRequest`
    - `RequestVersion`
    - 適用対象の設定プロファイルの名前
    - その時点の基本設定を解決し、変更不能にしたスナップショット
    - その時点の `RuntimeOverrides` スナップショット

最低限の出力:

- `TrackerUpdateResult`
  - `CommittedFrames`
    - この入力処理で確定した `TrackerFrame` の列
    - 0 件以上を許可する
    - 出力順に並ぶ
  - `EmittedEvents`
    - `ProfileSwitched`、`GeometryReset`、`WorldFrameCommitted`、`KickDetected`、`ContactChanged`、`BallLeftField` の列
    - 通知順に並ぶ
    - 通知は必要に応じて対象 `frame_number` を参照する

最低限の保持状態:

- 観測時刻順の未処理の検出情報のバッファ
- カメラごとの最新パケット時刻
- カメラごとのロボット追跡状態群
- カメラごとのボール追跡状態群
- 直近に確定したフィールド全体の状態のスナップショット
- 最新のフィールド形状
- 追跡フレームの採番用カウンター
- 現在の設定プロファイル
- 現在有効なキック・接触・場外退出に関する情報

`ITrackerEngine` の初期版契約は「パケットを受けるたびに直ちに 1 件の追跡結果を出す」ではなく、バッファに積んだうえで確定可能な観測時刻群だけを順に確定処理する方式とする。

`TrackerUpdateResult` の返却規則:

- 検出情報を含まない入力では `CommittedFrames` が 0 件でもよい
- `ReorderWindow` をまたいで複数の集合が確定した入力では `CommittedFrames` が複数件でもよい
- `TrackerCoordinator` は `CommittedFrames` を先頭から順に処理し、中間の追跡フレームを捨てない
- `TrackerCoordinator` は `CommittedFrames` が 0 件で `ProfileSwitched` / `GeometryReset` も無い入力ではパケット配信、追跡結果の表示更新、`WorldFrameCommitted` 通知を行わない
- `ProfileSwitched` / `GeometryReset` があり、確定済みの追跡フレームが 0 件の場合は、対応する状態の消去と UI・保存状態の更新だけを行ってよい
- 観測入力を伴わない制御専用の入力でも設定変更要求を処理できる。その場合、`CommittedFrames` は 0 件でも、`ProfileSwitched` などの通知だけを返してよい
- 設定プロファイルの切り替え要求を受けたときの設定プロファイルの適用、消去、設定差し替え、`ProfileSwitched` 通知は `ITrackerEngine` の責務とする
- `TrackerCoordinator` は追跡エンジンの状態を直接消去せず、切替要求を次の `Update` 呼び出しへ渡すだけにする
- `ITrackerEngine` は `Update` の先頭で切替要求を消費し、以後のフィールド形状 / 検出情報処理を新しい設定プロファイルで実行する
- `ProfileSwitched` は新しい設定プロファイルの反映と状態の消去が完了した直後に通知し、同じ `TrackerUpdateResult` に `WorldFrameCommitted` がある場合はそれより前に並べる
- `ProfileSwitched` と `GeometryReset` が同じ `TrackerUpdateResult` に共存する場合も、`EmittedEvents` の順序を正とし、`TrackerCoordinator` はその順に自身が管理する状態遷移を適用する
- `TrackerCoordinator` は `Update` 呼び出しごとに適用待ちの要求を最大 1 件だけ処理中の要求に移し、その内容を結果の処理が終わるまで変更不能として扱う

### `TrackerPacketGenerator`

役割:

- `TrackerFrame` を公式形式の `TrackerWrapperPacket` に変換する

最低限の責務:

- `uuid` / `source_name` の設定
- 内部単位から公式形式の単位への変換
- 複数のボールの出力
- 主対象のボールを先頭に配置
- `kicked_ball` の組み立て
- 対応機能の設定

### `TrackerCoordinator`

`Tracker.Core` 側の実行時の責務境界として置く。`RUNTIME-HOST-005` では新規の `Tracker.RuntimeHost` プロジェクトは作らず、将来の RuntimeHost から再利用できる、UI に依存しない共通の周期処理を `Tracker.Core` に抽出する。

役割:

- raw vision パケットを `ITrackerEngine` へ渡す
- `TrackerUpdateResult` に含まれる `CommittedFrames` を、順に状態の保存先と通知先へ反映する
- 設定プロファイルの変更時に追跡エンジンへ切替要求を渡す
- 必要に応じて UDP 配信を行う
- 送信先の切り替えや UI に表示する適用中の設定名の更新など、追跡エンジンの外部にある状態を反映する

境界規則:

- `TrackerCoordinator`、`ITrackerPacketPublisher`、`TrackerPublisherOptions`、`TrackedSnapshot`、`TrackedSnapshotStore` は `Tracker.Core` に置く
- `UdpTrackerPacketPublisher` は、UI に依存しない送信処理として `Tracker.Core` に置いてよい
- `Tracker.Core` の実行時処理のソースコードは `Tracker.DebugHost`、Blazor、診断処理やキャプチャーの書き込み・読み取り処理、`VisionPacketCaptureSession`、`TrackerRenderSnapshot`、`TrackerPacketSnapshotLog`、`TrackerSnapshotAlignmentLog` を参照しない
- 診断ファイルの保存、render snapshot の記録、対応付けの記録、キャプチャーに属する補助ファイルのファイルパスへの依存は、DebugHost 側の別処理として扱う。これらを `Tracker.Core` の周期処理へ入れない
- DebugHost の `VisionReceiverService` は、UDP のデコード、未加工入力の状態保存、キャプチャーの後に、`Tracker.Core` の `TrackerCoordinator.ProcessPacket` を呼ぶ接続用の処理とする
- DebugHost の診断設定の解決結果は `TrackerResolvedOptions` に残せるが、`Tracker.Core` の周期処理が必要とする設定は `TrackerRuntimeResolvedOptions` として、`Tracker.Core` に置ける構造に分離する

処理規則:

- `CommittedFrames` が複数件ある場合、古い順に全件を処理する
- UI 用 `TrackedSnapshotStore` には最後の `CommittedFrame` を残す
- official tracker packet は各 `CommittedFrame` ごとに生成する
- 通知先への通知は `EmittedEvents` の順序に従う
- `TrackerCoordinator` は同一の `TrackerUpdateResult` を反映する際、まず `ProfileSwitched` / `GeometryReset` に対応する自身の状態遷移を `EmittedEvents` 順に適用する。その完了後に、`WorldFrameCommitted` と対応する `CommittedFrame`・公式形式のパケットを処理する
- 設定プロファイルの切り替え要求を受けたら、`TrackerCoordinator` は要求内容を保持したまま次の `Update` に 1 回だけ渡す
- 未加工の入力パケットが来ていなくても適用待ちの要求がある場合は、`TrackerCoordinator` は観測入力を伴わない制御専用の `Update` を即時呼び出し、その要求を処理しなければならない
- 設定プロファイルの切り替え要求に伴う追跡エンジンの状態の消去や `ProfileSwitched` の発火順制御は `TrackerCoordinator` 側で再実装しない
- `TrackerCoordinator` は設定プロファイルの切り替え要求の受付、`Update` の呼び出し、`TrackerUpdateResult` の処理を同じ直列化区間で扱い、1 回の `Update` の処理中に、その処理中の要求を上書きしない
- `TrackerCoordinator` は設定プロファイルの切り替え要求を受け取った時点では、送信先や UI に表示する設定名を即時に反映しない
- `ProfileSwitched` を受け取った時点で、その処理中の要求に対応する適用済み設定のスナップショット、送信先、適用中の設定名の表示、`TrackedSnapshotStore` の現在の設定名を先に更新する。その後、`TrackedSnapshotStore` の最新の追跡フレームと受信時刻を消去する
- 通知先への `ProfileSwitched` の通知は、上記の状態更新と処理中の要求の解放が完了した後に行う
- `GeometryReset` を受け取った時点でも `TrackedSnapshotStore` の最新の追跡フレームと受信時刻を消去し、その消去完了後に `OnGeometryReset` を通知する
- 任意の `Update` 呼び出しの結果の処理が完了した直後に、適用待ちの要求がまだ残っていれば、`TrackerCoordinator` はその場で次の制御専用の `Update` を直ちに実行する。ユーザーが最終的に求める設定に到達するまで、この処理を繰り返す
- `TrackerCoordinator` は、ユーザーが最終的に求める設定のスナップショット、適用待ちの要求、処理中の要求、適用済み設定のスナップショットを別々に保持する。切り替え完了前の古い状態の出力と、新しい状態の表示を混在させない
- これにより、設定プロファイルの切り替え後の最初の公式形式のパケットと `WorldFrameCommitted` は、必ず新しい送信先と適用中の設定プロファイルの下で処理される

### `TrackedSnapshotStore`

`Tracker.Core` 側の実行時の状態を読み取るための保存先とする。DebugHost の UI と将来の RuntimeHost は、この保存先を介して最新の追跡フレーム、適用中の設定プロファイル、送信統計を読む。

最低限の内容:

- 最新 `TrackerFrame`
- 受信時刻
- 現在の設定プロファイルの名前
- 送信成功回数 / 失敗回数
- 設定プロファイルの切り替え直後に追跡結果が未確定であることを表す空の状態

実行時の設定プロファイルの選択操作の UI 規則:

- 追跡結果の詳細表示領域は、適用中の設定名の表示と、設定プロファイルの切り替えを要求する操作部を持つ
- 適用中の設定プロファイルの表示には、`TrackedSnapshotStore.ActiveProfileName` の値を使う
- 設定プロファイルの候補一覧は `TrackerOptions.Profiles` から作り、空なら現在の適用中の設定プロファイル 1 件だけを操作できない状態で表示する
- 設定プロファイルの切り替え要求 UI は `ProfileSwitched` 直後に最新の追跡フレームが消去されても操作不能にならないよう、追跡結果の有無とは独立して描画する

## 入出力詳細

### 入力パケットの扱い

`SSL_WrapperPacket` の扱いは 3 種に分ける。

1. 検出情報のみを含むパケット
2. フィールド形状のみを含むパケット
3. 検出情報とフィールド形状の両方を含むパケット

これに加えて、`TrackerCoordinator` から追跡エンジンへ、観測入力を伴わずに設定変更要求だけを渡す、制御専用の `Update` 呼び出しを許可する。

処理規則:

- フィールド形状があれば、まずフィールド形状のスナップショットを更新する
- 検出情報があれば、`TCapture` を第 1 優先、`TSent` を第 2 優先の観測時刻として未処理の入力バッファに積む
- 未処理の入力バッファは、観測時刻、カメラ ID、観測フレームの番号の順で安定して並べ替え、処理する
- 最新に見えた観測時刻から `ReorderWindow` を越えた検出情報の集合を確定処理対象にする
- 検出情報の確定処理時は、その時点のフィールド形状のスナップショットを参照しつつ追跡処理を進める
- 検出情報がないパケットでは `frame_number` を無理に進めない
- すでに確定処理済みの観測時刻より古い遅れて到着したパケットは診断に記録し、状態更新には使わない
- フィールド形状の大幅な変更による初期化が発生した場合、未処理の入力バッファに残っている旧形状に属する検出情報は確定処理せず破棄する
- 観測入力を伴わない制御専用の `Update` では検出情報 / フィールド形状を追加せず、適用待ちの要求の消費と通知生成だけを行う

### 複数のカメラの時系列契約

既存 `VisionReceiverService` は UDP 到着順でパケットを渡すが、トラッカーは到着順に依存しないよう次を守る。

- `ReorderWindow`
  - パケットを並べ替えるための猶予時間
  - 設定値として外部から指定できるようにする
- `MergeWindow`
  - 同じフィールド全体の追跡フレームに統合してよい、カメラ間の時刻差の上限
  - 設定値として外部から指定できるようにする
- 1 つのフィールド全体の追跡フレームは「基準とする観測時刻から `MergeWindow` 以内のカメラごとの状態」のみを使って構成する
- フィールド全体の追跡フレームは観測時刻の昇順で確定し、同時刻の場合はカメラ ID の昇順で順序を安定させる
- `frame_number` は確定処理されたフィールド全体の追跡フレームごとに 1 ずつ進める
- フィールド形状だけを含むパケットは追跡フレームを進めないが、次に確定処理される追跡フレームから新フィールド形状を参照できる
- 観測時刻の基準は検出データの Unix 時刻であり、受信時刻は統合順序の決定に使わない

### 出力パケットの並び順

`TrackedFrame` の出力規則は次とする。

- `Balls[0]` は主対象のボール
- `Balls[1..]` は secondary ball（主対象以外で追跡を続けるボール）
- `Robots` はチームと ID で安定順を持たせる
- `Capabilities` は毎回同じ順で出す

補助対象のボールの安定順は次で固定する。

- 主対象を除いた残りを `visibility desc`、`last_visible_timestamp_ns desc`、`internal_track_id asc` の順で整列する
- `internal_track_id` は追跡エンジンの内部で単調増加の採番とし、状態の初期化時のみ採番を初期化してよい

### フィールド形状の扱い

フィールド形状は次の 2 つの用途を持つ。

1. 追跡時のフィールド内外判定
2. UI の表示と、自動レフェリー向けの付随情報の計算

フィールド形状更新規則:

- 新しいフィールド形状を受信したらスナップショットを置き換える
- 既存追跡状態はフィールド形状更新で捨てない
- ただしフィールドの長さ・幅やゴールの形状が設定閾値以上に変化した場合は、camera-local track、キック・接触の状態、フィールド全体の状態のスナップショットを初期化する。現行実装の `ClearPendingStateAndAdvanceLateCutoff` は、カメラごとのボール・ロボット追跡状態、ボールの接触・場外退出・統合後の識別状態、継続中のキック、直前の primary ball を消去する。また、ボール追跡 ID の採番とキック停止判定の回数を初期化する。`TrackerCoordinator` は `GeometryReset` を受けて、保存中の最新の `TrackerFrame` と受信時刻を消去する
- フィールド形状の大幅な変更による初期化時は、未処理の入力バッファも同時に消去し、旧形状に属する未確定の検出情報を次の追跡フレームへ持ち越さない
- フィールド形状の変更による初期化でも、`frame_number` と実行中のトラッカーの識別情報は維持する

## 構成

### `Tracker.Core`

- `ITrackerEngine`
  - raw vision からトラッカーの状態を進める中核契約
- `TrackerEngine`
  - 初期版の決定的トラッカー実装
- `TrackerPacketGenerator`
  - `TrackerFrame` から `TrackerWrapperPacket` を生成する
- `TrackerFrame` と各状態型
  - 内部状態表現

### `Tracker.DebugHost`

- raw vision の受信処理
  - 既存 `VisionReceiverService` を入力源として再利用する
- `TrackerCoordinator` を継続して動かす処理
  - raw vision パケットを `Tracker.Core` に流し、最新の追跡フレームを更新する
- 追跡スナップショットの保存先
  - UI 用に、フィールド全体の最新の追跡状態を保持する
- 追跡パケットの送信処理
  - 公式形式の追跡パケットをマルチキャストで配信する
- 表示画面
  - `Raw / Tracked` ボタン切替を提供する

### 層ごとの責務境界

- `VisionReceiverService`
  - UDP の受信と通信データのデコード
  - 問題再現用に、必要な調査時だけ、受信した UDP パケットを圧縮したキャプチャーとして保存する
- `VisionPacketStore`
  - 未加工入力のスナップショット保持
- `TrackerCoordinator`
  - 未加工入力から追跡結果への橋渡し
- `Tracker.Core`
  - 追跡アルゴリズム本体
- `TrackerPacketGenerator`
  - 公式の通信形式への変換
- `Tracker.CaptureReplay`
  - 保存済みキャプチャーを再生し、集計指標と条件式で回帰検証 / 調査を行う CLI
- `TrackedSnapshotStore`
  - 追跡結果の UI 読み取り用状態
- 表示画面
  - 可視化のみ

## データの流れ

1. `VisionReceiverService` が `SSL_WrapperPacket` を受信する
2. キャプチャーが有効な場合は、デコード前の UDP パケットの元データのバイト列と受信時刻を `jsonl.gz` に保存する
3. 未加工の入力パケットを `VisionPacketStore` に反映する
4. 同じ未加工の入力パケットを `TrackerCoordinator` が `TrackerEngine` に流す
5. `TrackerEngine` が `TrackerFrame` を更新する
6. `TrackerPacketGenerator` が公式形式の `TrackerWrapperPacket` を生成する
7. 送信処理が UDP マルチキャストへ送信する
8. UI は未加工入力のスナップショットまたは追跡スナップショットをボタンで切り替えて描画する

CaptureOn の比較ログを有効にする場合は、上記の自前トラッカーの一連の処理とは別に、`Tracker.DebugHost` が `TrackerConnectionLib` 経由で official tracker packet をキャプチャーする。キャプチャーした `TrackerWrapperPacket` は自前トラッカーのものも除外せず、受信できた追跡パケットをすべて、CaptureOn の session folder にある tracker packet snapshot sidecar JSONL へ保存する。自前トラッカーか外部トラッカーかの判別結果は、保存後の source role、source label、source metadata として扱い、判別できない場合も保存する。`Tracker.Core` の入力や状態更新には流さない。

## 設定

`Tracker.DebugHost` 側に `Tracker` 設定階層を追加する前提とする。

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

`VisionReceiver` 側は再生用のキャプチャー設定を持つ。

- `PacketCapture`
  - `Enabled`
  - `DirectoryPath`
  - `FilePrefix`
  - `FlushEachPacket`

キャプチャーは protobuf デコード前の UDP パケットの元データのバイト列を `jsonl.gz` に保存し、`receivedAt` と送信元の通信アドレスと通信ポートを同じ記録に持つ。保存された記録は順序通りに読み戻し、`SSL_WrapperPacket` へ復元してトラッカーへ再投入できるようにする。

capture metadata には、適用中の設定プロファイルの名前だけでなく、`TrackerOptions` 全体の `Profiles` の設定値と、実行時の上書きを適用した解決済みの設定値を保存する。名前だけでは再生時に当時の調整値を復元できないため、キャプチャーと同時点の設定値を含める。CaptureOn の記録では、同じ session folder 配下にあるキャプチャー、トラッカーの診断ログ、render snapshot、`diagnostics-samples.jsonl`、tracker packet snapshot sidecar JSONL、tracker snapshot alignment sidecar JSONL の相対パスも保存する。`DiagnosticsSampleLog`、`TrackerSnapshotLog`、`TrackerSnapshotAlignmentLog` には作成状態と記録・省略・エラー件数を保持し、source identity の一覧、source role / source label、対応付けの状態、時刻の対応規則も capture metadata に含める。

`Tracker.CaptureReplay` は、保存済みキャプチャーを `TrackerEngine` へ再投入する汎用 CLI とする。特定の不具合専用にせず、`packets`、`committed-frames`、`max-balls`、`max-robots`、`max-raw-balls` などの集計指標と、追跡結果の詳細の絞り込み条件式で自動テストや調査に使えるようにする。詳細は `frame` の番号でも絞り込めるようにし、ロボットの詳細には位置だけでなく向き・角速度も出して、未加工の検出情報と追跡結果の姿勢差分を CLI だけで比較できるようにする。`--settings` で `Tracker.DebugHost/appsettings.json` を読む場合は、適用中の設定プロファイルに `Tracker:RuntimeOverrides` を反映した追跡エンジンの設定を使う。

raw vision に対して自前トラッカーが遅れて見える原因を調べる場合は、キャプチャーファイルを手作業で読むのではなく、`Tracker.CaptureReplay` の汎用分析出力を使う。CLI は、未加工の SSL-Vision パケットの収録時の受信周期と、再生で確定された自前トラッカーの追跡結果の収録時の受信時刻、観測データの時刻、確定の契機となった入力位置を同じ概要出力に載せる。これにより、並べ替えの猶予時間、統合する時間幅、検出情報の欠落、トラッカー側での追跡結果の確定保留のどれが遅延要因かを報告できるようにする。この出力は特定の記録ファイルの共通名や表示元名へ依存させず、`--analyze-latency` のような明示的なコマンドラインオプションで、次回以降の遅延、古い状態の残留、更新周期の調査にも再利用する。

CaptureOn の比較ログがある場合、`Tracker.CaptureReplay` は session folder 内の tracker packet snapshot を保存する sidecar JSONL と alignment sidecar を capture metadata から読む。外部トラッカーのスナップショットを保存時の対応付けに従って自前トラッカーの確定済み追跡フレームと並べて再生・比較できるようにする。対応付けがない既存キャプチャーでは、時刻が近い記録を選ぶ規則を使い、正確な対応を保証しない推定であることを明示する。この CLI 比較経路はエージェント / 自動検証 / 調査用に保持し、診断 UI の実装後も削除しない。

診断画面と再生操作は、新規キャプチャーでは diagnostics sample sidecar を replay timeline と `Vision Input` / `ibis tracker` の主な読み取り元にする。`Vision Input` は diagnostics sample sidecar の採取記録にある未加工入力の概要、自前トラッカーは同じ diagnostics sample tick の追跡結果の概要から復元し、外部トラッカーは tracker packet snapshot と alignment sidecar、または選択時点以前の latest-before snapshot を使う。表示元の識別情報・分類・表示名、追跡フレームの番号・時刻、対応付けの時刻差、ボール・ロボット数、元データの復元状態を画面上で確認できるようにする。capture metadata、diagnostics sample sidecar、`tracker-packet-snapshots.jsonl`、alignment sidecar の欠落・空・読み取りエラーは区別して表示し、旧 render snapshot しかない記録は旧形式または機能制限付きの表示として扱う。等倍速の通常再生は表示更新を毎秒30回相当に抑えて実行環境側の経過時間へ追従する一方、replay timeline の選択、Field source、比較では任意の diagnostics sample tick を選べる経路を維持する。

未加工入力と追跡結果の診断で比較する検出情報は、現在届いたパケットではなく、確定済みの `TrackerFrame` を生成した元の検出情報群に結び付ける。これにより、並べ替えや統合の猶予時間で確定が遅れた追跡フレームと、未加工入力の件数、入力元の観測フレームの番号、カメラとの対応がずれない。

`Tracker.DebugHost` の診断画面は、新規キャプチャーでは `diagnostics-samples.jsonl` に保存した同一採取時点の未加工入力と自前トラッカーの追跡結果を `Vision Input` / `ibis tracker` の描画元にする。`*.render-snapshots.jsonl.gz` は旧形式の表示やフィールド形状などの補助情報として残してよいが、新規記録の物体表示や replay timeline の主な入力にはしない。diagnostics sample sidecar がない旧記録は非対応または機能制限付きの旧形式として明示し、timeline scrubber の操作でも新規経路へ render snapshot を代用しない。フィールドの操作ではページ全体をスクロールさせず、フィールドの拡大縮小・表示位置の移動と画面スクロールが干渉しないレイアウトを維持する。

既定配信先は公式形式のトラッカーの慣例値に合わせる。

- `224.5.23.2:10010`

ただし既定値は埋め込み固定せず、すべて設定から注入する。

- マルチキャスト用の通信アドレス / 通信ポート / 表示元名 / UUID は設定から指定できるようにする
- 追跡パラメーターは設定から指定できるようにする
- 未加工入力・追跡結果診断ログの明示出力先は `Tracker:Diagnostics:FilePath` で設定できるようにする
- キャプチャーは `VisionReceiver:PacketCapture:Enabled` を起動時の初期値として持ち、起動後は UI から有効・無効を切り替えられるようにする
- 初期版標準である Kalman filter の process noise / measurement noise / 対応付けを許可する閾値も、設定として外部から指定可能にする
- 近傍判定、可視性の減衰、キック速度の閾値、浮き球キック判定閾値も設定から指定できるようにする

要望として、これらの設定は最終的に UI から動的変更できる構成にする。

初期版では次の 2 段階で進める。

1. `appsettings` と設定値のバインドにより、すべての設定を外部から指定可能にする
2. 実行時設定保存領域を追加し、UI から変更した値を `TrackerCoordinator` が再読込できるようにする

### 設定プロファイルの切り替え

設定値は個別項目だけでなく、まとまりで切り替えられるようにする。

`Profiles` は 2 個以上の設定プロファイルを保持できるようにする。

初期例:

- `Profiles.Simulation`
- `Profiles.RealHardware`
- `Profiles.RealHardwareB`

それぞれの設定プロファイルには少なくとも次を含める。

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
  - 対応付けの判定
  - 外れ値上限
- `BallTracker`
  - process noise
  - measurement noise
  - 対応付けの判定
  - 外れ値上限
  - 追跡寿命
- `KickDetector`
  - キック判定閾値
  - 浮き球キック判定閾値
  - 接触余白

`RuntimeOverrides` の意図:

- 選択中の設定プロファイルの上に一時上書きをかける
- UI からの微調整はまずここへ入れる
- 設定プロファイルそのものの保存は別操作に分ける
- 初期版では UI の微調整はまず編集段階の上書き設定として `TrackerCoordinator` 側に保持し、追跡エンジンへは明示適用時のスナップショットだけを渡す
- 適用待ちまたは処理中の要求に入った上書き設定のスナップショットは変更不能とし、その後の UI 編集は次の要求候補にだけ反映する

切替要件:

- 起動時に任意の設定プロファイルを 1 つ選べる
- UI から登録済み設定プロファイルの一覧を選択できる
- UI からの切替後は `TrackerCoordinator` が新しい設定プロファイルへの切替要求を追跡エンジンへ渡す
- 同名の `VisionReceiver` 設定プロファイルが存在する場合、起動時と設定プロファイルの切り替え完了後にその受信元設定へ追従できる
- 個別値の微調整は選択中の設定プロファイルに対する上書きとして扱えるようにする
- 設定プロファイルは将来的に追加できる前提にする
- `TrackerCoordinator` はユーザーが最終的に求める設定のスナップショットを保持し、設定プロファイルの選択や上書き設定の適用操作のたびに、その内容を最新の意図に合わせて置き換える
- 未適用の切り替え要求がある間に別の設定プロファイルの選択が来た場合、`TrackerCoordinator` は適用待ちの要求を最新の要求で上書きし、要求を列として蓄積しない
- `ProfileSwitched` は追跡エンジンへ実際に渡されて適用された `RequestVersion` に対してのみ 1 回通知される
- 上書き設定の明示適用要求も初期版では同じ設定変更要求経路で扱い、設定プロファイルの名前と編集段階の上書き設定のスナップショットを組にして適用待ちの要求を置き換える
- 処理中の要求がある間に上書き設定を編集しても、その要求は書き換えず、次の適用待ちの要求候補だけを更新する
- 新しいユーザー操作で求める設定が、保持している最終的に求める設定と同値の場合だけ、重複とみなして新たな要求を作らない
- 最終的に求める設定が適用済みの設定と同値でも、適用待ちまたは処理中の要求が別の設定を指している場合は、その差分を打ち消す要求を残す

切替責務の境界:

- `TrackerCoordinator`
  - UI や設定保存領域から新しい設定プロファイルの選択を受け取る
  - 最終的に求める設定のスナップショットを保持し、後続の設定プロファイルの選択や上書き設定の適用操作が来たら、最新の意図で置き換える
  - 適用待ちの要求を 1 件だけ保持し、後続の設定プロファイルの選択や上書き設定の適用操作が来たら、最終的に求める設定へ収束する内容に上書きする
  - 新しいユーザー操作で求める設定が、保持している最終的に求める設定と同値の場合だけ、変更不要として破棄する
  - `Update` の呼び出し直前に適用待ちの要求を処理中の要求へ移し、結果の処理が完了するまで内容を固定する
  - `ProfileSwitched` を受けるまでは、送信先や適用中の設定名の表示を切り替えない
  - `ProfileSwitched` を受けた時点で、その処理中の要求に対応する適用済み設定のスナップショット、送信先、適用中の設定名の表示、`TrackedSnapshotStore` の現在の設定名を、原子的に切り替える
  - 受信側の設定プロファイルの切り替えは、`ProfileSwitched` 後に通知を受ける側で行い、トラッカー側の適用中の設定プロファイルと受信元設定の、外部から観測できる切り替え時点を揃える
  - 上記の状態遷移と保存状態の消去を完了してから、`OnProfileSwitched` を通知する
  - 任意の `Update` の結果の処理後に適用待ちの要求が残る場合は、その場で、最終的に求める設定に一致するまで制御専用の `Update` を繰り返す
  - `TrackerProfileSwitchRequest` を次の `ITrackerEngine.Update` へ 1 回だけ渡す
- `ITrackerEngine`
  - `TrackerProfileSwitchRequest` を受けたら `Update` の先頭で新しい設定プロファイルと `RuntimeOverrides` を確定する
  - 解決済みの基本設定と上書き設定の変更不能なスナップショットを、要求そのものから読む
  - `TrackerCoordinator` が重複要求を除外する前提とし、追跡エンジンは受け取った要求を実変更として扱う
  - camera-local track、キック・接触の状態、未処理の入力バッファ、フィールド全体の状態のスナップショットを消去する
  - 消去完了後に `ProfileSwitched` を `EmittedEvents` へ積む
  - 同じ `Update` 呼び出し内で後続パケットを処理する場合、その確定処理結果は新しい設定プロファイルの状態だけを使う

実行中のトラッカーの識別情報は設定プロファイルとは分離する。

- `Uuid` はプロセス起動中に一定とし、設定プロファイルの切り替えでは変更しない
- `SourceName` も初期版では起動時固定とし、設定プロファイルの切り替えでは変更しない
- `MulticastAddress` / `Port` は設定プロファイルの切り替えで変えてよい
- raw vision 受信元 `MulticastAddress` / `Port` / `InterfaceAddress` も設定プロファイルの切り替えで変えてよい

設定プロファイルの切り替え時の状態規則:

- `ITrackerEngine` は新しい設定プロファイルの適用時に camera-local track、キック・接触の状態、未処理の入力バッファ、フィールド全体の状態のスナップショットを消去する
- 最新フィールド形状のスナップショットと実行中のトラッカーの識別情報は維持する
- `frame_number` は単調増加を保つため継続する
- これにより、以前の設定プロファイルで推定した状態を、新しい設定プロファイルへ持ち越さない
- 設定プロファイルの切り替え要求を受けた `Update` 呼び出しでは、消去前に未処理の入力バッファを確定処理しない
- その入力が検出情報を含む場合、消去後に新しい設定プロファイルの空状態へ積み直して処理する
- これにより `ProfileSwitched` より後に通知される `WorldFrameCommitted` は必ず新しい設定プロファイルの状態だけから生成される

初期実装では、設定プロファイルの切り替えは `appsettings` と実行時設定保存領域で扱う。

## アルゴリズム設計

初期版は決定的な古典的追跡を採用する。設計時点ではパーティクルフィルターや、学習に基づく推定方式は使わない。

この方針では、TIGERs の次の実装を参考にする。

- `VisionFilterImpl`
  - カメラごとの処理、統合、品質評価、公開周期の分離
- `BallFilterPreprocessor`
  - ボール追跡処理群の統合、キック検出、キック推定の前処理分離
- `BallTracker`
  - 個々のボールの Kalman filter、追跡状態の健全度、追跡状態が確立したかの判定、外れ値の除外
- `RobotTracker`
  - 個々のロボットの位置と角度を別々に推定する処理、向きの連続化、外れ値の除外
- `TrackerPacketGenerator`
  - フィールド全体の状態表現から official tracker proto への専用変換
  - 競技規則の判定側が、未加工の入力パケットを直接扱わずに済む責務境界を保つ

寄せる対象は「考え方」と「責務分離」であり、Java の構造そのものを複製することではない。

### TIGERs との対応関係

- `VisionFilterImpl`
  - 本設計では `TrackerCoordinator` と `TrackerEngine` の分担に相当
- `RobotTracker`
  - 本設計のカメラごとのロボット追跡状態に相当
- `BallTracker`
  - 本設計のカメラごとのボール追跡状態に相当
- `BallFilterPreprocessor`
  - 本設計のボール統合、キック検出、キック推定の前処理段に相当
- `TrackerPacketGenerator`
  - 本設計の `TrackerPacketGenerator` にそのまま相当
- `BotBallContactAutoRefCalc`
  - 本設計の `BallContactState` と最終接触者計算に相当
- `BallLeftFieldAutoRefCalc`
  - 本設計の `BallLeftFieldState` に相当

### 全体方針

- カメラごとの未加工の観測を時系列順に処理する
- カメラごとにいったん局所的に追跡し、その結果を統合してフィールド全体の状態を作る
- 対象の識別はボール / チーム / ロボット ID で分けて管理する
- 対応付けは明示的な規則で決める
- 状態推定の動作は設定で調整できるようにする
- 推定処理の実装は差し替え可能にするが、初期版は直線運動を前提とする Kalman filter を標準とする
- ボールについては、追跡本体と、キックや追加の付随情報を推定する処理を分離する
- 初期版では、フィールド全体を扱う側に、推定状態を持ち続ける処理を置かない。camera-local track を不確かさに応じて重み付けして統合し、その追跡フレームのフィールド全体の状態を表すスナップショットとする

初期版実装契約:

- カメラごとのボール・ロボットの追跡状態は、観測値をそのまま上書きする簡易追跡ではなく、予測と観測更新を持つ線形 Kalman filter で更新する
- 各追跡状態は少なくとも状態の推定値と共分散相当の不確かさを保持する
- `ProcessNoise` は `KalmanProcessNoiseScale` を通して process noise の共分散へ、`MeasurementNoise` は `MeasurementNoiseVarianceScale` を通して measurement noise の共分散へ反映する。`Gate` は、観測値と予測値の差や距離に基づき、観測を追跡状態へ対応付けてよいかの判定に使う
- `KalmanInitialVelocityVariance`、`KalmanProcessNoiseScale`、`MeasurementNoiseVarianceScale` は設定プロファイルごとの外部設定値とし、静止時の検出情報の小さな揺れと移動への追従性の兼ね合いを、ソースコードの変更なしで調整できるようにする
- `VisibilityHalfLifeSeconds` は観測欠測時の追跡状態の生存管理に使う値であり、Kalman の共分散更新を省略する理由にはならない
- フィールド全体の状態を統合する際の不確かさは、カメラごとの Kalman filter の更新後の不確かさから導く
- 単純な等速外挿、観測値による上書き、手動での不確かさの加算だけで済ませる実装は、この初期版の契約を満たさない

段階分割:

1. raw vision の正規化
2. カメラ単位の追跡状態の更新
3. カメラをまたいだ統合
4. キック・接触・ボールの場外退出の計算
5. 公式の通信形式への変換
6. 競技規則の判定で使うイベント通知

### ロボット追跡

ロボットは `team + robot id` が既知なので、対応付け問題はボールより小さい。

TIGERs の `RobotTracker` に合わせ、位置系と向き系の推定処理を分ける。

処理段階:

1. カメラ単位の観測を正規化する
2. カメラごとに `team + robot id` のロボット追跡状態を維持する
3. 同一 `team + robot id` の複数のカメラ追跡状態を束ねて統合する
4. 既存追跡状態と ID で直接対応付ける
5. 位置・速度と、向き・角速度を、別々の推定処理で更新する
6. 向きを連続した角度として扱い、複数回の回転も補正する
7. 欠測時は予測のみ行い可視性を減衰する
8. 外れ値は対応付けの判定で除外する

TIGERs 由来で重視する点:

- 位置と向きの推定処理を分ける
- 速度上限、角速度上限による外れ値除外
- 追跡状態の健全度と更新頻度から可視性 / 品質値を作る
- カメラごとの追跡状態と統合後のロボットを分けて扱う

ロボットごとの可視性:

- 直近 1 秒程度の更新履歴を保持する
- 更新頻度と平均観測間隔から `visibility` を作る
- 長時間欠測したロボットは出力から外す

ロボットの状態表現:

- 状態量
  - 位置用 Kalman filter: `x, y, vx, vy`
  - 向き用 Kalman filter: `theta, omega`
- 観測量
  - 位置用 Kalman filter: `x, y`
  - 向き用 Kalman filter: `theta`
- 推定
  - 位置用 Kalman filter: 等速移動
  - 向き用 Kalman filter: 一定角速度

初期版でロボットを追跡する Kalman filter の要件:

- `team + robot id` ごとに camera-local track を維持し、位置系と向き系を独立した線形 Kalman filter として更新する
- 同一カメラ / チームの未加工の検出情報に、既に採用済みロボットと近すぎる別 ID ロボットが含まれる場合は、TIGERs の `Geometry.getBotRadius() * 1.5` 相当の距離を基準に後続候補を採用しない
- 近接して重複したロボットの採用順は一意に決める。検出の信頼度が高い候補を優先し、信頼度が同じ場合はロボット ID の小さい候補を優先する
- 同じカメラ・チームの別 ID の追跡状態の近くで、検出されたロボット ID だけが突然変わる候補は、同一 ID の通常の位置ずれより起きにくいものとして扱う。`RobotTracker.IdentitySwitchDistanceMm` の範囲では既存の識別情報を優先し、新しい ID の観測を採用しない
- 同じカメラ・チーム・ロボット ID の候補が統合する時間幅内に複数ある場合は、既存の同一 ID の追跡状態に近い候補を遠方の候補より優先する。後続の検出情報に含まれる誤った ID により、追跡位置を瞬間移動させない
- 向き観測は観測更新前に角度を連続化して、`-pi` / `pi` 境界の不連続を状態推定外へ漏らさない
- 向き用 Kalman filter の観測分散、予測分散、初期角速度分散は、角度 rad と角速度 rad/s に対応する単位で扱い、位置 mm 用の不確かさをそのまま流用しない。設定プロファイルの `MeasurementNoiseVarianceScale`、`KalmanProcessNoiseScale`、`KalmanInitialVelocityVariance` は、既定値との比率で角度系の基準値へ反映する
- 静止したロボットの向きの小さな揺れが、過大な角速度として表示されないよう、向き用 Kalman filter の速度更新には角速度の上限を適用する
- 対応付けの判定は、生の観測値との差分だけではなく、予測状態に対する対応付け規則として使う
- 観測が欠けた場合は予測のみを行い、可視性減衰と追跡状態削除判定は別責務として扱う
- 統合に使う不確かさは、最新の観測の信頼度だけでなく、状態推定処理後の位置の不確かさを基準にする
- 欠測により可視性が十分低下した更新の途絶えた追跡状態は内部状態として短時間残せるが、追跡フレーム / 表示画面 / 公式形式のパケットへ出し続けてはならない
- 外部出力の可否は `OutputVisibilityThreshold` で判定し、TIGERs のロボットの品質判定の初期閾値 `0.05` を設定値の基準とする

向きを連続した角度として扱い、`-pi` / `pi` 境界での跳びを状態管理側で吸収する。

### ボール追跡

ボールは ID がないため、対応付けを明示設計する。

TIGERs の `BallTracker` と `BallFilterPreprocessor` に合わせ、ボールの処理は、個別の追跡状態群を扱う段階と、主対象のボールを決めてキックを推定する段階の 2 段に分ける。

処理段階:

1. カメラごとの未加工のボール観測を正規化する
2. カメラごとにボール追跡状態群を維持する
3. 追跡状態ごとに予測位置との距離と最大速度上限で外れ値を除外する
4. 更新できた追跡状態は健全度を上げ、まだ確立していない追跡状態と、観測を重ねて確立した追跡状態を分ける
5. カメラをまたいでボール追跡状態群を統合する
6. 状態推定処理で得られた直前のボール位置の近くを優先する探索半径で、主対象の候補を絞る
7. 古くなった追跡状態は可視性を減衰し、閾値以下で除外する
8. 主対象のボールを競技規則上重要な優先度で 1 つ選ぶ

TIGERs 由来で重視する点:

- `BallTracker` 単位の Kalman filter
- 追跡状態の健全度と、観測を重ねて追跡状態が確立したかの判定
- 最大速度による外れ値除外
- 直前のボール位置や空中ボール投影位置を基準にした探索半径
- カメラごとに 1 つまでの代表追跡状態を選んで統合する考え方

ボールごとの生存管理:

- 生成直後は、まだ追跡状態が確立していないものとして扱う
- 一定回数の更新後に、追跡状態が確立したとみなす
- まだ追跡状態が確立していないボールは、主対象の候補としての優先度を下げる
- 初期版では TIGERs の `grownUpAge = 3` に合わせ、primary ball 以外の secondary ball は、3 回以上観測されて追跡状態が確立したものだけを外部出力する
- 1 回だけ検出された、実体のない補助対象のボールは、camera-local track として短時間残せる。ただし、追跡結果・表示画面・公式形式のパケットへは出さない
- 長時間更新されない追跡状態は削除する

ボールの状態表現:

- 状態量
  - `x, y, z, vx, vy, vz`
- 観測量
  - `x, y, z`
- 推定
  - 等速移動

初期版でボールを追跡する Kalman filter の要件:

- カメラごとのボールの各追跡状態に線形 Kalman filter を持たせ、観測による更新と、欠測時の予測を分ける
- `ProcessNoise` と `MeasurementNoise` は、ボールを追跡する Kalman filter の共分散更新に直接使う
- `Gate` は新規観測を既存ボール追跡状態へ結び付ける可否判定に使い、対応付け失敗時だけ新規追跡状態を生成する
- 追跡状態の不確かさは、観測の信頼度の単純な逆数ではなく、Kalman filter の観測更新後の共分散から導く
- カメラをまたぐ重み付き統合では、ボールの Kalman filter の観測更新後の不確かさを重みに使う
- 追跡状態の健全度、追跡状態の確立、可視性の管理は、Kalman filter による更新とは別の責務とし、状態推定の代わりにはしない
- 欠測により可視性が十分低下した更新の途絶えた追跡状態は内部状態として短時間残せるが、追跡フレーム / 表示画面 / 公式形式のパケットへ出し続けてはならない
- 外部出力の可否は `OutputVisibilityThreshold` で判定可能とし、TIGERs のボールが見えなくなった場合の寿命の初期値 `1.0s` を、`TrackLifetimeNs` の基準とする

複数のボール対応:

- 内部では `TrackedBallState` を複数保持する
- 外部の `TrackedFrame.Balls` には primary ball と、追跡状態が確立した secondary ball だけを出す
- `Balls[0]` は主対象のボールに固定する
- 主対象の選定では直前の主対象の追跡状態を優先し、その後に可視性、経過時間、フィールド上の重要度、直近の接触との整合を使う
- secondary ball は出力規則節の安定した並べ替えに従う。ただし、1 観測フレームだけに現れた誤検出を抑えるため、まだ追跡状態が確立していないものは出力しない

### カメラ統合

複数のカメラから同一対象が見えるときは、単純平均ではなく「規則に基づく候補選別 + 不確かさに応じた重み付き統合」を行う。

- まず位置の近さによる判定と ID 規則で同一候補を束ねる
- ボールでは、状態推定処理で得られた直前のボール位置、または浮き球キックの投影位置の近くにある camera-local track だけを、主対象の候補に残す
- ボールはカメラごとに代表追跡状態を 1 つ選んでから統合する
- ロボットは同一 `team + robot id` の camera-local track だけを束ねる
- 統合自体はカメラごとの状態推定の内部状態の不確かさを重みとして使う
- 検出の信頼度やカメラ固有品質は不確かさ補正係数として将来拡張できるようにする
- 視線角やカメラ固有品質を後で入れられるよう拡張点を持つ
- 初期版では統合後のフィールド全体を扱う側に別の状態推定をもう 1 段かけない
- `TrackedBallState` / `TrackedRobotState` は、camera-local track 群から追跡フレームごとに合成した フィールド全体の状態を表すスナップショットとする

統合時の安定性要件:

- 同時刻近傍の観測のみを統合対象にする
- 明らかに古いカメラ追跡状態は統合対象から外す
- 統合順序で結果がぶれないよう、安定した並び順を持つ
- 統合前に候補を、カメラ ID、カメラ内の追跡 ID の順で安定して並べ替える
- 不確かさが同じ場合の候補選択は、カメラ ID とカメラ内の追跡 ID で決める

### キック検出

キックは自動レフェリーに重要なので初期版から入れる。TIGERs の `BallFilterPreprocessor` のように、ボールの主追跡から分離した前処理段で扱う。

候補条件:

- ボール速度が短時間で閾値以上に増加した
- 増加直前に近傍ロボットの接触候補がある
- ボール進行方向とロボット前方がある程度整合する

処理方針:

- 早期検出系と安定検出系の 2 系統を持てる構造にする
- 推定結果がある場合はそちらを優先する
- キック検出後は平面キック・浮き球キックの推定器へ流す

初期版の最小実装:

- 1 本の判定器から開始してよい
- ただし構造は 2 系統へ増やせる形にしておく

出力:

- キックしたロボットの ID
- キック開始時刻
- 開始位置 / 初速度
- 平面キック・浮き球キック候補
- 停止予測があれば停止時刻 / 停止位置

`kicked_ball` の寿命規則:

- 公式の通信形式には、キック済みでボールが動き続けている間だけ出力する
- 継続中のキックは、対応するボールの平面速度が `KickStillMovingSpeedThreshold` を下回る状態が `KickStillMovingGraceFrames` 続いたら消去する
- 対応するボールの追跡状態が削除された場合、または `BallInvisibleTimeout` を超えて見えなくなった場合も消去する
- 別のキックが確定した場合は古いキックを置き換える

平面キックと浮き球キックは次で近似判定する。

- `vz` または `z` 上昇が閾値以上なら浮き球キック候補
- それ以外は平面キック候補

### ボール接触

接触は専用状態として保持する。TIGERs の `BotBallContactAutoRefCalc` と同様に、「現在接触中」と「最終接触者」を分ける。

- ロボット半径 + ボール半径 + 余白に入ったら接触候補
- 方向整合や相対速度で誤判定を減らす
- 現在接触中と最終接触者を分けて保持する

出力規則:

- 現在接触中がいなければ、直前のキック情報も使って最終接触者を維持する
- 複数候補がある場合は距離と進行方向で優先順位を付ける

### ボールのフィールド外退出

フィールド形状の線分群またはフィールド寸法を使って判定する。TIGERs の `BallLeftFieldAutoRefCalc` と同じく、フィールド全体の状態から、ボールが場外へ出た位置とフィールド内外の状態を作る。

- ボール中心がフィールドの内側から外へ出た時刻を記録する
- 横切った線分種別を持つ
  - タッチライン
  - ゴールライン
  - ゴール内部
- 複数のボールがある場合も各ボール追跡状態ごとに判定する

ゴールの判定では、次の状態を分けて保持する。

- ゴール開口部を通ってゴール内部に入ったか
- 単にゴールラインを横切っただけか

### 競技規則の判定との連携

自動レフェリーなどの競技規則の判定側は、未加工の入力パケットや camera-local track を直接読むのではなく、確定済みのフィールド全体の状態を表すスナップショットとキックや接触などの通知を読む前提とする。

競技規則の判定側へ渡す基本要素:

- 最新 `TrackerFrame`
- 必要に応じた、直近数件の追跡フレームの履歴
- キックや接触などの通知
  - `WorldFrameCommitted`
  - `KickDetected`
  - `ContactChanged`
  - `BallLeftField`
  - `ProfileSwitched`
  - `GeometryReset`

設計方針:

- 競技規則の判定処理ごとに、通知を受け取る処理を持てる構造にする
- 通知を受け取る処理は raw vision パケットを直接購読しない
- 通知を受け取る処理は `TrackerFrame` と、`TrackerEvent` で表すキックや接触などの通知を入力にする
- キック / 接触 / ボールの場外退出の計算はトラッカー側で担当し、競技規則の判定側で同じ計算を重複させない
- 競技規則を判定する順序に依存しないよう、トラッカーで確定した順に通知する
- 競技規則の判定処理が追加されても、追跡処理の中核となる数値処理へ影響しない責務境界を保つ

通知順は次で固定する。

1. 状態の消去や意味の切替を伴う通知
   - `ProfileSwitched`
   - `GeometryReset`
2. 追跡フレームの本体
   - `WorldFrameCommitted`
3. その追跡フレームに従属する派生通知
   - `KickDetected`
   - `ContactChanged`
   - `BallLeftField`

同一段階内の並びは `TrackerUpdateResult.EmittedEvents` に格納された順を正とする。

最小限の公開 API の考え方:

- `ITrackerObserver`
  - `OnProfileSwitched(string profileName)`
  - `OnGeometryReset()`
  - `OnWorldFrameCommitted(TrackerFrame frame)`
  - `OnKickDetected(KickEventState kick, TrackerFrame frame)`
  - `OnContactChanged(TrackerFrame frame)`
  - `OnBallLeftField(BallLeftFieldState state, TrackerFrame frame)`

初期版では、通知先を同期的に呼び出す方式でよい。将来、非同期配信や通知を中継する仕組みへ差し替えられるよう、`TrackerEngine` 本体から通知の配信処理を分離できる余地を残す。

### 状態推定設定

状態推定と対応付けの判定の主要設定は外部から指定できるようにする。

- ロボットの process noise
- ロボットの measurement noise
- ロボットの対応付けを許可する距離
- ボールの process noise
- ボールの measurement noise
- ボールの対応付けを許可する距離
- 未更新の状態を無効にするまでの時間
- 可視性の減衰
- キック閾値
- 浮き球キック閾値

設定源は固定しない。`Tracker.Core` は設定オブジェクトを受け取り、`Tracker.DebugHost` がデバッグ起動時の設定供給責務を持つ。

## UI 方針

- 現在の raw vision viewer は維持する
- 表示画面上部または詳細表示領域に `Raw / Tracked` のボタン切替を置く
- 追跡結果の表示では状態推定処理後のボール / ロボット / キック情報 / 接触情報を確認できるようにする
- 未加工入力と追跡結果でフィールド表示の見た目を揃え、比較しやすくする

追跡結果の表示の最低限:

- 主対象のボール
- 補助対象のボール
- 追跡中のロボット
- 現在の設定プロファイルの名前
- キックされたボールの有無
- 最終接触者
- ボールのフィールド内外

## テスト方針

TDD の最初の対象は `Tracker.Core` の中核契約に限定する。

### 最初に失敗テストを作る対象

- `TrackerPacketGenerator` が内部単位から公式の通信形式で定める単位へ正しく変換する
- `TrackerPacketGenerator` が `kicked_ball` と対応機能を正しく埋める
- `TrackerPacketGenerator` が複数のボールを `TrackedFrame.Balls` に出し、主対象のボールを先頭に置く
- `TrackerPacketGenerator` が `TrackerFrame.data_timestamp_ns` を `TrackedFrame.timestamp` に使う
- `TrackerEngine` が 1 件の観測フレームの raw vision から primary ball とロボットを持つ `TrackerFrame` を返す
- `TrackerEngine` が複数のボール観測を別追跡状態として保持できる
- `TrackerEngine` が 2 件の観測フレームで得た同一ロボットの観測から速度を推定する
- `TrackerEngine` の閾値、process noise、measurement noise の設定値が設定オブジェクトから供給される
- `TrackerEngine` が複数の設定プロファイルから選択された 1 つを受け取れる
- `TrackerEngine` が到着順の異なる同一入力でも同じ観測時刻順で追跡フレームを確定する
- `TrackerEngine` が `MergeWindow` 外のカメラ観測を同一の追跡結果に混ぜない
- `TrackerEngine` が確定したフィールド全体の追跡フレームに対し、キックや接触などのイベントを安定した順序で通知する
- `TrackerEngine` が 1 入力から `0..N` 件の `CommittedFrames` を返せる
- `TrackerEngine` がフィールド形状の変化による初期化時に未処理の入力バッファを消去する

### 具体的な最初のテスト候補

1. `TrackerPacketGenerator` に 2 個のボールを与えると、主対象に指定したボールが `Balls[0]` になる
2. `TrackerPacketGenerator` が `mm` を `m` に、`ns` を `s` に変換する
3. `TrackerPacketGenerator` が `CAPABILITY_DETECT_MULTIPLE_BALLS` を含める
4. `TrackerPacketGenerator` が `TrackerFrame.data_timestamp_ns` を `TrackedFrame.timestamp` に使う
5. `TrackerEngine` がフィールド形状だけを含むパケットを受けても、例外なくフィールド形状のスナップショットを更新する
6. `TrackerEngine` が 2 件の観測フレームで得た同一ロボットの観測から非 0 の速度を出す
7. `TrackerEngine` が離れた 2 つのボールの観測を、別の追跡状態として保持する
8. `TrackerEngine` が選択する設定プロファイルの名前を切り替えると、新しい設定を参照する
9. `TrackerEngine` が到着順の異なる同一入力でも同じ追跡結果の順序を返す
10. `TrackerEngine` が `MergeWindow` を超えたカメラ観測を別の追跡結果に分ける
11. `TrackerPacketGenerator` が補助対象のボールを安定した並べ替えで出力する
12. `TrackerPacketGenerator` が、ボールの移動が継続していないキックを `kicked_ball` に出さない
13. `TrackerCoordinator` が設定プロファイルの切り替え時に追跡状態を初期化しても `frame_number` を巻き戻さない
14. `TrackerObserver` が未加工の入力パケットではなく、確定済みの `TrackerFrame` と `TrackerEvent` で表す通知を受け取る
15. `TrackerCoordinator` が 1 入力で複数 `CommittedFrames` を受けたとき中間の追跡フレームを落とさない
16. `TrackerObserver` が `ProfileSwitched` / `GeometryReset` / `WorldFrameCommitted` / 派生通知を固定順で受け取る
17. フィールド形状の大幅な変更時に、旧形状に属する未処理の検出情報が破棄される

### 後続で追加する対象

- ボールの可視性の減衰
- 直近の接触・最終接触者
- ボールの場外退出判定
- フィールド形状が大幅に変わったときの追跡状態の初期化
- 遅れて届いたパケットの診断
- 未加工入力と追跡結果の表示を切り替える統合確認

## 作業分割方針

- `TRACKER-000`: 設計書と進捗管理ファイル作成
- `TRACKER-001`: `Tracker.Tests` から `Tracker.Core` を参照可能にし契約テスト基盤を作る
- `TRACKER-002`: パケットの生成処理の契約テストを追加する
- `TRACKER-003`: 追跡エンジンの時系列契約テストを追加する
- `TRACKER-004`: `TrackerFrame` / 状態型 / `TrackerUpdateResult` / イベントと通知先の契約を実装する
- `TRACKER-005`: `TrackerPacketGenerator` を実装する
- `TRACKER-006`: `TrackerEngine` の順序を並べ替えるバッファと確定可能な入力を順に処理する仕組みを実装する
- `TRACKER-007`: `TrackerEngine` の設定プロファイルの切り替え / フィールド形状の変化による初期化 / 通知順を実装する
- `TRACKER-008`: ロボットの追跡とロボット追跡結果の統合を実装する
- `TRACKER-009`: ボールの追跡と主対象・補助対象のボール選定を実装する
- `TRACKER-010`: キックと接触に関する情報を実装する
- `TRACKER-011`: ボールの場外退出に関する情報を実装する
- `TRACKER-012`: 旧 `Tracker.Server` へ追跡エンジンとパケット配信を統合する
- `TRACKER-013`: トラッカーとネットワークの設定値のバインドを統合する
- `TRACKER-014`: 設定プロファイルの切り替え要求経路を統合する
- `TRACKER-015`: 追跡結果の表示画面と未加工入力・追跡結果切り替えを追加する
- `TRACKER-016`: 追跡結果の診断表示を追加する
- `TRACKER-017`: 実行中の設定プロファイルの表示・操作 UI を追加する
- `TRACKER-018`: トラッカー初期版のビルドとテスト証跡を取得する
- `TRACKER-019`: トラッカー初期版の統合観点検証を行う
- `TRACKER-020`: トラッカー初期版の最終レビューと進捗管理ファイルの同期を行う
- `TRACKER-027`: TIGERs 由来の近接重複ロボット / 短命ボール抑制を追加する

契約段階の着手順:

1. `TRACKER-001` で `Tracker.Tests` から `Tracker.Core` を参照し、テストで共有する準備処理とテストデータ基盤を整える
2. `TRACKER-002` でパケットの生成処理の失敗契約テストを固定する
3. `TRACKER-003` で追跡エンジンの時系列契約テストを固定する
4. `TRACKER-004` で内部状態表現・状態型・イベントと通知先の契約（`TrackerEvent` / `ITrackerObserver`）を固定する
5. `TRACKER-005` でパケットの生成処理実装へ進む

## 承認条件

`TRACKER-000` の設計承認は完了済みであり、以後はこの設計書を正本として契約段階以降を進める。

- 仕様変更や作業再分割があれば先にこの設計書と進捗管理ファイルを同期する
- 契約段階では先行する失敗テストと契約の範囲を先に固定する

## 前提

- ソースコードを根拠に確認する方針で進める
- 初期入力は raw vision のみ
- 配信はライブラリ + UDP
- 表示画面は `ssl-vision-client` のように未加工入力・追跡結果をボタンで切り替える
- 無関係な作業ディレクトリの変更は保護する
