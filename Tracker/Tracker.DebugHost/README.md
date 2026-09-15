# Tracker.DebugHost

`Tracker.DebugHost` は SSL-Vision の UDPパケットを受信し、ブラウザで未加工入力と追跡結果の表示を確認しながら、必要に応じて公式形式のトラッカーパケットを UDP で配信する ASP.NET Core アプリケーションです。


本書で raw vision は SSL-Vision の検出情報を指す。カメラの画像や動画そのものではない。
## できること

- `VisionReceiver` 設定に従って SSL-Vision パケットを受信する
- `Raw` 表示でカメラごとの検出結果と集約表示を確認する
- `Tracked` 表示でトラッカーの統合結果、キック・接触・フィールドの状態、送信関連カウンタを確認する
- 複数の設定プロファイルを定義し、UI または API から実行中の設定プロファイルを切り替える
- `Tracker:PublishUdp` が有効なら公式形式のトラッカーパケットを UDP のマルチキャストまたはユニキャストで送信する
- CaptureOn 中に受信した公式形式のトラッカーパケットを同じ記録単位のフォルダへ保存し、後から自前トラッカー出力と比較する

## 前提

- .NET SDK `10.0`
- SSL-Vision パケットを送る送信元
- CaptureOn 比較ログを取る場合は、公式形式のトラッカーパケットが流れているマルチキャスト接続先
- ブラウザで `Tracker.DebugHost` のHTTP接続先に接続できること

## 起動方法

リポジトリ直下から実行します。

```bash
dotnet run --project Tracker/Tracker.DebugHost --launch-profile https
```

既定の起動設定は次の URL を使います。

- `https://localhost:7042`
- `http://localhost:5289`

ブラウザを自動起動したくない場合は `--no-launch-profile` か `ASPNETCORE_URLS` を使ってください。

```bash
ASPNETCORE_URLS=http://0.0.0.0:5289 dotnet run --project Tracker/Tracker.DebugHost --no-launch-profile
```

## 画面の使い方

トップページは `/` です。

### 画面ヘッダー

- `Packets`: 受信した未加工パケット数です
- `Errors`: 受信・デコード・ソケット処理で記録されたエラー数です
- `Remote`: 直近パケットの送信元接続先です
- `Received`: 現在表示中データの受信時刻です

### `Raw` モード

- 初期表示は `Raw` です
- フィールドに未加工の検出結果を描画します
- `Aggregate` とカメラごとの表示を切り替えられます
- 右側の表示領域で観測フレームの番号、カメラ ID、表示元、未加工 JSON を確認できます

### `Tracked` モード

- トラッカーが確定した追跡フレームを描画します
- 右側の表示領域で次を確認できます
- 有効な設定プロファイルの名前
- 送信成功数 / 失敗数
- データ時刻 / 処理完了時刻
- キック / 接触 / フィールドの状態
- 主対象 / 補助対象のボール
- 黄色 / 青色チームのロボット

トラッカー側で追跡フレームがまだ確定していない場合は `No tracked frame` が表示されます。

`Publish OK` / `Publish Fail` は現在実装の内部カウンタです。`PublishUdp=false` のときも追跡フレームの処理自体は成功扱いになり、`Publish OK` が増えることがあります。実送信の有無は `Tracker:PublishUdp` と送信先設定を合わせて判断してください。

### 実行時の設定プロファイルの切り替え

- `Tracked` モードの `Profile Control` から定義済みの設定プロファイルを選べます
- 切り替えを要求すると有効な設定プロファイルが切り替わり、古い追跡フレームは一度消去されます
- `VisionReceiver:Profiles` に同名の設定プロファイルがある場合は、受信処理のマルチキャスト用の通信アドレス、通信ポート、使用する IPv4 の通信アドレスもその設定プロファイルに追従します
- 新しいパケットが確定すると、新しい設定プロファイルに基づく追跡フレームが再表示されます

### `Diagnostics` ページ

