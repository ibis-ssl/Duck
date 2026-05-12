# Tracker.Server

`Tracker.Server` は SSL-Vision の UDP packet を受信し、ブラウザで raw / tracked viewer を表示しながら、必要に応じて official tracker packet を UDP 配信する ASP.NET Core アプリです。

## できること

- `VisionReceiver` 設定に従って SSL-Vision packet を受信する
- raw viewer で camera ごとの detection と aggregate view を確認する
- tracked viewer で tracker の統合結果、kick / contact / field 状態、publish 関連カウンタを確認する
- 複数 profile を定義して、UI または API から runtime profile switch を行う
- `Tracker:PublishUdp` が有効なら official tracker packet を UDP multicast/unicast で送信する
- CaptureOn 中に見えている official tracker packet を同じ session folder へ保存し、後から ibis 出力と比較する

## 前提

- .NET SDK `10.0`
- SSL-Vision packet を送ってくる送信元
- CaptureOn 比較ログを取る場合は official tracker multicast endpoint に流れている tracker packet
- ブラウザで `Tracker.Server` の HTTP endpoint にアクセスできること

## 起動方法

repository root から実行します。

```bash
dotnet run --project Tracker/Tracker.Server --launch-profile https
```

既定の launch profile は次の URL を使います。

- `https://localhost:7042`
- `http://localhost:5289`

ブラウザを自動起動したくない場合は `--no-launch-profile` か `ASPNETCORE_URLS` を使ってください。

```bash
ASPNETCORE_URLS=http://0.0.0.0:5289 dotnet run --project Tracker/Tracker.Server --no-launch-profile
```

## 画面の使い方

トップページは `/` です。

### ヘッダー

- `Packets`: 受信した raw packet 数です
- `Errors`: 受信・decode・socket 処理で記録された error 数です
- `Remote`: 直近 packet の送信元 endpoint です
- `Received`: 現在表示中データの受信時刻です

### `Raw` モード

- 初期表示は `Raw` です
- field 上に raw detection を描画します
- `Aggregate` と camera ごとの view を切り替えられます
- 右パネルで frame number、camera id、source、raw JSON を確認できます

### `Tracked` モード

- tracker が commit した frame を描画します
- 右パネルで次を確認できます
- active profile 名
- publish 成功数 / 失敗数
- data timestamp / processed timestamp
- kick / contact / field 状態
- primary / secondary ball
- yellow / blue robots

tracker 側で frame がまだ commit されていない場合は `No tracked frame` が表示されます。

`Publish OK` / `Publish Fail` は現在実装の内部カウンタです。`PublishUdp=false` のときも tracked frame 処理自体は成功扱いになり、`Publish OK` が増えることがあります。実送信の有無は `Tracker:PublishUdp` と送信先設定を合わせて判断してください。

### runtime profile switch

- `Tracked` モードの `Profile Control` から configured profile を選べます
- switch を要求すると active profile が切り替わり、古い tracked frame は一度 clear されます
- `VisionReceiver:Profiles` に同名 profile がある場合は、receiver の multicast address / port / interface もその profile に追従します
- 新しい packet が commit されると、新 profile の context で tracked frame が再表示されます

### `Diagnostics` ページ

- `/diagnostics` で `VisionReceiver:PacketCapture:DirectoryPath` 配下の capture sidecar `*.tracker-diagnostics.log`、default `tracker-diagnostics-*.log`、`Tracker:Diagnostics:FilePath` のログを読めます
- 上部の timeline scrubber をドラッグすると、選択 frame が連続的に切り替わります
- 左側の timeline でログ行を時系列にスクロールできます
- capture sidecar と同じ basename の `*.render-snapshots.jsonl.gz` がある場合は、選択行の raw / tracked field を描画できます
- 右側で選択行の raw / tracked の ball、robot、frame 情報を比較できます
- capture sidecar の diagnostics log を選ぶと、`Settings` から capture metadata に保存された configured profile と resolved settings を確認できます
- capture sidecar の diagnostics log と tracker packet snapshot sidecar が揃っている場合は、`Tracker Comparison` panel で ibis tracker と 3rd party tracker の差分を確認できます
- `Tracker Comparison` panel の source filter は `All`、`External`、`Own`、`Unknown`、source label 単位で切り替えられます。通常確認では `External` または対象 source label を選び、選択中 diagnostics entry の ibis own snapshot timestamp に最も近い 3rd party tracker snapshot と比較します。
- panel には sidecar status、record / skipped / error count、選択 frame / time、comparison status、matching rule、source role / label、snapshot frame、own / nearest timestamp、delta、balls / robots、raw payload の復元状態が表示されます。

