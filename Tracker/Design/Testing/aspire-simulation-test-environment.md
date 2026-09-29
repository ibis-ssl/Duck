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

`Tracker.RuntimeHost` は `VisionReceiver` 設定から SSL-Vision の UDP 入力を受け、既定の `sim` 設定では `224.5.23.2:10020` を受信する。
ER-Force の `simulator-cli` は SSL simulation protocol の制御入力を受け、SSL-Vision の状態を UDP 10020 へ送信する。通常時の送信先は `224.5.23.2` で、`--localhost` 指定時は `127.0.0.1` となる。

SSL simulation protocol の既定ポートは次のとおりである。

- シミュレーション制御: UDP 10300。
- 青チーム制御: UDP 10301。
- 黄チーム制御: UDP 10302。

現在の Duck には Aspire の AppHost は存在しない。

## 基本方針

Aspire は試験用オーケストレータとしてのみ使用する。製品コードの実行責務を `Tracker.RuntimeHost` から AppHost へ移さない。

AppHost は `Testing/Duck.Testing.AppHost` に置く。Duck は既存の `Duck.slnx` に含まれるため、C# のプロジェクト型 AppHost とし、`ProjectReference` と `AddProject<Projects.Tracker_RuntimeHost>` で起動する。

パス指定の `AddDotnetProject` は現行 Aspire では試験的 API のため、初期実装では採用しない。

シミュレータと Crane はコンテナ資源として AppHost に登録する。Crane は Duck 側でビルドせず、`ibis-ssl/crane` が GitHub Container Registry へ公開する `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>` を `AddContainer` で起動する。`scenario-develop` は動作確認用の移動タグとして明示指定時だけ利用し、再現可能な試験では使用する Crane のコミット SHA に対応するイメージタグを固定する。
## 資源構成

AppHost では三つの主要資源に加え、レフェリー / game-state を供給する一つの試験資源と、Crane の現在のシミュレーション経路を維持する場合に一つの補助資源を管理する。

| 資源名 | 実行形態 | 責務 |
| --- | --- | --- |
| `simulator` | Docker image | Crane の現行シナリオ構成と同じ `ghcr.io/ibis-ssl/framework-simulatorcli:<tag>` から ER-Force `simulator-cli` を起動し、物理シミュレーションと SSL-Vision 出力を行う。 |
| `game-controller` | Docker image | `robocupssl/ssl-game-controller:<fixed tag or digest>` を host network で起動し、通常 mode / comparison mode / match mode のすべてで `224.5.23.1:11003` の referee message を生成する唯一の authoritative producer とする。制御 API は `127.0.0.1:8082` を使う。 |
| `crane` | Docker image | `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>` から Crane を起動し、ロボット制御指令を生成する。 |
| `cm4-sim` | Docker image | `ghcr.io/ibis-ssl/orion-cm4-sim:<commit SHA>` を使い、Crane が `visibility_graph` で出す mode 4 の位置指令を現在の Crane シナリオ構成と同じ経路で mode 3 の速度指令へ変換する補助資源。 |
| `duck` | .NET プロセス | `Tracker.RuntimeHost` を `sim` 設定で起動し、SSL-Vision を追跡してトラッカーパケットを出力する。 |

Aspire のダッシュボードは、AppHost が管理する各資源の起動状態、終了状態、標準出力、標準エラーを一か所で確認する用途に使う。

## ネットワーク方針

初期実装は host network を前提とする。

理由は、ER-Force の `simulator-cli` が SSL-Vision の送信先として任意の IP アドレスを指定する起動引数を持たず、通常のマルチキャスト送信か `127.0.0.1` 送信だけを選べるためである。

シミュレータを通常の Docker bridge network に置いた場合、`--localhost` はコンテナ自身を指し、ホストで動く Duck には届かない。マルチキャストを bridge network とホスト間で透過させる構成にも依存しない。

そのため `simulator`、`game-controller`、`crane`、`cm4-sim` は Docker の host network で起動し、Duck はホスト上で従来どおり SSL-Vision のマルチキャストへ参加する。
Linux の Docker Engine では host network はコンテナとホストのネットワーク名前空間を共有するため、UDP のポート公開や変換を挟まずに通信できる。

Docker Desktop を使う場合は host networking の有効化が必要である。初期受入環境は Linux の Docker Engine とし、Docker Desktop での動作は別途確認項目とする。