- `/diagnostics` で `VisionReceiver:PacketCapture:DirectoryPath` 配下の `*.tracker-diagnostics.log`、既定の `tracker-diagnostics-*.log`、`Tracker:Diagnostics:FilePath` のログを読めます
- 上部の timeline scrubber をドラッグすると、選択中の再生時点が連続的に切り替わります
- 左側の replay timeline でログ行を時系列にスクロールできます
- キャプチャーに付随する補助ファイルと同じ基本名の `*.render-snapshots.jsonl.gz` がある場合は、選択行の未加工入力と追跡結果をフィールドに描画できます
- 左右の `Field` 見出し行で `Field source` を `Vision Input`、`ibis tracker`、`External`、`Unknown`、保存された表示元名から選択できます。`Field source` には曖昧な `All` は含めず、ログ変更時は左 `Vision Input` / 右 `ibis tracker` に戻ります。
- 新規記録に `tracker-snapshot-alignment.jsonl` がある場合、`External` / `Unknown` / 保存された表示元名の `Field source` は保存時対応表を優先し、選択中の再生時点に対応するトラッカーのスナップショットを描画します。対応付けがない既存記録では、外部トラッカーの時刻対応は非対応または「正確な対応を保証しない推定」として表示されます。
- 右側で選択行の未加工入力と追跡結果について、ボール、ロボット、観測フレーム / 追跡フレームの情報を比較できます
- キャプチャーの診断ログを選ぶと、`Settings` からキャプチャーの付随情報に保存された定義済みの設定プロファイルと解決済みの設定を確認できます
- キャプチャーの診断ログとトラッカーパケットのスナップショット補助ファイルが揃っている場合は、折り畳み可能な `Tracker Comparison` 表示領域で自前トラッカーと外部トラッカーの差分を確認できます
- `Tracker Comparison` 表示領域の表示元の絞り込みは `All`、`External`、`Own`、`Unknown`、保存された表示元名単位で切り替えられます。通常確認では `External` または対象の表示元名を選び、新規記録では保存済みの対応付けに対応する外部トラッカーのスナップショットと比較します。対応付けがない既存記録で最も近い時刻を使う場合は「正確な対応を保証しない推定」として表示されます。
- `Tracker Comparison` はトラッカーパケットのスナップショット補助ファイルと対応付け用の補助ファイルをログ選択時に軽量な索引へ変換し、統合した replay timeline も同時に構築します。再生位置や再生時点を変更しても同じ補助ファイルを再読込しません。100MB を超える補助ファイルでも、再生位置や再生時点を変えるたびの I/O と解析量は補助ファイルのサイズに比例しません。
- 再生操作部は従来どおり `Play`、`Fast Forward`、`Stop` のアイコンボタン配置です。速度選択側には `等倍速` と可変の `早送り倍率` 操作部を小さく表示します。`4x` / `16x` / `64x` は固定上限ではなく定型値を選ぶ補助ボタンです。`等倍速` は全再生時点を逐次描画せず、30fps相当の表示更新で実時間の経過に対応する最新の再生時点へ追従します。早送り倍率の選択中に `Play` を押した場合は `等倍速` へ戻さず、選択中倍率の `Fast Forward` として開始します。`Fast Forward` は再生時点を間引かず収録時の受信時刻差と倍率で進み、64x 超の倍率も正規化やタイマーの下限によって 64x 相当に制限しないことを動作条件にします。
- 表示領域には補助ファイルの状態、対応付けの状態、記録 / 省略 / エラー件数、選択中の replay timeline の索引 / 時刻、保持中の診断行 / 描画内容、比較状態、対応付け規則、表示元の役割 / 表示名、表示元の識別子 / 送信元の接続先、スナップショット側の追跡フレームの番号、自前 / 対応付け済みスナップショットの時刻、時刻差、ボール / ロボット、未加工受信データの復元状態が表示されます。

## API

設定プロファイルの切り替えは HTTP API からも要求できます。既定の起動設定では `UseHttpsRedirection()` が有効なため、通常は HTTPS の接続先を使ってください。

```bash
curl -k -X POST https://localhost:7042/api/tracker/profile-switch/fast
```

- URL 内のパラメータ `{profileName}` は `Tracker:Profiles` に定義した名前です
- 既定の起動設定の通常系では `202 Accepted` が返ります
- 存在しない設定プロファイルの名前を指定すると `Tracker:Profiles` の解決に失敗し、現状実装では 4xx ではなくサーバーエラーになります

## 設定ファイル

主な設定は [設定ファイル](./appsettings.json) にあります。

### `VisionReceiver`

未加工の SSL-Vision パケットの受信設定です。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | 受信対象のマルチキャスト宛先です。値がマルチキャスト範囲ならその宛先へ参加します。通常は SSL-Vision 側のマルチキャスト宛先を指定します。 |
| `Port` | SSL-Vision パケットの受信に使う通信ポートです。 |
| `InterfaceAddress` | マルチキャスト参加に使う IPv4 の通信アドレスです。`null` の場合は利用可能な IPv4 の通信アドレスを自動探索します。ネットワーク接続が複数ある環境や参加に失敗する場合は、明示指定すると安定します。 |
| `PacketCapture` | 受信した UDP パケットを後で再生できるように圧縮保存する設定です。 |
| `Profiles` | 設定プロファイルごとの受信処理の上書き設定です。`Tracker` 側の有効な設定プロファイルの名前と同名の項目があれば、起動時と実行中の設定プロファイルの切り替え後にその受信設定へ追従します。 |

### `VisionReceiver:PacketCapture`

