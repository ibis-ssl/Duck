# Tracker 調査メモ

## 目的

`Tracker.Core` の設計を、Tigers の AutoRef 関連実装と official tracker proto の実態に寄せるための根拠を整理する。

## 調査対象

- `Tracker/Tracker.Core/Design/Ref/AutoReferee`
- `SslProto/src/external/ssl-game-controller/proto/tracker`
- `TrackerConnectionLib`
- 既存の `Tracker.Server` raw vision viewer 実装

## 参照した主なファイル

- `modules/moduli-vision/src/main/java/edu/tigers/sumatra/vision/VisionFilterImpl.java`
- `modules/moduli-vision/src/main/java/edu/tigers/sumatra/vision/BallFilterPreprocessor.java`
- `modules/moduli-vision/src/main/java/edu/tigers/sumatra/vision/tracker/BallTracker.java`
- `modules/moduli-vision/src/main/java/edu/tigers/sumatra/vision/tracker/RobotTracker.java`
- `modules/common/src/main/java/edu/tigers/sumatra/filter/tracking/TrackingFilterPosVel2D.java`
- `modules/common/src/main/java/edu/tigers/sumatra/filter/tracking/TrackingFilterPosVel1D.java`
- `modules/moduli-wp/src/main/java/edu/tigers/sumatra/wp/TrackerPacketGenerator.java`
- `modules/moduli-autoreferee/src/main/java/edu/tigers/autoreferee/engine/calc/BotBallContactAutoRefCalc.java`
- `modules/moduli-autoreferee/src/main/java/edu/tigers/autoreferee/engine/calc/BallLeftFieldAutoRefCalc.java`
- `proto/tracker/ssl_vision_detection_tracked.proto`
- `proto/tracker/ssl_vision_wrapper_tracked.proto`

## 主要な調査結果

### 1. Tigers は 1 段の単純 tracker ではない

Tigers は少なくとも次の段分離を持つ。

1. camera ごとの raw detection 受信
2. camera ごとの追跡
3. camera 横断の統合
4. ball 向け前処理
5. kick 検出と kick 推定
6. world model 生成
7. official tracker proto 変換
8. AutoRef 向け追加計算

`VisionFilterImpl` は全体の進行を持つが、個々の責務は `BallTracker`、`RobotTracker`、`BallFilterPreprocessor`、`TrackerPacketGenerator` などに分かれている。

### 2. 個別 track の基底推定器は Kalman filter

`TrackingFilterPosVel2D` はソースコメントで次を明示している。

- 線形 Kalman filter
- 状態量は位置と速度
- 観測量は位置のみ

`TrackingFilterPosVel1D` も同様に、1 次元の位置と速度を扱う線形 Kalman filter である。

これを実際に使っている箇所:

- `BallTracker`
  - `TrackingFilterPosVel2D`
- `RobotTracker`
  - 位置に `TrackingFilterPosVel2D`
  - 向きに `TrackingFilterPosVel1D`

したがって、Tigers の「個別追跡器の基底」は Kalman filter ベースと言ってよい。

### 3. ただし Tigers の本質はその上の分離にある

Kalman filter だけでは Tigers の考え方を言い切れない。

特に重要なのは次である。

- `VisionFilterImpl`
  - camera ごとの処理と統合後 frame の生成を分離
- `BallFilterPreprocessor`
  - ball tracker 群の統合
  - kick 検出
  - kick 推定
- `TrackerPacketGenerator`
  - world model から official proto への変換を専用化
- `BotBallContactAutoRefCalc`
  - 現在接触中と最終接触者を分離
- `BallLeftFieldAutoRefCalc`
  - ball の field 外退出位置と内外状態を別計算

つまり、Tigers に寄せるなら「個別 track の推定器」だけではなく、「追跡本体」と「追加メタ計算」を分離する必要がある。

### 4. ball は複数 track を持ちうる

`BallFilterPreprocessor` は `List<BallTracker>` を受け取り、`BallTrackerMerger` で統合候補を作る。

ここから分かること:

- raw 観測から 1 本の ball だけを即決しない
- 複数 track を持ちながら primary ball を決める
- camera ごとに代表 track を選ぶ考え方がある

これは `CAPABILITY_DETECT_MULTIPLE_BALLS` 対応と整合する。

### 5. robot は camera ごとの track と統合後 robot を分ける

`RobotTracker` は individual tracker として働き、`VisionFilterImpl` 側で merged robot を作る。

重要点:

- `team + robot id` で追跡
- 位置と向きを別 filter で扱う
- 向きは巻き戻し補正を持つ
- 更新頻度と health から quality を作る

### 6. kick は ball 主追跡から分けて考える

`BallFilterPreprocessor` は `KickDetector` と `EarlyKickDetector` を使い分けている。

重要点:

