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

AppHost では三つの主要資源と、Crane の現在のシミュレーション経路を維持する場合に一つの補助資源を管理する。

| 資源名 | 実行形態 | 責務 |
| --- | --- | --- |
| `simulator` | Docker image | Crane の現行シナリオ構成と同じ `ghcr.io/ibis-ssl/framework-simulatorcli:<tag>` から ER-Force `simulator-cli` を起動し、物理シミュレーションと SSL-Vision 出力を行う。 |
| `crane` | Docker image | `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>` から Crane を起動し、ロボット制御指令を生成する。 |
| `cm4-sim` | Docker image | `ghcr.io/ibis-ssl/orion-cm4-sim:<commit SHA>` を使い、Crane が `visibility_graph` で出す mode 4 の位置指令を現在の Crane シナリオ構成と同じ経路で mode 3 の速度指令へ変換する補助資源。 |
| `duck` | .NET プロセス | `Tracker.RuntimeHost` を `sim` 設定で起動し、SSL-Vision を追跡してトラッカーパケットを出力する。 |

Aspire のダッシュボードは、AppHost が管理する各資源の起動状態、終了状態、標準出力、標準エラーを一か所で確認する用途に使う。

## ネットワーク方針

初期実装は host network を前提とする。

理由は、ER-Force の `simulator-cli` が SSL-Vision の送信先として任意の IP アドレスを指定する起動引数を持たず、通常のマルチキャスト送信か `127.0.0.1` 送信だけを選べるためである。

シミュレータを通常の Docker bridge network に置いた場合、`--localhost` はコンテナ自身を指し、ホストで動く Duck には届かない。マルチキャストを bridge network とホスト間で透過させる構成にも依存しない。

そのため `simulator`、`crane`、`cm4-sim` は Docker の host network で起動し、Duck はホスト上で従来どおり SSL-Vision のマルチキャストへ参加する。
Linux の Docker Engine では host network はコンテナとホストのネットワーク名前空間を共有するため、UDP のポート公開や変換を挟まずに通信できる。

Docker Desktop を使う場合は host networking の有効化が必要である。初期受入環境は Linux の Docker Engine とし、Docker Desktop での動作は別途確認項目とする。

host network では SSL-Vision の UDP 10020 に加え、採用するシミュレータ構成が使う制御ポートをホスト全体で共有する。このため初期構成は同一ホストで同時に一組だけ起動し、利用ポートを使う別プロセスとの競合を起動前に検出する。Aspire の `--isolated` を指定しても、この固定 UDP ポートは分離されないものとして扱う。

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

シミュレーション制御を行う試験ツールが必要な場合は UDP 10300 を使用する。初期 AppHost 自身は試験シナリオを生成しない。

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

## 起動順序

Aspire は依存関係と起動順を管理するが、UDP サービスに HTTP のような既存の正常性確認先はない。

初期実装では次の順序を使う。

1. `simulator` を起動する。
2. `cm4-sim` を起動する。
3. `duck` を起動する。
4. `crane` を起動する。
単にプロセスが起動したことと、UDP を正常に処理できることは区別する。`WaitFor` による正常性確認を導入する場合は、確認可能な正常性条件を追加してから使う。

正常性確認がない段階では、存在しない正常性確認を成功条件として扱わない。

## 操作

開発者が覚える起動操作は一つにする。

```sh
aspire run --apphost Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj
```

停止は Aspire の通常の停止操作に従い、AppHost の終了時に `simulator`、`cm4-sim`、`crane` の各コンテナと Duck の子プロセスを終了する。

個別資源の再起動とログ確認は Aspire ダッシュボードから行える構成にする。

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

AppHost の全資源を一括起動し、シミュレータからの SSL-Vision を Duck が受信し、Duck が `TrackerWrapperPacket` を出力する正常経路を確認する。

