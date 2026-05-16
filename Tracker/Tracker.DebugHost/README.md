# `Tracker.DebugHost`

`Tracker.DebugHost` は SSL-Vision の UDP 通信内容を受信し、閲覧環境で `Raw` / `Tracked` 表示を確認しながら、必要に応じて公式追跡出力を UDP 配信する ASP.NET Core 実行体です。

## できること

- `VisionReceiver` 設定に従って SSL-Vision 通信内容を受信する
- `Raw` 表示で撮影元ごとの検出結果と集約表示を確認する
- `Tracked` 表示で追跡器の統合結果、蹴り出し、接触、競技場状態、送信関連計数を確認する
- 複数設定名を定義して、UI または API から実行中に切り替える
- `Tracker:PublishUdp` が有効なら公式追跡出力を UDP 多地点配信または単一先配信で送信する
- CaptureOn 中に見えている公式追跡出力を同じ保存単位置き場へ保存し、後から自前出力と比較する

## 前提

- .NET SDK `10.0`
- SSL-Vision 通信内容を送ってくる送信元
- CaptureOn 比較記録を取る場合は、公式追跡出力の送信先に流れている追跡出力
- 閲覧環境で `Tracker.DebugHost` の HTTP 受付先に接続できること

## 起動方法

作業一式の最上位から実行します。

```bash
dotnet run --project Tracker/Tracker.DebugHost --launch-profile https
```

既定の起動設定名は次の 受付先 を使います。

- `https://localhost:7042`
- `http://localhost:5289`

閲覧環境を自動起動したくない場合は `--no-launch-profile` か `ASPNETCORE_受付先S` を使ってください。

```bash
ASPNETCORE_受付先S=http://0.0.0.0:5289 dotnet run --project Tracker/Tracker.DebugHost --no-launch-profile
```

## 画面の使い方

先頭画面は `/` です。

### 見出し

- `Packets`: 受信した未加工通信内容数です
- `Errors`: 受信、復号、受け口処理で記録された異常数です
- `Remote`: 直近通信内容の送信元番地です
- `Received`: 現在表示中情報の受信時刻です

### `Raw` 表示

- 初期表示は `Raw` です
- 競技場上に未加工の検出結果を描画します
- `Aggregate` と撮影元ごとの表示を切り替えられます
- 右区画で結果番号、撮影元 ID、送信元、未加工 JSON を確認できます

### `Tracked` 表示

- 追跡器が確定した結果を描画します
- 右区画で次を確認できます
- 使用中設定名
- 送信成功数 / 失敗数
- 情報時刻 / 処理済み時刻
- 蹴り出し / 接触 / 競技場状態
- 主球 / 副球
- 黄色 / 青色機体

追跡器側で結果がまだ確定していない場合は `No tracked frame` が表示されます。

`Publish OK` / `Publish Fail` は現在実装の内部計数です。`PublishUdp=false` のときも追跡結果処理自体は成功扱いになり、`Publish OK` が増えることがあります。実送信の有無は `Tracker:PublishUdp` と送信先設定を合わせて判断してください。

### 実行中の設定名切替

- `Tracked` 表示の `Profile Control` から定義済み設定名を選べます
- 切替を要求すると使用中の設定名が切り替わり、古い追跡結果は一度消去されます
- `VisionReceiver:Profiles` に同名設定名がある場合は、受信側の `MulticastAddress` / `Port` / `InterfaceAddress` もその設定名に追従します
- 新しい通信内容で追跡結果が確定すると、新しい設定名の設定で再表示されます

### `Diagnostics` 画面

