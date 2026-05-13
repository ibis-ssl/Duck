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
- tracker snapshot alignment sidecar JSONL

metadata には session folder の path と、packet capture 本体、tracker diagnostics、render snapshots、tracker packet snapshot sidecar JSONL、tracker snapshot alignment sidecar JSONL などの各 file relative path を記録する。snapshot sidecar や alignment sidecar が未作成または record 0 件の場合も、その状態を metadata で表現できるようにする。

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

tracker snapshot alignment sidecar は `tracker-packet-snapshots.jsonl` とは別 file の `tracker-snapshot-alignment.jsonl` とする。snapshot record 自体へ diagnostics entry 対応を埋め込むと、同じ tracker snapshot を複数 diagnostics entry / Field source / aggregate source から参照するときに重複と後方互換の分岐が増え、snapshot sidecar 破損時と alignment 破損時を分けて扱いにくい。別 sidecar にすることで、snapshot sidecar は受信 packet の主記録、alignment sidecar は replay 用 index として責務を分け、alignment が欠落または壊れても raw snapshot 保存の成否を独立に診断できる。

alignment sidecar record は、CaptureOn 中に diagnostics/render snapshot と tracker source snapshot を同一 session timeline で対応付けるため、少なくとも次を保持する。

- diagnostics entry の stable key: diagnostics log line number、tracked frame number、diagnostics entry timestamp、ibis `TrackerFrame.data_timestamp_ns`
- render snapshot 参照: render snapshot frame number、render snapshot record index または session-relative offset
- session-relative time: session start からの diagnostics entry offset、vision packet/render snapshot の `receivedAt` offset、対応に使った capture-time `receivedAt`
- source key: source role、source label、source uuid、remote endpoint、normalized source key
- 選択した tracker snapshot 参照: tracker snapshot record index、tracker snapshot `receivedAt`、tracked frame number、tracked frame timestamp、semantic summary 有無
- matching rule: `saved-session-alignment`、`saved-session-received-at-nearest`、`legacy-nearest-timestamp`、`unsupported-alignment-missing` など
- delta: diagnostics entry / capture-time と tracker snapshot `receivedAt` の差分、必要なら own data timestamp と tracker timestamp の差分
- aggregate 情報: source label / role aggregate で選ばれた場合の代表 source key、tie-break 理由、同一 label / uuid の endpoint 数
- status: ready、source missing、snapshot missing、alignment skipped、alignment corrupt など

alignment sidecar は log open 時に軽く index 化できる形にする。reader は JSONL を 1 回だけ読み、diagnostics entry stable key と Field source key から alignment record へ直接引ける dictionary または sorted array を構築する。scrub / playback tick / Field source selector 変更時に `tracker-packet-snapshots.jsonl` 全体や alignment JSONL 全体を再読込・再探索しない。100MB 超または長時間 capture では、alignment record から tracker snapshot record index と source key を引き、必要な semantic summary だけを既存 snapshot index から参照する。

## source 識別と role分類

`Tracker:Uuid` と `Tracker:SourceName` は保存除外の条件ではなく、後続表示・比較用の source role / label / metadata を付与するために使う。ibis runtime identity と一致する `TrackerWrapperPacket` も tracker packet snapshot sidecar へ保存してよく、ibis 詳細ログや render snapshot との重複保持を仕様として許容する。

どちらかが空、設定と異なる、または他 tracker と衝突する場合も record を破棄しない。remote endpoint と受信経路を併記し、role を `unknown` や `ambiguous` として扱う。self / 3rdparty / unknown の判別は、保存後の表示名、フィルタ、比較対象選択のための metadata であり、保存可否を決める条件ではない。

同じ `uuid` で `sourceName` が異なる場合、または `sourceName` が空の場合も、record を破棄せず source identity の不足として保存する。source ごとの active tracker API と同一 `uuid` 衝突ケースは source summary / role 解決の追跡リスクとして扱う。通常経路では raw payload と source identity を落とさず、衝突時も `unknown` / `ambiguous` として保存できれば比較元データは保持されるため、保存処理の blocker にはしない。

ER-FORCE のように同じ source label / uuid が複数 remote endpoint から届く場合、保存上の source key は `sourceRole + sourceLabel + sourceUuid + remoteEndpoint` で endpoint 単位に分ける。UI の `External` や source label 選択は aggregate source として扱い、aggregate は endpoint 別 key の候補から diagnostics entry の session-relative `receivedAt` に最も近い snapshot を代表に選ぶ。tie-break は、絶対 delta が小さい候補、同 delta なら同じ tracked frame timestamp のうち record index が小さい候補、さらに同値なら remote endpoint 文字列の ordinal 順とし、alignment record に選択理由を残す。endpoint 別の詳細が必要な場合は source option に remote endpoint を含めた表示名を追加できるが、通常の Field source label では aggregate 代表を描画する。

## timestamp 比較

ibis committed frame と tracker packet snapshot は同じ frame number や publish frequency を持つとは限らない。比較は ibis `TrackerFrame.data_timestamp_ns` と snapshot 側 `TrackedFrame.timestamp` の timestamp 近傍で行う。

初期実装では nearest timestamp または latest-before のどちらを採用するかを task 内で固定する。採用した対応規則、許容 window、該当 source identity は出力と sidecar から後で確認できるようにする。

