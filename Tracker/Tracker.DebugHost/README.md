# Tracker.DebugHost

`Tracker.DebugHost` は SSL-Vision[^ssl-vision] の UDP[^udp] パケットを受信し、ブラウザで Raw / Tracked 表示を確認しながら、必要に応じて公式トラッカーパケットを UDP 配信する ASP.NET Core アプリです。

## できること

- `VisionReceiver` 設定に従って SSL-Vision パケットを受信する
- Raw 表示で camera ごとの detection と aggregate view を確認する
- Tracked 表示でトラッカーの統合結果、kick / contact / field 状態、publish 関連カウンタを確認する
- 複数プロファイルを定義して、UI または API から実行中に切り替える
- `Tracker:PublishUdp` が有効なら公式トラッカーパケットを UDP multicast / unicast で送信する
- CaptureOn 中に見えている公式トラッカーパケットを同じ session folder へ保存し、後から ibis 出力と比較する

## 前提

- .NET SDK `10.0`
- SSL-Vision パケットを送ってくる送信元
- CaptureOn 比較ログを取る場合は公式 tracker multicast endpoint に流れているトラッカーパケット
- ブラウザで `Tracker.DebugHost` の HTTP endpoint にアクセスできること

## 起動方法

リポジトリのルートディレクトリから実行します。

```bash
dotnet run --project Tracker/Tracker.DebugHost --launch-profile https
```

既定の launch profile は次の URL を使います。

- `https://localhost:7042`
- `http://localhost:5289`

ブラウザを自動起動したくない場合は `--no-launch-profile` か `ASPNETCORE_URLS` を使ってください。

```bash
ASPNETCORE_URLS=http://0.0.0.0:5289 dotnet run --project Tracker/Tracker.DebugHost --no-launch-profile
```

## 画面の使い方

トップページは `/` です。

### ヘッダー

- `Packets`: 受信した raw パケット数です
- `Errors`: 受信・decode・socket 処理で記録された error 数です
- `Remote`: 直近パケットの送信元 endpoint です
- `Received`: 現在表示中データの受信時刻です

### `Raw` モード

- 初期表示は `Raw` です
- field 上に raw detection を描画します
- `Aggregate` と camera ごとの view を切り替えられます
- 右パネルで frame number、camera id、source、raw JSON を確認できます

### `Tracked` モード

- トラッカーが確定した frame を描画します
- 右パネルで次を確認できます
- 使用中プロファイル名
- publish 成功数 / 失敗数
- data timestamp / processed timestamp
- kick / contact / field 状態
- primary / secondary ball
- yellow / blue robots

トラッカー側で frame がまだ確定していない場合は `No tracked frame` が表示されます。

`Publish OK` / `Publish Fail` は現在実装の内部カウンタです。`PublishUdp=false` のときも tracked frame 処理自体は成功扱いになり、`Publish OK` が増えることがあります。実送信の有無は `Tracker:PublishUdp` と送信先設定を合わせて判断してください。

### 実行中のプロファイル切替

- `Tracked` モードの `Profile Control` から定義済みプロファイルを選べます
- 切替を要求すると使用中のプロファイルが切り替わり、古い tracked frame は一度消去されます
- `VisionReceiver:Profiles` に同名プロファイルがある場合は、receiver の multicast address / port / interface もそのプロファイルに追従します
- 新しいパケットで tracked frame が確定すると、新しいプロファイルの設定で再表示されます

### `Diagnostics` ページ

