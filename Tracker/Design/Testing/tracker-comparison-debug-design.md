# トラッカー比較デバッグ 設計

## 目的

Aspire のシミュレーション試験環境で、同じ SSL-Vision 入力を Duck、TIGERs、ER-Force のトラッカーへ同時に与え、出力差を `Tracker.DebugHost` で確認できるようにする。

比較は単なる起動確認ではなく、どの入力時点に対する差か、どの表示元同士を比較したか、物体ごとの差がどこで発生したかを追跡できるデバッグ経路とする。

初期対象は次の三つのトラッカーとする。

- Duck `Tracker.RuntimeHost`。
- TIGERs Sumatra の world predictor / tracker。
- ER-Force AutoRef に含まれる tracker source。

raw vision は共通の比較入力として扱うが、シミュレータの真値とは扱わない。

## 既存機能の再利用

`Tracker.DebugHost` には、official `TrackerWrapperPacket` の外部受信、UUID を優先した表示元の識別、Split / Overlay 表示、CaptureOn 中の tracker packet snapshot 保存、診断再生時の対応付けが既にある。

したがって Aspire 側で別の比較 UI を新設せず、`Tracker.DebugHost` を比較デバッグの表示・記録面として再利用する。

ライブ表示では同一 UI render tick で各表示元の最新スナップショットを固定する。異なる UDP stream のパケット時刻が厳密に一致することは要求しない。

保存後の比較では diagnostics sample tick を共通の選択時点とし、各トラッカーを同じ選択時点へ独立に対応付ける。選択時点より後の tracker snapshot を比較対象へ使わない。

## Aspire 資源

比較モードでは通常のシミュレーション資源に次を追加する。

| 資源名 | 実行形態 | 役割 |
| --- | --- | --- |
| `tracker-tigers` | Docker image | raw vision `224.5.23.2:10020` を受信し、TIGERs の tracker 出力を `224.5.23.2:11010` へ送信する。 |
| `tracker-erforce` | Docker image | raw vision `224.5.23.2:10020` を受信し、ER-Force の tracker 出力を `224.5.23.2:11010` へ送信する。 |
| `debug-host` | .NET プロセス | raw vision と `224.5.23.2:11010` の全 tracker packet を受信し、ライブ比較と CaptureOn / replay を提供する。 |

Duck `Tracker.RuntimeHost` も `sim` profile で `224.5.23.2:11010` へ出力する。三つの tracker source は同じ official tracker multicast endpoint を共有し、`Tracker.DebugHost` 側で source identity により分離する。

## TIGERs tracker

TIGERs は `tigersmannheim/sumatra` image を使用する。Sumatra の `simulation_protocol` 構成は raw SSL-Vision `224.5.23.2:10020` を受信し、vision tracking data を `224.5.23.2:11010` へ送信する。

比較専用起動では AI を有効化しない。Crane の既存対戦構成にある `--aiBlue` は付けず、world predictor / tracker の出力だけを比較対象にする。

代表起動引数は次の形とする。

```text
--headless
--visionAddress 224.5.23.2:10020
--refereeAddress 224.5.23.1:11003
--moduli simulation_protocol
```

Sumatra image は移動タグをそのまま再現試験の識別子にしない。利用できる固定 version tag がある場合はそれを使い、固定 tag が無い場合は Docker image digest を AppHost 設定と試験証跡へ保存する。

## ER-Force tracker

ER-Force は Crane の開発用 compose で利用実績がある `roboticserlangen/autoref:2025.1.0` を初期 image とする。

代表起動引数は次とする。

```text
--vision-port 10020
--tracker-port 11010
--gc-port 11003
```

新しい ER-Force AutoRef image へ更新する場合は version tag または `commit-<hash>` を固定し、比較結果と image version を同じ証跡に残す。

## DebugHost の起動設定