`TrackedFrame.timestamp` は tracker 実装ごとの時刻系であり、ibis own と 3rdparty が同じ epoch / monotonic clock を使うとは限らない。新規 capture の diagnostics replay / Field source 表示は、保存済み alignment sidecar がある場合はこれを優先し、外部 tracker の `TrackedFrame.timestamp` ではなく CaptureOn 中に観測した `receivedAt`、session-relative time、diagnostics entry time を使って tracker source snapshot を対応付ける。ibis own と外部 tracker の timestamp range が明らかに非重複の場合でも、保存時 alignment があれば `saved-session-alignment` として replay / scrub / playback の Field 表示を成立させる。

保存済み alignment sidecar がない capture では、`/diagnostics` は外部 tracker Field source の正確な時刻対応を保証しない。既存 timestamp nearest を使う場合は `legacy-nearest-timestamp` / best-effort と表示し、timestamp range 非重複を検出した場合は `unsupported-alignment-missing` として、既存ログの欠落を正常な既存互換状態として扱う。既存ログ救済のために読み込み時 fallback を主経路へ昇格しない。

## CaptureOn lifecycle

live receiver のネットワーク受信は明示設定で有効化する。既定設定では常時 bind / receive を始めず、運用者が official tracker multicast receive を有効化した場合だけ receiver を起動する。

`Tracker:Receive:MulticastAddress` / `Port` が未指定なら、live receiver は起動時に解決した ibis publish endpoint と同じ address / port を監視する。3rdparty tracker が別 endpoint へ publish する運用では、`Tracker:Receive` に receiver 独自 endpoint を明示する。`Tracker:Receive:Enabled=false` の場合、external tracker packet は CaptureOn 中でも記録されない。

CaptureOn は receiver 起動条件ではなく、session sidecar 書き込み条件を制御する。Capture Off 中に receiver が packet を受けても snapshot sidecar を作成・追記しない。

Capture Off 中は snapshot sidecar を作成・追記しない。Capture Off / 再On では session folder を更新し、前 session folder の snapshot writer へ追記しない。

CaptureOn 直後、まだ packet capture 本体の session が遅延作成されている場合は、最初に保存対象 packet が来た時点で同一 session folder を確定し、snapshot sidecar をその folder 配下へ関連付ける。

他 tracker が存在しない場合、既存 packet capture、diagnostics log、render snapshot の内容上の挙動は変えない。metadata には snapshot sidecar が未作成、または record 0 件である状態を明示できるようにする。

## diagnostics / replay / playback 互換追加

diagnostics log reader、`Tracker.CaptureReplay`、diagnostics playback は、metadata の relative path から tracker packet snapshot sidecar を解決し、存在する場合だけ追加情報を読む。既存 capture や既存 diagnostics log では session folder または snapshot sidecar 欠落を正常系として扱う。

`Tracker.CaptureReplay` は agent / 自動検証 / CLI 調査向けに、3rdparty tracker snapshot と ibis committed frame の保存時 alignment comparison、または既存 capture の明示的な best-effort comparison を `trackerSnapshot` / `trackerComparison` 行として出力する。この CLI 比較実装は diagnostics UI 実装後も削除せず、UI と同じ reader contract の検証経路として維持する。

`/diagnostics` はユーザー向けに同じ comparison を画面上で確認できるようにする。diagnostics playback は選択中の replay timeline tick に合わせて tracker snapshot comparison を更新し、source identity / role / label で表示対象を切り替えられる comparison panel を持つ。新規 capture では保存済み alignment sidecar を基準にし、legacy best-effort の場合だけ render snapshot ではなく ibis own snapshot の `TrackedFrame.timestamp` を比較基準 timestamp とする。

新規 capture の `/diagnostics` の tracker snapshot comparison と Field source 表示は、metadata から解決した `tracker-snapshot-alignment.jsonl` を優先する。alignment sidecar が ready の場合、selected replay timeline tick と Field source key から保存済み tracker snapshot record index を引き、matching rule を `saved-session-alignment` として表示する。alignment sidecar がない、metadata に path がない、または壊れている capture では、既存 diagnostics log / render snapshot 表示を壊さず、external/source label の Field source は `unsupported-alignment-missing` または明示的な `legacy-nearest-timestamp` best-effort として扱う。

`/diagnostics` の tracker snapshot comparison は、timeline scrubber 移動や playback tick のたびに tracker packet snapshot sidecar JSONL や alignment sidecar JSONL を再読込しない。log 選択時に metadata path、sidecar path、alignment path、diagnostics log path と各 file の last write time / length を key にした lightweight index を作成または cache し、selected entry / selected replay timeline tick の変更時はその index から source options、保存済み alignment、または明示された best-effort comparison を生成する。

100MB は上限ではなく通常の capture で到達しうるサイズとして扱う。100MB 以上の sidecar でも tick / scrub 時に sidecar size へ比例した I/O / JSON parse / protobuf parse を発生させない。初回 log 選択時の index build は既存 JSONL sidecar を活かし、bounded memory cache により同じ file state の再読込を避ける。

