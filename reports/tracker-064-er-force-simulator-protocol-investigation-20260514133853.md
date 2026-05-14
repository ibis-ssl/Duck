# Sub-agent実行レポート

## タスク

- 目的: ER-Force simulator protocol でボール / ロボット移動を制御する経路、観測ノイズ込み出力、観測ノイズなし真値の取得可能性、真値取得不可時の simulator 改造候補を調査する。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: ユーザー指定により調査を分担し、gpt-5.5 high の sub-agent が証跡を日本語 report に残すため。

## 対象範囲

- 対象: `/home/ibis/ibis_ws/src/crane` と `/home/ibis/ibis_ws/src/crane/docker` にある ER-Force simulator protocol、protobuf、設定、script、ノイズ設定、真値取得または出力改造に関係するファイル。

## 対象外

- 対象外: grSim の採用、Duck repo への実装追加、Docker 資産の移植判断、Duck 側比較 UI / capture 実装詳細。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
  - `sed -n '1,260p' reports/tracker-064-er-force-simulator-protocol-investigation-20260514133853.md`
  - `sed -n '1,260p' Tracker/Tracker.Core/Design/tasks-status.md`
  - `sed -n '1,220p' Tracker/Tracker.Core/Design/phases-status.md`
  - `git status --short`
  - `rg --files /home/ibis/ibis_ws/src/crane | rg -i '(sim|erforce|er_force|vision|proto|protobuf|ssl|tracker|docker|config|launch|scenario|world|packet|noise|geometry)'`
  - `rg -n "(ER-Force|erforce|er_force|ssl-simulation|ssl_simulation|Simulator|simulation|vision|truth|noise|Detection|SSL_Wrapper|Teleport|RobotControl|1000[0-9]|2001[0-9]|1030[0-9])" /home/ibis/ibis_ws/src/crane -S`
  - `sed` / `nl -ba` で Crane の `docs/erforce_sim.md`、`docker/dev/docker-compose.yaml`、`docker/dev/README.md`、`scenario_test/README.md`、`crane_bringup/launch/crane.launch.xml`、`crane_sender/src/ibis_sender_node.cpp`、`robocup_ssl_comm/src/vision_component.cpp`、`robocup_ssl_comm/src/tracker_component.cpp`、SSL simulation / vision protobuf を確認。
  - `rg --files Tracker/Tracker.Core/Design/Ref/framework | rg -i '(sim|vision|tracker|protocol|protobuf|proto|realism|noise|truth|state|world|ball|robot|camera|ssl)'`
  - `rg -n "(SSL_WrapperPacket|SSL_DetectionFrame|TrackerWrapperPacket|TrackedFrame|RobotControl|SimulatorCommand|TeleportBall|TeleportRobot|noise|Noise|realism|Realistic|Friendly|vision|truth|ground|state|publish|port|10300|10301|10302|10020|11010)" Tracker/Tracker.Core/Design/Ref/framework -S`
  - `sed` / `nl -ba` で ER-Force framework の `README.md`、`src/simulator/simulator.cpp`、`src/amun/simulator/simulator.cpp`、`src/amun/simulator/include/simulator/simulator.h`、`src/amun/simulator/simball.cpp`、`src/amun/simulator/simrobot.cpp`、`src/protobuf/protobuf/world.proto`、`src/core/include/core/sslprotocols.h`、`src/protobuf/protobuf/ssl_sim/*.proto`、`src/protobuf/protobuf/ssl_vision/*.proto`、`config/simulator-realism/*.txt` を確認。
  - `git -C Tracker/Tracker.Core/Design/Ref/framework status --short && git -C Tracker/Tracker.Core/Design/Ref/framework log -1 --oneline --decorate`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/tracker-064-er-force-simulator-protocol-investigation-20260514133853.md`
  - 確認: `Tracker/Tracker.Core/Design/tasks-status.md`
  - 確認: `Tracker/Tracker.Core/Design/phases-status.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
  - 確認: `/home/ibis/ibis_ws/src/crane/docs/erforce_sim.md`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/dev/docker-compose.yaml`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/dev/README.md`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/ssl-vision-client-config.json`
  - 確認: `/home/ibis/ibis_ws/src/crane/scenario_test/README.md`
  - 確認: `/home/ibis/ibis_ws/src/crane/crane_bringup/launch/crane.launch.xml`
  - 確認: `/home/ibis/ibis_ws/src/crane/crane_sender/src/ibis_sender_node.cpp`
  - 確認: `/home/ibis/ibis_ws/src/crane/consai_ros2/robocup_ssl_msgs/proto/ssl_simulation_control.proto`
  - 確認: `/home/ibis/ibis_ws/src/crane/consai_ros2/robocup_ssl_msgs/proto/ssl_simulation_robot_control.proto`
  - 確認: `/home/ibis/ibis_ws/src/crane/consai_ros2/robocup_ssl_msgs/proto/ssl_simulation_synchronous.proto`
  - 確認: `/home/ibis/ibis_ws/src/crane/consai_ros2/robocup_ssl_msgs/proto/ssl_simulation_config.proto`
  - 確認: `/home/ibis/ibis_ws/src/crane/consai_ros2/robocup_ssl_comm/src/vision_component.cpp`
  - 確認: `/home/ibis/ibis_ws/src/crane/consai_ros2/robocup_ssl_comm/src/tracker_component.cpp`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/README.md`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/simulator/simulator.cpp`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/amun/simulator/simulator.cpp`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/amun/simulator/include/simulator/simulator.h`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/amun/simulator/simball.cpp`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/amun/simulator/simrobot.cpp`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/world.proto`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/core/include/core/sslprotocols.h`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_sim/ssl_simulation_control.proto`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_sim/ssl_simulation_robot_control.proto`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_sim/ssl_simulation_custom_erforce_realism.proto`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/config/simulator-realism/None.txt`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/config/simulator-realism/Realistic.txt`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/amun/amun.cpp`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/amun/processor/processor.cpp`
  - 確認: `Tracker/Tracker.Core/Design/Ref/framework/src/amun/gamecontroller/sslvisiontracked.cpp`

