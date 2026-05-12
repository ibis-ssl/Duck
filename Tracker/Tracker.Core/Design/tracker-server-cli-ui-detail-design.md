# Tracker Server / CLI / UI CaptureOn 比較ログ 詳細設計

## 目的

`TRACKER-040` 以降では、CaptureOn 中に ibis tracker と同時に存在する 3rdparty tracker の official `TrackerWrapperPacket` を保存し、capture 後に ibis 出力と比較できるようにする。

この文書は CaptureOn 比較ログの Server / CLI / UI 側の機能設計を定める。旧 `TRACKER-034` の巨大ファイル分割やコメント追加などの保守性改善は機能仕様に含めない。保守性改善の履歴と運用設計は `tracker-server-cli-ui-maintainability-design.md` と `tracker-history-000-038.md` を参照する。

## 対象範囲

- `Tracker.Server` の CaptureOn session と比較ログの関連付け
- `TrackerConnectionLib` を使った 3rdparty tracker packet 傍受
- CaptureOn session と同じ basename を使う比較 sidecar JSONL
- diagnostics log / replay から比較 sidecar を参照する互換追加
- `/diagnostics` または `Tracker.CaptureReplay` で後から比較結果を確認するための入力契約

対象外:

- `Tracker.Core` の追跡アルゴリズム変更
- ibis tracker の official packet 出力内容の変更
- 既存 packet capture / diagnostics log / render snapshot の破壊的 schema 変更
- 旧保守性改善タスクの巨大ファイル分割、履歴退避、tracking 軽量化

## 責務境界

- `TrackerConnectionLib` を 3rdparty tracker 傍受の第一候補統合点にする。
- `Tracker.Server` へ組み込む際は、既存の `UdpTrackerReceiver` / `MultiTrackerManager` / `TrackerPacketAdapter` の責務を優先して使う。CaptureOn lifecycle と session basename に合わせる必要がある場合だけ薄い adapter を置く。
- `Tracker.Server` は CaptureOn session と比較ログを紐付ける統合層にする。packet capture 本体、metadata、diagnostics sidecar、render snapshot、比較 sidecar を同じ session として扱う。
- `Tracker.Core` には 3rdparty tracker 傍受、比較 sidecar 保存、ibis / other tracker の後処理比較を入れない。Core は ibis tracker の internal frame と official packet 生成だけを担当する。

## 保存形式

比較ログの主記録は既存 `.tracker-diagnostics.log` の破壊的拡張ではなく、CaptureOn sidecar JSONL とする。ファイル名は packet capture と同じ basename に、例えば `*.tracker-comparison.jsonl.gz` のような suffix を付ける。

diagnostics log 側は互換追加に留める。

- 既存 key=value 行を読めることを維持する
- 比較 sidecar path、比較対象 source 数、近傍比較 summary などを optional field として追加する
- 比較 sidecar がない既存ログを引き続き読めるようにする

sidecar JSONL record は、後から ibis frame と再比較できるよう次を保持する。

- `receivedAt`
- remote endpoint
- `uuid`
- `sourceName`
- tracked frame number
- tracked frame timestamp
- payload base64 または ball/robot count などの再比較に必要な summary
- decode failure、tracked frame 欠落、timestamp 欠落などを示す skipped/error 情報

## source 識別と self除外

self除外は `Tracker:Uuid` と `Tracker:SourceName` を基準にする。両方が ibis runtime identity と一致する `TrackerWrapperPacket` は比較対象にしない。

どちらかが空、設定と異なる、または他 tracker と衝突する場合は、remote endpoint と受信経路を併記し、区別不能な packet を比較対象として断定しない。複数の 3rdparty tracker が存在する場合は、`uuid` / `sourceName` / remote endpoint の組を source identity として扱う。

同じ `uuid` で `sourceName` が異なる場合、または `sourceName` が空の場合も、record を破棄せず source identity の不足として保存する。

## timestamp 比較

ibis committed frame と 3rdparty tracker frame は同じ frame number や publish frequency を持つとは限らない。比較は ibis `TrackerFrame.data_timestamp_ns` と 3rdparty `TrackedFrame.timestamp` の timestamp 近傍で行う。

初期実装では nearest timestamp または latest-before のどちらを採用するかを task 内で固定する。採用した対応規則、許容 window、該当 source identity は出力と sidecar から後で確認できるようにする。

## CaptureOn lifecycle

Capture Off 中は比較 sidecar を作成・追記しない。Capture Off / 再On では session basename を更新し、前 session の comparison writer へ追記しない。

CaptureOn 直後、まだ packet capture 本体の session が遅延作成されている場合は、最初に保存対象 packet が来た時点で同一 session に比較 sidecar を関連付ける。

他 tracker が存在しない場合、既存 packet capture、diagnostics log、render snapshot の挙動は変えない。metadata には comparison sidecar が未作成、または record 0 件である状態を明示できるようにする。

## diagnostics / replay 互換追加

diagnostics log reader と `Tracker.CaptureReplay` は、比較 sidecar が存在する場合だけ追加情報を読む。既存 capture や既存 diagnostics log では比較 sidecar 欠落を正常系として扱う。

replay / diagnostics の比較出力は、少なくとも次を確認できるようにする。

- ibis committed frame の timestamp
- 対応する 3rdparty source identity
- 採用した timestamp 対応規則
- 3rdparty tracked frame number / timestamp
- ball / robot count
- skipped/error count

## 後続タスクへの固定事項

- `TRACKER-041` では、他 tracker packet 受信・識別と self除外の failing test を先に追加する。
- `TRACKER-042` では、CaptureOn session metadata に比較 sidecar path と比較ログ設定を追加する。
- `TRACKER-043` では、CaptureOn 中の他 tracker packet を sidecar JSONL へ保存する。
- `TRACKER-044` では、diagnostics / replay で ibis committed frame と他 tracker frame を timestamp 近傍比較できるようにする。
- `TRACKER-045` では、UI / README / 運用証跡を整え、既存 capture / diagnostics / render snapshot 表示を壊していないことを確認する。

## 完了条件

- CaptureOn 中に ibis tracker と同時刻近傍の 3rdparty tracker packet を self除外付きで sidecar JSONL に保存できる。
- Capture Off / 再On で session と comparison writer が切り替わり、前 session へ追記しない。
- 他 tracker が存在しない場合でも既存 packet capture、diagnostics log、render snapshot の挙動が変わらない。
- 既存 diagnostics log reader 互換性を壊さず、比較 sidecar がある場合だけ追加比較情報を読める。
- 各小タスクで TDD、review、commit、PR gate が閉じている。
