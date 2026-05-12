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
- official tracker packet は multicast endpoint から届く前提とし、receiver は設定済み multicast address / port を使って multicast group に参加する。loopback unicast 受信だけでは CaptureOn 比較ログの runtime 正常系証跡として扱わない。
- live receiver の endpoint は起動時に解決する。`Tracker:Receive:MulticastAddress` / `Port` を明示した場合は receiver 独自 endpoint を監視し、未指定項目は起動時 active profile と `Tracker:RuntimeOverrides:Publish` から解決した ibis publish endpoint へ fallback する。`Tracker:Receive:InterfaceAddress` は従来通り multicast join に使う local interface 指定であり、endpoint fallback とは独立して扱う。
- runtime profile switch 後に live receiver socket を再構成することは `TRACKER-054` の対象外とする。profile switch 後も receiver は起動時に解決した endpoint を監視し続けるため、運用手順と README では起動時固定であることを明記する。
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

sidecar JSONL record は、後から 3rdparty tracker frame を再生し、ibis frame と再比較できるよう次を保持する。snapshot は表示用データとして扱ってよいが、表示用 snapshot だけでは比較元データとして不十分である。通常経路では raw payload または raw payload を復元できる参照を必ず保持し、writer / reader round-trip で保存済み record から raw payload を復元または再decodeできることを入力契約にする。

- `receivedAt`
- remote endpoint
- `uuid`
- `sourceName`
- source role / label / metadata
- tracked frame number
- tracked frame timestamp
- raw payload base64、または raw payload を session folder 内で復元できる参照情報
- raw由来で作れる ball / robot count、team / robot id、代表位置、track source summary などの比較・一覧表示用 summary
- decode failure、tracked frame 欠落、timestamp 欠落などを示す skipped/error 情報

## source 識別と role分類

`Tracker:Uuid` と `Tracker:SourceName` は保存除外の条件ではなく、後続表示・比較用の source role / label / metadata を付与するために使う。ibis runtime identity と一致する `TrackerWrapperPacket` も tracker packet snapshot sidecar へ保存してよく、ibis 詳細ログや render snapshot との重複保持を仕様として許容する。

どちらかが空、設定と異なる、または他 tracker と衝突する場合も record を破棄しない。remote endpoint と受信経路を併記し、role を `unknown` や `ambiguous` として扱う。self / 3rdparty / unknown の判別は、保存後の表示名、フィルタ、比較対象選択のための metadata であり、保存可否を決める条件ではない。

同じ `uuid` で `sourceName` が異なる場合、または `sourceName` が空の場合も、record を破棄せず source identity の不足として保存する。source ごとの active tracker API と同一 `uuid` 衝突ケースは source summary / role 解決の追跡リスクとして扱う。通常経路では raw payload と source identity を落とさず、衝突時も `unknown` / `ambiguous` として保存できれば比較元データは保持されるため、保存処理の blocker にはしない。

## timestamp 比較

ibis committed frame と tracker packet snapshot は同じ frame number や publish frequency を持つとは限らない。比較は ibis `TrackerFrame.data_timestamp_ns` と snapshot 側 `TrackedFrame.timestamp` の timestamp 近傍で行う。

初期実装では nearest timestamp または latest-before のどちらを採用するかを task 内で固定する。採用した対応規則、許容 window、該当 source identity は出力と sidecar から後で確認できるようにする。

## CaptureOn lifecycle

live receiver のネットワーク受信は明示設定で有効化する。既定設定では常時 bind / receive を始めず、運用者が official tracker multicast receive を有効化した場合だけ receiver を起動する。

`Tracker:Receive:MulticastAddress` / `Port` が未指定なら、live receiver は起動時に解決した ibis publish endpoint と同じ address / port を監視する。3rdparty tracker が別 endpoint へ publish する運用では、`Tracker:Receive` に receiver 独自 endpoint を明示する。`Tracker:Receive:Enabled=false` の場合、external tracker packet は CaptureOn 中でも記録されない。