## 指摘事項

- 指摘要約または「指摘なし」:
  - 既存の `simulator-cli` / Docker 経路で外部から直接取得できるのは、観測ノイズ込みの `SSL_WrapperPacket` detection が主経路である。観測ノイズなし真値 `world::SimulatorState` は ER-Force framework 内部で生成・emit されているが、standalone `simulator-cli` では外部 network / file へ出す接続が見つからなかった。
  - `--realism None` は detection の位置ノイズ等をゼロにできるが、出力形式はあくまで camera / vision detection であり、Realistic 条件の観測とは別経路の真値にはならない。Realistic 観測と同時に同一tickのノイズなし物理状態を比較したい場合は simulator 改造が必要。
  - Crane 側には `ssl_simulation_synchronous.proto` があるが、今回確認した ER-Force framework の `simulator-cli` 実装では UDP async の `SimulatorCommand` / `RobotControl` が実経路で、synchronous request/response 実装は見つからなかった。

## 結果

- 結果:
  - 1. ボール / ロボット移動を制御する既存経路:
    - protocol 名は SSL Simulation Protocol。ER-Force framework 側 protobuf は `src/protobuf/protobuf/ssl_sim/ssl_simulation_control.proto` と `ssl_simulation_robot_control.proto`、Crane 側生成元は `/home/ibis/ibis_ws/src/crane/consai_ros2/robocup_ssl_msgs/proto/ssl_simulation_control.proto` と `ssl_simulation_robot_control.proto`。
    - simulator 全体制御は `sslsim.SimulatorCommand` を UDP `10300` に送る。`SimulatorControl.teleport_ball` は `x/y/z` と `vx/vy/vz`、`teleport_safely`、`roll`、`by_force` を持ち、ボール静止・直線運動・force 移動の初期条件を作れる。`SimulatorControl.teleport_robot` は `BotId`、`x/y`、`orientation`、`v_x/v_y/v_angular`、`present`、`by_force` を持ち、ロボット配置・直線運動・ID近接配置を作れる。
    - ロボット制御は `sslsim.RobotControl` を UDP `10301` blue / `10302` yellow に送る。`RobotCommand` は `MoveWheelVelocity`、`MoveLocalVelocity`、`MoveGlobalVelocity`、`kick_speed`、`kick_angle`、`dribbler_speed` を持つ。Crane の `ibis_sender_node` は `packet_type=ssl` 時に `RobotControl` を 10301 / 10302 へ送り、内部の `crane_msgs::msg::RobotCommands` を `MoveLocalVelocity` と kick / dribbler に変換している。
    - ER-Force framework の port 定義は `src/core/include/core/sslprotocols.h` にあり、vision output は multicast `224.5.23.2:10020`、simulator control は `10300`、blue / yellow robot control は `10301` / `10302`。Crane docker dev も `erforce-sim` を `ghcr.io/ibis-ssl/framework-simulatorcli:latest`、`-g 2023 --realism Realistic`、host network で起動し、Vision は `${VISION_PORT:-10020}`、tracker fallback は `11010` を使う。
    - config / script は Crane 側では `docker/dev/docker-compose.yaml` と `scripts/docker-dev.sh --sim erforce` が主経路。framework 側では `simulator-cli -g <geometry> --realism <realism>` で `config/simulator/<geometry>.txt` と `config/simulator-realism/<realism>.txt` を読む。Crane docs は geometry `2023` / realism `Realistic` を既定例としている。
  - 2. 観測ノイズ込み出力と、ノイズなし真値の既存経路:
    - 観測ノイズ込み出力は ER-Force framework の `Simulator::createVisionPacket()` が `SSL_DetectionFrame` を作り、`SSL_WrapperPacket.detection` として serialize し、`Simulator::sendVisionPacket()` から `gotPacket`、`simulator-cli` の `SSLVisionServer::sendVisionData()`、UDP multicast `224.5.23.2:10020` へ流す経路。Crane の `Vision` component は同 multicast / port を受け、`SSL_WrapperPacket` を parse して ROS `detection_frame` に変換する。
    - 観測ノイズは realism config で入る。`Realistic.txt` では `stddev_ball_p: 0.0014`、`stddev_robot_p: 0.0013`、`stddev_robot_phi: 0.01`、`stddev_ball_area: 6.5`、missing detection、camera position error、object position offset、command delay、rotated robot detection などが有効。framework の `SimBall::update(SSL_DetectionBall*)` は position / area noise を加え、`SimRobot::update(SSL_DetectionRobot*)` は position / orientation noise を加える。
    - ノイズなし真値は framework 内部では存在する。`Simulator::createVisionPacket()` は detection 生成前後に `world::SimulatorState simState` を作り、`SimBall::writeBallState()` と `SimRobot::update(world::SimRobot*)` で Bullet 物理状態の ball / robot position, velocity, rotation, angular velocity を noise なしで詰め、`sendRealData` signal で serialize 済み `world::SimulatorState` を emit している。
    - ただし standalone `simulator-cli` の `main()` では `SimProxy::gotPacket` を `SSLVisionServer::sendVisionData` に接続しているだけで、`sendRealData` を外部 network / file へ接続する処理は見つからなかった。Amun 内部 simulator では `sendRealData` が `Processor::handleSimulatorExtraVision` に接続され、`Status.world_state.reality` に注入されるため、full Amun / Ra 内部ログでは真値を持ち得るが、Crane docker の `erforce-sim` 単体を外部から読む normal path では未露出。
  - 3. 真値出力改造候補:
    - 第一候補は ER-Force `simulator-cli` に truth output を追加し、既存 `world::SimulatorState` をそのまま `SSL_WrapperPacket` とは別に出すこと。最小改造点は `src/simulator/simulator.cpp` の `SimProxy` / `main()` 周辺で、`Simulator::sendRealData` を新しい writer / UDP publisher に接続する。物理状態生成済みなので、ball / robot の再計算は不要。
    - network protocol 案: multicast または localhost UDP で `world.SimulatorState` を protobuf binary のまま送る。既存 vision と衝突しない専用 port、例えば `11020` や設定可能 `--truth-port` を使う。Duck 側は `Tracker.Server` に truth receiver を追加し、CaptureOn session に `simulator-truth.jsonl` または protobuf payload sidecar として保存し、diagnostics comparison は `truth` source を最優先にする。利点は live E2E と相性がよい。リスクは protocol 周知、port 管理、packet loss、timestamp alignment。
    - file output 案: `--truth-output <path>` を追加し、`world::SimulatorState` を length-delimited protobuf または JSONL に保存する。Duck 側は CaptureOn metadata に truth file path を持たせ、log replay / offline comparison で読み込む。利点は packet loss がなく再現性が高い。リスクは live diagnostics 連携が遅れ、container volume / path 管理が必要。
    - 推奨は段階導入。まず file output で deterministic E2E の比較基準を作り、次に必要なら network output を追加する。Duck 側の受け口は raw vision / tracker packet と同じ CaptureOn sidecar 方針に合わせ、timestamp は `world.SimulatorState.time` ns と capture-time `ReceivedAt` を両方保持する。
  - 4. 参照真値 fallback:
    - fallback 優先順位は要件どおり ER-Force tracker -> Tigers tracker。Crane docker dev には `roboticserlangen/autoref:2025.1.0` が `--vision-port ${VISION_PORT:-10020}`、`--tracker-port 11010`、`--gc-port ${REFEREE_PORT:-11003}` で定義され、`ssl-log-recorder` も `-vision-tracker-address 224.5.23.2:${TRACKER_PORT:-11010}` を読む。Tigers auto-ref は comment out されているが同じ `trackerAddress 224.5.23.2:11010` の候補が残っている。
    - fallback の限界は、どちらも simulator truth ではなく `SSL_DetectionFrame` 由来の tracker output であること。Realistic の noise、missing detection、camera split、tracker 独自の補間・ID保持・遅延・予測・timestamp 系が入るため、真値として使うと tracker-vs-tracker 差分の一部が参照 tracker の推定誤差になる。
    - 比較指標への影響は、絶対誤差やID switch判定では閾値を緩くし、参照 tracker の不確実性を residual risk として別記する必要があること。ノイズなし truth がない場合は「真値誤差」ではなく「参照 tracker との差分」と表記し、速度・加速度・ID近接の評価は reference bias を含むものとして扱う。
  - 5. 初期シナリオに必要な protocol 入力:
    - ボール静止: `SimulatorCommand.control.teleport_ball` に `x/y/z=0`、`vx/vy/vz=0`、必要なら `roll=false` を送る。観測後は `world.SimulatorState.ball.v_*` または fallback tracker 速度がほぼ0であることを見る。
    - ロボット静止: `TeleportRobot` で `id/team`、`x/y`、`orientation`、`v_x/v_y/v_angular=0`、`present=true` を送る。必要な robot specs は既定11台で足りるが、特殊半径・質量を変える場合は `SimulatorConfig.robot_specs` が必要。
    - ボール直線運動: `TeleportBall` に開始 `x/y/z` と `vx/vy/vz` を入れる。転がりを合わせるなら `roll=true`。一定目標位置へ寄せる試験なら `by_force=true` を使えるが、持続 force は cancel が必要なので、初期E2Eでは単発 velocity のほうが比較しやすい。
    - ロボット直線運動: 初期配置は `TeleportRobot`、運動は `RobotControl.robot_commands.move_command.global_velocity` または Crane の `packet_type=ssl` から `MoveLocalVelocity` を送る。真値比較では body orientation と local/global 座標変換を明示する必要がある。
    - ロボットID近接: 同一 team の2台以上に `TeleportRobot` を使い、`id` を明示して近接した `x/y` と orientation を設定する。距離、交差の有無、速度、camera overlap 領域、Realistic の rotated robot detection / missing_robot_detections の有無を入力条件として固定する。
  - 6. 要件定義書に入れる脚注候補:
    - SSL Simulation Protocol: RoboCup SSL simulator と team / controller 間の protobuf UDP protocol。`SimulatorCommand`、`RobotControl`、`RobotControlResponse` などを含む。
    - `SimulatorCommand`: simulator の teleport / config / simulation speed を制御する protobuf message。
    - `RobotControl`: blue / yellow team port に送るロボット制御 protobuf message。wheel / local / global velocity、kick、dribbler を含む。
    - `SSL_WrapperPacket`: SSL-Vision互換の観測出力 wrapper。detection と geometry を含む。
    - `SSL_DetectionFrame`: camera単位の観測フレーム。Realistic realism では位置・角度・missing detection 等のノイズが入る。
    - `world::SimulatorState`: ER-Force framework 内部のノイズなし物理状態。ball / robot の position、velocity、rotation、angular velocity を持つ。
    - `TrackerWrapperPacket` / `TrackedFrame`: 参照 tracker fallback が出す追跡済み object 出力。truth ではなく tracker 推定値。
    - realism config: ER-Force simulator の観測ノイズ、遅延、欠落、カメラ誤差、robot command loss 等をまとめた設定。
    - ground truth / 真値: 比較基準に使うノイズなし物理状態。本調査では `world::SimulatorState` を最有力候補とする。
    - reference truth / 参照真値: 真値が取れない場合に代用する external tracker 出力。評価名では真値と区別する。
    - multicast address / port: vision は `224.5.23.2:10020`、simulator control は UDP `10300`、robot control は `10301` / `10302`、tracker fallback は `224.5.23.2:11010`。

## リスク

- 未解決のリスクまたは後続対応:
  - `ghcr.io/ibis-ssl/framework-simulatorcli:latest` の実イメージには Crane compose で指定される `--ibis-use-referee` 等の追加引数があるが、配置された framework repo の `src/simulator/simulator.cpp` では該当 option を確認できなかった。image 側の差分 branch / patch が別にある可能性がある。
  - framework 内部の `sendRealData` は有力な真値経路だが、standalone `simulator-cli` から外部出力されていないため、Duck 側で使うには ER-Force simulator 改造または full Amun/Ra log 取り込み設計が必要。
  - `world::SimulatorState` は framework 独自 protobuf で、Duck repo の既存 `SslProto` にはその schema がない可能性が高い。取り込み時は proto 移植、schema固定、license / generated code 管理が必要。
  - network truth output は packet loss / ordering / timestamp alignment の設計が必要。file truth output は volume path と live diagnostics 反映遅延の設計が必要。
  - fallback tracker は観測ノイズ込み出力の推定結果なので、ID近接・速度・加速度の比較で reference bias が残る。要件定義書では「truth unavailable mode」の指標名と閾値を分ける必要がある。