- `/diagnostics` で `VisionReceiver:PacketCapture:DirectoryPath` 配下の capture sidecar `*.tracker-diagnostics.log`、default `tracker-diagnostics-*.log`、`Tracker:Diagnostics:FilePath` のログを読めます
- 上部の timeline scrubber をドラッグすると、選択 replay tick が連続的に切り替わります
- 左側の timeline でログ行を時系列にスクロールできます
- capture sidecar と同じ basename の `*.render-snapshots.jsonl.gz` がある場合は、選択行の raw / tracked field を描画できます
- 左右 Field の見出し行で Field source を `Vision Input`、ibis tracker、`External`、`Unknown`、source label から選択できます。Field source には曖昧な `All` は含めず、log 変更時は左 `Vision Input` / 右 ibis tracker に戻ります。
- 新規 capture に `tracker-snapshot-alignment.jsonl` がある場合、`External` / `Unknown` / source label の Field source は保存時対応表を優先して選択 replay tick に対応する tracker snapshot を描画します。alignment がない既存 capture では、外部トラッカーの時刻対応は unsupported または明示的な best-effort として表示されます。
- 右側で選択行の raw / tracked の ball、robot、frame 情報を比較できます
- capture sidecar の diagnostics log を選ぶと、`Settings` から capture metadata に保存された configured profile と resolved settings を確認できます
- capture sidecar の diagnostics log と tracker packet snapshot sidecar が揃っている場合は、折り畳み可能な `Tracker Comparison` panel で ibis tracker と 3rd party tracker の差分を確認できます
- `Tracker Comparison` panel の source filter は `All`、`External`、`Own`、`Unknown`、source label 単位で切り替えられます。通常確認では `External` または対象 source label を選び、新規 capture では保存済み alignment に対応する 3rd party tracker snapshot と比較します。alignment がない既存 capture で nearest timestamp を使う場合は best-effort として表示されます。
- `Tracker Comparison` は tracker packet snapshot sidecar と alignment sidecar を log 選択時に lightweight index 化し、unified replay timeline も同時に構築します。timeline scrubber 移動や playback tick では同じ file state の sidecar を再読込しません。100MB を超える sidecar でも scrub / tick ごとの I/O と parse は sidecar サイズに比例しません。
- Playback controls は従来どおり Play、Fast Forward、Stop の icon button 配置です。速度選択側には `等倍速` と可変 `早送り倍率` control を compact に表示します。`4x` / `16x` / `64x` は固定上限ではなく preset shortcut です。`等倍速` は Play として全 tick を逐次描画せず、30fps相当の表示更新で wall-clock 経過時間に対応する latest replay tick へ追従します。fast multiplier 選択中に Play を押した場合は `等倍速` へ戻さず、選択中倍率の Fast Forward として開始します。Fast Forward は tick を間引かず capture-time delta と倍率で進み、64x 超の倍率も normalization や timer floor で 64x 相当に潰さないことを contract にします。
- panel には sidecar status、alignment status、record / skipped / error count、選択 replay timeline index / time、held diagnostics line / render frame、comparison status、matching rule、source role / label、source key / remote endpoint、snapshot frame、own / aligned snapshot timestamp、delta、balls / robots、raw payload の復元状態が表示されます。

## API

プロファイル切替は HTTP API からも要求できます。既定 launch profile では `UseHttpsRedirection()` が有効なため、通常は HTTPS endpoint を使ってください。

```bash
curl -k -X POST https://localhost:7042/api/tracker/profile-switch/fast
```

- path parameter の `{profileName}` は `Tracker:Profiles` に定義した名前です
- 既定 launch profile の通常系では `202 Accepted` が返ります
- 存在しないプロファイル名を指定すると `Tracker:Profiles` 解決に失敗し、現状実装では 4xx ではなく server error になります

## 設定ファイル

主な設定は [appsettings.json](./appsettings.json) にあります。

### `VisionReceiver`

raw SSL-Vision パケットの受信設定です。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | 受信対象の multicast group address です。値が multicast 範囲ならその group へ join します。通常は SSL-Vision 側の multicast address を指定します。 |
| `Port` | SSL-Vision パケットの受信 port です。 |
| `InterfaceAddress` | multicast join に使う local IPv4 address です。`null` の場合は利用可能な IPv4 interface を自動探索します。複数 NIC がある環境や join 失敗時は明示指定すると安定します。 |
| `PacketCapture` | 受信した UDP パケットを後で replay できるように圧縮保存する設定です。 |
| `Profiles` | プロファイルごとの receiver override です。`Tracker` 側の使用中プロファイル名と同名エントリがあれば、起動時と実行中のプロファイル切替完了後にその receiver 設定へ追従します。 |

### `VisionReceiver:PacketCapture`

SSL-Vision から着信した UDP datagram を、protobuf decode 前の bytes として `jsonl.gz` に保存します。各行には `receivedAt`、remote endpoint、payload の base64 が入るため、後から同じ順序で `SSL_WrapperPacket` に戻して tracker へ再投入できます。decode に失敗したパケットも保存対象です。

