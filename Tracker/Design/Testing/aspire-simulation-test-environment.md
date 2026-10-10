# Aspire シミュレーション試験環境の設計

## 目的

GitHub課題 #18 のAspire対応として、シミュレーション試験に必要な複数のプロセスを一度の操作で起動できるようにする。

初期対象は次の三つとする。

- ER-Forceの`simulator-cli`を実行するシミュレータ。
- `ibis-ssl/crane` のロボット制御プログラム。
- Duck の `Tracker.RuntimeHost`。

シミュレータとCraneはDockerコンテナとして起動し、Duckは開発中のプログラムをそのままデバッグできるよう、ホスト上の.NETプロセスとして起動する。

本設計は手元での開発と手動試験を簡単に始めるためのものであり、本番の配置方法は定義しない。

## 現状

`Testing/Duck.Testing.AppHost`は実装済みである。`duck`は`Tracker.RuntimeHost`の`ProjectResource`として登録し、`simulator`、`game-controller`、`crane`、必要な場合の`cm4-sim`は、Docker上で動かす各起動対象の管理プログラムとしてAspireの`ExecutableResource`に登録している。このため、旧方式の`ContainerResource`は現行実装では使っていない。「AppHostは存在しない」は設計開始時の説明である。

初期のAppHost実装では`AddContainer`と`WithContainerRuntimeArgs("--network", "host")`相当の指定でホストネットワークを使っていた。当初の設計に沿った実装だったが、Aspire AppHost SDK 13.5.4 / DCP 0.25.13でGitHub Actionsを実行した際、DCPが利用者定義ネットワークを割り当てた後に、Dockerへホストネットワークの指定も渡していた。その結果、Dockerは`cannot attach both user-defined and non-user-defined network-modes`によりコンテナ作成を拒否した。この記述は初期実装の履歴であり、現在の実装や最終的な受入方式として扱わない。

本書では、起動対象ごとのAspire外部実行資源（現行実装では`ExecutableResource`）から起動管理プログラムを立ち上げ、固定版のDockerコンテナを`--network host`で起動する方式を扱う。Duckは引き続き`AddProject<Projects.Tracker_RuntimeHost>`とする。旧方式の`ContainerResource`は現行実装では使っていない。現行方式の実通信による受入は未完了である。

`Tracker.RuntimeHost` は `VisionReceiver` 設定から SSL-Vision の UDP 入力を受け、既定の `sim` 設定では `224.5.23.2:10020` を受信する。
ER-Forceの`simulator-cli`はSSLの模擬通信規約による制御入力を受け、SSL-Visionの状態をUDP 10020へ送る。通常の送信先は`224.5.23.2`で、`--localhost`を指定すると`127.0.0.1`になる。

SSLの模擬通信規約で使う既定のUDPポートは次のとおりである。

- シミュレーション制御: UDP 10300。
- 青チーム制御: UDP 10301。
- 黄チーム制御: UDP 10302。


## 基本方針

Aspireは試験対象を一括起動して状態を管理するために使う。`Tracker.RuntimeHost`の実行責務は同プロジェクトに置き、AppHostへ移さない。

AppHost は `Testing/Duck.Testing.AppHost` に置く。Duck は既存の `Duck.slnx` に含まれるため、C# のプロジェクト型 AppHost とし、`ProjectReference` と `AddProject<Projects.Tracker_RuntimeHost>` で起動する。

`AddDotnetProject`で場所を指定する方法は、現行Aspireでは試験的なAPIのため初期実装で採用しない。

シミュレータ、Game Controller、Crane、cm4-simは、サービスごとに分けたAspire外部実行資源から起動管理プログラムを立ち上げ、それぞれが固定版のDockerコンテナを所有する。CraneはDuck側でビルドせず、`ibis-ssl/crane`がGitHubで公開するコンテナイメージを、`ghcr.io/ibis-ssl/crane`の`scenario-`にコミットのハッシュ値を続けた版識別子で取得する。`scenario-develop`は動作確認に使う更新可能な識別子であり、利用者が明示した場合だけ使う。再現可能な試験では、対象コミットのハッシュ値に対応する版識別子を固定する。

## 起動対象の構成

AppHostでは三つの主要資源に加え、審判情報と試合状態を供給する試験資源を一つ、Craneの現在のシミュレーション経路を維持する場合は補助資源を一つ管理する。

| 起動対象名 | 実行形態 | 責務 |
| --- | --- | --- |
| `simulator` | Aspire外部実行資源＋Dockerコンテナ | Craneの現在の模擬環境構成と同じ`ghcr.io/ibis-ssl/framework-simulatorcli:<tag>`からER-Forceの`simulator-cli`を起動し、物理シミュレーションとSSL-Visionデータの出力を行う。 |
| `game-controller` | Aspire外部実行資源＋Dockerコンテナ | `robocupssl/ssl-game-controller:<fixed tag or digest>`をホストネットワークで起動する。各モードで`224.5.23.1:11003`の審判情報を生成する唯一の送信元とし、制御APIには`127.0.0.1:8082`を使う。 |
| `crane` | Aspire外部実行資源＋Dockerコンテナ | `ghcr.io/ibis-ssl/crane:scenario-<対象コミットのハッシュ値>`からCraneを起動し、ロボット制御指令を生成する。 |
| `cm4-sim` | Aspire外部実行資源＋Dockerコンテナ | `ghcr.io/ibis-ssl/orion-cm4-sim:<commit SHA>`を使い、Craneの`visibility_graph`が出すモード4の位置指令をモード3の速度指令へ変換する。 |
| `duck` | .NET プロセス | `Tracker.RuntimeHost` を `sim` 設定で起動し、SSL-Vision を追跡してトラッカーパケットを出力する。 |

Aspire管理画面では、AppHostが管理する各起動管理プログラムの状態と標準出力・標準エラーを確認する。DCPはDockerコンテナを直接管理しないため、コンテナの状態・ID・最終ログは起動管理プログラムの記録と受入証跡に残す。この方式ではDCPにコンテナの詳細は表示されない。

## ネットワーク方針

通信要件はホストネットワークを前提とする。サービスごとの起動管理プログラムがDocker CLIに`--network host`を指定してコンテナを起動する。Aspireの`AddContainer`に実行引数としてホストネットワークを加える方式は初期実装の履歴であり、AspireとDCPが利用者定義ネットワークも同時に指定してコンテナ作成に失敗したため、現行の方式では使わない。

理由は、ER-Forceの`simulator-cli`がSSL-Visionの送信先を任意に指定する起動引数を持たず、通常のマルチキャスト送信か`127.0.0.1`への送信だけを選べるためである。

シミュレータを通常のDockerブリッジネットワークに置いた場合、`--localhost`はコンテナ自身を指し、ホストで動くDuckには届かない。ブリッジネットワークとホスト間でマルチキャストを透過させる構成にも依存しない。

そのため、`simulator`、`game-controller`、`crane`、`cm4-sim`はDockerのホストネットワークで起動し、Duckは従来どおりホスト上でSSL-Visionのマルチキャストを受信する。起動管理プログラムはサービスごとに分け、それぞれ一つのコンテナだけを所有する。Duckのプロジェクト資源とAppHostの多重起動防止ロックは維持する。
LinuxのDocker Engineでは、ホストネットワークを使うコンテナがホストと同じネットワーク名前空間を共有するため、UDPポートの公開や変換を挟まずに通信できる。

Docker Desktopを使う場合は、ホストネットワークを有効にする。初期受入環境はLinuxのDocker Engineとし、Docker Desktopでの動作は別途確認する。

ホストネットワークではSSL-Visionとトラッカーマルチキャスト、および採用するシミュレータ構成の制御ポートをホスト全体で共有する。ポートは用途ごとに分ける。`10020`と`11010`は複数の受信側が`SO_REUSEADDR`を使って同時に受信する共有マルチキャストポートとする。一方、UDP 10300 / 10301 / 10302のうち構成で使う待受ポートと、Crane経路のUDP 12345 / 12346は、一つの起動構成が占有する制御ポートとする。`10020` / `11010`で単に待受を開始できるか、ポートが使用中かどうかを二重起動の判定には使わない。

同じホスト上でDuckのAspire構成を二つ起動しないよう、AppHostは資源の起動前に取得し、終了まで保持するホスト内の排他ロックで二重起動を拒否する。初期実装では一意なロックファイルを`FileShare.None`で開き、そのハンドルを保持する。ファイルが存在するだけでは失敗としない。二つ目のAppHostがロックを取得できない場合は資源を起動せず、明示的に失敗する。別プロセスとの競合確認は占有型制御ポートだけを対象とし、共有マルチキャストポートは実パケットの複数受信試験で検証する。Aspireの`--isolated`を指定しても、ロックと固定UDPポートは分離されない。

Craneの現行構成では、`visibility_graph`を使うと、`crane`がモード4の位置指令をUDP 12345へ送り、`cm4-sim`がその指令をモード3の速度指令に変換してUDP 12346へ転送する。既存のCraneコンテナを現在の仕様のまま使う初期構成では、この経路を汎用のSSL模擬通信規約で使う10301 / 10302への直接送信に置き換えない。

ホストネットワーク使用時はDockerの`-p`によるポート公開を併用しない。ホスト上の起動管理プログラムは、引数配列を直接渡してDocker CLIを起動し、子プロセスを監視する。ホスト側ではシェルや`bash -c`を介さない。Craneの起動例にある`bash -c`は、Craneコンテナ内でROS環境を読み込んで起動するコマンドであり、ホスト側の起動管理プログラムでシェルを使う根拠にはならない。実装時はCraneイメージの起動時に実行するプログラムと引数の仕様を確認する。

