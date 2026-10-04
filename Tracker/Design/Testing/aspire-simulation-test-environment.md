Warning: truncated output (original token count: 18009)
Total output lines: 519

# Aspire シミュレーション試験環境 設計

## 目的

Issue #18 の Aspire 対応として、シミュレーション試験に必要な複数プロセスを一つの起動操作で立ち上げられるようにする。

初期対象は次の三つとする。

- ER-Force の `simulator-cli` を実行するシミュレータ。
- `ibis-ssl/crane` のロボット制御 AI。
- Duck の `Tracker.RuntimeHost`。

シミュレータと Crane は Docker コンテナとして起動し、Duck は開発中のコードをそのままデバッグできるよう、ホスト上の .NET プロセスとして起動する。

本設計はローカル開発と手動試験の起動を簡単にするためのものであり、本番配置方式を定義するものではない。

## 現状

`Testing/Duck.Testing.AppHost` は実装済みである。`duck` は `Tracker.RuntimeHost` の `ProjectResource`、`simulator`、`game-controller`、`crane`、必要な場合の `cm4-sim` は Aspire の `ContainerResource` として登録されている。「AppHost は存在しない」は設計開始時点の背景であり、現在の状態ではない。

現行 AppHost は `AddContainer` と `WithContainerRuntimeArgs("--network", "host")` 相当でホストネットワークを指定している。この方式は当初設計に沿った実装だが、Aspire AppHost SDK 13.5.4 / DCP 0.25.13 の GitHub Actions 実行では、DCP が利用者定義ネットワークを設定した後にホストネットワーク引数も Docker へ渡していた。Docker は `cannot attach both user-defined and non-user-defined network-modes` によりコンテナ作成を拒否した。この方式は現行コードの記録であり、最終的な受入方式として扱わない。

本書は、サービスごとの外部実行資源から起動管理プログラムを立ち上げ、固定版の Docker コンテナを `--network host` で起動する方式を提案する。Duck は引き続き `AddProject<Projects.Tracker_RuntimeHost>` とする。これは設計提案であり、起動管理プログラムの実装・設計承認・実通信の成功を意味しない。移行完了までは、現行コンテナ資源によるホストネットワーク起動を失敗状態として扱う。

`Tracker.RuntimeHost` は `VisionReceiver` 設定から SSL-Vision の UDP 入力を受け、既定の `sim` 設定では `224.5.23.2:10020` を受信する。
ER-Force の `simulator-cli` は SSL simulation protocol の制御入力を受け、SSL-Vision の状態を UDP 10020 へ送信する。通常時の送信先は `224.5.23.2` で、`--localhost` 指定時は `127.0.0.1` となる。

SSL simulation protocol の既定ポートは次のとおりである。

- シミュレーション制御: UDP 10300。
- 青チーム制御: UDP 10301。
- 黄チーム制御: UDP 10302。


## 基本方針

Aspire は試験用オーケストレータとしてのみ使用する。製品コードの実行責務を `Tracker.RuntimeHost` から AppHost へ移さない。

AppHost は `Testing/Duck.Testing.AppHost` に置く。Duck は既存の `Duck.slnx` に含まれるため、C# のプロジェクト型 AppHost とし、`ProjectReference` と `AddProject<Projects.Tracker_RuntimeHost>` で起動する。

パス指定の `AddDotnetProject` は現行 Aspire では試験的 API のため、初期実装では採用しない。

シミュレータ、Game Controller、Crane、cm4-sim はサービスごとの Aspire 外部実行資源として個別の起動管理プログラムを立ち上げ、それぞれが固定版の Docker コンテナを所有する。Crane は Duck 側でビルドせず、`ibis-ssl/crane` が GitHub Container Registry へ公開する `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>` を起動管理プログラム経由で起動する。`scenario-develop` は動作確認用の移動タグとして明示指定時だけ利用し、再現可能な試験では Crane のコミット SHA に対応するタグを固定する。
## 資源構成

AppHost では三つの主要資源に加え、レフェリー / game-state を供給する一つの試験資源と、Crane の現在のシミュレーション経路を維持する場合に一つの補助資源を管理する。

| 資源名 | 実行形態 | 責務 |
| --- | --- | --- |
| `simulator` | Aspire 外部実行資源＋Docker コンテナ | Crane の現行シナリオ構成と同じ `ghcr.io/ibis-ssl/framework-simulatorcli:<tag>` から ER-Force `simulator-cli` を起動し、物理シミュレーションと SSL-Vision 出力を行う。 |
| `game-controller` | Aspire 外部実行資源＋Docker コンテナ | `robocupssl/ssl-game-controller:<fixed tag or digest>` をホストネットワークで起動し、各 mode で `224.5.23.1:11003` の referee message を生成する唯一の送信元とする。制御 API は `127.0.0.1:8082` を使う。 |
| `crane` | Aspire 外部実行資源＋Docker コンテナ | `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>` から Crane を起動し、ロボット制御指令を生成する。 |
| `cm4-sim` | Aspire 外部実行資源＋Docker コンテナ | `ghcr.io/ibis-ssl/orion-cm4-sim:<commit SHA>` を使い、Crane の `visibility_graph` が出す mode 4 の位置指令を mode 3 の速度指令へ変換する。 |
| `duck` | .NET プロセス | `Tracker.RuntimeHost` を `sim` 設定で起動し、SSL-Vision を追跡してトラッカーパケットを出力する。 |

Aspire のダッシュボードでは、AppHost が管理する各起動管理プログラムの状態と標準出力・標準エラーを確認する。DCP は Docker コンテナを直接管理しないため、コンテナの状態・ID・最終ログは起動管理プログラムの記録と受入証跡に残す。この方式では DCP のコンテナ詳細表示は得られない。

## ネットワーク方針

通信要件はホストネットワークを前提とする。サービスごとの起動管理プログラムが Docker CLI から `--network host` を指定してコンテナを起動する。Aspire の `AddContainer` に実行引数としてホストネットワークを加える方式は現行実装の履歴であり、Aspire/DCP が利用者定義ネットワークも同時指定してコンテナ作成に失敗したため、移行先では使わない。

理由は、ER-Force の `simulator-cli` が SSL-Vision の送信先として任意の IP アドレスを指定する起動引数を持たず、通常のマルチキャスト送信か `127.0.0.1` 送信だけを選べるためである。

シミュレータを通常の Docker bridge network に置いた場合、`--localhost` はコンテナ自身を指し、ホストで動く Duck には届かない。マルチキャストを bridge network とホスト間で透過させる構成にも依存しない。

そのため `simulator`、`game-controller`、`crane`、`cm4-sim` は Docker のホストネットワークで起動し、Duck はホスト上で従来どおり SSL-Vision のマルチキャストへ参加する。起動管理プログラムはサービスごとに分け、それぞれ一つのコンテナだけを所有する。Duck のプロジェクト資源と AppHost の多重起動防止 lock は維持する。
Linux の Docker Engine では host network はコンテナとホストのネットワーク名前空間を共有するため、UDP のポート公開や変換を挟まずに通信できる。

Docker Desktop を使う場合は host networking の有効化が必要である。初期受入環境は Linux の Docker Engine とし、Docker Desktop での動作は別途確認項目とする。

host network では SSL-Vision と tracker multicast、採用するシミュレータ構成の制御ポートをホスト全体で共有する。ポートは用途で分け、`10020` と `11010` は複数 receiver が `SO_REUSEADDR` を使って同時受信する共有 multicast port とする。一方、UDP 10300 / 10301 / 10302 のうち構成で有効にする listener と、Crane 経路の UDP 12345 / 12346 は一つの stack が占有する制御 port とする。`10020` / `11010` の単純な bind 可否や in-use 判定を二重 stack の検出には使わない。

同一ホストで同じ Duck Aspire stack を二つ起動すること自体は、AppHost が resource 起動前に取得して終了まで保持する host-local の stack ownership lock で拒否する。初期実装は一意な lock file を `FileShare.None` で開いた handle を保持する方式とし、ファイルの存在だけでは失敗としない。二つ目の AppHost が lock を取得できなければ resource を起動せず明示的に失敗する。別プロセスとの競合確認は占有制御 port だけを対象とし、共有 multicast port は実 packet の複数受信試験で検証する。Aspire の `--isolated` を指定しても、この ownership と固定 UDP port は分離されないものとして扱う。