capture を開始すると、`<prefix>-<timestamp>-<guid>` という CaptureOn session folder を作り、その中に同じ basename で次の sidecar も作成します。

- `<prefix>-<timestamp>-<guid>.jsonl.gz`: packet capture 本体
- `<prefix>-<timestamp>-<guid>.metadata.json`: capture 時の `Tracker` 設定と解決済みプロファイル設定
- `<prefix>-<timestamp>-<guid>.tracker-diagnostics.log`: capture と対応する tracker diagnostics log。`Tracker:Diagnostics:FilePath` が指定されていても、capture 有効時は sidecar として同時に出力します。
- `<prefix>-<timestamp>-<guid>.render-snapshots.jsonl.gz`: timeline / 逆方向スクラブ用の描画 snapshot。トラッカーエンジンの内部状態ではなく、確定済み `TrackerFrame` だけを保存します。
- `tracker-packet-snapshots.jsonl`: CaptureOn 中に `Tracker:Receive:Enabled=true` の live receiver が見た公式トラッカーパケットの snapshot sidecar です。
- `tracker-snapshot-alignment.jsonl`: CaptureOn 中の diagnostics entry / render snapshot / tracker source snapshot を session timeline で対応付ける replay 用 sidecar です。外部 トラッカーの `TrackedFrame.timestamp` が ibis own と別時刻系でも、`receivedAt` と session-relative time で `/diagnostics` の Field source と comparison を再生するために使います。schema version 2 では diagnostics line 単位ではなく fastest source cadence の replay timeline records を保存し、同じ Vision / render frame を複数 fast tracker records から参照できます。v1 互換は持たず、log 選択時の index 構築と playback/scrub 時の高速 lookup を優先します。
- `diagnostics-samples.jsonl`: CaptureOn 中に `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` の周期で latest raw / tracker snapshot を同じ sample record として固定する diagnostics sample sidecar です。

`metadata.json` には使用中プロファイル名だけではなく、`Tracker:Profiles` 配下のプロファイル設定値と、起動時上書き適用後の resolved settings も保存します。CaptureOn 比較ログがある場合は、`SessionFolder`、`PacketPath`、`DiagnosticsLogPath`、`RenderSnapshotPath`、`TrackerSnapshotSidecarPath`、`TrackerSnapshotAlignmentPath`、`TrackerSnapshotLog`、`TrackerSnapshotAlignmentLog`、`TrackerSnapshotSources` もここから辿ります。

| キー | 意味 |
| --- | --- |
| `Enabled` | 起動時の packet capture 初期値です。起動後は画面の `Capture On/Off` ボタンで切り替えできます。 |
| `DirectoryPath` | capture file の出力 directory です。相対 path は実行ファイル directory から解決します。 |
| `FilePrefix` | capture file 名の prefix です。実際の file 名は `<prefix>-<timestamp>-<guid>.jsonl.gz` になります。 |
| `FlushEachPacket` | `true` ならパケットごとに flush します。異常終了時の欠落は減りますが、I/O cost は上がります。 |
| `DiagnosticsSampleIntervalMilliseconds` | CaptureOn 中に `diagnostics-samples.jsonl` へ latest raw / tracker snapshot を固定保存する周期です。0 以下の場合は既定値 `100` ms を使います。 |

現在の `appsettings.json` では、起動時 capture は無効ですが、画面で `Capture On` にした後はパケットごとに flush します。

```json
"PacketCapture": {
  "Enabled": false,
  "DirectoryPath": "packet-captures",
  "FilePrefix": "ssl-vision-packets",
  "FlushEachPacket": true,
  "DiagnosticsSampleIntervalMilliseconds": 100
}
```

問題再現用に保存したい場合は `Enabled=true` にします。

```json
"PacketCapture": {
  "Enabled": true,
  "DirectoryPath": "packet-captures",
  "FilePrefix": "ssl-vision-packets",
  "FlushEachPacket": true,
  "DiagnosticsSampleIntervalMilliseconds": 100
}
```

保存された capture は `Tracker.CaptureReplay` tool で replay / analyze できます。通常のユーザー確認は `/diagnostics` の `Tracker Comparison` panel を主経路にし、この CLI は agent / 自動検証 / regression 調査で同じ session を再現するために残します。詳細は [Tracker.CaptureReplay README](../Tracker.CaptureReplay/README.md) を参照してください。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-<timestamp>-<guid>.jsonl.gz \
  --settings Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-<timestamp>-<guid>.metadata.json \
  --profile sim