host network では SSL-Vision と tracker multicast、採用するシミュレータ構成の制御ポートをホスト全体で共有する。ポートは用途で分け、`10020` と `11010` は複数 receiver が `SO_REUSEADDR` を使って同時受信する共有 multicast port とする。一方、UDP 10300 / 10301 / 10302 のうち構成で有効にする listener と、Crane 経路の UDP 12345 / 12346 は一つの stack が占有する制御 port とする。`10020` / `11010` の単純な bind 可否や in-use 判定を二重 stack の検出には使わない。

同一ホストで同じ Duck Aspire stack を二つ起動すること自体は、AppHost が resource 起動前に取得して終了まで保持する host-local の stack ownership lock で拒否する。初期実装は一意な lock file を `FileShare.None` で開いた handle を保持する方式とし、ファイルの存在だけでは失敗としない。二つ目の AppHost が lock を取得できなければ resource を起動せず明示的に失敗する。別プロセスとの競合確認は占有制御 port だけを対象とし、共有 multicast port は実 packet の複数受信試験で検証する。Aspire の `--isolated` を指定しても、この ownership と固定 UDP port は分離されないものとして扱う。

Crane の現在のシナリオ構成では、`visibility_graph` を使う場合に `crane` が mode 4 の位置指令を UDP 12345 へ送り、`cm4-sim` が実機 CM4 相当の位置制御を行って mode 3 の速度指令を UDP 12346 へ転送する。したがって Crane を既存 image の現在の挙動のまま組み込む初期構成では、汎用 SSL simulation protocol の 10301 / 10302 へ直接送る経路へ置き換えない。

Aspire からは `WithContainerRuntimeArgs("--network", "host")` 相当を使ってコンテナ実行時のネットワーク方式を指定する。host network 使用時は Docker の `-p` 相当のポート公開を併用しない。

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

シミュレーション制御を行う試験ツールが必要な場合は UDP 10300 を使用する。初期 AppHost 自身は自律的な試験シナリオを生成しないが、`ASPIRE-005` / `ASPIRE-NET-007` の active motion 確認では専用の `referee-driver` 試験 fixture を使う。`referee-driver` は `224.5.23.1:11003` を直接 publish せず、`game-controller` の `ws://127.0.0.1:8082/api/control` へ continue action を送る。最初に 11003 で `HALT` を確認し、`NEXT_COMMAND` を送り、遷移後も `HALT` / `STOP` なら `FORCE_START`、それ以外の準備状態なら `NORMAL_START` を送る。11003 で active command への遷移を確認してから Crane の指令と SSL-Vision の位置変化を検査する。

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

## 起動順序

Aspire の起動順序は `WaitForStart` による開始依存として明示する。`WithReference` は接続情報の参照を構成するために使い、起動順序の根拠にはしない。UDP サービスに HTTP のような既存の正常性確認先はないため、開始依存と正常性確認を分けて扱う。

既定の `visibility_graph` 構成では次の依存グラフを使う。

1. `simulator` と `game-controller` は開始依存を持たずに起動し、互いに並行起動を許可する。
2. `cm4-sim` と `duck` はそれぞれ `simulator` に `WaitForStart` し、`simulator` の開始後は互いの順序を要求せず並行起動を許可する。
3. `crane` は `cm4-sim`、`duck`、`game-controller` に `WaitForStart` してから起動する。
4. comparison mode の `tracker-tigers` と `tracker-erforce` は `simulator` と `game-controller` に `WaitForStart` してから起動する。

`cm4-sim` を使わない planner を選択した場合、`crane` は存在しない `cm4-sim` への依存を作らず、`duck` と `game-controller` に `WaitForStart` する。`duck` 自身が `simulator` に `WaitForStart` するため、この構成でも `simulator` と `game-controller` の開始後に `crane` を起動する。

`WaitForStart` が保証するのは対象資源が起動済み状態になったことまでであり、UDP を正常に処理できることまでは保証しない。`WaitFor` による正常性確認を導入する場合は、確認可能な正常性条件を追加してから使う。正常性確認がない段階では、存在しない正常性確認を成功条件として扱わない。

## 操作

開発者が覚える起動操作は一つにする。

```sh
aspire run --apphost Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj
```

停止は Aspire の通常の停止操作に従い、AppHost の終了時に `simulator`、`cm4-sim`、`crane` の各コンテナと Duck の子プロセスを終了する。

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

