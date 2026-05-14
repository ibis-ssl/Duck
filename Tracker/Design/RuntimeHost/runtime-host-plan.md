# Tracker.RuntimeHost 設計

## 目的

`Tracker.RuntimeHost`[^tracker-runtime-host] は、tracker と将来の AutoRef mode[^autoref-mode] を同一 process[^same-process] で低遅延に実行する本番寄り headless host[^headless-host] とする。Web UI[^web-ui]、diagnostics replay[^diagnostics-replay]、capture viewer[^capture-viewer] は `Tracker.DebugHost`[^tracker-debug-host] へ分離し、描画や logging の負荷が tracker / AutoRef の実時間処理へ影響しないようにする。

## 命名

- `Tracker.RuntimeHost`: tracker operation[^tracker-operation] と将来 AutoRef mode を同一 process で実行する runtime host。
- `Tracker.DebugHost`: 旧 `Tracker.Server` から rename した debug host。Web UI、raw vision viewer[^raw-vision-viewer]、diagnostics、capture / replay、比較表示を担当する。
- `Tracker.Core`: tracker algorithm[^tracker-algorithm]、contract、pure model、runtime host と debug host の共通ロジックを置く。

`Tracker.Executer` / `Tracker.Executor` は採用しない。今回の実行体は tracker 専用の executor ではなく、将来 AutoRef mode も同居する試合時 runtime だからである。

## 責務境界

`Tracker.RuntimeHost` は次を担当する。

- SSL-Vision input の受信または runtime 用 receiver 境界。
- tracker operation loop の実行。
- tracker packet publish。
- 将来 AutoRef mode を同一 process へ入れるための mode 境界。
- 実時間処理の performance を優先する設定と起動経路。

`Tracker.RuntimeHost` は次を担当しない。

- Web UI rendering。
- diagnostics replay UI。
- capture viewer。
- debug 用 comparison panel。
- 旧 logging 形式の互換維持。

`Tracker.DebugHost` は次を担当する。

- Web UI と diagnostics 表示。
- raw vision / tracked / 3rd party tracker の debug visualization。
- capture / replay / comparison。
- RuntimeHost または published tracker output の購読と debug 用 sample 保存。

`Tracker.DebugHost` は tracker operation loop を主実行責務として持たない。debug 用に同一 repository の共通部品を使っても、Web rendering や diagnostics logging が RuntimeHost の処理周期を支配しない構造にする。

## AutoRef 方針

AutoRef 実装は今回の対象外とする。ただし `Tracker.RuntimeHost` は将来 AutoRef mode を同一 process に内包できる名前と責務境界にする。想定 mode は次のように扱う。

- tracker only mode。
- tracker + AutoRef mode。

AutoRef mode は tracker output を process 外通信で再購読する前提にしない。試合時 performance を優先するため、RuntimeHost 内で tracker state と AutoRef logic を同居できる境界を残す。

## RuntimeHost 設定方針

RuntimeHost の実行周期は code 内の magic number にしない。`Tracker.RuntimeHost` scaffold では `RuntimeHost:OperationLoopIntervalMilliseconds` を設定として公開し、RuntimeHost main loop / control loop はこの値を使って周期を決める。0 以下の値は performance tuning の意図を曖昧にするため、既定値 fallback ではなく起動時 validation error とする。

`Tracker.RuntimeHost` の実装で追加する調整値は、実運用で変更する可能性があるものを options / appsettings に出す。protocol 名、sidecar 名、metadata field 名など replay / wire contract として固定すべき値は設定化しない。

## Loop isolation 方針

tracker operation loop は、Web server live display processing と diagnostics logging / replay processing の両方から切り離す。

- tracker operation loop は tracker state update と publish を最優先する。
- DebugHost live display は latest immutable snapshot または published output を読む側に回る。
- diagnostics logging / replay は DebugHost 側の sample loop として扱い、tracker committed frame cadence を保存 cadence として要求しない。
- 旧 render snapshot sidecar 互換は非要件とし、新規 logging / new capture の performance を優先する。

### RUNTIME-HOST-005: Core shared runtime boundary

RUNTIME-HOST-005 では新規 `Tracker.RuntimeHost` project scaffold は作らない。先に `Tracker.DebugHost` が持っていた tracker operation loop を `Tracker.Core` 内の UI 非依存 runtime 境界へ抽出し、将来 `Tracker.RuntimeHost` project からそのまま再利用できる形にする。

