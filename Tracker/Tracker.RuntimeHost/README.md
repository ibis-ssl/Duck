# Tracker.RuntimeHost

`Tracker.RuntimeHost` は、画面を持たずに SSL-Vision[^ssl-vision] パケットを受信し、トラッカーを実行して公式トラッカーパケットを UDP[^udp] 送信する常駐プロセスです。試合時やシミュレータ連携で、DebugHost の画面や診断機能を使わずにトラッカーだけを動かす用途を想定しています。

## できること

- `VisionReceiver` 設定に従って SSL-Vision パケットを受信する
- `Tracker:ActiveProfileName` または `--profile` で選んだプロファイルを使ってトラッカーを実行する
- `Tracker:PublishUdp=true` のとき、確定した tracked frame[^tracked-frame] から公式トラッカーパケットを送信する
- `RuntimeHost:OperationLoopIntervalMilliseconds` の周期でトラッカー実行ループを動かす

DebugHost の Raw / Tracked 表示、CaptureOn、`/diagnostics`、外部トラッカー比較ログは持ちません。これらが必要な場合は [Tracker.DebugHost README](../Tracker.DebugHost/README.md) を参照してください。

## 前提

- .NET SDK `10.0`
- SSL-Vision 互換パケットを送ってくる送信元
- 公式トラッカーパケットの送信先となる multicast / unicast の endpoint

## 起動方法

リポジトリのルートディレクトリから実行します。

```bash
dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile
```

起動時プロファイルをコマンドライン引数で指定する場合:

```bash
dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile -- --profile sim
```

`--profile <name>` は `Tracker:ActiveProfileName` への起動時上書きです。指定しない場合は [appsettings.json](./appsettings.json) の `Tracker:ActiveProfileName` を使います。空文字や値なしの `--profile` はエラーです。指定した名前は `Tracker:Profiles` に存在する必要があります。

現在の `appsettings.json` は `ActiveProfileName` が `sim` です。`sim` プロファイルの `ReorderWindowNs` は `10000000`、つまり 10 ms の reorder window[^reorder-window] です。

## 設定ファイル

RuntimeHost 固有の主な設定は [appsettings.json](./appsettings.json) にあります。`Tracker` と `Tracker:Profiles:<name>` の共有設定は [Tracker appsettings README](../README.appsettings.md) を参照してください。

### `VisionReceiver`

SSL-Vision パケットの受信設定です。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | 受信対象の multicast group address です。値が multicast 範囲ならその group へ join します。 |
| `Port` | SSL-Vision パケットの受信 UDP ポートです。 |
| `InterfaceAddress` | multicast join に使う local IPv4 address です。`null` の場合は利用可能な IPv4 interface を自動探索します。複数 NIC[^nic] がある環境や join 失敗時は明示指定してください。 |

### `RuntimeHost`

RuntimeHost の実行周期を決める設定です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `OperationLoopIntervalMilliseconds` | ms | トラッカー実行ループの待機周期です。既定の `16` はおおよそ 60 Hz 相当です。 |

### `Logging`

コンソールに出すログの最小レベルです。現在の設定では既定を `Warning` にし、`Tracker.RuntimeHost` だけ `Information` を出します。

```json
"Logging": {
  "LogLevel": {
    "Default": "Warning",
    "Tracker.RuntimeHost": "Information"
  }
}
```

## 典型的な変更例

### 受信 NIC を固定する

```json
{
  "VisionReceiver": {
    "MulticastAddress": "224.5.23.2",
    "Port": 10020,
    "InterfaceAddress": "192.168.10.5"
  }
}
```

### 起動時プロファイルを appsettings 側で変更する

```json
{
  "Tracker": {
    "ActiveProfileName": "fast"
  }
}
```

起動引数の `--profile` がある場合は、`appsettings.json` より起動引数が優先されます。

## 注意点

- RuntimeHost は起動中のプロファイル切替 API を持ちません。設定を変える場合は `appsettings.json` または `--profile` を変えて再起動してください。
- `Tracker.RuntimeHost/appsettings.json` と `Tracker.DebugHost/appsettings.json` は別ファイルです。どちらのホストを起動しているかで効く設定が変わります。
- `ReorderWindowNs=0` は reorder window を持たない指定です。トラッカーの遅れを調べるときは、実際に起動したホストの設定と `--profile` の有無を確認してください。

## 脚注

[^ssl-vision]: SSL-Vision は RoboCup Small Size League で使われる vision system です。
[^udp]: UDP は User Datagram Protocol の略です。
[^nic]: NIC は Network Interface Card の略で、ここでは受信に使う network interface を指します。
[^tracked-frame]: tracked frame は、raw detection をトラッカーが統合して出力する 1 フレーム分の追跡結果です。
[^reorder-window]: reorder window は、event time が少し古いパケットの到着を待つ猶予時間です。複数 camera や network jitter で到着順が前後する場合に効きます。
