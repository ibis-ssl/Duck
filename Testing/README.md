# Aspire を使って開発用シミュレーションを起動する

このページでは、Windows 上の Docker Desktop を使い、Aspire で開発用シミュレーション環境を起動する方法を説明します。

## 構成図

次の単一ページの編集可能な SVG は、主要処理と通信の流れ、Windows 上の AppHost/Aspire、Docker Desktop の Linux コンテナ、Duck が作るイメージと外部イメージの境界、プロジェクト参照を示します。

![Duckの開発・実行構成](duck-architecture.dio.svg)

[編集用ソース](duck-architecture.drawio)も参照できます。SVG の単一ページ構造と、原図に対する埋込編集データの整合性を確認しています。画面上の重なりや日本語の読みやすさは未確認で、この原稿の PNG プレビューも未生成です。

## AppHost の役割

`Testing/Duck.Testing.AppHost` は `Duck.slnx` に登録済みの Aspire AppHost です。AppHost は `base`、`comparison`、`match` の各構成を登録し、Aspire/DCP を通じて Docker Engine 上のシミュレーター、Game Controller、Crane、Duck RuntimeHost などを起動・管理します。本書の Windows 構成では、AppHost 自体は Windows 上で動く .NET プロセスです。

図中のソースプロジェクトは編集元です。例えば `Tracker/Tracker.RuntimeHost` はソースの場所で、`dotnet publish` により `Tracker.RuntimeHost.dll` と依存ファイルが発行先に生成されます。AppHost は `Release` 構成の発行先と、そのプロジェクトの `Dockerfile` から `duck-runtime` イメージを作って `duck` リソースのコンテナを起動します。コンテナ内の起動対象は `/app` にコピーされた `Tracker.RuntimeHost.dll` で、実行コマンドは `dotnet Tracker.RuntimeHost.dll` です。

`Tracker/Tracker.Core` も編集元のソースプロジェクトで、発行物の `Tracker.Core.dll` は RuntimeHost などに組み込まれるライブラリです。図のコンテナ内の処理は、これらの発行済み実行物の動作を表します。DebugHost と `Duck.Testing.RefereeDriver.Host` も、それぞれの発行先からイメージを作り、コンテナ内で対応するホストの `.dll` を `dotnet` で起動します。

後述する `base` の `rvo2` 起動例では、シミュレーター、Game Controller、Crane、Duck RuntimeHost を起動します。`comparison` は比較対象や DebugHost を追加する構成、`match` は対戦相手、AutoRef、ログ記録、試合進行制御を含む構成です。通常の `base` 起動後は Game Controller の試合設定を画面から行います。

RuntimeHost の変更を反映するには、`dotnet publish` を再実行してから AppHost を起動し直します。この構成には `dotnet watch` による RuntimeHost のホットリロードはありません。

## Windows の前提条件

- .NET 10 SDK。CI は `10.0.x` を使います。リポジトリに `global.json` はなく、SDK のパッチ版は固定されていません。
- Docker Desktop を起動し、Linux コンテナを実行できる状態にします。AppHost は Docker CLI と選択中の Docker 接続先を使います。
- 初回起動時は Docker Engine からコンテナレジストリへ接続できる必要があります。
- リポジトリを取得したら、ソリューションを復元する前にサブモジュールを初期化します。

以下はリポジトリのルートで実行する Windows PowerShell 用の確認・復元コマンドです。後述する `rvo2` の起動例は既存の WSL ディストリビューションを必要としません。

```powershell
git submodule update --init --recursive
dotnet --list-sdks
docker version
docker info --format '{{.OSType}}'
dotnet restore Duck.slnx
```

`dotnet --list-sdks` に .NET 10 SDK が表示されること、`docker version` に `Client` と `Server` の両方が表示されること、`docker info` の結果が `linux` であることを確認します。

## Windows で AppHost を起動する

`base` の既定の `Testing:Crane:Planner` は `visibility_graph` で、この構成は CM4 simulator も起動します。WSL を使わず最初に AppHost を試す場合は `rvo2` を指定します。`rvo2` の `base` 構成では CM4 simulator と `cm4-transport-control` は使いません。

AppHost は Docker イメージのビルド元に `Tracker.RuntimeHost` の `Release` 構成の発行先を使います。先に RuntimeHost の `dotnet publish` を実行し、その後 AppHost を起動します。

```powershell
dotnet publish Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj --configuration Release --output Tracker/Tracker.RuntimeHost/bin/Release/net10.0/publish
dotnet run --project Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj -- --Testing:Mode=base --Testing:Crane:Planner=rvo2
```

起動時は Aspire の出力に表示された Dashboard URL を開いてください。Game Controller の画面は Dashboard に表示される `game-controller` の HTTP 接続先から開けます。各リソースの公開ポートは動的に割り当てられるため、実際に表示された URL を使ってください。終了時は同じ PowerShell ウィンドウで `Ctrl+C` を押します。別の Duck AppHost が既定の排他ロックを保持している場合は、その AppHost を先に終了してください。

RuntimeHost のコードを変更したら、上記の `dotnet publish` を再実行してから AppHost を起動し直します。AppHost が発行先から Docker イメージを作り、更新後のコードをコンテナ内で実行します。

## 既存の起動スクリプト

`scripts/start-duck-simulation.ps1` は通常の `base` 構成を準備して起動します。RuntimeHost、DebugHost、`Duck.Testing.RefereeDriver.Host` の発行、CM4 補助プログラムの確認、AppHost のビルドと起動をまとめて行います。

```powershell
.\scripts\start-duck-simulation.ps1
```

