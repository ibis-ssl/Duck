# Tracker appsettings

この README は、`Tracker.RuntimeHost` と `Tracker.DebugHost` が共有する `Tracker` 設定を説明します。ホスト固有の受信、画面、保存、診断設定はそれぞれの README を参照してください。

- [Tracker.RuntimeHost README](Tracker.RuntimeHost/README.md)
- [Tracker.DebugHost README](Tracker.DebugHost/README.md)

## `Tracker`

トラッカー全体の動作を決める設定です。

| キー | 意味 |
| --- | --- |
| `Enabled` | `true` のとき、受信した SSL-Vision パケットをトラッカーエンジンに渡します。`false` のときはホスト側の受信や表示だけを行い、tracked frame[^tracked-frame] は更新しません。 |
| `PublishUdp` | `true` のとき、確定した tracked frame から公式トラッカーパケットを UDP 送信します。`false` のときも追跡計算は続けますが、UDP 送信は行いません。 |
| `SourceName` | 公式トラッカーパケットへ入れる送信元名です。 |
| `Uuid` | 公式トラッカーパケットへ入れる UUID です。受信側の送信元識別に使われます。 |
| `ActiveProfileName` | 起動時に使うプロファイル名です。`Tracker:Profiles` に同名の定義が必要です。 |
| `Profiles` | プロファイルごとの送信先、トラッカーエンジン、robot、ball、kick 判定設定です。 |

`Tracker.DebugHost` には `Tracker:Receive`、`Tracker:Diagnostics`、`Tracker:RuntimeOverrides` もあります。これらは DebugHost 固有の比較ログと診断用設定なので、詳細は [Tracker.DebugHost README](Tracker.DebugHost/README.md) を参照してください。

## `Tracker:Profiles:<name>`

`default`、`sim`、`fast` のように複数のプロファイルを置けます。プロファイルを切り替えると、送信先だけではなくトラッカーエンジン、robot tracker、ball tracker、kick detector の設定もまとめて切り替わります。

`Tracker.RuntimeHost` は起動時だけプロファイルを決定します。`--profile <name>` を指定した場合はその値を使い、指定しない場合は `Tracker:ActiveProfileName` を使います。

`Tracker.DebugHost` は起動時の `Tracker:ActiveProfileName` に加えて、画面または HTTP API から実行中にプロファイルを切り替えられます。

### `Publish`

公式トラッカーパケットの送信先です。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | 公式トラッカーパケットの送信先アドレスです。multicast と unicast のどちらも指定できます。 |
| `Port` | 公式トラッカーパケットの送信先 UDP ポートです。 |

### `Engine`

時系列処理とトラッカーエンジンの基本設定です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `ReorderWindowNs` | ns | reorder window[^reorder-window] の長さです。値を大きくすると、到着順と event time 順がずれたパケットを吸収しやすくなりますが、tracked frame の確定はその分遅くなります。`0` は遅延パケットを待たず、到着済みの入力だけで即時確定する指定です。 |
| `MergeWindowNs` | ns | 近い timestamp の検出結果を同じ world frame にまとめる時間幅です。値を大きくするとカメラ間の統合はしやすくなりますが、別 frame の検出結果まで混ざりやすくなります。 |
| `GeometryResetFieldLengthThresholdMm` | mm | field length の変化を geometry reset とみなす閾値です。 |
| `GeometryResetFieldWidthThresholdMm` | mm | field width の変化を geometry reset とみなす閾値です。 |
| `KalmanInitialVelocityVariance` | 任意係数 | 新規 track の初期速度の不確かさです。大きいほど初期の観測揺れを速度として取り込みやすくなります。 |
| `KalmanProcessNoiseScale` | 任意係数 | `ProcessNoise` をカルマン推定の分散へ変換する係数です。大きいほど急な動きへ追従しやすく、停止時の揺れは増えやすくなります。 |
| `MeasurementNoiseVarianceScale` | 任意係数 | `MeasurementNoise` を観測分散へ変換する係数です。大きいほど未加工の検出結果の小刻みな揺れを弱く信用します。 |