`Tracker.Core` の shared runtime boundary は次を担当する。

- `ITrackerEngine.Update` の直列実行。
- profile switch request の pending / in-flight 管理と control-only update drain。
- `TrackerUpdateResult.EmittedEvents` 順の dispatch。
- committed frame ごとの latest snapshot store 更新。
- official `TrackerWrapperPacket` 生成と `ITrackerPacketPublisher` への publish。
- publisher 設定反映、publish 成功/失敗統計、observer 通知。

`Tracker.Core` の shared runtime boundary は次を参照しない。

- `Tracker.DebugHost` namespace / project。
- Blazor / Web UI。
- diagnostics file logging。
- capture writer / reader。
- `VisionPacketCaptureSession`。
- `TrackerRenderSnapshot`。
- `TrackerPacketSnapshotLog`。
- `TrackerSnapshotAlignmentLog`。

DebugHost の `VisionReceiverService` は UDP decode、raw store、capture の後に `Tracker.Core.TrackerCoordinator.ProcessPacket` を呼ぶ adapter として残してよい。DebugHost 固有の diagnostics 設定解決結果は `TrackerResolvedOptions` として残すが、Core loop が受け取る設定 shape は `TrackerRuntimeResolvedOptions` に分離し、Core が DebugHost 型を参照しないようにする。

### RUNTIME-HOST-006: DebugHost live display read-side snapshot boundary

RUNTIME-HOST-006 では DebugHost の live display を render tick ごとの composite read-side snapshot 境界へ寄せる。`Home.razor` は `VisionPacketStore` / `TrackedSnapshotStore` を直接 inject せず、`VisionLiveDisplaySnapshotProvider` から `VisionLiveDisplayRenderSnapshot` を 1 回取得する。この snapshot は同一 tick の raw SSL-Vision snapshot、ibis tracked snapshot、3rd party tracker read-side snapshot、comparison 用 `VisionLiveComparisonRenderSnapshot` を同時に保持する。

`VisionLiveComparisonSnapshotComposer` は store を直接読まない。provider が固定済み raw / tracked / external tracker snapshot を渡し、composer はその値から comparison source option、Layer A/B、details を生成する。これにより Raw / Tracked / Compare の表示は同一 render tick snapshot から派生し、comparison のために raw / tracked store を再読取しない。

3rd party tracker は `MultiTrackerManager<TrackerPacketAdapter>` の mutable state を render path で直接読まない。`ExternalTrackerSnapshotStore` が manager の update event から packet と metadata を clone 済み DTO として保持し、live display provider はその read-side snapshot だけを読む。`TrackerConnectionLibReceiverHostedService` と CaptureOn recorder は従来どおり manager update path に接続し、RUNTIME-HOST-006 では diagnostics sample sidecar や RuntimeHost scaffold へ踏み込まない。

### RUNTIME-HOST-007: DebugHost diagnostics sample sidecar fast path

RUNTIME-HOST-007 では RuntimeHost scaffold へ踏み込まず、DebugHost の CaptureOn session に diagnostics sample sidecar を追加する。`DiagnosticsSampleHostedService` は UI 表示有無に依存しない diagnostics sample loop として `VisionLiveDisplaySnapshotProvider` から latest raw snapshot と latest ibis tracker snapshot を固定し、同じ sample record として `diagnostics-samples.jsonl` へ保存する。sample loop の周期は `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` で設定し、既定値は `100` ms、0 以下は既定値へ戻す。`Home.razor` の refresh tick は live display の描画更新だけを担当し、diagnostics logging cadence を決めない。capture metadata は `DiagnosticsSampleSidecarPath` と `DiagnosticsSampleLog` を持つ。

Diagnostics replay / comparison は diagnostics sample sidecar が存在する session では sample tick を replay timeline の主経路にする。`Vision Input` field source と `ibis tracker` field source は旧 render snapshot sidecar ではなく diagnostics sample record の semantic summary から復元する。旧 render snapshot sidecar だけを持ち diagnostics sample sidecar を持たない session は unsupported / degraded legacy として扱い、高コストな互換 path は復活させない。

### RUNTIME-HOST-009: RuntimeHost normal path