Crane の現行シナリオ構成と同じ `ghcr.io/ibis-ssl/framework-simulatorcli:<tag>` を接続し、host network で `simulator-cli` を起動する。UDP 10020 の SSL-Vision がホスト上の受信処理へ届くことを確認する。

### `ASPIRE-004`: Crane image と制御経路

`ghcr.io/ibis-ssl/crane:scenario-<commit SHA>` を `crane` 資源として接続する。`visibility_graph` の既存挙動を維持するため `cm4-sim` も image から起動し、Crane の mode 4 指令をシミュレータ向け mode 3 指令へ変換する現在の経路を再現する。

### `ASPIRE-005`: 起動試験

AppHost の全資源を一括起動し、`game-controller` が 11003 の唯一の referee producer として動作し、シミュレータからの SSL-Vision を Duck が受信し、Duck が `TrackerWrapperPacket` を出力する正常経路を確認する。

Crane を動かす試験では、`referee-driver` が `game-controller` を `HALT` から active command へ遷移させたことを 11003 で確認した後、Crane の指令が `cm4-sim` を経由してシミュレータへ入り、その結果が SSL-Vision と Duck の出力へ反映されることまで確認する。referee 遷移を確認できない場合は game-state fixture の失敗として扱い、UDP 12345 / 12346 の失敗と混同しない。

### `ASPIRE-006`: 外部トラッカー比較デバッグ

TIGERs Sumatra と ER-Force AutoRef の tracker source を追加で起動し、Duck と同じ raw vision `224.5.23.2:10020` を入力する。三つの tracker 出力を official tracker multicast `224.5.23.2:11010` へ集約し、`Tracker.DebugHost` が source identity ごとに分離して受信する。

ライブでは既存の Split / Overlay を使い、保存後は diagnostics sample tick を共通の選択時点として比較する。物体単位の位置・速度・角度・存在差を数値で確認する詳細設計は `Tracker/Design/Testing/tracker-comparison-debug-design.md` を正本とする。

`ASPIRE-006A` から `ASPIRE-006E` はこのトラッカー比較を実装単位へ分ける。

### `ASPIRE-006F`: TIGERs vs Crane 対戦資源

`ASPIRE-005` の基本 stack を基礎に、対戦用 Simulator 設定、対戦用 Game Controller fixture、`tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller` を追加する。対戦モードでは `cm4-sim` を除外し、Crane は `team:=ibis`、Sumatra は `--aiBlue`、Duck は 11010 publish 無効として application model test で固定する。

### `ASPIRE-006G`: TIGERs vs Crane 一括対戦試験

`ASPIRE-MATCH-001` から `ASPIRE-MATCH-005` を Linux の実 packet で確認し、双方の active motion、AutoRef / tracker 経路、試合終了、結果・SSL log・Crane の記録データ・resource log を証跡化する。勝敗は合否条件にしない。

## テスト方針

実装では TDD を使う。

最初に AppHost のアプリケーションモデルを検査するテストを追加し、未実装状態で失敗することを確認する。
最低限、次を自動検査する。