SSL-Vision から受信したUDPパケットを、protobuf デコード前のバイト列として `jsonl.gz` に保存します。各行には `receivedAt`、送信元の通信アドレスと通信ポート、受信データを Base64 で符号化した文字列が入るため、後から同じ順序で `SSL_WrapperPacket` に戻してトラッカーへ再投入できます。デコードに失敗したパケットも保存対象です。

キャプチャーを開始すると、`<prefix>-<timestamp>-<guid>` という CaptureOn の記録単位フォルダを作り、その中に同じ基本名で次の補助ファイルも作成します。

- `<prefix>-<timestamp>-<guid>.jsonl.gz`: パケットキャプチャーの本体
- `<prefix>-<timestamp>-<guid>.metadata.json`: キャプチャー時の `Tracker` 設定と解決済みの設定プロファイル
- `<prefix>-<timestamp>-<guid>.tracker-diagnostics.log`: キャプチャーと対応するトラッカー診断ログ。`Tracker:Diagnostics:FilePath` が指定されていても、キャプチャーが有効なときは補助ファイルとして同時に出力します。
- `<prefix>-<timestamp>-<guid>.render-snapshots.jsonl.gz`: replay timeline と逆方向の再生位置移動に使う render snapshot。追跡エンジンの内部状態ではなく、確定済みの `TrackerFrame` だけを保存します。
- `tracker-packet-snapshots.jsonl`: CaptureOn 中に `Tracker:Receive:Enabled=true` の受信処理が受信した公式形式のトラッカーパケットのスナップショット補助ファイルです。
- `tracker-snapshot-alignment.jsonl`: CaptureOn 中の診断記録、render snapshot、トラッカーの表示元スナップショットを、1 回の記録の replay timeline 上で対応付ける補助ファイルです。外部トラッカーの `TrackedFrame.timestamp` が自前トラッカーと別の時刻系でも、`receivedAt` と記録開始からの相対時間を使って `/diagnostics` の `Field source` と比較を再生できます。保存形式の版 2 では診断ログ行単位ではなく、最も更新が速い表示元の周期に合わせた replay timeline の記録を保存し、同じ `Vision` / 描画記録を複数の高速なトラッカー記録から参照できます。版 1 との互換は持たず、ログ選択時の索引構築と再生位置移動時の高速参照を優先します。
- `diagnostics-samples.jsonl`: CaptureOn 中に `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` の周期で最新の未加工入力とトラッカースナップショットを同じ採取記録として固定する、診断用の補助ファイルです。

`metadata.json` には有効な設定プロファイルの名前だけではなく、`Tracker:Profiles` 配下の設定プロファイルの値と、実行時の上書き適用後に解決した設定も保存します。CaptureOn 比較ログがある場合は、`SessionFolder`、`PacketPath`、`DiagnosticsLogPath`、`RenderSnapshotPath`、`TrackerSnapshotSidecarPath`、`TrackerSnapshotAlignmentPath`、`TrackerSnapshotLog`、`TrackerSnapshotAlignmentLog`、`TrackerSnapshotSources` もここから辿ります。

| キー | 意味 |
| --- | --- |
| `Enabled` | 起動時のパケットキャプチャーの初期値です。起動後は画面の `Capture On/Off` ボタンで切り替えできます。 |
| `DirectoryPath` | キャプチャーファイルの出力ディレクトリです。相対ファイルパスは実行ファイルのディレクトリから解決します。 |
| `FilePrefix` | キャプチャーファイル名の接頭辞です。実際のファイル名は `<prefix>-<timestamp>-<guid>.jsonl.gz` になります。 |
| `FlushEachPacket` | `true` ならパケットごとに書き出します。異常終了時の欠落は減りますが、入出力負荷は上がります。 |
| `DiagnosticsSampleIntervalMilliseconds` | CaptureOn 中に `diagnostics-samples.jsonl` へ最新の未加工入力とトラッカースナップショットを固定保存する周期です。0 以下の場合は既定値 `100` ms を使います。 |

現在の `appsettings.json` では起動時のキャプチャーは無効ですが、画面で `Capture On` にした後はパケットごとに書き出します。

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

保存されたキャプチャーは `Tracker.CaptureReplay` で再生・分析できます。通常のユーザー確認は `/diagnostics` の `Tracker Comparison` 表示領域を主経路にし、この CLI はエージェント / 自動検証 / 回帰調査で同じ記録単位を再現するために残します。詳細は [CaptureReplay の利用手順](../Tracker.CaptureReplay/README.md) を参照してください。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" \
NUGET_PACKAGES="$PWD/.codex-nuget-packages" \
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-<timestamp>-<guid>.jsonl.gz \
  --settings Tracker/Tracker.DebugHost/bin/Debug/net10.0/packet-captures/ssl-vision-packets-<timestamp>-<guid>.metadata.json \
  --profile sim