`TRACKER-059` 以降の `/diagnostics` Play / Fast Forward / scrub は、diagnostics entry count ではなく unified replay timeline を選択軸にする。unified replay timeline は capture-time `ReceivedAt` を時刻軸とし、diagnostics entry / render snapshot / tracker packet snapshot の union、または同等に fastest available source cadence を含む index とする。3rdparty tracker の `TrackedFrame.timestamp` は ibis own と時刻系が違う場合があるため、unified replay timeline の時刻軸には使わない。`TrackedFrame.timestamp` は source 内の表示・比較値として保持し、capture-time ordering は `ReceivedAt` / session-relative received offset で固定する。

通常 Play は全 unified replay timeline tick を順番に描画しない。Play 開始時に、開始 wall-clock と現在選択中 replay timeline tick の capture-time `ReceivedAt` を基準として保持する。表示更新は30fps相当、つまり約33.3msごとの render interval を目標に行い、各更新時に `targetReceivedAt = startTick.ReceivedAt + (currentWallClock - startWallClock)` を計算する。UI は replay timeline から `ReceivedAt <= targetReceivedAt` を満たす latest tick を選択し、その tick へ直接追従する。高頻度 tracker tick が 100Hz / 200Hz 相当で存在する場合でも、Play 表示は中間 tick を表示スキップしてよく、wall-clock に対する等倍速の再生位置を優先する。

この表示スキップは Play 専用の表示対象 index 選択であり、保存済み alignment v2、unified replay timeline、tracker packet snapshot、comparison 用 data を削らない。timeline scrubber のドラッグ、Field source selector、`Tracker Comparison` panel、`Tracker.CaptureReplay` は引き続き selected replay timeline tick を任意に選べる経路として維持し、ユーザーが気に入っている saved alignment / scrub / Field source / comparison による「確実に比較できる」能力を落とさない。Play が 30fps で描画しなかった中間 tick でも、scrub や comparison では保存済み alignment record から選択・比較できる必要がある。

`TRACKER-062` では、TRACKER-061 の巨大な playback choice button 配置を撤回し、transport 操作は従来どおり Play icon button、Fast Forward icon button、Stop button の配置へ戻す。`等倍速`、`4x`、`16x`、`64x` は transport button ではなく速度選択側の compact segmented/tabs として並べる。select box へ単純に項目追加するより、ユーザーの「選択肢のタブ」という意図と、`等倍速` / 調査用倍率が独立した choices に見える自然さを優先する。ただし tabs は scrubber 行の補助 control として小さく置き、TRACKER-061 のような巨大な action button 群にはしない。数値の等倍ラベルは使わず、表示は必ず `等倍速` とする。

速度 choice と transport の対応は次で固定する。`等倍速` は `DiagnosticsPlaybackMode.Play` と TRACKER-060 の30fps相当 realtime stepping に対応する。`4x` / `16x` / `64x` は `DiagnosticsPlaybackMode.FastForward` と該当 multiplier に対応し、TRACKER-059 の tick 非間引き挙動を維持する。Play button を押した場合は `等倍速` choice を選択して Play を開始する。Fast Forward button を押した場合は選択中の fast multiplier で FastForward を開始し、現在 choice が `等倍速` の場合は既定の fast multiplier に切り替えて開始する。active mode の停止は active Play / Fast Forward affordance が Stop button へ入れ替わる、または同じ位置の Stop button で止める構成とし、速度 tab 自体を Stop action に変えない。Stop、末尾到達時の先頭戻り、mode switch、speed switch の stale tick guard は Play / Fast Forward の両方で維持し、旧 mode または旧 multiplier の queued tick が selection を進めないことを contract にする。saved alignment v2、timeline scrubber、Field source selector、`Tracker Comparison` panel、`Tracker.CaptureReplay` の任意 tick 比較経路は変更しない。

高速 tracker tick では、Vision / render snapshot は tick timestamp に対する latest-before を保持する。先頭だけ prior render snapshot がない場合は nearest-after fallback を許容する。例えば Vision / render snapshot が 0ms / 100ms、ER-FORCE snapshot が 0 / 20 / 40 / 60 / 80 / 100ms の場合、20 / 40 / 60 / 80ms の replay tick は同じ Vision / render 0ms frame を参照し、100ms tick で Vision / render 100ms frame へ進む。これにより replay は高速 tracker cadence に合わせて進み、低速 Vision 側は同じ frame を保持してカクカク見える。

保存時 alignment sidecar は diagnostics line 単位だけでは不足する。新規 capture では、ER-FORCE のような fast tracker source sample 分の alignment record も `tracker-snapshot-alignment.jsonl` に保存し、同じ Vision / render frame を複数の fast tracker records から参照できるようにする。低速 Vision / render tick でも、その時点の latest/current tracker snapshot と対応する alignment record を残す。これにより UI が後から sidecar から推定するだけでなく、保存済み比較点として fastest source cadence の比較根拠を再現できる。