RUNTIME-HOST-009 では `Tracker.RuntimeHost` に headless SSL-Vision receiver と tracker operation loop を実装する。RuntimeHost は `VisionReceiver` section から SSL-Vision multicast address、UDP port、任意の local IPv4 interface address を読み取り、DebugHost の `VisionReceiverService` / raw store / capture writer / diagnostics UI に依存せずに `SSL_WrapperPacket` を受信する。

受信処理は latest packet buffer へ packet と受信時刻を保存する。tracker operation loop はこの buffer を `RuntimeHost:OperationLoopIntervalMilliseconds` に従う周期で読み取り、未処理の latest packet がある場合だけ `TrackerCoordinator.ProcessPacket` へ渡す。実行周期は code 内の固定値にせず、`RuntimeHostOptions` の validation 済み設定値だけから決める。

RuntimeHost は `Tracker` section から tracker enable、source name、uuid、publish UDP 有効化、profile 単位の publish 宛先、engine 設定を解決して `TrackerRuntimeResolvedOptions` を作る。Core 側の `TrackerCoordinator`、`TrackedSnapshotStore`、`ITrackerPacketPublisher` / `UdpTrackerPacketPublisher`、`TrackerPacketGenerator` を DI で組み立て、committed frame ごとに official `TrackerWrapperPacket` を publish し、同じ shared boundary の latest tracker snapshot を更新する。

DebugHost が読む latest tracker snapshot は RuntimeHost から DebugHost project へ直接依存して公開しない。DebugHost 側は official tracker packet publish / receive path、または `Tracker.Core` の shared runtime boundary に沿った read-side snapshot を読む側として成立させる。RUNTIME-HOST-009 では RuntimeHost の正常系を executable contract で固定し、DebugHost UI / diagnostics replay / capture viewer の manual evidence は RUNTIME-HOST-010 に残す。

## 設計資料配置

設計資料は `Tracker/Design/` を canonical root とする。

- `Tracker/Design/Core/`: tracker algorithm / contract / pure logic。
- `Tracker/Design/DebugHost/`: Web UI、diagnostics、raw vision viewer、capture / replay。
- `Tracker/Design/RuntimeHost/`: RuntimeHost、process 分離、将来 AutoRef mode。
- `Tracker/Design/Archive/`: 旧 tracking file の保存先。active tracking ではない。

## 非スコープ

- AutoRef logic の実装。
- Referee program の rule engine 実装。
- 旧 diagnostics logging 形式の完全互換。
- `BreakingChanges` の作成。

## テスト方針

- RuntimeHost が Web UI project を参照しないことを project reference / dependency test で固定する。
- RuntimeHost の tracker operation loop が diagnostics logging / replay API を直接呼ばないことを contract test で固定する。
- DebugHost が tracker output を読む側であり、tracker operation loop を Web rendering tick から駆動しないことを contract test で固定する。
- diagnostics sample tick が tracker committed frame cadence に依存しないことを regression test で固定する。

[^tracker-runtime-host]: Tracker.RuntimeHost: tracker operation と将来 AutoRef mode を同一 process で動かす本番寄り headless 実行体。
[^autoref-mode]: AutoRef mode: referee program 相当の判定処理を tracker と同一 process で動かす将来 mode。今回の実装対象ではない。
[^same-process]: same process: tracker と将来 AutoRef logic を process 外通信なしで同じ OS process 内に置く実行形態。
[^headless-host]: headless host: Web UI を持たず、入出力と実時間処理を主目的に起動する実行体。
[^web-ui]: Web UI: browser で見る debug / diagnostics 画面。RuntimeHost の実時間処理から分離する。
[^diagnostics-replay]: diagnostics replay: 保存済み sample / log を DebugHost 側で再生し、raw / tracker 出力を比較する debug 機能。
[^capture-viewer]: capture viewer: 保存済み capture session の内容を確認する debug 表示機能。
[^tracker-debug-host]: Tracker.DebugHost: 旧 `Tracker.Server` から rename した debug 用 host。Web UI、diagnostics、capture / replay、比較表示を担当する。
[^tracker-operation]: tracker operation: SSL-Vision input から tracker state を更新し、official tracker packet を publish する実時間処理。
[^raw-vision-viewer]: raw vision viewer: SSL-Vision detection / geometry を field 上に表示する DebugHost の viewer。
[^tracker-algorithm]: tracker algorithm: raw detection から balls / robots の tracked state を決定的に生成する Core 側の追跡ロジック。