### 通信経路

```text
Craneコンテナ
  │ モード4 / UDP 12345
  ▼
cm4-simコンテナ
  │ モード3 / UDP 12346
  ▼
ER-Forceシミュレータコンテナ
  │ SSL-Vision UDP 224.5.23.2:10020
  ▼
Tracker.RuntimeHost（ホスト上のプロセス）
  │ TrackerWrapperPacket UDP
  ▼
試験用の確認先
```

シミュレーション制御を行う試験ツールが必要な場合はUDP 10300を使う。初期AppHost自体は自動で試験の進行手順を作らないが、`ASPIRE-005` / `ASPIRE-NET-007`の動作確認では専用の`referee-driver`設定を使う。`referee-driver`は`224.5.23.1:11003`へ審判情報を直接送らず、`game-controller`の`ws://127.0.0.1:8082/api/control`へ試合続行指令を送る。起動世代ごとに方式固有の初期指令（基本・比較方式は`HALT`、対戦方式は`STOP`）を確認した後に`NEXT_COMMAND`を送り、遷移後も`HALT` / `STOP`なら`FORCE_START`、それ以外の準備状態なら`NORMAL_START`を送る。11003で試合進行中の指令への遷移を確認してから、Craneの指令とSSL-Visionの位置変化を検査する。試合進行中へ遷移した後も、初期指令と一致しないことだけを理由に試合管理機能の準備完了が解除されず、通信とAPIの正常性が保たれることを確認する。

## Duck の起動

`duck`は`Tracker.RuntimeHost`のプロジェクト資源として起動する。
試験用の既定値には既存の`sim`設定を使い、少なくとも次の値を明示する。

- `Tracker:ActiveProfileName=sim`。
- `VisionReceiver:MulticastAddress=224.5.23.2`。
- `VisionReceiver:Port=10020`。

製品用の`appsettings.json`に試験環境固有のコンテナ情報を追加しない。AppHostから環境変数で上書きする。

DuckをDockerコンテナへ移行することは初期範囲に含めない。これにより、ブレークポイントを使う通常の.NETデバッグ経路を維持する。

## 模擬環境の構成

シミュレータには、Craneの現在の模擬環境構成ですでに使われている`ghcr.io/ibis-ssl/framework-simulatorcli:<tag>`を使う。Duck側ではER-Forceの模擬環境実装を複製してビルドしない。

シミュレータのイメージの版識別子はAppHostの設定に明記し、再現可能な試験では固定した版識別子を使う。起動引数はCraneの現在の模擬環境構成に合わせる。

```sh
./bin/simulator-cli -g 2020B --realism None --ibis-port 12346 --ibis-team-color yellow
```

AppHostでは少なくともイメージの版識別子、フィールド形状、物理特性、`IBIS_PORT`、`IBIS_TEAM_COLOR`を設定できるようにする。初期値はCraneの現在の模擬環境構成と同じく、フィールド形状を`2020B`、物理特性を`None`、`IBIS_PORT`を`12346`、チーム色を`yellow`とし、外部の既定値が変わっても試験結果が変わらないよう各値を明示する。
## Crane の構成

人工知能には`ibis-ssl/crane`の既存Dockerイメージを使う。Duck側でCraneのプログラム原本を複製してビルドする方式や、旧`ibis-ssl/crane_docker`のROS 2 Foxyを基盤にする構成は採用しない。

`ibis-ssl/crane`の現行`docker/Dockerfile`はROS 2 Jazzyを基盤とし、`scenario`をビルド対象としてCraneを構築する。Craneの`docker build`処理では、GitHubで次の形式の識別子を付けたコンテナイメージを公開する。

- `ghcr.io/ibis-ssl/crane:scenario-<対象コミットのハッシュ値>`: 再現可能な試験で使う固定した版識別子。
- `ghcr.io/ibis-ssl/crane:scenario-develop`: 開発中の確認に使う更新可能な識別子。

AppHostの既定設定では固定した版識別子を要求する。`scenario-develop`は利用者が明示した場合だけ使う。指定した識別子のイメージが保管先に存在しない場合、別の識別子へ暗黙に切り替えず起動を失敗させる。

Crane の起動コマンドは現在の構成を基準にする。

```sh
bash -c "source /root/ibis_ws/install/setup.bash && ros2 launch crane_bringup crane.launch.xml sim:=true speak:=false team:=Yellow planner:=${PLANNER}"
```

`team`と`planner`はAppHostの設定から変更できるようにする。既定の経路計画方式はCraneの現行構成と同じ`visibility_graph`とし、その場合は`cm4-sim`も起動する。Crane、`cm4-sim`、模擬環境はROS 2 / UDP / マルチキャストの通信要件を満たすため、Dockerを動かす計算機のネットワークを共有する。

## 審判情報と試合状態の送信元

`224.5.23.1:11003`の審判指令は、通常方式、比較方式、対戦方式のいずれでも`game-controller`だけが生成する。11003は複数の受信側が受け取るマルチキャストのUDPポートであり、受信側がそのポートを使えるかどうかで送信元の一意性を判定しない。AppHostでは、11003への送信元を`game-controller`一つに限定する。

通常方式では`game-controller`だけを審判情報の送信元とする。比較方式でも同じ送信元を使う。`tracker-tigers`はSumatraの審判機能を`source=NETWORK`、`port=11003`、`gameController=false`、`publishRefereeMessages=false`に固定し、外部の試合管理機能から審判情報を受け取る設定で起動する。標準の`simulation_protocol.xml`のように内蔵の試合管理機能を有効にする構成は使わない。`tracker-erforce`の`--gc-port 11003`も受信側として扱い、審判情報を送信させない。

対戦方式も同じ`game-controller`を使う。`tigers-blue`はDuck側で固定する対戦用`simulation_protocol_fixed.xml`相当の設定により`source=NETWORK`、`port=11003`、`gameController=false`とする。`autoref-tigers`は審判情報の受信側として動作し、試合管理機能のAPIを呼び出す。対戦を進行させる`match-controller`もAPIを呼び出すだけとし、11003へ直接送信しない。

この起動構成では`game-controller`が制御API`127.0.0.1:8082`を占有する。通常方式と比較方式の`referee-driver`はAPIを呼び出すだけとし、11003の送信元にはしない。これらのロボット動作試験では、初期審判指令を`HALT`に固定する。対戦方式ではCraneの現行構成を基準に、青チームを`TIGERs Mannheim`、黄チームを`ibis`、初期指令を`STOP`とする別の設定を使う。`match-controller`はAPIを呼び出して試合を進行させる。再現可能な試験で使う試合管理機能のDockerイメージは、固定した版識別子またはダイジェストで指定する。

## 準備完了と起動順序

現行実装の`WaitForStart`は起動対象の起動だけを示し、通信や各対象の準備完了までは示さない。移行後は各起動管理プログラムが`/health/live`と`/health/ready`を提供する。Dockerコンテナが起動しただけでは準備完了とせず、各対象の確認条件を満たした場合に限り成功とする。AppHostは、依存先の準備完了を待つ起動対象に対して、正常性確認付きの`WaitFor`を使う。`WithReference`は接続情報の参照に限る。

初期の準備完了条件は次のとおり。

| 起動対象名 | 準備完了条件 |
| --- | --- |
| `simulator` | 管理対象のDockerコンテナが稼働し、起動管理プログラムが`224.5.23.2:10020`のSSL-Vision検出パケットを受信して復号できる。継続受信とロボットの移動は一括受入試験で確認する。 |
| `game-controller` | 起動世代ごとに方式固有の初期状態を検証する。初期化中は`base` / `comparison`の`HALT`、`match`の`STOP`を11003で確認するまで未準備とする。確認後は有効な審判指令の遷移を許容し、初期指令との一致を継続的な準備条件にしない。各正常性確認では所有者情報が一致するDockerコンテナ、試合管理機能のプロセスとAPIの応答、期限内に復号できる既知の審判指令を確かめる。通信断、API不通、未知指令、プロセスまたはコンテナの終了は未準備とする。 |
| `tracker-tigers` | 所有者ラベル、起動対象名、期待するDockerイメージ、ホストとの共有ネットワーク、完全なコンテナID、稼働状態を確認する。コンテナ内のSumatraプロセスと起動完了診断に加え、比較実行開始後に`224.5.23.2:11010`で受信した、期待する送信元識別子を持つ復号可能なトラッカーパケットを確認する。コンテナが起動しただけでは準備完了としない。 |
| `tracker-erforce` | 所有者ラベル、起動対象名、期待するDockerイメージ、ホストとの共有ネットワーク、完全なコンテナID、稼働状態を確認する。コンテナ内のER-Forceトラッカープロセスと起動完了診断に加え、比較実行開始後に`224.5.23.2:11010`で受信した、期待する送信元識別子を持つ復号可能なトラッカーパケットを確認する。コンテナが起動しただけでは準備完了としない。 |
| `debug-host` | `Tracker.DebugHost`の.NETプロジェクト資源として起動する。Duckの起動通知と両外部トラッカーの準備完了を待つ。比較用正常性確認でDuck、TIGERs、ER-Forceの三つの役割がそれぞれ別の送信元識別子に対応し、同じ確認時間内に三者の新しいトラッカーパケットを受信した場合に準備完了とする。 |
| `cm4-sim` | 所有コンテナが稼働し、UDP 12345の待受ソケットが当該コンテナ内のプロセスに属すると確認できる。ポートが使用中というだけでは準備完了にしない。 |
| `crane` | 所有コンテナが稼働し、準備完了後に一覧取得を実行して終了値0となり、`/session_controller`の行が完全一致する。指令と移動は受入試験で別途確認する。 |
| `tigers-blue` | 所有コンテナが稼働し、Sumatraの起動完了記録と、この実行で生成したトラッカーパケットを`224.5.23.2:11010`で復号した結果を確認する。 |
| `autoref-tigers` | 所有コンテナが稼働し、AutoRef自身の診断が起動完了を示す。また、この実行で受信・復号したビジョン情報`10020`、審判情報`11003`、トラッカー情報`11010`の各入力数が増えたことを確認する。外部でマルチキャストパケットを観測しただけでは代用しない。イメージに受信証跡がなければ、確認用アダプターを実装するまで準備完了にしない。 |
| `ssl-log-recorder` | 所有コンテナが稼働し、この実行で作成したログファイルがあり、後続の読み取り処理でビジョン情報、審判情報、トラッカー情報の各記録を少なくとも一件ずつ復号できる。 |