schema 方針は、既存 file 名 `tracker-snapshot-alignment.jsonl` を維持したまま schema version 2 の clean record へ置き換えることを推奨する。別 sidecar は metadata、status、reader、manual evidence の分岐が増えて性能面でも不利なため作らない。互換性は TRACKER-059 の非要件とし、v1 reader fallback、optional-field fallback、旧 positional constructor 維持のための分岐は入れない。v2 record は `replayTimelineIndex`、`replayTimelineReceivedAt`、`replayTimelineKind`、`diagnosticsLineNumber?`、`renderFrameNumber?`、`renderReceivedAt?`、`renderMatchRule`、`sourceKey`、`sourceRole`、`sourceLabel`、`remoteEndpoint`、`trackerSnapshotRecordIndex?`、`trackerSnapshotReceivedAt?`、`receivedAtDeltaTicks`、`status` を明示 field として持つ。reader は log open 時に v2 JSONL を 1 回だけ読み、timeline tick array、source-key index、render latest-before index、tracker source index を構築する。

replay / diagnostics / playback の出力または UI 表示は、少なくとも次を確認できるようにする。

- ibis committed frame の timestamp
- 対応する source identity と role / label
- 採用した timestamp 対応規則
- alignment sidecar の有無、alignment record の status、aggregate/tie-break 理由
- snapshot 側 tracked frame number / timestamp
- timestamp delta
- ball / robot count
- skipped/error count
- raw payload 参照または復元状態

3rdparty tracker packet は snapshot として保持し、`Tracker.CaptureReplay` と `/diagnostics` の playback は session folder 内の snapshot log と alignment sidecar を読み、保存時対応付け規則で ibis committed frame と並べて再生・比較表示できるようにする。playback は raw / tracked render snapshot だけに依存せず、source identity / role ごとの tracker packet snapshot timeline と diagnostics entry alignment を入力として扱える必要がある。

metadata がない、metadata に snapshot sidecar path または alignment sidecar path がない、sidecar file がない、metadata `TrackerSnapshotLog.IsCreated=false`、record count 0、alignment record 0、読み取り error はそれぞれ UI 上の status として区別する。これらは既存 diagnostics log / render snapshot 表示を壊す blocker ではない。

## diagnostics Field source 切替

`TRACKER-056` では、`/diagnostics` の下部 Field 表示を左右それぞれ独立した source selector で切り替えられるようにする。既定は現在の表示を維持し、左 Field は `Vision Input`、右 Field は ibis tracker output とする。ここでの ibis tracker output は既存 render snapshot sidecar から作る `TrackedVisionViewState` を優先して使い、tracker packet snapshot sidecar の `own` record が存在しない capture でも現行の右 Field 表示を維持する。

Field source selector は `Tracker Comparison` panel 内ではなく、左右 Field の見出し行に置く。`Tracker Comparison` panel は header の toggle button で折り畳めるようにし、折り畳み中も左右 selector と Field 描画は使える状態を維持する。comparison panel の折り畳み状態と左右 Field source 選択状態は `Diagnostics.razor.cs` の page state として保持し、query string、session storage、local storage には保存しない。log file 変更時は左 `Vision Input`、右 ibis tracker output に戻し、timeline scrubber / playback tick では選択状態を維持する。reload 時は page state を保持してよいが、選択した source option が新しい view-state に存在しない場合は既定へ fallback する。

Field source の選択肢は次を使う。

- `Vision Input`: 選択中 diagnostics entry に対応する render snapshot frame の `SourceDetections` を既存 mapper で描画する virtual source。
- `ibis tracker`: 選択中 diagnostics entry に対応する render snapshot frame を既存 `TrackedVisionViewState.FromSnapshot(...)` で描画する virtual source。source role としては `own` に相当するが、既定表示維持のため sidecar の有無に依存させない。
- `External`: 保存済み alignment sidecar がある場合は source role `external` の aggregate 代表 snapshot を描画する。alignment がない既存 capture では `unsupported-alignment-missing`、または明示的な best-effort として既存 nearest timestamp を使う。
- `Unknown`: 保存済み alignment sidecar がある場合は source role `unknown` の aggregate 代表 snapshot を描画する。alignment がない既存 capture では `External` と同じ status 方針を使う。
- source label: sidecar に存在する正規化済み source label と完全一致する snapshot 群から、保存済み alignment sidecar の代表 snapshot を描画する。同じ label / uuid が複数 remote endpoint を持つ場合は aggregate tie-break を alignment record に残す。

`All` は Field source としては使わない。`All` は複数 source のうちどれを Field に描くかが曖昧で、既存 comparison filter の non-own 優先規則を Field に持ち込むと、Field の既定表示や左右比較の根拠が不明確になるためである。`All` は `Tracker Comparison` panel の数値比較 filter にだけ残す。

tracker source Field data は、`TrackerDiagnosticsComparisonViewStateReader` の cached index、または TRACKER-059 で置き換える `TrackerDiagnosticsReplayTimelineIndex` 相当の UI 非依存 model から作る。TRACKER-059 以降の `Load` は selected replay timeline tick、comparison filter、左右 Field source selection を受け取り、既存 `SelectedEntryComparison` 相当の比較結果に加えて左右の `TrackerDiagnosticsFieldSourceFrame` を返せるようにする。`TrackerDiagnosticsFieldSourceFrame` は少なくとも次を持つ。