このスクリプトは `visibility_graph` を使うため、`cm4-transport-control` が必要です。最新の Linux x64 補助プログラムが出力先に見つからない場合、スクリプトは既存の WSL ディストリビューションにある C コンパイラーを使って生成します。ソフトウェアはインストールしません。補助プログラムがなく、利用できる WSL ディストリビューションやコンパイラーもない場合は起動前に停止します。WSL を使わない場合は、前節の `rvo2` 手順を使ってください。このスクリプトは `base` 起動用で、`match` 用の Java エージェントは生成しません。

## モードと補助ファイル

`native/cm4-transport` は、CM4 simulator コンテナ内で動く POSIX C 補助プログラムのソースとテストを含むディレクトリです。`cm4-transport-control` は Linux x64 の実行ファイルで、AppHost はこれを `/opt/duck/cm4-transport-control` に読み取り専用でマウントし、Docker CLI の `exec` 経由で制御要求を送ります。補助プログラムは simulator のフィードバックを CM4 へ転送し、CM4 からの UDP パケットを simulator へ転送します。Windows の AppHost がこの Linux 実行ファイルを直接実行するわけではありません。Docker Desktop は Linux コンテナを実行しますが、この実行ファイル自体は自動生成しません。

| 補助ファイル | 用途と必要なモード | このリポジトリでの準備 |
| --- | --- | --- |
| `cm4-transport-control` | `base` / `comparison` の `visibility_graph` と `match` で使う Linux x64 補助プログラム。`base` / `comparison` の `rvo2` では CM4 simulator を使わないため不要です。 | `start-duck-simulation.ps1` は既定の `base` 用に、既存 WSL のコンパイラーで不足・古い補助プログラムを作ります。GitHub Actions も Ubuntu 上で生成します。通常の AppHost ビルド・発行は既存ファイルのコピーだけです。 |
| `autoref-game-log-agent.jar` | `match` の AutoRef コンテナに付ける Java エージェントです。AutoRef のモジュールが有効になった後、既存の `GameLogRecorder` を開始します。 | `scripts/build_autoref_game_log_agent.sh` が `javac --release 17` と `jar` で作ります。GitHub Actions は Ubuntu 上でこのスクリプトを実行してから AppHost をビルドします。AppHost のビルド・発行は既存ファイルのコピーだけです。 |

コピー元は AppHost プロジェクトの `$(IntermediateOutputPath)` です。上記の `Debug` 手順と CI では `Testing/Duck.Testing.AppHost/obj/Debug/net10.0` に準備し、ビルド時に `bin/Debug/net10.0` へコピーされます。`Release` など別の構成では中間出力先も変わります。いずれもファイルが存在する場合だけコピーする設定で、欠落していてもビルド時には生成しません。起動時に必要なモードを選ぶと、ファイルがないためリソースの起動に失敗します。

一方、Crane の起動を補助する `crane-launch-overlay` は AppHost のビルドで Linux x64 向けに自動発行されます。この処理は CM4 補助プログラムや Java エージェントの生成を含みません。

`base` を `visibility_graph` で起動する場合は補助プログラムが必要ですが、`match` 用の Java エージェントは不要です。`match` は両方を必要とします。既存の Windows `start-duck-simulation.ps1` は `base` 用で、Java エージェントを作りません。生成物のない Windows 作業ツリーから `match` 用の両ファイルを自動準備する PowerShell 手順はありません。CI は Ubuntu で両方を準備します。

[課題 #53](https://github.com/ibis-ssl/Duck/issues/53) の過去調査では、確認した画面なしでの起動、CI 設定、録画クラス、画面設定から自動録画開始の設定・引数は見つからず、Java エージェントから既存の録画開始 API を呼んで実ログ生成を確認しています。全呼出箇所を網羅した調査ではないため、別の開始方法が存在しないと断定したものではありません。

## 検証範囲と未確認事項

本書の起動構成と準備処理は、[AppHost の登録コード](Duck.Testing.AppHost/Program.cs)、[共通リソースの登録コード](Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs)、[プロジェクト設定](Duck.Testing.AppHost/Duck.Testing.AppHost.csproj)、[Windows 起動スクリプト](../scripts/start-duck-simulation.ps1)、[CI 定義](../.github/workflows/dotnet-test.yml) と照合しました。今回の文書作業では、掲載した復元、発行、AppHost 起動を Windows/Docker Desktop 上で通して実行していません。ソースから確認した手順であり、この作業ツリーでの起動成功を報告するものではありません。

通常の `dotnet test` は `Tracker/Tracker.Tests/AspireFullStackAcceptanceTests.cs` を既定でコンパイル対象から外します。そのため、通常のテスト成功は Docker コンテナを使う全体の受入試験の成功を示しません。`.github/workflows/dotnet-test.yml` の `workflow_dispatch` は `comparison` または `match` の受入経路を実行できますが、条件により処理が省略される場合があります。省略を成功とは扱わず、同じコミットでの CI 結果を確認してください。

Windows で `match` の準備操作を減らすには、Linux 補助プログラムを Docker Desktop の Linux コンテナで生成し、Java 17 でエージェントを作って AppHost のビルド前に配置・検証する PowerShell 準備スクリプトを追加する案があります。現在その自動化はありません。

関連する実装・検証記録は [Aspire シミュレーションテスト環境](../Tracker/Design/Testing/aspire-simulation-test-environment.md) と [課題 49 のフォローアップ記録](../Tracker/Design/Testing/issue49-kickoff-api-phase-followup.md)を参照してください。
