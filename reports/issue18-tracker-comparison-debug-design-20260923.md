# Issue #18 外部トラッカー比較デバッグ 設計報告

## メタデータ

- リポジトリ: `ibis-ssl/Duck`
- Issue: `#18 Aspire対応`
- PR: `#28 docs: design Aspire simulation test environment`
- ブランチ: `design/issue18-aspire-test-orchestration`
- 設計更新 technical HEAD: `31b61ee59ecae8c0eb185a258c01fb9c21f4b008`
- 実行環境: RDC 接続先 `FA780`
- 作成日時: `2026-09-23T20:37:25+09:00`

## 目的

シミュレーション試験で Duck tracker だけを動かすのではなく、TIGERs と ER-Force の tracker を同じ raw SSL-Vision 入力へ接続し、出力差を `Tracker.DebugHost` でライブおよび保存後にデバッグできる構成を設計する。

今回の変更は設計のみで、AppHost、DebugHost、比較計算、外部 tracker の起動実装は行わない。

## 確認した既存 Duck 機能

`Tracker.DebugHost` は既に official `TrackerWrapperPacket` の受信経路を持ち、`MultiTrackerManager<TrackerPacketAdapter>` と `ExternalTrackerSnapshotStore` で外部 tracker を source identity ごとに保持できる。

live UI は raw vision、自前 tracker、外部 tracker を同じ UI render tick で固定し、Split / Overlay で比較する設計と実装を持つ。

CaptureOn では tracker packet snapshot sidecar と alignment sidecar を保存し、`/diagnostics` は selected diagnostics sample tick に対して保存済み alignment、または選択時点以前の latest-before snapshot を使う。future snapshot は比較候補にしない。

既存比較 UI は source role / label、frame、timestamp delta、ball / robot count、raw payload などを表示するが、物体ごとの位置・速度・角度差を数値化する機能は初期設計に含まれていなかった。

## 比較対象

比較対象を次の三つに固定した。

- Duck: `Tracker.RuntimeHost` の `TrackerWrapperPacket`。
- TIGERs: Sumatra の world predictor / tracker 出力。
- ER-Force: ER-Force AutoRef の tracker source 出力。

## 共通入力と出力 endpoint

三 tracker は同じ raw vision `224.5.23.2:10020` を入力にする。

Duck の `sim` profile、TIGERs Sumatra `simulation_protocol`、ER-Force tracker source の出力は official tracker multicast `224.5.23.2:11010` へ集約する。

`Tracker.DebugHost` は `Tracker:Receive:Enabled=true`、receiver endpoint `224.5.23.2:11010` で全 packet を受信し、UUID を優先して source を分離する。

DebugHost 自身の tracker は無効にし、observer identity を `debug-host-observer` とする。これにより Duck の `uuid=ibis` を DebugHost 自身の packet と誤認しない。

source identity が一意に解決できない場合は、異なる tracker の物体を混ぜず比較準備未完了として扱う。

## TIGERs

TIGERs は `tigersmannheim/sumatra` image を使う。Sumatra の `simulation_protocol` 構成は raw vision `224.5.23.2:10020` を受け、tracking data を `224.5.23.2:11010` へ送ることを確認した。

比較専用では AI を有効化しない。Crane の既存対戦構成にある `--aiBlue` は付けない。

代表起動条件は `--headless --visionAddress 224.5.23.2:10020 --refereeAddress 224.5.23.1:11003 --moduli simulation_protocol` とする。

移動タグだけを再現試験の識別子にせず、固定 tag が無い場合は image digest を試験設定・証跡に残す。

## ER-Force

ER-Force は Crane の既存開発用 compose で使われている `roboticserlangen/autoref:2025.1.0` を初期 image とする。

既存構成に合わせて `--vision-port 10020 --tracker-port 11010 --gc-port 11003` で起動する。

更新時は version tag または `commit-<hash>` を固定して比較結果と一緒に記録する。

## 時刻の扱い

live UI では各 tracker が別 UDP stream で動くため、厳密な同一 packet timestamp は要求しない。

