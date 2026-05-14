# ER-Force simulator E2E 差分評価 要件定義案

## 目的

本書は issue #14「dockerでシミュレーターのケースを追加」に対する要件定義案である。ER-Force simulator[^er-force-simulator] を Docker 資産から扱える形にし、SSL Simulation Protocol[^ssl-simulation-protocol] でボール / ロボットの移動を制御し、その観測から ibis tracker と他 tracker の挙動差を測る E2E 検証基盤を段階的に増やすことを目的にする。

最終的には Docker 資産を使った E2E ケースを増やしていく。ただし TRACKER-064 では要件定義だけを行い、Docker / script / test / application code の実装追加は行わない。

## 背景

Duck 側には既に raw SSL-Vision 受信、CaptureOn[^capture-on]、tracker receiver[^tracker-receiver]、snapshot sidecar[^snapshot-sidecar]、alignment sidecar[^alignment-sidecar]、diagnostics replay / comparison、`Tracker.CaptureReplay`[^capture-replay] がある。これらは「同一 session に raw vision、ibis tracker、3rd party tracker、保存時 alignment を残し、後から CLI と UI で確認する」土台として使える。

ER-Force simulator は `SimulatorCommand`[^simulator-command] と `RobotControl`[^robot-control] を UDP で受け、通常の SSL-Vision[^ssl-vision] として `SSL_WrapperPacket`[^ssl-wrapper-packet] を送る。調査結果では、この通常出力は realism config[^realism-config] による観測ノイズ込みの detection data であり、観測ノイズなし真値[^ground-truth] そのものではない。

そのため、本要件案では「観測ノイズ込み SSL-Vision を tracker へ入力すること」と「評価基準として観測ノイズなし真値を得ること」を明確に分ける。観測ノイズなし真値が取れない段階では、ER-Force tracker、Tigers tracker[^tigers-tracker] の順に fallback して差分を見るが、これは真値比較ではなく参照 tracker 差分[^reference-tracker-diff]として扱う。

## 非目的

- grSim[^grsim] は使わない。調査、設計、実装、Docker 移植、E2E normal path の対象外とする。
- TRACKER-064 では実装を追加しない。
- Docker build / 起動を常時 CI の必須条件にしない。
- ER-Force tracker または Tigers tracker の出力を、観測ノイズなし真値と呼ばない。
- 初期段階で全 RoboCup scenario や対戦制御を再現することは目的にしない。
- raw vision / tracker packet の処理を `Tracker.Core` へ広げることは優先しない。raw vision / diagnostics / E2E 入出力の境界は既存方針どおり `Tracker.Server` 側を優先する。

## 提案する全体像

提案する normal path は次の段階に分ける。

1. ER-Force simulator を手元または opt-in Docker で起動し、simulated vision を `224.5.23.2:10020` または localhost `10020` から出す。
2. Duck の `VisionReceiver`[^vision-receiver] と ibis tracker が同じ simulated vision を受ける。
3. 必要に応じて ER-Force tracker または Tigers tracker を同じ simulated vision へ接続し、`TrackerWrapperPacket`[^tracker-wrapper-packet] を external tracker source として Duck の tracker receiver に入れる。
4. CaptureOn で raw vision、ibis tracker output、external tracker output、alignment を同一 session に保存する。
5. 観測ノイズなし真値を取得できる場合は、truth sidecar[^truth-sidecar] または session manifest[^session-manifest] に保存し、比較の最優先基準にする。
6. 観測ノイズなし真値が取れない場合は、ER-Force tracker、Tigers tracker の順に参照 tracker として使い、結果名と指標名では「truth error」ではなく「reference tracker delta」を明示する。
7. `Tracker.CaptureReplay` と diagnostics comparison で、保存済み session の source、timestamp delta、count、metric を確認できるように段階的に拡張する。

この全体像は提案であり、TRACKER-065 以降で Docker image、command path、tracker endpoint、truth output の実取得可否を手元で確認して固定する。

## 真値と参照値の優先順位

評価基準の優先順位は次の提案とする。

1. 最優先: ER-Force simulator 内部または改造で取得できる観測ノイズなし真値。
2. fallback 1: ER-Force tracker output。
3. fallback 2: Tigers tracker output。

最優先の候補は ER-Force framework 内部の `world::SimulatorState`[^world-simulator-state] である。調査結果では `Simulator::sendRealData`[^send-real-data] により内部では生成されているが、既存 `simulator-cli` から外部 file / UDP へ出す標準経路は未確認である。