```

CaptureOn 比較ログを CLI で検証する場合は、`--capture` に記録単位のフォルダまたはその中の `*.jsonl.gz` を渡します。フォルダを渡した場合、CLI は同じフォルダの `*.metadata.json` からキャプチャーと解決済みのトラッカー設定を読み取ります。再生には記録時点で解決済みのトラッカー設定を使い、付随情報に記録された相対ファイルパスから `tracker-packet-snapshots.jsonl` と `tracker-snapshot-alignment.jsonl` も解決します。出力に `trackerSnapshot ... rawPayloadRestored=True` と `trackerComparison ... rule=saved-session-alignment ...` が出れば、保存時対応表に基づく比較まで読み取れています。対応付けがない既存記録では `legacy-nearest-timestamp` または非対応状態を確認してください。

raw vision に対して自前トラッカーが遅れて見える場合は、キャプチャーファイルを手作業で読む代わりに `--analyze-latency` を使います。これは未加工の検出結果の受信周期と、再生後に自前トラッカーの追跡フレームが確定されるまでのキャプチャー上の遅れを出力します。付随情報由来のスナップショット行が多い記録では `--skip-tracker-snapshots` と `--max-latency-frames` で出力量を絞れます。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture <session-folder> \
  --analyze-latency \
  --skip-tracker-snapshots \
  --max-latency-frames 8
```

ER-Force 外部トラッカーを手動検証に使う場合は、`Tracker/Design/Core/Ref/ibis` 配下の Docker 開発環境を使えます。CI や通常のテストは Docker に依存させず、手元の再現確認だけで使ってください。

```bash
cd Tracker/Design/Core/Ref/ibis
./scripts/docker-dev.sh --sim erforce -d
```

ER-Force の設定プロファイルは同じリポジトリの `docker/dev/README.md` に従います。`Tracker.DebugHost` 側は `Tracker:Receive:Enabled=true` とし、外部トラッカーが送るマルチキャスト接続先と `Tracker:Receive:MulticastAddress` / `Port` / `InterfaceAddress` が合っていることを確認してから CaptureOn します。検証後の停止は同じディレクトリで `./scripts/docker-dev.sh down` を使います。

自動テストや回帰検証では終了値を使えます。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture <capture.jsonl.gz> \
  --profile sim \
  --expect 'committed-frames>0' \
  --expect 'max-balls<=1'
```

- `--expect <condition>`: 集計指標の期待条件です。失敗すると終了値 `1` になります。
- `--detail-filter <condition>`: 条件に一致する確定済みの追跡フレームの詳細を出力します。複数指定した場合は、すべてを満たす条件として扱います。
- `--max-details <count>`: 詳細出力数を制限します。
- `--settings <file>`: `Tracker.DebugHost/appsettings.json` 形式、またはキャプチャーの `metadata.json` 形式からトラッカー設定を読み込みます。`Tracker.DebugHost/appsettings.json` 形式では `Tracker:RuntimeOverrides` も設定プロファイルへ反映します。

`--settings` はトラッカー設定の解決にも使われます。CaptureOn の付随情報を渡す通常経路では、記録時に保存済みの解決済み設定と補助ファイルの相対ファイルパスを使うため、当時の設定プロファイル、上書き設定、スナップショットの補助ファイルをまとめて再現できます。手元の `Tracker.DebugHost/appsettings.json` を渡すのは、キャプチャーの付随情報がない古い記録を現在設定で再評価したい場合や、意図的に別設定で再生したい場合に限ります。その場合、付随情報から辿る `tracker-packet-snapshots.jsonl` は自動解決されないため、CaptureOn 比較ログの確認手順としては付随情報を優先してください。手書きの付随情報を作る場合は、キャプチャーと同じ記録単位のフォルダを基準に `PacketPath`、`MetadataPath`、`DiagnosticsLogPath`、`RenderSnapshotPath`、`TrackerSnapshotSidecarPath`、`TrackerSnapshotLog` を矛盾なく入れてください。

利用できる集計指標は `packets`, `detections`, `geometries`, `committed-frames`, `max-balls`, `max-robots`, `max-raw-balls`, `max-raw-yellow`, `max-raw-blue` です。追跡フレームの詳細の絞り込みでは `balls`, `robots`, `raw-balls`, `raw-yellow`, `raw-blue` を使えます。未加工系の集計指標は、その確定済みの追跡フレームの元になった検出結果群から集計します。

例えば、未加工入力ではボールが 1 個なのに再生後の追跡フレームでボールが 2 個以上になる箇所を確認する場合は次のようにします。

```bash
dotnet run --project Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj --no-restore -- \
  --capture <capture.jsonl.gz> \
  --profile sim \
  --expect 'max-balls>=2' \
  --detail-filter 'raw-balls==1' \
  --detail-filter 'balls>=2'
