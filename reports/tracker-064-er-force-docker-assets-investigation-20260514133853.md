# Sub-agent実行レポート

## タスク

- 目的: `/home/ibis/ibis_ws/src/crane/docker` のうち、ER-Force simulator と 3rd party tracker の E2E 差分評価に使う Docker / compose / config / script / README 資産を調査する。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: ユーザー指定により調査を分担し、gpt-5.5 high の sub-agent が証跡を日本語 report に残すため。

## 対象範囲

- 対象: `/home/ibis/ibis_ws/src/crane/docker` の ER-Force simulator / 3rd party tracker 関連 Docker 資産、service topology、port、multicast、volume、config、script、README。

## 対象外

- 対象外: grSim の採用、Duck repo への実装追加、長時間 Docker build / 起動、他 sub-agent が担当する simulator protocol 詳細と Duck 側実装詳細。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `sed -n '1,260p' reports/tracker-064-er-force-docker-assets-investigation-20260514133853.md`
  - `sed -n '1,260p' Tracker/Tracker.Core/Design/tasks-status.md`
  - `sed -n '1,220p' Tracker/Tracker.Core/Design/phases-status.md`
  - `rg --files /home/ibis/ibis_ws/src/crane/docker`
  - `rg -n "erforce|er-force|ER-Force|tracker|simulator|grsim|grSim|ssl|multicast|224\\.|100[0-9]{2}|compose|Dockerfile" /home/ibis/ibis_ws/src/crane/docker`
  - `sed -n '1,240p' /home/ibis/ibis_ws/src/crane/docker/dev/docker-compose.yaml`
  - `sed -n '1,220p' /home/ibis/ibis_ws/src/crane/docker/scenario/docker-compose.yaml`
  - `sed -n '1,220p' /home/ibis/ibis_ws/src/crane/docker/scenario/docker-compose.local.yaml`
  - `sed -n '1,220p' /home/ibis/ibis_ws/src/crane/docker/scenario/README.md`
  - `sed -n '1,220p' /home/ibis/ibis_ws/src/crane/docker/dev/README.md`
  - `sed -n '1,180p' /home/ibis/ibis_ws/src/crane/docker/config/ssl-game-controller-sim.yaml`
  - `sed -n '1,140p' /home/ibis/ibis_ws/src/crane/docker/config/ssl-game-controller.yaml`
  - `sed -n '1,220p' /home/ibis/ibis_ws/src/crane/docker/config/engine.yaml`
  - `sed -n '1,360p' /home/ibis/ibis_ws/src/crane/docker/config/ssl-simulation-controller/robot-specs.yaml`
  - `sed -n '1,120p' /home/ibis/ibis_ws/src/crane/docker/ssl-vision-client-config.json`
  - `sed -n '1,160p' /home/ibis/ibis_ws/src/crane/docker/README.md`
  - `sed -n '1,220p' /home/ibis/ibis_ws/src/crane/docker/scenario/Dockerfile`
  - `sed -n '1,230p' /home/ibis/ibis_ws/src/crane/docker/match-vs-tigers/docker-compose.yaml`
  - `sed -n '1,240p' /home/ibis/ibis_ws/src/crane/docker/match-vs-tigers/README.md`
  - `sed -n '1,190p' /home/ibis/ibis_ws/src/crane/docker/match-vs-tigers/TESTING_NOTES.md`
  - `sed -n '1,150p' /home/ibis/ibis_ws/src/crane/docker/match-vs-tigers/config/ssl-game-controller-match.yaml`
  - `rg --files /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework`
  - `rg -n "simulator|tracker|compose|Dockerfile|docker|ssl-simulation-protocol|erforce|ER-Force|vision|trackerAddress|11010|10020|10003|11003|truth|ground" /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework`
  - `rg --files /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework | rg '(^|/)(Dockerfile|docker-compose|compose|.*docker.*|.*Docker.*)$|\\.ya?ml$|\\.proto$|simulator|tracking|tracker'`
  - `rg -n "Simulator CLI|simulator-cli|ssl-simulation-protocol|port 10020|--localhost|tracker|tracked|Vision|multicast|raw vision|ground|truth|ssl_vision_wrapper_tracked" /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/README.md /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/COMPILE.md /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/simulator /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/amun/processor/tracking /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf`
  - `sed -n '1,170p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/README.md`
  - `sed -n '1,180p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/COMPILE.md`
  - `sed -n '1,220p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/data/docker/Dockerfile.simulatorcli`
  - `sed -n '1,180p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/data/docker/simulatorcli_entrypoint.bash`
  - `sed -n '640,730p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/simulator/simulator.cpp`
  - `sed -n '1,90p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/simulator/simulator.cpp`
  - `sed -n '1,140p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_sim/ssl_simulation_control.proto`
  - `sed -n '1,140p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_sim/ssl_simulation_robot_control.proto`
  - `sed -n '1,100p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/core/include/core/sslprotocols.h`
  - `sed -n '100,210p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/simulator/simulator.cpp`
  - `sed -n '1,130p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/trackingreplaycli/trackingreplaycli.cpp`
  - `rg -n "TrackerWrapperPacket|SSL_VISION_TRACKER|source_name|tracked_frame|send.*tracker|tracker.*port|10010" /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/amun /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/trackingreplaycli /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/protobuf`
  - `sed -n '1,160p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/amun/gamecontroller/sslvisiontracked.cpp`
  - `docker compose -f docker/dev/docker-compose.yaml --profile sim-erforce config`
  - `docker compose -f docker/scenario/docker-compose.yaml config`
  - `docker compose -f docker/scenario/docker-compose.local.yaml config`
  - `docker compose -f docker/match-vs-tigers/docker-compose.yaml config`
  - `find /home/ibis/ibis_ws/src/crane/scripts -maxdepth 3 -type f | sort`
  - `sed -n '1,240p' /home/ibis/ibis_ws/src/crane/scripts/docker-dev.sh`
  - `sed -n '1,160p' /home/ibis/ibis_ws/src/crane/scripts/match-vs-tigers/run_local.sh`
  - `sed -n '1,200p' /home/ibis/ibis_ws/src/crane/scripts/scenario_test/run_test.sh`
  - `sed -n '1,180p' /home/ibis/ibis_ws/src/crane/scripts/scenario_test/build_docker.sh`
  - `sed -n '1,180p' /home/ibis/ibis_ws/src/crane/scripts/scenario_test/setup_env.sh`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `/home/ibis/ssl/IbisDuck/reports/tracker-064-er-force-docker-assets-investigation-20260514133853.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tasks-status.md`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/phases-status.md`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/README.md`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/dev/README.md`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/dev/docker-compose.yaml`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/dev/config -> ../config`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/dev/ssl-vision-client-config.json -> ../ssl-vision-client-config.json`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/ssl-vision-client-config.json`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/config/ssl-game-controller-sim.yaml`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/config/ssl-game-controller.yaml`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/config/engine.yaml`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/config/ssl-simulation-controller/robot-specs.yaml`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/scenario/README.md`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/scenario/docker-compose.yaml`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/scenario/docker-compose.local.yaml`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/scenario/Dockerfile`
  - 確認: `/home/ibis/ibis_ws/src/crane/scripts/docker-dev.sh`
  - 確認: `/home/ibis/ibis_ws/src/crane/scripts/scenario_test/run_test.sh`
  - 確認: `/home/ibis/ibis_ws/src/crane/scripts/scenario_test/build_docker.sh`
  - 確認: `/home/ibis/ibis_ws/src/crane/scripts/scenario_test/setup_env.sh`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/match-vs-tigers/README.md`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/match-vs-tigers/docker-compose.yaml`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/match-vs-tigers/TESTING_NOTES.md`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/match-vs-tigers/config/ssl-game-controller-match.yaml`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/match-vs-tigers/config/simulation_protocol_fixed.xml`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/match-vs-tigers/scripts/start_grsim.sh`
  - 確認: `/home/ibis/ibis_ws/src/crane/docker/match-vs-tigers/scripts/match_controller_pb.py`
  - 確認: `/home/ibis/ibis_ws/src/crane/scripts/match-vs-tigers/run_local.sh`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/README.md`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/COMPILE.md`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/data/docker/README.md`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/data/docker/Dockerfile.simulatorcli`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/data/docker/simulatorcli_entrypoint.bash`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/core/include/core/sslprotocols.h`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/simulator/simulator.cpp`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_sim/ssl_simulation_control.proto`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_sim/ssl_simulation_robot_control.proto`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_vision/ssl_detection_tracked.proto`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/protobuf/protobuf/ssl_vision/ssl_wrapper_tracked.proto`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/amun/gamecontroller/sslvisiontracked.cpp`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/amun/gamecontroller/internalgamecontroller.cpp`
  - 確認: `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/Ref/framework/src/trackingreplaycli/trackingreplaycli.cpp`