- Field side: left / right
- selected Field source kind: `VisionInput` / `IbisOwn` / `External` / `Unknown` / `SourceLabel`
- status: ready / no diagnostics entry / diagnostics tracked frame missing / render snapshot missing / sidecar unavailable / own baseline snapshot missing / candidate snapshot missing / drawable objects empty / error
- source role / source label
- matching rule: `saved-session-alignment` / `legacy-nearest-timestamp` / `unsupported-alignment-missing`
- ibis own baseline timestamp ns
- diagnostics entry time / session-relative received offset
- nearest snapshot tracked frame number / timestamp ns / delta ns
- alignment source key / aggregate tie-break reason
- raw payload restored flag
- `TrackerPacketSnapshotSemanticSummary`、または同等の ball / robot position projection

tracker source の selection は、新規 capture では selected replay timeline tick の stable key から保存済み alignment record を引き、その record が参照する tracker snapshot を source role / label 別の候補として使う。Field と comparison が別々の規則で snapshot を選ばないよう、alignment lookup と legacy nearest selection は cached index 内の共通処理を使う。alignment がない既存 capture で legacy nearest を許可する場合だけ、held diagnostics entry の tracked frame number から ibis `own` snapshot を引き、その `TrackedFrame.timestamp` を基準 timestamp にして source role / label 別の候補から nearest snapshot を選ぶ。

`TRACKER-055` の cache / index 経路を維持するため、scrub / playback tick / Field source selector 変更時に tracker packet snapshot sidecar JSONL または alignment sidecar JSONL 全体を再読込しない。index build は log / metadata / sidecar / alignment の path、last write time、length を key にした既存 cache 経路に統合し、Field 用には raw payload 全体ではなく描画に必要な semantic summary または最小 projection だけを index に保持する。通常 writer が作る record では `SemanticSummary` を使い、古い record などで summary がない場合だけ index build 時に payload fallback を行う。unified replay timeline も log 選択時の index build で作成し、tick / scrub ごとに alignment sidecar や tracker packet snapshot sidecar を全再読込しない。

ただし TRACKER-059 は性能第一とし、既存 `TrackerDiagnosticsComparisonViewStateReader` / alignment reader / selected diagnostics entry 前提の model が unified replay timeline のボトルネックになる場合は温存しない。必要なら `TrackerDiagnosticsReplayTimelineIndex` 相当の新しい pure index を主経路にし、既存 reader は削除または薄い adapter に縮退させる。実装判断は「log open 時に一度だけ構築し、Play / Fast Forward / scrub / Field source selector 変更では sidecar 再読込なしで bounded lookup できるか」を基準にする。

Field 描画はすべて `VisionFieldCanvas` を使う。geometry は選択中 render snapshot の geometry を使い、tracker source sidecar だけから geometry を復元しようとしない。tracker source snapshot の ball / robot は `TrackerPacketSnapshotSemanticSummary` から `SSL_DetectionBall`、yellow / blue 別 `SSL_DetectionRobot` へ変換する mapper を `DiagnosticsFieldViewFactory` に追加する。team が yellow / blue と判定できない robot は Field 上へ無理に描画せず、Field source frame の status / summary で drawable object が欠落し得ることを示す。

missing / empty / error 時の Field は、Field 領域を消さずに空の `VisionFieldCanvas` または同等の empty state を表示し、Field 見出し付近に status を表示する。`Vision Input` / `ibis tracker` で render snapshot がない場合は既存 render snapshot error を優先する。tracker source では metadata missing、sidecar not-created、sidecar missing、sidecar empty、sidecar corrupt、own baseline missing、candidate missing、nearest snapshot の drawable objects empty を区別し、既存 diagnostics log / render snapshot 表示を壊す blocker にはしない。

`TRACKER-057` の overlay は `TRACKER-056` の対象外とする。ただし `TrackerDiagnosticsFieldSourceFrame` は単一 Field source の描画入力として独立させ、後続で複数 frame を同じ `VisionFieldCanvas` 相当の overlay renderer に渡せる最小 model として再利用する。`TRACKER-056` では重ね合わせ、色分け、legend、visibility toggle は実装しない。

focused tests では、少なくとも次を固定する。