`debug-host` は `Tracker.DebugHost` をホスト上の .NET project resource として起動する。比較専用 observer とし、DebugHost 自身の tracker は動かさない。

AppHost から最低限、次を上書きする。

- `VisionReceiver:MulticastAddress=224.5.23.2`。
- `VisionReceiver:Port=10020`。
- `Tracker:Enabled=false`。
- `Tracker:Receive:Enabled=true`。
- `Tracker:Receive:MulticastAddress=224.5.23.2`。
- `Tracker:Receive:Port=11010`。
- `Tracker:Uuid=debug-host-observer`。
- `Tracker:SourceName=debug-host-observer`。

`debug-host-observer` を DebugHost 自身の identity とすることで、Duck の `uuid=ibis` を DebugHost 自身と誤って同一視しない。TIGERs / ER-Force は packet の UUID を優先し、UUID が無い場合だけ source name と remote endpoint を補助識別に使う。

source identity が衝突して複数 tracker を一意に分離できない場合は、異なる tracker の物体を一つの source に混ぜず、比較準備未完了として表示する。

## 比較の時刻基準

### ライブ比較

ライブ比較は同一 UI render tick で Duck / TIGERs / ER-Force の latest snapshot を固定する。

各 source について tracker frame timestamp、UDP 受信時刻、render tick の採取時刻を表示し、source 間の timestamp delta と snapshot age を併記する。

時刻差が大きい場合は物体差を隠さず表示するが、「同時刻に近い比較ではない」ことを状態として明示する。閾値は設定値とし、初期実装では値を製品コードへ固定しない。

### CaptureOn / replay

CaptureOn では既存の tracker packet snapshot sidecar に Duck / TIGERs / ER-Force の raw `TrackerWrapperPacket` と source metadata をすべて保存する。

replay では diagnostics sample tick を共通基準にする。各 source は保存済み alignment を優先し、対応が無い場合は同じ source の選択時点以前にある latest-before snapshot を使う。

三つの source を比較する場合でも timeline cursor は一つとし、source ごとに別の時点へ移動させない。

## 数値差分

既存の Split / Overlay に加え、比較用の数値差分を表示する。

比較は一つの基準 source と一つ以上の比較 source で行う。既定の基準 source は Duck とし、次を標準の組合せとする。

- Duck ↔ TIGERs。
- Duck ↔ ER-Force。
- TIGERs ↔ ER-Force は必要に応じて選択可能にする。

### ロボット

ロボットは `(team color, robot id)` を同一物体のキーとして対応付ける。

両 source に存在するロボットについて、official tracker packet に値が存在する項目だけを比較する。

- 位置 X/Y と二次元位置差。
- 速度 X/Y と二次元速度差。
- orientation と最短角度差。
- angular velocity の差。

片方だけに存在するロボットは数値 0 として比較せず、`Missing in <source>` として別の差分種別にする。

### ボール

tracker ごとの ball track id は同一性を保証しないため、異なる tracker 間の対応付けキーには使わない。

両 source が単一ボールだけを出している場合はその二つを比較する。

複数ボールが存在する場合は位置距離に基づく一対一対応を行い、対応しないボールは unmatched として表示する。対応距離の上限は設定可能にし、上限を越えたボールを無理に同一物体へ対応付けない。

対応したボールについて、存在する項目だけを比較する。

- X/Y/Z 位置と位置差。
- X/Y/Z 速度と速度差。

## 差分 UI

`Tracker.DebugHost` の live Compare と `/diagnostics` に共通の `Tracker Difference` 表示モデルを追加する。

最低限、次を確認できるようにする。

- 基準 source と比較 source。
- 各 source の UUID / source label / frame number。
- 各 source の tracker timestamp / received time / age。
- source 間の timestamp delta。
- ball の matched / unmatched 状態と位置・速度差。
- robot ごとの matched / missing 状態と位置・速度・角度差。
- source ごとの ball / robot count。