固定版 `4063cd31cd5b11b1cc919003907f5f4c527b252d` の起動クラスは `session_controller` を登録する（[クラス定義](https://github.com/ibis-ssl/crane/blob/4063cd31cd5b11b1cc919003907f5f4c527b252d/crane_session_coordinator/src/crane_session_coordinator.cpp#L21-L22)）。[起動定義](https://github.com/ibis-ssl/crane/blob/4063cd31cd5b11b1cc919003907f5f4c527b252d/crane_bringup/launch/crane.launch.xml#L104) では名称を変更していない。一覧との照合には、パッケージ名や実行ファイル名を使わない。

準備確認では、ROSの準備完了を示す固定行`DUCK_CRANE_ROS_SETUP_OK`と、その後に実行する`ros2 node list`の出力を分けて扱う。終了値、経過時間、試行番号、固定行の有無、対象行との一致、および秘密情報を除いた出力抜粋を記録する。抜粋は各512文字までとし、対象行との照合には切り詰める前の出力全体を使う。終了値124は期限超過、137は強制終了として記録する。ただし、137だけを根拠に期限超過が原因とは断定しない。実行中の試行には終了値を記録しない。

診断記録ファイルの保存先はAppHostの`Testing:Crane:DiagnosticsPath`設定で指定する。環境変数で指定する場合は`Testing__Crane__DiagnosticsPath`を使う。AppHostは設定値を絶対パスに解決し、`CraneDiagnosticsPath`としてCrane用Docker起動管理プログラムの起動引数へ渡す。起動管理プログラムはホスト上で診断記録ファイルを作成する。この設定をCraneコンテナの環境変数には渡さない。確認の開始、完了結果、終了時の集計を専用のJSON形式の記録ファイルへ一行ずつ追記し、各行の書き込みを完了する。書き込みに失敗した場合は固定文言のエラーを記録する。失敗しても準備判定、再試行、所有するコンテナの後始末を続ける。

一括試験の保存先は `artifacts/aspire-full-stack/crane-probe.jsonl` とする。標準エラーを間接的に採取する経路だけには頼らない。後始末後、記録を解析して秘密情報を除き、一行一記録の形式で保存し直す。解析または秘匿処理に失敗した場合は、成果物を外部へ送信しない。保存先がない場合、完了済み試行の記録がない場合、実行中に中断した場合を区別し、いずれも成功や受信件数0として扱わない。

確認処理の上限10秒、終了要求から強制終了までの2秒、一括開始待ち300秒、60秒以内にSSL-Visionの検出データで黄チームのCraneロボットの位置が100 mm以上変化することを確認する条件は変えない。準備完了後、審判指令の遷移を確認する。Craneの指令をcm4-sim経由でシミュレータに渡す構成で、受入試験がSSL-Visionの検出データから黄チームのCraneロボットの位置変化を観測する。続いて、Duckが出力したトラッカーパケットを受入試験が受信できることを確かめる。公式の試験手順とは審判情報の送信元が異なり、本構成では`game-controller`だけが送信する。

各確認には有限の期限を設ける。期限超過、コンテナ終了、復号失敗では準備未完了のまま失敗し、失敗条件と調査ログを記録する。起動管理プログラムは、各正常性確認でコンテナ情報を調べ、所有者ラベル、起動対象名、期待イメージ、ホストネットワーク、コンテナID、稼働状態をすべて照合する。コンテナ内のプロセスやサービス診断、新しいパケットはサービス準備の証拠とし、所有権の証拠にはこの照合を使う。外部でパケットを受信しただけでは、送信コンテナの所有証明に代えられない。マルチキャストの確認では、選択したIPv4インターフェースからマルチキャストを受信できるよう参加設定を行い、`SO_REUSEADDR`を使って受入試験側の受信を妨げない。待受ソケットの所有者を確認する方法は、初期対象のLinux上で実証する必要がある。実証できないOSでは確認条件を弱めず、未対応として明示する。

試合管理機能の準備確認は、起動世代ごとに段階を設ける。初期化中は方式固有の初期指令を一度観測するまで未準備とし、確認後は「初期状態確認済み」をその起動対象の世代で保持する。起動後は有効な既知の審判指令への遷移を許容し、`HALT` / `STOP`へ戻ることは要求しない。継続的な正常性確認では、所有コンテナと試合管理機能のプロセスが稼働し、APIが応答し、期限内に11003の既知指令を復号して受信できることを確かめる。未知の指令、審判通信またはAPI通信の断絶、プロセスまたはコンテナの終了は未準備とする。コンテナIDまたは起動時刻が変わる再起動は新しい世代とみなし、初期状態の確認記録を破棄する。新世代で方式固有の初期指令を再確認するまで、試合管理機能の準備完了を待つ後続の起動対象は開始しない。

既定の`visibility_graph`構成では、次の依存関係を使う。

1. `simulator`と`game-controller`は依存なしで起動し、並行して準備完了条件を満たす。
2. `cm4-sim` と `duck` はそれぞれ準備完了した `simulator` を待つ。その後は並行して起動できる。
3. `crane`は準備完了した`cm4-sim`と`game-controller`、およびDuckの起動通知を待つ。
4. 比較方式の`tracker-tigers`と`tracker-erforce`は、それぞれ個別の起動管理プログラムとして登録し、準備完了した`simulator`と`game-controller`を待つ。各起動対象は所有者情報の照合と、固有のプロセス確認またはパケット確認の両方を満たす。
5. 比較方式の`debug-host`は、Duckのプロジェクト起動通知と両トラッカーの準備完了を待つ。起動後も、DebugHostの比較用正常性確認でDuck、TIGERs、ER-Forceの三つの役割がそれぞれ異なる送信元識別子に対応し、確認時間内に新しいパケットを受信するまでは未準備とする。

`cm4-sim`を使わない経路を選んだ場合、`crane`は存在しない`cm4-sim`への依存を作らず、`duck`と`game-controller`の既定の依存関係だけを持つ。`duck`は準備完了した`simulator`を待つ。Duckの起動通知はプロジェクトの起動だけを示すため、サービス準備完了の根拠には使わない。

移行前の`WaitForStart`と、移行後の準備完了確認付き`WaitFor`を混同しない。構成試験では依存関係と起動対象ごとの正常性確認を検証し、起動管理プログラムの試験では各条件と期限超過時の動作を検証する。試合管理機能は起動時の初期状態確認、初期化後の審判指令の正常な遷移、通信断、プロセス終了、起動対象の再起動を別々の試験で検証する。実ネットワークでの受入では、実際のパケット、審判情報、ロボットの動きを別途確認する。

## コンテナ所有権と停止契約

AppHostは起動時に起動単位を識別するIDを生成し、各起動管理プログラムに個別の実行IDを渡す。コンテナはこれらの値、資源名、使用するDockerイメージ、生成した名前がすべて一致する場合だけ、その起動管理プログラムの所有物とみなす。起動には`docker run --detach --cidfile ... --name ... --label ... --network host ...`を使う。`--rm`、イメージ名による絞り込み、`*`を使った一括削除、起動単位のIDだけを使った広範な削除は行わず、取得済みのイメージも削除しない。

起動管理プログラムはDockerのコマンドとログ追跡処理を子プロセスとして直接起動し、標準出力と標準エラーを最後まで読み取る。取消時は子プロセスへの取消要求と強制終了、終了待ちを済ませてから後始末する。`docker run`がIDを返す前に取り消された場合、Dockerデーモン側で作成完了の反映が遅れる可能性を考慮し、所有者ラベルと資源ラベルを厳密に指定して期限付きで再検索する。期限までに作成状態を確定できない場合は後始末失敗として0以外の終了値を返し、残留の可能性を記録する。一度だけラベルを検索しただけで後始末完了とはしない。失敗を注入する試験には、作成完了の反映が遅れる場合も含める。

通常終了、起動失敗、準備完了期限超過、コンテナの予期しない終了では、準備完了を解除し、所有コンテナを停止する。状態、終了値、最終ログを収集した後、所有者情報を再検証した完全なコンテナIDだけを削除する。停止全体の期限はDCPの実行資源停止上限15秒より短くし、子プロセスの終了、`docker stop`、`docker rm`には個別の上限を設ける。後始末の遅延や失敗は記録し、成功扱いにしない。`SIGKILL`、ホスト障害、電源断、Dockerデーモン停止時の自動後始末は保証しない。その場合もラベルで残留コンテナの所有者を識別し、無関係なコンテナには触れない。

## 操作

開発者が覚える起動操作は一つにする。

```sh
aspire run --apphost Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj
```

停止はAspireの通常操作に従う。`SIGINT` / `SIGTERM`による通常停止では、各起動管理プログラムが上記期限内に所有コンテナを停止・削除し、Duckの子プロセスも終了する。強制終了、ホスト障害、Dockerデーモンと通信できない場合のコンテナ後始末は保証しない。残留コンテナは所有者ラベルで後から識別する。AppHostの管理画面には起動管理プログラムの正常性と転送ログを表示し、`docker inspect`の結果と最終状態を受入証跡に残す。

個別の起動対象の再起動とログ確認はAspireの管理画面から行える構成にする。

### 実行方式の選択

AppHost自身の設定`Testing:Mode`で起動構成を選ぶ。値は`base`、`comparison`、`match`の三つに限定し、未指定時は`base`とする。未知の値は起動対象を開始する前に設定エラーとして失敗させる。

- `base`: シミュレータ、試合管理機能、Crane、必要な`cm4-sim`、Duckを起動する。
- `comparison`: `base`に`tracker-tigers`、`tracker-erforce`、`debug-host`を追加する。
- `match`: 対戦用シミュレータ、試合管理機能の設定一式、Crane、`tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller`、Duckを起動する。`cm4-sim`、`tracker-tigers`、`tracker-erforce`、`debug-host`は起動しない。

`match`は`comparison`への追加機能ではなく、独立した構成とする。比較用トラッカーと対戦用Sumatraの人工知能を同時に起動して、11010やチーム制御の意味が混在するのを防ぐ。

## 設定の責務

AppHostが決めるのは「何を一緒に起動するか」と「試験環境でどの接続設定を使うか」だけとする。

追跡アルゴリズムの設定値、RuntimeHostの処理周期、正式な通信形式は、既存のDuck側設定と設計を正本とする。

コンテナのイメージ版、フィールド形状、物理特性、Craneのイメージの版識別子、チーム色、経路計画方式、`cm4-sim`のイメージの版識別子など、試験環境を再現するための値だけをAppHostの設定に置く。

秘密情報が必要になった場合はリポジトリへ直接保存せず、Aspireのパラメーターまたは利用者の手元設定から渡す。
## 実装単位

実装は次の順に分ける。

### `ASPIRE-002`: AppHost の骨格

`Testing/Duck.Testing.AppHost` を追加し、`duck` だけを `Tracker.RuntimeHost` として起動できる状態にする。

### `ASPIRE-003`: ER-Forceシミュレータのイメージ

Craneの現在の模擬環境構成と同じ`ghcr.io/ibis-ssl/framework-simulatorcli:<tag>`を使う。これは初期の`ContainerResource`方式にシミュレータのイメージ参照と起動契約を追加した作業を指し、ホストネットワーク用の実行引数で動作することを受け入れた意味ではない。移行後は`simulator`の起動管理プログラムが同じイメージをホストネットワークで起動し、UDP 10020のSSL-Visionがホスト上の受信処理へ届くことを確認する。

### `ASPIRE-004`: Craneイメージと制御経路

初期の構成では`ghcr.io/ibis-ssl/crane:scenario-<対象コミットのハッシュ値>`と`cm4-sim`を`ContainerResource`として登録し、構成試験で登録内容と接続関係を確認していた。現在のAppHostでは、両者を`AddDockerWrapper`で起動管理プログラムの`ExecutableResource`として登録し、Dockerコンテナを別プロセスで管理する。`visibility_graph`を使う指令経路は維持し、Craneのモード4指令を`cm4-sim`でシミュレータ向けのモード3指令へ変換する。

### `ASPIRE-005`: 基本方式の起動試験

GitHub Actions上の`ASPIRE-005`受入試験は基本方式に限定し、シミュレータ、試合管理機能、Crane、必要な`cm4-sim`、Duckを対象とする。個別の起動管理プログラムへの移行、構成試験と起動管理プログラムの個別試験、独立した通常レビューが完了するまで実行しない。比較方式のトラッカーとDebugHostの実装・受入は、`ASPIRE-006A`以降および適用される`ASPIRE-NET-003`〜`006`の作業範囲とし、`ASPIRE-005`の成功に含めない。初期の`AddContainer`方式でのコンテナ作成失敗を再試行で回避した結果を、受入成功として記録しない。

基本方式の起動対象を一括で起動し、`game-controller`が11003の唯一の審判情報送信元として動作すること、DuckがシミュレータからSSL-Visionを受信すること、Duckが`TrackerWrapperPacket`を出力することを確認する。基本方式では黄チームのCraneロボットの動作を確認し、青チームのAIは加えない。TIGERsとCraneの両チームの実動作は、対戦方式の`ASPIRE-MATCH-003`で確認する。比較方式は後続の別試験とする。

黄チームのCraneロボットを動かす基本方式の試験では、`referee-driver`が`game-controller`の初期`HALT`を確認し、そこから進行中の指令へ遷移させたことを11003で確かめる。その後、Craneの指令が`cm4-sim`を経由してシミュレータへ届き、黄チームのCraneロボットの位置変化がSSL-VisionとDuckの出力に反映されることまで確認する。審判指令の遷移を確認できない場合は試合状態設定の失敗として扱い、UDP 12345 / 12346の失敗と混同しない。正常な遷移後も試合管理機能の準備完了が維持されること、審判情報またはAPIの通信断で準備未完了になること、試合管理機能の再起動後に`HALT`を再確認するまで、これを依存先として準備完了を待つ後続資源を起動しないことを個別試験で確かめる。

### `ASPIRE-006`: 外部トラッカー比較デバッグ

比較方式の実装・受入は基本方式の`ASPIRE-005`には含めない。TIGERs SumatraとER-ForceのトラッカーをDuckと同じSSL-Vision検出データ`224.5.23.2:10020`で動かし、三つのトラッカー出力を正式なトラッカーマルチキャスト`224.5.23.2:11010`へ集約する。

比較方式では、`tracker-tigers`と`tracker-erforce`を個別の起動管理資源とし、所有者情報の完全な照合とサービス固有の準備確認を行う。`debug-host`は`Tracker.DebugHost`の.NETプロジェクト資源とし、Duckの起動通知と両トラッカーの準備完了に依存させる。DebugHost自身の比較用正常性確認は、Duck、TIGERs、ER-Forceの三つの役割を異なる送信元識別子に対応づけ、新しいパケットを確認するまで未準備とする。外部パケットを受信しただけでは、起動管理プログラムが所有するコンテナの証拠にはしない。

実行中は既存の分割表示と重ね合わせ表示を使う。保存後の比較では、診断記録の時刻を全方式に共通する比較時点として揃える。物体ごとの位置・速度・角度・存在の差を数値で確認する詳細設計は、`Tracker/Design/Testing/tracker-comparison-debug-design.md`を正本とする。

`ASPIRE-006A`では起動対象の構成、サービス準備条件、依存関係を固定する。`ASPIRE-006B`では三つの役割の送信元識別と同時のパケット到着を個別試験で固定する。後続の`ASPIRE-006C`〜`006E`では比較画面と再生機能を実装する。比較方式の実装・受入は基本方式の`ASPIRE-005`には含めない。

### `ASPIRE-006F`: TIGERs対Craneの対戦用起動対象

`ASPIRE-005`の基本構成を土台に、対戦用シミュレータ設定、試合管理機能の設定一式、`tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller`を追加する。対戦方式では`cm4-sim`を除外し、Craneは`team:=ibis`、Sumatraは`--aiBlue`、Duckは11010への送信を無効にすることをアプリケーション構成試験で固定する。

実装は次の子作業項目へ分割する。

| ID | 作業 | 主な完了条件 |
| --- | --- | --- |
| `ASPIRE-006F1` | 対戦設定の版管理 | Sumatraの`simulation_protocol_fixed.xml`相当の設定、試合管理機能の初期状態、試合時間をDuck側の設定として追加する。青チーム=`TIGERs Mannheim` / 黄チーム=`ibis` / `FRIENDLY` / 初期`STOP`を個別試験で固定する。 |
| `ASPIRE-006F2` | `match`方式と起動対象の構成 | `Testing:Mode=match`の選択、`base` / `comparison`との排他、対戦用起動対象の存在、`cm4-sim`と比較専用トラッカーが存在しないことをアプリケーション構成試験で固定する。 |
| `ASPIRE-006F3` | 対戦用シミュレータと試合管理の起動対象 | シミュレータの起動引数、11003の単一送信元、試合管理API、設定の割当、固定イメージ参照をアプリケーション構成試験で固定する。準備完了試験では`base` / `comparison`の`HALT`と`match`の`STOP`を受理し、未知または想定外の指令では未完了とする。 |
| `ASPIRE-006F4` | TIGERs / AutoRef / SSLログ資源 | `tigers-blue`、`autoref-tigers`、`ssl-log-recorder`をそれぞれAspireの外部実行資源として登録する。イメージ、ホストネットワーク、10020 / 11003 / 11010、`--aiBlue`、外部審判設定、各サービスの準備完了条件と`WaitFor`による依存をアプリケーション構成試験で固定する。起動管理プログラムの対象試験では条件成立・期限超過と診断根拠を検証する。 |
| `ASPIRE-006F5` | Crane / Duckの対戦設定 | Craneの`team:=ibis`、`cm4-sim`への非依存、Duckの11010送信無効化を固定し、起動管理プログラムの準備完了を待つ依存関係をアプリケーション構成試験で確認する。 |
| `ASPIRE-006F6` | `match-controller`と試合進行 | 試合管理API、審判情報、ビジョン情報の準備完了、`STOP` / `HALT`からの続行操作、`POST_GAME`または最大時間での終了、結果保存を対象試験で固定する。 |

### `ASPIRE-006G`: TIGERs対Craneの一括対戦試験

`ASPIRE-MATCH-001`から`ASPIRE-MATCH-005`までをLinux上で確認する。両チームの実際の動き、AutoRefとトラッカーの通信経路、試合終了、結果、SSLログ、Craneの記録データ、各起動対象のログを証跡として残す。通常対戦では勝敗や得点を合否条件にしない。

一括試験は次の子作業項目に分ける。

| ID | 作業 | 主な完了条件 |
| --- | --- | --- |
| `ASPIRE-006G1` | 構成・チーム・審判情報の受入 | Linux上で`ASPIRE-MATCH-001` / `002`を実パケットと実際の起動対象で確認し、起動対象一覧、チーム対応、11003の送信元が一つであることを記録する。 |
| `ASPIRE-006G2` | 両チームの動作確認 | `ASPIRE-MATCH-003`を実行し、同じ進行中の審判指令の間に黄側のCraneと青側のTIGERs双方の位置が変化することを確認する。 |
| `ASPIRE-006G3` | AutoRef・トラッカー経路の受入 | `ASPIRE-MATCH-004`を実行し、Sumatraからの11010送信、AutoRefによる10020 / 11003 / 11010の利用、Duckが11010へ送信しないことをパケット数とログで確認する。 |
| `ASPIRE-006G4` | 試合終了・証跡 | `ASPIRE-MATCH-005`を実行し、`POST_GAME`または最大時間での終了、対戦結果、SSLログ、Craneの記録データ、各起動対象の標準出力と標準エラーを失敗時も保存する。 |

## テスト方針

実装ではテスト駆動開発を使う。

最初にAppHostのアプリケーション構成を検査する試験を追加し、未実装の状態では失敗することを確認する。
少なくとも次の項目を自動で検査する。

- `simulator`、`game-controller`、`crane`、`cm4-sim`、`duck`の各資源が存在する。
- `duck`が`Tracker.RuntimeHost`を参照する.NETプロジェクト資源である。
- `crane`が`ghcr.io/ibis-ssl/crane`の`scenario-<対象コミットのハッシュ値>`イメージを参照する。
- `simulator`が`ghcr.io/ibis-ssl/framework-simulatorcli`、`cm4-sim`が`ghcr.io/ibis-ssl/orion-cm4-sim`の固定した版識別子を参照する。
- `simulator`、`game-controller`、`crane`、`cm4-sim`がそれぞれ独立した起動管理資源として登録され、Duckはプロジェクト資源のままである。各資源のイメージ、起動コマンド、引数、ホストネットワーク、`-p`を使わないこと、所有者ラベルを確認する。DCPの`ContainerResource`を併用しない。
- Duckに`sim`用の`VisionReceiver`設定が渡される。
- シミュレータのフィールド形状と物理特性が明示される。
- Craneの`team`、`planner`と`cm4-sim`の接続ポートが明示される。
- `game-controller`が固定した版識別子またはダイジェスト値を参照し、11003の唯一の審判情報送信元として構成される。
- 比較方式の`tracker-tigers`は外部の試合管理機能を使う設定とし、`gameController=false`と`publishRefereeMessages=false`によって11003を受信専用にする。
- 比較方式の`tracker-tigers` / `tracker-erforce`を個別の起動管理プログラム資源として登録する。両資源は、想定した所有者ラベル、資源名、イメージ、ホストネットワーク、完全なコンテナID、稼働状態が一致することを`inspect`で確認する。コンテナ内の想定プロセスまたはサービスの起動診断と、送信元識別子が一致する新しいトラッカーパケットを確認するまで準備完了にしない。
- 比較方式では両トラッカーが、準備完了したシミュレータと試合管理機能を正常性確認付きの`WaitFor`で待つ。11003の送信元は追加しない。`debug-host`はDuckの起動通知と両トラッカーの準備完了を待つ。DebugHost自身の比較用正常性確認では、Duck / TIGERs / ER-Forceの三つの送信元識別子を区別し、新しいパケットを受信するまで準備完了にしない。
- `referee-driver`の結合試験用設定では、起動世代ごとに方式固有の初期指令（`base` / `comparison`は`HALT`、`match`は`STOP`）を確かめてから試合管理APIへ続行指令を送り、進行指令への遷移後も正常性が保たれることを確認する。
- 試合管理機能の準備完了は、起動世代ごとに初期状態を一度確認して決める。その後は有効な審判指令への遷移を許容する。審判情報またはAPIの通信断、未知の指令、プロセスまたはコンテナの終了時は未準備とする。資源の再起動時は初期状態の確認記録を消去し、想定した初期指令を再確認するまで後続資源を待機させる。
- 各起動管理資源に`/health/live`と、サービス固有条件を判定する`/health/ready`確認が設定される。起動だけを待つ`WaitForStart`を準備完了条件として使わない。
- 既定の`visibility_graph`構成では、`cm4-sim`と`duck`は準備完了した`simulator`を待ち、`crane`は準備完了した`cm4-sim` / `game-controller`とDuckの起動通知を待つ。
- `planner`の設定で`cm4-sim`を使わない場合、`crane`は`duck`と`game-controller`だけに依存し、存在しない`cm4-sim`を待たない。
- 対戦方式では`tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller`を追加し、`cm4-sim`と比較専用の`tracker-tigers`は登録しない。
- 対戦方式のシミュレータ起動引数、青チーム=`TIGERs Mannheim` / 黄チーム=`ibis`の割当、Craneの`team:=ibis`、Sumatraの`--aiBlue`を一組の設定として固定する。
- 対戦用Sumatra設定では10020のSSL-Vision検出データ、11003の外部審判情報、`gameController=false`、11010へのトラッカー情報送信を指定する。
- 対戦方式のDuckは11010へのトラッカー情報送信を無効にし、AutoRefのトラッカー入力にDuckのデータが混ざらないようにする。
- `match-controller`は参加資源の起動後に起動し、試合管理APIと11003、10020の実データを確認してから試合を開始する。
- 比較方式の`tracker-tigers` / `tracker-erforce`は、所有者ラベル、資源名、イメージ、ネットワーク、コンテナID、稼働状態を確認する。コンテナ内の想定プロセスと起動診断に加え、想定した送信元識別子を持つ新しいトラッカーパケットが揃うまで準備完了にしない。
- 比較方式の`debug-host`はDuckの起動通知と両トラッカーの準備完了を待つ。さらにDebugHost自身の比較用正常性確認で、Duck / TIGERs / ER-Forceの三つの役割を異なる送信元識別子として識別し、新しいパケットを受信するまで準備完了にしない。

起動管理プログラムの単体試験では、Dockerの代替実行ファイルを使う。取消後にDocker側の作成完了通知が遅れる場合と、コンテナIDを取得できない場合の期限付き再検索を検証する。所有者ラベルの不一致、子プロセスの回収、ログ追跡処理の取消、他者所有コンテナが変更されないことも確認する。通常・対戦・比較の各方式について、準備完了、期限超過、サービス診断の根拠を検証する。

試合管理機能は、起動時の初期状態の記録と継続的な正常性確認、有効な指令への正常な遷移、通信断またはプロセス終了、資源再起動後の初期状態再確認を別々に検証する。

比較方式の構成試験では、トラッカー起動管理プログラムと`DebugHost`の資源種別、所有者情報の完全照合、固有の準備完了条件、シミュレータ・試合管理機能・Duck・各トラッカーの準備完了を待つ依存関係を固定する。`DebugHost`の対象試験では、三つの役割の送信元識別と同時刻に届くパケットを検証する。構成試験だけで実通信やロボットの実動作を確認したことにはしない。

Dockerを必要とする一括起動試験は、AppHostの構成検査と分ける。Dockerを利用できない環境でも、アプリケーション構成の回帰不具合を検出できるようにする。

一括起動試験では、すべての資源が`Running`になっただけで成功とはしない。SSL-Visionの受信とDuckのトラッカーパケット出力までを試験証跡に含める。

起動構成の所有権を確認する個別試験では、一つ目のAppHostがロックを保持している間に二つ目を起動すると、資源起動前に失敗することを確認する。また、最初のAppHostの終了後は同じロックファイルが残っていても次の起動が成功することを確認する。`10020` / `11010`の待受可否はこの判定に使わない。占有型制御ポートの外部競合は別試験で事前検出する。

`RuntimeVisionReceiverService`の個別試験では、有効なSSL-Visionパケットを復号してバッファへ渡すたびに`VisionPacketsReceivedTotal`が増加し、診断ログから接続先、インターフェース、累積値を取得できることを確認する。復号に失敗したUDPパケットは正常受信数に加算しない。

## 対応 OS の動作確認仕様

複数OSへの対応は、.NETのビルド成功だけでは完了としない。DockerのホストネットワークとUDPマルチキャストが実際の開発ホストで成立することを確認してから、そのOSを対応済みとして扱う。

確認対象は次の三環境とする。

- LinuxとDocker Engine。
- Windows 上の Docker Desktop で Linux コンテナを実行し、ホストネットワークを有効にする。
- macOS 上の Docker Desktop で Linux コンテナを実行し、ホストネットワークを有効にする。

未実施のOSは「未確認」とし、他OSの成功結果を代用しない。

### 必須確認項目

| ID | 経路 | 合格条件 |
| --- | --- | --- |
| `ASPIRE-NET-001` | AppHost起動 | シミュレータ、`game-controller`、Crane、`cm4-sim`、Duckが起動し、要求した比較資源も起動できる。 |
| `ASPIRE-NET-002` | コンテナからホストへのSSL-Visionマルチキャスト | シミュレータが`224.5.23.2:10020`へ送信し、ホスト上の`Tracker.RuntimeHost`が継続して受信する。安定起動後5秒以内にRuntimeHost自身の`VisionPacketsReceivedTotal`が10件以上増加する。 |
| `ASPIRE-NET-003` | 同じマルチキャストの複数受信 | 比較方式では`Tracker.RuntimeHost`と`Tracker.DebugHost`が同時に`224.5.23.2:10020`を受信し、RuntimeHostの`VisionPacketsReceivedTotal`とDebugHostの未加工入力パケット数が同じ確認時間内にともに増加する。 |
| `ASPIRE-NET-004` | コンテナ間のSSL-Visionマルチキャスト | TIGERsとER-Forceが同じ`224.5.23.2:10020`を受信し、それぞれ追跡データを生成する。 |
| `ASPIRE-NET-005` | コンテナからホストへの追跡データのマルチキャスト | TIGERs / ER-Forceが`224.5.23.2:11010`へ送信し、`Tracker.DebugHost`がそれぞれ別の送信元として受信する。 |
| `ASPIRE-NET-006` | ホスト間の追跡データのマルチキャスト | Duckが`224.5.23.2:11010`へ送信し、`Tracker.DebugHost`がDuckからのデータとして受信する。 |
| `ASPIRE-NET-007` | ホストネットワーク上の制御UDP | `referee-driver`が11003の指令を`HALT`から試合進行中の状態へ遷移させたことを確認する。その後、Craneから`cm4-sim`へのUDP 12345と、`cm4-sim`からシミュレータへのUDP 12346が通ることを確認する。別途、受入試験がSSL-Vision検出データから黄チームのCraneロボットの位置変化を確かめ、Duckの出力したトラッカーパケットを受信する。審判指令の遷移が成立しなければ、UDP経路の成功証跡とはしない。
| `ASPIRE-NET-008` | マルチキャスト用インターフェースの選択 | `InterfaceAddress`を指定しない通常経路を確認する。複数の通信経路や仮想専用網などで自動選択できない場合は、IPv4アドレスを明示して受信できることを確認する。 |
| `ASPIRE-NET-009` | 起動構成の所有権と固定ポートの競合 | 同じホストで二つ目の起動構成を始める場合、資源起動前にAppHostの排他ロックを取得できず、明示的に失敗する。外部プロセスとのポート競合は占有型制御ポートだけを事前確認し、共有マルチキャストポート`10020` / `11010`が使用中であることを失敗条件にしない。 |

`Tracker.RuntimeHost`にはOS受入用の運用診断値として、正常に復号して`RuntimeVisionPacketBuffer`へ渡したSSL-Visionパケットの累積値`VisionPacketsReceivedTotal`を追加する。受信サービスはこの値を単調増加させ、起動時と一定間隔の診断ログに接続先、選択したインターフェース、累積値を出力する。`ASPIRE-NET-002/003`ではログ確認前後の差をRuntimeHostの受信数として使う。独立したパケット観測器の数はネットワークの補助証跡には使えるが、RuntimeHost自身の受信数の代わりにはしない。DebugHost側では既存の未加工入力記録が持つパケット数を使う。

パケット数の条件は、ソケットを作成できただけでなく、実際のパケットが継続して届くことを確認するための最低条件とする。

### OS ごとの証跡

各 OS の確認では、少なくとも次を記録する。

- OS名と版。
- Docker Engine / Docker Desktopの版。
- Docker Desktopを使う場合は、ホストネットワークが有効かどうか。
- 使用したIPv4インターフェースの一覧と、明示した`InterfaceAddress`。
- Aspireの起動管理資源の状態（`live` / `ready` / `failed`）とDuckのプロジェクト状態。
- 各コンテナの確定したイメージ参照、ID、`NetworkMode=host`、所有者ラベル、終了値、診断結果、最終ログ。
- AppHostの正常停止を始めた時刻、各起動対象の後始末にかかった時間、DCPの15秒上限内に終わったかどうか。
- シミュレータ、`game-controller`、Crane、`cm4-sim`、TIGERs、ER-Forceの標準出力・標準エラー。
- `referee-driver`が観測した11003の指令遷移と、試合管理APIへ送った続行指令。
- `Tracker.RuntimeHost` / `Tracker.DebugHost` の標準出力・標準エラー。
- RuntimeHostの`VisionPacketsReceivedTotal`とDebugHostの未加工入力パケット数の確認前後の値。トラッカーパケットは送信元ごとの受信数も併記する。
- DebugHostが認識したトラッカーの送信元識別子。
- 起動構成の排他ロックの取得結果と、占有制御ポートの競合検出結果。共有マルチキャストポート`10020` / `11010`は使用中かどうかの判定対象外であることも記録する。

Linuxでは自動統合試験を基本とする。Windows / macOSではDocker Desktopが必要なため、専用の実行環境または手動の受入試験を使ってもよい。ただし、実機での成功記録を残すまで対応済みとは扱わない。

Windows / macOSでコンテナとホスト間のマルチキャストが成立しない場合は、別OSでの成功を代用せず失敗として記録する。UDP中継やユニキャストへの変更は後続の設計で検討し、試験中に断りなく通信経路を切り替えない。


### 自動統合試験による一括起動の受入

`ASPIRE-NET-002`の手動試験では、模擬装置からDuckの実行プログラムまでの通信経路だけを検査する。`ASPIRE-NET-001`と`ASPIRE-NET-007`を確認する一括起動試験は、既存の`.github/workflows/dotnet-test.yml`に追加する。手動実行に加え、作業用ブランチ`task/pr28-aspire-005`へ更新を送った場合も、通常のテスト成功後に起動する。これにより作業中の実装を実測でき、新しい実行定義を標準の変更履歴に取り込む前から手動で試験できる。実行定義ファイルは新設しない。

この試験では、標準の仮想計算機でDockerと同じネットワークを共有する設定を使う。標準実行環境の中央処理装置数、メモリ量、保存容量は公式資料に記載されている。Dockerを使えない軽量実行環境は対象外とする。既存の通信試験が成功しても、全起動対象の起動に十分な保存容量がある証拠にはならない。最初の一括起動試験では、Dockerイメージ取得の前後と試験終了時の空き容量を記録し、実際に起動した資源の容量も調べる。

CIでは検証対象の起動管理プログラムを実行する。`Testing:StackOwnership:LockPath`は`$RUNNER_TEMP`の下に置き、実行識別番号と再試行回数を含める。この試験が起動した起動対象だけを停止・削除し、Docker全体を対象とする一括削除は行わない。後始末と診断情報の収集は成功時・失敗時のどちらも実施する。

起動管理プログラムの出力、Dockerの情報、各起動対象の設定とログ、受入試験で観測した審判状態・操作、SSL-Vision検出データと追跡データの受信数、試験結果を成果物として保存する。各待機には有限の時間制限を設ける。空き容量が不足しても、広範囲な削除によって.NET SDKなどの開発ツールを壊さない。

審判管理プログラム3.20.3のGit固定版`8050f232c3130323bbd91d1d3d56e9553506c8e4`では、新しい試合状態と指令は`HALT`で初期化される。起動時に`config/state-store.json.stream`があれば、その保存状態が復元される。このため試験では起動対象の世代ごとに新しい実行単位を作り、設定や状態を外部保存領域に永続化しない。基本方式と比較方式では空の保存先からの`HALT`、対戦方式では版管理した対戦設定の`STOP`を期待初期状態とし、いずれもUDP 11003で実測する。起動時の準備確認では方式固有の期待指令を世代ごとに一度確認する。初期化後の継続的な正常性確認では、その指令との一致を求めず、既知の審判指令を期限内に受信できること、APIが応答すること、プロセスと所有コンテナが稼働していることを確かめる。進行指令へ遷移した後、初期指令と一致しないことだけで未準備にしない。通信断、API不通、未知の指令、プロセスまたはコンテナの終了は未準備とする。起動対象の再起動では前世代の初期確認を破棄し、次世代の期待初期指令を再検証する。受入試験で期待と異なる初期指令を観測した場合は、UDP 12345 / 12346の確認へ進まず、審判状態の準備失敗として報告する。

実行環境の容量は固定されているため、利用可否や費用を確認せずに大容量環境を前提としない。Dockerイメージを展開した後に必要な容量は、圧縮時の合計からは分からない。初回の実測値、最小空き容量、実行時間を成果物に記録し、複数回測定してから実行環境を決める。Docker常駐処理の設定変更はこの試験に含めない。

根拠: [標準実行環境の仕様](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)、[大容量実行環境の概要](https://docs.github.com/en/actions/concepts/runners/github-hosted-runners)、[審判管理プログラムの起動処理](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/cmd/ssl-game-controller/main.go)、[初期状態の定義](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/state/state.go)、[状態保存からの復元処理](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/engine/engine.go)。

## トラッカー比較デバッグ

通常のDuckとCraneによる基本方式とは別に、比較方式を用意する。比較用トラッカーは後続の`ASPIRE-006A`で個別の起動管理資源として追加し、`Tracker.DebugHost`は.NETプロジェクト資源とする。

比較方式では同じSSL-Vision検出データをDuck、TIGERs、ER-Forceへ与え、`Tracker.DebugHost`で三者の正式なトラッカーパケットを比較する。外部トラッカーそれぞれの所有者情報と固有の準備条件を確認し、DebugHostはDuckの起動と両トラッカーの準備完了を待つ。また、三つの送信元を区別する比較用正常性確認を行う。外部トラッカーを利用できない場合でも、通常方式のシミュレーション受入を停止させない。

比較の詳細、送信元の識別、時刻の対応付け、ボールとロボットの対応付け、数値差、CaptureOnと再生の契約は`Tracker/Design/Testing/tracker-comparison-debug-design.md`に定義する。

## TIGERs対Craneの対戦方式

TIGERsの制御用人工知能とCraneを実際に対戦させる方式を、トラッカー比較とは独立したAppHost構成として用意する。設計の基準は、2026-09-29時点の`ibis-ssl/crane`の`develop`ブランチにあるコミット`af6e0d3dec745415ce060ff5de2042afd3ec5145`の`docker/match-vs-tigers/docker-compose.yaml`、`simulation_protocol_fixed.xml`、`match_controller_pb.py`とする。

トラッカー比較用の`tracker-tigers`は制御用人工知能を起動しないため、対戦方式には流用しない。対戦用Sumatraは別の起動対象`tigers-blue`とし、Craneの現行構成と同じく青チーム側の制御用人工知能として起動する。

| 起動対象名 | 実行形態 | 対戦方式での責務 |
| --- | --- | --- |
| `simulator` | Aspire 外部実行資源＋Docker コンテナ | ER-Force `simulator-cli` をホストネットワークで起動する。対戦用引数はCraneの現行構成を基準にし、`-g 2020 --realism None --ibis-use-referee --ibis-feedback-team-name ibis --ibis-referee-port 11003`を使う。 |
| `game-controller` | Aspire外部実行資源＋Dockerコンテナ | 11003の唯一の審判情報送信元とする。対戦用の初期状態を読み込む。 |
| `crane` | Aspire 外部実行資源＋Docker コンテナ | 固定した`ghcr.io/ibis-ssl/crane:scenario-<対象コミットのハッシュ値>`を使い、`sim:=true speak:=false team:=ibis`で黄チームを制御する。 |
| `tigers-blue` | Aspire外部実行資源＋Dockerコンテナ | 固定した版識別子またはダイジェスト値を指定した`tigersmannheim/sumatra`を`--headless --aiBlue --visionAddress 224.5.23.2:10020 --refereeAddress 224.5.23.1:11003 --matchStats --moduli simulation_protocol`で起動する。 |
| `autoref-tigers` | Aspire外部実行資源＋Dockerコンテナ | `tigersmannheim/auto-referee:1.2.0`を起動し、SSL-Vision検出データ10020、審判情報11003、トラッカー情報11010を使って試合判定を試合管理機能へ返す。 |
| `ssl-log-recorder` | Aspire外部実行資源＋Dockerコンテナ | 審判情報11003、ビジョン情報10020、トラッカー情報11010を対戦記録として保存する。 |
| `match-controller` | Aspire外部実行資源＋.NET 10コンテナ | 既存の`Testing/Duck.Testing.RefereeDriver`にある審判情報の受信と制御API接続の部品、および`SslProto`を再利用する.NET 10の試験用プログラム。初期`STOP`を確かめてから試合を進行し、`POST_GAME`または最大時間で終了して結果と診断記録を保存する。実通信の受入確認には`Tracker.Tests`を使い、試合制御プログラムには含めない。実行環境イメージの基盤と固定ダイジェストは未確定であり、実装前に確定する。新しいNuGet依存は加えない。 |
| `duck` | .NETプロセス | SSL-Visionの観測とDuck側のデバッグを続ける。ただし対戦判定に影響しないよう、対戦方式では正式なトラッカーマルチキャスト11010への送信を無効にする。 |

対戦方式では、基本構成の`visibility_graph`用`cm4-sim`を起動しない。Craneの現行`match-vs-tigers`構成は`cm4-sim`を含まず、対戦用シミュレータとSumatraの設定を一体で使うため、基本方式の`--ibis-port 12346`およびUDP 12345 / 12346の経路を混在させない。

Sumatraへ渡す`simulation_protocol_fixed.xml`相当の設定ファイルはDuck側で版管理し、少なくともSSL-Vision検出データ`224.5.23.2:10020`、審判情報の`source=NETWORK` / ポート`11003` / `gameController=false`、`SumatraSimBotManager`、トラッカーの送信先`224.5.23.2:11010`を指定する。AppHost起動時にCraneリポジトリを複製して設定ファイルを取得する方式にはしない。

試合管理機能の初期状態もDuck側の対戦用設定として版管理する。初期対戦はCraneの現行構成に合わせ、青チーム名を`TIGERs Mannheim`、黄チーム名を`ibis`、試合種別を`FRIENDLY`とする。Craneの`team:=ibis`とSumatraの`--aiBlue`でチーム対応を定め、片方だけの設定を変更しない。

固定版Game Controller 3.20.3の`isGoalValid`は、提案に`NumRobotsByTeam`または`MaxBallHeight`が含まれる場合に、それぞれ許容人数の超過とボール高度の0.15 m超過を検査する。また、`LastTouchByTeam`が設定されている場合は、得点チームの直近2秒以内の非停止反則を検査する。選手番号そのものの照合は、この関数では行わない。

Crane が現在使う構成ファイルでは、Sumatra、試合管理機能、SSLログ記録器のイメージの版識別子が更新される。そのためDuckの再現可能な試験では暗黙の`latest`を使わず、AppHostの設定でイメージの版識別子またはダイジェスト値を明示し、実行証跡に解決済みのイメージ参照を保存する。Craneのイメージは既存の`scenario-<対象コミットのハッシュ値>`を既定とし、Craneの対戦処理が同じCraneイメージに`match-<commit SHA>`という版識別子を付けて利用できる構成と整合させる。

### 対戦方式の起動順序

1. `simulator`と`game-controller`を起動する。
2. `duck`はプロジェクト起動通知を公開する。`tigers-blue`と`ssl-log-recorder`は準備完了した`simulator` / `game-controller`を待つ。
3. `autoref-tigers` は準備完了した `simulator`、`game-controller`、`tigers-blue` を待つ。
4. `crane` は準備完了した `simulator` と `game-controller` を待つ。対戦方式では存在しない `cm4-sim` への依存を作らない。
5. `match-controller` は準備完了した `simulator`、`game-controller`、`crane`、`tigers-blue`、`autoref-tigers`、`ssl-log-recorder` と Duck のプロジェクト起動後に起動する。
6. 依存関係を満たした後も`match-controller`は試合管理APIへの接続、11003の審判状態、10020のSSL-Vision、トラッカー情報11010を実測する。`HALT` / `STOP`の初期状態を確認してから試合続行指令を送り、起動対象が起動しただけでは試合を開始しない。

`match-controller`はCraneの現行試合制御を基準に、`HALT` / `STOP`から使える続行指令を選ぶ。必要に応じて`NEXT_COMMAND`、`NORMAL_START`、`FORCE_START`を使って試合を進める。終了条件は`POST_GAME`または設定した最大試合時間とする。結果には少なくとも両チームの得点、終了理由、`CRANE WIN` / `TIGERs WIN` / `DRAW`のいずれかを保存する。

勝敗そのものはCIの合否条件にしない。両チームの人工知能が同じ試合に参加し、規定の終了条件まで進行し、結果と診断証跡を生成できることを対戦機能の正常条件とする。

AutoRef 1.2.0の調査基準はコミット`1cb2545b81f3568767139a145e3c1aa062399848`とする。この版で再現可能かつ妥当な得点提案を発生させる試験条件は未確定であり、再現試験も未実施である。固定した試験配置、発生させる提案、判定に使う入力値を特定するまでは、自動承認の受入を完了扱いにしない。実装前に具体的な再現手順を確認する。

### 対戦方式の受入項目

| ID | 確認内容 | 合格条件 |
| --- | --- | --- |
| `ASPIRE-MATCH-001` | 資源構成 | `simulator`、`game-controller`、`crane`、`tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller`が存在し、各Docker資源に起動管理処理と準備完了条件がある。構成試験では準備完了を待つ`WaitFor`の依存関係を固定し、`cm4-sim`、`tracker-tigers`、`tracker-erforce`、`debug-host`を起動対象に含めない。 |
| `ASPIRE-MATCH-002` | チームと審判情報の契約 | 青チーム=`TIGERs Mannheim`、黄チーム=`ibis`、Crane=`team:=ibis`、Sumatra=`--aiBlue`が一致し、11003の送信元は`game-controller`一つだけである。方式固有の準備確認で`match`の初期`STOP`を確かめる。 |
| `ASPIRE-MATCH-003` | 両チームの実動作 | 同じ進行中の審判状態の確認時間内に、SSL-Vision上で黄側のCraneロボットと青側のTIGERsロボット双方の位置変化を観測できる。片側だけの移動では合格にしない。 |
| `ASPIRE-MATCH-004` | AutoRefとトラッカーの経路 | `tigers-blue`が11010へトラッカーパケットを出力し、`autoref-tigers`が10020 / 11003 / 11010を使って動作する。Duckは11010へ送信せず、AutoRefのトラッカー入力に別の送信元を混在させない。通常対戦で得点が発生することは合格条件にしない。 |
| `ASPIRE-MATCH-005` | 試合終了と証跡 | `POST_GAME`または最大試合時間で終了し、対戦結果、全起動対象の標準出力・標準エラー、SSLログ、Craneの記録データ、試合管理機能 / AutoRef / Sumatraの診断情報を保存できる。通常対戦で得点が発生することは合格条件にしない。 |
Linuxの自動結合試験では`ASPIRE-MATCH-001`から`005`までを対戦方式の受入条件とする。Windows / macOSでの対応を表明する場合も、各OS上で同じ項目を実際のパケットで確認するまでは対応済みと扱わない。

## 診断

最初の確認にはAspire管理画面の起動対象別ログを使う。

自動試験をCIへ追加する場合は、既存の`.NET`試験と同様に、失敗時の標準出力、標準エラー、試験結果、Aspireとコンテナのログを成果物として保存する。

シミュレータ、`game-controller`、Crane、`cm4-sim`のコンテナのログは資源名が分かる形で分ける。`ASPIRE-NET-007`では`referee-driver`の操作ログと11003の指令遷移も同じ試験証跡に保存する。

対戦方式では、これに加えて`tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller`の標準出力・標準エラー、対戦結果、SSLログ、Craneの記録データ、解決済みイメージ参照、チーム対応、試合時間設定を保存する。失敗時も途中まで生成された結果と各起動対象のログを破棄しない。

## 対象外と段階境界

初期の`ASPIRE-005`基本方式の実装・受入には、次を含めない。

- Duck 自体の Docker 化。
- 本番環境の配置方式。
- Kubernetesへの配置。
- 課題 #14の比較方式の実装と実パケット受入。構成と準備完了の契約は本設計で定義し、起動管理とDebugHostの実装は`ASPIRE-006A`以降、送信元識別・画面・再生は`ASPIRE-006B`〜`006E`、Linux上の実パケット受入は`ASPIRE-007A`で行う。
- 自動レフェリーの実装。
- 人工知能のアルゴリズム変更。
- 既存の `Tracker.RuntimeHost` の通信形式変更。
- Dockerブリッジネットワーク越しのSSL-Visionマルチキャスト対応。

## 後続段階での比較方式

課題 #14の比較試験では、`tracker-tigers`と`tracker-erforce`をAspireの個別起動管理資源とし、それぞれが固定イメージのホストネットワークコンテナを所有する。DCPの`ContainerResource`には戻さない。各資源の準備条件には、所有者情報の完全な照合、コンテナ内の対象プロセスと起動診断、想定する送信元識別子を持つ新しいパケットの受信を含める。`debug-host`は`Tracker.DebugHost`の.NETプロジェクト資源とし、Duckの起動と両トラッカーの準備完了を待つ。比較用正常性確認も三つの役割の送信元識別とパケット到着を確認するまで未準備とする。

`ASPIRE-006A`では起動対象の構成と正常性確認構成、`006B`では送信元識別と三者の同時受信、`006C`〜`006E`では差分・表示・再生、`ASPIRE-007A`ではLinux上の実パケット受入を扱う。比較用トラッカーを追加しても、基本方式の`duck`、`simulator`、`crane`、`cm4-sim`の契約は変えない。

将来ブリッジネットワークへ移行する必要が生じた場合は、ER-ForceのSSL-Visionを任意の宛先へ転送する中継資源を明示的に追加するか、シミュレータに送信先指定機能を追加する。暗黙のマルチキャスト転送には依存しない。

## 完了条件

この設計の移行完了条件を次に示す。初期実装の`AddContainer`方式が存在したことや、構成試験で資源が登録されたことだけでは完了としない。

- 一度のAspire AppHost起動でシミュレータ、`game-controller`、Crane、必要な`cm4-sim`、Duckを管理できる。四つのDockerサービスは個別の起動管理資源とし、それぞれがホストネットワークコンテナを所有する。Duckは`AddProject`のままとする。
- `ASPIRE-005`の完了は`base`方式だけを意味する。`comparison`方式の完了は`ASPIRE-006A`〜`006E`と適用される`ASPIRE-007A`の受入後に別途判定する。
- `comparison`方式ではTIGERs / ER-Forceを個別の起動管理資源、`Tracker.DebugHost`を.NETプロジェクト資源とする。各トラッカーの所有者確認とサービス準備を行い、DebugHostはDuckの起動通知と両トラッカーの準備完了を待つ。三つの送信元識別と新しいパケットを使う比較用正常性確認を満たす。
- 対戦方式では`tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller`を追加し、青チームのTIGERs制御用人工知能と黄チームのCraneを同じ試合管理機能とシミュレータで対戦させられる。
- 対戦方式では`cm4-sim`を起動せず、Duckから11010への送信を無効にしてAutoRefのトラッカー入力に干渉しない。
- `ASPIRE-MATCH-001`から`ASPIRE-MATCH-005`により、両チームの実動作、AutoRefとトラッカーの経路、通常対戦の終了と証跡を確認できる。通常対戦で得点が発生することは合格条件にしない。
- `match-controller`は既存の`Testing/Duck.Testing.RefereeDriver`と`SslProto`を使う.NET 10コンテナとして設計する。新しいNuGet依存を導入しない。実行環境イメージと配布経路は未確定のため、実装前に基盤イメージ、固定ダイジェスト、導入方法を確認する。
- シミュレータと Crane は Docker コンテナ、Duck はホスト上の .NET プロセスとして起動する。
- Craneは`ghcr.io/ibis-ssl/crane:scenario-<対象コミットのハッシュ値>`の固定した識別子で起動し、Duck側ではビルドしない。
- Duck は既存の `sim` 設定と SSL-Vision 契約を維持する。
- 開発者が各資源の起動コマンドを個別に管理する必要がない。
- すべての起動管理資源の稼働・準備完了・失敗状態と標準出力・標準エラーをAspire管理画面で確認できる。コンテナの調査結果、ID、最終ログは受入成果物で確認できる。DCPでコンテナ詳細を直接表示できない制約を文書化している。
- Docker CLIの代替実行ファイルを使う起動管理プログラムの試験では、コンテナの作成と起動、コンテナIDを記録する`--cidfile`の作成遅延、所有者情報の不一致、予期しない終了、起動・準備完了・ログ追跡処理の取消、作成完了の遅延競合、他者所有コンテナに変更がないこと、15秒未満の正常停止と後始末を検証する。
- 試合管理機能の個別試験では、方式固有の初期`HALT` / `STOP`を起動世代ごとに一度確認する。その後の正常な進行指令への遷移は異常とせず、通信断・API不通・未知の指令・プロセス終了では未準備とする。起動対象の再起動時には初期状態を再確認する。
- 比較方式の構成試験、起動管理試験、DebugHost試験では、`ASPIRE-006A` / `006B`の資源種別、所有者の証拠、サービス準備、依存関係、三つの送信元識別とパケット正常性を固定する。Linux上の比較パケット受入は`ASPIRE-007A`まで未完了とし、`ASPIRE-005`をもって比較方式が完了したとはしない。
- Linux上の受入試験で、`cm4-sim`のUDP 12345待受を正確なコンテナ内プロセスに結び付ける準備確認を実証する。実装できない場合は設計上の条件を解除せず、確認を弱めない。
- `SIGINT` / `SIGTERM`による各停止経路で、起動管理資源がDCPの15秒停止上限内に後始末を完了する。`SIGKILL`、ホスト障害、Dockerデーモン停止は保証対象外として記録する。
- SSL-Vision 受信と Duck のトラッカーパケット出力を含む正常経路を確認できる。
- Linux、WindowsのDocker Desktop、macOSのDocker Desktopについて`ASPIRE-NET-001`から`ASPIRE-NET-009`の該当項目を確認し、未確認のOSを対応済みと表現しない。
- 実装と試験の記録を報告書へ残し、PRの最新コミットと同じコミットハッシュのCI結果だけを最終確認に使う。
## 参照

- GitHub課題 #18 `Aspire対応`。
- GitHub課題 #14 `dockerでシミュレーターのケースを追加`。
- [Game Controller 3.20.3のAutoRef提案受信処理](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/rcon/server_autoref.go)、[提案の処理](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/engine/process_proposals.go)、[試合事象への反映](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/statemachine/change_gameevent.go)、[設定と既定値](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/config/config.go)。
- [Craneの対戦用Docker起動設定](https://github.com/ibis-ssl/crane/blob/af6e0d3dec745415ce060ff5de2042afd3ec5145/docker/match-vs-tigers/docker-compose.yaml)に記載されたAutoRefイメージと起動引数。AutoRefの調査基準はコミット`1cb2545b81f3568767139a145e3c1aa062399848`であり、その固定版での得点提案の再現試験は未実施である。
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`。
- `Tracker/Tracker.RuntimeHost/appsettings.json`。
- ER-Forceの`simulator-cli`実装とREADME。
- `ibis-ssl/crane`の`docker/Dockerfile`、`.github/workflows/docker_build.yaml`、`docker/scenario/docker-compose.yaml`、`docker/dev/docker-compose.yaml`、`docker/match-vs-tigers/docker-compose.yaml`、`docker/match-vs-tigers/config/simulation_protocol_fixed.xml`、`docker/match-vs-tigers/config/state-store-initial.json.stream`、`docker/match-vs-tigers/scripts/match_controller_pb.py`、`.github/workflows/match-vs-tigers.yaml`。対戦方式設計の確認時点は`develop`ブランチのコミット`af6e0d3dec745415ce060ff5de2042afd3ec5145`。
- `Tracker/Design/Testing/tracker-comparison-debug-design.md`。
- RoboCup SSLのシミュレーション通信規約。
- Aspire の AppHost、コンテナ、.NET プロジェクト資源、コンテナ実行引数の公式文書。
- Dockerのホストネットワークドライバーの公式文書。