- unified replay timeline は diagnostics entry count ではなく fastest available source cadence を含む。Vision / render snapshots を 0ms / 100ms、ER-FORCE snapshots を 0 / 20 / 40 / 60 / 80 / 100ms にした fixture で、timeline が 20 / 40 / 60 / 80ms tick を含むことを固定する。
- 保存時 alignment sidecar は diagnostics log line 2 件に退化せず、fast tracker sample 数以上の v2 alignment records を持つ。20 / 40 / 60 / 80ms record は同じ Vision / render 0ms frame を参照し、100ms record は Vision / render 100ms frame を参照する。
- ER-FORCE の `TrackedFrame.timestamp` を ibis own と非重複の値にしても、timeline ordering と render hold は `ReceivedAt` / session-relative received offset で決まる。
- `/diagnostics` Play / Fast Forward / scrub は unified replay timeline index を使い、fast tracker tick で Vision Input / ibis tracker Field は latest-before render snapshot を保持する。
- 等倍速 `Play` は30fps相当の表示更新で、開始 wall-clock と開始 tick `ReceivedAt` から target capture-time を計算し、その時刻以下の latest replay timeline tick へ追従する。200Hz tick fixture では、開始から1秒後に約30個目の逐次 tick ではなく wall-clock 1秒相当の tick へ進むことを固定する。
- Play が表示スキップした中間 tick でも、timeline scrubber、Field source、comparison は selected replay timeline tick と saved alignment v2 record から任意 tick を選択・比較できることを固定する。
- Fast Forward は Play 専用 realtime stepping に巻き込まず、既存の調査用 capture-time delta / multiplier 挙動を維持する。
- playback controls は Play icon button、Fast Forward icon button、Stop button の従来配置を持ち、`等倍速` / `4x` / `16x` / `64x` を巨大な action button として描画しないことを固定する。
- 速度選択は compact segmented/tabs として `等倍速`、`4x`、`16x`、`64x` を並べ、数値の等倍ラベルを表示しないことを固定する。
- `等倍速` choice は `DiagnosticsPlaybackMode.Play` と TRACKER-060 の30fps相当 realtime stepping に対応し、Fast Forward multiplier を変更しないことを UI state / component contract で固定する。
- `4x` / `16x` / `64x` choice は `DiagnosticsPlaybackMode.FastForward` と該当 multiplier に対応し、TRACKER-059 の tick 非間引き挙動を維持することを固定する。
- Play button は `等倍速` choice を選択して Play を開始し、Fast Forward button は選択中または既定の fast multiplier で FastForward を開始することを固定する。
- active mode は Stop button で停止でき、Stop / mode switch / speed switch 後の queued tick が stale guard で破棄されることを固定する。
- Field source options は `Vision Input`、ibis tracker、`External`、`Unknown`、source label を持ち、Field source には `All` を含めない。
- 既定は左 `Vision Input`、右 ibis tracker output で、log 変更時に既定へ戻る。
- selected replay timeline tick と source label / role から、comparison と同じ alignment または明示的 best-effort snapshot の semantic summary が Field source frame に返る。
- 新規 capture では selected replay timeline tick と source label / role から、保存済み alignment sidecar が参照する snapshot の semantic summary が Field source frame に返る。
- regression test は Red test から追加する。external tracker の `TrackedFrame.timestamp` range が ibis own の `TrackedFrame.timestamp` range と非重複な fixture を作り、nearest data timestamp だけに戻る実装では失敗することを固定する。
- 保存時 alignment regression では、external timestamp が own と非重複でも、capture-time alignment により selected replay timeline tick / held render frame に対応する external snapshot が replay Field に選ばれることを検証する。
- 対応付け結果の時間軸検査として、selected replay timeline tick の `ReceivedAt` と chosen external snapshot の capture-time `ReceivedAt` の差分が許容範囲内であることを assertion に含める。許容範囲は task 実装時に明示し、fixture の packet 間隔より十分小さい値に固定する。
- alignment sidecar がない既存 capture では、external/source label Field source が unsupported または明示的 best-effort status になる。
- source selector 変更、timeline scrub、playback tick で sidecar / alignment 全体再読込に戻らず、`TRACKER-055` の index cache と log 選択時に構築した replay timeline index を使う。
- missing / empty / corrupt / own baseline missing / candidate missing / drawable objects empty の status が Field 表示用 model に残る。
- `DiagnosticsFieldViewFactory` が semantic summary の ball と yellow / blue robot を `VisionFieldCanvas` 用 DTO に変換する。

## diagnostics Field 重ね合わせ表示

`TRACKER-057` では、`TRACKER-056` の左右 Field source selector と `TrackerDiagnosticsFieldSourceFrame` を再利用して、選択中 diagnostics entry に対する 2 source overlay を追加する。want 扱いのため、source selector を別体系に増やす実装や、任意個数 source の多重 overlay はこの PR の最小実装に含めない。

overlay mode の UI は Field 表示領域の見出し行に置く。左右 Field の selector は維持し、表示 mode は `Split` / `Overlay` の segmented control または同等の二択 control として Field 表示領域全体に対して切り替える。`Split` は現行どおり左 Field と右 Field を並べ、`Overlay` は同じ左右 selector の選択結果を `Layer A` / `Layer B` として 1 枚の Field に重ねる。`Tracker Comparison` panel の折り畳み状態とは独立させ、panel 折り畳み中も mode 切替、左右 selector、overlay legend / visibility は使える。

overlay 対象 source は、追加の multi-select ではなく現在の左 Field source と右 Field source の 2 つに限定する。既定は左 `Vision Input`、右 ibis tracker output のため、初期 overlay は vision input と ibis tracker output の重ね合わせになる。`External`、`Unknown`、source label は `TRACKER-056` の `TrackerDiagnosticsFieldSourceFrame` を使い、新規 capture では保存済み alignment、既存 capture では unsupported または明示的 best-effort で解決する。Field source として `All` は引き続き使わない。左右が同じ source の場合は 1 layer として扱い、legend に同一 source であることを表示する。

overlay の色分けは source layer を識別するためのもので、yellow / blue team の意味を置き換えない。最小仕様では、`Layer A` を cyan 系 stroke / label、`Layer B` を magenta 系 stroke / label とし、robot body の yellow / blue fill は維持する。ball は layer 色の ring または stroke で区別する。重なりを読めるように `Layer B` は破線または半透明 stroke を使う。legend は overlay Field の近くに表示し、各 layer の表示名、source role / label、status、alignment delta または best-effort timestamp delta、record count または drawable count を最小限表示する。