- `/diagnostics` で `VisionReceiver:PacketCapture:DirectoryPath` 配下の補助文書 `*.tracker-diagnostics.log`、既定の `tracker-diagnostics-*.log`、`Tracker:Diagnostics:FilePath` の記録を読めます
- 上部の時系列つまみを動かすすると、選択中の再生位置が連続的に切り替わります
- 左側の時系列で記録行を時系列に送れます
- 補助文書と同じ基底名の `*.render-snapshots.jsonl.gz` がある場合は、選択行の未加工 / 追跡済み競技場を描画できます
- 左右競技場の見出し行で表示元を `Vision Input`、自前追跡器、`External`、`Unknown`、送信元表示名[^送信元表示名]から選択できます。表示元には曖昧な `All` は含めず、記録変更時は左 `Vision Input` / 右自前追跡器に戻ります。
- 新規保存に `tracker-snapshot-alignment.jsonl` がある場合、`External` / `Unknown` / 送信元表示名の表示元は保存時対応表を優先し、選択中の再生位置に対応する追跡器記録[^追跡記録]を描画します。対応表[^対応表]がない既存保存では、外部追跡器の時刻対応は非対応、または可能な範囲の推定として表示されます。
- 右側で選択行の未加工 / 追跡済みの球、機体、結果情報を比較できます
- 補助文書の診断記録を選ぶと、`Settings` から保存付帯情報に保存された記録時設定名と解決済み設定[^解決済み設定] を確認できます
- 補助文書の診断記録と追跡出力記録[^追跡出力記録]が揃っている場合は、折り畳み可能な `Tracker Comparison` 区画で自前追跡器と外部追跡器の差分を確認できます
- `Tracker Comparison` 区画の送信元抽出条件は `All`、`External`、`Own`、`Unknown`、送信元表示名単位で切り替えられます。通常確認では `External` または対象送信元表示名を選び、新規保存では保存済み対応表に対応する外部追跡器の追跡器記録と比較します。対応表がない既存保存で最近傍時刻を使う場合は、可能な範囲の推定として表示されます。`Own` は自前追跡器自身の出力です。
- `Tracker Comparison` は追跡出力記録と対応表用補助文書を記録選択時に軽量な索引へ変換し、統合再生時系列[^統合再生時系列]も同時に構築します。時系列つまみの移動や再生中の位置更新では、同じ状態の補助文書を再読込しません。100MB を超える補助文書でも、再生位置を動かすたびの I/O と解析量は文書大きさに比例しません。
- 再生操作は従来どおり `Play`、`Fast Forward`、`Stop` の絵柄付き操作配置です。速度選択側には `等倍速` と可変 `早送り倍率` の操作部品を小さく表示します。`4x` / `16x` / `64x` は固定上限ではなく、よく使う倍率のよく使う倍率です。`等倍速` は全ての再生位置を逐次描画せず、30fps 相当の表示更新で実時間の経過に対応する最新の再生位置へ追従します。早送り倍率を選んだ状態で `Play` を押した場合は `等倍速` へ戻さず、選択中倍率の `Fast Forward` として開始します。`Fast Forward` は途中の再生位置を間引かず、保存時刻の差分と倍率で進み、64x 超の倍率も正規化処理や時計処理下限で 64x 相当に潰さないことを仕様にします。
- 区画には補助文書の状態、保存時対応表の状態、記録数、省略数、異常数、選択中の再生位置、保持中の診断記録行、描画結果、比較状態、対応付け方法、送信元、記録結果、時刻差、球 / 機体、未加工本文の復元状態が表示されます。実際の表示名は `Tracker Comparison` 区画内の英語表記です[^比較表示項目]。

## API

設定名切替は HTTP API からも要求できます。既定の起動設定名では `UseHttpsRedirection()` が有効なため、通常は HTTPS の受付先を使ってください。

```bash
curl -k -X POST https://localhost:7042/api/tracker/profile-switch/fast
```

- 受付先の `{profileName}` は `Tracker:Profiles` に定義した名前です
- 既定の起動設定名の通常系では `202 Accepted` が返ります
- 存在しない設定名を指定すると `Tracker:Profiles` 解決に失敗し、現状実装では 4xx ではなく実行体異常になります