Crane を動かした場合は、Crane の指令が `cm4-sim` を経由してシミュレータへ入り、その結果が SSL-Vision と Duck の出力へ反映されることまで確認する。

## テスト方針

実装では TDD を使う。

最初に AppHost のアプリケーションモデルを検査するテストを追加し、未実装状態で失敗することを確認する。
最低限、次を自動検査する。

- `simulator`、`crane`、`cm4-sim`、`duck` の資源が存在する。
- `duck` が `Tracker.RuntimeHost` を参照する .NET プロジェクト資源である。
- `crane` が `ghcr.io/ibis-ssl/crane` の `scenario-<commit SHA>` image を参照する。
- `simulator` が `ghcr.io/ibis-ssl/framework-simulatorcli`、`cm4-sim` が `ghcr.io/ibis-ssl/orion-cm4-sim` の固定タグを参照する。
- `simulator`、`crane`、`cm4-sim` へ host network の実行引数が設定される。
- Duck に `sim` 用の VisionReceiver 設定が渡される。
- シミュレータの geometry と realism が明示される。
- Crane の `team`、`planner` と `cm4-sim` の接続ポートが明示される。

Docker を必要とする一括起動試験は、AppHost のモデル検査と分離する。Docker が利用できない環境でも、アプリケーションモデルの退行を検出できるようにする。

一括起動試験では、単に全資源が `Running` になっただけで成功としない。SSL-Vision の受信と Duck のトラッカーパケット出力までを試験証跡に含める。

## 診断

Aspire ダッシュボードの資源別ログを一次確認に使う。

自動試験を CI へ追加する場合は、既存の `.NET tests` と同様に、失敗時の標準出力、標準エラー、テスト結果、Aspire とコンテナのログを artifact として保存する。

シミュレータ、Crane、`cm4-sim` のコンテナログは資源名が分かる形で分離する。

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

Issue #14 の比較試験を行うときは、`tigers-tracker` と `erforce-tracker` を追加のコンテナ資源として AppHost へ登録する。

比較用トラッカーを追加しても `duck`、`simulator`、`crane`、`cm4-sim` の初期構成の契約は変えない。

将来 bridge network へ移行する必要が出た場合は、ER-Force の SSL-Vision を任意の宛先へ転送する明示的な中継資源を追加するか、シミュレータ側の送信先指定機能を追加する。暗黙のマルチキャスト転送には依存しない。

## 完了条件

本設計の実装完了条件は次のとおりとする。

- 一つの Aspire AppHost 起動でシミュレータ、Crane、必要な `cm4-sim`、Duck を管理できる。
- シミュレータと Crane は Docker コンテナ、Duck はホスト上の .NET プロセスとして起動する。
- Crane は `ghcr.io/ibis-ssl/crane:scenario-<commit SHA>` の固定 image tag から起動し、Duck 側ではビルドしない。
- Duck は既存の `sim` 設定と SSL-Vision 契約を維持する。
- 開発者が各資源の起動コマンドを個別に管理しなくてよい。
- AppHost が管理する全資源の標準出力と標準エラーを Aspire から確認できる。
- SSL-Vision 受信と Duck のトラッカーパケット出力を含む正常経路を確認できる。
- 実装と試験の証跡を報告書へ残し、PR の最新コミットと同じ SHA の CI だけを最終確認に使う。
## 参照

- GitHub Issue #18 `Aspire対応`。
- GitHub Issue #14 `dockerでシミュレーターのケースを追加`。
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`。
- `Tracker/Tracker.RuntimeHost/appsettings.json`。
- ER-Force Framework の `simulator-cli` 実装と README。
- `ibis-ssl/crane` の `docker/Dockerfile`、`.github/workflows/docker_build.yaml`、`docker/scenario/docker-compose.yaml`。
- RoboCup SSL simulation protocol。
- Aspire の AppHost、コンテナ、.NET プロジェクト資源、コンテナ実行引数の公式文書。
- Docker の host network driver の公式文書。