同一 UI render tick で latest snapshot を固定し、各 source の tracker timestamp、received time、snapshot age、source 間 timestamp delta を表示する。

保存後は diagnostics sample tick を唯一の timeline cursor とし、各 source をその時点へ独立に対応付ける。source ごとに timeline cursor をずらさない。

## 物体差分

### Robot

robot は `(team color, robot id)` を対応付けキーにする。

両 source に存在する robot について、packet に値が存在する項目の X/Y 位置、二次元位置差、X/Y 速度、二次元速度差、orientation の最短角度差、angular velocity 差を表示する。

片方だけに存在する robot はゼロ差分にせず missing として表示する。

### Ball

tracker 固有の ball track id は異なる tracker 間の同一性を保証しないため、対応付けキーに使わない。

単一 ball の場合は一対一で比較し、複数 ball の場合は位置距離による一対一対応を行う。距離上限を超える候補は無理に対応させず unmatched とする。

対応済み ball は、存在する X/Y/Z 位置と速度、およびそれぞれの差を表示する。

## UI

既存 Split / Overlay と同じ固定済み snapshot pair から `Tracker Difference` の数値表を生成する。

基準 source の既定は Duck とし、Duck ↔ TIGERs、Duck ↔ ER-Force を標準比較とする。TIGERs ↔ ER-Force も選択可能とする。

最低限、source identity、frame number、tracker timestamp、received time、age、timestamp delta、ball matched/unmatched、robot matched/missing、物体単位の数値差を同じ画面で確認できるようにする。

## Aspire comparison mode

通常の Simulator + Crane + Duck 構成へ次を追加する。

- `tracker-tigers`。
- `tracker-erforce`。
- `debug-host`。

外部 tracker の一方が起動できなくても通常の Duck + Crane 試験は停止させない。ただし要求した comparison source が欠ける場合は comparison mode を Ready としない。

## 実装単位

- `ASPIRE-006A`: TIGERs / ER-Force tracker と DebugHost の AppHost resource。
- `ASPIRE-006B`: 三 tracker の source identity と同時受信。
- `ASPIRE-006C`: robot / ball の対応付けと数値差分モデル。
- `ASPIRE-006D`: live Split / Overlay と `Tracker Difference` の同一 snapshot pair 化。
- `ASPIRE-006E`: CaptureOn / replay の三 source 同一 tick 比較。

各実装は TDD で進める。

## 検証

- `Tracker/Design/Testing/aspire-simulation-test-environment.md` と `tracker-comparison-debug-design.md` に対する CSpell: 2 files / 0 issues。
- `git diff --check`: 成功。
- 全体 `npm run lint:md`: 終了値 1。

全体 Markdown lint は今回の本文違反ではなく、PR 基点の `package.json` が参照する `.agents/skills/review-enforcer/scripts/list-markdown-targets.js` が checkout に存在しないため `MODULE_NOT_FOUND` で停止した。

RDC 接続先には `dotnet`、Docker、Aspire CLI が無いため、実行時構成の検証は今回の設計作業では行っていない。

既存 `.github/workflows/dotnet-test.yml` は失敗時に TRX、標準出力、標準エラー、VSTest diagnostics、binlog、環境情報、ソースアーカイブを artifact に保存するため、今回 workflow は変更していない。

## 対象外

- TIGERs / ER-Force tracker algorithm の変更。
- tracker ranking や優劣判定。
- raw vision / simulator state を自動的な真値として扱うこと。
- CSV / JSON export。
- AppHost / DebugHost の実装。
- merge。

## 次作業

`ASPIRE-002` から通常シミュレーション基盤を TDD で実装し、`ASPIRE-005` まで成立後に `ASPIRE-006A` から比較デバッグを追加する。

本報告は technical HEAD `31b61ee59ecae8c0eb185a258c01fb9c21f4b008` に対する管理記録である。報告・handoff を commit / push 後、PR current HEAD SHA と一致する workflow run だけを最終 CI 証跡として採用する。
