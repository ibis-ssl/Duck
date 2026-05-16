# `Tracker` 設定

この文書は、`Tracker.RuntimeHost` と `Tracker.DebugHost` が共有する `Tracker` 設定を説明します。実行体固有の受信、画面、保存、診断設定はそれぞれの文書を参照してください。

- [`Tracker.RuntimeHost` の文書](Tracker.RuntimeHost/README.md)
- [`Tracker.DebugHost` の文書](Tracker.DebugHost/README.md)

## `Tracker`

追跡器全体の動作を決める設定です。

| キー | 意味 |
| --- | --- |
| `Enabled` | `true` のとき、受信した SSL-Vision 通信内容を追跡処理に渡します。`false` のときは実行体側の受信や表示だけを行い、追跡結果[^追跡結果] は更新しません。 |
| `PublishUdp` | `true` のとき、確定した追跡結果から公式追跡出力を UDP 送信します。`false` のときも追跡計算は続けますが、UDP 送信は行いません。 |
| `SourceName` | 公式追跡出力へ入れる送信元名です。 |
| `Uuid` | 公式追跡出力へ入れる UUID です。受信側の送信元識別に使われます。 |
| `ActiveProfileName` | 起動時に使う設定名です。`Tracker:Profiles` に同名の定義が必要です。 |
| `Profiles` | 設定名ごとの送信先、追跡処理、機体、球、蹴り出し判定設定です。 |

`Tracker.DebugHost` には `Tracker:Receive`、`Tracker:Diagnostics`、`Tracker:RuntimeOverrides` もあります。これらは `Tracker.DebugHost` 固有の比較記録と診断用設定なので、詳細は [`Tracker.DebugHost` の文書](Tracker.DebugHost/README.md) を参照してください。

## `Tracker:Profiles:<name>`

`default`、`sim`、`fast` のように複数の設定名を置けます。設定名を切り替えると、送信先だけではなく追跡処理、機体追跡、球追跡、蹴り出し検出の設定もまとめて切り替わります。

`Tracker.RuntimeHost` は起動時だけ設定名を決定します。`--profile <name>` を指定した場合はその値を使い、指定しない場合は `Tracker:ActiveProfileName` を使います。

`Tracker.DebugHost` は起動時の `Tracker:ActiveProfileName` に加えて、画面または HTTP API から実行中に設定名を切り替えられます。

### `Publish`

公式追跡出力の送信先です。

| キー | 意味 |
| --- | --- |
| `MulticastAddress` | 公式追跡出力の送信先番地です。多地点配信と単一先配信のどちらも指定できます。 |
| `Port` | 公式追跡出力の送信先 UDP 口番号です。 |

### `Engine`

時系列処理と追跡処理の基本設定です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `ReorderWindowNs` | ns | 並べ替え猶予[^reorder-window]の長さです。値を大きくすると、到着順と通信内容内時刻順がずれた通信内容を吸収しやすくなりますが、追跡結果の確定はその分遅くなります。`0` は遅延通信内容を待たず、到着済みの入力だけで即時確定する指定です。 |
| `MergeWindowNs` | ns | 近い時刻の検出結果を同じ統合結果にまとめる時間幅です。値を大きくすると撮影元間の統合はしやすくなりますが、別結果の検出結果まで混ざりやすくなります。 |
| `GeometryResetFieldLengthThresholdMm` | mm | 競技場長の変化を形状初期化とみなす閾値です。 |
| `GeometryResetFieldWidthThresholdMm` | mm | 競技場幅の変化を形状初期化とみなす閾値です。 |
| `KalmanInitialVelocityVariance` | 任意係数 | 新規追跡対象の初期速度の不確かさです。大きいほど初期の観測揺れを速度として取り込みやすくなります。 |
| `KalmanProcessNoiseScale` | 任意係数 | `ProcessNoise` を推定の分散へ変換する係数です。大きいほど急な動きへ追従しやすく、停止時の揺れは増えやすくなります。 |
| `MeasurementNoiseVarianceScale` | 任意係数 | `MeasurementNoise` を観測分散へ変換する係数です。大きいほど未加工の検出結果の小刻みな揺れを弱く信用します。 |