CaptureOn は receiver 起動条件ではなく、session sidecar 書き込み条件を制御する。Capture Off 中に receiver が packet を受けても snapshot sidecar を作成・追記しない。

Capture Off 中は snapshot sidecar を作成・追記しない。Capture Off / 再On では session folder を更新し、前 session folder の snapshot writer へ追記しない。

CaptureOn 直後、まだ packet capture 本体の session が遅延作成されている場合は、最初に保存対象 packet が来た時点で同一 session folder を確定し、snapshot sidecar をその folder 配下へ関連付ける。

他 tracker が存在しない場合、既存 packet capture、diagnostics log、render snapshot の内容上の挙動は変えない。metadata には snapshot sidecar が未作成、または record 0 件である状態を明示できるようにする。

## diagnostics / replay / playback 互換追加

diagnostics log reader、`Tracker.CaptureReplay`、diagnostics playback は、metadata の relative path から tracker packet snapshot sidecar を解決し、存在する場合だけ追加情報を読む。既存 capture や既存 diagnostics log では session folder または snapshot sidecar 欠落を正常系として扱う。

`Tracker.CaptureReplay` は agent / 自動検証 / CLI 調査向けに、3rdparty tracker snapshot と ibis committed frame の nearest timestamp comparison を `trackerSnapshot` / `trackerComparison` 行として出力する。この CLI 比較実装は diagnostics UI 実装後も削除せず、UI と同じ reader contract の検証経路として維持する。

`/diagnostics` はユーザー向けに同じ comparison を画面上で確認できるようにする。diagnostics playback は選択中の diagnostics entry と playback tick に合わせて tracker snapshot comparison を更新し、source identity / role / label で表示対象を切り替えられる comparison panel を持つ。render snapshot と同じく session folder 内 sidecar への対応付けを行うが、比較の基準 timestamp は render snapshot ではなく、ibis own snapshot の `TrackedFrame.timestamp` とする。

`/diagnostics` の tracker snapshot comparison は、timeline scrubber 移動や playback tick のたびに tracker packet snapshot sidecar JSONL を再読込しない。log 選択時に metadata path、sidecar path、diagnostics log path と各 file の last write time / length を key にした lightweight index を作成または cache し、selected entry の変更時はその index から source options と nearest timestamp comparison を生成する。

100MB は上限ではなく通常の capture で到達しうるサイズとして扱う。100MB 以上の sidecar でも tick / scrub 時に sidecar size へ比例した I/O / JSON parse / protobuf parse を発生させない。初回 log 選択時の index build は既存 JSONL sidecar を活かし、bounded memory cache により同じ file state の再読込を避ける。

通常 Play は diagnostics entry の実 timestamp delta を維持する。調査用の高速再生は Fast Forward と明示 speed selector として表現し、Play の 1x real-time 契約、Stop、末尾到達時の先頭戻り、stale tick guard を維持する。

replay / diagnostics / playback の出力または UI 表示は、少なくとも次を確認できるようにする。

- ibis committed frame の timestamp
- 対応する source identity と role / label
- 採用した timestamp 対応規則
- snapshot 側 tracked frame number / timestamp
- timestamp delta
- ball / robot count
- skipped/error count
- raw payload 参照または復元状態

3rdparty tracker packet は snapshot として保持し、`Tracker.CaptureReplay` と `/diagnostics` の playback は session folder 内の snapshot log を読み、timestamp 近傍規則で ibis committed frame と並べて再生・比較表示できるようにする。playback は raw / tracked render snapshot だけに依存せず、source identity / role ごとの tracker packet snapshot timeline を入力として扱える必要がある。

metadata がない、metadata に snapshot sidecar path がない、sidecar file がない、metadata `TrackerSnapshotLog.IsCreated=false`、record count 0、読み取り error はそれぞれ UI 上の status として区別する。これらは既存 diagnostics log / render snapshot 表示を壊す blocker ではない。

