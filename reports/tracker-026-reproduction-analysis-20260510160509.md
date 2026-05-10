# TRACKER-026 再現ログ解析レポート

## 対象

- 実行場所: `Tracker/Tracker.Server/bin/Release/net10.0/publish`
- 診断ログ: `Tracker/Tracker.Server/bin/Release/net10.0/publish/tracker-diagnostics-20260510T065940298Z-384d43bfd581429ba745e98a508d56d9.log`
- ユーザー確認事象: Tracked 表示で黄色 11 番が表示される。ボールが多すぎる。

## 結論

黄色 11 番は Tracker が完全に無から生成しているのではなく、SSL-Vision raw detection に一瞬入った近接重複 robot を camera-local track として採用し、その後 stale track としてしばらく出力している。

ボール増加も同様に、raw 側で boundary 付近などに複数 ball が入る瞬間があり、Tracker が短命の secondary ball track をすぐ出力している。

現在の `OutputVisibilityThreshold` 調整だけでは、raw に一度入ったゴーストを「初回から採用しない」ことはできない。Tigers 実装に寄せるなら、次の追加対策が必要。

- robot: 既存 merged robot に近すぎる別 ID の新規 robot を採用しない
- ball: single-frame / 短命 ball を tracked output にすぐ出さない grown-up 判定を入れる

## ログ証跡

### 黄色 11 番

該当ログでは、raw の `rawYellow` に Y1 とほぼ同座標の Y11 が入っている。

- line 249:
  - `Y1:x=-5539.6,y=-4310.4`
  - `Y11:x=-5539.2,y=-4310.4`
  - 距離は約 0.4mm で、同一 robot の誤 ID と見なすべき近接重複。

続く line 252 では raw から Y11 が消えているが、tracked 出力には `Y11:x=-5539.8,y=-4309.3,vis=1` として残る。line 256 以降は `vis=0.953`、`0.908`、`0.865` のように減衰しながら出力され続ける。

このため、現在の問題は次の 2 段階で発生している。

1. raw detection の近接重複 Y11 を新規 track として採用している
2. 採用後の stale track が visibility threshold を下回るまで表示される

### ボール増加

ログには raw 側で複数 ball が入る瞬間がある。

- line 668:
  - `rawBalls=2`
  - `rawBallDetails=[x=-3173.6,y=-4397,z=0,c=1; x=-21,y=-1.2,z=0,c=1]`

Tracked 側では過去に採用した boundary 付近の ball track が残るため、別フレームで `trackedBalls=3`、`4`、`5` のような状態が発生している。

## Tigers との差分

Tigers の `CamFilter.processRobots` には、既に merged 済みの robot と近すぎる別 ID の robot を無視する処理がある。

- 参照: `Tracker/Tracker.Core/Design/Ref/AutoReferee/modules/moduli-vision/src/main/java/edu/tigers/sumatra/vision/CamFilter.java`
- 近接判定: `Geometry.getBotRadius() * 1.5`
- 意味: 同じ位置に別 ID の robot detection が来ても、新しい tracker として増やさない

現在の IbisDuck 側は `TrackerExecutionContracts.CollectCameraRobotObservations` / `AddRobotObservation` で raw robot を `(camera, team, robotId)` ごとにそのまま observations へ追加している。近接する別 ID を拒否する処理はない。

Tigers の ball 側には `BallTracker.grownUpAge = 3` があり、短命 tracker は成長済み扱いにならない。また `BallFilterPreprocessor` や代表 ball 選定の段階で primary に寄せる処理がある。

現在の IbisDuck 側は、raw に一度入った secondary ball track を visibility / lifetime 条件だけで tracked output に出せる。特に sim の `BallTracker.OutputVisibilityThreshold=0` では、短命ゴーストが表示に出やすい。

## 次の修正案

### 1. robot 近接重複 ID の採用抑制

`CollectCameraRobotObservations` で、同一 camera / team 内の raw robot を confidence 降順、robotId 昇順で評価し、既に採用済みの別 ID robot から `RobotRadiusMm * 1.5` 未満なら採用しない。

今回の Y1/Y11 は confidence が同じなので、robotId 昇順により Y1 を採用し Y11 を落とせる。

### 2. ball grown-up 判定

`BallTrackState` に observation count または age を持たせ、Tigers の `grownUpAge=3` に合わせて、primary 以外の新規 ball は連続観測 3 回未満では output に出さない。

主 ball を消さないため、既存 primary / merged primary の扱いは維持し、secondary ghost のみ抑制するのが安全。

## 注意

今回の診断ログは published executable で出力されたものなので、publish 環境でも file diagnostics は動作している。

ただし、現時点の診断ログは問題の発生源を示すためのもの。問題自体を止めるには、上記の robot close-duplicate filter と ball grown-up filter の実装が必要。