```

テスト用のソースコードから直接扱う場合は `VisionPacketCaptureFile.ReadRecords(path)` で読み戻し、各記録の `ParsePacket()` を `TrackerCoordinator.ProcessPacket(packet, record.ReceivedAt)` または `TrackerEngine.Update(...)` へ順番に渡すことで再生できます。

### CaptureOn 比較ログの手動検証

CaptureOn 比較ログを手動で確認する場合は、次の順に証跡を残します。

1. `Tracker:Receive:Enabled=true` にし、受信処理が監視する接続先を確認します。`Tracker:Receive:MulticastAddress` / `Port` が未指定なら起動時に解決した自前トラッカーの送信先を使います。既定の `sim` 設定プロファイルは `224.5.23.2:11010`、`default` 設定プロファイルは `224.5.23.2:10010` です。外部トラッカーが別の接続先に送信している場合は `Tracker:Receive:MulticastAddress` / `Port` を明示します。ネットワーク接続が複数ある環境では `Tracker:Receive:InterfaceAddress` を明示します。
2. `Tracker.DebugHost` を起動し、画面で `Capture On` にしてから SSL-Vision パケットと公式形式のトラッカーパケットを流します。`Tracker:Receive:Enabled=false` のままではトラッカー受信処理が起動しないため、キャプチャーと診断ログは残ってもトラッカーパケットのスナップショット補助ファイルは増えません。
3. `Capture Off` 後、`VisionReceiver:PacketCapture:DirectoryPath` 配下の記録単位フォルダに `*.jsonl.gz`、`*.metadata.json`、`*.tracker-diagnostics.log`、`*.render-snapshots.jsonl.gz`、`tracker-packet-snapshots.jsonl`、`tracker-snapshot-alignment.jsonl` があることを確認します。付随情報では `SessionFolder` と各相対ファイルパス、`TrackerSnapshotLog.RecordCount` / `SkippedRecordCount` / `ErrorCount`、`TrackerSnapshotAlignmentLog.RecordCount`、`TrackerSnapshotSources` の `SourceRole` / `SourceLabel` / `RemoteEndpoint` を確認します。
4. `/diagnostics` を開き、同じ記録単位フォルダの `*.tracker-diagnostics.log` を選びます。統合した replay timeline、timeline scrubber、`Play` / `Fast Forward` / `Stop` の再生ボタン、速度選択タブ（`等倍速`、`4x`、`16x`、`64x`）、左右の `Field source` 選択、`Settings` の解決済み設定を確認します。ER-FORCE などトラッカー表示元が `Vision` より速い記録では、replay timeline に高速なトラッカー時点が含まれ、`Vision` / 描画結果の `Field` が選択時点以前の最新結果を保持することも確認します。`Play` の等倍速再生は毎秒 30 回相当の表示更新で実時間の経過に追従するため、高頻度な再生時点の中間表示を省略する場合があります。
5. 左右の `Field` で `External`、`Unknown`、対象の表示元名を選び、選択中の再生時点の保存済み対応付けに対応するトラッカーのスナップショットが `Field` に描画されることを確認します。`Field` の表示は既定の `Split` のほか `Overlay` を選べます。`Overlay` では左の `Field source` が `Layer A`、右の `Field source` が `Layer B` として同じ `Field` に重なり、凡例の表示層チェックボックスで表示 / 非表示を切り替えられます。`Tracker Comparison` 表示領域は必要に応じて折り畳めます。
6. `Tracker Comparison` 表示領域で `Status` が `Ready` になることを確認し、表示元の絞り込みを `External` または対象の表示元名に切り替えます。比較対象がないことを確認したい場合は `Own` / `Unknown` も使えます。
7. 報告書には、選択中の replay timeline の索引 / 時刻、`Play` の表示更新で到達した時刻、保持中の診断行 / 描画内容、フィールドの表示モード、`Layer A` / `Layer B` の `Field source` と可視性、表示元の絞り込み、補助ファイル / 対応付け / 記録の状態、記録 / 省略 / エラー件数、表示元の役割 / 表示名 / 識別子 / 送信元接続先、同率時の選択規則、スナップショット側の追跡フレームの番号、自前側 / 対応付け側の時刻、時刻差、ボール / ロボット、未加工受信データの表示を残します。UI の未加工受信データは `Restored` が `rawPayloadRestored=true`、`Missing` が `rawPayloadRestored=false` を表します。`Play` で表示されなかった中間の再生時点も、timeline scrubber や比較から選択できることを必要に応じて確認します。
8. 必要に応じて `Tracker.CaptureReplay` を `--capture <session>/<capture>.jsonl.gz --settings <session>/<capture>.metadata.json --profile <capture時のprofile>` で実行し、`trackerSnapshot` と `trackerComparison` 行、`rawPayloadRestored=True`、`saved-session-alignment` の比較概要をエージェント / 検証 / 回帰用の検証記録として残します。CLI の検証記録は UI の検証記録の補助であり、通常確認の主経路ではありません。

`Tracker Comparison` 表示領域の補助ファイル状態は次のように読みます。

- `Ready`: 付随情報と `tracker-packet-snapshots.jsonl` を読み、選択中の診断記録の比較を作成できる状態です。
- `NoLogSelected`: 診断ログがまだ選択されていません。
- `MetadataMissing` / `MetadataCorrupt`: 選択したログに対応する `*.metadata.json` がない、または JSON を読み取れません。古い診断ログや壊れた付随情報の可能性があります。
- `SnapshotMetadataMissing`: 付随情報にトラッカーのスナップショットのログ情報がありません。CaptureOn 比較ログ導入前のキャプチャーではこの状態になり得ます。
- `SidecarNotCreated`: 付随情報はスナップショット補助ファイルが未作成であることを示します。`Tracker:Receive:Enabled=false`、受信処理未起動、または CaptureOn 中に書き込み処理が開始されなかった場合を疑います。
- `SidecarPathMissing` / `SidecarMissing`: 付随情報に補助ファイルのファイルパスがない、または付随情報が指すファイルが存在しません。記録単位のフォルダの移動や部分コピーを疑います。
- `SidecarEmpty` または `RecordCount=0`: 補助ファイルは作成されていますが、保存済みのトラッカーパケットがありません。公式形式のトラッカーパケットが接続先に流れていない、マルチキャスト用の通信アドレスが違う、表示元がまだ見えていない場合を確認します。
- `SidecarCorrupt`: 補助ファイルの JSONL を読み取れません。壊れたファイル、途中書き込み、手動編集を疑います。
- `Skipped` が 0 より大きい場合は、デコードまたは書き込み失敗でスナップショット記録にできなかったパケットがあることを示します。`Errors` が 0 より大きい場合は書き込み処理側で記録されたエラーがあるため、比較結果の代表性を報告書のリスクに残します。

### `VisionReceiver:Profiles:<name>`

受信処理の設定プロファイルの上書きです。未指定項目は最上位の `VisionReceiver` の値を引き継ぎます。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | 設定プロファイルの切り替え後に参加するマルチキャストグループの通信アドレスです。 |
| `Port` | 設定プロファイルの切り替え後にソケットを割り当て、受信に使う UDP の通信ポートです。 |
| `InterfaceAddress` | 設定プロファイルの切り替え後にマルチキャスト参加へ使う IPv4 の通信アドレスです。 |

### `Tracker`

トラッカー全体の設定です。

| キー | 意味 |
| --- | --- |
| `Enabled` | `true` なら受信パケットを追跡エンジンに流します。`false` なら未加工入力の表示画面だけ動き、追跡結果の更新は行いません。 |
| `PublishUdp` | `true` ならトラッカーパケットの送信処理が UDP 送信します。`false` なら追跡結果の計算は続けますが UDP 送信は行いません。なお UI の `Publish OK` は送信保証カウンタではなく、無送信でも増えることがあります。 |
| `SourceName` | トラッカーパケットの表示元名です。設定プロファイルごとの送信設定と合わせてパケット生成処理に渡されます。 |
| `Uuid` | トラッカーパケットの UUID です。受信側で表示元の識別に使う値です。 |
| `ActiveProfileName` | 起動時に使う設定プロファイルの名前です。`Tracker:Profiles` に存在する必要があります。 |
| `Diagnostics` | トラッカーの未加工入力 / 追跡結果の診断ログ出力設定です。 |
| `Receive` | CaptureOn 比較ログ用に公式形式のトラッカーパケットを受信する設定です。既定は無効です。 |
| `RuntimeOverrides` | 起動時に有効な設定プロファイルへ上書きする任意の設定群です。設定プロファイルの定義を変えずに、一時的な送信設定 / トラッカー調整値を差し込む用途です。 |
| `Profiles` | 設定プロファイルごとの送信 / 追跡エンジン / 調整値の設定です。UI と API の設定プロファイルの切り替え対象にもなります。 |

### `Tracker:Receive`

CaptureOn 比較ログ用のトラッカーパケット受信設定です。`Enabled=true` のときだけ `TrackerConnectionLib` の受信処理が起動します。`MulticastAddress` / `Port` が未指定なら、起動時に有効な設定プロファイルと `Tracker:RuntimeOverrides:Publish` から解決した自前トラッカーの送信先を監視します。`MulticastAddress` / `Port` を明示した場合は、受信処理独自の接続先を監視します。接続先の解決は起動時固定で、実行時の設定プロファイルの切り替え後に受信ソケットは再構成されません。受信した `TrackerWrapperPacket` は CaptureOn 中だけ `tracker-packet-snapshots.jsonl` へ保存され、Capture Off 中は追記しません。

`Enabled=false` のままでは受信処理が起動しないため、外部トラッカーパケットは記録されません。補助ファイルが空の場合は、監視接続先、`InterfaceAddress`、OS のマルチキャスト経路、外部トラッカーの送信先が一致しているか確認してください。

| キー | 意味 |
| --- | --- |
| `Enabled` | `true` なら公式形式のトラッカーパケットの受信処理を起動します。既定は `false` です。 |
| `MulticastAddress` | 受信処理が監視するマルチキャストグループの通信アドレスです。`null` の場合は起動時に解決済みの自前トラッカー送信先の通信アドレスを使います。 |
| `Port` | 受信処理が監視する UDP の通信ポートです。`null` の場合は起動時に解決済みの自前トラッカー送信先の通信ポートを使います。 |
| `InterfaceAddress` | マルチキャスト参加に使う IPv4 の通信アドレスです。`null` の場合は受信処理実装の既定に任せます。複数 NIC がある環境では明示指定してください。 |

```json
"Receive": {
  "Enabled": true,
  "MulticastAddress": null,
  "Port": null,
  "InterfaceAddress": "192.0.2.10"
}
```

### `Tracker:Diagnostics`

トラッカーの調査用診断ログ設定です。診断ログは常に出力され、`FilePath` が `null` の場合は `VisionReceiver:PacketCapture:DirectoryPath` 配下に起動ごとの `tracker-diagnostics-<timestamp>-<guid>.log` を作成します。パケットのキャプチャーが有効なときは、キャプチャーに付随する `*.tracker-diagnostics.log` にも同時に出力します。

| キー | 意味 |
| --- | --- |
| `FilePath` | 明示的なファイル出力先です。`null` の場合は `VisionReceiver:PacketCapture:DirectoryPath` 配下に出力します。 |

現在の `appsettings.json` は、`packet-captures` 配下へ起動ごとの新規ファイルを出力する設定です。

```json
"Diagnostics": {
  "FilePath": null
}
```

標準出力に出るトラッカー診断の構造化ログは `Logging:LogLevel` で抑制します。ファイル出力はこの設定とは別に継続します。

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "Tracker.DebugHost.Tracking.TrackerCoordinator": "Warning"
  }
}
```