Crane の現在のシナリオ構成では、`visibility_graph` を使う場合に `crane` が mode 4 の位置指令を UDP 12345 へ送り、`cm4-sim` が実機 CM4 相当の位置制御を行って mode 3 の速度指令を UDP 12346 へ転送する。したがって Crane を既存 image の現在の挙動のまま組み込む初期構成では、汎用 SSL simulation protocol の 10301 / 10302 へ直接送る経路へ置き換えない。

ホストネットワーク使用時は Docker の `-p` 相当のポート公開を併用しない。ホスト上で Docker CLI を開始する起動管理プログラムは shell command string や `bash -c` を使わず、引数配列を直接渡して Docker CLI を実行し、子プロセスを監督する。Crane 構成に記録した `bash -c` 例は、Crane image 内部で ROS 環境を読み込んで launch するコンテナ内 command の記録であり、ホスト側 wrapper / Docker CLI の起動方式を許可するものではない。実装時は image の entrypoint / command 契約を確認し、ホスト側で shell を挟まない。

### 通信経路

```text
Crane image
  │ mode 4 / UDP 12345
  ▼
cm4-sim image
  │ mode 3 / UDP 12346
  ▼
ER-Force simulator container
  │ SSL-Vision UDP 224.5.23.2:10020
  ▼
Tracker.RuntimeHost (host process)
  │ TrackerWrapperPacket UDP
  ▼
試験用の確認先
```

シミュレーション制御を行う試験ツールが必要な場合は UDP 10300 を使用する。初期 AppHost 自身は自律的な試験シナリオを生成しないが、`ASPIRE-005` / `ASPIRE-NET-007` の active motion 確認では専用の `referee-driver` 試験 fixture を使う。`referee-driver` は `224.5.23.1:11003` を直接 publish せず、`game-controller` の `ws://127.0.0.1:8082/api/control` へ continue action を送る。起動世代ごとに mode 固有の初期 command（base / comparison は `HALT`、match は `STOP`）が検証済みであることを前提に `NEXT_COMMAND` を送り、遷移後も `HALT` / `STOP` なら `FORCE_START`、それ以外の準備状態なら `NORMAL_START` を送る。11003 で active command への遷移を確認してから Crane の指令と SSL-Vision の位置変化を検査する。active 遷移後も初期 command との不一致だけでは Game Controller readiness が落ちず、通信/API health が継続することを確認する。

## Duck の起動

`duck` は `Tracker.RuntimeHost` のプロジェクト資源として起動する。
試験用の既定値は既存の `sim` 設定を使い、少なくとも次を明示する。

- `Tracker:ActiveProfileName=sim`。
- `VisionReceiver:MulticastAddress=224.5.23.2`。
- `VisionReceiver:Port=10020`。

製品の `appsettings.json` に試験環境固有のコンテナ情報を追加しない。AppHost から環境変数で上書きする。

Duck を Docker コンテナへ変更することは初期範囲に含めない。これにより、ブレークポイントを使う通常の .NET デバッグ経路を維持する。

## シミュレータの構成

シミュレータ資源は、Crane の現行シナリオ構成で既に使われている `ghcr.io/ibis-ssl/framework-simulatorcli:<tag>` を使用する。Duck 側では ER-Force Framework を clone してビルドしない。

シミュレータの image tag は AppHost の設定で明示し、再現可能な試験では固定タグを使う。起動引数は Crane の現行シナリオ構成を基準にする。

```sh
./bin/simulator-cli -g 2020B --realism None --ibis-port 12346 --ibis-team-color yellow
```

AppHost では少なくとも image tag、geometry、realism、`IBIS_PORT`、`IBIS_TEAM_COLOR` を設定可能にする。初期値は Crane の現行シナリオ構成と同じ geometry `2020B`、realism `None`、`IBIS_PORT=12346`、team color `yellow` とし、試験結果が外部の既定値変更だけで変化しない構成にする。
## Crane の構成

AI は `ibis-ssl/crane` の既存 Docker image を使用する。Duck リポジトリ側で Crane のソースを clone してビルドする方式や、旧 `ibis-ssl/crane_docker` の ROS 2 Foxy ベース構成は採用しない。

`ibis-ssl/crane` の現在の `docker/Dockerfile` は ROS 2 Jazzy を基底とし、`scenario` target で Crane をビルドする。Crane の `docker build` workflow は GitHub Container Registry へ次のタグを公開する。

- `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>`: 再現可能な試験で使う固定タグ。
- `ghcr.io/ibis-ssl/crane:scenario-develop`: 開発中の確認用に使える移動タグ。

AppHost の既定設定では固定タグを要求する。`scenario-develop` は利用者が明示的に指定した場合だけ使う。指定したタグが registry に存在しない場合は、別タグへ暗黙にフォールバックせず起動失敗とする。

Crane の起動コマンドは現在のシナリオ構成を基準にする。

```sh
bash -c "source /root/ibis_ws/install/setup.bash && ros2 launch crane_bringup crane.launch.xml sim:=true speak:=false team:=Yellow planner:=${PLANNER}"
```

`team` と `planner` は AppHost の設定から変更可能にする。既定 planner は Crane の現行シナリオ構成と同じ `visibility_graph` とし、その場合は `cm4-sim` を同時に起動する。Crane、`cm4-sim`、シミュレータは ROS 2 / UDP / multicast の通信要件を保つため host network を使う。

## レフェリー / game-state の所有権

`224.5.23.1:11003` の referee message は、通常 mode / comparison mode / 対戦モードのすべてで `game-controller` だけが生成する。11003 は複数 consumer が受信する multicast endpoint であり、socket の bind 可否では producer の一意性を判定しない。AppHost の構成上、11003 を publish する資源を `game-controller` 一つに限定する。

通常 mode では standalone の `game-controller` が authoritative producer になる。comparison mode でも同じ `game-controller` を使い、`tracker-tigers` は Sumatra の referee module を `source=NETWORK`、`port=11003`、`gameController=false`、`publishRefereeMessages=false` に固定した外部 Game Controller 用設定で起動する。標準の `simulation_protocol.xml` のように内蔵 Game Controller を有効にする構成は使わない。`tracker-erforce` の `--gc-port 11003` も consumer として扱い、referee message を publish させない。

対戦モードも同じ `game-controller` を使う。`tigers-blue` は Duck 側で固定する対戦用 `simulation_protocol_fixed.xml` 相当の設定により `source=NETWORK`、`port=11003`、`gameController=false` とし、`autoref-tigers` は referee consumer / Game Controller client として動作する。対戦の進行操作を行う `match-controller` も API client に限定し、11003 を直接 publish しない。

`game-controller` の制御 API `127.0.0.1:8082` はこの stack の `game-controller` が占有する。通常 mode / comparison mode の `referee-driver` は API client としてだけ動作し、11003 の producer にはならない。これらの active-motion 試験では初期 referee command が `HALT` である fixture を固定する。対戦モードでは Crane の現行対戦構成を基準に Blue=`TIGERs Mannheim`、Yellow=`ibis`、初期 command=`STOP` の対戦 fixture を別に使い、`match-controller` が API client として試合を進行させる。どちらも再現可能な試験では Game Controller image を固定 tag または digest で指定する。

## 準備完了と起動順序

現行コードの `WaitForStart` は対象資源の起動だけを示し、UDP/ROS の準備完了を示さない。移行後は各起動管理プログラムが `/health/live` と `/health/ready` を提供する。コンテナの稼働だけでは準備完了とせず、各サービスの確認条件を満たした場合に限って成功とする。AppHost は準備完了を待つ依存関係に正常性確認付きの `WaitFor` を使い、`WithReference` は接続情報の参照に限る。

初期の準備完了条件は次のとおり。