## API

profile switch は HTTP API からも要求できます。既定 launch profile では `UseHttpsRedirection()` が有効なため、通常は HTTPS endpoint を使ってください。

```bash
curl -k -X POST https://localhost:7042/api/tracker/profile-switch/fast
```

- path parameter の `{profileName}` は `Tracker:Profiles` に定義した名前です
- 既定 launch profile の通常系では `202 Accepted` が返ります
- 存在しない profile 名を指定すると `Tracker:Profiles` 解決に失敗し、現状実装では 4xx ではなく server error になります

## 設定ファイル

主な設定は [appsettings.json](./appsettings.json) にあります。

### `VisionReceiver`

raw SSL-Vision packet の受信設定です。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | 受信対象の multicast group address です。値が multicast 範囲ならその group へ join します。通常は SSL-Vision 側の multicast address を指定します。 |
| `Port` | SSL-Vision packet の受信 port です。 |
| `InterfaceAddress` | multicast join に使う local IPv4 address です。`null` の場合は利用可能な IPv4 interface を自動探索します。複数 NIC がある環境や join 失敗時は明示指定すると安定します。 |
| `PacketCapture` | 受信した UDP packet を後で replay できるように圧縮保存する設定です。 |
| `Profiles` | profile ごとの receiver override です。`Tracker` 側の active profile 名と同名エントリがあれば、起動時と runtime profile switch 完了後にその receiver 設定へ追従します。 |

### `VisionReceiver:PacketCapture`

SSL-Vision から着信した UDP datagram を、protobuf decode 前の bytes として `jsonl.gz` に保存します。各行には `receivedAt`、remote endpoint、payload の base64 が入るため、後から同じ順序で `SSL_WrapperPacket` に戻して tracker へ再投入できます。decode に失敗した packet も保存対象です。

capture を開始すると、`<prefix>-<timestamp>-<guid>` という CaptureOn session folder を作り、その中に同じ basename で次の sidecar も作成します。

- `<prefix>-<timestamp>-<guid>.jsonl.gz`: packet capture 本体
- `<prefix>-<timestamp>-<guid>.metadata.json`: capture 時の `Tracker` 設定と resolved profile 設定
- `<prefix>-<timestamp>-<guid>.tracker-diagnostics.log`: capture と対応する tracker diagnostics log。`Tracker:Diagnostics:FilePath` が指定されていても、capture 有効時は sidecar として同時に出力します。
- `<prefix>-<timestamp>-<guid>.render-snapshots.jsonl.gz`: timeline / 逆方向スクラブ用の描画 snapshot。tracker engine の内部状態ではなく、commit 済み `TrackerFrame` だけを保存します。
- `tracker-packet-snapshots.jsonl`: CaptureOn 中に `Tracker:Receive:Enabled=true` の live receiver が見た official tracker packet の snapshot sidecar です。

`metadata.json` には active profile 名だけではなく、`Tracker:Profiles` 配下の profile 設定値と、runtime override 適用後の resolved settings も保存します。CaptureOn 比較ログがある場合は、`SessionFolder`、`PacketPath`、`DiagnosticsLogPath`、`RenderSnapshotPath`、`TrackerSnapshotSidecarPath`、`TrackerSnapshotLog`、`TrackerSnapshotSources` もここから辿ります。

| キー | 意味 |
| --- | --- |
| `Enabled` | 起動時の packet capture 初期値です。起動後は画面の `Capture On/Off` ボタンで切り替えできます。 |
| `DirectoryPath` | capture file の出力 directory です。相対 path は実行ファイル directory から解決します。 |
| `FilePrefix` | capture file 名の prefix です。実際の file 名は `<prefix>-<timestamp>-<guid>.jsonl.gz` になります。 |
| `FlushEachPacket` | `true` なら packet ごとに flush します。異常終了時の欠落は減りますが、I/O cost は上がります。 |

