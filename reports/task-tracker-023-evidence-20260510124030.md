# TRACKER-023 evidence

## タスク

- `TRACKER-023`: camera-local tracking を線形 Kalman filter 標準へ是正する

## 実装内容

- `TrackerEngine` の camera-local ball / robot track 内部状態を、位置・速度・分散を持つ `KalmanAxisState` ベースへ変更した。
- ball は track ごとに x/y/z の predict-update を行い、欠測時は predict のみを進める。
- robot は位置 x/y と向き theta を別 axis として predict-update し、既存の orientation unwrap を維持した。
- ball の対応付け gate は、前回位置ではなく観測 timestamp へ予測した track 位置に対して判定する。
- merge weight は camera-local filter の事後 position variance 相当から導く。

## 追加・更新したテスト

- `Update_AppliesRobotKalmanMeasurementNoiseInsteadOfOverwritingObservation`
- `Update_UsesPredictedRobotPositionForGateAfterVelocityIsLearned`
- `Update_AppliesBallKalmanMeasurementNoiseInsteadOfOverwritingObservation`
- `Update_UsesConfiguredBallProcessNoiseWhenUpdatingAfterPredictionOnlyFrame`

## TDD failing proof

実装前に focused test を実行し、追加テストが失敗することを確認した。

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerEngineTemporalContractTests" --no-restore
Failed: 3, Passed: 47, Skipped: 0, Total: 50
```

review 指摘対応時に、robot gate が予測位置を使わない不具合の regression test も実装前に失敗することを確認した。

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~Update_UsesPredictedRobotPositionForGateAfterVelocityIsLearned" --no-restore
Failed: 1, Passed: 0, Skipped: 0, Total: 1
```

## 検証結果

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerEngineTemporalContractTests" --no-restore
Passed: 51, Failed: 0, Skipped: 0
```

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
Passed: 101, Failed: 0, Skipped: 0
```

review 指摘対応の focused regression:

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~Update_UsesPredictedRobotPositionForGateAfterVelocityIsLearned" --no-restore
Passed: 1, Failed: 0, Skipped: 0
```

## 実行できなかった検証

```text
dotnet format Tracker/Tracker.Tests/Tracker.Tests.csproj --verify-no-changes --no-restore
```

結果: sandbox の named pipe 接続制限により `System.Net.Sockets.SocketException (13): Permission denied` で失敗した。

## Git submit 状況

`git add` 実行時に `.git/index.lock` を作成できず、commit 前の staging が失敗した。

```text
git add <TRACKER-023対象ファイル>
fatal: Unable to create '/home/ibis/ssl/IbisDuck/.git/index.lock': Read-only file system
```

作業ツリー上の実装・テスト・レポート更新は完了しているが、この環境では `.git` への書き込みが拒否されているため、commit / PR 作成は未完了。

## 残リスク

- Kalman 実装は v1 用の diagonal axis model であり、full covariance matrix ではない。
- process noise は既存 contract の観測可能挙動を維持するため内部 scale を掛けて covariance に反映している。
