# Duck

Duck は SSL ロボット向けの支援リポジトリです。現在は SSL-Vision packet の受信、raw / tracked 状態の可視化、official tracker packet の publish を行う Tracker 関連機能を中心にしています。

## リポジトリ構成

- `Tracker/Tracker.Core`: tracker のドメインロジック、packet 生成、実行契約。
- `Tracker/Tracker.DebugHost`: raw / tracked SSL-Vision data を表示する ASP.NET Core viewer/server。
- `Tracker/Tracker.Tests`: tracker と server 周辺のテスト。
- `TrackerConnectionLib`: tracker 接続用の再利用ライブラリ。
- `TrackerConnectionLibExample`: `TrackerConnectionLib` のサンプルクライアント。
- `SslProto`: tracker component が使う protocol binding。
- `reports`: 調査、レビュー、handover、検証レポート。

## 前提

- .NET SDK 10.0
- `Tracker.DebugHost` を実行する場合は SSL-Vision 互換の packet 送信元

## ビルド

repository root から実行します。

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

`Tracker.DebugHost` の設定、UI、profile switch、API の詳細は [Tracker/Tracker.DebugHost/README.md](Tracker/Tracker.DebugHost/README.md) を参照してください。

## `sim` profile で起動する例

`Tracker.RuntimeHost` を `sim` profile で起動する場合:

```bash
dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile
```

`Tracker.DebugHost` を `sim` profile で起動する場合:

```bash
dotnet run --project Tracker/Tracker.DebugHost --launch-profile https
```

`sim` profile の既定設定では SSL-Vision を `224.5.23.2:10020` で受信し、official tracker packet を `224.5.23.2:11010` へ publish します。