現在の `appsettings.json` では、起動時 capture は無効ですが、画面で `Capture On` にした後は packet ごとに flush します。

```json
"PacketCapture": {
  "Enabled": false,
  "DirectoryPath": "packet-captures",
  "FilePrefix": "ssl-vision-packets",
  "FlushEachPacket": true
}
```

問題再現用に保存したい場合は `Enabled=true` にします。

```json
"PacketCapture": {
  "Enabled": true,
  "DirectoryPath": "packet-captures",
  "FilePrefix": "ssl-vision-packets",
  "FlushEachPacket": true
}
```

保存された capture は `Tracker.CaptureReplay` tool で replay / analyze できます。通常のユーザー確認は `/diagnostics` の `Tracker Comparison` panel を主経路にし、この CLI は agent / 自動検証 / regression 調査で同じ session を再現するために残します。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-<timestamp>-<guid>.jsonl.gz \
  --settings Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-<timestamp>-<guid>.metadata.json \
  --profile sim
```

CaptureOn 比較ログを CLI で検証する場合は、`--capture` に session folder 内の `*.jsonl.gz` を渡し、`--settings` には同じ session folder 内の `*.metadata.json` を渡します。この場合、replay は capture 時点の resolved tracker settings を使い、metadata の relative path から `tracker-packet-snapshots.jsonl` も解決します。出力に `trackerSnapshot ... rawPayloadRestored=True` と `trackerComparison ... rule=nearest-timestamp ...` が出れば、tracker packet snapshot sidecar と ibis diagnostics frame の近傍比較まで読めています。

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
- `--settings <file>`: `Tracker.Server/appsettings.json` 形式、または capture の `metadata.json` 形式から tracker 設定を読み込みます。`Tracker.Server/appsettings.json` 形式では `Tracker:RuntimeOverrides` も profile 設定へ反映します。

`--settings` は tracker settings の解決にも使われます。CaptureOn metadata を渡す normal path では、capture 時に保存済みの resolved settings と sidecar relative path を使うため、当時の profile / override / snapshot sidecar をまとめて再現できます。手元の `Tracker.Server/appsettings.json` を渡すのは、capture metadata がない古い capture を現在設定で再評価したい場合や、意図的に別設定で replay したい場合に限ります。その場合、metadata から辿る `tracker-packet-snapshots.jsonl` は自動解決されないため、CaptureOn 比較ログの確認手順としては metadata を優先してください。手書き metadata を作る場合は、packet capture と同じ session folder を基準に `PacketPath`、`MetadataPath`、`DiagnosticsLogPath`、`RenderSnapshotPath`、`TrackerSnapshotSidecarPath`、`TrackerSnapshotLog` を矛盾なく入れてください。

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

1. `Tracker:Receive:Enabled=true` にし、active profile の `Tracker:Profiles:<name>:Publish:MulticastAddress` / `Port` が監視したい official tracker multicast endpoint を指していることを確認します。既定の `sim` profile は `224.5.23.2:11010`、`default` profile は `224.5.23.2:10010` です。複数 NIC 環境では `Tracker:Receive:InterfaceAddress` を明示します。
2. `Tracker.Server` を起動し、画面で `Capture On` にしてから SSL-Vision packet と official tracker packet を流します。`Tracker:Receive:Enabled=false` のままでは live tracker receiver が起動しないため、packet capture と diagnostics は残っても tracker packet snapshot sidecar は増えません。
3. `Capture Off` 後、`VisionReceiver:PacketCapture:DirectoryPath` 配下の session folder に `*.jsonl.gz`、`*.metadata.json`、`*.tracker-diagnostics.log`、`*.render-snapshots.jsonl.gz`、`tracker-packet-snapshots.jsonl` があることを確認します。metadata では `SessionFolder` と各 relative path、`TrackerSnapshotLog.RecordCount` / `SkippedRecordCount` / `ErrorCount`、`TrackerSnapshotSources` の `SourceRole` / `SourceLabel` / `RemoteEndpoint` を確認します。
4. `/diagnostics` を開き、同じ session folder の `*.tracker-diagnostics.log` を選びます。timeline、scrubber、Play / Fast Forward、raw / tracked field、`Settings` modal の resolved settings を確認します。
5. `Tracker Comparison` panel で `Status` が `Ready` になることを確認し、source filter を `External` または対象 source label に切り替えます。比較対象がないことを確認したい場合は `Own` / `Unknown` も使えます。
6. report には、selected frame / selected time、source filter、sidecar status、record / skipped / error count、entry status、source role / label、snapshot frame、own timestamp ns、nearest timestamp ns、delta ns、balls / robots、raw payload 表示を残します。UI の raw payload は `Restored` が rawPayloadRestored true、`Missing` が false です。
7. 必要に応じて `Tracker.CaptureReplay` を `--capture <session>/<capture>.jsonl.gz --settings <session>/<capture>.metadata.json --profile <capture時のprofile>` で実行し、`trackerSnapshot` と `trackerComparison` 行、`rawPayloadRestored=True`、`nearest-timestamp` の比較 summary を agent / 検証 / 回帰用 evidence として残します。CLI evidence は UI evidence の補助であり、通常確認の主経路ではありません。

`Tracker Comparison` panel の sidecar status は次のように読みます。

- `Ready`: metadata と `tracker-packet-snapshots.jsonl` を読み、選択 diagnostics entry の comparison を作成できる状態です。
- `NoLogSelected`: diagnostics log がまだ選択されていません。
- `MetadataMissing` / `MetadataCorrupt`: 選択 log に対応する `*.metadata.json` がない、または JSON を読み取れません。古い diagnostics log や壊れた metadata の可能性があります。
- `SnapshotMetadataMissing`: metadata に tracker snapshot log 情報がありません。CaptureOn 比較ログ導入前の capture ではこの状態になり得ます。
- `SidecarNotCreated`: metadata は snapshot sidecar 未作成を示しています。`Tracker:Receive:Enabled=false`、receiver 未起動、または CaptureOn 中に writer が開始されなかった場合を疑います。
- `SidecarPathMissing` / `SidecarMissing`: metadata に sidecar path がない、または metadata が指す file が存在しません。session folder の移動や部分コピーを疑います。
- `SidecarEmpty` または `RecordCount=0`: sidecar は作成されていますが、保存済み tracker packet がありません。official tracker packet が endpoint に流れていない、multicast interface が違う、source がまだ見えていない場合を確認します。
- `SidecarCorrupt`: sidecar JSONL を読み取れません。壊れた file、途中書き込み、手動編集を疑います。
- `Skipped` が 0 より大きい場合は decode または書き込み失敗で snapshot record にできなかった packet があることを示します。`Errors` が 0 より大きい場合は writer 側で記録された error があるため、比較結果の代表性を report のリスクに残します。

### `VisionReceiver:Profiles:<name>`

receiver profile override です。未指定項目は top-level `VisionReceiver` の値を引き継ぎます。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | profile 切替後に join する multicast group address です。 |
| `Port` | profile 切替後に bind / receive する UDP port です。 |
| `InterfaceAddress` | profile 切替後に multicast join へ使う local IPv4 address です。 |

### `Tracker`

tracker 全体の設定です。

| キー | 意味 |
| --- | --- |
| `Enabled` | `true` なら受信 packet を tracker engine に流します。`false` なら raw viewer だけ動き、tracked 更新は行いません。 |
| `PublishUdp` | `true` なら tracker packet publisher が UDP 送信します。`false` なら tracked 計算は続けますが UDP 送信は行いません。なお UI の `Publish OK` は送信保証カウンタではなく、無送信でも増えることがあります。 |
| `SourceName` | tracker packet の source name です。profile ごとの publish 設定と合わせて packet generator に渡されます。 |
| `Uuid` | tracker packet の UUID です。受信側で source 識別に使う値です。 |
| `ActiveProfileName` | 起動時に使う profile 名です。`Tracker:Profiles` に存在する必要があります。 |
| `Diagnostics` | tracker の raw / tracked 診断ログ出力設定です。 |
| `Receive` | CaptureOn 比較ログ用に official tracker packet を受信する設定です。既定は無効です。 |
| `RuntimeOverrides` | 起動時に active profile へ上書きする optional override 群です。profile 定義を変えずに一時的な publish / tracker tuning を差し込む用途です。 |
| `Profiles` | profile ごとの publish / engine / tuning 設定です。UI と API の profile switch 対象にもなります。 |

### `Tracker:Receive`

CaptureOn 比較ログ用の live tracker packet receiver 設定です。`Enabled=true` のときだけ `TrackerConnectionLib` receiver が起動し、active profile の `Tracker:Profiles:<name>:Publish:MulticastAddress` / `Port` を監視します。受信した `TrackerWrapperPacket` は CaptureOn 中だけ `tracker-packet-snapshots.jsonl` へ保存され、Capture Off 中は追記しません。

| キー | 意味 |
| --- | --- |
| `Enabled` | `true` なら official tracker packet receiver を起動します。既定は `false` です。 |
| `InterfaceAddress` | multicast join に使う local IPv4 address です。`null` の場合は receiver 実装の既定に任せます。複数 NIC がある環境では明示指定してください。 |

```json
"Receive": {
  "Enabled": true,
  "InterfaceAddress": "192.0.2.10"
}
```

### `Tracker:Diagnostics`

tracker の調査用診断ログ設定です。diagnostics log は常に出力され、`FilePath` が `null` の場合は `VisionReceiver:PacketCapture:DirectoryPath` 配下に起動ごとの `tracker-diagnostics-<timestamp>-<guid>.log` を作成します。packet capture 有効時は、capture sidecar の `*.tracker-diagnostics.log` にも同時に出力します。

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
    "Tracker.Server.Tracking.TrackerCoordinator": "Warning"
  }
}
```

