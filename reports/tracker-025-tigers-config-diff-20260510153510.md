# TRACKER-025 Tigers 設定差分調査

## 目的

Tracked 表示に、存在しない 11 番ロボットや過剰な ball が残る事象について、`Tracker/Tracker.Core/Design/Ref/AutoReferee` の Tigers 実装を基準に、IbisDuck Tracker の初期設定差分と必要な写像を記録する。

## 現時点の差分

| 項目 | Tigers 実装 | IbisDuck 現状 | 影響 |
| --- | --- | --- | --- |
| ball 不可視 lifetime | `CamFilter.invisibleLifetimeBall = 1.0s` | `TrackLifetimeNs = 10000000000` で 10.0s | 欠測 ball track が長く残り、Tracked 上の ball が増えやすい。 |
| robot 不可視 lifetime | `CamFilter.invisibleLifetimeRobot = 2.0s` | visibility が `<= 0.01` になるまで残る。half-life 1.0s では約 6.6s | 一度だけ出た誤検出 robot id が数秒間 Tracked に出続ける。 |
| robot 出力品質 gate | `RobotQualityInspector.robotQualityThreshold = 0.05` | 明示的な output visibility/quality threshold なし | 内部 stale track と外部出力の切り分けが弱い。 |
| robot 品質評価 horizon | `RobotQualityInspector.trackingTimeHorizon = 20.0s` | exponential visibility half-life のみ | Tigers は長期の検出頻度で外出し品質を判定する。IbisDuck は簡易 threshold へ写像する必要がある。 |
| robot Kalman noise | `modelErrorXY = 0.1`, `measErrorXY = 20.0` | `ProcessNoise = 50.0`, `MeasurementNoise = 1.0` | 現状は Tigers と比べて process/measurement の意味とスケールが一致していない。初期値を Tigers 寄せに見直す必要がある。 |
| ball Kalman noise | `modelError = 0.1`, `measError = 100.0` | `ProcessNoise = 50.0`, `MeasurementNoise = 1.0` | ball も Tigers と初期値が大きく異なる。 |
| ball tracker maturity | `BallTracker.grownUpAge = 3` | 未実装 | Tigers は十分育った tracker を primary merge 対象にする。今回の最小修正では output threshold/lifetime を優先する。 |
| ball tracker 最大数 | `CamFilter.maxBallTrackers = 10` | 明示制限なし | 誤 ball が多い場合に内部 track 数が増えやすい可能性がある。今回の修正候補に含めるかは実装影響を確認する。 |

## 追加調査で更新した差分状態

TRACKER-025 反映後も Tracked 表示で過剰な object が残るため、現時点では次の切り分けが未完了。

| 観点 | Tigers 実装 | IbisDuck 現状 | 次の確認 |
| --- | --- | --- | --- |
| raw detection の混入 | `CamFilter` が camera viewport / field rect / 近接 robot / outlier で新規 tracker 生成を抑制する | raw viewer / tracked viewer のどちらで過剰 object が発生しているか未確定 | `Tracker diagnostics` log で raw ball / robot と tracked ball / robot を同一行に出し、raw 入力時点の誤検出か tracker 出力側の stale かを判定する。 |
| robot quality 判定 | `RobotQualityInspector` が 20 秒 horizon の検出頻度で `robotQualityThreshold = 0.05` を超えた robot だけを出す | `OutputVisibilityThreshold = 0.05` は単発 track の exponential visibility に対する簡易 gate | 11 番 robot が raw に継続的に出ている場合、Tigers 相当の検出頻度 quality gate が別途必要。 |
| ball primary 候補 | `BallTracker.grownUpAge = 3`、直前 ball 近傍探索、camera ごと代表 1 件で primary merge | `TrackLifetimeNs = 1s` と output threshold はあるが、grown-up / primary 探索半径 / camera 代表選択は未実装 | raw ball が複数継続して入る場合、lifetime ではなく Tigers の grown-up / primary selector 差分が原因候補。 |
| ball tracker 数制限 | `CamFilter.maxBallTrackers = 10` | 明示制限なし | raw ball が多い環境で内部 track が増え続けるかを diagnostics log で確認する。 |

## 追加した調査ログ

`TrackerCoordinator` に `Tracker diagnostics` log を追加する。

- 出力頻度:
  - 通常は最大 1 秒に 1 回
  - raw ball または tracked ball が複数ある場合は追加で出力