| 資源名 | 準備完了条件 |
| --- | --- |
| `simulator` | 所有コンテナが稼働し、起動管理プログラムが `224.5.23.2:10020` の SSL-Vision 検出パケットを受信して復号できる。継続受信とロボット移動は一括受入試験で確認する。 |
| `game-controller` | 起動世代ごとに mode 固有の初期状態を検証する。初期化中は `base` / `comparison` の `HALT`、`match` の `STOP` を11003で確認するまで未準備とする。初期状態確認後は、有効な審判指令の状態遷移を許容し、初期指令との一致を継続準備条件にしない。各health確認では所有者情報が完全一致するコンテナ、Game Controller プロセスと API の応答、期限内に復号できる既知の審判指令を確かめる。通信断、API不通、未知指令、プロセス / コンテナ終了は未準備とする。 |
| `tracker-tigers` | 所有者ラベル / 資源名 / 期待イメージ / ホストネットワーク / 完全なコンテナID / 稼働状態を確認し、コンテナ内の Sumatra プロセスと起動完了診断、および比較実行開始後に `224.5.23.2:11010` で受信した期待送信元識別子の復号可能なトラッカーパケットを確認する。コンテナ稼働だけでは準備完了としない。 |
| `tracker-erforce` | 所有者ラベル / 資源名 / 期待イメージ / ホストネットワーク / 完全なコンテナID / 稼働状態を確認し、コンテナ内の ER-Force tracker プロセスと起動完了診断、および比較実行開始後に `224.5.23.2:11010` で受信した期待送信元識別子の復号可能なトラッカーパケットを確認する。コンテナ稼働だけでは準備完了としない。 |
| `debug-host` | `Tracker.DebugHost` の .NET プロジェクト資源として起動する。Duck の起動通知と両外部トラッカーの準備完了を待ち、比較用正常性確認が Duck / TIGERs / ER-Force の三つの論理役割をそれぞれ別の送信元識別子に解決し、同じ確認窓で三者の新しいトラッカーパケットを受信した場合に準備完了とする。 |
| `cm4-sim` | 所有コンテナが稼働し、UDP 12345 の待受 socket が当該コンテナ内のプロセスに属すると確認できる。ポートが使用中というだけでは準備完了にしない。 |
| `crane` | 所有コンテナが稼働し、コンテナ内の ROS graph に `crane_session_coordinator` があると確認できる。指令と移動は受入試験で別途確認する。 |
| `tigers-blue` | 所有コンテナが稼働し、Sumatra の起動完了記録と、この実行で生成した tracker packet の復号結果を `224.5.23.2:11010` で確認する。 |
| `autoref-tigers` | 所有コンテナが稼働し、AutoRef 自身の診断が起動完了を示す。また、この実行で受信・復号した vision `10020`、referee `11003`、tracker `11010` の各入力数が増えたことを確認する。外部から multicast packet を観測しただけでは代用しない。image に受信証跡がなければ、確認用 adapter を実装するまで準備完了にしない。 |
| `ssl-log-recorder` | 所有コンテナが稼働し、この実行で作成した log file があり、後続 reader が vision / referee / tracker の各記録を少なくとも一件ずつ復号できる。 |

各確認には有限の期限を設ける。期限超過、コンテナ終了、復号失敗では準備未完了のまま失敗し、失敗条件と調査ログを記録する。wrapper は各health確認で inspect により所有者ラベル、資源名、期待イメージ、ホストネットワーク、コンテナID、稼働状態を完全照合する。コンテナ内プロセス / サービス診断と新しいパケットはサービス準備の証拠とし、所有権の証拠は当該 inspect 照合とする。外部パケットの受信だけでは送信コンテナの所有証明に代用しない。multicast の確認は選択した IPv4 interface で group join し `SO_REUSEADDR` を使って、受入試験側の受信を妨げない。待受 socket の所有者を確認する方法は初期対象 Linux 上で実証する必要がある。実証できない OS では確認条件を弱めず、未対応として明示する。

Game Controller の準備確認は起動世代内で段階を持つ。初期化中は mode 固有の初期指令を一度観測するまで未準備とし、確認後は「初期状態確認済み」をその資源世代で保持する。起動後は有効な既知の審判指令への遷移を許容し、HALT / STOP へ戻ることは要求しない。継続正常性は所有コンテナと Game Controller プロセスが稼働し、API が応答し、期限内に11003の既知指令を復号受信できることを確認する。未知指令、審判通信 / API 通信断、プロセス / コンテナ終了は未準備とする。container ID または起動時刻が変わる再起動は新世代であり、初期状態確認済みを破棄する。新世代は外部永続状態を使わない一時設定で起動し、mode 固有の初期指令を再確認するまで依存資源を準備完了にしない。

既定の `visibility_graph` 構成では次の依存グラフを使う。

1. `simulator` と `game-controller` は依存なしで起動し、並行して準備完了条件を満たす。
2. `cm4-sim` と `duck` はそれぞれ準備完了した `simulator` を待つ。その後は並行して起動できる。
3. `crane` は準備完了した `cm4-sim` と `game-controller`、および既存の Duck 起動通知を待つ。
4. comparison mode の `tracker-tigers` と `tracker-erforce` は個別の wrapper executable resource とし、準備完了した `simulator` と `game-controller` を待つ。各々は owner inspection と固有 process / packet probe の両方を満たす。
5. comparison mode の `debug-host` は Duck の project-start 通知と両 tracker wrapper の ready を待つ。その起動後も、DebugHost comparison health が Duck / TIGERs / ER-Force の三 logical role を別 source identity に解決し、確認窓内の新しい packet を受信するまでは未準備とする。

`cm4-sim` を使わない planner を選択した場合、`crane` は存在しない `cm4-sim` への依存を作らず、`duck` と `game-controller` の既定依存だけを持つ。`duck` は準備完了した `simulator` を待つ。Duck の起動通知はプロジェクトが起動したことだけを示すため、サービス準備完了の根拠には使わない。

移行前の `WaitForStart` と移行後の準備完了確認付き `WaitFor` を混同しない。モデル試験では依存関係と資源ごとの正常性確認を検証し、起動管理プログラムの試験では各条件と期限超過時の動作を検証する。Game Controller は起動時の初期状態検証、初期化後の正常 command 遷移、通信断、process 終了、resource 再起動を別シナリオで検証する。実ネットワーク受入では実際の packet / referee / motion を別途確認する。

## コンテナ所有権と停止契約

AppHost は起動時に stack-run ID を生成し、各起動管理プログラムに一意な resource-run ID を渡す。コンテナはこれらの値、資源名、期待する image、生成した name がすべて一致する場合だけ当該起動管理プログラムの所有物とみなす。起動には `docker run --detach --cidfile ... --name ... --label ... --network host ...` を使う。`--rm`、image filter、wildcard cleanup、stack ID だけを使った広範な削除は使用せず、image cache も削除しない。

起動管理プログラムは Docker CLI と log follower の子プロセスをすべて直接起動し、標準出力・標準エラーを読み切る。取消時は CLI 子プロセスの取消・強制終了と終了待ちを済ませてから後始末する。`docker run` が ID を返す前に取消された場合、daemon 側で作成完了の反映が遅れる可能性を考慮し、所有者・資源ラベルを厳密に指定して期限付きの再検索を行う。期限までに作成状態を確定できない場合は後始末失敗として非ゼロ終了し、残留の可能性を記録する。一度だけのラベル検索で後始末完了とはしない。失敗注入試験には作成反映が遅れるケースを含める。

通常終了、起動失敗、準備完了期限超過、コンテナの予期しない終了では、準備完了を解除し、所有コンテナを停止する。状態・終了値・最終ログを収集した後、所有者情報を再検証した完全なコンテナ ID だけを削除する。停止全体の期限は DCP の実行資源停止上限 15 秒より短くし、子プロセス終了、`docker stop`、`docker rm` に個別の上限を配分する。後始末の遅延・失敗は記録し、成功扱いにしない。SIGKILL、ホスト障害、電源断、Docker daemon 停止時の自動後始末は保証しない。その場合もラベルで残留所有者を識別し、無関係なコンテナには触れない。

## 操作

開発者が覚える起動操作は一つにする。

```sh
aspire run --apphost Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj
```

停止は Aspire の通常操作に従う。SIGINT/SIGTERM による通常停止では各起動管理プログラムが上記期限内に所有コンテナを停止・削除し、Duck の子プロセスも終了する。強制終了、ホスト障害、Docker daemon 不通時のコンテナ後始末は保証しない。残留資源は所有者ラベルで後から識別する。AppHost の dashboard には起動管理プログラムの正常性と転送ログを表示し、Docker inspect と最終状態は受入証跡に残す。

