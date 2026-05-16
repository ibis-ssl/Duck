# `IbisDuck`

`IbisDuck` は競技支援用作業一式です。現在は SSL-Vision の受信、未加工入力と追跡済み状態の表示、公式追跡出力の送信を行う `Tracker` 関連機能を中心にしています。

## 構成

- `Tracker/Tracker.Core`: 追跡処理、送信用内容生成、実行契約。
- `Tracker/Tracker.RuntimeHost`: 画面を持たずに SSL-Vision 受信、追跡処理、公式追跡出力送信を行う常駐処理。
- `Tracker/Tracker.DebugHost`: SSL-Vision の未加工入力と追跡済み情報を表示し、CaptureOn や診断画面を提供する ASP.NET Core 実行体。
- `Tracker/Tracker.CaptureReplay`: 保存済み記録を再生、解析する CLI。
- `Tracker/Tracker.Tests`: 追跡処理と表示実行体周辺の試験。
- `TrackerConnectionLib`: 追跡器接続用の再利用部品。
- `TrackerConnectionLibExample`: `TrackerConnectionLib` の使用例。
- `SslProto`: 追跡器関連部品が使う通信形式結合。
- `reports`: 調査、点検、引き継ぎ、検証の報告書。

## 前提

- .NET SDK 10.0
- `Tracker.RuntimeHost` または `Tracker.DebugHost` を実行する場合は SSL-Vision 互換入力の送信元

## 構築

作業一式の最上位から実行します。

```bash
dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
```

Codex の隔離環境で一時保存領域の影響を避けた証跡を取りたい場合は、作業一式の内側に置く一時領域を明示します。

```bash
mkdir -p .codex-dotnet-home .codex-nuget-packages

DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-cache --force
```

## 試験

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

`Tracker.DebugHost` の設定、画面、設定名切替、API の詳細は `Tracker/Tracker.DebugHost/README.md` を参照してください。

## Tracker.RuntimeHost の起動

```bash
dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile
```

`sim` 設定で起動する場合:

```bash
dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile -- --profile sim
```

`--profile <name>` は `Tracker.RuntimeHost` の起動時設定名を指定します。未指定時は実行設定の `Tracker:ActiveProfileName` を使います。
現在の `Tracker.RuntimeHost/appsettings.json` では `sim` 設定が既定で、`ReorderWindowNs=10000000`、つまり 10 ms の並べ替え猶予時間[^1] で起動します。

詳細は `Tracker/Tracker.RuntimeHost/README.md` を参照してください。

## 実行設定の共通項目

`Tracker.RuntimeHost` と `Tracker.DebugHost` が共有する `Tracker` / `Tracker:Profiles:<name>` の設定は `Tracker/README.appsettings.md` を参照してください。

## `sim` 設定の `Tracker.DebugHost` 起動例

`Tracker.DebugHost` を `sim` 設定で起動する場合:

```bash
dotnet run --project Tracker/Tracker.DebugHost --launch-profile https
```

`sim` 設定の既定では SSL-Vision を `224.5.23.2:10020` で受信し、公式追跡出力を `224.5.23.2:11010` へ送信します。

## 脚注

[^1]: 並べ替え猶予時間の意味は `Tracker/README.appsettings.md` の脚注を参照してください。
