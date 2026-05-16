# CAPTURE-REPLAY-001 遅延調査レポート

## 対象

- Task: `CAPTURE-REPLAY-001`
- Capture session:
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260514T132706328Z-4a19e61b92c443929f91eccdff3512c9`
- 方針:
  - capture file を手作業で読むのではなく、`Tracker.CaptureReplay` を汎用調査 tool として拡張して確認した。

## 追加した調査機能

- `--capture <session-folder>`:
  - session folder を渡すと、同 folder の `*.metadata.json` から packet capture と resolved tracker settings を解決する。
- `--analyze-latency`:
  - raw detection の `ReceivedAt` cadence と ibis tracker committed frame の `ReceivedAt` ベース commit lag を出す。
- `--max-latency-frames <count>`:
  - latency detail frame の表示件数を制限する。
- `--skip-tracker-snapshots`:
  - metadata 由来の大量 `trackerSnapshot` / `trackerComparison` 行を抑制する。

## 実行結果

実行コマンド:

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture /home/ibis/ssl/IbisDuck/Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260514T132706328Z-4a19e61b92c443929f91eccdff3512c9 \
  --analyze-latency \
  --max-latency-frames 8 \
  --skip-tracker-snapshots \
  --max-details 0
```

主要出力:

```text
settings=reorderWindowNs=100000000 mergeWindowNs=20000000
packets=2826 detections=2826 geometries=1413 committedFrames=703
latencySummary rawDetections=2826 committedFrames=703 rawAvgDeltaMs=7.997 committedAvgDeltaMs=32.000 avgCommitLagMs=111.813 maxCommitLagMs=117.302 maxCommitLagInputs=13 reorderWindowMs=100.000 mergeWindowMs=20.000
latencyOmittedFrames count=695
```

最初の detail 例:

```text
latencyFrame input=17 committedFrame=1 rawFrame=219751/219752 rawCamera=0/1 sourceReceivedAt=2026-05-14T13:27:06.3515890+00:00 commitReceivedAt=2026-05-14T13:27:06.4575049+00:00 commitLagMs=105.916 commitLagInputs=13 dataTs=225993769339350
```

対照実行:

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture /home/ibis/ssl/IbisDuck/Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260514T132706328Z-4a19e61b92c443929f91eccdff3512c9 \
  --analyze-latency \
  --max-latency-frames 4 \
  --skip-tracker-snapshots \
  --max-details 0 \
  --reorder-window-ns 0
```

主要出力:

```text
settings=reorderWindowNs=0 mergeWindowNs=20000000
packets=2826 detections=2826 geometries=1413 committedFrames=706
latencySummary rawDetections=2826 committedFrames=706 rawAvgDeltaMs=7.997 committedAvgDeltaMs=32.000 avgCommitLagMs=15.812 maxCommitLagMs=20.842 maxCommitLagInputs=1 reorderWindowMs=0.000 mergeWindowMs=20.000
```

## 原因判断

- raw vision detection は平均 `7.997ms` cadence で入っている。
- ibis tracker committed frame は平均 `32.000ms` cadence で出ている。
- 通常設定では `ReorderWindowNs=100ms` のため、commit lag は平均 `111.813ms`、最大 `117.302ms` になっている。
- `--reorder-window-ns 0` の対照実行では、同じ capture の commit lag が平均 `15.812ms`、最大 `20.842ms` まで下がった。
- ここでの commit lag は capture record の `ReceivedAt` と commit packet の `ReceivedAt` の差であり、event timestamp 差分ではない。

結論: この capture で ibis tracker が vision より遅れて見える主因は、`TrackerEngine` の reorder window による commit hold である。raw vision の到着自体が遅れているわけではなく、ibis tracker は out-of-order detection を待つために約 `100ms` 保持してから committed frame を出している。残る約 `15ms` から `20ms` は camera pair / merge window による統合待ちとして説明できる。

## 検証

- Red:
  - `CaptureReplayTests` は `AnalyzeLatency` / `LatencyLines` / session folder resolver 未実装で compile failure。
- Green:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~CaptureReplayTests -m:1 /nr:false`
  - Result: 11 passed.
- Build:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -m:1 /nr:false`
  - Result: 0 warnings / 0 errors.

## 残り

- dedicated review は未実施。現在の実行ルールでは sub-agent review に明示許可が必要なため、review gate は pending とする。
