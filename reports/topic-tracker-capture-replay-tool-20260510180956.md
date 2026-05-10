# Tracker capture replay tool report

## 目的

保存済み `*.jsonl.gz` packet capture を、後から再生・解析・自動検証できる CLI として再利用可能にする。
tool は複数ボール検出専用ではなく、capture replay の summary metric と条件式で任意の regression check に使える形にした。

## 追加した tool

- project: `Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj`
- `--settings` で外部設定を読み込める。
  - `Tracker.Server/appsettings.json` 形式
  - capture と同時に生成される `*.metadata.json` 形式
- 実行例:

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture <capture.jsonl.gz> \
  --settings <capture.metadata.json> \
  --profile sim
```

## capture sidecar

packet capture 開始時に、同じ basename で次のファイルを生成するようにした。

- `*.jsonl.gz`
  - packet capture 本体。
- `*.metadata.json`
  - capture 時の `Tracker` 設定全体と resolved tracker options を保存する。
  - `Tracker.CaptureReplay --settings <metadata.json>` で直接再利用できる。
- `*.tracker-diagnostics.log`
  - capture と対応する tracker diagnostics log。
  - capture 有効時は必ず capture sidecar として出力される。
  - `Tracker:Diagnostics:FilePath` が明示されている場合は、指定 file と capture sidecar の両方へ出力される。
- `*.render-snapshots.jsonl.gz`
  - timeline / 逆方向スクラブ用の描画 snapshot。
  - tracker engine の内部状態ではなく、commit 済み `TrackerFrame` だけを保存する。
  - tracker の挙動再現は packet capture replay 側に任せ、UI 用の表示状態だけを別途持つ。

## diagnostics log viewer

`Tracker.Server` に `/diagnostics` ページを追加した。

- `VisionReceiver:PacketCapture:DirectoryPath` 配下の `*.tracker-diagnostics.log` を一覧表示する。
- 選択した log file を timeline としてスクロール表示する。
- `trackedBalls > 1` の行を強調し、raw / tracked の ball / robot details を横並びで比較できる。
- `TrackerDiagnosticsLogReader` で diagnostics 行を parse し、UI と test から同じ parser を使う。

## 自動検証用 option

- `--expect <condition>`
  - summary metric の期待条件。
  - 失敗時は exit code `1`。
  - 例: `--expect 'committed-frames>0'`, `--expect 'max-balls<=1'`
- `--detail-filter <condition>`
  - 条件に一致する committed frame の詳細を出力する。
  - 複数指定時は AND 条件。
  - 例: `--detail-filter 'raw-balls==1' --detail-filter 'balls>=2'`
- `--max-details <count>`
  - 詳細出力数を制限する。

## 利用できる metric

summary metric:

- `packets`
- `detections`
- `geometries`
- `committed-frames`
- `max-balls`
- `max-robots`
- `max-raw-balls`
- `max-raw-yellow`
- `max-raw-blue`

frame detail filter metric:

- `balls`
- `robots`
- `raw-balls`
- `raw-yellow`
- `raw-blue`

## 実データ replay 結果

対象:

- `Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260510T085916791Z-d5eab90ef9f146029bc515a97be3894c.jsonl.gz`

実行:

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260510T085916791Z-d5eab90ef9f146029bc515a97be3894c.jsonl.gz \
  --profile sim \
  --expect 'committed-frames>0' \
  --expect 'max-balls>=2' \
  --detail-filter 'raw-balls==1' \
  --detail-filter 'balls>=2' \
  --max-details 8
```

結果:

- `packets=2944`
- `detections=2944`
- `geometries=1472`
- `committedFrames=732`
- `maxBalls=3`
- `maxRobots=23`
- `maxRawBalls=2`
- `maxRawYellow=10`
- `maxRawBlue=13`
- `committed-frames>0`: ok
- `max-balls>=2`: ok

raw ball が 1 個の frame でも replay 後に ball が 2 個以上になる箇所を detail filter で抽出できた。

## 設定値調整の一次調査

同じ capture を `Tracker.CaptureReplay --settings Tracker/Tracker.Server/appsettings.json --profile sim` で再生し、
ball tracker の主要 parameter を変更して `maxBalls` が 1 まで落ちるか確認した。

