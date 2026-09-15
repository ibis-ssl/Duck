# Duck

Duck は SSL ロボット向けの支援リポジトリです。現在は SSL-Vision パケットの受信、未加工入力 / 追跡結果の可視化、公式形式のトラッカーパケットの送信を行うトラッカー関連機能を中心にしています。

## リポジトリ構成

- `Tracker/Tracker.Core`: トラッカーの中核処理、パケット生成、実行契約。
- `Tracker/Tracker.DebugHost`: 未加工入力 / 追跡結果の SSL-Vision データを表示する ASP.NET Core の表示画面兼サーバー。
- `Tracker/Tracker.CaptureReplay`: 保存済みの受信記録を再生・分析する CLI ツール。
- `Tracker/Tracker.Tests`: トラッカーとサーバー周辺のテスト。
- `TrackerConnectionLib`: トラッカー接続用の再利用ライブラリ。
- `TrackerConnectionLibExample`: `TrackerConnectionLib` の利用例となるクライアント。
- `SslProto`: トラッカーのコンポーネントが使う通信形式の生成型。
- `reports`: 調査、レビュー、引き継ぎ、検証レポート。

## 前提

- .NET SDK 10.0
- `Tracker.DebugHost` を実行する場合は SSL-Vision 互換のパケット送信元

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

## Tracker.DebugHost の起動

```bash
dotnet run --project Tracker/Tracker.DebugHost --launch-profile https
```

既定の起動設定の接続先:

- `https://localhost:7042`
- `http://localhost:5289`

`Tracker.DebugHost` の設定、UI、設定組の切り替え、API の詳細は [DebugHost の利用手順](Tracker/Tracker.DebugHost/README.md) を参照してください。

## `sim` 設定組で起動する例

`Tracker.RuntimeHost` を `sim` 設定組で起動する場合:

```bash
dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile -- --profile sim
```

`--profile <name>` は `Tracker.RuntimeHost` の起動時に有効な設定組を指定します。未指定時は `appsettings.json` の `Tracker:ActiveProfileName` を使います。
`Tracker.RuntimeHost` のリポジトリに保存済みの `sim` 設定組は `ReorderWindowNs=10000000`、つまり 10 ms の並べ替え対象時間幅で起動します。

`Tracker.DebugHost` を `sim` 設定組で起動する場合:

```bash
dotnet run --project Tracker/Tracker.DebugHost --launch-profile https
```

`sim` 設定組の既定設定では SSL-Vision を `224.5.23.2:10020` で受信し、公式形式のトラッカーパケットを `224.5.23.2:11010` へ送信します。