```

CaptureOn 比較ログを CLI で検証する場合は、`--capture` に session folder または session folder 内の `*.jsonl.gz` を渡します。session folder を渡した場合、CLI は同じ folder の `*.metadata.json` から packet capture と resolved tracker settings を解決します。この場合、replay は capture 時点の resolved tracker settings を使い、metadata の relative path から `tracker-packet-snapshots.jsonl` と `tracker-snapshot-alignment.jsonl` も解決します。出力に `trackerSnapshot ... rawPayloadRestored=True` と `trackerComparison ... rule=saved-session-alignment ...` が出れば、保存時対応表に基づく比較まで読めています。alignment がない既存 capture では `legacy-nearest-timestamp` または unsupported status を確認してください。

raw vision に対して ibis tracker が遅れて見える場合は、capture file を手作業で読む代わりに latency analysis を使います。`--analyze-latency` は raw detection の受信 cadence と、replay 後に ibis tracker frame が確定するまでの capture-time lag を出します。metadata 由来 snapshot 行が多い session では `--skip-tracker-snapshots` と `--max-latency-frames` で出力量を絞れます。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture <session-folder> \
  --analyze-latency \
  --skip-tracker-snapshots \
  --max-latency-frames 8
```

ER-Force 外部 トラッカーを手動検証に使う場合は、`Tracker/Design/Core/Ref/ibis` 配下の Docker 開発環境を使えます。CI や通常 unit test は Docker に依存させず、手元の再現確認だけで使ってください。

```bash
cd Tracker/Design/Core/Ref/ibis
./scripts/docker-dev.sh --sim erforce -d
```

ER-Force プロファイルは同 repository の `docker/dev/README.md` に従います。Tracker.DebugHost 側は `Tracker:Receive:Enabled=true` とし、外部トラッカーが送る multicast endpoint と `Tracker:Receive:MulticastAddress` / `Port` / `InterfaceAddress` が合っていることを確認してから CaptureOn します。検証後の停止は同じ directory で `./scripts/docker-dev.sh down` を使います。

自動テストや regression check では exit code を使えます。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture <capture.jsonl.gz> \
  --profile sim \
  --expect 'committed-frames>0' \
  --expect 'max-balls<=1'
```

- `--expect <condition>`: summary metric の期待条件です。失敗すると exit code `1` になります。
- `--detail-filter <condition>`: 条件に一致する committed frame の詳細を出力します。複数指定した場合は AND 条件です。
- `--max-details <count>`: 詳細出力数を制限します。
- `--settings <file>`: `Tracker.DebugHost/appsettings.json` 形式、または capture の `metadata.json` 形式からトラッカー設定を読み込みます。`Tracker.DebugHost/appsettings.json` 形式では `Tracker:RuntimeOverrides` もプロファイル設定へ反映します。

`--settings` は tracker settings の解決にも使われます。CaptureOn metadata を渡す normal path では、capture 時に保存済みの resolved settings と sidecar relative path を使うため、当時の profile / override / snapshot sidecar をまとめて再現できます。手元の `Tracker.DebugHost/appsettings.json` を渡すのは、capture metadata がない古い capture を現在設定で再評価したい場合や、意図的に別設定で replay したい場合に限ります。その場合、metadata から辿る `tracker-packet-snapshots.jsonl` は自動解決されないため、CaptureOn 比較ログの確認手順としては metadata を優先してください。手書き metadata を作る場合は、packet capture と同じ session folder を基準に `PacketPath`、`MetadataPath`、`DiagnosticsLogPath`、`RenderSnapshotPath`、`TrackerSnapshotSidecarPath`、`TrackerSnapshotLog` を矛盾なく入れてください。

利用できる summary metric は `packets`, `detections`, `geometries`, `committed-frames`, `max-balls`, `max-robots`, `max-raw-balls`, `max-raw-yellow`, `max-raw-blue` です。frame 詳細の filter では `balls`, `robots`, `raw-balls`, `raw-yellow`, `raw-blue` を使えます。raw 系 metric は、その committed frame の source detection 群から集計します。

例えば、raw では ball が 1 個なのに replay 後 frame で ball が 2 個以上になる箇所を確認する場合は次のようにします。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture <capture.jsonl.gz> \
  --profile sim \
  --expect 'max-balls>=2' \
  --detail-filter 'raw-balls==1' \
  --detail-filter 'balls>=2'
```