フィールド上の Overlay と数値表は同じ固定済み snapshot pair から生成し、描画と数値で別の時点を参照しない。

## デバッグ操作

比較モードでは次の流れを主なデバッグ手順とする。

1. Aspire から Simulator、Crane、Duck、TIGERs tracker、ER-Force tracker、DebugHost を一括起動する。
2. DebugHost の live Compare で Duck / TIGERs / ER-Force が別 source として受信されていることを確認する。
3. Split または Overlay で視覚的なずれを確認する。
4. `Tracker Difference` で同じ snapshot pair の位置、速度、角度、存在差を見る。
5. 再現が必要な場合は CaptureOn を開始する。
6. `/diagnostics` で同じ diagnostics sample tick に各 tracker を合わせ、問題が発生した時点を scrub して比較する。

## 起動失敗と部分起動

TIGERs または ER-Force tracker の一方が起動できなくても Duck と Simulator の通常試験まで巻き込んで停止させない。

ただし比較モードの状態は、要求した tracker source が受信できていない場合に `Ready` としない。Aspire の resource 状態と DebugHost の source 一覧の両方で欠落を確認できるようにする。

## テスト方針

実装は TDD で行う。

AppHost の application model test で次を先に固定する。

- `tracker-tigers`、`tracker-erforce`、`debug-host` が比較モードに存在する。
- TIGERs / ER-Force tracker が host network を使う。
- TIGERs が raw vision 10020 を入力し tracker 11010 を出力する設定である。
- ER-Force が `--vision-port 10020 --tracker-port 11010` で起動する。
- DebugHost の tracker receiver が 11010 で有効になる。
- DebugHost 自身の tracker が無効である。

DebugHost の focused test では次を固定する。

- UUID が異なる Duck / TIGERs / ER-Force を別 source として保持する。
- source identity が不足する場合も異なる endpoint を勝手に一つへ混ぜない。
- robot は team + id で対応付ける。
- ball track id を tracker 間対応付けに使わない。
- missing / unmatched object をゼロ差分として扱わない。
- live の Overlay と数値差分が同じ snapshot pair を使う。
- replay の三 source が同じ selected diagnostics sample tick を基準にし、future snapshot を使わない。

## 実装単位

- `ASPIRE-006A`: TIGERs / ER-Force tracker と DebugHost を Aspire comparison mode に追加する。
- `ASPIRE-006B`: source identity と三 tracker 同時受信の回帰テストを追加する。
- `ASPIRE-006C`: `Tracker Difference` の物体対応付けと数値差分モデルを TDD で追加する。
- `ASPIRE-006D`: live Split / Overlay と数値差分を同じ snapshot pair へ接続する。
- `ASPIRE-006E`: CaptureOn / replay で三 tracker を同じ diagnostics sample tick に揃える比較を確認する。

## 対象外

- TIGERs / ER-Force の tracker algorithm の改変。
- 外部 tracker の出力を Duck tracker の入力へ戻す処理。
- 外部 tracker の track id を Duck の track id へ変換する処理。
- raw vision や Simulator 出力を真値として自動採点する機能。
- 初期段階での統計的な優劣判定や tracker ranking。
- 比較結果の CSV / JSON export。

## 完了条件

- 一回の Aspire 起動で Duck / TIGERs / ER-Force tracker と DebugHost を比較モードとして起動できる。
- 三 tracker が同じ raw vision stream を入力として使用する。
- DebugHost が三 source の official tracker packet を別 source として識別できる。
- live で Split / Overlay と数値差分を確認できる。
- CaptureOn 後に同じ選択時点へ三 source を対応付けて再現できる。
- robot の位置・速度・角度・存在差と、ball の対応・位置・速度・存在差を追跡できる。
- 時刻差が物体差と分離して表示され、古い snapshot を同時刻の結果と誤認しない。
- 比較機能が利用できない場合でも通常の Duck + Crane シミュレーション試験は維持される。
