# Sub-agent実行レポート

## タスク

- 目的: `TRACKER-053` PR ready gate のため、`/diagnostics` Tracker Comparison panel の実ブラウザ manual evidence を採取できるか確認し、可能なら採取する。
- タスク種別: environment verification / manual evidence

## sub-agentを使う理由

- 理由: ユーザー指定により、実装・調査・レビューは gpt-5.5 high sub-agent を使う。manual evidence は PR ready 判断に関わる環境検証であり、独立 evidence として report に残す必要がある。

## 対象範囲

- 対象:
  - `/diagnostics` UI の Tracker Comparison panel
  - CaptureOn session folder / diagnostics log / tracker packet snapshot sidecar の evidence
  - local dev server / browser automation / available sample data

## 対象外

- 対象外:
  - production / test / docs / tracking の変更
  - PR body の実更新
  - draft解除操作
  - commit / push / PR 操作

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,240p' reports/tracker-053-manual-evidence-20260512222737.md`
  - `sed -n '1,260p' reports/tracker-053-pr-ready-prep-20260512221920.md`
  - `rg -n "CaptureOn|comparison|Tracker Comparison|manual evidence|diagnostics" Tracker/Tracker.Server/README.md Tracker/Tracker.Core/Design/tasks-status.md`
  - `git status --short && git rev-parse --abbrev-ref HEAD && git rev-parse --short HEAD`
  - `sed -n '208,232p' Tracker/Tracker.Server/README.md`
  - `rg --files | rg '(tracker-diagnostics|metadata\\.json|tracker-packet-snapshots|jsonl\\.gz|render-snapshots|diagnostics.*\\.log|sample|fixture|CaptureOn)'`
  - `rg -n "TrackerDiagnosticsComparison|tracker-packet-snapshots|TrackerSnapshotSidecarPath|DiagnosticsLogPath|RecordCount|SourceRole|SourceLabel" Tracker/Tracker.Tests Tracker/Tracker.Server Tracker/Tracker.CaptureReplay`
  - `dotnet new console -n Tracker053FixtureGenerator -o /tmp/tracker053-fixture-generator-2 --framework net10.0`
  - `dotnet add /tmp/tracker053-fixture-generator-2/Tracker053FixtureGenerator.csproj reference /home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Tracker.Server.csproj /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Tracker.Core.csproj`
  - `dotnet add /tmp/tracker053-fixture-generator-2/Tracker053FixtureGenerator.csproj reference /home/ibis/ssl/IbisDuck/SslProto/SslProto.csproj`
  - `DOTNET_CLI_HOME=/tmp/tracker053-dotnet-home NUGET_PACKAGES=/tmp/tracker053-nuget-packages NUGET_HTTP_CACHE_PATH=/tmp/tracker053-nuget-http-cache dotnet run --project /tmp/tracker053-fixture-generator-2/Tracker053FixtureGenerator.csproj --no-restore`
  - `ASPNETCORE_URLS=http://127.0.0.1:5078 VisionReceiver__PacketCapture__DirectoryPath=/tmp/tracker053-evidence-captures DOTNET_CLI_HOME=/tmp/tracker053-dotnet-home NUGET_PACKAGES=/tmp/tracker053-nuget-packages NUGET_HTTP_CACHE_PATH=/tmp/tracker053-nuget-http-cache dotnet run --project Tracker/Tracker.Server/Tracker.Server.csproj --no-restore`
  - `curl -sS -D - http://localhost:5289/diagnostics -o /tmp/tracker053-diagnostics.html | head -20`
  - `python3 - <<'PY' ... import playwright ... PY`
  - `node -e "try{require('playwright'); ...}catch(e){...}"`
  - `which chromium || which chromium-browser || which google-chrome || which firefox || true`
  - `firefox --headless --screenshot /tmp/tracker053-diagnostics-initial.png --window-size=1600,1200 http://localhost:5289/diagnostics`
  - `HOME=/tmp/tracker053-firefox-home XDG_RUNTIME_DIR=/tmp/tracker053-xdg-runtime firefox --headless --screenshot /tmp/tracker053-evidence-screenshots/diagnostics-tracker-comparison.png --window-size=1600,1200 http://localhost:5289/diagnostics`
  - `HOME=/tmp/tracker053-edge-home XDG_CONFIG_HOME=/tmp/tracker053-edge-config XDG_CACHE_HOME=/tmp/tracker053-edge-cache microsoft-edge --headless --disable-gpu --no-sandbox --disable-crash-reporter --disable-dev-shm-usage --user-data-dir=/tmp/tracker053-edge-profile-2 --window-size=1600,1200 --screenshot=/tmp/tracker053-evidence-screenshots/diagnostics-tracker-comparison.png http://localhost:5289/diagnostics`
  - `HOME=/tmp/tracker053-edge-home XDG_CONFIG_HOME=/tmp/tracker053-edge-config XDG_CACHE_HOME=/tmp/tracker053-edge-cache microsoft-edge --headless --disable-gpu --no-sandbox --disable-crash-reporter --disable-dev-shm-usage --user-data-dir=/tmp/tracker053-edge-cdp-profile --remote-debugging-port=9222 --remote-allow-origins='*' http://localhost:5289/diagnostics`
  - `node --input-type=module - <<'JS' ... DevTools Protocol で source filter option / thirdparty-b 選択 / screenshot 採取 ... JS`
  - `node --input-type=module - <<'JS' ... DevTools Protocol で All / External / Own / Unknown / source label filter matrix 採取 ... JS`
  - `file /tmp/tracker053-evidence-screenshots/diagnostics-tracker-comparison.png && ls -l /tmp/tracker053-evidence-screenshots/diagnostics-tracker-comparison.png`
  - `python3 - <<'PY' ... /tmp/tracker053-diagnostics.html から Tracker Comparison panel text 抽出 ... PY`
  - `sed -n '1,220p' /tmp/tracker053-evidence-captures/tracker-053-synthetic-session/tracker-053-synthetic-session.metadata.json`
  - `wc -l /tmp/tracker053-evidence-captures/tracker-053-synthetic-session/tracker-packet-snapshots.jsonl && head -1 /tmp/tracker053-evidence-captures/tracker-053-synthetic-session/tracker-packet-snapshots.jsonl`
  - `pgrep -af 'microsoft-edge|Tracker.Server|dotnet run' || true`
  - `ps -e -o comm=,args= | awk '$1=="microsoft-edge" || ($1=="dotnet" && $0 ~ /Tracker.Server|dotnet run/) {print}'`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/tracker-053-manual-evidence-20260512222737.md`
  - 確認: `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - 確認: `reports/tracker-053-pr-ready-prep-20260512221920.md`
  - 確認: `Tracker/Tracker.Server/README.md`
  - 確認: `Tracker/Tracker.Core/Design/tasks-status.md`
  - 確認: `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogReader.cs`
  - 作成/確認: `/tmp/tracker053-evidence-captures/tracker-053-synthetic-session/tracker-053-synthetic-session.metadata.json`
  - 作成/確認: `/tmp/tracker053-evidence-captures/tracker-053-synthetic-session/tracker-053-synthetic-session.tracker-diagnostics.log`
  - 作成/確認: `/tmp/tracker053-evidence-captures/tracker-053-synthetic-session/tracker-packet-snapshots.jsonl`
  - 作成/確認: `/tmp/tracker053-evidence-screenshots/diagnostics-tracker-comparison.png`
  - 作成/確認: `/tmp/tracker053-evidence-screenshots/diagnostics-tracker-comparison-thirdparty-b.png`
  - 作成/確認: `/tmp/tracker053-cdp-filter-evidence.json`
  - 作成/確認: `/tmp/tracker053-cdp-filter-matrix.json`