test code から直接扱う場合は `VisionPacketCaptureFile.ReadRecords(path)` で読み戻し、各 record の `ParsePacket()` を `TrackerCoordinator.ProcessPacket(packet, record.ReceivedAt)` または `TrackerEngine.Update(...)` へ順番に渡すことで replay できます。

### CaptureOn 比較ログの manual evidence

CaptureOn 比較ログを手動で確認する場合は、次の順に証跡を残します。

1. `Tracker:Receive:Enabled=true` にし、receiver が監視する endpoint を確認します。`Tracker:Receive:MulticastAddress` / `Port` が未指定なら起動時に解決した ibis publish endpoint を使います。既定の `sim` profile は `224.5.23.2:11010`、`default` profile は `224.5.23.2:10010` です。3rd party トラッカーが別 endpoint に送信している場合は `Tracker:Receive:MulticastAddress` / `Port` を明示します。複数 NIC 環境では `Tracker:Receive:InterfaceAddress` を明示します。
2. `Tracker.DebugHost` を起動し、画面で `Capture On` にしてから SSL-Vision パケットと公式トラッカーパケットを流します。`Tracker:Receive:Enabled=false` のままでは live tracker receiver が起動しないため、packet capture と diagnostics は残っても tracker packet snapshot sidecar は増えません。
3. `Capture Off` 後、`VisionReceiver:PacketCapture:DirectoryPath` 配下の session folder に `*.jsonl.gz`、`*.metadata.json`、`*.tracker-diagnostics.log`、`*.render-snapshots.jsonl.gz`、`tracker-packet-snapshots.jsonl`、`tracker-snapshot-alignment.jsonl` があることを確認します。metadata では `SessionFolder` と各 relative path、`TrackerSnapshotLog.RecordCount` / `SkippedRecordCount` / `ErrorCount`、`TrackerSnapshotAlignmentLog.RecordCount`、`TrackerSnapshotSources` の `SourceRole` / `SourceLabel` / `RemoteEndpoint` を確認します。
4. `/diagnostics` を開き、同じ session folder の `*.tracker-diagnostics.log` を選びます。unified replay timeline、scrubber、Play / Fast Forward / Stop の playback buttons、速度選択 tabs (`等倍速`、`4x`、`16x`、`64x`)、左右 Field source selector、`Settings` modal の resolved settings を確認します。ER-FORCE など tracker source が Vision より速い capture では、timeline が fast tracker ticks を含み、Vision / render Field が latest-before frame を保持することも確認します。等倍速 Play は30fps相当の表示更新で wall-clock 経過時間に追従するため、高頻度 tick の中間表示をスキップする場合があります。
5. 左右 Field で `External`、`Unknown`、対象 source label を選び、選択 replay tick の保存済み alignment に対応する tracker snapshot が Field に描画されることを確認します。Field view は既定の `Split` のほか `Overlay` を選べます。`Overlay` では左 Field source selector が `Layer A`、右 Field source selector が `Layer B` として同一 Field に重なり、legend の layer checkbox で表示/非表示を切り替えられます。`Tracker Comparison` panel は必要に応じて折り畳めます。
6. `Tracker Comparison` panel で `Status` が `Ready` になることを確認し、source filter を `External` または対象 source label に切り替えます。比較対象がないことを確認したい場合は `Own` / `Unknown` も使えます。
7. report には、selected replay timeline index / selected time、Play の表示更新で到達した selected time、held diagnostics line / held render frame、Field view mode、Layer A / Layer B の Field source と可視度、source filter、sidecar status、alignment status、record / skipped / error count、entry status、source role / label、source key / remote endpoint、aggregate tie-break、snapshot frame、own timestamp ns、aligned snapshot timestamp ns、delta ns、balls / robots、raw payload 表示を残します。UI の raw payload は `Restored` が rawPayloadRestored true、`Missing` が false です。Play で表示されなかった中間 tick も、scrubber や comparison から選択できることを必要に応じて確認します。
8. 必要に応じて `Tracker.CaptureReplay` を `--capture <session>/<capture>.jsonl.gz --settings <session>/<capture>.metadata.json --profile <capture時のprofile>` で実行し、`trackerSnapshot` と `trackerComparison` 行、`rawPayloadRestored=True`、`saved-session-alignment` の比較 summary を agent / 検証 / 回帰用 evidence として残します。CLI evidence は UI evidence の補助であり、通常確認の主経路ではありません。

