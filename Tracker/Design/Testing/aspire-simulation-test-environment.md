# Aspire シミュレーション試験環境 設計

## 目的

Issue #18 の Aspire 対応として、シミュレーション試験に必要な複数プロセスを一つの起動操作で立ち上げられるようにする。

初期対象は次の三つとする。

- ER-Force の `simulator-cli` を実行するシミュレータ。
- ロボット制御を実行する AI。
- Duck の `Tracker.RuntimeHost`。

シミュレータと AI は Docker コンテナとして起動し、Duck は開発中のコードをそのままデバッグできるよう、ホスト上の .NET プロセスとして起動する。

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

シミュレータと AI はコンテナ資源として AppHost に登録する。既存イメージを使う場合は `AddContainer`、リポジトリ内 Dockerfile から作る場合は `AddDockerfile` を使う。
## 資源構成

AppHost では次の三資源を管理する。

| 資源名 | 実行形態 | 責務 |
| --- | --- | --- |
| `simulator` | Docker | ER-Force `simulator-cli` を起動し、物理シミュレーションと SSL-Vision 出力を行う。 |
| `ai` | Docker | ロボット制御指令を SSL simulation protocol でシミュレータへ送る。 |
| `duck` | .NET プロセス | `Tracker.RuntimeHost` を `sim` 設定で起動し、SSL-Vision を追跡してトラッカーパケットを出力する。 |

Aspire のダッシュボードは、三資源の起動状態、終了状態、標準出力、標準エラーを一か所で確認する用途に使う。

## ネットワーク方針

初期実装は host network を前提とする。

理由は、ER-Force の `simulator-cli` が SSL-Vision の送信先として任意の IP アドレスを指定する起動引数を持たず、通常のマルチキャスト送信か `127.0.0.1` 送信だけを選べるためである。

シミュレータを通常の Docker bridge network に置いた場合、`--localhost` はコンテナ自身を指し、ホストで動く Duck には届かない。マルチキャストを bridge network とホスト間で透過させる構成にも依存しない。

そのため `simulator` と `ai` は Docker の host network で起動し、Duck はホスト上で従来どおり SSL-Vision のマルチキャストへ参加する。
Linux の Docker Engine では host network はコンテナとホストのネットワーク名前空間を共有するため、UDP のポート公開や変換を挟まずに通信できる。

Docker Desktop を使う場合は host networking の有効化が必要である。初期受入環境は Linux の Docker Engine とし、Docker Desktop での動作は別途確認項目とする。

host network では UDP 10020、10300、10301、10302 をホスト全体で共有する。このため初期構成は同一ホストで同時に一組だけ起動し、これらのポートを使う別プロセスとの競合を起動前に検出する。Aspire の `--isolated` を指定しても、この固定 UDP ポートは分離されないものとして扱う。

Aspire からは `WithContainerRuntimeArgs("--network", "host")` 相当を使ってコンテナ実行時のネットワーク方式を指定する。host network 使用時は Docker の `-p` 相当のポート公開を併用しない。

### 通信経路