個別資源の再起動とログ確認は Aspire ダッシュボードから行える構成にする。

### 実行モードの選択

AppHost 自身の設定 `Testing:Mode` で起動構成を選ぶ。値は `base`、`comparison`、`match` の三つに限定し、未指定時は `base` とする。未知の値は resource を起動する前に設定エラーとして失敗させる。

- `base`: Simulator、Game Controller、Crane、必要な `cm4-sim`、Duck を起動する。
- `comparison`: `base` に `tracker-tigers`、`tracker-erforce`、`debug-host` を追加する。
- `match`: 対戦用 Simulator / Game Controller fixture、Crane、`tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller`、Duck を起動し、`cm4-sim`、`tracker-tigers`、`tracker-erforce`、`debug-host` は起動しない。

`match` は `comparison` の追加オプションではなく排他的な構成とする。これにより、比較用 tracker と対戦用 Sumatra AI を同時に起動して 11010 やチーム制御の意味を混在させない。

## 設定の責務

AppHost は「何を一緒に起動するか」と「試験環境でどの接続設定を使うか」だけを持つ。

追跡アルゴリズムの設定値、RuntimeHost の処理周期、正式な通信形式は既存の Duck 側設定と設計を正本とする。

コンテナのイメージ版、geometry、realism、Crane の image tag、チーム色、planner、`cm4-sim` の image tag など、試験環境を再現するための値だけを AppHost の設定へ置く。

秘密情報が必要になった場合はリポジトリへ直接保存せず、Aspire のパラメータまたは利用者のローカル設定から渡す。
## 実装単位

実装は次の順に分ける。

### `ASPIRE-002`: AppHost の骨格

`Testing/Duck.Testing.AppHost` を追加し、`duck` だけを `Tracker.RuntimeHost` として起動できる状態にする。

### `ASPIRE-003`: ER-Force シミュレータ image

Crane の現行シナリオ構成と同じ `ghcr.io/ibis-ssl/framework-simulatorcli:<tag>` を使う。これは現在の container-resource 実装で image と simulator 契約を追加した作業を指し、host-network runtime args で動作することを受け入れた意味ではない。移行後は `simulator` wrapper が同じ image を host network で起動し、UDP 10020 の SSL-Vision がホスト上の受信処理へ届くことを確認する。

### `ASPIRE-004`: Crane image と制御経路

現在の実装では `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>` と `cm4-sim` を container resource として接続し、model contract を固定している。最終 topology では各々を個別 wrapper executable resource へ移行し、`visibility_graph` の既存経路を維持する。Crane の mode 4 指令は cm4-sim で simulator 向け mode 3 指令へ変換する。

### `ASPIRE-005`: base-mode 起動試験

この GitHub Actions 上の `ASPIRE-005` 受入試験は base mode に限定し、Simulator、Game Controller、Crane、必要な `cm4-sim`、Duck を対象とする。個別 wrapper への移行、model test / wrapper focused test、独立通常レビューが完了するまで実行しない。comparison mode の tracker / DebugHost 実装・受入は `ASPIRE-006A` 以降と適用される `ASPIRE-NET-003`〜`006` の作業境界とし、`ASPIRE-005` の成功に含めない。現行 `AddContainer` 方式のコンテナ作成失敗を再試行で回避した結果を受入成功として記録しない。

base mode の資源を一括起動し、`game-controller` が 11003 の唯一の referee producer として動作し、シミュレータからの SSL-Vision を Duck が受信し、Duck が `TrackerWrapperPacket` を出力する正常経路を確認する。comparison mode は別の後続 acceptance とする。

Crane を動かす base-mode 試験では、`referee-driver` が `game-controller` の検証済み初期 `HALT` から active command へ遷移させたことを 11003 で確認した後、Crane の指令が `cm4-sim` を経由してシミュレータへ入り、その結果が SSL-Vision と Duck の出力へ反映されることまで確認する。referee 遷移を確認できない場合は game-state fixture の失敗として扱い、UDP 12345 / 12346 の失敗と混同しない。正常遷移後も Game Controller readiness が維持されること、referee/API 通信断で readiness が落ちること、Game Controller resource 再起動後は `HALT` を再検証するまで dependent resource が待機することを focused test で別々に確認する。

### `ASPIRE-006`: 外部トラッカー比較デバッグ

comparison mode の topology、wrapper 資源種別、readiness、依存関係は本設計で定義するが、その実装・受入は base-mode `ASPIRE-005` には含めない。後続 `ASPIRE-006A`〜`006E` が個別 wrapper tracker、DebugHost health / source identity、比較 UI / replay を実装する。TIGERs Sumatra と ER-Force tracker source を Duck と同じ raw vision `224.5.23.2:10020` で動かし、三つの tracker 出力を official tracker multicast `224.5.23.2:11010` へ集約する。

comparison mode では `tracker-tigers` / `tracker-erforce` を完全な owner inspection とサービス固有 readiness を持つ個別 wrapper executable resource とする。`debug-host` は `Tracker.DebugHost` の .NET project resource とし、Duck project-start と両 tracker wrapper ready に依存する。DebugHost 自身の comparison health は Duck / TIGERs / ER-Force の三 logical role の別 source identity と新しい packet を確認するまで not-ready とする。外部 packet の受信だけでは wrapper の所有コンテナ証拠にしない。

ライブでは既存の Split / Overlay を使い、保存後は diagnostics sample tick を共通の選択時点として比較する。物体単位の位置・速度・角度・存在差を数値で確認する詳細設計は `Tracker/Design/Testing/tracker-comparison-debug-design.md` を正本とする。

comparison mode の topology、wrapper 資源種別、readiness、依存関係は本設計で定義するが、その実装・受入は base-mode `ASPIRE-005` には含めない。後続 `ASPIRE-006A`〜`006E` が個別 wrapper tracker、DebugHost health / source identity、比較 UI / replay を実装する。`ASPIRE-006A` は wrapper topology / service readiness / dependency model を固定し、`ASPIRE-006B` は三 role の source identity と同時 packet 到着を focused test で固定する。

### `ASPIRE-006F`: TIGERs vs Crane 対戦資源

`ASPIRE-005` の基本 stack を基礎に、対戦用 Simulator 設定、対戦用 Game Controller fixture、`tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller` を追加する。対戦モードでは `cm4-sim` を除外し、Crane は `team:=ibis`、Sumatra は `--aiBlue`、Duck は 11010 publish 無効として application model test で固定する。

実装は次の子タスクへ分割する。

| ID | 作業 | 主な完了条件 |
| --- | --- | --- |
| `ASPIRE-006F1` | 対戦 fixture の版管理 | Sumatra の `simulation_protocol_fixed.xml` 相当、Game Controller 初期状態、試合時間設定を Duck 側 fixture として追加し、Blue=`TIGERs Mannheim` / Yellow=`ibis` / `FRIENDLY` / 初期 `STOP` を focused test で固定する。 |
| `ASPIRE-006F2` | `match` mode と resource topology | `Testing:Mode=match` の選択、`base` / `comparison` との排他、対戦資源の存在、`cm4-sim` / 比較専用 tracker の非存在を application model test で固定する。 |
| `ASPIRE-006F3` | Simulator / Game Controller 対戦資源 | 対戦用 Simulator 引数、11003 の単一送信元、Game Controller API、fixture mount、固定イメージ参照をアプリケーションモデル試験で固定する。Game Controller の準備完了試験は `base` / `comparison` の `HALT` と `match` の `STOP` を受理し、未知または想定外の指令では未完了とする。 |
| `ASPIRE-006F4` | TIGERs / AutoRef / SSL log 資源 | `tigers-blue`、`autoref-tigers`、`ssl-log-recorder` を個別の Aspire 外部実行資源として登録し、イメージ、ホストネットワーク、10020 / 11003 / 11010、`--aiBlue`、外部 referee 設定、各サービスの準備完了条件と `WaitFor` 依存をアプリケーションモデル試験で固定する。起動管理プログラムの対象試験では条件成立・期限超過と診断根拠を検証する。 |…9 tokens truncated…対戦設定 | Crane の `team:=ibis`、`cm4-sim` 非依存、Duck の 11010 送信無効、および起動管理プログラムの準備完了に基づく依存関係をアプリケーションモデル試験で固定する。 |
| `ASPIRE-006F6` | `match-controller` と試合進行 | GC API / referee / vision の準備完了、STOP / HALT からの継続操作、`POST_GAME` / 最大時間での終了、結果保存を対象試験で固定する。 |