`Tracker Comparison` panel の sidecar status は次のように読みます。

- `Ready`: metadata と `tracker-packet-snapshots.jsonl` を読み、選択 diagnostics entry の comparison を作成できる状態です。
- `NoLogSelected`: diagnostics log がまだ選択されていません。
- `MetadataMissing` / `MetadataCorrupt`: 選択 log に対応する `*.metadata.json` がない、または JSON を読み取れません。古い diagnostics log や壊れた metadata の可能性があります。
- `SnapshotMetadataMissing`: metadata に tracker snapshot log 情報がありません。CaptureOn 比較ログ導入前の capture ではこの状態になり得ます。
- `SidecarNotCreated`: metadata は snapshot sidecar 未作成を示しています。`Tracker:Receive:Enabled=false`、receiver 未起動、または CaptureOn 中に writer が開始されなかった場合を疑います。
- `SidecarPathMissing` / `SidecarMissing`: metadata に sidecar path がない、または metadata が指す file が存在しません。session folder の移動や部分コピーを疑います。
- `SidecarEmpty` または `RecordCount=0`: sidecar は作成されていますが、保存済みトラッカーパケットがありません。公式トラッカーパケットが endpoint に流れていない、multicast interface が違う、source がまだ見えていない場合を確認します。
- `SidecarCorrupt`: sidecar JSONL を読み取れません。壊れた file、途中書き込み、手動編集を疑います。
- `Skipped` が 0 より大きい場合は decode または書き込み失敗で snapshot record にできなかったパケットがあることを示します。`Errors` が 0 より大きい場合は writer 側で記録された error があるため、比較結果の代表性を report のリスクに残します。

### `VisionReceiver:Profiles:<name>`

receiver profile override です。未指定項目は top-level `VisionReceiver` の値を引き継ぎます。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | profile 切替後に join する multicast group address です。 |
| `Port` | profile 切替後に bind / receive する UDP port です。 |
| `InterfaceAddress` | profile 切替後に multicast join へ使う local IPv4 address です。 |

### `Tracker`

`Tracker` と `Tracker:Profiles:<name>` の共有設定は [Tracker appsettings README](../README.appsettings.md) を参照してください。この README では DebugHost 固有の `Tracker:Receive`、`Tracker:Diagnostics`、`Tracker:RuntimeOverrides` だけを説明します。

### `Tracker:Receive`

CaptureOn 比較ログ用の live tracker packet receiver 設定です。`Enabled=true` のときだけ `TrackerConnectionLib` receiver が起動します。`MulticastAddress` / `Port` が未指定なら、起動時の使用中プロファイルと `Tracker:RuntimeOverrides:Publish` から解決した ibis publish endpoint を監視します。`MulticastAddress` / `Port` を明示した場合は、receiver 独自 endpoint を監視します。endpoint 解決は起動時固定で、実行中のプロファイル切替後に receiver socket は再構成されません。受信した `TrackerWrapperPacket` は CaptureOn 中だけ `tracker-packet-snapshots.jsonl` へ保存され、Capture Off 中は追記しません。

`Enabled=false` のままでは live receiver が起動しないため、external tracker packet は記録されません。sidecar が空の場合は、監視 endpoint、`InterfaceAddress`、OS の multicast route、3rd party tracker の送信先が一致しているか確認してください。

| キー | 意味 |
| --- | --- |
| `Enabled` | `true` なら公式トラッカーパケット receiver を起動します。既定は `false` です。 |
| `MulticastAddress` | receiver が監視する multicast group address です。`null` の場合は起動時に解決済みの ibis publish address を使います。 |
| `Port` | receiver が監視する UDP port です。`null` の場合は起動時に解決済みの ibis publish port を使います。 |
| `InterfaceAddress` | multicast join に使う local IPv4 address です。`null` の場合は receiver 実装の既定に任せます。複数 NIC がある環境では明示指定してください。 |