既存 ER-Force simulator から観測ノイズなし真値を外部取得できない場合は、次の simulator 改造を要件候補に入れる。

- file output 案: `sendRealData` の `world::SimulatorState` を `--truth-output <path>` などで length-delimited protobuf または JSONL として出力する。
- UDP output 案: `sendRealData` の `world::SimulatorState` を専用 port へ protobuf binary として出す。port は vision / tracker と衝突しない設定可能値を優先する。

初期提案では file output を優先する。packet loss がなく、CaptureOn artifact と同じ session folder へ結びつけやすいためである。UDP output は live diagnostics との相性がよいが、ordering、packet loss、timestamp alignment の設計が必要になる。

ER-Force tracker と Tigers tracker は fallback として有用だが、どちらも観測ノイズ込みの `SSL_DetectionFrame`[^ssl-detection-frame] から推定した tracker output である。したがって、fallback mode の結果は「真値からの誤差」ではなく「参照 tracker との差分」として保存・表示する。

## 初期シナリオ

初期シナリオは、手元確認しやすく、TDD で入力と比較規則を固定しやすいものを優先する。

- ボール静止: `TeleportBall`[^teleport-ball] で位置と速度ゼロを与え、観測後に position、velocity、missing frame を確認する。
- ロボット静止: `TeleportRobot`[^teleport-robot] で team / ID / 位置 / orientation / 速度ゼロを与え、position、orientation、count mismatch を確認する。
- ボール直線運動: `TeleportBall` に初期位置と速度を与え、position error、velocity error、latency / timestamp delta を確認する。
- ロボット直線運動: `TeleportRobot` で初期配置し、`RobotControl` の global / local velocity で移動させ、position、velocity、orientation の差分を見る。
- ロボット ID 近接: 同一 team の複数 robot を近接配置または交差させ、ID switch[^id-switch]、missing frame、count mismatch を見る。

初期値、速度、距離、許容閾値は未確定である。TRACKER-065 以降で simulator の実出力周期、tracker 出力周期、ノイズ設定を確認してから固定する。

## E2E 観測・保存・比較の要求

E2E session は、少なくとも次の情報を同じ session 単位で結びつけることを要求候補にする。

- scenario id、scenario definition[^scenario-definition]、送信した simulator command。
- simulator endpoint、vision endpoint、tracker receive endpoint。
- realism config と geometry。
- truth source priority と、実際に採用した比較基準。
- CaptureOn artifact path。
- raw vision log、tracker snapshot sidecar、alignment sidecar。
- truth sidecar または参照 tracker source identity。
- `Tracker.CaptureReplay` で再現可能な比較出力。

Duck 側の既存資産は次のように整理する。

- CaptureOn: raw packet、diagnostics log、render snapshot、tracker snapshot、alignment を同一 session folder に保存する基盤として使う。
- diagnostics: 保存済み session の Field source、split / overlay、timestamp delta、sidecar status、raw payload restored を確認する UI として使う。
- tracker receiver: ER-Force tracker / Tigers tracker の `TrackerWrapperPacket` を external tracker source として受ける入口として使う。
- snapshot sidecar: external tracker の raw payload、source role / label、tracked frame、semantic summary を保存する。
- alignment sidecar: selected replay timeline tick[^selected-replay-timeline-tick] と tracker snapshot の対応を保存する。
- CaptureReplay: agent / regression 用の CLI evidence として、session artifact から比較出力を得る入口として使う。

不足している要求候補は、scenario definition、scenario command sender、session manifest、truth sidecar、metric engine、object matching rule、単位 / 角度正規化である。

## 差分指標

metric 候補は次を優先する。

- position error: ball / robot の位置差。単位は最終表示で mm を優先する。
- velocity error: ball / robot の速度差。semantic summary に速度が不足する場合は raw payload decode または summary 拡張を検討する。
- orientation error: robot orientation の wrap-aware angle delta[^wrap-aware-angle-delta]。
- ID switch: 近接または交差 scenario で tracker が team / robot ID を入れ替えた事象。
- missing frame: 比較基準 tick に対象 source が存在しない、または許容 staleness を超えた状態。
- latency / timestamp delta: selected replay timeline tick、source `ReceivedAt`、data timestamp の差。
- count mismatch: ball count、team 別 robot count、expected present robot count との差。

object matching rule は段階的に固定する。初期案では ball は primary ball、robot は team + robot ID を優先する。ID 近接 scenario では、truth robot と tracker robot の対応が入れ替わること自体を ID switch として扱う。

## Spec driven / TDD の進め方