## diagnostics Field source 切替

`TRACKER-056` では、`/diagnostics` の下部 Field 表示を左右それぞれ独立した source selector で切り替えられるようにする。既定は現在の表示を維持し、左 Field は `Vision Input`、右 Field は ibis tracker output とする。ここでの ibis tracker output は既存 render snapshot sidecar から作る `TrackedVisionViewState` を優先して使い、tracker packet snapshot sidecar の `own` record が存在しない capture でも現行の右 Field 表示を維持する。

Field source selector は `Tracker Comparison` panel 内ではなく、左右 Field の見出し行に置く。`Tracker Comparison` panel は header の toggle button で折り畳めるようにし、折り畳み中も左右 selector と Field 描画は使える状態を維持する。comparison panel の折り畳み状態と左右 Field source 選択状態は `Diagnostics.razor.cs` の page state として保持し、query string、session storage、local storage には保存しない。log file 変更時は左 `Vision Input`、右 ibis tracker output に戻し、timeline scrubber / playback tick では選択状態を維持する。reload 時は page state を保持してよいが、選択した source option が新しい view-state に存在しない場合は既定へ fallback する。

Field source の選択肢は次を使う。

- `Vision Input`: 選択中 diagnostics entry に対応する render snapshot frame の `SourceDetections` を既存 mapper で描画する virtual source。
- `ibis tracker`: 選択中 diagnostics entry に対応する render snapshot frame を既存 `TrackedVisionViewState.FromSnapshot(...)` で描画する virtual source。source role としては `own` に相当するが、既定表示維持のため sidecar の有無に依存させない。
- `External`: tracker packet snapshot sidecar の source role `external` に一致する snapshot 群から、選択中 diagnostics entry の ibis own timestamp に最も近い snapshot を描画する。
- `Unknown`: source role `unknown` に一致する snapshot 群から nearest snapshot を描画する。
- source label: sidecar に存在する正規化済み source label と完全一致する snapshot 群から nearest snapshot を描画する。

`All` は Field source としては使わない。`All` は複数 source のうちどれを Field に描くかが曖昧で、既存 comparison filter の non-own 優先規則を Field に持ち込むと、Field の既定表示や左右比較の根拠が不明確になるためである。`All` は `Tracker Comparison` panel の数値比較 filter にだけ残す。

tracker source Field data は、`TrackerDiagnosticsComparisonViewStateReader` の cached index から作る UI 非依存 model とする。`Load` は selected diagnostics entry、comparison filter、左右 Field source selection を受け取り、既存 `SelectedEntryComparison` に加えて左右の `TrackerDiagnosticsFieldSourceFrame` を返せるようにする。`TrackerDiagnosticsFieldSourceFrame` は少なくとも次を持つ。

- Field side: left / right
- selected Field source kind: `VisionInput` / `IbisOwn` / `External` / `Unknown` / `SourceLabel`
- status: ready / no diagnostics entry / diagnostics tracked frame missing / render snapshot missing / sidecar unavailable / own baseline snapshot missing / candidate snapshot missing / drawable objects empty / error
- source role / source label
- matching rule: `nearest-timestamp`
- ibis own baseline timestamp ns
- nearest snapshot tracked frame number / timestamp ns / delta ns
- raw payload restored flag
- `TrackerPacketSnapshotSemanticSummary`、または同等の ball / robot position projection

tracker source の nearest selection は、既存 comparison と同じく selected diagnostics entry の tracked frame number から ibis `own` snapshot を引き、その `TrackedFrame.timestamp` を基準 timestamp にして source role / label 別の候補から nearest snapshot を選ぶ。Field と comparison が別々の規則で nearest を選ばないよう、nearest selection は cached index 内の共通処理を使う。