```json
"Receive": {
  "Enabled": true,
  "MulticastAddress": null,
  "Port": null,
  "InterfaceAddress": "192.0.2.10"
}
```

### `Tracker:Diagnostics`

トラッカーの調査用診断ログ設定です。diagnostics log は常に出力され、`FilePath` が `null` の場合は `VisionReceiver:PacketCapture:DirectoryPath` 配下に起動ごとの `tracker-diagnostics-<timestamp>-<guid>.log` を作成します。packet capture 有効時は、capture sidecar の `*.tracker-diagnostics.log` にも同時に出力します。

| キー | 意味 |
| --- | --- |
| `FilePath` | 明示的なファイル出力先です。`null` の場合は `VisionReceiver:PacketCapture:DirectoryPath` 配下に出力します。 |

現在の `appsettings.json` は、`packet-captures` 配下へ起動ごとの新規ファイルを出力する設定です。

```json
"Diagnostics": {
  "FilePath": null
}
```

console に出る tracker diagnostics の structured log は `Logging:LogLevel` で抑制します。ファイル出力はこの設定とは別に継続します。

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "Tracker.DebugHost.Tracking.TrackerCoordinator": "Warning"
  }
}
```

全体の `Information` log も止めたい場合は `Default` を `Warning` にします。

### `Tracker:RuntimeOverrides`

プロファイルの基本設定に対する「起動時上書き」です。指定しない項目はプロファイル側の値をそのまま使います。

| キー | 意味 |
| --- | --- |
| `Publish.MulticastAddress` | トラッカーパケットの送信先 address を一時的に上書きします。 |
| `Publish.Port` | トラッカーパケットの送信先 port を一時的に上書きします。 |
| `Publish.SourceName` | トラッカーパケットの source name を一時的に上書きします。 |
| `Publish.Uuid` | トラッカーパケットの UUID を一時的に上書きします。 |
| `RobotTracker.*` | robot tracking の調整値を使用中プロファイルに対して上書きします。 |
| `BallTracker.*` | ball tracking の調整値を使用中プロファイルに対して上書きします。 |
| `KickDetector.*` | kick 判定の調整値を使用中プロファイルに対して上書きします。 |

現状の UI / HTTP API では個別の上書き値を入力できません。`appsettings.json` に書いた起動時設定として使います。

## 典型的な変更例

### SSL-Vision の受信 NIC を固定する

```json
{
  "VisionReceiver": {
    "MulticastAddress": "224.5.23.2",
    "Port": 10020,
    "InterfaceAddress": "192.168.10.5"
  }
}
```

### tracker profile に対応する receiver profile を分ける

```json
{
  "VisionReceiver": {
    "MulticastAddress": "224.5.23.2",
    "Port": 10020,
    "InterfaceAddress": null,
    "Profiles": {
      "sim": {
        "MulticastAddress": "224.5.23.2",
        "Port": 12020,
        "InterfaceAddress": "10.0.0.5"
      }
    }
  },
  "Tracker": {
    "ActiveProfileName": "sim"
  }
}
```

### tracked packet の送信を止めて viewer だけ使う

```json
{
  "Tracker": {
    "Enabled": true,
    "PublishUdp": false
  }
}
```

### 起動時プロファイルを `fast` にする

```json
{
  "Tracker": {
    "ActiveProfileName": "fast"
  }
}
```

## 注意点

- `Tracker:ActiveProfileName` やプロファイル切替先は、必ず `Tracker:Profiles` に定義した名前にしてください
- multicast 受信に失敗する場合は `VisionReceiver:InterfaceAddress` の明示指定を優先してください
- プロファイル切替 API は未知プロファイル名に対して 4xx を返さず server error になるため、呼び出し側で事前にプロファイル一覧を一致させてください
- 現在の環境では solution build に `-m:1 -p:BuildInParallel=false` が必要なことがありますが、`Tracker.DebugHost` の実行方法自体は上記のとおりです

## 脚注

[^ssl-vision]: SSL-Vision は RoboCup Small Size League で使われる vision system です。
[^udp]: UDP は User Datagram Protocol の略です。
