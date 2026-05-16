# `Tracker.CaptureReplay`

`Tracker.CaptureReplay` は、保存済み SSL-Vision 保存を追跡処理に再投入し、概要、詳細、遅延解析を命令行で確認するための道具です。通常の目視確認は `Tracker.DebugHost` の `/diagnostics` を使い、この道具は自動検証や回帰調査で同じ作業単位を再現するために使います。

## 基本実行

保存単位の置き場をそのまま渡すと、同じ置き場の付帯情報から通信内容保存と解決済み追跡器設定を解決します。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder>
```

保存記録と設定を明示する場合:

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder>/<capture>.jsonl.gz \
  --settings <session-folder>/<capture>.metadata.json \
  --profile sim
```

## 遅延解析

未加工入力に対して自前追跡器が遅れて見える場合は、保存記録を直接読む代わりに `--analyze-latency` を使います。未加工検出の受信間隔と、再生後に追跡結果が確定されるまでの `ReceivedAt` 基準の遅れを同じ出力で確認できます。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder> \
  --analyze-latency \
  --skip-tracker-snapshots \
  --max-latency-frames 8
```

`--reorder-window-ns 0` のように処理設定を一時上書きして対照実行すると、並べ替え猶予が遅延に与える影響を切り分けられます。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder> \
  --analyze-latency \
  --skip-tracker-snapshots \
  --max-latency-frames 8 \
  --reorder-window-ns 0
```

## よく使う引数

| 引数 | 用途 |
| --- | --- |
| `--capture <path>` | `*.jsonl.gz` 保存記録、または保存単位の置き場。 |
| `--settings <file>` | `Tracker.DebugHost/appsettings.json` 形式、または保存付帯情報。保存単位置き場入力では省略できます。 |
| `--profile <name>` | 設定から選ぶ追跡器設定名。既定は `sim`。 |
| `--analyze-latency` | 未加工入力 の受信間隔と追跡器確定遅れを出力します。 |
| `--max-latency-frames <count>` | 遅延詳細結果の最大出力数。 |
| `--skip-tracker-snapshots` | 付帯情報由来の `trackerSnapshot` / `trackerComparison` 行を抑制します。 |
| `--detail-filter <condition>` | 条件に合う確定済み結果の詳細を出力します。複数指定できます。 |
| `--expect <condition>` | 概要指標を検証し、失敗時は終了符号 `1` にします。 |
| `--merge-window-ns <value>` | 再生中だけ `Engine.MergeWindowNs` を上書きします。 |
| `--reorder-window-ns <value>` | 再生中だけ `Engine.ReorderWindowNs` を上書きします。 |

## 出力の見方

- `capture=...`: 実際に再生した保存記録。
- `settingsFile=...`: 実際に使った設定または付帯情報。
- `settings=...`: 再生に適用した主要追跡器設定。
- `packets=... committedFrames=...`: 再生の概要。
- `trackerSnapshot ...` / `trackerComparison ...`: 付帯情報補助文書から復元した保存時追跡器記録と比較。
- `latencySummary ...`: 未加工情報の受信間隔と追跡器確定遅れの概要。
- `latencyFrame ...`: 結果単位の遅延詳細。

`latencySummary` の確定遅れは保存記録の `ReceivedAt` と、追跡結果が確定された通信内容の `ReceivedAt` の差です。事象時刻差分ではなく、保存再生上で「未加工入力 が見えた時刻」と「追跡器確定が出た時刻」の差を見る指標です。`--reorder-window-ns` や `--merge-window-ns` を変えた対照実行で遅れが下がる場合、その時間幅設定が見かけの遅れに寄与しています。

## 自動検証

`--expect` は自動検証用の簡易判定です。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -- \
  --capture <session-folder> \
  --expect committed-frames\>0 \
  --expect max-balls\<=1
```

利用できる概要指標は `--help` で確認できます。