本作業は Spec driven で進め、実装は TDD とする。TRACKER-064 では仕様候補だけを定義し、TRACKER-066 以降で最初に作る test 候補は次とする。

- `E2ESimulatorScenarioCommandBuilderTests`: static / linear scenario から `SimulatorCommand` / `RobotControl` payload、送信先 port `10300` / `10301` / `10302`、単位 m / m/s が期待通りになること。
- `E2ESessionManifestTests`: scenario id、simulator endpoint、vision endpoint、tracker endpoint、truth source priority、artifact relative path を manifest が保持すること。
- `E2EComparisonMetricTests`: in-memory frames から position error、orientation error、count mismatch、missing frame、latency delta を算出すること。
- `CaptureReplayE2EComparisonOutputTests`: metadata、snapshot sidecar、truth sidecar または参照 tracker source から `Tracker.CaptureReplay` が `e2eComparison` 行を出すこと。
- `TrackerPacketSemanticSummaryTests`: velocity error を semantic summary で扱う方針にする場合、ball / robot velocity が summary に含まれること。

Docker / multicast / 外部 process を必要とする確認は、初期段階では manual evidence または opt-in automation[^opt-in-automation] に留める。通常 unit test は scenario parser、command builder、manifest writer、metric model、CaptureReplay formatter のように file / in-memory data で閉じるものを優先する。

## Docker / 手元確認方針

初期検証は手元確認を優先し、常時 CI 必須にはしない。

Docker 資産は `/home/ibis/ibis_ws/src/crane/docker` と ER-Force framework の `data/docker/Dockerfile.simulatorcli` を参考にする。ただし grSim 関連 service / config / script は採用対象外とする。

手元確認の候補は次とする。

- `docker compose config` による静的確認。
- `simulator-cli --localhost` または host multicast で simulated vision `10020` を出し、Duck raw viewer の packet count が増えること。
- CaptureOn 中に `TeleportBall` / `TeleportRobot` を送り、session artifact が作られること。
- external tracker endpoint から snapshot sidecar が保存され、source role / label と raw payload restored を確認できること。
- `Tracker.CaptureReplay` で session と metadata を読み、comparison / E2E 行を出せること。

CI に入れる場合も、最初は Docker なしの unit / file-based tests を優先し、Docker E2E は明示 opt-in の smoke とする。

## 段階的ロードマップ

提案する段階は次の通りである。

1. TRACKER-064: 本要件定義書で目的、非目的、真値優先順位、初期シナリオ、Spec driven / TDD 方針を固定する。
2. TRACKER-065: ER-Force Docker / simulator protocol / 参照 tracker の実資産を調査し、Duck repo へ移植する最小構成、移植対象外、truth 取得可否、fallback endpoint を固定する。
3. TRACKER-066: TDD で Docker / compose / config / script / README / 比較補助を追加し、最小 normal path を作る。
4. TRACKER-067: final validation、gpt-5.5 high review、tracking 同期、commit / PR ready 化を行う。

この固定一覧の外に新しい TRACKER 番号を追加する場合は、先に既存一覧で扱えるかを監査し、ユーザー確認を必要とする。

## 未確定事項 / ユーザー確認事項

- ER-Force simulator を改造して `world::SimulatorState` を出すことを許容するか。
- truth output は file output を先にするか、UDP output を先にするか。
- truth sidecar の schema は `world::SimulatorState` protobuf をそのまま使うか、Duck 側で比較用 DTO / JSONL に正規化するか。
- ER-Force tracker fallback はどの binary / service / endpoint を採用するか。
- Tigers tracker fallback は Docker 資産のどの範囲を採用し、grSim 前提資産をどこまで除外するか。
- simulator / tracker の port は sim 用 `10020` / `11010` / `11003` を優先するか、Duck own tracker publish endpoint と衝突しない別 port を固定するか。
- 初期 scenario の具体的な座標、速度、距離、duration、許容閾値。
- velocity error を raw payload decode で計算するか、snapshot semantic summary を拡張するか。
- Docker E2E smoke をいつ CI opt-in に入れるか。
- GPLv3 の ER-Force framework 資産を Duck repo へコピー / 改変する場合の license 境界。

## 脚注