visibility は overlay legend 内の layer ごとの checkbox または toggle で制御する。既定は両 layer visible とする。visibility state は `Diagnostics.razor.cs` の page state に保持し、query string、session storage、local storage には保存しない。log file 変更時は両 layer visible に戻し、timeline scrub / playback tick / Field source selector 変更では現在の visibility を維持する。片方を非表示にしても source selection 自体は変えない。

描画 component は、既存 `VisionFieldCanvas` を多 source 入力へ拡張するのではなく、`Tracker.Server` の diagnostics 用 overlay component を追加する。`VisionFieldCanvas` は raw vision / single source Field の汎用 component として維持し、overlay component は `VisionFieldProjection`、`VisionFieldLines`、`VisionRenderOptions`、既存 geometry DTO を再利用する。marker の source layer styling が必要な場合は `VisionBallMarker` / `VisionRobotMarker` に任意 class / stroke option を最小追加するか、overlay component 内で layer marker を直接描く。既存 `VisionFieldCanvas` の single source 表示、zoom / pan、cursor overlay を壊さない範囲に留める。

overlay 用 model は `TRACKER-056` の `TrackerDiagnosticsFieldSourceFrame` を直接再利用し、raw `Vision Input` と ibis tracker output も同じ overlay layer に変換できる小さな view model を `Diagnostics.razor.cs` または専用 factory で作る。tracker source layer は `TrackerPacketSnapshotSemanticSummary` を `DiagnosticsFieldViewFactory` の mapper で ball / yellow robot / blue robot に変換する。render snapshot 由来 layer は既存の raw source detections と `TrackedVisionViewState.FromSnapshot(...)` を使う。nearest selection、own baseline timestamp、candidate missing 等の status 判定は `TrackerDiagnosticsComparisonViewStateReader.LoadFieldSourceFrame(...)` と cached index を使い、overlay 専用に sidecar JSONL を再読込しない。

missing / empty / geometry なし / candidate なしの扱いは `TRACKER-056` と揃える。render snapshot geometry がない場合、overlay Field は geometry なしの empty state とし、tracker source sidecar だけから geometry を復元しない。metadata missing、sidecar not-created、sidecar missing、sidecar empty、sidecar corrupt、own baseline missing、candidate missing、drawable objects empty は layer status として legend に表示し、他の ready layer があればその layer だけ描画する。両 layer が描画不可でも Field 領域は消さず、empty Field と status を表示する。

focused tests では、少なくとも次を固定する。

- overlay mode state は `Split` / `Overlay` を持ち、log file 変更時に `Split` または既定 mode へ戻す。scrub / playback tick では mode と visibility を維持する。
- overlay 対象 source は左右 Field source selector の 2 source であり、overlay 専用 source list や Field source `All` を追加しない。
- overlay layer は `Vision Input`、ibis tracker、`External`、`Unknown`、source label を混在でき、tracker source は `TRACKER-056` と同じ `TrackerDiagnosticsFieldSourceFrame` / alignment lookup / cached index を使う。
- sidecar unavailable、own baseline missing、candidate missing、drawable empty、geometry missing が layer status として残り、ready layer の描画を巻き込んで消さない。
- layer visibility toggle は source selection を変えず、hidden layer を overlay 描画から除外する。
- overlay component または factory が layer A / B の色分け、legend 表示値、semantic summary mapper の ball / yellow / blue 変換を固定する。

実装対象は `TrackerDiagnosticsComparisonUiState`、`TrackerDiagnosticsComparisonViewStateReader`、`Diagnostics.razor` / `.cs` / `.css`、diagnostics Field overlay component、`DiagnosticsFieldViewFactory`、関連 focused tests、必要なら `Tracker.Server/README.md` に限定する。非対象は receiver / snapshot writer / metadata schema / `Tracker.Core` tracking algorithm / `Tracker.CaptureReplay` 出力変更 / 任意個数 source overlay / 永続化設定とする。

## 後続タスクへの固定事項