## 指摘事項

- 指摘要約または「指摘なし」:
  - Blocking: `docker/scenario/docker-compose.yaml` は `CRANE_TAG` 未設定時に `ghcr.io/ibis-ssl/crane:` となる。Duck へ移植する場合、prebuilt crane image 前提を持ち込まない、または明示 tag / build 手順を固定する必要がある。
  - Blocking: `docker/dev/docker-compose.yaml` の ER-Force sim command は `./bin/simulator-cli`、`docker/scenario/*.yaml` と ER-Force framework の公式 `Dockerfile.simulatorcli` は `./build/bin/simulator-cli` 系で、同じ image 名でも layout 前提が揺れている。Duck 移植時は採用 image と command path を静的に検証する必要がある。
  - Blocking: `match-vs-tigers` は 3rd party tracker / Tigers Sumatra / log-recorder の参考資産だが、シミュレータが grSim 前提であり今回の normal path には採用しない。Tigers 関連は tracker endpoint と source 比較の参考に留める。
  - Non-blocking: `docker/match-vs-tigers/README.md` と `TESTING_NOTES.md` には `docker/match_vs_tigers` / `scripts/match_vs_tigers` という underscore path が残るが、実体は `docker/match-vs-tigers` / `scripts/match-vs-tigers`。README コマンド検証候補として保持する。
  - Non-blocking: ER-Force framework 追加 repo に Docker Compose は見つからず、Docker 資産は主に `data/docker/Dockerfile.simulatorcli` と `data/docker/README.md`。compose topology は Crane 側 `docker/dev` / `docker/scenario` を土台にするのが現実的。