### `ASPIRE-006G`: TIGERs vs Crane 一括対戦試験

`ASPIRE-MATCH-001` から `ASPIRE-MATCH-005` を Linux の実 packet で確認し、双方の active motion、AutoRef / tracker 経路、試合終了、結果・SSL log・Crane の記録データ・resource log を証跡化する。勝敗は合否条件にしない。

一括試験は次の子タスクへ分割する。

| ID | 作業 | 主な完了条件 |
| --- | --- | --- |
| `ASPIRE-006G1` | topology / team / referee 受入 | Linux 上で `ASPIRE-MATCH-001` / `002` を実 packet と実 resource で確認し、resource 一覧、team mapping、11003 producer の一意性を保存する。 |
| `ASPIRE-006G2` | 双方 active motion 受入 | `ASPIRE-MATCH-003` を実行し、同一 active referee 窓で Yellow の Crane と Blue の TIGERs の双方に位置変化があることを確認する。 |
| `ASPIRE-006G3` | AutoRef / tracker 経路受入 | `ASPIRE-MATCH-004` を実行し、Sumatra 11010 出力、AutoRef の 10020 / 11003 / 11010 利用、Duck 11010 非送信を packet count とログで確認する。 |
| `ASPIRE-006G4` | 試合完了 / 証跡受入 | `ASPIRE-MATCH-005` を実行し、`POST_GAME` または最大時間での終了、対戦結果、SSL log、Crane の記録データ、各 resource の標準出力・標準エラーを失敗時も保存する。 |

## テスト方針

実装では TDD を使う。

最初に AppHost のアプリケーションモデルを検査するテストを追加し、未実装状態で失敗することを確認する。
最低限、次を自動検査する。

- `simulator`、`game-controller`、`crane`、`cm4-sim`、`duck` の資源が存在する。
- `duck` が `Tracker.RuntimeHost` を参照する .NET プロジェクト資源である。
- `crane` が `ghcr.io/ibis-ssl/crane` の `scenario-<commit SHA>` image を参照する。
- `simulator` が `ghcr.io/ibis-ssl/framework-simulatorcli`、`cm4-sim` が `ghcr.io/ibis-ssl/orion-cm4-sim` の固定タグを参照する。
- `simulator`、`game-controller`、`crane`、`cm4-sim` が個別 wrapper executable resource として登録され、Duck は project resource のままである。各 wrapper の image、entrypoint、引数、host network、`-p` 不使用、ownership labels を確認する。DCP `ContainerResource` を併用しない。
- Duck に `sim` 用の VisionReceiver 設定が渡される。
- シミュレータの geometry と realism が明示される。
- Crane の `team`、`planner` と `cm4-sim` の接続ポートが明示される。
- `game-controller` が固定 tag または digest の image を参照し、11003 の唯一の referee producer として構成される。
- comparison mode の `tracker-tigers` が外部 Game Controller 用設定を使い、`gameController=false` と `publishRefereeMessages=false` で 11003 を受信専用にする。
- comparison mode の `tracker-tigers` / `tracker-erforce` が個別 wrapper executable resource として登録される。両者は期待 owner labels、resource 名、image、host network、完全 container ID、Running 状態を inspect で一致確認し、コンテナ内の期待 process / service 起動診断 / source identity に対応する新しい tracker packet を確認するまで ready にならない。
- comparison mode で両 tracker が ready な Simulator と Game Controller に health-based `WaitFor` し、11003 producer を追加しない。`debug-host` は Duck project-start と両 tracker ready を待つ。DebugHost 自身の comparison health は Duck / TIGERs / ER-Force の三 source identity を区別して新 packet を確認するまで ready としない。
- `referee-driver` の integration fixture が起動世代ごとの mode 固有初期 command（base / comparison は `HALT`、match は `STOP`）を確認してから Game Controller API へ continue action を送り、active command への遷移後も正常 health が続くことを確認できる。
- Game Controller readiness は初期状態を世代ごとに一度確認する。その後の有効な referee command 遷移を許容し、referee / API 通信断、未知 command、process / container 終了では not-ready となる。resource 再起動時は初期検証状態を reset し、期待 command の再確認まで downstream を待たせる。
- 各 wrapper に `/health/live` とサービス固有 predicate の `/health/ready` check が設定される。開始依存だけの `WaitForStart` を readiness 条件として使わない。
- 既定の `visibility_graph` 構成で、`cm4-sim` と `duck` が ready な `simulator` を待ち、`crane` が ready な `cm4-sim` / `game-controller` と Duck project-start を待つ。
- `cm4-sim` を使わない planner 構成では、`crane` が `duck` と `game-controller` の必要な依存だけを持ち、存在しない `cm4-sim` への待機依存を持たない。
- 対戦モードでは `tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller` が追加され、`cm4-sim` と比較専用 `tracker-tigers` は存在しない。
- 対戦モードの Simulator 起動引数、Blue=`TIGERs Mannheim` / Yellow=`ibis` の team mapping、Crane の `team:=ibis`、Sumatra の `--aiBlue` が一組の設定として固定される。
- 対戦用 Sumatra 設定が 10020 の vision、11003 の external referee、`gameController=false`、11010 の tracker output を持つ。
- 対戦モードの Duck は 11010 への tracker publish が無効で、AutoRef の tracker 入力へ Duck source を混在させない。
- `match-controller` が参加資源の開始後に起動し、Game Controller API、11003、10020 の実データを確認してから試合を開始する。
- comparison mode の `tracker-tigers` / `tracker-erforce` は各 owner labels、資源名、image、network、container ID、Running 状態を確認し、コンテナ内の期待 process / 起動診断と identity-matched の新しい tracker packet が揃うまで ready にならない。
- comparison mode の `debug-host` は Duck project-start と両 tracker ready を待つ。さらに DebugHost 自身の comparison health が Duck / TIGERs / ER-Force の三 role を異なる source identity として解決し、新しい packet を受信するまで ready にしない。

Wrapper unit test は fake Docker executable を使い、cancellation 中の遅延 create、CID 未取得時の exact-owner bounded discovery、ラベル不一致、child-process reap、log follower cancellation、foreign container 非変更、base / match / comparison wrapper の readiness 成功・timeout・service 診断根拠を検証する。Game Controller は startup baseline latch と継続 health、正常 active 遷移、通信断・process 終了、resource 再起動後の初期状態再検証を別シナリオで検証する。Comparison model test は tracker wrapper / DebugHost の resource kind、完全 owner inspection と固有 readiness profile、Simulator / Game Controller / Duck / tracker readiness に対する依存辺を固定する。DebugHost focused test は三 role の source 解決と同時 packet 到着を検証する。実ネットワーク・active motion の acceptance は model test で代用しない。

Docker を必要とする一括起動試験は、AppHost のモデル検査と分離する。Docker が利用できない環境でも、アプリケーションモデルの退行を検出できるようにする。

一括起動試験では、単に全資源が `Running` になっただけで成功としない。SSL-Vision の受信と Duck のトラッカーパケット出力までを試験証跡に含める。

stack ownership の focused test では、一つ目の AppHost が lock を保持している間は二つ目が resource 起動前に失敗し、最初の AppHost 終了後は同じ lock file が残っていても次の起動が成功することを固定する。`10020` / `11010` の bind 可否はこの判定に使わない。占有制御 port の外部競合は別テストで事前検出を固定する。

`RuntimeVisionReceiverService` の focused test では、有効な SSL-Vision packet を decode して buffer へ渡すたびに `VisionPacketsReceivedTotal` が増加し、診断ログから endpoint、interface、累積値を取得できることを先に固定する。decode に失敗した UDP packet はこの正常受信 counter へ加算しない。

## 対応 OS の動作確認仕様

クロスプラットフォーム対応は、.NET のビルド成功だけでは完了としない。Docker の host network と UDP multicast が実際の開発ホストで成立することを確認してから、その OS を対応済みとして扱う。

確認対象は次の三環境とする。

- Linux + Docker Engine。
- Windows + Docker Desktop の Linux containers。host networking を有効にする。
- macOS + Docker Desktop の Linux containers。host networking を有効にする。

