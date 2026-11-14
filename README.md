# Duck

Duck は SSL ロボット向けの支援リポジトリです。現在は SSL-Vision パケットの受信、未加工入力 / 追跡結果の可視化、公式形式の `TrackerWrapperPacket` の送信を行うトラッカー関連機能を中心にしています。

Windows と Docker Desktop を使った Aspire 開発環境の準備・起動手順と構成図は、[開発用シミュレーション環境の利用手順](Testing/README.md)を参照してください。

## リポジトリ構成

- `Tracker/Tracker.Core`: トラッカーの中核処理、パケット生成、実行契約。
- `Tracker/Tracker.RuntimeHost`: 画面なしで SSL-Vision を受信し、トラッカーを周期実行して公式形式の `TrackerWrapperPacket` を送信する実行用アプリケーション。
- `Tracker/Tracker.DebugHost`: SSL-Vision から受信した検出情報とトラッカーの追跡結果を表示する ASP.NET Core のアプリケーション。
- `Tracker/Tracker.CaptureReplay`: 保存済みのキャプチャーを再生・分析する CLI ツール。
- `Tracker/Tracker.Tests`: トラッカーとサーバー周辺のテスト。
- `TrackerConnectionLib`: トラッカー接続用の再利用ライブラリ。
- `TrackerConnectionLibExample`: `TrackerConnectionLib` の利用例となるクライアント。
- `SslProto`: トラッカーのコンポーネントが使う通信形式の生成型。
- `reports`: 調査、レビュー、引き継ぎ、検証レポート。

## 参考仕様資料

- [reference/specs](reference/specs): ibis-sslの仕様資料を参照用のGit submoduleとして配置します。親リポジトリは参照する固定commitを記録し、仕様資料を更新する際はsubmoduleの参照commitを更新します。Duckの実行時依存には含みません。

## 前提

- .NET SDK 10.0
- `Tracker.RuntimeHost` または `Tracker.DebugHost` で SSL-Vision を受信する場合は SSL-Vision 互換のパケット送信元

## ビルド

リポジトリの最上位から実行します。

```bash
dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
```

Codex の隔離環境でキャッシュの影響を避けた検証記録を取りたい場合は、リポジトリ内の一時領域を明示します。

```bash
mkdir -p .codex-dotnet-home .codex-nuget-packages

DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-cache --force
```

## テスト

```bash
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
```

## Aspire全体受入の起動設定

全体受入試験では、AppHostと受入試験に同じ識別子と設定ファイルの場所を渡します。設定ファイルは実行ごとに別の名前を使い、既存のものを再利用しません。AppHostの起動前に次を設定してください。

```bash
stack_id="local-$(date -u +%Y%m%dT%H%M%SZ)-$$"
results_dir="$PWD/artifacts/aspire-full-stack"
mkdir -p "$results_dir"
manifest_path="$results_dir/crane-fixture-$stack_id.json"
export Testing__StackOwnership__StackId="$stack_id"
export Testing__StackOwnership__LockPath="${TMPDIR:-/tmp}/duck-aspire-$stack_id.lock"
export Testing__Crane__FixtureManifestPath="$manifest_path"
export DUCK_ASPIRE_STACK_ID="$stack_id"
export DUCK_ASPIRE_FIXTURE_MANIFEST="$manifest_path"
```

起動から全資源の準備完了、所有者と識別子の確認、受入試験、所有資源の後始末まで、CIと同じ順序で行います。手順と後始末の対象は[CIの設定](.github/workflows/dotnet-test.yml)を参照してください。準備完了前に全体受入試験を開始しません。

## Tracker.DebugHost の起動

```bash
dotnet run --project Tracker/Tracker.DebugHost --launch-profile https
```

既定の起動設定の接続先:

- `https://localhost:7042`
- `http://localhost:5289`

`Tracker.DebugHost` の設定、UI、設定プロファイルの切り替え、API の詳細は [DebugHost の利用手順](Tracker/Tracker.DebugHost/README.md) を参照してください。

## `sim` 設定プロファイルで起動する例

`Tracker.RuntimeHost` を `sim` 設定プロファイルで起動する場合:

```bash
dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile -- --profile sim
```

`--profile <name>` は `Tracker.RuntimeHost` の起動時に有効な設定プロファイルを指定します。未指定時は `appsettings.json` の `Tracker:ActiveProfileName` を使います。
リポジトリに含まれる `Tracker.RuntimeHost` の `sim` 設定プロファイルは、`ReorderWindowNs=10000000`、つまり 10 ms の reorder window で起動します。

`Tracker.DebugHost` を `sim` 設定プロファイルで起動する場合:

```bash
dotnet run --project Tracker/Tracker.DebugHost --launch-profile https
```

`sim` 設定プロファイルの既定設定では SSL-Vision を `224.5.23.2:10020` で受信し、公式形式の `TrackerWrapperPacket` を `224.5.23.2:11010` へ送信します。

## Duck base simulationのローカル起動

Windows PowerShellでリポジトリルートから実行します。スクリプトは`Tracker.RuntimeHost`、`Tracker.DebugHost`、`Duck.Testing.RefereeDriver.Host`をReleaseで再publishし、AppHostをDebugでbuildしてからbase simulationを起動します。以前にstale publishが発生したため、毎回publishしています。Ctrl+CでSessionを止めるとSessionコンテナは後片付けされます。

```powershell
.\scripts\start-duck-simulation.ps1
```

Trackerのテストは次のコマンドで実行します（AppHost停止後に実行してください）。

```powershell
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
```

Persistentにする場合は、各起動で同じIDを指定します。使い終わったら同じIDを指定して明示的に停止・削除します。AppHostを再起動しても既存Persistentコンテナは再利用されることがあり、毎回の再publishだけではコンテナ内のコード更新を保証しません。Trackerコードの更新を確実に反映する場合はSessionを使うか、Persistentスタックを `-StopPersistent` で削除してから再起動してください。環境変数などの設定差でもリソースが再作成されることがあります。この作業の同一コード再起動では7件中4件が再利用され3件が再作成されましたが、コード変更の反映は検証していません。

```powershell
.\scripts\start-duck-simulation.ps1 -Persistent -PersistentStackId duck-local-dev
# AppHostを終了した後、同じコマンドで再起動
.\scripts\start-duck-simulation.ps1 -Persistent -PersistentStackId duck-local-dev
# 完了後のcleanup
.\scripts\start-duck-simulation.ps1 -StopPersistent -PersistentStackId duck-local-dev
```

この手順はbase simulationとTrackerのローカル確認用です。Game Controllerのmatch設定とUI操作は手動です。Auto-refereeテストは今後の用途で、まだ実装・検証していません。