[^er-force-simulator]: ER-Force simulator: ER-Force framework の headless simulator。SSL Simulation Protocol の制御入力を受け、通常は SSL-Vision 互換の観測 packet を出す。
[^ssl-simulation-protocol]: SSL Simulation Protocol: RoboCup SSL simulator と controller / team software 間で使う protobuf / UDP protocol。`SimulatorCommand` や `RobotControl` を含む。
[^capture-on]: CaptureOn: Duck `Tracker.Server` が raw packet、diagnostics log、render snapshot、tracker snapshot、alignment などを session folder に保存する状態。
[^tracker-receiver]: tracker receiver: official tracker packet を UDP で受け、own / external / unknown tracker source として扱う Duck 側の受信入口。
[^snapshot-sidecar]: snapshot sidecar: CaptureOn session に保存する `tracker-packet-snapshots.jsonl`。tracker packet の source、raw payload、semantic summary を後から読むための補助ファイル。
[^alignment-sidecar]: alignment sidecar: CaptureOn session に保存する `tracker-snapshot-alignment.jsonl`。diagnostics replay timeline と tracker snapshot の対応を復元するための補助ファイル。
[^capture-replay]: Tracker.CaptureReplay: CaptureOn session と metadata を読み、agent / regression 用の CLI evidence を出す Duck 側ツール。
[^simulator-command]: SimulatorCommand: simulator 全体を制御する protobuf message。`TeleportBall`、`TeleportRobot`、simulation speed などを含む。
[^robot-control]: RobotControl: team 別 robot control port に送る protobuf message。wheel velocity、local / global velocity、kick、dribbler を含む。
[^ssl-vision]: SSL-Vision: RoboCup SSL の camera detection / geometry を配信する標準的な vision protocol。
[^ssl-wrapper-packet]: SSL_WrapperPacket: SSL-Vision の detection と geometry を含む wrapper packet。
[^realism-config]: realism config: ER-Force simulator の観測ノイズ、欠落、camera error、command delay などをまとめる設定。
[^ground-truth]: 観測ノイズなし真値: camera detection noise や tracker 推定を通す前の物理状態。本書では ER-Force 内部の `world::SimulatorState` を最有力候補にする。
[^tigers-tracker]: Tigers tracker: TIGERs Mannheim 系の tracker / AutoRef 資産から得る外部 tracker output 候補。
[^reference-tracker-diff]: 参照 tracker 差分: 真値が取れない場合に、別 tracker の推定結果を基準にして差を見る mode。真値誤差とは区別する。
[^grsim]: grSim: RoboCup SSL の別 simulator。本 issue では使わず、調査・設計・実装対象外にする。
[^vision-receiver]: VisionReceiver: Duck `Tracker.Server` が `SSL_WrapperPacket` datagram を受け、raw vision viewer / tracker 入力へつなぐ受信機能。
[^tracker-wrapper-packet]: TrackerWrapperPacket: tracked vision 出力を source uuid / name と `TrackedFrame` 付きで包む packet。
[^truth-sidecar]: truth sidecar: E2E session に保存する真値用補助ファイル。`world::SimulatorState` または比較用 DTO を時系列で保持する候補。
[^session-manifest]: session manifest: scenario、endpoint、truth source、artifact path を同一 E2E session として結びつける metadata。
[^world-simulator-state]: world::SimulatorState: ER-Force framework 内部の物理状態 protobuf。ball / robot の position、velocity、rotation、angular velocity を持つ。
[^send-real-data]: sendRealData: ER-Force simulator 内部で `world::SimulatorState` を emit する signal。標準 `simulator-cli` では外部 file / UDP への接続が未確認。
[^ssl-detection-frame]: SSL_DetectionFrame: camera 単位の detection frame。Realistic realism では位置、角度、欠落などの観測ノイズが入る。
[^teleport-ball]: TeleportBall: `SimulatorCommand` 内の ball 配置 / 速度設定 command。静止や直線運動の初期条件に使う。
[^teleport-robot]: TeleportRobot: `SimulatorCommand` 内の robot 配置 / 速度 / present 設定 command。ID 近接 scenario の初期配置にも使う。
[^id-switch]: ID switch: 同じ物体を追っているはずの tracker output で team / robot ID が入れ替わる事象。
[^scenario-definition]: scenario definition: ball / robot の初期状態、移動 command、duration、期待 source、閾値をまとめた E2E 入力仕様。
[^selected-replay-timeline-tick]: selected replay timeline tick: diagnostics replay で現在選択している比較基準 tick。source ごとに cursor をずらさず、この tick を基準にする。
[^wrap-aware-angle-delta]: wrap-aware angle delta: `-pi` と `pi` の境界をまたぐ角度差を最短差として扱う計算。
[^opt-in-automation]: opt-in automation: Docker、multicast、外部 process を使うため通常 unit test には含めず、明示実行時だけ走らせる検証。