全体の `Information` log も止めたい場合は `Default` を `Warning` にします。

### `Tracker:RuntimeOverrides`

profile の base 設定に対する「上書き」です。指定しない項目は profile 側の値をそのまま使います。

| キー | 意味 |
| --- | --- |
| `Publish.MulticastAddress` | tracker packet の送信先 address を一時的に上書きします。 |
| `Publish.Port` | tracker packet の送信先 port を一時的に上書きします。 |
| `Publish.SourceName` | tracker packet の source name を一時的に上書きします。 |
| `Publish.Uuid` | tracker packet の UUID を一時的に上書きします。 |
| `RobotTracker.*` | robot tracking の tuning を active profile に対して上書きします。 |
| `BallTracker.*` | ball tracking の tuning を active profile に対して上書きします。 |
| `KickDetector.*` | kick 判定の tuning を active profile に対して上書きします。 |

現状の UI / HTTP API では individual override 値の入力機能はなく、`appsettings.json` 起動時設定として使う想定です。

### `Tracker:Profiles:<name>:Publish`

tracker packet の送信先設定です。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | official tracker packet の送信先 address です。multicast / unicast のどちらも指定できます。 |
| `Port` | official tracker packet の送信先 port です。profile ごとに切り替えられます。 |

### `Tracker:Profiles:<name>:Engine`