## 設定文書

主な設定は [設定文書](./appsettings.json) にあります。

### `VisionReceiver`

未加工 SSL-Vision 通信内容の受信設定です。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | 受信対象の多地点配信集合番地です。値が多地点配信範囲ならその集合へ参加します。通常は SSL-Vision 側の多地点配信番地を指定します。 |
| `Port` | SSL-Vision 通信内容の受信口番号です。 |
| `InterfaceAddress` | 多地点配信参加に使う手元 IPv4 番地です。`null` の場合は利用可能な IPv4 接続口を自動探索します。複数 NIC がある環境や参加失敗時は明示指定すると安定します。 |
| `PacketCapture` | 受信した UDP 通信内容を後で再生できるように圧縮保存する設定です。 |
| `Profiles` | 設定名ごとの受信設定の上書きです。`Tracker` 側の使用中設定名と同名項目があれば、起動時と実行中の設定名切替完了後にその受信設定へ追従します。 |

### `VisionReceiver:PacketCapture`

SSL-Vision から着信した UDP 通信単位を、復号前の元の列として `jsonl.gz` に保存します。各行には `receivedAt`、送信元番地、本文の `base64` が入るため、後から同じ順序で `SSL_WrapperPacket` に戻して追跡器へ再投入できます。復号に失敗した通信内容も保存対象です。

保存を開始すると、`<prefix>-<timestamp>-<guid>` という CaptureOn 保存単位置き場を作り、その中に同じ基底名で次の補助文書も作成します。

- `<prefix>-<timestamp>-<guid>.jsonl.gz`: 通信内容保存本体
- `<prefix>-<timestamp>-<guid>.metadata.json`: 保存時の `Tracker` 設定と解決済み設定
- `<prefix>-<timestamp>-<guid>.tracker-diagnostics.log`: 保存と対応する追跡器診断記録。`Tracker:Diagnostics:FilePath` が指定されていても、保存有効時は補助文書として同時に出力します。
- `<prefix>-<timestamp>-<guid>.render-snapshots.jsonl.gz`: 時系列表示と再生位置の逆方向移動に使う描画記録。追跡処理の内部状態ではなく、確定済み `TrackerFrame` だけを保存します。
- `tracker-packet-snapshots.jsonl`: CaptureOn 中に `Tracker:Receive:Enabled=true` の受信処理が見た公式追跡出力の記録を保存する補助文書です。
- `tracker-snapshot-alignment.jsonl`: CaptureOn 中の診断記録行、描画記録、追跡器送信元記録を作業単位内の時系列で対応付ける再生用補助文書です。外部追跡器の `TrackedFrame.timestamp` が自前追跡器と別時刻系でも、`receivedAt` と作業単位相対時刻で `/diagnostics` の表示元と比較を再現するために使います。形式版数 2 では診断記録の 1 行単位ではなく、最も記録間隔が短い送信元に合わせた再生位置の記録を保存し、同じ未加工入力 / 描画結果を複数の高速な追跡出力記録から参照できます。v1 互換は持たず、記録選択時の索引構築と、再生位置を動かす操作での高速検索を優先します。
- `diagnostics-samples.jsonl`: CaptureOn 中に `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` の周期で最新の未加工情報と追跡器記録を同じ標本行として固定する診断標本用補助文書です。

`metadata.json` には使用中設定名だけではなく、`Tracker:Profiles` 配下の設定値と、起動時上書き適用後の解決済み設定も保存します。CaptureOn 比較記録がある場合は、`SessionFolder`、`PacketPath`、`DiagnosticsLogPath`、`RenderSnapshotPath`、`TrackerSnapshotSidecarPath`、`TrackerSnapshotAlignmentPath`、`TrackerSnapshotLog`、`TrackerSnapshotAlignmentLog`、`TrackerSnapshotSources` もここから辿ります。