geometry reset が起きると、旧 geometry 前提の pending frame は破棄されます。

### `RobotTracker`

robot tracking の調整値です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `ProcessNoise` | 任意係数 | model 側の変化量をどれだけ許すかです。大きいほど素早い動きに追従しやすく、安定性は下がります。 |
| `MeasurementNoise` | 任意係数 | 観測値のノイズをどれだけ見込むかです。大きいほど観測を弱く信用します。 |
| `VisibilityHalfLifeSeconds` | s | 観測が来ない track の可視度をどの速度で減衰させるかです。 |
| `OutputVisibilityThreshold` | 任意係数 | 出力に含める可視度の下限です。 |
| `Gate` | 任意係数 | 既存 track と新しい観測を同一対象とみなす近傍判定の厳しさです。小さいほど厳しくなります。 |
| `OutlierLimitMm` | mm | 外れ値として弾く距離上限です。 |
| `IdentitySwitchDistanceMm` | mm | 既存の別 ID track 近傍へ突然現れた robot id 変更候補を抑制する距離です。`0` で無効化できます。 |
| `OrientationMeasurementNoiseRad` | rad | robot 向き観測のノイズ想定です。大きいほど向き観測を弱く信用します。 |
| `OrientationProcessNoise` | 任意係数 | robot 向き filter の model 変化量をどれだけ許すかです。 |
| `InitialAngularVelocityVariance` | 任意係数 | 新規 robot track の初期角速度の不確かさです。 |
| `AngularVelocityLimitRadPerS` | rad/s | robot 角速度推定の上限です。 |

### `BallTracker`

ball tracking の調整値です。意味は robot tracker とほぼ同じですが、ball 固有に `TrackLifetimeNs` を持ちます。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `ProcessNoise` | 任意係数 | ball motion model の変化量をどれだけ許すかです。 |
| `MeasurementNoise` | 任意係数 | ball 観測値のノイズ想定です。 |
| `VisibilityHalfLifeSeconds` | s | 観測が消えた ball track の可視度をどの速度で減衰させるかです。 |
| `OutputVisibilityThreshold` | 任意係数 | 出力に含める可視度の下限です。 |
| `Gate` | 任意係数 | 既存 ball track と観測を結び付ける近傍判定の厳しさです。 |
| `OutlierLimitMm` | mm | 外れ値として弾く距離上限です。 |
| `TrackLifetimeNs` | ns | 観測消失後も track を保持する最長時間です。 |

### `KickDetector`

kick、chip、contact 周辺の判定設定です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `KickSpeedThresholdMmPerS` | mm/s | この速度以上を kick 検出候補とみなします。 |
| `ChipHeightThresholdMm` | mm | ball 高さがこの値を超えると chip 系挙動の判定に使われます。 |
| `ContactMarginMm` | mm | robot と ball の接触とみなす距離マージンです。 |

## 典型的な変更例

### 公式トラッカーパケットの送信を止める

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

### `sim` プロファイルの reorder window を 10 ms にする

```json
{
  "Tracker": {
    "Profiles": {
      "sim": {
        "Engine": {
          "ReorderWindowNs": 10000000
        }
      }
    }
  }
}
```

## 注意点

- `Tracker:ActiveProfileName` と切替先プロファイル名は、必ず `Tracker:Profiles` に定義してください。
- `ReorderWindowNs` は tracked frame の確定時刻に直接影響します。raw vision に対してトラッカーが遅れて見える場合は、まず実際に使われているホスト、設定ファイル、起動引数の `--profile` を確認してください。
- `Tracker.RuntimeHost/appsettings.json` の `sim` は `ReorderWindowNs=10000000` です。`Tracker.DebugHost/appsettings.json` の `sim` はこの README 作成時点では `ReorderWindowNs=100000000` です。

## 脚注

[^tracked-frame]: tracked frame は、未加工の検出結果をトラッカーが統合して出力する 1 フレーム分の追跡結果です。
[^reorder-window]: reorder window は、パケット内時刻が少し古い入力の到着を待つ猶予時間です。複数カメラやネットワーク揺らぎで到着順が前後する場合に効きます。