全体の `Information` ログも止めたい場合は `Default` を `Warning` にします。

### `Tracker:RuntimeOverrides`

設定プロファイルの基本設定に対する「上書き」です。指定しない項目は設定プロファイルの値をそのまま使います。

| キー | 意味 |
| --- | --- |
| `Publish.MulticastAddress` | トラッカーパケットの送信先の通信アドレスを一時的に上書きします。 |
| `Publish.Port` | トラッカーパケットの送信先の通信ポートを一時的に上書きします。 |
| `Publish.SourceName` | トラッカーパケットの表示元名を一時的に上書きします。 |
| `Publish.Uuid` | トラッカーパケットの UUID を一時的に上書きします。 |
| `RobotTracker.*` | ロボット追跡の調整値を有効な設定プロファイルに対して上書きします。 |
| `BallTracker.*` | ボール追跡の調整値を有効な設定プロファイルに対して上書きします。 |
| `KickDetector.*` | キック判定の調整値を有効な設定プロファイルに対して上書きします。 |

現状の UI / HTTP API では個別の上書き値を入力する機能はなく、`appsettings.json` の起動時設定として使う想定です。

### `Tracker:Profiles:<name>:Publish`

トラッカーパケットの送信先設定です。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | 公式形式のトラッカーパケットの送信先の通信アドレスです。マルチキャスト / ユニキャストのどちらも指定できます。 |
| `Port` | 公式形式のトラッカーパケットの送信先の通信ポートです。設定プロファイルごとに切り替えられます。 |