| キー | 意味 |
| --- | --- |
| `Enabled` | 起動時の通信内容保存初期値です。起動後は画面の `Capture On/Off` 操作で切り替えできます。 |
| `DirectoryPath` | 保存記録の出力置き場です。相対経路は実行文書の置き場から解決します。 |
| `FilePrefix` | 保存記録名の接頭辞です。実際の文書名は `<prefix>-<timestamp>-<guid>.jsonl.gz` になります。 |
| `FlushEachPacket` | `true` なら通信内容ごとに書き出します。異常終了時の欠落は減りますが、I/O 負荷は上がります。 |
| `DiagnosticsSampleIntervalMilliseconds` | CaptureOn 中に `diagnostics-samples.jsonl` へ最新の未加工情報と追跡器記録を固定保存する周期です。0 以下の場合は既定値 `100` ms を使います。 |

現在の `設定文書` では、起動時の保存は無効です。画面で `Capture On` にした後は通信内容ごとに書き出します。

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

保存された記録は `Tracker.CaptureReplay` で再生 / 解析できます。通常の利用者確認は `/diagnostics` の比較区画を主経路にし、この CLI は自動検証や回帰調査で同じ作業単位を再現するために使います。詳細は [`Tracker.CaptureReplay` の文書](../Tracker.CaptureReplay/README.md) を参照してください。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-<timestamp>-<guid>.jsonl.gz \
  --settings Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-<timestamp>-<guid>.metadata.json \
  --profile sim
```

CaptureOn 比較記録を CLI で検証する場合は、`--capture` に保存単位置き場、または保存単位置き場内の `*.jsonl.gz` を渡します。保存単位置き場を渡した場合、CLI は同じ置き場の `*.metadata.json` から通信内容保存と解決済み追跡器設定を特定します。この場合、再生は保存時点の解決済み追跡器設定を使い、付帯情報の相対経路から `tracker-packet-snapshots.jsonl` と `tracker-snapshot-alignment.jsonl` も解決します。出力に `trackerSnapshot ... rawPayloadRestored=True` と `trackerComparison ... rule=saved-session-alignment ...` が出れば、保存時対応表に基づく比較まで読めています。対応表がない既存保存では `legacy-nearest-timestamp` または非対応状態を確認してください。

未加工入力に対して自前追跡器が遅れて見える場合は、保存記録を手作業で読む代わりに遅延解析を使います。`--analyze-latency` は未加工の検出結果の受信間隔と、再生後に自前追跡器の結果が確定するまでの保存時刻上の遅れを出します。付帯情報由来の記録行が多い作業単位では `--skip-tracker-snapshots` と `--max-latency-frames` で出力量を絞れます。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture <session-folder> \
  --analyze-latency \
  --skip-tracker-snapshots \
  --max-latency-frames 8
```

ER-Force 外部追跡器を手動検証に使う場合は、`Tracker/Design/Core/Ref/ibis` 配下の Docker 開発環境を使えます。継続的検証や通常の単体試験は Docker に依存させず、手元の再現確認だけで使ってください。

```bash
cd Tracker/Design/Core/Ref/ibis
./scripts/docker-dev.sh --sim erforce -d
```

ER-Force 設定名は同作業一式の `docker/dev/README.md` に従います。`Tracker.DebugHost` 側は `Tracker:Receive:Enabled=true` とし、外部追跡器が送る多地点配信送信先と `Tracker:Receive:MulticastAddress` / `Port` / `InterfaceAddress` が合っていることを確認してから CaptureOn します。検証後の停止は同じ置き場で `./scripts/docker-dev.sh down` を使います。

自動試験や回帰確認では終了符号を使えます。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture <capture.jsonl.gz> \
  --profile sim \
  --expect 'committed-frames>0' \
  --expect 'max-balls<=1'
