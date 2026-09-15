# Tracker.CaptureReplay

`Tracker.CaptureReplay` は、保存済みの SSL-Vision キャプチャーを追跡エンジンに再投入し、概要 / 詳細 / 遅延分析を CLI で確認するためのツールです。通常の目視確認は `Tracker.DebugHost` の `/diagnostics` を使い、このツールはエージェント / 自動検証 / 回帰調査で同じ記録単位を再現するために使います。


本書で raw vision は SSL-Vision の検出情報を指す。カメラの画像や動画そのものではない。
## 基本実行

session folder をそのまま渡すと、同じフォルダの付随情報からキャプチャーと解決済みのトラッカー設定を取得します。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder>
```

キャプチャーファイルと設定ファイルを明示する場合:

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder>/<capture>.jsonl.gz \
  --settings <session-folder>/<capture>.metadata.json \
  --profile sim
```

## 遅延分析

raw vision に対して自前トラッカーが遅れて見える場合は、キャプチャーファイルを直接読む代わりに `--analyze-latency` を使います。未加工の検出情報の受信周期と、再生後に追跡フレームが確定するまでの `ReceivedAt` 基準の遅延を同じ出力で確認できます。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder> \
  --analyze-latency \
  --skip-tracker-snapshots \
  --max-latency-frames 8
```

`--reorder-window-ns 0` のように追跡エンジンの設定を一時的に上書きして対照実行すると、並べ替え対象時間幅が遅延に与える影響を切り分けられます。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder> \
  --analyze-latency \
  --skip-tracker-snapshots \
  --max-latency-frames 8 \
  --reorder-window-ns 0
```

## よく使うコマンドラインオプション

| コマンドラインオプション | 用途 |
| --- | --- |
| `--capture <path>` | `*.jsonl.gz` のキャプチャーファイル、または session folder。 |
| `--settings <file>` | `Tracker.DebugHost/appsettings.json` 形式の設定、または capture metadata。session folder を入力する場合は省略できます。 |
| `--profile <name>` | 設定から選ぶトラッカーの設定プロファイル。既定は `sim`。 |
| `--analyze-latency` | raw vision の受信周期と、トラッカーの確定遅延を出力します。 |
| `--max-latency-frames <count>` | 追跡フレームごとの遅延詳細の最大出力数。 |
| `--skip-tracker-snapshots` | 付随情報由来の `trackerSnapshot` / `trackerComparison` 行を抑制します。 |
| `--detail-filter <condition>` | 条件に合う確定済みの追跡フレームの詳細を出力します。複数指定できます。 |
| `--expect <condition>` | 集計指標を検証し、失敗時は exit code を 1 にします。 |
| `--merge-window-ns <value>` | 再生中だけ `Engine.MergeWindowNs` を上書きします。 |
| `--reorder-window-ns <value>` | 再生中だけ `Engine.ReorderWindowNs` を上書きします。 |

## 出力の見方

- `capture=...`: 実際に再生したキャプチャーファイル。
- `settingsFile=...`: 実際に使った設定または付随情報。
- `settings=...`: 再生に適用した主なトラッカー設定。
- `packets=... committedFrames=...`: 再生結果の概要。
- `trackerSnapshot ...` / `trackerComparison ...`: 付随する補助ファイルから復元した、保存時のトラッカースナップショット / 比較結果。
- `latencySummary ...`: 未加工入力の受信周期とトラッカーの確定遅延の概要。
- `latencyFrame ...`: 出力数を制限した追跡フレームごとの遅延詳細。

`latencySummary` の確定遅延はキャプチャーの `ReceivedAt` と、追跡フレームが確定したパケットの `ReceivedAt` の差です。イベント時刻の差ではなく、保存記録の再生上で「raw vision が見えた時刻」と「トラッカーの確定結果が出た時刻」の差を見る指標です。`--reorder-window-ns` や `--merge-window-ns` を変えた対照実行で遅延が下がる場合、その対象時間幅の設定が見かけの遅れに寄与しています。

## 自動検証

`--expect` は自動検証用の簡易な期待条件の検証です。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder> \
  --expect committed-frames\>0 \
  --expect max-balls\<=1
```

利用できる集計指標は `--help` で確認できます。