tracker engine の時系列処理設定です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `ReorderWindowNs` | ns | packet arrival 順と event time 順がずれたとき、遅延 packet を待つ reorder window です。大きいほど並べ替えには強くなりますが commit は遅れます。 |
| `MergeWindowNs` | ns | 近接 timestamp の detection を同じ world frame にまとめる window です。大きいほど camera 間 merge はしやすくなりますが、別 frame まで混ざりやすくなります。 |
| `GeometryResetFieldLengthThresholdMm` | mm | field length 変化を geometry reset とみなす閾値です。 |
| `GeometryResetFieldWidthThresholdMm` | mm | field width 変化を geometry reset とみなす閾値です。 |
| `KalmanInitialVelocityVariance` | 任意係数 | 新規 track の速度不確かさです。大きいほど初期の観測揺れを速度として取り込みやすくなります。 |
| `KalmanProcessNoiseScale` | 任意係数 | `ProcessNoise` を Kalman prediction の分散へ変換する係数です。大きいほど急な動きへ追従しやすく、停止時の揺れは増えやすくなります。 |
| `MeasurementNoiseVarianceScale` | 任意係数 | `MeasurementNoise` を観測分散へ変換するときの係数です。大きいほど raw detection の小刻みな揺れを弱く信用します。 |

geometry reset が起きると pending state が切り替わり、旧 geometry 前提の tracked frame は破棄されます。