```

- `--expect <condition>`: 概要指標の期待条件です。失敗すると終了符号 `1` になります。
- `--detail-filter <condition>`: 条件に一致する確定済み結果の詳細を出力します。複数指定した場合は かつ条件です。
- `--max-details <count>`: 詳細出力数を制限します。
- `--settings <file>`: `Tracker.DebugHost/appsettings.json` 形式、または保存の `metadata.json` 形式から追跡器設定を読み込みます。`Tracker.DebugHost/appsettings.json` 形式では `Tracker:RuntimeOverrides` も設定へ反映します。

`--settings` は追跡器設定の解決にも使われます。CaptureOn 付帯情報を渡す通常手順では、保存時に保存済みの解決済み設定と補助文書の相対経路を使うため、当時の設定名、上書き、記録補助文書をまとめて再現できます。手元の `Tracker.DebugHost/appsettings.json` を渡すのは、保存付帯情報がない古い保存を現在設定で再評価したい場合や、意図的に別設定で再生したい場合に限ります。その場合、付帯情報から辿る `tracker-packet-snapshots.jsonl` は自動解決されないため、CaptureOn 比較記録の確認手順としては付帯情報を優先してください。手書き付帯情報を作る場合は、通信内容保存と同じ保存単位置き場を基準に `PacketPath`、`MetadataPath`、`DiagnosticsLogPath`、`RenderSnapshotPath`、`TrackerSnapshotSidecarPath`、`TrackerSnapshotLog` を矛盾なく入れてください。

利用できる概要指標は `packets`, `detections`, `geometries`, `committed-frames`, `max-balls`, `max-robots`, `max-raw-balls`, `max-raw-yellow`, `max-raw-blue` です。結果詳細の絞り込みでは `balls`, `robots`, `raw-balls`, `raw-yellow`, `raw-blue` を使えます。未加工系の指標は、その確定済み結果の元になった検出結果から集計します。

例えば、未加工情報では球が 1 個なのに再生後結果で球が 2 個以上になる箇所を確認する場合は次のようにします。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture <capture.jsonl.gz> \
  --profile sim \
  --expect 'max-balls>=2' \
  --detail-filter 'raw-balls==1' \
  --detail-filter 'balls>=2'
```

試験符号から直接扱う場合は `VisionPacketCaptureFile.ReadRecords(path)` で読み戻し、各記録の `ParsePacket()` を `TrackerCoordinator.ProcessPacket(packet, record.ReceivedAt)` または `TrackerEngine.Update(...)` へ順番に渡すことで再生できます。

### CaptureOn 比較記録の手動確認証跡

CaptureOn 比較記録を手動で確認する場合は、次の順に証跡を残します。