```text
AI container
  │ UDP 10301 / 10302
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

シミュレータ資源は ER-Force `simulator-cli` を実行する。

外部から利用できる固定済みイメージを採用するまでは、リポジトリ管理の Dockerfile から構築する方式を想定する。上流の内容が変化して試験結果が変わらないよう、使用する ER-Force のコミットを固定する。

シミュレータの設定値は AppHost の設定として次を持たせる。

- ER-Force のソースまたはイメージの版。
- geometry 名。
- realism 名。
- 追加の起動引数。

初期値は geometry を `2026`、realism を `None` とし、AppHost の設定から明示的に変更できるようにする。試験結果が上流の既定値変更だけで変化しない構成にする。
## AI の構成

AI は Docker コンテナとして扱う。

AI の具体的なリポジトリ、イメージ名、起動コマンドは本依頼では指定されていないため、AppHost から差し替え可能な入力とする。Issue #18 に記載されている `crane(ros2)` を最初の候補とする場合も、AppHost の資源名と外部契約は `ai` のままにする。

AI がシミュレータへ接続するために必要な値は環境変数または起動引数として渡し、Dockerfile 内へ固定しない。

最低限、次を差し替え可能にする。

- AI イメージまたは Dockerfile。
- チーム色。
- シミュレータ制御先。
- AI 固有の追加引数。

ROS 2 を使う AI では DDS の通信要件もあるため、初期構成ではシミュレータと同じ host network を使う。

## 起動順序

Aspire は依存関係と起動順を管理するが、UDP サービスに HTTP のような既存の正常性確認先はない。

初期実装では次の順序を使う。

1. `simulator` を起動する。
2. `duck` を起動する。
3. `ai` を起動する。
単にプロセスが起動したことと、UDP を正常に処理できることは区別する。`WaitFor` による正常性確認を導入する場合は、確認可能な正常性条件を追加してから使う。

正常性確認がない段階では、存在しない正常性確認を成功条件として扱わない。

## 操作

開発者が覚える起動操作は一つにする。

```sh
aspire run --apphost Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj
```

停止は Aspire の通常の停止操作に従い、AppHost の終了時に二つのコンテナと Duck の子プロセスを終了する。

個別資源の再起動とログ確認は Aspire ダッシュボードから行える構成にする。

## 設定の責務

AppHost は「何を一緒に起動するか」と「試験環境でどの接続設定を使うか」だけを持つ。

追跡アルゴリズムの設定値、RuntimeHost の処理周期、正式な通信形式は既存の Duck 側設定と設計を正本とする。

コンテナのイメージ版、geometry、realism、AI のチーム色など、試験環境を再現するための値だけを AppHost の設定へ置く。

秘密情報が必要になった場合はリポジトリへ直接保存せず、Aspire のパラメータまたは利用者のローカル設定から渡す。
## 実装単位

実装は次の順に分ける。

### `ASPIRE-002`: AppHost の骨格

`Testing/Duck.Testing.AppHost` を追加し、`duck` だけを `Tracker.RuntimeHost` として起動できる状態にする。

### `ASPIRE-003`: ER-Force シミュレータ

シミュレータ用 Dockerfile または承認済みイメージを追加し、host network で `simulator-cli` を起動する。UDP 10020 の SSL-Vision がホスト上の受信処理へ届くことを確認する。

### `ASPIRE-004`: AI コンテナ

採用する AI の Dockerfile またはイメージを接続し、UDP 10301 / 10302 の対象チームへ指令を送れるようにする。

### `ASPIRE-005`: 起動試験

三資源を一括起動し、シミュレータからの SSL-Vision を Duck が受信し、Duck が `TrackerWrapperPacket` を出力する正常経路を確認する。

AI を動かした場合は、ロボット指令がシミュレータへ入り、その結果が SSL-Vision と Duck の出力へ反映されることまで確認する。

## テスト方針

実装では TDD を使う。

最初に AppHost のアプリケーションモデルを検査するテストを追加し、未実装状態で失敗することを確認する。
最低限、次を自動検査する。

- `simulator`、`ai`、`duck` の三資源が存在する。
- `duck` が `Tracker.RuntimeHost` を参照する .NET プロジェクト資源である。
- `simulator` と `ai` がコンテナ資源である。
- 二つのコンテナへ host network の実行引数が設定される。
- Duck に `sim` 用の VisionReceiver 設定が渡される。
- シミュレータの geometry と realism が明示される。

Docker を必要とする一括起動試験は、AppHost のモデル検査と分離する。Docker が利用できない環境でも、アプリケーションモデルの退行を検出できるようにする。

一括起動試験では、単に三資源が `Running` になっただけで成功としない。SSL-Vision の受信と Duck のトラッカーパケット出力までを試験証跡に含める。

## 診断

Aspire ダッシュボードの資源別ログを一次確認に使う。

自動試験を CI へ追加する場合は、既存の `.NET tests` と同様に、失敗時の標準出力、標準エラー、テスト結果、Aspire とコンテナのログを artifact として保存する。

シミュレータと AI のコンテナログは資源名が分かる形で分離する。

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

比較用トラッカーを追加しても `duck`、`simulator`、`ai` の三資源の契約は変えない。

将来 bridge network へ移行する必要が出た場合は、ER-Force の SSL-Vision を任意の宛先へ転送する明示的な中継資源を追加するか、シミュレータ側の送信先指定機能を追加する。暗黙のマルチキャスト転送には依存しない。

## 完了条件

本設計の実装完了条件は次のとおりとする。

- 一つの Aspire AppHost 起動でシミュレータ、AI、Duck を管理できる。
- シミュレータと AI は Docker コンテナ、Duck はホスト上の .NET プロセスとして起動する。
- Duck は既存の `sim` 設定と SSL-Vision 契約を維持する。
- 開発者が個別に三つの起動コマンドを管理しなくてよい。
- 三資源の標準出力と標準エラーを Aspire から確認できる。
- SSL-Vision 受信と Duck のトラッカーパケット出力を含む正常経路を確認できる。
- 実装と試験の証跡を報告書へ残し、PR の最新コミットと同じ SHA の CI だけを最終確認に使う。
## 参照

- GitHub Issue #18 `Aspire対応`。
- GitHub Issue #14 `dockerでシミュレーターのケースを追加`。
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`。
- `Tracker/Tracker.RuntimeHost/appsettings.json`。
- ER-Force Framework の `simulator-cli` 実装と README。
- RoboCup SSL simulation protocol。
- Aspire の AppHost、コンテナ、.NET プロジェクト資源、コンテナ実行引数の公式文書。
- Docker の host network driver の公式文書。