### `Tracker:Profiles:<name>:RobotTracker`

robot tracking の tuning 値です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `ProcessNoise` | 任意係数 | model 側の変化量をどれだけ許すかです。大きいほど素早い動きに追従しやすく、安定性は下がります。 |
| `MeasurementNoise` | 任意係数 | 観測値のノイズをどれだけ見込むかです。大きいほど観測を弱く信用します。 |
| `VisibilityHalfLifeSeconds` | s | 観測が来ない track の visibility をどの速度で減衰させるかです。 |
| `Gate` | 任意係数 | 既存 track と新観測を同一対象とみなす近傍判定の厳しさです。小さいほど厳しくなります。 |
| `OutlierLimitMm` | mm | 外れ値として弾く許容距離の上限です。 |
| `IdentitySwitchDistanceMm` | mm | 既存別 ID track 近傍へ突然現れた robot id 変更候補を抑制する距離です。`0` で無効化できます。 |
| `OrientationMeasurementNoiseRad` | rad | robot 向き観測の noise 想定です。大きいほど向き観測を弱く信用します。 |
| `OrientationProcessNoise` | 任意係数 | robot 向き filter の model 変化量をどれだけ許すかです。 |
| `InitialAngularVelocityVariance` | 任意係数 | 新規 robot track の初期角速度不確かさです。 |
| `AngularVelocityLimitRadPerS` | rad/s | robot 角速度推定の上限です。 |

### `Tracker:Profiles:<name>:BallTracker`

ball tracking の tuning 値です。意味は robot tracker とほぼ同じですが、ball 固有に `TrackLifetimeNs` を持ちます。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `ProcessNoise` | 任意係数 | ball motion model の変化量をどれだけ許すかです。 |
| `MeasurementNoise` | 任意係数 | ball 観測値のノイズ想定です。 |
| `VisibilityHalfLifeSeconds` | s | 観測が消えた ball track をどの速度で減衰させるかです。 |
| `Gate` | 任意係数 | 既存 ball track と観測を結び付ける近傍判定の厳しさです。 |
| `OutlierLimitMm` | mm | 外れ値として弾く距離上限です。 |
| `TrackLifetimeNs` | ns | 観測消失後も track を保持する最長時間です。 |

### `Tracker:Profiles:<name>:KickDetector`

kick / chip / contact 周辺の判定設定です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `KickSpeedThresholdMmPerS` | mm/s | この速度以上を kick 検出候補とみなします。 |
| `ChipHeightThresholdMm` | mm | ball 高さがこの値を超えると chip 系挙動の判定に使われます。 |
| `ContactMarginMm` | mm | robot-ball contact をみなす距離マージンです。 |

## profile の考え方

- `default` と `fast` のように複数 profile を置けます
- profile switch は publish 先 port だけでなく engine / robot / ball / kick tuning もまとめて切り替えます
- 同一 profile 名でも runtime override が違えば別設定として適用されます

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

### 起動時 profile を `fast` にする

```json
{
  "Tracker": {
    "ActiveProfileName": "fast"
  }
}
```

## 注意点

- `Tracker:ActiveProfileName` や profile switch 先は、必ず `Tracker:Profiles` に定義した名前にしてください
- multicast 受信に失敗する場合は `VisionReceiver:InterfaceAddress` の明示指定を優先してください
- profile switch API は未知 profile 名に対して 4xx を返さず server error になるため、呼び出し側で事前に profile 一覧を一致させてください
- current environment では solution build に `-m:1 -p:BuildInParallel=false` が必要なことがありますが、`Tracker.Server` の実行方法自体は上記のとおりです