1. `Tracker:Receive:Enabled=true` にし、受信処理が監視する受付先を確認します。`Tracker:Receive:MulticastAddress` / `Port` が未指定なら起動時に解決した自前追跡器の送信先を使います。既定の `sim` 設定名は `224.5.23.2:11010`、`default` 設定名は `224.5.23.2:10010` です。外部追跡器が別の受付先へ送信している場合は `Tracker:Receive:MulticastAddress` / `Port` を明示します。複数 NIC 環境では `Tracker:Receive:InterfaceAddress` を明示します。
2. `Tracker.DebugHost` を起動し、画面で `Capture On` にしてから SSL-Vision 通信内容と公式追跡出力を流します。`Tracker:Receive:Enabled=false` のままでは公式追跡出力の受信処理が起動しないため、通信内容保存と診断記録は残っても追跡出力記録の補助文書は増えません。
3. `Capture Off` 後、`VisionReceiver:PacketCapture:DirectoryPath` 配下の保存単位置き場に `*.jsonl.gz`、`*.metadata.json`、`*.tracker-diagnostics.log`、`*.render-snapshots.jsonl.gz`、`tracker-packet-snapshots.jsonl`、`tracker-snapshot-alignment.jsonl` があることを確認します。付帯情報では `SessionFolder` と各相対経路、`TrackerSnapshotLog.RecordCount` / `SkippedRecordCount` / `ErrorCount`、`TrackerSnapshotAlignmentLog.RecordCount`、`TrackerSnapshotSources` の `SourceRole` / `SourceLabel` / `RemoteEndpoint` を確認します。
4. `/diagnostics` を開き、同じ保存単位置き場の `*.tracker-diagnostics.log` を選びます。統合再生時系列[^統合再生時系列]、再生位置のつまみ、`Play` / `Fast Forward` / `Stop` の再生操作、速度選択切替欄 (`等倍速`、`4x`、`16x`、`64x`)、左右表示元の選択部品、`Settings` 浮動画面の解決済み設定[^解決済み設定]を確認します。ER-FORCE など、未加工入力より短い間隔で記録する追跡器がある保存では、時系列に追跡器側の高頻度な時点が入り、未加工入力 / 描画競技場が直前の結果を保持することも確認します。等倍速の再生は 30fps 相当の表示更新で実時間の経過に追従するため、高頻度な時点の中間表示を省略する場合があります。
5. 左右競技場で `External`、`Unknown`、対象送信元表示名を選び、選択中の再生位置の保存済み対応表に対応する追跡器記録が競技場に描画されることを確認します。競技場表示は既定の `Split` のほか `Overlay` を選べます。`Overlay` では左表示元が `Layer A`、右表示元が `Layer B` として同一競技場に重なり、凡例の層表示切替で表示/非表示を切り替えられます。`Tracker Comparison` 区画は必要に応じて折り畳めます。
6. `Tracker Comparison` 区画で `Status` が `Ready` になることを確認し、送信元抽出条件を `External` または対象送信元表示名に切り替えます。比較対象がないことを確認したい場合は `Own` / `Unknown` も使えます。
7. 報告書には、選択中の再生位置と時刻、`Play` の表示更新で到達した時刻、保持中の診断記録行と描画結果、競技場表示方式、`Layer A` / `Layer B` の表示元と可視度、送信元抽出条件、補助文書と保存時対応表の状態、記録数 / 省略数 / 異常数、比較状態、送信元、対応付け方法、記録結果、自前追跡器と対応先記録の時刻、差分 ns、球 / 機体、未加工本文表示を残します。UI の未加工本文は `Restored` が `rawPayloadRestored=true`、`Missing` が `false` です。`Play` で表示されなかった中間時点も、再生位置のつまみや比較区画から選択できることを必要に応じて確認します。これらの英語は `Tracker Comparison` 区画の表示項目です[^比較表示項目]。
8. 必要に応じて `Tracker.CaptureReplay` を `--capture <session>/<capture>.jsonl.gz --settings <session>/<capture>.metadata.json --profile <capture時のprofile>` で実行し、`trackerSnapshot` と `trackerComparison` 行、`rawPayloadRestored=True`、`saved-session-alignment` の比較概要を自動検証や回帰確認の証跡として残します。CLI の証跡は UI の証跡の補助であり、通常確認の主経路ではありません。

`Tracker Comparison` 区画の補助文書状態[^補助状態]は次のように読みます。ここでの補助文書は、保存本体と同じ保存単位置き場に保存する補助文書を指します。

