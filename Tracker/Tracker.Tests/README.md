# Windows で Tracker.Tests と Aspire AppHost を使う

このテストプロジェクトは .NET 10 を対象とします。ローカルのシミュレーション環境を起動する AppHost は **Testing/Duck.Testing.AppHost** です。AppHost は Aspire/DCP 経由で Docker コンテナを管理し、**Tracker.RuntimeHost** はホスト上のプロセスではなくコンテナとして起動します。

## 実行モード

AppHost は **base**、**comparison**、**match** の各モードを実装しています。モードを省略した場合は **base** です。

- **base**: Simulator、Game Controller、Crane、Duck RuntimeHost を起動します。既定の Crane planner は **visibility_graph** で、CM4 simulator 用 Linux helper も使います。Windows でまず起動経路を試す場合は、下記の例のように **rvo2** を指定すると CM4 simulator を使わない base 構成になります。
- **comparison**: base の資源に加えて TIGERs、ER-Force、DebugHost を使います。DebugHost のコンテナ用に Tracker.DebugHost の publish 出力も必要です。
- **match**: match-controller、TIGERs Blue、AutoRef、SSL log recorder を追加します。CM4 transport helper と AutoRef の Java agent も必要です。

この AppHost はシミュレーションとテスト用の起動構成です。**dotnet watch** によるホットリロード構成ではありません。RuntimeHost の変更を試すときは、再 publish してから AppHost を再起動してください。

## Windows 環境

- .NET 10 SDK をインストールします。CI は **10.0.x** を使います。このリポジトリには **global.json** がないため、SDK のパッチ版は固定されていません。
- Docker Desktop を起動し、Linux コンテナを実行できる状態にします。AppHost は Docker CLI と現在選択されている Docker context を使います。Docker Desktop の Engine が応答し、Engine の OS が **linux** と表示されることを確認してください。
- 初回起動ではコンテナイメージを取得するため、Docker Desktop の Engine から参照先のコンテナレジストリへ接続できる必要があります。
- 以下は Windows PowerShell で実行できます。別の Ubuntu Docker Engine を用意したり、WSL のディストリビューションを直接起動したりする手順はありません。
- **git clone** 後は、ソリューションを復元する前に submodule を初期化します。

PowerShell でリポジトリのルートへ移動して、次を実行します。

    git submodule update --init --recursive
    dotnet --list-sdks
    docker version
    docker info --format '{{.OSType}}'
    dotnet restore Duck.slnx

**dotnet --list-sdks** に .NET 10 SDK が表示されること、**docker version** に Client と Server の両方が表示されること、**docker info** の結果が **linux** であることを確認します。

## 通常の .NET テスト

テストプロジェクトの通常のテストを実行するコマンドです。

    dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj

これは AppHost を起動するコマンドではありません。**Tracker.Tests.csproj** は既定で **AspireFullStackAcceptanceTests.cs** をコンパイル対象から外します。そのため、通常の **dotnet test** の成功を実コンテナでの full-stack acceptance 成功として扱わないでください。

## Windows で base AppHost を起動する

AppHost が **Tracker.RuntimeHost** の Docker build context として使うのは、Release の publish 出力です。先に RuntimeHost を publish し、その後 AppHost を起動します。次の例は Windows の Docker Desktop を使う開発用 base 構成です。

    dotnet publish Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj --configuration Release --output Tracker/Tracker.RuntimeHost/bin/Release/net10.0/publish
    dotnet run --project Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj -- --Testing:Mode=base --Testing:Crane:Planner=rvo2

起動中は Aspire の出力に表示される Dashboard URL を開きます。ポートは実行ごとに割り当てられるため、固定 URL を前提にしません。AppHost を終了するには、その PowerShell ウィンドウで Ctrl+C を押します。既定の stack ownership lock は同時起動を拒否するため、別の Duck AppHost が起動中の場合は先に終了してください。

RuntimeHost のソースを変更した場合は、上記の **dotnet publish** を再実行してから AppHost を起動し直します。AppHost が publish 出力から Docker イメージを作り直して、そのコードをコンテナ内で実行します。

## comparison / match と full-stack acceptance

comparison モードで起動する場合は、RuntimeHost と同様に DebugHost の publish 出力も作ってから AppHost を実行します。

    dotnet publish Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj --configuration Release --output Tracker/Tracker.DebugHost/bin/Release/net10.0/publish
    dotnet run --project Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj -- --Testing:Mode=comparison --Testing:Crane:Planner=rvo2

match モードでは、RuntimeHost と RefereeDriver Host の publish 出力に加え、AppHost の出力フォルダーに Linux 用の **cm4-transport-control** と **autoref-game-log-agent.jar** が必要です。AppHost のコードはそれらのファイルを明示的に確認します。リポジトリの **.github/workflows/dotnet-test.yml** workflow は Ubuntu 上で C helper と Java 17 agent を準備し、AppHost をビルドしてから起動します。クリーンな Windows checkout でこれらの Linux 用生成物を作成する PowerShell スクリプトはリポジトリにありません。そのため、上記の base 例は Windows で試せる起動経路であり、match モードの Windows 起動手順を完全に検証したものではありません。

**AspireFullStackAcceptanceTests.cs** を含める CI 経路では、workflow が生成物を用意し、AppHost を起動して readiness を待ってから受入テストを実行します。手動実行時は **.github/workflows/dotnet-test.yml** の workflow_dispatch を使い、**comparison** または **match** を選択します。workflow の条件により受入 job が skip されることがあります。skip は成功を意味しないため、同じ HEAD の実行結果を確認してください。AppHost の起動だけでも受入テストの合格にはなりません。

match モードを起動するコードは次のとおりですが、先に RuntimeHost と RefereeDriver Host の publish 出力、ならびに AppHost の **obj/Debug/net10.0** に前述の2つの Linux 用生成物を準備してください。

    dotnet publish Testing/Duck.Testing.RefereeDriver.Host/Duck.Testing.RefereeDriver.Host.csproj --configuration Release --output Testing/Duck.Testing.RefereeDriver.Host/bin/Release/net10.0/publish
    dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --configuration Debug
    dotnet run --no-build --project Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj -- --Testing:Mode=match

これらの生成物をクリーンな Windows 環境で作成する方法はリポジトリに用意されていません。既存の Windows/Docker Desktop match 実行記録は設計文書に記載されていますが、その記録は現在の checkout に対する受入合格を証明するものではありません。

関連する設計と実行記録は [Aspire シミュレーションテスト環境](../Design/Testing/aspire-simulation-test-environment.md)を参照してください。
