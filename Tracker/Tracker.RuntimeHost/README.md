# `Tracker.RuntimeHost`

`Tracker.RuntimeHost` は、画面を持たずに SSL-Vision 通信内容を受信し、追跡器を実行して公式追跡出力を UDP 送信する常駐処理です。試合時や模擬器連携で、`Tracker.DebugHost` の画面や診断機能を使わずに追跡器だけを動かす用途を想定しています。

## できること

- `VisionReceiver` 設定に従って SSL-Vision 通信内容を受信する
- `Tracker:ActiveProfileName` または `--profile` で選んだ設定名を使って追跡器を実行する
- `Tracker:PublishUdp=true` のとき、確定した追跡結果[^追跡結果]から公式追跡出力を送信する
- `RuntimeHost:OperationLoopIntervalMilliseconds` の周期で追跡器実行繰り返しを動かす

`Tracker.DebugHost` の `Raw` / `Tracked` 表示、CaptureOn、`/diagnostics`、外部追跡器比較記録は持ちません。これらが必要な場合は [`Tracker.DebugHost` の文書](../Tracker.DebugHost/README.md) を参照してください。

## 前提

- .NET SDK `10.0`
- SSL-Vision 互換通信内容を送ってくる送信元
- 公式追跡出力の送信先となる多地点配信または単一先配信の受付先

## 起動方法

作業一式の最上位から実行します。

```bash
dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile
```

起動時設定名を命令行引数で指定する場合:

```bash
dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile -- --profile sim
```

`--profile <name>` は `Tracker:ActiveProfileName` への起動時上書きです。指定しない場合は [設定文書](./appsettings.json) の `Tracker:ActiveProfileName` を使います。空文字や値なしの `--profile` は異常です。指定した名前は `Tracker:Profiles` に存在する必要があります。

現在の `設定文書` は `ActiveProfileName` が `sim` です。`sim` 設定名の `ReorderWindowNs` は `10000000`、つまり 10 ms の並べ替え猶予[^reorder-window]です。

## 設定文書

`Tracker.RuntimeHost` 固有の主な設定は [設定文書](./appsettings.json) にあります。`Tracker` と `Tracker:Profiles:<name>` の共有設定は [`Tracker` 設定の文書](../README.appsettings.md) を参照してください。

### `VisionReceiver`

SSL-Vision 通信内容の受信設定です。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | 受信対象の多地点配信集合番地です。値が多地点配信範囲ならその集合へ参加します。 |
| `Port` | SSL-Vision 通信内容の受信 UDP 口番号です。 |
| `InterfaceAddress` | 多地点配信参加に使う手元 IPv4 番地です。`null` の場合は利用可能な IPv4 接続口を自動探索します。複数 NIC がある環境や参加失敗時は明示指定してください。 |

### `Tracker.RuntimeHost`

`Tracker.RuntimeHost` の実行周期を決める設定です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `OperationLoopIntervalMilliseconds` | ms | 追跡器実行繰り返しの待機周期です。既定の `16` はおおよそ 60 Hz 相当です。 |

### `Logging`

端末に出す記録の最小水準です。現在の設定では既定を `Warning` にし、`Tracker.RuntimeHost` だけ `Information` を出します。

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

### 起動時設定名を設定文書側で変更する

```json
{
  "Tracker": {
    "ActiveProfileName": "fast"
  }
}
```

起動引数の `--profile` がある場合は、`設定文書` より起動引数が優先されます。

## 注意点

- `Tracker.RuntimeHost` は起動中の設定名切替 API を持ちません。設定を変える場合は `設定文書` または `--profile` を変えて再起動してください。
- `Tracker.RuntimeHost/appsettings.json` と `Tracker.DebugHost/appsettings.json` は別文書です。どちらの実行体を起動しているかで効く設定が変わります。
- `ReorderWindowNs=0` は並べ替え猶予を持たない指定です。追跡器の遅れを調べるときは、実際に起動した実行体の設定と `--profile` の有無を確認してください。

## 脚注

[^追跡結果]: 追跡結果は、未加工の検出結果を追跡器が統合して出力する 1 結果分の追跡結果です。
[^reorder-window]: 並べ替え猶予は、通信内容内時刻が少し古い入力の到着を待つ猶予時間です。複数撮影元やネットワーク揺らぎで到着順が前後する場合に効きます。