- `simulator`、`game-controller`、`crane`、`cm4-sim`、`duck` の資源が存在する。
- `duck` が `Tracker.RuntimeHost` を参照する .NET プロジェクト資源である。
- `crane` が `ghcr.io/ibis-ssl/crane` の `scenario-<commit SHA>` image を参照する。
- `simulator` が `ghcr.io/ibis-ssl/framework-simulatorcli`、`cm4-sim` が `ghcr.io/ibis-ssl/orion-cm4-sim` の固定タグを参照する。
- `simulator`、`game-controller`、`crane`、`cm4-sim` へ host network の実行引数が設定される。
- Duck に `sim` 用の VisionReceiver 設定が渡される。
- シミュレータの geometry と realism が明示される。
- Crane の `team`、`planner` と `cm4-sim` の接続ポートが明示される。
- `game-controller` が固定 tag または digest の image を参照し、11003 の唯一の referee producer として構成される。
- comparison mode の `tracker-tigers` が外部 Game Controller 用設定を使い、`gameController=false` と `publishRefereeMessages=false` で 11003 を受信専用にする。
- `referee-driver` の integration fixture が 11003 の `HALT` を確認してから Game Controller API へ continue action を送り、active command への遷移を確認できる。
- 既定の `visibility_graph` 構成で、`cm4-sim` と `duck` が `simulator` への `WaitForStart` 依存を持ち、`crane` が `cm4-sim`、`duck`、`game-controller` への `WaitForStart` 依存を持つ。
- `cm4-sim` を使わない planner 構成では、`crane` が `duck` と `game-controller` への `WaitForStart` 依存を持ち、存在しない `cm4-sim` への待機依存を持たない。
- 対戦モードでは `tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller` が追加され、`cm4-sim` と比較専用 `tracker-tigers` は存在しない。
- 対戦モードの Simulator 起動引数、Blue=`TIGERs Mannheim` / Yellow=`ibis` の team mapping、Crane の `team:=ibis`、Sumatra の `--aiBlue` が一組の設定として固定される。
- 対戦用 Sumatra 設定が 10020 の vision、11003 の external referee、`gameController=false`、11010 の tracker output を持つ。
- 対戦モードの Duck は 11010 への tracker publish が無効で、AutoRef の tracker 入力へ Duck source を混在させない。
- `match-controller` が参加資源の開始後に起動し、Game Controller API、11003、10020 の実データを確認してから試合を開始する。

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
- Aspire の resource 状態。
- Simulator、`game-controller`、Crane、`cm4-sim`、TIGERs、ER-Force の標準出力・標準エラー。
- `referee-driver` が観測した 11003 の遷移前後 command と Game Controller API へ送った continue action。
- `Tracker.RuntimeHost` / `Tracker.DebugHost` の標準出力・標準エラー。
- RuntimeHost の `VisionPacketsReceivedTotal` と DebugHost の raw input packet count の確認前後値。tracker packet は DebugHost の source ごとの受信数を併記する。
- DebugHost が認識した tracker source identity。
- stack ownership lock の取得結果と、占有制御 port の競合検出結果。共有 multicast port `10020` / `11010` は in-use 判定の対象外であることも記録する。

Linux は自動統合試験を基本とする。Windows / macOS は Docker Desktop が必要なため、専用 runner または手動の受入試験でもよいが、実機の成功証跡を残すまで対応済みとは扱わない。

Windows / macOS で container と host の multicast が成立しない場合、別 OS の成功を代用せず失敗として記録する。UDP relay / gateway や unicast 化は後続設計として検討し、試験中に暗黙の fallback を入れない。

## トラッカー比較デバッグ

通常の Duck + Crane シミュレーションとは別に comparison mode を用意し、TIGERs / ER-Force の tracker source と `Tracker.DebugHost` を追加起動できるようにする。

comparison mode は同じ raw vision を Duck / TIGERs / ER-Force へ与え、`Tracker.DebugHost` で三者の official tracker packet を比較する。外部 tracker が利用できない場合に通常のシミュレーション試験まで停止させない。

比較の詳細、source identity、時刻対応、ball / robot の対応付け、数値差分、CaptureOn / replay の契約は `Tracker/Design/Testing/tracker-comparison-debug-design.md` に定義する。

## TIGERs vs Crane 対戦モード

TIGERs の AI と Crane を実際に対戦させる対戦モードを、トラッカー比較とは独立した AppHost の構成として用意する。設計の基準は 2026-09-29 時点の `ibis-ssl/crane` develop `af6e0d3dec745415ce060ff5de2042afd3ec5145` にある `docker/match-vs-tigers/docker-compose.yaml`、`simulation_protocol_fixed.xml`、`match_controller_pb.py` とする。

トラッカー比較の `tracker-tigers` は AI を起動しないため、対戦モードには流用しない。対戦用 Sumatra は別資源 `tigers-blue` とし、Crane の現行対戦構成と同じく Blue 側 AI として起動する。

