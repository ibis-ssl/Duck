# Sub-agent実行レポート

## タスク

- 目的: Duck 側で ER-Force simulator 入力、ibis tracker 出力、他 tracker 出力、参照真値を E2E 差分評価へ接続する既存機能と不足点を調査する。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: ユーザー指定により調査を分担し、gpt-5.5 high の sub-agent が証跡を日本語 report に残すため。

## 対象範囲

- 対象: `/home/ibis/ssl/IbisDuck` の Tracker.Server diagnostics / CaptureOn / tracker receiver / snapshot sidecar / comparison / README / Design、および E2E 差分評価の最小テスト項目。

## 対象外

- 対象外: crane 側 Docker 資産の詳細整理、ER-Force simulator protocol の真値取得詳細、Duck repo への実装追加。

## 実行コマンド

- 実行コマンド:
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
- `rg -n "TRACKER-064|E2E|comparison|CaptureOn|CaptureReplay" /home/ibis/.codex/memories/MEMORY.md`
- `sed -n '1,260p' reports/tracker-064-duck-e2e-comparison-investigation-20260514133853.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tasks-status.md`
- `sed -n '1,240p' Tracker/Tracker.Core/Design/phases-status.md`
- `sed -n '1,260p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
- `sed -n '1,220p' /home/ibis/.codex/memories/rollout_summaries/2026-05-12T05-50-55-ZWsV-tracker_captureon_fixed_list_review_audit_and_followup.md`
- `rg --files Tracker | rg 'CaptureReplay|TrackerSnapshot|Diagnostics|Alignment|Receive|README|Design|appsettings'`
- `find Tracker -maxdepth 4 -type f \( -name '*TrackerSnapshot*' -o -name '*Diagnostics*' -o -name '*CaptureReplay*' -o -name '*Alignment*' -o -name '*Receiver*' \)`
- `nl -ba Tracker/Tracker.Server/README.md | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerConnectionLibReceiverHostedService.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs | sed -n '1,280p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '1,760p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs | sed -n '1,380p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsReplayTimelineIndex.cs | sed -n '1,280p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs | sed -n '1,260p'`
- `rg -n "TrackerConnectionLibReceiverHostedService|TrackerSnapshotAlignmentLogWriter|TrackerPacketSnapshotLogWriter|Tracker:Receive|Receive" Tracker/Tracker.Server/Program.cs Tracker/Tracker.Server/appsettings.json Tracker/Tracker.Server/Tracking -g '*.cs' -g '*.json'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs | sed -n '1,320p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs | sed -n '1,340p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerReceiveEndpointResolver.cs | sed -n '1,220p'`
- `find Tracker/Tracker.Core/Design/Ref/framework -maxdepth 3 -type f | sed -n '1,220p'`
- `rg -n "simulator|protocol|tracker|ssl|vision|protobuf|udp|multicast|ground|truth|truth|er-force|erforce|command|packet|Tracked|Wrapper|TrackerWrapper|grSim|grpc|docker|compose" Tracker/Tracker.Core/Design/Ref/framework -g '!**/build/**' -g '!**/.git/**'`
- `nl -ba Tracker/Tracker.Core/Design/Ref/framework/README.md | sed -n '1,100p'`
- `nl -ba Tracker/Tracker.Core/Design/Ref/framework/data/docker/README.md | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Core/Design/Ref/framework/data/docker/simulatorcli_entrypoint.bash | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Core/Design/Ref/framework/src/simulator/simulator.cpp | sed -n '1,790p'`
- `nl -ba Tracker/Tracker.Core/Design/Ref/framework/src/simulator/ssl_robocup_server.cpp | sed -n '1,320p'`
- `rg -n "SSL_SIMULATION_CONTROL|SSL_VISION|VISION_PORT|CONTROL_PORT|10020|10300|10301|10302|localhost|multicast|sendVisionData|teleport|RobotControl|SimulatorCommand" Tracker/Tracker.Core/Design/Ref/framework/src/simulator Tracker/Tracker.Core/Design/Ref/framework/src/core Tracker/Tracker.Core/Design/Ref/framework/src/protobuf Tracker/Tracker.Core/Design/Ref/framework -g '*.cpp' -g '*.h' -g '*.proto' -g 'CMakeLists.txt' -g '*.txt'`
- `nl -ba Tracker/Tracker.Core/Design/Ref/framework/src/core/include/core/sslprotocols.h | sed -n '1,120p'`
- `nl -ba Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_sim/ssl_simulation_control.proto | sed -n '1,140p'`
- `nl -ba Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_sim/ssl_simulation_robot_control.proto | sed -n '1,150p'`
- `nl -ba Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_vision/ssl_wrapper_tracked.proto | sed -n '1,180p'`
- `nl -ba Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_vision/ssl_detection_tracked.proto | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Core/Design/Ref/framework/src/amun/gamecontroller/sslvisiontracked.cpp | sed -n '1,180p'`
- `rg -n "TrackerPacketSnapshot|TrackerSnapshotAlignment|ReplayTimeline|CaptureOn|trackerSnapshot|trackerComparison|source label|LatestBefore|saved-session-alignment|RawPayload|SemanticSummary|Position|Velocity|Orientation|Delta" Tracker/Tracker.Tests -g '*.cs'`
- `git status --short`
- テスト / build はユーザー指示により未実行。

## 対象ファイル

- 変更または確認したファイル:
- 変更: `reports/tracker-064-duck-e2e-comparison-investigation-20260514133853.md`
- 確認: `Tracker/Tracker.Core/Design/tasks-status.md`
- 確認: `Tracker/Tracker.Core/Design/phases-status.md`
- 確認: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
- 確認: `Tracker/Tracker.Server/README.md`
- 確認: `Tracker/Tracker.Server/Program.cs`
- 確認: `Tracker/Tracker.Server/appsettings.json`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerConnectionLibReceiverHostedService.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerReceiveEndpointResolver.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsReplayTimelineIndex.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
- 確認: `Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs`
- 確認: `Tracker/Tracker.Tests/CaptureReplayTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerCaptureOnSessionSnapshotContractTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerDiagnosticsReplayTimelineIndexTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerLiveExternalTrackerReceiverTddTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerRuntimeRegistrationTddTests.cs`
- 確認: `Tracker/Tracker.Core/Design/Ref/framework/README.md`
- 確認: `Tracker/Tracker.Core/Design/Ref/framework/data/docker/README.md`
- 確認: `Tracker/Tracker.Core/Design/Ref/framework/data/docker/simulatorcli_entrypoint.bash`
- 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/core/include/core/sslprotocols.h`
- 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/simulator/simulator.cpp`
- 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/simulator/ssl_robocup_server.cpp`
- 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_sim/ssl_simulation_control.proto`
- 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_sim/ssl_simulation_robot_control.proto`
- 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_vision/ssl_wrapper_tracked.proto`
- 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_vision/ssl_detection_tracked.proto`
- 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/amun/gamecontroller/sslvisiontracked.cpp`

## 指摘事項

- 指摘要約または「指摘なし」:
- blocking: Duck 側の既存 CaptureOn / diagnostics / comparison は「同一 session に raw vision、ibis own tracker、3rd party tracker packet、alignment を保存して再生する」土台として使える。一方で、E2E 差分評価に必要な「simulator protocol 入力シナリオ」「参照真値」「数値 metric 計算」「同一 session manifest」はまだない。
- blocking: `Tracker.CaptureReplay` と `/diagnostics` の既存 comparison は source role / label、timestamp delta、ball / robot count、raw payload restored、Field 表示までで、position error / velocity error / orientation error / ID switch / missing frame / count mismatch を数値評価する実装は未整備。
- blocking: `TrackerPacketSnapshotSemanticSummary` は raw payload 由来の ball position と robot position / orientation を保持するが、velocity error には raw `TrackerWrapperPacket` decode か semantic summary 拡張が必要。ibis own 側も metric 計算用に同じ形へ正規化する境界が必要。
- blocking: `Ref/framework` の `simulator-cli` は SSL simulation protocol の `SimulatorCommand` を 10300、blue/yellow の `RobotControl` を 10301/10302 で受け、SSL-Vision を 224.5.23.2:10020 または `--localhost` で 127.0.0.1:10020 へ出す。Duck は `VisionReceiver` を 10020 に合わせれば入力を受けられるが、scenario command sender は Duck 側にない。
- blocking: `Ref/framework` には tracked vision protocol (`TrackerWrapperPacket`, default 224.5.23.2:10010) と `SSLVisionTracked` 生成実装があるが、確認範囲では `simulator-cli` 自体が tracked packet を multicast する経路は見つけていない。ER-Force tracker 出力を CaptureOn に入れるには、別 tracker process / bridge / 送信 endpoint の固定が必要。
- blocking: 参照真値は未接続。`simulator-cli` の標準出力は SSL-Vision protocol であり、要件が求める観測ノイズなし真値として使える専用 sidecar / protocol は Duck 側にも `Ref/framework` の確認範囲にもまだ見えていない。初期要件では「ノイズなし真値が取れない場合は ER-Force tracker、Tigers tracker の順で fallback」と明記すべき。
- non-blocking: grSim は既存 tracking で不採用が固定されており、今回の Duck 側接続調査でも参照しない。

## 結果

- 結果:
- Duck 側の既存機能で使えるもの:
- `Tracker.Server` raw vision receiver は `SSL_WrapperPacket` を直接受ける。`raw-vision-viewer-plan.md` は raw viewer が `Tracker.Server` 境界で `SslProto` を使い、Tracker Core に raw-vision 処理を混ぜない方針を固定している。
- `Tracker.Server` の CaptureOn は session folder に `*.jsonl.gz`、`*.metadata.json`、`*.tracker-diagnostics.log`、`*.render-snapshots.jsonl.gz`、`tracker-packet-snapshots.jsonl`、`tracker-snapshot-alignment.jsonl` をまとめる。README は metadata から `TrackerSnapshotSidecarPath` / `TrackerSnapshotAlignmentPath` / source metadata を辿る運用を説明している。
- tracker receiver は `Tracker:Receive:Enabled=true` のときだけ `UdpTrackerReceiver<TrackerPacketAdapter>` を起動し、`Tracker:Receive:MulticastAddress` / `Port` / `InterfaceAddress` を優先、未指定値は resolved publish endpoint へ fallback する。official tracker packet は `MultiTrackerManager` 経由で own / external / unknown に分類される。
- snapshot sidecar は `TrackerWrapperPacket` raw payload、source uuid/name/role/label、remote endpoint、tracked frame number、timestamp ns、semantic summary を保存する。semantic summary は ball count / robot count、ball 代表位置、robot 代表位置 / orientation を持つ。
- alignment は `tracker-snapshot-alignment.jsonl` schema v2 で diagnostics entry、render snapshot、tracker snapshot を capture-time `ReceivedAt` と replay timeline index で対応付ける。外部 tracker の `TrackedFrame.timestamp` が ibis と非同一時刻系でも、保存時 alignment と latest-before fallback で `/diagnostics` Field / comparison に出せる。
- `/diagnostics` は selected replay timeline tick、source filter、左右 Field source、split / overlay、Tracker Comparison panel、sidecar / alignment status、timestamp delta、raw payload restored を表示できる。速度制御、scrub、fast tracker cadence timeline も既存実装がある。
- `Tracker.CaptureReplay` は capture と metadata を読み、同一 session の sidecar から `trackerSnapshot` / `trackerComparison` 行を出せる。agent / regression 用の CLI evidence として使える。
- `Ref/framework` から Duck に接続できる見込み:
- `simulator-cli` は SSL simulation protocol 入力を受け、世界状態を SSL-Vision protocol で出す。入力は `SimulatorCommand` の `teleport_ball` / `teleport_robot` / `simulation_speed`、および `RobotControl` の robot velocity / kick / dribbler command が使える。
- network 既定は simulator control 10300、blue robot control 10301、yellow robot control 10302、simulated vision 10020。Duck `VisionReceiver` は profile で 10020 を受ける設定にすれば ER-Force simulator 入力を CaptureOn へ入れられる。
- `data/docker/Dockerfile.simulatorcli` と Docker Hub `roboticserlangen/simulatorcli` の記述があり、TRACKER-066 では Duck repo 側に opt-in 起動 script / compose を置く候補がある。ただし Docker 起動は unit test の常時依存にしない。
- `TrackerWrapperPacket` / `TrackedFrame` protocol は `Ref/framework` に同梱され、default tracker port は 10010。Duck 側 receiver が読む protocol と整合するため、ER-Force tracker または bridge がこの packet を送れば CaptureOn sidecar に保存できる。
- 足りないもの:
- scenario definition: ボール静止、ロボット静止、ボール直線運動、ロボット直線運動、ロボット ID 近接を同一形式で表す YAML/JSON/CLI 引数がない。
- scenario command sender: `ssl_simulation_control.proto` / `ssl_simulation_robot_control.proto` を使い、10300/10301/10302 に command を送る Duck 側 tool がない。
- session manifest: simulator command、Duck profile、VisionReceiver endpoint、tracker receiver endpoint、参照真値 source、capture artifact path、scenario id を同一 session に結びつける manifest がない。
- reference truth input: 観測ノイズなし真値の取得経路が未確定。標準 `simulator-cli` の SSL-Vision 出力は観測出力であり、真値専用出力としては扱えない。
- third-party tracker orchestration: ER-Force tracker / Tigers tracker を同じ simulator 入力に接続し、`TrackerWrapperPacket` をどの endpoint に出すかを Duck repo から opt-in で起動 / 設定する資産がない。
- metric engine: position error、velocity error、orientation error、ID switch、missing frame、latency / timestamp delta、count mismatch を session artifact から算出する pure model / CLI がない。
- object matching rule: ball primary / multiple ball、robot team/id、missing / stale / latest-before、timestamp hold を評価 metric に変換する規則が未定義。
- coordinate / unit normalization: simulator protocol は外部単位 m / m/s、Duck sidecar summary は mm、tracker protocol は m のため、metric 境界で単位・角度 wrap・team orientation を固定する必要がある。
- 最小 E2E smoke / 手元確認項目案:
- smoke: `simulator-cli --localhost -g 2020 --realism None` または Docker 起動後、Duck `VisionReceiver` を `127.0.0.1:10020` または `224.5.23.2:10020` に合わせ、`/` raw viewer の packet count と geometry/detection 表示が増えること。
- packet受信: CaptureOn 中に simulator へ `TeleportBall` / `TeleportRobot` を送信し、session folder に `*.jsonl.gz` と `*.tracker-diagnostics.log` が作られ、`Tracker.CaptureReplay --capture ... --settings ... --expect 'committed-frames>0'` が通ること。
- CaptureOn sidecar: `Tracker:Receive:Enabled=true` と receiver endpoint を 10010 など外部 tracker 出力へ合わせ、`tracker-packet-snapshots.jsonl` の `RecordCount>0`、source role / label が `external` または対象 label、`rawPayloadRestored=True` になること。
- alignment: `tracker-snapshot-alignment.jsonl` の `RecordCount>0`、`matchingRule=saved-session-alignment`、selected replay timeline で `/diagnostics` の `External` Field source と `Tracker Comparison` が Ready になること。
- 差分出力: 初期は count / timestamp delta / raw payload restored の CLI 出力を smoke とし、TRACKER-066 で metric engine が入った後に position / velocity / orientation / ID switch / missing frame / latency / count mismatch を同じ capture から出す。
- TRACKER-066 で最初に書くべき TDD 候補:
- `E2ESimulatorScenarioCommandBuilderTests`: ball / robot static と linear motion scenario から `SimulatorCommand` / `RobotControl` payload、target endpoint 10300/10301/10302、単位 m / m/s が期待通りになること。red proof は builder / scenario schema 未存在の compile failure または missing command failure。
- `E2ESessionManifestTests`: capture metadata と E2E manifest が scenario id、simulator endpoint、vision endpoint、tracker receive endpoint、truth source priority、artifact relative path を保持すること。red proof は manifest 型 / writer 未存在。
- `E2EComparisonMetricTests`: own / external / truth の最小 in-memory frames から position error、orientation wrap error、count mismatch、missing frame、latency delta を計算する pure model。red proof は metric model 未存在または count/timestamp しか出せない失敗。
- `CaptureReplayE2EComparisonOutputTests`: metadata + snapshot sidecar + truth sidecar / manifest から `Tracker.CaptureReplay` が `e2eComparison` 行を出すこと。red proof は現在 `trackerSnapshot` / `trackerComparison` しか出ないことを確認する。
- `TrackerPacketSemanticSummaryTests`: velocity error を semantic summary で扱う方針にする場合、ball / robot velocity が summary に含まれること。red proof は現在 velocity property が存在しない compile failure。
- manual evidence と opt-in automation の切り分け:
- manual evidence: simulator / external tracker / Docker / multicast に依存する起動、実 packet 受信、ブラウザ `/diagnostics` 表示、CaptureOn session artifact 確認。
- opt-in automation: `scripts/e2e-smoke` などで Docker 起動、command 送信、CaptureOn artifact path 確認、`Tracker.CaptureReplay` 実行までを行う。ただし通常 unit test / CI の必須経路に Docker と multicast を入れない。
- always-on tests: scenario parser / command builder / manifest writer / artifact path resolver / metric model / CaptureReplay formatter など、file と in-memory packet で閉じるもの。
- 差分指標候補:
- position error: ball は primary ball または truth ball index、robot は team + robot id で対応し、mm 単位距離を出す。
- velocity error: ball / robot velocity vector の差。現状 summary には不足するため raw payload decode または summary 拡張が必要。
- orientation error: robot orientation の wrap-aware angle delta。
- ID switch: 同じ truth robot に対する tracker 側 team/id の変化、または近接 scenario で id が入れ替わる事象。
- missing frame: selected truth tick に own / external / fallback source が存在しない、または latest-before が許容 staleness を超えた状態。
- latency / timestamp delta: selected replay timeline `ReceivedAt` と source snapshot `ReceivedAt`、own data timestamp と candidate timestamp の delta。
- count mismatch: ball / robot count の差、team別 robot count 差、expected present robot countとの差。
- 要件定義書の脚注候補:
- SSL simulation protocol: ER-Force simulator を制御する RoboCup SSL の protobuf/UDP protocol。`SimulatorCommand` と `RobotControl` を含む。
- `simulator-cli`: ER-Force framework の headless simulator binary。control command を受けて simulated SSL-Vision packet を出す。
- `SSL_WrapperPacket`: SSL-Vision の raw detection / geometry を含む wrapper packet。
- `TrackerWrapperPacket`: tracked vision 出力を source uuid/name と `TrackedFrame` 付きで包む packet。
- `TrackedFrame`: tracker が推定した ball / robot 状態の frame。frame number、timestamp、tracked balls / robots を持つ。
- CaptureOn: Duck `Tracker.Server` が raw packet / diagnostics / render snapshot / tracker snapshot / alignment を同一 session folder に保存する状態。
- snapshot sidecar: `tracker-packet-snapshots.jsonl`。CaptureOn 中に受けた official tracker packet の保存ログ。
- alignment sidecar: `tracker-snapshot-alignment.jsonl`。diagnostics / render / tracker snapshot を session timeline で対応付ける保存ログ。
- saved-session-alignment: 保存時 alignment record に基づき比較対象 snapshot を選ぶ matching rule。
- latest-before: selected tick 以前の同一 source の最新 snapshot を hold して比較する fallback。
- reference truth: 差分評価の基準値。優先順位は観測ノイズなし simulator truth、ER-Force tracker、Tigers tracker。
- opt-in automation: Docker / multicast / 外部 process を使うため通常 unit test には含めず、明示実行時だけ走らせる検証。

## リスク

- 未解決のリスクまたは後続対応:
- `Ref/framework` の標準 `simulator-cli` がノイズなし真値を外部出力する経路は未確認。TRACKER-064 要件では、取れない場合に simulator 改造または別経路 sidecar を検討する必要がある。
- `simulator-cli` 単体は確認範囲では tracked packet を multicast していない。ER-Force tracker 出力を参照真値 fallback にするには、どの binary / mode が `TrackerWrapperPacket` を 10010 等へ出すかを TRACKER-065 以降で固定する必要がある。
- GPLv3 の `Ref/framework` 資産を Duck repo へコピー / 改変する場合はライセンス境界を確認する必要がある。TRACKER-066 では最小限、Docker image 利用や protocol 参照、script 連携に留める方が安全。
- multicast / localhost / Docker network は環境依存。manual smoke は `--localhost` と明示 multicast の両方を候補にし、通常 unit test は network に依存させない。
- 速度 metric は現状 semantic summary だけでは不足する。raw payload decode を metric engine が直接行うか、snapshot semantic summary を拡張するかを設計で決める必要がある。
- 同一 session で「simulator 入力」「truth」「own tracker」「external tracker」を結ぶ manifest がないため、現状の metadata だけでは scenario 単位の再現性が弱い。
- `git status --short` では本 report 以外にも `tasks-status.md`、`phases-status.md`、`Tracker.Server/appsettings.json`、他 report の変更 / 未追跡が見えているが、本 sub-agent では一切編集していない。