## 指摘事項

- 指摘要約または「指摘なし」:
  - 指摘なし。
  - production / test / docs / tracking の repo 内変更は実施していない。
  - 実 external SSL-Vision stream / official tracker packet source はこの環境では確認できなかったため、実機由来 CaptureOn evidence は未採取。
  - Playwright は Python / Node とも未導入。Firefox は snap の `/home/ibis/snap/firefox/current` と `/run/user/1000/snap.firefox` が read-only で headless 起動不可。Microsoft Edge headless は `/tmp` の HOME / XDG_CONFIG_HOME / XDG_CACHE_HOME / user-data-dir 指定で起動でき、screenshot と DevTools Protocol 操作に成功。

## 結果

- 結果:
  - evidence 採取可否:
    - 実 external stream ではないが、synthetic CaptureOn session fixture を `/tmp/tracker053-evidence-captures/tracker-053-synthetic-session` に作成し、実 `Tracker.Server` と headless Microsoft Edge で `/diagnostics` UI evidence を採取できた。
    - `Tracker.Server` 起動 URL は launch settings により `http://localhost:5289`。起動 command は `VisionReceiver__PacketCapture__DirectoryPath=/tmp/tracker053-evidence-captures ... dotnet run --project Tracker/Tracker.Server/Tracker.Server.csproj --no-restore`。
    - diagnostics log 選択: `/tmp/tracker053-evidence-captures/tracker-053-synthetic-session/tracker-053-synthetic-session.tracker-diagnostics.log`。
    - screenshot: `/tmp/tracker053-evidence-screenshots/diagnostics-tracker-comparison.png`。1600x1200 PNG、`Tracker Comparison` panel、`Status Ready`、selected log、selected frame を確認できる。
  - synthetic session:
    - metadata: `/tmp/tracker053-evidence-captures/tracker-053-synthetic-session/tracker-053-synthetic-session.metadata.json`
    - sidecar: `/tmp/tracker053-evidence-captures/tracker-053-synthetic-session/tracker-packet-snapshots.jsonl`
    - `tracker-packet-snapshots.jsonl`: 4 records。
    - `TrackerSnapshotLog`: `IsCreated=true`、`RecordCount=4`、`SkippedRecordCount=0`、`ErrorCount=0`。
    - `TrackerSnapshotSources`: `own/ibis` 1、`external/thirdparty-a` 1、`external/thirdparty-b` 1、`unknown/unknown` 1。
    - packet raw payload は `TrackerPacketGenerator` と `TrackerPacketSnapshotRecord.FromPacket(...)` で生成した synthetic protobuf payload。UI では `Raw payload Restored`。
  - `/diagnostics` / `Tracker Comparison` panel 表示:
    - HTTP check: `curl http://localhost:5289/diagnostics` は 200 OK。
    - panel text 抽出で次を確認: `All (4)`、`External (2)`、`Own (1)`、`Unknown (1)`、`ibis (1)`、`thirdparty-a (1)`、`thirdparty-b (1)`、`unknown (1)`。
    - default `All` 表示で `Status Ready`、`Records 4`、`Skipped 0`、`Errors 0`、`Selected frame 9100`、`Selected time 21:00:00.000`。
    - entry comparison は `Entry status Ready`、`Rule nearest-timestamp`、`Source role external`、`Source label thirdparty-a`、`Snapshot frame 9101`、`Own timestamp ns 91000000000`、`Nearest timestamp ns 91004000000`、`Delta ns 4000000`、`Balls 2`、`Robots 2`、`Raw payload Restored`。
  - source filter 操作:
    - DevTools Protocol で UI select を操作し、次の filter matrix を確認した。
    - `All (4)`: `Status Ready`、`Entry status Ready`、`Source role external`、`Source label thirdparty-a`、`Snapshot frame 9101`、`Delta ns 4000000`、`Balls 2`、`Robots 2`、`Raw payload Restored`。
    - `External (2)`: `Status Ready`、`Entry status Ready`、`Source role external`、`Source label thirdparty-a`、`Snapshot frame 9101`、`Delta ns 4000000`、`Balls 2`、`Robots 2`、`Raw payload Restored`。
    - `Own (1)`: `Status Ready`、`Entry status Ready`、`Source role own`、`Source label ibis`、`Snapshot frame 9100`、`Delta ns 0`、`Balls 1`、`Robots 1`、`Raw payload Restored`。
    - `Unknown (1)`: `Status Ready`、`Entry status Ready`、`Source role unknown`、`Source label unknown`、`Snapshot frame 9103`、`Delta ns 10000000`、`Balls 3`、`Robots 0`、`Raw payload Restored`。
    - `source-label:thirdparty-b`: `Status Ready`、`Entry status Ready`、`Source role external`、`Source label thirdparty-b`、`Snapshot frame 9102`、`Delta ns 20000000`、`Balls 1`、`Robots 3`、`Raw payload Restored`。
    - source label 選択後 screenshot: `/tmp/tracker053-evidence-screenshots/diagnostics-tracker-comparison-thirdparty-b.png`。
  - ready gate 判断:
    - UI component の real browser evidence としては、`/diagnostics` 起動、diagnostics log 選択、`Tracker Comparison` panel 表示、filter 操作、panel 項目表示、`Status Ready` を確認できた。
    - ただし session は synthetic fixture であり、実 external SSL-Vision / official tracker packet stream 由来ではない。よって「実ブラウザ UI gate」は満たすが、「実 external stream を使った end-to-end CaptureOn gate」はこの環境では未充足。
    - PR ready gate を厳密に「実 stream 由来の CaptureOn session」まで要求するなら draft 継続。synthetic fixture + real browser UI evidence を manual evidence として許容するなら ready 化判断材料として使用可能。
  - 長時間プロセス:
    - `Tracker.Server` は Ctrl+C で停止済み。
    - Microsoft Edge headless / CDP session は終了済み。`ps -e -o comm=,args= | awk '$1=="microsoft-edge" || ($1=="dotnet" && $0 ~ /Tracker.Server|dotnet run/) {print}'` は出力なし。

## リスク

- 未解決のリスクまたは後続対応:
  - この evidence は synthetic CaptureOn session であり、real SSL-Vision multicast と official tracker multicast を同時に流した session ではない。実機 / simulator の external stream normal path は別環境で追加採取する必要がある。
  - synthetic fixture は comparison reader / UI contract に沿う session folder、metadata、`tracker-packet-snapshots.jsonl` を `/tmp` に生成したもの。packet raw payload は production generator 由来だが、CaptureOn ボタン操作で生成されたものではない。
  - screenshot では render snapshot file が空のため `Render snapshot for tracked frame '9100' was not found.` が表示される。今回の対象は Tracker Comparison panel であり、raw/tracked render snapshot evidence は範囲外。
  - Firefox headless と Playwright はこの環境では利用不可。Microsoft Edge headless + DevTools Protocol で代替した。