| 資源名 | 実行形態 | 対戦モードでの責務 |
| --- | --- | --- |
| `simulator` | Docker image | ER-Force `simulator-cli` を host network で起動する。対戦用起動引数は Crane の現行構成を基準にし、`-g 2020 --realism None --ibis-use-referee --ibis-feedback-team-name ibis --ibis-referee-port 11003` を使う。 |
| `game-controller` | Docker image | 11003 の唯一の referee producer とし、対戦用の初期状態を読み込む。 |
| `crane` | Docker image | 固定した `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>` を使い、`sim:=true speak:=false team:=ibis` で Yellow 側を制御する。 |
| `tigers-blue` | Docker image | 固定 tag または digest の `tigersmannheim/sumatra` を `--headless --aiBlue --visionAddress 224.5.23.2:10020 --refereeAddress 224.5.23.1:11003 --matchStats --moduli simulation_protocol` で起動する。 |
| `autoref-tigers` | Docker image | `tigersmannheim/auto-referee:1.2.0` を active / headless で起動し、vision 10020、referee 11003、tracker 11010 を使って試合判定を Game Controller へ返す。 |
| `ssl-log-recorder` | Docker image | referee 11003、vision 10020、tracker 11010 を対戦証跡として保存する。 |
| `match-controller` | 試験 fixture | Game Controller API、referee、vision の準備完了を確認し、試合開始・停止状態からの継続・終了監視・結果保存を行う。 |
| `duck` | .NET プロセス | SSL-Vision の観測と Duck 側デバッグを継続する。ただし対戦判定へ影響を与えないよう、対戦モードでは official tracker multicast 11010 への publish を無効にする。 |

対戦モードでは基本 stack の `visibility_graph` 用 `cm4-sim` を起動しない。Crane の現行 `match-vs-tigers` 構成は `cm4-sim` を含まず、対戦用 Simulator と Sumatra 設定を一体として使っているため、基本 mode の `--ibis-port 12346` / UDP 12345 / 12346 の経路を対戦モードへ混在させない。

Sumatra へ渡す `simulation_protocol_fixed.xml` 相当の fixture は Duck 側で版管理し、少なくとも raw vision `224.5.23.2:10020`、referee `source=NETWORK` / port `11003` / `gameController=false`、`SumatraSimBotManager`、tracker output `224.5.23.2:11010` を固定する。Crane リポジトリを AppHost 起動時に clone して fixture を取得する方式にはしない。

Game Controller の初期状態も Duck 側の対戦 fixture として版管理する。初期対戦は Crane の現行構成に合わせ、Blue team name を `TIGERs Mannheim`、Yellow team name を `ibis`、match type を `FRIENDLY` とする。Crane の `team:=ibis` と Sumatra の `--aiBlue` はこの team mapping と一組の契約として扱い、一方だけを変更しない。

Crane の現行 compose は Sumatra、Game Controller、SSL log recorder に移動タグを含むが、Duck の再現可能な試験では暗黙の `latest` を使わない。AppHost 設定で image tag または digest を明示し、実行証跡へ解決済み image reference を保存する。Crane image は既存の `scenario-<commit SHA>` を既定とし、Crane の match workflow が同じ scenario image を `match-<commit SHA>` へ再タグ付けして利用できる構成と整合させる。

### 対戦モードの起動順序

1. `simulator` と `game-controller` を開始する。
2. `duck`、`tigers-blue`、`autoref-tigers`、`ssl-log-recorder` は必要な `simulator` / `game-controller` に `WaitForStart` して起動する。
3. `crane` は `simulator` と `game-controller` に `WaitForStart` して起動する。対戦モードでは存在しない `cm4-sim` への依存を作らない。
4. `match-controller` は `simulator`、`game-controller`、`crane`、`tigers-blue`、`autoref-tigers` の開始後に起動する。
5. `match-controller` は resource の Started 状態だけで試合を開始せず、Game Controller API 接続、11003 の referee message、10020 の SSL-Vision を実際に確認してから continue action を送る。

`match-controller` は Crane の現行試合 controller を基準に、`HALT` / `STOP` から利用可能な continue action を選び、必要に応じて `NEXT_COMMAND`、`NORMAL_START`、`FORCE_START` を使って試合を進行させる。終了条件は `POST_GAME` または設定した最大試合時間とし、結果には少なくとも両チームの得点、終了理由、`CRANE WIN` / `TIGERs WIN` / `DRAW` のいずれかを保存する。

勝敗そのものは CI の合否条件にしない。両 AI が同一試合へ参加し、試合が規定の終了条件まで進行し、結果と診断証跡を生成できることを対戦機能の正常条件とする。

### 対戦モードの受入項目