形状初期化が起きると、旧競技場形状前提の未確定結果は破棄されます。

### `RobotTracker`

機体追跡の調整値です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `ProcessNoise` | 任意係数 | 予測側の変化量をどれだけ許すかです。大きいほど素早い動きに追従しやすく、安定性は下がります。 |
| `MeasurementNoise` | 任意係数 | 観測値の雑音をどれだけ見込むかです。大きいほど観測を弱く信用します。 |
| `VisibilityHalfLifeSeconds` | s | 観測が来ない追跡対象の可視度をどの速度で減衰させるかです。 |
| `OutputVisibilityThreshold` | 任意係数 | 出力に含める可視度の下限です。 |
| `Gate` | 任意係数 | 既存追跡対象と新しい観測を同一対象とみなす近傍判定の厳しさです。小さいほど厳しくなります。 |
| `OutlierLimitMm` | mm | 外れ値として弾く距離上限です。 |
| `IdentitySwitchDistanceMm` | mm | 既存の別 ID 追跡対象近傍へ突然現れた機体 ID 変更候補を抑制する距離です。`0` で無効化できます。 |
| `OrientationMeasurementNoiseRad` | rad | 機体向き観測の雑音想定です。大きいほど向き観測を弱く信用します。 |
| `OrientationProcessNoise` | 任意係数 | 機体向き推定器の予測側変化量をどれだけ許すかです。 |
| `InitialAngularVelocityVariance` | 任意係数 | 新規機体追跡対象の初期角速度の不確かさです。 |
| `AngularVelocityLimitRadPerS` | rad/s | 機体角速度推定の上限です。 |

### `BallTracker`

球追跡の調整値です。意味は機体追跡とほぼ同じですが、球固有に `TrackLifetimeNs` を持ちます。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `ProcessNoise` | 任意係数 | 球運動予測の変化量をどれだけ許すかです。 |
| `MeasurementNoise` | 任意係数 | 球観測値の雑音想定です。 |
| `VisibilityHalfLifeSeconds` | s | 観測が消えた球追跡対象の可視度をどの速度で減衰させるかです。 |
| `OutputVisibilityThreshold` | 任意係数 | 出力に含める可視度の下限です。 |
| `Gate` | 任意係数 | 既存球追跡対象と観測を結び付ける近傍判定の厳しさです。 |
| `OutlierLimitMm` | mm | 外れ値として弾く距離上限です。 |
| `TrackLifetimeNs` | ns | 観測消失後も追跡対象を保持する最長時間です。 |

### `KickDetector`

蹴り出し、浮き球、接触周辺の判定設定です。

| キー | 単位 | 意味 |
| --- | --- | --- |
| `KickSpeedThresholdMmPerS` | mm/s | この速度以上を蹴り出し検出候補とみなします。 |
| `ChipHeightThresholdMm` | mm | 球高さがこの値を超えると浮き球系挙動の判定に使われます。 |
| `ContactMarginMm` | mm | 機体と球の接触とみなす距離余白です。 |

## 典型的な変更例

### 公式追跡出力の送信を止める

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

### `sim` 設定名の並べ替え猶予を 10 ms にする

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

- `Tracker:ActiveProfileName` と切替先設定名は、必ず `Tracker:Profiles` に定義してください。
- `ReorderWindowNs` は追跡結果の確定時刻に直接影響します。未加工入力に対して追跡器が遅れて見える場合は、まず実際に使われている実行体、設定文書、起動引数の `--profile` を確認してください。
- `Tracker.RuntimeHost/appsettings.json` の `sim` は `ReorderWindowNs=10000000` です。`Tracker.DebugHost/appsettings.json` の `sim` はこの文書作成時点では `ReorderWindowNs=100000000` です。

## 脚注

[^追跡結果]: 追跡結果は、未加工の検出結果を追跡器が統合して出力する 1 結果分の追跡結果です。
[^reorder-window]: 並べ替え猶予は、通信内容内時刻が少し古い入力の到着を待つ猶予時間です。複数撮影元やネットワーク揺らぎで到着順が前後する場合に効きます。