`TRACKER-055` の cache / index 経路を維持するため、scrub / playback tick / Field source selector 変更時に tracker packet snapshot sidecar JSONL 全体を再読込しない。index build は log / metadata / sidecar の path、last write time、length を key にした既存 cache 経路に統合し、Field 用には raw payload 全体ではなく描画に必要な semantic summary または最小 projection だけを index に保持する。通常 writer が作る record では `SemanticSummary` を使い、古い record などで summary がない場合だけ index build 時に payload fallback を行う。

Field 描画はすべて `VisionFieldCanvas` を使う。geometry は選択中 render snapshot の geometry を使い、tracker source sidecar だけから geometry を復元しようとしない。tracker source snapshot の ball / robot は `TrackerPacketSnapshotSemanticSummary` から `SSL_DetectionBall`、yellow / blue 別 `SSL_DetectionRobot` へ変換する mapper を `DiagnosticsFieldViewFactory` に追加する。team が yellow / blue と判定できない robot は Field 上へ無理に描画せず、Field source frame の status / summary で drawable object が欠落し得ることを示す。

missing / empty / error 時の Field は、Field 領域を消さずに空の `VisionFieldCanvas` または同等の empty state を表示し、Field 見出し付近に status を表示する。`Vision Input` / `ibis tracker` で render snapshot がない場合は既存 render snapshot error を優先する。tracker source では metadata missing、sidecar not-created、sidecar missing、sidecar empty、sidecar corrupt、own baseline missing、candidate missing、nearest snapshot の drawable objects empty を区別し、既存 diagnostics log / render snapshot 表示を壊す blocker にはしない。

`TRACKER-057` の overlay は `TRACKER-056` の対象外とする。ただし `TrackerDiagnosticsFieldSourceFrame` は単一 Field source の描画入力として独立させ、後続で複数 frame を同じ `VisionFieldCanvas` 相当の overlay renderer に渡せる最小 model として再利用する。`TRACKER-056` では重ね合わせ、色分け、legend、visibility toggle は実装しない。

focused tests では、少なくとも次を固定する。

- Field source options は `Vision Input`、ibis tracker、`External`、`Unknown`、source label を持ち、Field source には `All` を含めない。
- 既定は左 `Vision Input`、右 ibis tracker output で、log 変更時に既定へ戻る。
- selected diagnostics entry と source label / role から、comparison と同じ nearest timestamp snapshot の semantic summary が Field source frame に返る。
- source selector 変更、timeline scrub、playback tick で sidecar 全体再読込に戻らず、`TRACKER-055` の index cache を使う。
- missing / empty / corrupt / own baseline missing / candidate missing / drawable objects empty の status が Field 表示用 model に残る。
- `DiagnosticsFieldViewFactory` が semantic summary の ball と yellow / blue robot を `VisionFieldCanvas` 用 DTO に変換する。

## 後続タスクへの固定事項