| 変更 | maxBalls | メモ |
| --- | ---: | --- |
| default sim | 3 | `ballGate=1`, `ballOutlierLimitMm=120`, `ballOutputVisibility=0`, `ballTrackLifetimeNs=1000000000` |
| `--ball-gate 1.25 --ball-outlier-limit-mm 150` | 2 | camera 間の近い split は一部減るが残る |
| `--ball-gate 2 --ball-outlier-limit-mm 240` | 2 | さらに広げても 1 にはならない |
| `--ball-output-visibility 0.5` | 3 | visibility だけでは改善しない |
| `--ball-output-visibility 0.8` | 2 | 低 visibility track は減るが残る |
| `--ball-output-visibility 1` | 2 | 1.0 まで上げても残る |
| `--ball-track-lifetime-ns 100000000` | 2 | 100ms でも残る |
| `--ball-track-lifetime-ns 50000000` | 2 | 50ms でも残る |
| `--ball-track-lifetime-ns 20000000` | 2 | 20ms でも残る |
| `--ball-gate 4.2 --ball-outlier-limit-mm 500 --ball-output-visibility 0.8 --ball-track-lifetime-ns 100000000` | 2 | かなり強い調整でも残る |

baseline の最初の split は、raw ball が 1 個でも output が 2 個になっている。

- `input=1093`, `rawFrame=576930`, `rawBalls=1`, `committedFrame=270`
- balls:
  - `#13 x=-1108.8 y=162.4 vis=1.000 cams=0`
  - `#14 x=-967.3 y=154.1 vis=1.000 cams=1`

強い調整後にも別の 2 ball 出力が残る。

- `input=1805`, `rawFrame=577286`, `rawBalls=0`, `committedFrame=448`
- balls:
  - `#23 x=5.3 y=1158.5 vis=1.000 cams=0/1`
  - `#1 x=-4957.2 y=1060.2 vis=0.989 cams=0`

現時点の判断:

- 軽い parameter 調整だけで `maxBalls=3` から `maxBalls=2` までは改善する。
- ただし `maxBalls=1` にはならないため、設定値調整だけで完了する問題とは判断しにくい。
- 残っている 2 ball は、単なる output visibility や track lifetime では消えない。
- 実装上、非 primary ball も `ObservationCount >= 3` なら出力候補に残る。
  - `Tracker/Tracker.Core/TrackerExecutionContracts.cs` の `CommitGroup` では、`index > 0 && ObservationCount < 3` の候補だけを捨てている。
  - `CollectMergedBallStates` は fresh track に近くない stale track を別 cluster として追加できる。
  - そのため、十分観測済みの stale ball track が fresh observation なしでも visibility が高い間は output に残り得る。
- 次は ball track の merge / stale track pruning / camera 別 detection association の実装差分を確認し、非 primary stale ball の出力条件を契約化する必要がある。

## 追加 capture replay 結果

対象:

- `Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260510T093810975Z-4ffc5f12a809424c8de4617947e2fcf9.jsonl.gz`
- sidecar:
  - `ssl-vision-packets-20260510T093810975Z-4ffc5f12a809424c8de4617947e2fcf9.metadata.json`
  - `ssl-vision-packets-20260510T093810975Z-4ffc5f12a809424c8de4617947e2fcf9.tracker-diagnostics.log`

`--settings <metadata.json>` で capture 時の `sim` 設定を読み込んで replay した。

結果:

- `packets=3036`
- `detections=3036`
- `geometries=1518`
- `committedFrames=755`
- `maxBalls=2`
- `maxRobots=22`
- `maxRawBalls=3`
- `maxRawYellow=10`
- `maxRawBlue=13`

設定値を変えた replay でも `maxBalls=2` のまま:

| 変更 | maxBalls |
| --- | ---: |
| default metadata settings | 2 |
| `--ball-gate 2 --ball-outlier-limit-mm 240` | 2 |
| `--ball-output-visibility 0.8` | 2 |
| `--ball-track-lifetime-ns 100000000` | 2 |
| `--ball-gate 4.2 --ball-outlier-limit-mm 500 --ball-output-visibility 0.8 --ball-track-lifetime-ns 100000000` | 2 |

この capture でも parameter 調整だけでは `maxBalls=1` へ落ちなかった。

## 検証

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet build Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore
```

結果: Build succeeded, Warning 0, Error 0

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~VisionPacketCaptureTests|FullyQualifiedName~TrackerCoordinatorTests.ProcessPacket_WithPacketCaptureSession_WritesDiagnosticsLogSidecar"
```

結果: Passed 4 / Failed 0

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerDiagnosticsLogReaderTests"
```

結果: Passed 1 / Failed 0

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
```

結果: `VisionReceiverConfigurationResolverTests.AppsettingsJson_ExposesPacketCaptureDefaults` が失敗。

原因:

- local の `Tracker/Tracker.Server/appsettings.json` で `VisionReceiver:PacketCapture:Enabled=true` になっている。
- test は repository default として `Enabled=false` を期待している。
- 実 capture 採取用の local config 差分であり、tool 追加による compile 失敗ではない。