- `Ready`: 付帯情報と `tracker-packet-snapshots.jsonl` を読み、選択中の診断記録行の比較を作成できる状態です。
- `NoLogSelected`: 診断記録がまだ選択されていません。
- `MetadataMissing` / `MetadataCorrupt`: 選択記録に対応する `*.metadata.json` がない、または JSON を読み取れません。古い診断記録や壊れた付帯情報の可能性があります。
- `SnapshotMetadataMissing`: 付帯情報に追跡器記録記録情報がありません。CaptureOn 比較記録導入前の保存ではこの状態になり得ます。
- `SidecarNotCreated`: 付帯情報は記録補助文書の未作成を示しています。`Tracker:Receive:Enabled=false`、受信処理未起動、または CaptureOn 中に書き込み処理が開始されなかった場合を疑います。
- `SidecarPathMissing` / `SidecarMissing`: 付帯情報に補助文書経路がない、または付帯情報が指す文書が存在しません。保存単位置き場の移動や部分複写を疑います。
- `SidecarEmpty` または `RecordCount=0`: 補助文書は作成されていますが、保存済み追跡出力がありません。公式追跡出力が受付先に流れていない、多地点配信接続口が違う、送信元がまだ見えていない場合を確認します。
- `SidecarCorrupt`: 補助 JSONL を読み取れません。壊れた文書、途中書き込み、手動編集を疑います。
- `Skipped` が 0 より大きい場合は復号または書き込み失敗で記録にできなかった通信内容があることを示します。`Errors` が 0 より大きい場合は書き込み処理側で記録された異常があるため、比較結果の代表性を報告書の懸念に残します。

### `VisionReceiver:Profiles:<name>`

受信設定の設定名別上書きです。未指定項目は上位の `VisionReceiver` の値を引き継ぎます。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | 設定名切替後に参加する多地点配信集合番地です。 |
| `Port` | 設定名切替後に割り当てて受信する UDP 口番号です。 |
| `InterfaceAddress` | 設定名切替後に多地点配信参加へ使う手元 IPv4 番地です。 |

### `Tracker`

`Tracker` と `Tracker:Profiles:<name>` の共有設定は [`Tracker` 設定の文書](../README.appsettings.md) を参照してください。この文書では `Tracker.DebugHost` 固有の `Tracker:Receive`、`Tracker:Diagnostics`、`Tracker:RuntimeOverrides` だけを説明します。

### `Tracker:Receive`

CaptureOn 比較記録用に公式追跡出力を受け取る設定です。`Enabled=true` のときだけ `TrackerConnectionLib` 受信処理が起動します。`MulticastAddress` / `Port` が未指定なら、起動時の使用中設定名と `Tracker:RuntimeOverrides:Publish` から解決した自前追跡器の送信先を監視します。`MulticastAddress` / `Port` を明示した場合は、受信処理独自の受付先を監視します。受付先の解決は起動時固定で、実行中の設定名切替後に受信受け口は再構成されません。受信した `TrackerWrapperPacket` は CaptureOn 中だけ `tracker-packet-snapshots.jsonl` へ保存され、Capture Off 中は追記しません。

`Enabled=false` のままでは受信処理が起動しないため、外部追跡器の通信内容は記録されません。補助文書が空の場合は、監視する受付先、`InterfaceAddress`、OS の多地点配信経路、外部追跡器の送信先が一致しているか確認してください。

| キー | 意味 |
| --- | --- |
| `Enabled` | `true` なら公式追跡出力受信処理を起動します。既定は `false` です。 |
| `MulticastAddress` | 受信処理が監視する多地点配信集合番地です。`null` の場合は起動時に解決済みの自前追跡器送信先番地を使います。 |
| `Port` | 受信処理が監視する UDP 口番号です。`null` の場合は起動時に解決済みの自前追跡器送信先口番号を使います。 |
| `InterfaceAddress` | 多地点配信参加に使う手元 IPv4 番地です。`null` の場合は受信処理実装の既定に任せます。複数 NIC がある環境では明示指定してください。 |

```json
"Receive": {
  "Enabled": true,
  "MulticastAddress": null,
  "Port": null,
  "InterfaceAddress": "192.0.2.10"
}
```

### `Tracker:Diagnostics`

追跡器の調査用診断記録設定です。診断記録は常に出力され、`FilePath` が `null` の場合は `VisionReceiver:PacketCapture:DirectoryPath` 配下に起動ごとの `tracker-diagnostics-<timestamp>-<guid>.log` を作成します。通信内容保存有効時は、補助文書の `*.tracker-diagnostics.log` にも同時に出力します。