- `TRACKER-047` では、既存 `TrackerSnapshotReplayReader` / `TrackerReplayIntegrationTddTests` の review gate を閉じる。focused 4 passed、関連 focused 39 passed、full `Tracker.Tests` 191 passed の実装検証済み状態を保持し、gpt-5.5 high review で blocking finding がないことを確認する。finding が出た場合は修正・再検証・r2 review まで完了する。
- `TRACKER-048` では、diagnostics / replay / playback の比較表示・出力へ接続する。metadata relative path から snapshot sidecar を読み、source role / label、tracked timestamp、ball / robot count、raw payload restored、nearest timestamp summary を `Tracker.CaptureReplay` または diagnostics playback で確認可能にする。既存 capture / diagnostics / render snapshot 表示を壊さない。
- `TRACKER-049` では、diagnostics comparison の design / tracking を再同期する。CLI 比較実装は保持し、`/diagnostics` UI comparison を PR ready 前の固定タスクに入れ、後続タスクの dependencies と exit criteria を明確にする。
- `TRACKER-050` では、diagnostics comparison reader / view-state contract を追加する。diagnostics log path から metadata / sidecar を解決し、source list、selected source filter、selected entry comparison、sidecar status、skipped/error count を pure model として固定する。
- `TRACKER-051` では、`/diagnostics` UI へ comparison 表示と source filtering を接続する。selected log / selected entry / playback tick と comparison view-state を同期し、既存 render snapshot、settings modal、timeline、playback controls、resize layout を壊さない。
- `TRACKER-052` では、CaptureOn 比較ログの運用ドキュメントと manual evidence を UI 比較完了後の実態へ更新する。CLI は agent / 検証用、通常確認は `/diagnostics` の comparison panel を主経路として説明する。
- `TRACKER-054` では、live tracker receiver の endpoint override を追加する。既定は起動時 resolved ibis publish endpoint を監視し、`Tracker:Receive:MulticastAddress` / `Port` 指定時は receiver 独自 endpoint を監視する。runtime profile switch 後の receiver socket 再構成は対象外とし、起動時固定として README と設計に明記する。
- `TRACKER-055` では、diagnostics playback / scrubber の低速問題を解消する。scrub / playback tick は lightweight index cache から comparison を更新し、sidecar size に比例する再読込に戻さない。
- `TRACKER-056` では、`Tracker Comparison` panel を折り畳み可能にし、左右 Field の source を `Vision Input`、ibis tracker、external、unknown、source label から選べるようにする。既定は左 `Vision Input`、右 ibis tracker output とし、tracker source は selected diagnostics entry に対する nearest timestamp snapshot を Field に描画する。`All` は Field source として使わない。
- `TRACKER-057` では、Field 重ね合わせ表示を追加する want タスクとして、`TRACKER-056` の `TrackerDiagnosticsFieldSourceFrame` を再利用する。overlay 実装が複雑化する場合は PR ready 前に defer 判断を report に明記する。
- `TRACKER-053` では、PR #9 ready 化を行う。PR本文を `TRACKER-040` から最終状態まで更新し、final validation、review evidence、risk整理、tracking同期、draft解除判断材料を揃える。
- `TRACKER-058` 以降は、socket abstraction 等の hardening を今回PRへ含める判断が明示された場合、またはユーザー承認がある場合だけ追加する。

## 完了条件

- CaptureOn 中に見えている tracker packet を self 除外なしで sidecar JSONL に保存できる。
- live receiver は起動時 resolved ibis publish endpoint を既定で監視し、`Tracker:Receive:MulticastAddress` / `Port` 指定時は receiver 独自 endpoint を監視できる。
- 同一 CaptureOn session で生成される packet capture、metadata、tracker diagnostics、render snapshots、tracker packet snapshot sidecar JSONL が一つの session folder 配下にまとまり、異なる CaptureOn タイミングのログは別 folder に分かれる。
- metadata から session folder と各 file relative path を辿れる。
- Capture Off / 再On で session folder と snapshot writer が切り替わり、前 session folder へ追記しない。
- 他 tracker が存在しない場合でも既存 packet capture、diagnostics log、render snapshot の挙動が変わらない。
- 既存 diagnostics log reader 互換性を壊さず、snapshot sidecar がある場合だけ追加比較情報を読める。
- 3rdparty tracker snapshot を `Tracker.CaptureReplay` の CLI 出力と `/diagnostics` の comparison panel / playback から再生・比較表示できる。
- `/diagnostics` の左右 Field で `Vision Input`、ibis tracker、external、unknown、source label を選択でき、既定は左 `Vision Input`、右 ibis tracker output のまま維持される。
- `Tracker Comparison` panel を折り畳んでも Field source selector と Field 描画を使える。
- scrub / playback tick / Field source selector 変更で tracker packet snapshot sidecar JSONL 全体を再読込しない。
- 各小タスクで TDD、review、commit、PR gate が閉じている。