### `Tracker:Profiles:<name>:Engine`

追跡エンジンの時系列処理設定です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `ReorderWindowNs` | ns | パケットの到着順とイベント時刻順がずれたとき、遅延パケットを待つ並べ替え対象時間幅です。大きいほど並べ替えには強くなりますが、追跡フレームの確定は遅れます。 |
| `MergeWindowNs` | ns | 近接した時刻の検出結果を同じ追跡フレームにまとめる対象時間幅です。大きいほどカメラ間の統合はしやすくなりますが、別の追跡フレームまで混ざりやすくなります。 |
| `GeometryResetFieldLengthThresholdMm` | mm | フィールドの長さの変化をフィールド形状の初期化とみなす閾値です。 |
| `GeometryResetFieldWidthThresholdMm` | mm | フィールドの幅の変化をフィールド形状の初期化とみなす閾値です。 |
| `KalmanInitialVelocityVariance` | 任意係数 | 新規の追跡状態の速度不確かさです。大きいほど初期の観測揺れを速度として取り込みやすくなります。 |
| `KalmanProcessNoiseScale` | 任意係数 | `ProcessNoise` をカルマン予測の分散へ変換する係数です。大きいほど急な動きへ追従しやすく、停止時の揺れは増えやすくなります。 |
| `MeasurementNoiseVarianceScale` | 任意係数 | `MeasurementNoise` を観測分散へ変換するときの係数です。大きいほど未加工の検出結果の小刻みな揺れを弱く信用します。 |

