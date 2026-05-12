# Sub-agent実行レポート

## タスク

- 目的: `TRACKER-053` PR ready gate のため、local synthetic UDP stream と CaptureOn UI 操作を通した end-to-end manual evidence を採取できるか確認し、可能なら採取する。
- タスク種別: environment verification / end-to-end manual evidence

## sub-agentを使う理由

- 理由: ユーザー指定により、実装・調査・レビューは gpt-5.5 high sub-agent を使う。PR ready 判断に関わる CaptureOn end-to-end evidence は独立確認が必要。

## 対象範囲

- 対象:
  - `Tracker.Server` の CaptureOn UI
  - SSL-Vision synthetic UDP stream
  - official tracker synthetic UDP stream
  - CaptureOn session folder / metadata / diagnostics log / tracker packet snapshot sidecar
  - `/diagnostics` Tracker Comparison panel

## 対象外

- 対象外:
  - production / test / docs / tracking の変更
  - PR body の実更新
  - draft解除操作
  - commit / push / PR 操作

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,260p' reports/tracker-053-captureon-e2e-evidence-20260512224740.md`
  - `sed -n '1,260p' reports/tracker-053-manual-evidence-20260512222737.md`
  - `sed -n '1,260p' reports/tracker-053-pr-ready-prep-20260512221920.md`
  - `rg -n "CaptureOn|Capture On|Capture Off|Tracker Comparison|manual evidence|diagnostics|Tracker:Receive|Receive|PacketCapture|tracker-packet-snapshots|metadata|UDP|multicast" Tracker/Tracker.Server/README.md`
  - `sed -n '180,260p' Tracker/Tracker.Server/README.md`
  - `rg -n "TRACKER-053|TRACKER-052|TRACKER-051|CaptureOn|manual evidence|comparison" Tracker/Tracker.Core/Design/tasks-status.md`
  - `git status --short && git rev-parse --abbrev-ref HEAD && git rev-parse --short HEAD`
  - `sed -n '1,110p' Tracker/Tracker.Server/Program.cs`
  - `sed -n '1,260p' Tracker/Tracker.Server/Components/Pages/Home.razor`
  - `sed -n '1,170p' Tracker/Tracker.Server/appsettings.json`
  - `find Tracker SslProto TrackerConnectionLib -path '*/bin/*' -type f \\( -name 'Tracker.Server.dll' -o -name 'Tracker.Core.dll' -o -name 'SslProto.dll' -o -name 'TrackerConnectionLib.dll' -o -name 'Tracker.Tests.dll' \\) -print`
  - `dotnet new console -n Tracker053E2eSender -o /tmp/tracker053-e2e-sender --framework net10.0`
  - `DOTNET_CLI_HOME=/tmp/tracker053-e2e-dotnet-home NUGET_PACKAGES=/tmp/tracker053-e2e-nuget dotnet build /tmp/tracker053-e2e-sender/Tracker053E2eSender.csproj --no-restore -m:1 /nr:false`
  - `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5299 VisionReceiver__MulticastAddress=127.0.0.1 VisionReceiver__Port=12020 VisionReceiver__Profiles__sim__MulticastAddress=127.0.0.1 VisionReceiver__Profiles__sim__Port=12020 VisionReceiver__PacketCapture__DirectoryPath=/tmp/tracker053-e2e-captures Tracker__Receive__Enabled=true Tracker__RuntimeOverrides__Publish__MulticastAddress=127.0.0.1 Tracker__RuntimeOverrides__Publish__Port=12010 ./Tracker.Server`
  - `HOME=/tmp/tracker053-e2e-edge-home XDG_CONFIG_HOME=/tmp/tracker053-e2e-edge-config XDG_CACHE_HOME=/tmp/tracker053-e2e-edge-cache microsoft-edge --headless --disable-gpu --no-sandbox --disable-dev-shm-usage --user-data-dir=/tmp/tracker053-e2e-edge-profile --remote-debugging-port=9333 --remote-allow-origins='*' http://127.0.0.1:5299/`
  - `node --input-type=module - <<'JS' ... DevTools Protocol で Capture Off 表示の button を click し、Capture On 状態 screenshot を保存 ... JS`
  - `DOTNET_CLI_HOME=/tmp/tracker053-e2e-dotnet-home NUGET_PACKAGES=/tmp/tracker053-e2e-nuget dotnet run --project /tmp/tracker053-e2e-sender/Tracker053E2eSender.csproj --no-build -- --vision-host 127.0.0.1 --vision-port 12020 --tracker-host 127.0.0.1 --tracker-port 12010 --count 10 --delay-ms 120`
  - `node --input-type=module - <<'JS' ... DevTools Protocol で Capture On 表示の button を click し、Capture Off 状態 screenshot を保存 ... JS`
  - `find /tmp/tracker053-e2e-captures -maxdepth 2 -type f -printf '%p %s bytes\\n' | sort`
  - `jq '{SessionFolder, PacketPath, MetadataPath, DiagnosticsLogPath, RenderSnapshotPath, TrackerSnapshotSidecarPath, TrackerSnapshotLog, TrackerSnapshotSources}' /tmp/tracker053-e2e-captures/.../*.metadata.json`
  - `wc -l /tmp/tracker053-e2e-captures/.../tracker-packet-snapshots.jsonl`
  - `node --input-type=module - <<'JS' ... /diagnostics で diagnostics log 選択、source filter All / External / source-label:thirdparty-e2e の panel text と screenshot を採取 ... JS`
  - `dotnet Tracker/Tracker.CaptureReplay/bin/Debug/net10.0/Tracker.CaptureReplay.dll --capture /tmp/tracker053-e2e-captures/.../*.jsonl.gz --settings /tmp/tracker053-e2e-captures/.../*.metadata.json --profile sim`
  - `file /tmp/tracker053-e2e-screenshots/home-capture-on.png /tmp/tracker053-e2e-screenshots/home-capture-off.png /tmp/tracker053-e2e-screenshots/diagnostics-comparison-thirdparty-e2e.png`
  - `ps -e -o pid=,comm= | awk '$2=="microsoft-edge" || $2=="Tracker.Server" || $2=="Tracker053E2e" || $2=="Tracker053E2eSender" || $2=="dotnet" {print}'`
  - `git status --short`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/tracker-053-captureon-e2e-evidence-20260512224740.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - 確認: `reports/tracker-053-manual-evidence-20260512222737.md`
  - 確認: `reports/tracker-053-pr-ready-prep-20260512221920.md`
  - 確認: `Tracker/Tracker.Server/README.md`
  - 確認: `Tracker/Tracker.Core/Design/tasks-status.md`
  - 確認: `Tracker/Tracker.Server/Program.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Home.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - 確認: `Tracker/Tracker.Server/Vision/VisionReceiverService.cs`
  - 確認: `Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerConnectionLibReceiverHostedService.cs`
  - 確認: `TrackerConnectionLib/src/UdpTrackerReceiver.cs`
  - 作成/確認: `/tmp/tracker053-e2e-sender/Tracker053E2eSender.csproj`
  - 作成/確認: `/tmp/tracker053-e2e-sender/Program.cs`
  - 作成/確認: `/tmp/tracker053-e2e-captures/ssl-vision-packets-20260512T135558249Z-d31c0bebde834ab9b6b874ac44aa5490/`
  - 作成/確認: `/tmp/tracker053-e2e-screenshots/home-capture-on.png`
  - 作成/確認: `/tmp/tracker053-e2e-screenshots/home-capture-off.png`
  - 作成/確認: `/tmp/tracker053-e2e-screenshots/diagnostics-comparison-thirdparty-e2e.png`
  - 作成/確認: `/tmp/tracker053-e2e-cdp-evidence.json`

## 指摘事項

- 指摘要約または「指摘なし」:
  - 指摘なし。
  - production / test / docs / tracking の repo 内変更は実施していない。
  - repo 内で今回変更したのは、この report file の記入のみ。
  - evidence は real `Tracker.Server`、real browser 操作、real UDP receive path を通して採取した。ただし入力 stream は local synthetic UDP であり、外部実機 hardware / simulator 由来ではない。

## 結果

- 結果:
  - evidence 採取可否:
    - 採取成功。
    - `Tracker.Server` を既存 `Tracker/Tracker.Server/bin/Debug/net10.0/Tracker.Server` から起動し、`Tracker:Receive:Enabled=true`、packet capture dir `/tmp/tracker053-e2e-captures`、SSL-Vision receiver `127.0.0.1:12020`、tracker publish/receive `127.0.0.1:12010` に runtime override した。
    - Microsoft Edge headless + DevTools Protocol で `/` を開き、UI button を実 click して `Capture Off` 表示から `Capture On` 表示へ切り替えた。screenshot: `/tmp/tracker053-e2e-screenshots/home-capture-on.png`。
    - `/tmp/tracker053-e2e-sender` の helper から SSL-Vision synthetic UDP packet 11 packets（geometry 1、detection 10）を `127.0.0.1:12020` へ送信した。
    - 同じ helper から official tracker synthetic UDP packet 10 packets（source `thirdparty-e2e`, uuid `thirdparty-e2e-uuid`）を `127.0.0.1:12010` へ送信した。
    - server 自身の tracker publisher も `127.0.0.1:12010` へ own packet を publish し、live tracker receiver が own packet と external packet の両方を見た。
    - Microsoft Edge headless + DevTools Protocol で UI button を再 click し、`Capture On` 表示から `Capture Off` 表示へ切り替えた。screenshot: `/tmp/tracker053-e2e-screenshots/home-capture-off.png`。
  - 起動 command:
    - `ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5299 VisionReceiver__MulticastAddress=127.0.0.1 VisionReceiver__Port=12020 VisionReceiver__Profiles__sim__MulticastAddress=127.0.0.1 VisionReceiver__Profiles__sim__Port=12020 VisionReceiver__PacketCapture__DirectoryPath=/tmp/tracker053-e2e-captures Tracker__Receive__Enabled=true Tracker__RuntimeOverrides__Publish__MulticastAddress=127.0.0.1 Tracker__RuntimeOverrides__Publish__Port=12010 ./Tracker.Server`
  - sender command:
    - `DOTNET_CLI_HOME=/tmp/tracker053-e2e-dotnet-home NUGET_PACKAGES=/tmp/tracker053-e2e-nuget dotnet run --project /tmp/tracker053-e2e-sender/Tracker053E2eSender.csproj --no-build -- --vision-host 127.0.0.1 --vision-port 12020 --tracker-host 127.0.0.1 --tracker-port 12010 --count 10 --delay-ms 120`
  - session folder:
    - `/tmp/tracker053-e2e-captures/ssl-vision-packets-20260512T135558249Z-d31c0bebde834ab9b6b874ac44aa5490`
  - session folder contents:
    - `ssl-vision-packets-20260512T135558249Z-d31c0bebde834ab9b6b874ac44aa5490.jsonl.gz`: 726 bytes
    - `ssl-vision-packets-20260512T135558249Z-d31c0bebde834ab9b6b874ac44aa5490.metadata.json`: 9088 bytes
    - `ssl-vision-packets-20260512T135558249Z-d31c0bebde834ab9b6b874ac44aa5490.tracker-diagnostics.log`: 1245 bytes
    - `ssl-vision-packets-20260512T135558249Z-d31c0bebde834ab9b6b874ac44aa5490.render-snapshots.jsonl.gz`: 952 bytes
    - `tracker-packet-snapshots.jsonl`: 16459 bytes
  - metadata:
    - path: `/tmp/tracker053-e2e-captures/ssl-vision-packets-20260512T135558249Z-d31c0bebde834ab9b6b874ac44aa5490/ssl-vision-packets-20260512T135558249Z-d31c0bebde834ab9b6b874ac44aa5490.metadata.json`
    - `TrackerSnapshotLog.Format=jsonl`
    - `TrackerSnapshotLog.IsCreated=true`
    - `TrackerSnapshotLog.RecordCount=14`
    - `TrackerSnapshotLog.SkippedRecordCount=0`
    - `TrackerSnapshotLog.ErrorCount=0`
    - `TrackerSnapshotSources`: `external/thirdparty-e2e` 10 records、`own/ibis` 4 records。own は direct manager update と UDP loopback receive の両方が source として見えている。
  - sidecar:
    - path: `/tmp/tracker053-e2e-captures/ssl-vision-packets-20260512T135558249Z-d31c0bebde834ab9b6b874ac44aa5490/tracker-packet-snapshots.jsonl`
    - `wc -l`: 14 records
    - first external record: `sourceRole=external`、`sourceLabel=thirdparty-e2e`、`sourceUuid=thirdparty-e2e-uuid`、`sourceName=thirdparty-e2e`、`trackedFrameNumber=6100`、`semanticSummary.trackedFrameTimestampNs=61004000000`、`ballCount=1`、`robotCount=2`、`payloadBase64` present。
  - `/diagnostics` UI evidence:
    - selected diagnostics log: `/tmp/tracker053-e2e-captures/ssl-vision-packets-20260512T135558249Z-d31c0bebde834ab9b6b874ac44aa5490/ssl-vision-packets-20260512T135558249Z-d31c0bebde834ab9b6b874ac44aa5490.tracker-diagnostics.log`
    - screenshot: `/tmp/tracker053-e2e-screenshots/diagnostics-comparison-thirdparty-e2e.png`
    - CDP evidence JSON: `/tmp/tracker053-e2e-cdp-evidence.json`
    - source filter options: `All (14)`、`External (10)`、`Own (4)`、`Unknown (0)`、`ibis (4)`、`thirdparty-e2e (10)`
    - `All` panel: `Status=Ready`、`Records=14`、`Skipped=0`、`Errors=0`、`Selected frame=1`、`Selected time=22:55:58.997`
    - `External` panel: `Entry status=Ready`、`Rule=nearest-timestamp`、`Source role=external`、`Source label=thirdparty-e2e`、`Snapshot frame=6100`、`Own timestamp ns=61000000000`、`Nearest timestamp ns=61004000000`、`Delta ns=4000000`、`Balls=1`、`Robots=2`、`Raw payload=Restored`
    - `source-label:thirdparty-e2e` panel でも同じ comparison 表示を確認した。
  - `Tracker.CaptureReplay` 補助 evidence:
    - command: `dotnet Tracker/Tracker.CaptureReplay/bin/Debug/net10.0/Tracker.CaptureReplay.dll --capture <session>/<capture>.jsonl.gz --settings <session>/<capture>.metadata.json --profile sim`
    - output summary: `packets=11 detections=10 geometries=1 committedFrames=2`
    - `trackerSnapshot`: `source=ibis role=own ... rawPayloadRestored=True` と `source=thirdparty-e2e role=external ... rawPayloadRestored=True` を確認。
    - `trackerComparison`: `rule=nearest-timestamp ibisTs=61000000000 source=thirdparty-e2e role=external nearestTs=61004000000 balls=1 robots=2 rawPayloadRestored=True`、および 2 件目の comparison を確認。
  - 停止確認:
    - Microsoft Edge headless は Ctrl+C で停止済み。
    - `Tracker.Server` は Ctrl+C で停止済み。
    - `ps -e -o pid=,comm= | awk '$2=="microsoft-edge" || $2=="Tracker.Server" || $2=="Tracker053E2e" || $2=="Tracker053E2eSender" || $2=="dotnet" {print}'` は出力なし。
  - ready gate 判断:
    - `CaptureOn` UI 操作、real `Tracker.Server` capture path、SSL-Vision UDP receiver path、official tracker UDP receiver path、session folder 生成、metadata / sidecar / diagnostics log / render snapshots 生成、`/diagnostics` Tracker Comparison `Ready`、source filter 操作まで確認できた。
    - 今回の PR ready gate は満たすと判断する。ただし入力は local synthetic UDP stream であり、external hardware stream ではないため、その制約は PR body / ready 判断に明記すること。

## リスク

- 未解決のリスクまたは後続対応:
  - 実機または simulator 由来の external hardware stream ではなく、localhost synthetic UDP stream による evidence。receiver / capture / UI normal path の evidence としては十分だが、ネットワーク interface、multicast routing、実 external tracker の送信形式差分は別環境 risk として残る。
  - tracker endpoint は multicast `224.5.23.2:11010` ではなく localhost unicast `127.0.0.1:12010` へ runtime override した。`UdpTrackerReceiver` は同じ production receiver path を通るが、multicast join 自体の runtime evidence ではない。
  - own source は direct manager update と localhost UDP loopback receive の両方で合計 4 records になった。external source filter の comparison は `thirdparty-e2e` 10 records を使って確認済み。
  - Microsoft Edge headless 起動時に dconf read-only warnings は出たが、DevTools Protocol 操作と screenshot 採取は成功した。