未実施の OS は「未確認」とし、他 OS の成功結果を代用しない。

### 必須確認項目

| ID | 経路 | 合格条件 |
| --- | --- | --- |
| `ASPIRE-NET-001` | AppHost 起動 | Simulator、`game-controller`、Crane、`cm4-sim`、Duck が起動し、要求した比較資源も起動できる。 |
| `ASPIRE-NET-002` | container → host SSL-Vision multicast | Simulator が `224.5.23.2:10020` へ送信し、ホスト上の `Tracker.RuntimeHost` が継続受信する。安定起動後5秒以内に RuntimeHost receiver 自身の `VisionPacketsReceivedTotal` が10件以上増加する。 |
| `ASPIRE-NET-003` | 同一 multicast の複数受信 | comparison mode では `Tracker.RuntimeHost` と `Tracker.DebugHost` が同時に `224.5.23.2:10020` を受信し、RuntimeHost の `VisionPacketsReceivedTotal` と DebugHost の raw input packet count が同じ確認窓でともに増加する。 |
| `ASPIRE-NET-004` | container → container SSL-Vision multicast | TIGERs と ER-Force が同じ `224.5.23.2:10020` を受信し、それぞれ tracker output を生成する。 |
| `ASPIRE-NET-005` | container → host tracker multicast | TIGERs / ER-Force が `224.5.23.2:11010` へ送信し、`Tracker.DebugHost` が両者を別 source として受信する。 |
| `ASPIRE-NET-006` | host → host tracker multicast | Duck が `224.5.23.2:11010` へ送信し、`Tracker.DebugHost` が Duck source として受信する。 |
| `ASPIRE-NET-007` | host network の制御 UDP | `referee-driver` が 11003 の command を `HALT` から active state へ遷移させたことを確認した後、Crane → `cm4-sim` の UDP 12345 と `cm4-sim` → Simulator の UDP 12346 が通り、ロボット指令の結果が SSL-Vision の位置変化へ反映される。referee 遷移未成立はこの UDP 経路の成功証跡にしない。 |
| `ASPIRE-NET-008` | multicast interface 選択 | `InterfaceAddress` 未指定の通常経路を確認し、複数 NIC / VPN 等で自動選択が成立しない場合は明示 IPv4 address の指定で受信できることを確認する。 |
| `ASPIRE-NET-009` | stack ownership / 固定 port 競合 | 同一ホストで二つ目の stack を起動しようとした場合は AppHost の stack ownership lock を resource 起動前に取得できず明示的に失敗する。外部プロセスとの port 競合は占有制御 port だけを事前確認し、共有 multicast port `10020` / `11010` の in-use 判定は失敗条件にしない。 |

`Tracker.RuntimeHost` には OS 受入用の production diagnostics として、正常に decode して `RuntimeVisionPacketBuffer` へ渡した SSL-Vision packet の累積値 `VisionPacketsReceivedTotal` を追加する。receiver service が値を単調増加させ、起動時と一定間隔の診断ログに endpoint、選択 interface、累積値を出力する。`ASPIRE-NET-002/003` はこのログの確認前後差分を RuntimeHost の受信数として使う。独立 packet sniffer の count はネットワーク補助証跡には使えるが、RuntimeHost が受信した packet count の代用にはしない。DebugHost 側は既存の raw input snapshot が持つ packet count を使う。

packet count の条件は、単に socket が作成できたことではなく実 packet が継続して届いていることを確認するための最低条件とする。

### OS ごとの証跡

各 OS の確認では、少なくとも次を記録する。

- OS 名と version。
- Docker Engine / Docker Desktop の version。
- Docker Desktop の場合は host networking の有効状態。
- 使用した IPv4 interface 一覧と、明示した `InterfaceAddress`。
- Aspire の wrapper resource 状態 (`live` / `ready` / `failed`) と Duck project 状態。
- 各 container の解決済み image reference、ID、`NetworkMode=host`、ownership labels、終了 code、inspect と最終 log。
- graceful AppHost shutdown の開始時刻、各 wrapper cleanup の所要時間、DCP 15秒上限内の完了可否。
- Simulator、`game-controller`、Crane、`cm4-sim`、TIGERs、ER-Force の標準出力・標準エラー。
- `referee-driver` が観測した 11003 の遷移前後 command と Game Controller API へ送った continue action。
- `Tracker.RuntimeHost` / `Tracker.DebugHost` の標準出力・標準エラー。
- RuntimeHost の `VisionPacketsReceivedTotal` と DebugHost の raw input packet count の確認前後値。tracker packet は DebugHost の source ごとの受信数を併記する。
- DebugHost が認識した tracker source identity。
- stack ownership lock の取得結果と、占有制御 port の競合検出結果。共有 multicast port `10020` / `11010` は in-use 判定の対象外であることも記録する。

Linux は自動統合試験を基本とする。Windows / macOS は Docker Desktop が必要なため、専用 runner または手動の受入試験でもよいが、実機の成功証跡を残すまで対応済みとは扱わない。

Windows / macOS で container と host の multicast が成立しない場合、別 OS の成功を代用せず失敗として記録する。UDP relay / gateway や unicast 化は後続設計として検討し、試験中に暗黙の fallback を入れない。


### 自動統合試験による一括起動の受入

`ASPIRE-NET-002` の手動試験は模擬装置から Duck の実行プログラムへの通信経路だけを検査する。`ASPIRE-NET-001` と `ASPIRE-NET-007` を受け入れる一括起動試験は既存の `.github/workflows/dotnet-test.yml` へ追加する。手動実行に加え、作業用の分岐名 `task/pr28-aspire-005` に更新を送った場合も通常テスト成功後に起動する。これにより作業中の実装を一度実測でき、実行定義ファイルが標準の変更履歴へ取り込まれる前からも手動実行できる。新しい定義ファイルは作らない。

この試験は標準の仮想計算機で Docker と同じ計算機の通信網を共有する設定を使う。標準実行環境の処理装置数、主記憶量、保存容量は公式資料に記載されている。Docker を扱えない軽量実行環境は対象外とする。既存の通信試験が成功していても、全資源を起動する場合に保存容量が足りる証拠にはならない。最初の一括起動試験では、Docker の配布物取得前後と試験終了時の空き容量を記録し、実際に起動した資源の容量も調べる。

CI では検証対象の管理プログラムを実行する。`Testing:StackOwnership:LockPath` は `$RUNNER_TEMP` 配下に置き、実行識別番号と再試行回数を含める。この試験が起動した資源だけを停止・削除し、Docker 全体へ作用する一括削除はしない。後片付けと診断収集は成功・失敗どちらでも実施する。管理プログラムの出力、Docker の情報、各資源の設定情報とログ、受入テストが観測した審判状態・操作と映像・追跡データの受信数、試験結果を成果物として保存する。各待機には有限の時間制限を設ける。空き容量が不足した場合も、広範囲な削除で .NET SDK などの開発ツールを壊さない。

審判管理プログラム 3.20.3 の Git 固定版 `8050f232c3130323bbd91d1d3d56e9553506c8e4` では、新規の試合状態と指令は `HALT` で初期化される。起動時に `config/state-store.json.stream` が存在すれば、その保存状態が復元される。このため各 resource 世代で新しい実行単位を作り、設定や状態を外部保存領域へ永続化しないことを試験用初期条件とする。base / comparison mode の期待初期状態は空の保存先からの `HALT`、match mode は版管理した対戦 fixture の `STOP` とし、いずれも UDP 11003 で実測する。startup readiness は mode 固有の期待 command を世代ごとに一度確認し、初期化後の継続 health はこの command との一致を要求せず、known referee command の期限内受信、API 応答、process / owner container 稼働を確認する。active command へ遷移した後に初期 command 不一致だけで not-ready にしない。通信断、API 不通、未知 command、process / container 終了は not-ready とする。resource 再起動では前世代の初期確認を破棄し、次世代の期待初期 command を再検証する。受入テストが期待状態と異なる初期指令を観測した場合は、UDP 12345 / 12346 の確認へ進まず、審判状態の準備失敗として報告する。

