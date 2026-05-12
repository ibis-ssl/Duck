# Tracker Server / CLI / UI CaptureOn 比較ログ 詳細設計

## 目的

`TRACKER-040` 以降では、CaptureOn 中に見えている official `TrackerWrapperPacket` をすべて保存し、capture 後に ibis 出力、ibis 自身の official packet、3rdparty tracker packet を再生・比較できるようにする。

この文書は CaptureOn 比較ログの Server / CLI / UI 側の機能設計を定める。旧 `TRACKER-034` の巨大ファイル分割やコメント追加などの保守性改善は機能仕様に含めない。保守性改善の履歴と運用設計は `tracker-server-cli-ui-maintainability-design.md` と `tracker-history-000-038.md` を参照する。

## 対象範囲

- `Tracker.Server` の CaptureOn session と比較ログの関連付け
- `TrackerConnectionLib` を使った official tracker packet 傍受
- CaptureOn session folder 配下へ packet capture、metadata、diagnostics、render snapshot、tracker packet snapshot sidecar JSONL をまとめる保存契約
- diagnostics log / replay / playback から snapshot sidecar を参照する互換追加
- `/diagnostics` または `Tracker.CaptureReplay` で後から 3rdparty tracker snapshot を再生・比較表示するための入力契約

対象外:

- `Tracker.Core` の追跡アルゴリズム変更
- ibis tracker の official packet 出力内容の変更
- 既存 packet capture / diagnostics log / render snapshot の破壊的 schema 変更
- 旧保守性改善タスクの巨大ファイル分割、履歴退避、tracking 軽量化

## 責務境界

- `TrackerConnectionLib` を official tracker packet 傍受の第一候補統合点にする。
- `Tracker.Server` へ組み込む際は、既存の `UdpTrackerReceiver` / `MultiTrackerManager` / `TrackerPacketAdapter` の責務を優先して使う。CaptureOn lifecycle と session folder に合わせる必要がある場合だけ薄い adapter を置く。
- `Tracker.Server` は CaptureOn session と tracker packet snapshot log を紐付ける統合層にする。packet capture 本体、metadata、diagnostics sidecar、render snapshot、tracker packet snapshot sidecar を同じ session folder 配下の成果物として扱う。
- `Tracker.Core` には official tracker packet 傍受、snapshot sidecar 保存、ibis / other tracker の後処理比較を入れない。Core は ibis tracker の internal frame と official packet 生成だけを担当する。

## 保存形式

tracker packet snapshot log の主記録は既存 `.tracker-diagnostics.log` の破壊的拡張ではなく、CaptureOn session folder 配下の sidecar JSONL とする。異なる CaptureOn タイミングのログは別 folder へ分け、同じ階層に多数のログファイルを横並びにしない。

session folder は `VisionReceiver:PacketCapture:DirectoryPath` 配下に作成し、folder 名には既存の `<prefix>-<timestamp>-<guid>` basename を使う。folder 内の file 名も同じ basename を含めるか、または `packets.jsonl.gz` のような用途名を使う。どちらの場合も metadata から相対 path で辿れるようにし、既存 basename 同期の考え方は session folder 名または folder 内 file 名で維持する。

session folder には少なくとも次を配置できるようにする。

- packet capture 本体
- session metadata
- tracker diagnostics sidecar
- render snapshots
- tracker packet snapshot sidecar JSONL

metadata には session folder の path と、packet capture 本体、tracker diagnostics、render snapshots、tracker packet snapshot sidecar JSONL などの各 file relative path を記録する。snapshot sidecar が未作成または record 0 件の場合も、その状態を metadata で表現できるようにする。

diagnostics log 側は互換追加に留める。

- 既存 key=value 行を読めることを維持する
- metadata から解決できる snapshot sidecar relative path、source 数、role 別件数、近傍比較 summary などを optional field として追加する
- snapshot sidecar がない既存ログを引き続き読めるようにする

sidecar JSONL record は、後から 3rdparty tracker frame を再生し、ibis frame と再比較できるよう次を保持する。

- `receivedAt`
- remote endpoint
- `uuid`
- `sourceName`
- source role / label / metadata
- tracked frame number
- tracked frame timestamp
- raw payload base64、または raw payload を session folder 内で復元できる参照情報
- ball / robot count、team / robot id、代表位置などの比較・一覧表示用 summary
- decode failure、tracked frame 欠落、timestamp 欠落などを示す skipped/error 情報

## source 識別と role分類

`Tracker:Uuid` と `Tracker:SourceName` は保存除外の条件ではなく、後続表示・比較用の source role / label / metadata を付与するために使う。ibis runtime identity と一致する `TrackerWrapperPacket` も tracker packet snapshot sidecar へ保存してよく、ibis 詳細ログや render snapshot との重複保持を仕様として許容する。

