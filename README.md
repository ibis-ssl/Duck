# Duck

Duck は SSL ロボット向けの支援リポジトリです。現在は SSL-Vision パケットの受信、Raw / Tracked 状態の可視化、公式トラッカーパケットの送信を行う Tracker 関連機能を中心にしています。

## リポジトリ構成

- `Tracker/Tracker.Core`: トラッカーのドメインロジック、パケット生成、実行契約。
- `Tracker/Tracker.RuntimeHost`: 画面を持たずに SSL-Vision 受信、トラッカー実行、公式トラッカーパケット送信を行う常駐プロセス。
- `Tracker/Tracker.DebugHost`: Raw / Tracked SSL-Vision data を表示し、CaptureOn や診断画面を提供する ASP.NET Core viewer/server。
- `Tracker/Tracker.CaptureReplay`: 保存済み capture を replay / analyze する CLI tool。
- `Tracker/Tracker.Tests`: トラッカーと server 周辺のテスト。
- `TrackerConnectionLib`: tracker 接続用の再利用ライブラリ。
- `TrackerConnectionLibExample`: `TrackerConnectionLib` のサンプルクライアント。
- `SslProto`: トラッカー関連 component が使う protocol binding。
- `reports`: 調査、レビュー、handover、検証レポート。

## 前提

- .NET SDK 10.0
- `Tracker.RuntimeHost` または `Tracker.DebugHost` を実行する場合は SSL-Vision 互換パケットの送信元

## ビルド

リポジトリのルートディレクトリから実行します。

```bash
dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
```

Codex sandbox で cache の影響を避けた証跡を取りたい場合は、project-local の一時領域を明示します。

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

## Tracker.DebugHost の起動

```bash
dotnet run --project Tracker/Tracker.DebugHost --launch-profile https
```

既定の launch profile endpoint:

- `https://localhost:7042`
- `http://localhost:5289`

`Tracker.DebugHost` の設定、UI、プロファイル切替、API の詳細は [Tracker/Tracker.DebugHost/README.md](Tracker/Tracker.DebugHost/README.md) を参照してください。

## Tracker.RuntimeHost の起動

```bash
dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile
```

`sim` プロファイルで起動する場合:

```bash
dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile -- --profile sim
```

`--profile <name>` は `Tracker.RuntimeHost` の起動時プロファイルを指定します。未指定時は appsettings の `Tracker:ActiveProfileName` を使います。
現在の `Tracker.RuntimeHost/appsettings.json` では `sim` プロファイルが既定で、`ReorderWindowNs=10000000`、つまり 10 ms の reorder window[^reorder-window] で起動します。

詳細は [Tracker/Tracker.RuntimeHost/README.md](Tracker/Tracker.RuntimeHost/README.md) を参照してください。

## appsettings の共通設定

`Tracker.RuntimeHost` と `Tracker.DebugHost` が共有する `Tracker` / `Tracker:Profiles:<name>` の設定は [Tracker/README.appsettings.md](Tracker/README.appsettings.md) を参照してください。

## `sim` プロファイルの DebugHost 起動例

`Tracker.DebugHost` を `sim` プロファイルで起動する場合:

```bash
dotnet run --project Tracker/Tracker.DebugHost --launch-profile https
```

`sim` プロファイルの既定設定では SSL-Vision を `224.5.23.2:10020` で受信し、公式トラッカーパケットを `224.5.23.2:11010` へ送信します。

## 脚注

[^reorder-window]: `reorder window` の意味は [Tracker appsettings README](Tracker/README.appsettings.md) の脚注を参照してください。