| キー | 意味 |
| --- | --- |
| `FilePath` | 明示的な文書出力先です。`null` の場合は `VisionReceiver:PacketCapture:DirectoryPath` 配下に出力します。 |

現在の `設定文書` は、`packet-captures` 配下へ起動ごとの新規文書を出力する設定です。

```json
"Diagnostics": {
  "FilePath": null
}
```

端末に出る追跡器診断の構造化記録は `Logging:LogLevel` で抑制します。文書出力はこの設定とは別に継続します。

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "Tracker.DebugHost.Tracking.TrackerCoordinator": "Warning"
  }
}
```

全体の `Information` 記録も止めたい場合は `Default` を `Warning` にします。

### `Tracker:RuntimeOverrides`

設定名の基本設定に対する「起動時上書き」です。指定しない項目は設定名側の値をそのまま使います。

| キー | 意味 |
| --- | --- |
| `Publish.MulticastAddress` | 追跡出力の送信先番地を一時的に上書きします。 |
| `Publish.Port` | 追跡出力の送信先口番号を一時的に上書きします。 |
| `Publish.SourceName` | 追跡出力の送信元名を一時的に上書きします。 |
| `Publish.Uuid` | 追跡出力の UUID を一時的に上書きします。 |
| `RobotTracker.*` | 機体追跡の調整値を使用中設定名に対して上書きします。 |
| `BallTracker.*` | 球追跡の調整値を使用中設定名に対して上書きします。 |
| `KickDetector.*` | 蹴り出し判定の調整値を使用中設定名に対して上書きします。 |

現状の UI / HTTP API では個別の上書き値を入力できません。`設定文書` に書いた起動時設定として使います。

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

### 追跡器設定名に対応する受信設定名を分ける

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

### 追跡出力の送信を止めて表示だけ使う

```json
{
  "Tracker": {
    "Enabled": true,
    "PublishUdp": false
  }
}
```

### 起動時設定名を `fast` にする

```json
{
  "Tracker": {
    "ActiveProfileName": "fast"
  }
}
```

## 注意点

- `Tracker:ActiveProfileName` や設定名切替先は、必ず `Tracker:Profiles` に定義した名前にしてください
- 多地点配信受信に失敗する場合は `VisionReceiver:InterfaceAddress` の明示指定を優先してください
- 設定名切替 API は未知設定名に対して 4xx を返さず実行体異常になるため、呼び出し側で事前に設定名一覧を一致させてください
- 現在の環境では全体構築に `-m:1 -p:BuildInParallel=false` が必要なことがありますが、`Tracker.DebugHost` の実行方法自体は上記のとおりです

## 脚注

[^送信元表示名]: 送信元表示名は `Tracker Comparison` 区画や競技場表示元の選択部品に表示する送信元名です。
[^追跡記録]: 追跡器記録は、公式追跡出力を比較表示用に保存した記録です。
[^対応表]: 対応表は、未加工入力、自前追跡器、外部追跡器の記録を同じ再生位置へ対応付ける保存時対応表です。
[^解決済み設定]: 解決済み設定は、設定と起動時上書きを適用した後に実際に使われた設定です。
[^追跡出力記録]: 追跡出力記録は、`tracker-packet-snapshots.jsonl` に保存する公式追跡出力の記録です。
[^統合再生時系列]: 統合再生時系列は、未加工入力、描画結果、外部追跡器記録を 1 つの再生位置として選べるようにした時系列です。
[^比較表示項目]: ここで列挙している英語は `Tracker Comparison` 区画の表示名です。比較対象、対応付けの状態、送信元、時刻差、復元状態を確認するために表示します。
[^補助状態]: 補助文書状態は `Tracker Comparison` 区画の表示名です。補助文書を読めているか、未作成か、壊れているかを示します。