どちらかが空、設定と異なる、または他 tracker と衝突する場合も record を破棄しない。remote endpoint と受信経路を併記し、role を `unknown` や `ambiguous` として扱う。self / 3rdparty / unknown の判別は、保存後の表示名、フィルタ、比較対象選択のための metadata であり、保存可否を決める条件ではない。

同じ `uuid` で `sourceName` が異なる場合、または `sourceName` が空の場合も、record を破棄せず source identity の不足として保存する。

## timestamp 比較

ibis committed frame と tracker packet snapshot は同じ frame number や publish frequency を持つとは限らない。比較は ibis `TrackerFrame.data_timestamp_ns` と snapshot 側 `TrackedFrame.timestamp` の timestamp 近傍で行う。

初期実装では nearest timestamp または latest-before のどちらを採用するかを task 内で固定する。採用した対応規則、許容 window、該当 source identity は出力と sidecar から後で確認できるようにする。

## CaptureOn lifecycle

Capture Off 中は snapshot sidecar を作成・追記しない。Capture Off / 再On では session folder を更新し、前 session folder の snapshot writer へ追記しない。

CaptureOn 直後、まだ packet capture 本体の session が遅延作成されている場合は、最初に保存対象 packet が来た時点で同一 session folder を確定し、snapshot sidecar をその folder 配下へ関連付ける。

他 tracker が存在しない場合、既存 packet capture、diagnostics log、render snapshot の内容上の挙動は変えない。metadata には snapshot sidecar が未作成、または record 0 件である状態を明示できるようにする。

## diagnostics / replay / playback 互換追加

diagnostics log reader、`Tracker.CaptureReplay`、diagnostics playback は、metadata の relative path から tracker packet snapshot sidecar を解決し、存在する場合だけ追加情報を読む。既存 capture や既存 diagnostics log では session folder または snapshot sidecar 欠落を正常系として扱う。

replay / diagnostics / playback の出力は、少なくとも次を確認できるようにする。

- ibis committed frame の timestamp
- 対応する source identity と role / label
- 採用した timestamp 対応規則
- snapshot 側 tracked frame number / timestamp
- ball / robot count
- skipped/error count
- raw payload 参照または復元状態

3rdparty tracker packet は snapshot として保持し、`Tracker.CaptureReplay` と `/diagnostics` の playback は session folder 内の snapshot log を読み、timestamp 近傍規則で ibis committed frame と並べて再生・比較表示できるようにする。playback は raw / tracked render snapshot だけに依存せず、source identity / role ごとの tracker packet snapshot timeline を入力として扱える必要がある。

## 後続タスクへの固定事項

- `TRACKER-041` では、既存の self除外 test / 実装が新方針と矛盾するため、次に test contract を all tracker 保存へ修正する。ibis 自身の official packet も保持し、source role は保存後の metadata として検証する。
- `TRACKER-042` では、CaptureOn session folder と metadata の relative path 契約を test で固定し、tracker packet snapshot sidecar path、source role metadata、snapshot log 設定を追加する。
- `TRACKER-043` では、CaptureOn 中に見えている tracker packet をすべて session folder 配下の sidecar JSONL へ保存する。
- `TRACKER-044` では、diagnostics / replay / playback で metadata relative path から snapshot sidecar を解決し、ibis committed frame と tracker packet snapshot を timestamp 近傍比較・再生できるようにする。
- `TRACKER-045` では、UI / README / 運用証跡を整え、session folder 配下の既存 capture / diagnostics / render snapshot 表示を壊していないことを確認する。

## 完了条件

- CaptureOn 中に見えている tracker packet を self 除外なしで sidecar JSONL に保存できる。
- 同一 CaptureOn session で生成される packet capture、metadata、tracker diagnostics、render snapshots、tracker packet snapshot sidecar JSONL が一つの session folder 配下にまとまり、異なる CaptureOn タイミングのログは別 folder に分かれる。
- metadata から session folder と各 file relative path を辿れる。
- Capture Off / 再On で session folder と snapshot writer が切り替わり、前 session folder へ追記しない。
- 他 tracker が存在しない場合でも既存 packet capture、diagnostics log、render snapshot の挙動が変わらない。
- 既存 diagnostics log reader 互換性を壊さず、snapshot sidecar がある場合だけ追加比較情報を読める。
- 3rdparty tracker snapshot を `Tracker.CaptureReplay` と diagnostics playback から再生・比較表示できる。
- 各小タスクで TDD、review、commit、PR gate が閉じている。