実行環境の容量は固定で、利用可否や費用を確認せずに大容量環境を前提にしない。Docker の配布物を展開した後の容量は、圧縮状態の合計からは分からないため、初回実測値、最小空き容量、実行時間を成果物へ記録する。複数回の測定後に実行環境を決める。Docker の常駐処理の設定変更はこの試験に含めない。

根拠: [標準実行環境の仕様](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)、[大容量実行環境の概要](https://docs.github.com/en/actions/concepts/runners/github-hosted-runners)、[審判管理プログラムの起動処理](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/cmd/ssl-game-controller/main.go)、[初期状態の定義](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/state/state.go)、[状態保存からの復元処理](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/engine/engine.go)。

## トラッカー比較デバッグ

通常の Duck + Crane base mode とは別に comparison mode を用意する。比較用 tracker は後続 `ASPIRE-006A` で個別 wrapper resource として追加し、`Tracker.DebugHost` は .NET project resource とする。

comparison mode は同じ raw vision を Duck / TIGERs / ER-Force へ与え、`Tracker.DebugHost` で三者の official tracker packet を比較する。両外部 tracker wrapper の owner inspection / 固有 readiness と DebugHost の Duck project-start / tracker-ready dependency、三 source comparison health を要求する。外部 tracker が利用できない場合に通常の base-mode simulation acceptance まで停止させない。

比較の詳細、source identity、時刻対応、ball / robot の対応付け、数値差分、CaptureOn / replay の契約は `Tracker/Design/Testing/tracker-comparison-debug-design.md` に定義する。

## TIGERs vs Crane 対戦モード

TIGERs の AI と Crane を実際に対戦させる対戦モードを、トラッカー比較とは独立した AppHost の構成として用意する。設計の基準は 2026-09-29 時点の `ibis-ssl/crane` develop `af6e0d3dec745415ce060ff5de2042afd3ec5145` にある `docker/match-vs-tigers/docker-compose.yaml`、`simulation_protocol_fixed.xml`、`match_controller_pb.py` とする。

トラッカー比較の `tracker-tigers` は AI を起動しないため、対戦モードには流用しない。対戦用 Sumatra は別資源 `tigers-blue` とし、Crane の現行対戦構成と同じく Blue 側 AI として起動する。

| 資源名 | 実行形態 | 対戦モードでの責務 |
| --- | --- | --- |
| `simulator` | Aspire 外部実行資源＋Docker コンテナ | ER-Force `simulator-cli` をホストネットワークで起動する。対戦用引数は Crane の現行構成を基準にし、`-g 2020 --realism None --ibis-use-referee --ibis-feedback-team-name ibis --ibis-referee-port 11003` を使う。 |
| `game-controller` | Aspire 外部実行資源＋Docker コンテナ | 11003 の唯一の referee 送信元とし、対戦用の初期状態を読み込む。 |
| `crane` | Aspire 外部実行資源＋Docker コンテナ | 固定した `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>` を使い、`sim:=true speak:=false team:=ibis` で Yellow 側を制御する。 |
| `tigers-blue` | Aspire 外部実行資源＋Docker コンテナ | 固定した tag または digest の `tigersmannheim/sumatra` を `--headless --aiBlue --visionAddress 224.5.23.2:10020 --refereeAddress 224.5.23.1:11003 --matchStats --moduli simulation_protocol` で起動する。 |
| `autoref-tigers` | Aspire 外部実行資源＋Docker コンテナ | `tigersmannheim/auto-referee:1.2.0` を起動し、vision 10020、referee 11003、tracker 11010 を使って試合判定を Game Controller へ返す。 |
| `ssl-log-recorder` | Aspire 外部実行資源＋Docker コンテナ | referee 11003、vision 10020、tracker 11010 を対戦記録として保存する。 |
| `match-controller` | 試験 fixture | Game Controller API、referee、vision の準備完了を確認し、試合開始・停止状態からの継続・終了監視・結果保存を行う。 |
| `duck` | .NET プロセス | SSL-Vision の観測と Duck 側デバッグを継続する。ただし対戦判定へ影響を与えないよう、対戦モードでは official tracker multicast 11010 への publish を無効にする。 |

対戦モードでは基本 stack の `visibility_graph` 用 `cm4-sim` を起動しない。Crane の現行 `match-vs-tigers` 構成は `cm4-sim` を含まず、対戦用 Simulator と Sumatra 設定を一体として使っているため、基本 mode の `--ibis-port 12346` / UDP 12345 / 12346 の経路を対戦モードへ混在させない。

Sumatra へ渡す `simulation_protocol_fixed.xml` 相当の fixture は Duck 側で版管理し、少なくとも raw vision `224.5.23.2:10020`、referee `source=NETWORK` / port `11003` / `gameController=false`、`SumatraSimBotManager`、tracker output `224.5.23.2:11010` を固定する。Crane リポジトリを AppHost 起動時に clone して fixture を取得する方式にはしない。

Game Controller の初期状態も Duck 側の対戦 fixture として版管理する。初期対戦は Crane の現行構成に合わせ、Blue team name を `TIGERs Mannheim`、Yellow team name を `ibis`、match type を `FRIENDLY` とする。Crane の `team:=ibis` と Sumatra の `--aiBlue` はこの team mapping と一組の契約として扱い、一方だけを変更しない。

Crane の現行 compose は Sumatra、Game Controller、SSL log recorder に移動タグを含むが、Duck の再現可能な試験では暗黙の `latest` を使わない。AppHost 設定で image tag または digest を明示し、実行証跡へ解決済み image reference を保存する。Crane image は既存の `scenario-<commit SHA>` を既定とし、Crane の match workflow が同じ scenario image を `match-<commit SHA>` へ再タグ付けして利用できる構成と整合させる。

### 対戦モードの起動順序

1. `simulator` と `game-controller` を開始する。
2. `duck` はプロジェクト起動通知を公開する。`tigers-blue` と `ssl-log-recorder` は準備完了した `simulator` / `game-controller` を待つ。
3. `autoref-tigers` は準備完了した `simulator`、`game-controller`、`tigers-blue` を待つ。
4. `crane` は準備完了した `simulator` と `game-controller` を待つ。対戦モードでは存在しない `cm4-sim` への依存を作らない。
5. `match-controller` は準備完了した `simulator`、`game-controller`、`crane`、`tigers-blue`、`autoref-tigers`、`ssl-log-recorder` と Duck のプロジェクト起動後に起動する。
6. 依存を満たした後も `match-controller` は Game Controller API 接続、11003 の referee state、10020 の SSL-Vision、tracker `11010` を実測し、HALT/STOP の初期状態を確認してから試合継続指令を送る。資源が起動しただけでは試合を開始しない。

`match-controller` は Crane の現行試合 controller を基準に、`HALT` / `STOP` から利用可能な continue action を選び、必要に応じて `NEXT_COMMAND`、`NORMAL_START`、`FORCE_START` を使って試合を進行させる。終了条件は `POST_GAME` または設定した最大試合時間とし、結果には少なくとも両チームの得点、終了理由、`CRANE WIN` / `TIGERs WIN` / `DRAW` のいずれかを保存する。

勝敗そのものは CI の合否条件にしない。両 AI が同一試合へ参加し、試合が規定の終了条件まで進行し、結果と診断証跡を生成できることを対戦機能の正常条件とする。

### 対戦モードの受入項目

| ID | 確認内容 | 合格条件 |
| --- | --- | --- |
| `ASPIRE-MATCH-001` | resource model | `simulator`、`game-controller`、`crane`、`tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller` が存在し、各 Docker resource は wrapper / readiness profile を持つ。model test は readiness-based `WaitFor` 辺を固定し、`cm4-sim` と比較専用 `tracker-tigers` は起動対象に入らない。 |
| `ASPIRE-MATCH-002` | team / referee 契約 | Blue=`TIGERs Mannheim`、Yellow=`ibis`、Crane=`team:=ibis`、Sumatra=`--aiBlue` が一致し、11003 の producer は `game-controller` 一つだけである。mode-specific readiness は `match` の初期 `STOP` を確認する。 |
| `ASPIRE-MATCH-003` | 双方の active motion | active referee state の同一確認窓で、SSL-Vision 上に Yellow の Crane robot と Blue の TIGERs robot の位置変化がそれぞれ観測できる。片側だけの移動では合格にしない。 |
| `ASPIRE-MATCH-004` | AutoRef / tracker 経路 | `tigers-blue` が 11010 へ tracker packet を出力し、`autoref-tigers` が 10020 / 11003 / 11010 を使って active に動作する。Duck は 11010 へ publish せず、AutoRef の tracker 入力へ別 source を混在させない。 |
| `ASPIRE-MATCH-005` | 試合完了と証跡 | `POST_GAME` または最大試合時間で終了し、対戦結果、全 resource の stdout / stderr、SSL log、Crane の記録データ、Game Controller / AutoRef / Sumatra の診断情報を保存できる。 |

Linux の自動統合試験では `ASPIRE-MATCH-001` から `005` を対戦モードの受入条件とする。Windows / macOS で対戦モード対応を表明する場合も、各 OS 上で同じ受入項目を実 packet で確認するまで対応済みとは扱わない。

## 診断

Aspire ダッシュボードの資源別ログを一次確認に使う。

自動試験を CI へ追加する場合は、既存の `.NET tests` と同様に、失敗時の標準出力、標準エラー、テスト結果、Aspire とコンテナのログを artifact として保存する。

シミュレータ、`game-controller`、Crane、`cm4-sim` のコンテナログは資源名が分かる形で分離する。`ASPIRE-NET-007` では `referee-driver` の操作ログと 11003 の command 遷移も同じ試験証跡へ保存する。

対戦モードではこれに加えて `tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller` の標準出力・標準エラー、対戦結果、SSL log、Crane の記録データ、解決済み image reference、team mapping、試合時間設定を保存する。失敗時も途中まで生成された結果と各 resource のログを破棄しない。

## 対象外と段階境界

初期 `ASPIRE-005` base-mode 実装・受入には次を含めない。

- Duck 自体の Docker 化。
- 本番環境の配置方式。
- Kubernetes への配置。
- Issue #14 の comparison mode 実装・実 packet 受入。comparison の topology / readiness 契約は本設計で定義し、wrapper / DebugHost 実装は `ASPIRE-006A` 以降、source identity / UI / replay は `ASPIRE-006B`〜`006E`、Linux 実 packet 受入は `ASPIRE-007A` で行う。
- 自動レフェリーの実装。
- AI のアルゴリズム変更。
- 既存の `Tracker.RuntimeHost` の通信形式変更。
- Docker bridge network 越しの SSL-Vision マルチキャスト対応。

## 後続段階での comparison mode

Issue #14 の比較試験では、`tracker-tigers` と `tracker-erforce` を Aspire の個別 wrapper executable resource とし、各 wrapper が固定 image の host-network container を所有する。DCP `ContainerResource` へ戻さない。各資源は完全な owner inspection、コンテナ内の期待 process / 起動診断、期待 source identity の新しい packet を ready 条件とする。`debug-host` は `Tracker.DebugHost` の .NET project resource として Duck project-start と両 tracker wrapper ready を待つ。自身の comparison health も三 role の source identity と packet arrival を確認するまで ready としない。

`ASPIRE-006A` は topology / health model、`006B` は source identity と三者同時受信、`006C`〜`006E` は差分・表示・replay、`ASPIRE-007A` は Linux 実 packet acceptance を担当する。比較用 tracker を追加しても base-mode の `duck`、`simulator`、`crane`、`cm4-sim` 契約は変えない。

将来 bridge network へ移行する必要が出た場合は、ER-Force の SSL-Vision を任意の宛先へ転送する明示的な中継資源を追加するか、シミュレータ側の送信先指定機能を追加する。暗黙のマルチキャスト転送には依存しない。

## 完了条件

この設計の移行完了条件は次のとおりとする。既存 `AddContainer` 実装が存在することや、model test で resource が登録されたことだけでは完了としない。

- 一つの Aspire AppHost 起動でシミュレータ、`game-controller`、Crane、必要な `cm4-sim`、Duck を管理できる。四つの Docker service は個別の executable wrapper resource であり、wrapper が host network container を所有する。Duck は `AddProject` のままとする。
- `ASPIRE-005` の完了は base mode のみを意味する。comparison mode の完了は `ASPIRE-006A`〜`006E` および適用される `ASPIRE-007A` の受入後に別判定する。comparison mode では TIGERs / ER-Force を個別 wrapper resource、`Tracker.DebugHost` を .NET project resource とし、各 tracker の owner evidence / service readiness、Duck 起動通知・両 tracker ready への DebugHost 依存、三 source identity と新 packet を用いる comparison health を満たす。
- 対戦モードでは `tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller` を追加し、Blue の TIGERs AI と Yellow の Crane を同じ Game Controller / Simulator 上で対戦させられる。
- 対戦モードでは `cm4-sim` を起動せず、Duck の 11010 publish を無効にして AutoRef の tracker 入力へ干渉しない。
- `ASPIRE-MATCH-001` から `ASPIRE-MATCH-005` により、両チームの active motion、AutoRef / tracker 経路、試合終了、結果と診断 artifact を確認できる。
- シミュレータと Crane は Docker コンテナ、Duck はホスト上の .NET プロセスとして起動する。
- Crane は `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>` の固定 image tag から起動し、Duck 側ではビルドしない。
- Duck は既存の `sim` 設定と SSL-Vision 契約を維持する。
- 開発者が各資源の起動コマンドを個別に管理しなくてよい。
- 全 wrapper の live/ready/failed state と stdout/stderr を Aspire dashboard で確認でき、container inspect・ID・最終 log は acceptance artifact から確認できる。DCP first-class container details の欠如は文書化されている。
- Fake Docker CLI による wrapper test が create/start、遅延 cidfile、ownership mismatch、unexpected exit、startup/readiness/log-follow cancellation、遅延 create race、foreign container 非変更、15秒未満の正常停止・cleanup を検証する。
- Game Controller focused tests は mode 固有の初期 `HALT` / `STOP` を世代ごとに一度検証する。その後の正常 active 遷移は health failure とせず、通信断・API 不通・未知 command・process 終了では unhealthy とする。resource 再起動時には初期検証を reset する。
- comparison model / wrapper / DebugHost tests は `ASPIRE-006A` / `006B` の resource kind、owner evidence、service readiness、依存 graph、三 source identity / packet health を固定する。Linux の比較 packet 受入は `ASPIRE-007A` まで未完了とし、`ASPIRE-005` をもって comparison 完了とはしない。
- Linux hosted acceptance で cm4-sim UDP 12345 listener を正確な container process に結び付ける readiness probe を実証する。実装できない場合は設計 gate を解除せず、readiness を弱めない。
- SIGINT/SIGTERM の各停止経路で wrapper が DCP の15秒 stop ceiling 内に cleanup を完了する。SIGKILL、host loss、daemon outage は保証外として記録する。
- SSL-Vision 受信と Duck のトラッカーパケット出力を含む正常経路を確認できる。
- Linux、Windows Docker Desktop、macOS Docker Desktop について `ASPIRE-NET-001` から `ASPIRE-NET-009` の適用項目を確認し、未確認 OS を対応済みと表現しない。
- 実装と試験の証跡を報告書へ残し、PR の最新コミットと同じ SHA の CI だけを最終確認に使う。
## 参照

- GitHub Issue #18 `Aspire対応`。
- GitHub Issue #14 `dockerでシミュレーターのケースを追加`。
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`。
- `Tracker/Tracker.RuntimeHost/appsettings.json`。
- ER-Force Framework の `simulator-cli` 実装と README。
- `ibis-ssl/crane` の `docker/Dockerfile`、`.github/workflows/docker_build.yaml`、`docker/scenario/docker-compose.yaml`、`docker/dev/docker-compose.yaml`、`docker/match-vs-tigers/docker-compose.yaml`、`docker/match-vs-tigers/config/simulation_protocol_fixed.xml`、`docker/match-vs-tigers/config/state-store-initial.json.stream`、`docker/match-vs-tigers/scripts/match_controller_pb.py`、`.github/workflows/match-vs-tigers.yaml`。対戦モード設計の確認時点は develop `af6e0d3dec745415ce060ff5de2042afd3ec5145`。
- `Tracker/Design/Testing/tracker-comparison-debug-design.md`。
- RoboCup SSL simulation protocol。
- Aspire の AppHost、コンテナ、.NET プロジェクト資源、コンテナ実行引数の公式文書。
- Docker の host network driver の公式文書。