- `TRACKER-047` では、既存 `TrackerSnapshotReplayReader` / `TrackerReplayIntegrationTddTests` の review gate を閉じる。focused 4 passed、関連 focused 39 passed、full `Tracker.Tests` 191 passed の実装検証済み状態を保持し、gpt-5.5 high review で blocking finding がないことを確認する。finding が出た場合は修正・再検証・r2 review まで完了する。
- `TRACKER-048` では、diagnostics / replay / playback の比較表示・出力へ接続する。metadata relative path から snapshot sidecar を読み、source role / label、tracked timestamp、ball / robot count、raw payload restored、nearest timestamp summary を `Tracker.CaptureReplay` または diagnostics playback で確認可能にする。既存 capture / diagnostics / render snapshot 表示を壊さない。
- `TRACKER-049` では、diagnostics comparison の design / tracking を再同期する。CLI 比較実装は保持し、`/diagnostics` UI comparison を PR ready 前の固定タスクに入れ、後続タスクの dependencies と exit criteria を明確にする。
- `TRACKER-050` では、diagnostics comparison reader / view-state contract を追加する。diagnostics log path から metadata / sidecar を解決し、source list、selected source filter、selected entry comparison、sidecar status、skipped/error count を pure model として固定する。
- `TRACKER-051` では、`/diagnostics` UI へ comparison 表示と source filtering を接続する。selected log / selected entry / playback tick と comparison view-state を同期し、既存 render snapshot、settings modal、timeline、playback controls、resize layout を壊さない。
- `TRACKER-052` では、CaptureOn 比較ログの運用ドキュメントと manual evidence を UI 比較完了後の実態へ更新する。CLI は agent / 検証用、通常確認は `/diagnostics` の comparison panel を主経路として説明する。
- `TRACKER-054` では、live tracker receiver の endpoint override を追加する。既定は起動時 resolved ibis publish endpoint を監視し、`Tracker:Receive:MulticastAddress` / `Port` 指定時は receiver 独自 endpoint を監視する。runtime profile switch 後の receiver socket 再構成は対象外とし、起動時固定として README と設計に明記する。
- `TRACKER-055` では、diagnostics playback / scrubber の低速問題を解消する。scrub / playback tick は lightweight index cache から comparison を更新し、sidecar size に比例する再読込に戻さない。
- `TRACKER-056` では、`Tracker Comparison` panel を折り畳み可能にし、左右 Field の source を `Vision Input`、ibis tracker、external、unknown、source label から選べるようにする。既定は左 `Vision Input`、右 ibis tracker output とし、tracker source は selected diagnostics entry に対する alignment または当時の nearest timestamp snapshot を Field に描画する。`All` は Field source として使わない。
- `TRACKER-057` では、Field 重ね合わせ表示を追加する want タスクとして、`TRACKER-056` の左右 Field source selector と `TrackerDiagnosticsFieldSourceFrame` を再利用する。最小実装は左右 2 source overlay、layer 色分け、legend、layer visibility に限定し、任意個数 source overlay や永続化設定は含めない。overlay 実装が複雑化する場合は PR ready 前に defer 判断を report に明記する。
- `TRACKER-053` では、PR #9 ready 化を行う。PR本文を `TRACKER-040` から最終状態まで更新し、final validation、review evidence、risk整理、tracking同期、draft解除判断材料を揃える。
- `TRACKER-058` では、新規 capture の保存時 alignment sidecar を追加し、external tracker timestamp が ibis own と非同一時刻系でも Field source / CLI comparison が saved alignment を優先できるようにする。
- `TRACKER-059` では、diagnostics replay timeline を fastest available source cadence に合わせる。Play / Fast Forward / scrub は diagnostics entry count ではなく unified replay timeline を使い、高速 tracker tick では Vision / render snapshot を latest-before で保持する。保存時 alignment sidecar は既存 file 名のまま schema version 2 の clean record へ置き換え、diagnostics line だけでなく fast tracker sample 分の alignment records を残す。互換 fallback は入れず、log open 時の index 構築と tick/scrub 時の O(1) または bounded lookup を優先する。
- `TRACKER-060` では、等倍速 `Play` だけを30fps相当の表示更新で wall-clock 経過時間へ追従させる。開始 tick の `ReceivedAt` と開始 wall-clock から target capture-time を計算し、その時刻以下の latest replay timeline tick を表示対象にする。saved alignment v2 / scrub / Field source / comparison は任意 tick を比較できる経路として維持し、Fast Forward は既存の調査用 capture-time delta / multiplier 挙動を壊さない。
- `TRACKER-061` では、diagnostics playback UI の `等倍速` と `4x` / `16x` / `64x` を別の playback choices として表現したが、ユーザー意図より action button が大きく変わりすぎたため、`TRACKER-062` で UI 形状を修正する。`TRACKER-062` では Play / Fast Forward / Stop の従来 transport button 配置を戻し、速度選択側の compact tabs に `等倍速`、`4x`、`16x`、`64x` を並べる。`等倍速` は `DiagnosticsPlaybackMode.Play`、各倍率は `DiagnosticsPlaybackMode.FastForward` と該当 multiplier に対応し、saved alignment v2 / scrub / Field source / comparison、TRACKER-060 realtime Play、TRACKER-059 Fast Forward tick 非間引き挙動は壊さない。
- `TRACKER-059` 以降の socket abstraction 等の hardening は今回PRへ含める判断が明示された場合、またはユーザー承認がある場合だけ追加する。

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
- `/diagnostics` の Field overlay mode で左右 Field source selector の 2 source を同一 Field に重ね、source layer ごとの色分け、legend、visibility を確認できる。
- Vision より高速な tracker source がある場合、scrub / playback tick は unified replay timeline の fast tracker ticks を含み、Vision / render snapshot は latest-before frame を保持する。
- 等倍速 `Play` は30fps相当の表示更新で wall-clock 経過時間に対応する latest replay timeline tick へ追従し、高頻度 tick の全件逐次描画で遅れ続けない。
- playback UI は Play / Fast Forward / Stop の従来 transport button 配置を持ち、速度選択側に `等倍速`、`4x`、`16x`、`64x` の compact tabs を表示する。
- 保存時 alignment sidecar は fastest source cadence の comparison records を持ち、複数 fast tracker records が同じ Vision / render frame を参照できる。
- scrub / playback tick / Field source selector 変更で tracker packet snapshot sidecar JSONL や alignment sidecar JSONL 全体を再読込しない。
- 各小タスクで TDD、review、commit、PR gate が閉じている。
