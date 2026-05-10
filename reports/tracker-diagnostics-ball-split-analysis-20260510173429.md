# Tracker diagnostics ball split analysis

## 対象ログ

- `reports/tracker-diagnostics-20260510T082911533Z-190a39676b7d436cb2c038e5e77b2c89.log`
- 行数: 304

## 観測サマリ

- `rawCamera` は全行 `0` だった。
- `rawBalls` は `0`: 12行、`1`: 265行、`2`: 27行。
- `trackedBalls` は `1`: 138行、`2`: 150行、`3`: 16行。
- 最後付近のキック後と見られる区間では、raw はほぼ常に1個なのに tracked が2から3個になっていた。

## 最後付近の分裂区間

### キック前の安定状態

- `08:31:03.802` から `08:31:21.883` 付近までは、raw ball はおおむね `(677, 1126)` 付近で、tracked primary は `#228` として安定していた。
- この区間では `#228` が `cams=0/1` で、camera 0 と camera 1 の観測が同じボールとしてmergeされている。

### キック直後

- `08:31:22.299` で raw ball が `x=-393.4, y=1067.2` に移動し、tracked primary は `#252` に切り替わった。
- 同時に旧ボール `#228` が `x=677.4, y=1119.9, vis=0.978` として残り、tracked は2個になった。
- `#228` はその後も visibility が減衰しながら残り、`08:31:23.258` まで出力されていた。

### 3個に増える区間

- `08:31:22.522` から `08:31:23.258` まで、tracked は最大3個になった。
- 内訳は次の3種類:
  - `#252`: camera 0 由来の移動中ボール。visibility は1のまま。
  - `#253` から `#269`: camera 1 由来の別track。毎frameのように新しい internal id になっている。
  - `#228`: キック前の旧primary。観測なしで visibility が減衰している残留track。
- 例: `08:31:22.522` では raw は1個 `x=-1827.2, y=993.2` だが、tracked は `#252`, `#253`, `#228` の3個だった。

## 推定原因

1. キックによる高速移動で、旧primary `#228` と新しい移動中ボール `#252` が別trackになっている。
2. `BallTracker.OutputVisibilityThreshold=0.0` なので、観測が消えた旧trackも visibility が0より大きい間は出力対象になる。
3. `BallTracker.TrackLifetimeNs=1000000000` かつ `VisibilityHalfLifeSeconds=1.0` なので、旧trackは約1秒程度残りやすい。
4. camera 0 と camera 1 の移動中ボール位置が `BallMergeDistanceMm` の実効値120mmを超えてずれており、同一ボールとしてmergeされず `cams=1` の別trackとして出ている。
5. `cams=1` 側のtrackは高速移動またはcamera間ずれにより、merged identity matchingの120mm gateにも入りにくく、`#253`, `#254`, ... のように毎frame新規id化している。

## 次の修正候補

- primary以外の stale ball は、fresh observation が無ければ出力しない、または secondary 用により高い visibility threshold を設ける。
- kick直後に旧primaryを短時間で抑制する条件を追加する。
- camera間 ball merge の距離閾値を、同時刻・高速移動時だけ広げる。
- camera 0 / camera 1 の観測時刻差や位置差を診断ログに追加し、mergeできない理由を直接見えるようにする。