## 結果

- 結果:
  - 関係する service / compose:
    - `docker/dev/docker-compose.yaml`: `erforce-sim`、`ssl-game-controller`、`autoref-erforce`、`ssl-log-recorder`、`ssl-vision-client` が E2E 差分評価に関係する。全体は `network_mode: host`。`sim-erforce` profile で ER-Force simulator と status board が有効になる。
    - `docker/scenario/docker-compose.yaml`: `erforce-sim`、`crane`、`autoref-tigers` の最小構成。CI / scenario test 用で `crane` image 前提がある。
    - `docker/scenario/docker-compose.local.yaml`: `erforce-sim` と `autoref-tigers` のみを Docker で起動し、crane はホスト実行する想定。Duck へ移植するなら、ibis tracker / Duck server をホスト側で動かす smoke に近い。
    - `docker/match-vs-tigers/docker-compose.yaml`: `autoref-tigers`、`tigers-blue`、`ssl-log-recorder`、`match-controller`、`ssl-game-controller` の参考実装。ただし `grsim` 前提のため今回の採用対象外。
  - 関係する Dockerfile / image:
    - Crane 側 ER-Force simulator image は `ghcr.io/ibis-ssl/framework-simulatorcli:latest`。
    - ER-Force framework 側公式 simulator image は `data/docker/Dockerfile.simulatorcli` で、`ubuntu:24.04` build stage から `make simulator-cli` し、runtime stage で `tini` と `simulatorcli_entrypoint.bash` を使う。Docker Hub image は `roboticserlangen/simulatorcli` と README に記載。
    - `docker/scenario/Dockerfile` は Crane workspace image 作成用で、Duck への最小移植には重い。
    - `docker/match-vs-tigers/Dockerfile.match-controller` は grSim 対戦制御用の proto 生成 / Python controller 用で、今回の ER-Force normal path には不要。
  - 関係する config:
    - `docker/config/ssl-game-controller-sim.yaml`: sim 用 network は referee `224.5.23.1:11003`、vision `224.5.23.2:10020`、tracker `224.5.23.2:11010`、`publish-nif: "lo"`。
    - `docker/config/ssl-game-controller.yaml`: real 用 network は referee `224.5.23.1:10003`、vision `224.5.23.2:10006`、tracker `224.5.23.2:10010`。
    - `docker/ssl-vision-client-config.json`: `visionPort: 10020`、`trackedPort: 11010`、`refereePort: 11003`、`simAddress: 127.0.0.1`。
    - `docker/config/engine.yaml`: autoRef config に `ER-Force` と `TIGERs AutoRef` があり、`activeTrackerSource` を持つ。差分評価自体の真値ではなく GC / AutoRef 周辺設定。
    - `docker/config/ssl-simulation-controller/robot-specs.yaml`: `custom_erforce` を含む robot specs。ER-Force framework 側 `src/protobuf/protobuf/ssl_sim/ssl_simulation_custom_erforce_robot_spec.proto` と対応する設定資産。
  - 関係する script / README:
    - `scripts/docker-dev.sh`: 既定 `--sim erforce`、`docker/dev/docker-compose.yaml`、`sim-erforce` profile、実機時だけ `VISION_PORT=10006 REFEREE_PORT=10003 TRACKER_PORT=10010` に切替。`up/build/create` では ball-calibration proto sync を実行するため、Duck 移植ではこの script 全体ではなく compose config 値だけを抜くのがよい。
    - `scripts/scenario_test/run_test.sh`: 既定 `USE_LOCAL=1` で `docker/scenario/docker-compose.local.yaml` を使い、crane はホスト workspace から `ros2 launch`。ER-Force sim + 3rd party tracker + ホスト側実装という topology 参考になるが、pytest / ROS / ssl-log-recorder download が混ざるため移植対象外。
    - `scripts/match-vs-tigers/run_local.sh`: Tigers 対戦全体の起動 wrapper。grSim 前提かつ interactive cleanup を含むため移植対象外。
    - `docker/dev/README.md` と `docker/README.md`: ER-Force sim の起動例、port、service 一覧を持つ。`docker/match-vs-tigers/README.md` は path 表記に古い underscore が残るため参考扱い。
  - ER-Force framework repo からの追加情報:
    - `README.md` は `simulator-cli` が SSL simulation protocol の robot command を受け、SSL vision protocol で world state を broadcast し、simulated vision は port `10020` を使うと説明している。
    - `src/core/include/core/sslprotocols.h` は標準値として vision `224.5.23.2:10006`、sim vision `10020`、tracker `224.5.23.2:10010`、simulation control `10300`、blue robot control `10301`、yellow robot control `10302` を定義している。
    - `src/simulator/simulator.cpp` は `SSL_SIMULATION_CONTROL_PORT` / blue / yellow robot control port に UDP bind し、sim vision を `SSL_SIMULATED_VISION_PORT` から multicast する。CLI option は `-g` / `--geometry`、`--realism`、`--localhost`。
    - `src/protobuf/protobuf/ssl_sim/ssl_simulation_control.proto` は `TeleportBall`、`TeleportRobot`、`SimulatorControl`、`SimulatorCommand` を定義し、ボール / ロボット静止・直線運動・ID 近接の初期 scenario 制御に使える。
    - `src/protobuf/protobuf/ssl_sim/ssl_simulation_robot_control.proto` は wheel / local / global velocity の robot control を定義する。移動継続入力を作る場合の protocol 候補。
    - `src/protobuf/protobuf/ssl_vision/ssl_wrapper_tracked.proto` と `ssl_detection_tracked.proto` は tracker source UUID / source_name / tracked_frame / tracked ball / tracked robot の wire format で、Duck 側 CaptureOn の external tracker sidecar との対応確認に有用。
    - `src/amun/gamecontroller/sslvisiontracked.cpp` は ER-Force 内部 world state から `TrackerWrapperPacket` を作る処理があるが、`internalgamecontroller.cpp` 経由で GC CI input に入れる用途に見える。独立した UDP tracker publisher として使えるかは未確認。
    - framework repo 内に Docker Compose は見つからなかった。
  - grSim 対象外候補:
    - `docker/dev/docker-compose.yaml` の `grsim` service、`docker/dev/grsim.xml`、`docker/match-vs-tigers/*` の `grsim` service / `grsim_config.xml` / `start_grsim.sh` / `proto/grsim` は存在するが、今回の grSim 不採用方針により採用しない。
  - 同じ ER-Force simulator 入力を ibis tracker と他 tracker に供給できそうな Docker topology:
    - 最小候補は `network_mode: host` で `erforce-sim` を起動し、sim vision `224.5.23.2:10020` を Duck の raw vision receiver / ibis tracker と 3rd party tracker の両方に購読させる。
    - 3rd party tracker 候補は `autoref-erforce` または `autoref-tigers` の tracker output。Crane compose では `autoref-erforce --vision-port 10020 --tracker-port 11010 --gc-port 11003`、Tigers では `--visionAddress 224.5.23.2:10020 --refereeAddress 224.5.23.1:10003/11003 --trackerAddress 224.5.23.2:11010`。
    - Duck 側は own ibis tracker output と external tracker `224.5.23.2:11010` を CaptureOn sidecar に保存し、source UUID / source_name で分離する。必要なら Duck own tracker publish endpoint と external tracker endpoint の衝突を避けるため、Duck own と external を別 port にするか、同一 multicast port で source identity を必ず見る。
    - referee / GC を入れる場合は sim profile に合わせて `224.5.23.1:11003` を使う。scenario compose は crane launch が `referee_port:=10003` なのに ER-Force sim は `--ibis-referee-port 11003` で、移植時は 10003 / 11003 の混在を解消する必要がある。
  - port / multicast / volume / network / image 前提:
    - ER-Force sim vision: `224.5.23.2:10020`。
    - 3rd party tracker output: Crane sim compose では `224.5.23.2:11010`。ER-Force framework 標準定数は tracker `10010` なので、Crane sim の 11010 は tournament conflict 回避用の上書きと見るべき。
    - referee sim: `224.5.23.1:11003`。real / standard は `10003`。
    - simulator protocol: control `10300`、blue robot command `10301`、yellow robot command `10302`。
    - network は relevant compose がほぼ `network_mode: host`。multicast を host loopback / interface で扱う前提。Docker bridge 前提の README 記述は実 compose と一致しない箇所がある。
    - volumes は dev で `docker/dev/config -> ../config`、`docker/dev/ssl-vision-client-config.json -> ../ssl-vision-client-config.json`、`../../:/logs`、match で `./ssl-logs:/logs` / `./results:/app/results` / Sumatra config mount。Duck 最小構成には log output volume 以外は不要。
    - prebuilt image は `ghcr.io/ibis-ssl/framework-simulatorcli:latest`、`roboticserlangen/autoref:2025.1.0`、`tigersmannheim/auto-referee:1.2.0`、`robocupssl/ssl-log-recorder:latest`、`robocupssl/ssl-game-controller:latest`。framework 公式 simulator image は `roboticserlangen/simulatorcli`。
  - Duck repo へ移植する最小候補:
    - ER-Force sim service 定義。ただし image / command path を `ghcr.io/ibis-ssl/framework-simulatorcli:latest + ./build/bin/simulator-cli` または `roboticserlangen/simulatorcli + entrypoint` のどちらかへ固定して検証する。
    - 3rd party tracker service はまず `autoref-erforce` か `autoref-tigers` のどちらか 1 つに絞り、vision `10020`、tracker output `11010`、referee `11003` を明示する。
    - `ssl-log-recorder` は静的 / E2E evidence 用に有用だが、Duck CaptureOn が主経路なら必須ではない。比較ログの外部証跡用に optional とする。
    - `ssl-game-controller-sim.yaml` の network 値、`ssl-vision-client-config.json` の port 値、framework の `ssl_sim/*.proto` / `ssl_vision/*tracked*.proto` を参照資料として取り込む候補。
    - scenario control 用には `ssl_simulation_control.proto` の `TeleportBall` / `TeleportRobot` を使う小さな送信 script を新規作成するほうが、Crane の scenario / match wrapper を移植するより小さい。
  - 移植対象外にすべきもの:
    - grSim service / config / proto / start script。
    - `docker/match-vs-tigers` 全体の対戦制御、Sumatra AI、match-controller、README の古い path。Tigers tracker endpoint の引数だけ参考にする。
    - Crane workspace image build (`docker/scenario/Dockerfile`、`scripts/scenario_test/build_docker.sh`)。
    - `web-debugger`、`ball-calibration`、`robot-manager`、`voicevox`、ROS scenario pytest wrapper、ball calibration proto sync。
    - ER-Force framework の CI / V8 / robocup setup Dockerfile 群。`Dockerfile.simulatorcli` 以外は Duck E2E 差分評価には重い。
  - 静的検証結果 / 候補:
    - 実行済み: `docker compose -f docker/dev/docker-compose.yaml --profile sim-erforce config` は成功し、ER-Force sim / autoref-erforce / ssl-log-recorder の port 展開を確認した。
    - 実行済み: `docker compose -f docker/scenario/docker-compose.yaml config` は成功したが、`CRANE_TAG` 未設定 warning と `ghcr.io/ibis-ssl/crane:` を確認した。
    - 実行済み: `docker compose -f docker/scenario/docker-compose.local.yaml config` は成功し、ER-Force sim + autoref-tigers の host network 構成を確認した。
    - 実行済み: `docker compose -f docker/match-vs-tigers/docker-compose.yaml config` は成功し、Tigers / autoref / log-recorder の endpoint を確認した。ただし grSim 前提のため採用対象外。
    - 実行済み: path 検証で `docker/dev/config` と `docker/dev/ssl-vision-client-config.json` は symlink として存在。`docker/scenario/run_scenario_test_local.sh`、`scripts/match_vs_tigers/run_local.sh`、`docker/match_vs_tigers` は存在せず、README の一部 command は修正候補。
    - 候補: Duck 移植後に `docker compose -f <移植compose> config`、bind mount source の `test -e`、image command path の短時間 `docker run --rm <image> --help` 相当を実施する。ただし今回タスクでは image pull / build / 起動は未実施。