| ID | 確認内容 | 合格条件 |
| --- | --- | --- |
| `ASPIRE-MATCH-001` | resource model | `simulator`、`game-controller`、`crane`、`tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller` が存在し、`cm4-sim` と比較専用 `tracker-tigers` は起動対象に入らない。 |
| `ASPIRE-MATCH-002` | team / referee 契約 | Blue=`TIGERs Mannheim`、Yellow=`ibis`、Crane=`team:=ibis`、Sumatra=`--aiBlue` が一致し、11003 の producer は `game-controller` 一つだけである。 |
| `ASPIRE-MATCH-003` | 双方の active motion | active referee state の同一確認窓で、SSL-Vision 上に Yellow の Crane robot と Blue の TIGERs robot の位置変化がそれぞれ観測できる。片側だけの移動では合格にしない。 |
| `ASPIRE-MATCH-004` | AutoRef / tracker 経路 | `tigers-blue` が 11010 へ tracker packet を出力し、`autoref-tigers` が 10020 / 11003 / 11010 を使って active に動作する。Duck は 11010 へ publish せず、AutoRef の tracker 入力へ別 source を混在させない。 |
| `ASPIRE-MATCH-005` | 試合完了と証跡 | `POST_GAME` または最大試合時間で終了し、対戦結果、全 resource の stdout / stderr、SSL log、Crane の記録データ、Game Controller / AutoRef / Sumatra の診断情報を保存できる。 |

Linux の自動統合試験では `ASPIRE-MATCH-001` から `005` を対戦モードの受入条件とする。Windows / macOS で対戦モード対応を表明する場合も、各 OS 上で同じ受入項目を実 packet で確認するまで対応済みとは扱わない。

## 診断

Aspire ダッシュボードの資源別ログを一次確認に使う。

自動試験を CI へ追加する場合は、既存の `.NET tests` と同様に、失敗時の標準出力、標準エラー、テスト結果、Aspire とコンテナのログを artifact として保存する。

シミュレータ、`game-controller`、Crane、`cm4-sim` のコンテナログは資源名が分かる形で分離する。`ASPIRE-NET-007` では `referee-driver` の操作ログと 11003 の command 遷移も同じ試験証跡へ保存する。

対戦モードではこれに加えて `tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller` の標準出力・標準エラー、対戦結果、SSL log、Crane の記録データ、解決済み image reference、team mapping、試合時間設定を保存する。失敗時も途中まで生成された結果と各 resource のログを破棄しない。

## 対象外

初期実装には次を含めない。

- Duck 自体の Docker 化。
- 本番環境の配置方式。
- Kubernetes への配置。
- Tigers Tracker と ER-Force Tracker の比較起動。これは Issue #14 の比較試験として別段階で追加する。
- 自動レフェリーの実装。
- AI のアルゴリズム変更。
- 既存の `Tracker.RuntimeHost` の通信形式変更。
- Docker bridge network 越しの SSL-Vision マルチキャスト対応。

## 将来拡張

Issue #14 の比較試験を行うときは、`tracker-tigers` と `tracker-erforce` を追加のコンテナ資源として AppHost へ登録する。

比較用トラッカーを追加しても `duck`、`simulator`、`crane`、`cm4-sim` の初期構成の契約は変えない。

将来 bridge network へ移行する必要が出た場合は、ER-Force の SSL-Vision を任意の宛先へ転送する明示的な中継資源を追加するか、シミュレータ側の送信先指定機能を追加する。暗黙のマルチキャスト転送には依存しない。

## 完了条件

本設計の実装完了条件は次のとおりとする。

- 一つの Aspire AppHost 起動でシミュレータ、`game-controller`、Crane、必要な `cm4-sim`、Duck を管理できる。
- comparison mode では TIGERs tracker、ER-Force tracker、`Tracker.DebugHost` を追加し、Duck を含む三 tracker の差を同じ raw vision 入力に対して確認できる。
- 対戦モードでは `tigers-blue`、`autoref-tigers`、`ssl-log-recorder`、`match-controller` を追加し、Blue の TIGERs AI と Yellow の Crane を同じ Game Controller / Simulator 上で対戦させられる。
- 対戦モードでは `cm4-sim` を起動せず、Duck の 11010 publish を無効にして AutoRef の tracker 入力へ干渉しない。
- `ASPIRE-MATCH-001` から `ASPIRE-MATCH-005` により、両チームの active motion、AutoRef / tracker 経路、試合終了、結果と診断 artifact を確認できる。
- シミュレータと Crane は Docker コンテナ、Duck はホスト上の .NET プロセスとして起動する。
- Crane は `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>` の固定 image tag から起動し、Duck 側ではビルドしない。
- Duck は既存の `sim` 設定と SSL-Vision 契約を維持する。
- 開発者が各資源の起動コマンドを個別に管理しなくてよい。
- AppHost が管理する全資源の標準出力と標準エラーを Aspire から確認できる。
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