- 出力先:
  - 標準の `ILogger` 出力
  - publish / 実行時の working directory に起動ごとに新規作成される `tracker-diagnostics-<timestamp>-<id>.log`
- 出力内容:
  - active tracker profile
  - raw frame / camera id
  - raw ball count/detail
  - raw blue/yellow robot id/detail
  - tracked frame
  - tracked ball count/detail
  - tracked robot count/detail
  - robot / ball の output visibility threshold、half-life、ball lifetime
- 期待する切り分け:
  - raw 側に B11/Y11 や複数 ball が出ているなら SSL-Vision 入力または raw filtering 差分を疑う
  - raw 側に出ていないのに tracked 側だけ残るなら tracker stale / merge / output gate 差分を疑う
- build 確認:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore`
  - 0 warnings / 0 errors
- ファイル出力追加後の build 確認:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore`
  - 0 warnings / 0 errors
- 起動ごとに新規ファイル化した後の build 確認:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore`
  - 0 warnings / 0 errors

## 今回反映した写像

| IbisDuck 設定 | 反映値 | Tigers 根拠 | 備考 |
| --- | --- | --- | --- |
| `RobotTracker.ProcessNoise` | `0.1` | `RobotTracker.modelErrorXY = 0.1` | 位置系 filter の model error に寄せる。 |
| `RobotTracker.MeasurementNoise` | `20.0` | `RobotTracker.measErrorXY = 20.0` | 位置系 filter の measurement error に寄せる。 |
| `RobotTracker.OutputVisibilityThreshold` | `0.05` | `RobotQualityInspector.robotQualityThreshold = 0.05` | Tracked / official 出力前の visibility gate として写像。 |
| `RobotTracker.VisibilityHalfLifeSeconds` | `0.462756` | `CamFilter.invisibleLifetimeRobot = 2.0s` と quality threshold `0.05` | 初期 visibility `1.0` が約 2 秒で `0.05` へ落ちる half-life として算出。 |
| `BallTracker.ProcessNoise` | `0.1` | `BallTracker.modelError = 0.1` | ball filter の model error に寄せる。 |
| `BallTracker.MeasurementNoise` | `100.0` | `BallTracker.measError = 100.0` | ball filter の measurement error に寄せる。 |
| `BallTracker.TrackLifetimeNs` | `1000000000` | `CamFilter.invisibleLifetimeBall = 1.0s` | 欠測 ball track を Tigers と同程度で破棄する。 |
| `BallTracker.OutputVisibilityThreshold` | `0.0` | 直接対応なし | ball は lifetime で抑制し、追加 gate は設定面だけ用意する。 |

## TDD 証跡

- 追加した red test:
  - `Update_DoesNotEmitRobotTrackAfterOutputVisibilityFallsBelowThreshold`
  - `Update_DoesNotEmitBallTrackAfterOutputVisibilityFallsBelowThreshold`
- 実装前の失敗:
  - `TrackerRobotTrackerOverrides` / `TrackerBallTrackerOverrides` に `OutputVisibilityThreshold` が存在せず compile error。
- 実装後の確認:
  - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerEngineTemporalContractTests|FullyQualifiedName~TrackerConfigurationBindingTests|FullyQualifiedName~TrackerCoreContractSurfaceTests" --no-restore`
    - 59 tests passed。
  - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore`
    - 103 tests passed。
  - review follow-up 後:
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerConfigurationBindingTests" --no-restore`
      - 4 tests passed。
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore`
      - 104 tests passed。

## 現時点の方針

- `appsettings.json` の tracker algorithm 初期値は Tigers に寄せる。
- ball は `TrackLifetimeNs = 1000000000` を基準値にする。
- robot は直接 lifetime 設定がないため、外部出力 threshold を追加し、visibility が十分落ちた stale track を `TrackerFrame` へ出さない。
- 既存契約として、1 frame 程度の短期欠測は引き続き出力する。

## 参照した Tigers 実装

- `modules/moduli-vision/src/main/java/edu/tigers/sumatra/vision/CamFilter.java`
- `modules/moduli-vision/src/main/java/edu/tigers/sumatra/vision/RobotQualityInspector.java`
- `modules/moduli-vision/src/main/java/edu/tigers/sumatra/vision/tracker/RobotTracker.java`
- `modules/moduli-vision/src/main/java/edu/tigers/sumatra/vision/tracker/BallTracker.java`
