# Tracker.Server

`Tracker.Server` は SSL-Vision の UDP packet を受信し、ブラウザで raw / tracked viewer を表示しながら、必要に応じて official tracker packet を UDP 配信する ASP.NET Core アプリです。

## できること

- `VisionReceiver` 設定に従って SSL-Vision packet を受信する
- raw viewer で camera ごとの detection と aggregate view を確認する
- tracked viewer で tracker の統合結果、kick / contact / field 状態、publish 関連カウンタを確認する
- 複数 profile を定義して、UI または API から runtime profile switch を行う
- `Tracker:PublishUdp` が有効なら official tracker packet を UDP multicast/unicast で送信する

## 前提

- .NET SDK `10.0`
- SSL-Vision packet を送ってくる送信元
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
- 新しい packet が commit されると、新 profile の context で tracked frame が再表示されます

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

### `Tracker`

tracker 全体の設定です。

| キー | 意味 |
| --- | --- |
| `Enabled` | `true` なら受信 packet を tracker engine に流します。`false` なら raw viewer だけ動き、tracked 更新は行いません。 |
| `PublishUdp` | `true` なら tracker packet publisher が UDP 送信します。`false` なら tracked 計算は続けますが UDP 送信は行いません。なお UI の `Publish OK` は送信保証カウンタではなく、無送信でも増えることがあります。 |
| `SourceName` | tracker packet の source name です。profile ごとの publish 設定と合わせて packet generator に渡されます。 |
| `Uuid` | tracker packet の UUID です。受信側で source 識別に使う値です。 |
| `ActiveProfileName` | 起動時に使う profile 名です。`Tracker:Profiles` に存在する必要があります。 |
| `RuntimeOverrides` | 起動時に active profile へ上書きする optional override 群です。profile 定義を変えずに一時的な publish / tracker tuning を差し込む用途です。 |
| `Profiles` | profile ごとの publish / engine / tuning 設定です。UI と API の profile switch 対象にもなります。 |

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
