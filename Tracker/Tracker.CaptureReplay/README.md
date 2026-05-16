# Tracker.CaptureReplay

`Tracker.CaptureReplay` は、保存済み SSL-Vision capture を tracker engine に再投入し、summary / detail / latency analysis を CLI で確認するための tool です。通常の目視確認は `Tracker.DebugHost` の `/diagnostics` を使い、この tool は agent / 自動検証 / regression 調査で同じ session を再現するために使います。

## 基本実行

capture session folder をそのまま渡すと、同じ folder の metadata から packet capture と resolved tracker settings を解決します。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder>
```

capture file と settings を明示する場合:

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder>/<capture>.jsonl.gz \
  --settings <session-folder>/<capture>.metadata.json \
  --profile sim
```

## latency analysis

raw vision に対して ibis tracker が遅れて見える場合は、capture file を直接読む代わりに `--analyze-latency` を使います。raw detection の受信 cadence と、replay 後に tracker frame が commit されるまでの `ReceivedAt` ベースの lag を同じ出力で確認できます。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder> \
  --analyze-latency \
  --skip-tracker-snapshots \
  --max-latency-frames 8
```

`--reorder-window-ns 0` のように engine setting を一時 override して対照実行すると、reorder window が遅延に与える影響を切り分けられます。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder> \
  --analyze-latency \
  --skip-tracker-snapshots \
  --max-latency-frames 8 \
  --reorder-window-ns 0
```

## よく使う option

| option | 用途 |
| --- | --- |
| `--capture <path>` | `*.jsonl.gz` capture file、または capture session folder。 |
| `--settings <file>` | `Tracker.DebugHost/appsettings.json` 形式、または capture metadata。session folder 入力では省略できます。 |
| `--profile <name>` | settings から選ぶ tracker profile。既定は `sim`。 |
| `--analyze-latency` | raw vision cadence と tracker commit lag を出力します。 |
| `--max-latency-frames <count>` | latency detail frame の最大出力数。 |
| `--skip-tracker-snapshots` | metadata 由来の `trackerSnapshot` / `trackerComparison` 行を抑制します。 |
| `--detail-filter <condition>` | 条件に合う committed frame の detail を出力します。複数指定できます。 |
| `--expect <condition>` | summary metric を検証し、失敗時は exit code 1 にします。 |
| `--merge-window-ns <value>` | replay 中だけ `Engine.MergeWindowNs` を上書きします。 |
| `--reorder-window-ns <value>` | replay 中だけ `Engine.ReorderWindowNs` を上書きします。 |

## 出力の見方

- `capture=...`: 実際に replay した capture file。
- `settingsFile=...`: 実際に使った settings または metadata。
- `settings=...`: replay に適用した主要 tracker settings。
- `packets=... committedFrames=...`: replay summary。
- `trackerSnapshot ...` / `trackerComparison ...`: metadata sidecar から復元した保存時 tracker snapshot / comparison。
- `latencySummary ...`: raw cadence と tracker commit lag の summary。
- `latencyFrame ...`: bounded な frame-level latency detail。

`latencySummary` の commit lag は capture record の `ReceivedAt` と、tracker frame が commit された packet の `ReceivedAt` の差です。event timestamp 差分ではなく、capture replay 上で「raw vision が見えた時刻」と「tracker commit が出た時刻」の差を見る指標です。`--reorder-window-ns` や `--merge-window-ns` を変えた対照実行で lag が下がる場合、その window 設定が見かけの遅れに寄与しています。

## 自動検証

`--expect` は automation 用の簡易 assertion です。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder> \
  --expect committed-frames\>0 \
  --expect max-balls\<=1
```

利用できる summary metrics は `--help` で確認できます。