フィールド形状の初期化が起きると適用待ち状態が切り替わり、旧形状を前提とした追跡フレームは破棄されます。

### `Tracker:Profiles:<name>:RobotTracker`

ロボット追跡の調整値です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `ProcessNoise` | 任意係数 | 状態推定で許容する変化量です。大きいほど素早い動きに追従しやすく、安定性は下がります。 |
| `MeasurementNoise` | 任意係数 | 観測値の揺らぎをどれだけ見込むかです。大きいほど観測を弱く信用します。 |
| `VisibilityHalfLifeSeconds` | s | 観測が来ない追跡状態の可視性をどの速度で減衰させるかです。 |
| `Gate` | 任意係数 | 既存の追跡状態と新観測を同一対象とみなす近傍判定の厳しさです。小さいほど厳しくなります。 |
| `OutlierLimitMm` | mm | 外れ値として弾く許容距離の上限です。 |
| `IdentitySwitchDistanceMm` | mm | 既存の別 ID の追跡状態の近傍へ突然現れたロボット ID 変更候補を抑制する距離です。`0` で無効化できます。 |
| `OrientationMeasurementNoiseRad` | rad | ロボットの向き観測に見込む観測誤差です。大きいほど向き観測を弱く信用します。 |
| `OrientationProcessNoise` | 任意係数 | ロボット向きの状態推定で許容する変化量です。 |
| `InitialAngularVelocityVariance` | 任意係数 | 新規ロボット追跡状態の初期角速度不確かさです。 |
| `AngularVelocityLimitRadPerS` | rad/s | ロボット角速度推定の上限です。 |

### `Tracker:Profiles:<name>:BallTracker`

ボール追跡の調整値です。意味はロボット追跡とほぼ同じですが、ボール固有に `TrackLifetimeNs` を持ちます。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `ProcessNoise` | 任意係数 | ボールの運動状態で許容する変化量です。 |
| `MeasurementNoise` | 任意係数 | ボール観測値に見込む観測誤差です。 |
| `VisibilityHalfLifeSeconds` | s | 観測が消えたボールの追跡状態をどの速度で減衰させるかです。 |
| `Gate` | 任意係数 | 既存のボール追跡状態と観測を結び付ける近傍判定の厳しさです。 |
| `OutlierLimitMm` | mm | 外れ値として弾く距離上限です。 |
| `TrackLifetimeNs` | ns | 観測消失後も追跡状態を保持する最長時間です。 |

### `Tracker:Profiles:<name>:KickDetector`

キック、浮き球、接触周辺の判定設定です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `KickSpeedThresholdMmPerS` | mm/s | この速度以上をキック検出候補とみなします。 |
| `ChipHeightThresholdMm` | mm | ボールの高さがこの値を超えると浮き球の判定に使われます。 |
| `ContactMarginMm` | mm | ロボットとボールの接触とみなす距離の余裕です。 |

## 設定プロファイルの考え方

- `default` と `fast` のように複数の設定プロファイルを置けます
- 設定プロファイルの切り替えでは、送信先の通信ポートだけでなく、追跡エンジン、ロボット追跡、ボール追跡、キック判定の調整値もまとめて切り替えます
- 同一の設定プロファイルの名前でも実行時の上書き設定が違えば別設定として適用されます

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

### トラッカーの設定プロファイルに対応する受信処理の設定プロファイルを分ける

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

### 追跡結果パケットの送信を止めて表示画面だけ使う

```json
{
  "Tracker": {
    "Enabled": true,
    "PublishUdp": false
  }
}
```

### 起動時の設定プロファイルを `fast` にする

```json
{
  "Tracker": {
    "ActiveProfileName": "fast"
  }
}
```

## 注意点

- `Tracker:ActiveProfileName` や設定プロファイルの切り替え先は、必ず `Tracker:Profiles` に定義した名前にしてください
- マルチキャスト受信に失敗する場合は `VisionReceiver:InterfaceAddress` の明示指定を優先してください
- 設定プロファイルの切り替え API は未知の設定プロファイルの名前に対して 4xx を返さずサーバーエラーになるため、呼び出し側で事前に設定プロファイルの一覧を一致させてください
- 現在の実行環境では保存庫全体のビルドに `-m:1 -p:BuildInParallel=false` が必要なことがありますが、`Tracker.DebugHost` の実行方法自体は上記のとおりです