## リスク

- 未解決のリスクまたは後続対応:
  - ER-Force simulator が「観測ノイズなし真値」を外部へ直接出す Docker / CLI option は、今回読んだ Docker / README / proto / simulator source からは確認できなかった。`ssl_simulation_control.proto` で入力真値は制御できるが、出力真値は simulator 内部 world state を改造して出すか、ノイズなし設定の vision を fallback とする検討が必要。
  - ER-Force framework の `SSLVisionTracked` は内部 world state から tracked packet を作れるが、独立した 3rd party tracker UDP publisher として利用できる経路は未確認。ER-Force tracker fallback として使うなら追加調査が必要。
  - `autoref-erforce` / `autoref-tigers` が tracker packet を必ず `224.5.23.2:11010` に出すかは compose 引数上の推定で、実起動確認はしていない。
  - `ghcr.io/ibis-ssl/framework-simulatorcli:latest` の binary path は compose 間で `./bin/simulator-cli` と `./build/bin/simulator-cli` が揺れており、実 image の layout 確認が必要。
  - `network_mode: host` と multicast port を使うため、Duck 側の既存 diagnostics / CaptureOn / Tracker receiver と同一 port を取り合うリスクがある。特に 10003/11003、10010/11010 の sim/real 差分を明示設定にする必要がある。
  - Tigers 関連資産は README と compose の network 記述が一致せず、path 表記も古い。採用範囲を endpoint 参考に限定しないと移植コストが膨らむ。
  - Docker image build、pull、長時間起動、runtime packet 受信は制約により未実施。今回の結論は静的検証ベース。