- 早期系と安定系の複数検出器を持てる
- kick 検出後に `StraightKickEstimator`、`ChipKickEstimator` などへ流れる
- kick は ball state の副産物ではなく、別責務である

### 7. AutoRef に必要な情報は official proto より広い

`BotBallContactAutoRefCalc` と `BallLeftFieldAutoRefCalc` から、AutoRef で必要なのは少なくとも次だと分かる。

- 現在接触中の robot
- 最終接触者
- kicked ball
- ball の field 内外状態
- field 外退出位置

したがって、`Tracker.Core` は official `TrackedFrame` だけではなく、内部用の richer model を持つ必要がある。

### 8. official tracker proto 変換は専用層に分けるべき

`TrackerPacketGenerator` は world model から `TrackerWrapperPacket` を組み立てる専用クラスである。

重要点:

- `uuid` は source 単位で安定
- `source_name` は tracker 実装名
- unit 変換はここで行う
- `kicked_ball` と capabilities もここで埋める

この構成は `Tracker.Core` でも維持すべきである。

### 9. multi-camera 統合は単純平均ではない

Tigers の統合は「規則選別なしの平均」ではない。

`BallFilterPreprocessor.BallTrackerMerger` から分かること:

- 直前 ball 近傍または chip 投影近傍で search radius を作って候補を絞る
- camera ごとに 1 本までの代表 tracker を選ぶ
- その後に `BallTracker.mergeBallTrackers` で uncertainty-weighted merge を行う

`RobotTracker.mergeRobotTrackers` も同様に、位置・速度・向きを uncertainty で重み付けして統合している。

したがって、Tigers に寄せるなら次の 2 段で書くのが正確である。

1. 規則ベースで統合候補を選別する
2. 候補群を tracker の不確かさに応じて重み付き統合する

### 10. official proto の時刻と source identity には強い契約がある

official proto のコメントから、設計で先に固定すべき事項がある。

- `TrackedFrame.timestamp`
  - unix timestamp の data time
- `KickedBall.start_timestamp`
  - unix timestamp の kick 開始時刻
- `TrackerWrapperPacket.uuid`
  - source が動作中のあいだ一定
- `kicked_ball`
  - kick されており、かつ still moving の間だけ出る想定

よって設計では少なくとも次を先に固定すべきである。

- `TCapture` / `TSent` / receive time / processing time のどれを data time として採用するか
- `uuid` を runtime profile 切替で変えてよいか
- `kicked_ball` をいつ clear するか

### 11. 既存 `Tracker.Server` の受信は arrival order であり、そのままでは deterministic merge にならない

既存 `VisionReceiverService` は UDP datagram を decode して store へ渡すだけで、camera 間の event time 再順序化は持っていない。

このため、`Tracker.Core` 側で次を設計する必要がある。

- event time の決め方
- reorder window
- merge window
- late packet の扱い

これを決めずに「時系列順処理」や「同時刻近傍だけ統合」を書くと、到着順依存が残る。

## `Tracker.Server` への示唆

既存 `Tracker.Server` は raw vision receiver と viewer をすでに持つ。

これを踏まえると、最小構成は次になる。

1. `VisionReceiverService` はそのまま raw source として残す
2. raw packet を `VisionPacketStore` に入れる
3. 同じ raw packet を tracker coordinator が `TrackerEngine` に渡す
4. `TrackerFrame` を別 store に保持する
5. `TrackerPacketGenerator` で official tracker packet を配信する
6. UI は raw / tracked を切り替える

## 設計へ反映すべき事項

### 必須

- `Tracker.Core` は internal model と official proto 変換を分離する
- ball は複数 track を持てる
- primary ball 選定は別責務にする
- robot は位置と向きを別 filter で扱う
- kick 検出は追跡本体から分離する
- contact / last toucher / ball left field は internal metadata として持つ
- 設定は tracker 単位ではなく、複数 profile を切り替えられるようにする
- multi-camera 統合は「候補選別 + uncertainty-weighted merge」で書く
- timestamp / uuid / kicked_ball lifetime の proto 契約を設計へ落とす
- arrival order を隠蔽する reorder window と merge window を設計へ入れる

### 初期段階では簡略化してよいが、設計で先に受け口を持つもの

- 早期 kick 検出系と安定 kick 検出系の 2 系統
- camera 固有品質を merge に反映する拡張点
- 実行時 profile 切替
- UI からの parameter 上書き
- late packet diagnostics

## 設計への結論

`Tracker.Core` は次の 5 層を持つ設計にするのが妥当である。

1. raw vision 正規化
2. 個別 track 更新
3. camera 横断統合
4. kick / contact / field 外退出の追加計算
5. official tracker proto 変換

Tigers に寄せるとは、

- Kalman filter を使うことだけではなく
- 各責務をこの粒度で分けること

を意味する。
