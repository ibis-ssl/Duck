# Tracker Handover

## 目的

- `/home/ibis/ssl/IbisDuck` の `Tracker` 作業を次チャットへ安全に引き継ぐ。
- 現在の主目的は、設計契約違反として判明した Kalman 未実装を `TRACKER-023` / `TRACKER-024` で是正し、正常系が動く状態で早期リリースへ寄せること。
- このメモ作成時点では `TRACKER-023` の実装・テスト・sub-agent review は完了しているが、Codex sandbox の `.git` read-only 問題により commit は未完了。
- 次チャットでは、まずユーザー側 shell で `TRACKER-023` 対象差分を commit 済みにするか、Codex 側で `.git` writable な環境に切り替えてから、`development-orchestrator` を入口に `TRACKER-024` の verification / release 判定へ進む。

## 確定事項

- リポジトリ: `/home/ibis/ssl/IbisDuck`
- branch: `feat/tracker-004-contract-surface`
- handover 作成時刻: `2026-05-10 12:54:46 +0900`
- 最新 commit:
  - `f81db03` `docs(reports): Kalman是正前提の最新handoverを追加する`
  - `7a74e1d` `docs(reports): tracker handover memo を追加する`
  - `3e1a3ff` `docs(reports): 既存の tracker review report を追加する`
  - `4e2fccf` `feat(visionreceiver): profile-aware な受信設定切替を追加する`
- `AGENTS.md` の強い制約:
  - 実装・調査・設計はまず既存 skill に従う
  - `development-orchestrator` を入口にする
  - 作業中は関連 skill があるか常に疑う
  - 判断に迷う場合は即興で補う前に skill 側の不足を疑う
- ユーザーの継続指示:
  - 正常系が動くことを優先
  - 早期リリースを優先
  - ユーザーが止めるまで自走
  - reviewer は sub-agent、model は GPT-5.5、reasoning は high

## 現在の repository 状態

`git status --short --branch`:

```text
## feat/tracker-004-contract-surface
 M .gitignore
 M Tracker/Tracker.Core/Design/phases-status.md
 M Tracker/Tracker.Core/Design/tasks-status.md
 M Tracker/Tracker.Core/Design/tracker-architecture-plan.md
 M Tracker/Tracker.Core/TrackerExecutionContracts.cs
 M Tracker/Tracker.Server/appsettings.json
 M Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs
?? reports/task-tracker-023-evidence-20260510124030.md
?? reports/task-tracker-023-review-20260510124030.md
?? reports/task-tracker-023-review-r2-20260510124505.md
```

confirmed fact:

- `.gitignore` は user-owned 既存差分。
- `Tracker/Tracker.Server/appsettings.json` は user-owned 既存差分。
- 上記 2 ファイルは巻き戻さないこと。
- `Tracker/Tracker.Server/appsettings.json` にはユーザーが持つ `sim` profile などの差分がある。

## Git blocker

confirmed fact:

- Codex 実行環境では `.git` が read-only mount になっている。

```text
findmnt -T .git -o TARGET,OPTIONS -n
/home/ibis/ssl/IbisDuck/.git ro,nosuid,nodev,relatime
```

- `.git` と `.git/index` は書き込み不可。
- `git add` は `.git/index.lock` を作れず失敗する。

```text
git add <TRACKER-023対象ファイル>
fatal: Unable to create '/home/ibis/ssl/IbisDuck/.git/index.lock': Read-only file system
```

- `git remote -v` は空。PR 作成には remote 設定も必要。
- 参照した関連 issue:
  - `https://github.com/openai/codex/issues/15505`
  - 症状は issue の `.git is mounted read-only even though Codex is configured for workspace-write` と一致する。
  - 関連 PR `https://github.com/openai/codex/pull/17036` では将来の `allow_limited_git_writes = true` が提案されているが、現時点では安定利用前提にしない。

inference:

- 次チャットで commit まで進めるには、ユーザー側の通常 shell で commit するか、Codex CLI を `.git` writable なバージョン・設定へ切り替える必要がある。
- issue 本文上は v0.114.0 以前への downgrade が暫定回避策として挙げられていた。

## TRACKER-023 の完了内容

confirmed fact:

- `TRACKER-023` は実装・テスト・review r2 まで完了済み。
- ただし commit は未完了。

変更ファイル:

- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `reports/task-tracker-023-evidence-20260510124030.md`
- `reports/task-tracker-023-review-20260510124030.md`
- `reports/task-tracker-023-review-r2-20260510124505.md`

主な実装内容:

- `TrackerEngine` の camera-local ball / robot track 内部状態を、位置・速度・分散を持つ `KalmanAxisState` ベースへ変更。
- ball は track ごとに x/y/z の predict-update を行い、欠測時は predict のみを進める。
- robot は位置 x/y と向き theta を別 axis として predict-update し、既存の orientation unwrap を維持。
- ball の対応付け gate は、前回位置ではなく観測 timestamp へ予測した track 位置に対して判定。
- robot の gate も review 指摘後に `predictedState.XMm/YMm` と観測値の距離で判定するよう修正。
- merge weight は camera-local filter の事後 position variance 相当から導出。

追加した主な test:

- `Update_AppliesRobotKalmanMeasurementNoiseInsteadOfOverwritingObservation`
- `Update_UsesPredictedRobotPositionForGateAfterVelocityIsLearned`
- `Update_AppliesBallKalmanMeasurementNoiseInsteadOfOverwritingObservation`
- `Update_UsesConfiguredBallProcessNoiseWhenUpdatingAfterPredictionOnlyFrame`

## TRACKER-023 の検証

TDD failing proof:

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerEngineTemporalContractTests" --no-restore
Failed: 3, Passed: 47, Skipped: 0, Total: 50
```

review 指摘対応時の failing proof:

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~Update_UsesPredictedRobotPositionForGateAfterVelocityIsLearned" --no-restore
Failed: 1, Passed: 0, Skipped: 0, Total: 1
```

最終 focused:

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~Update_UsesPredictedRobotPositionForGateAfterVelocityIsLearned" --no-restore
Passed: 1, Failed: 0, Skipped: 0
```

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerEngineTemporalContractTests" --no-restore
Passed: 51, Failed: 0, Skipped: 0
```

最終 full:

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
Passed: 101, Failed: 0, Skipped: 0
```

実行できなかった検証:

```text
dotnet format Tracker/Tracker.Tests/Tracker.Tests.csproj --verify-no-changes --no-restore
```

結果:

- sandbox の named pipe 接続制限により `System.Net.Sockets.SocketException (13): Permission denied` で失敗。

## Review 状態

reviewer はユーザー指定どおり sub-agent / GPT-5.5 high。

review r1:

- report: `reports/task-tracker-023-review-20260510124030.md`
- 指摘:
  - High: robot gate が予測済み状態ではなく前回位置と観測値の距離で判定されていた。
  - 設計書は「予測状態に対する gate」を要求しており、速度学習済み robot が正常移動するケースで track reset する可能性があった。
- 対応:
  - regression test `Update_UsesPredictedRobotPositionForGateAfterVelocityIsLearned` を追加。
  - `TrackerExecutionContracts.cs` の robot gate 判定を `predictedState.XMm/YMm` 基準へ修正。

review r2:

- report: `reports/task-tracker-023-review-r2-20260510124505.md`
- 結果:
  - no findings
  - Blocking normal-path problems: なし
  - User-confirmation-required capability gaps: なし
  - Non-blocking held concerns: diagonal axis model / process noise scale は evidence に記録済みの保留リスク

## Tracking 状態

confirmed fact:

- `Tracker/Tracker.Core/Design/tasks-status.md` は現在 `TRACKER-024` を current task としている。
- `TRACKER-023` は `done`。
- `Tracker/Tracker.Core/Design/phases-status.md` は現在 phase を `verification` としている。
- `engine` phase は `done`。
- `verification` / `review` は `pending`。

現在のタスク:

- ID: `TRACKER-024`
- Title: `Kalman 標準準拠の検証と release 判定をやり直す`
- Phase: `verification`
- Status: `pending`
- Dependencies: `TRACKER-023` が完了していること
- Exit Criteria:
  - Kalman 化後の focused/full test と review report が存在する
  - 設計書の「v1 は直線運動前提の Kalman filter を標準とする」に対して未解決 blocker が残っていない

注意:

- tracking 上は `TRACKER-023 done` だが、Git commit は未完了。次チャットでは commit 未完了を blocker として扱うこと。

## 023 commit 用コマンド

ユーザー側通常 shell または `.git` writable な Codex 環境で、user-owned 差分を除外して以下を実行する。

```bash
git add \
  Tracker/Tracker.Core/Design/phases-status.md \
  Tracker/Tracker.Core/Design/tasks-status.md \
  Tracker/Tracker.Core/Design/tracker-architecture-plan.md \
  Tracker/Tracker.Core/TrackerExecutionContracts.cs \
  Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs \
  reports/task-tracker-023-evidence-20260510124030.md \
  reports/task-tracker-023-review-20260510124030.md \
  reports/task-tracker-023-review-r2-20260510124505.md

git commit -m "feat(tracker): camera-local trackingをKalman標準へ是正する"
```

commit message を skill 形式で詳しく書くなら以下:

```text
feat(tracker): camera-local trackingをKalman標準へ是正する

## 背景
- v1設計契約では camera-local ball / robot track が線形 Kalman filter 標準である必要があった
- 既存実装は観測上書きと手動 uncertainty 加算に近く、設計契約を満たしていなかった

## 変更内容
- ball / robot の camera-local track を axisごとの predict-update と covariance相当の状態へ変更した
- ball / robot の gate を予測位置基準へ寄せ、measurement/process noise が挙動に効く regression test を追加した
- TRACKER-023 の evidence と GPT-5.5 high sub-agent review report を追加した

## 検証
- dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerEngineTemporalContractTests" --no-restore
- dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
```

## まだやっていないこと

- `TRACKER-023` の commit
- remote 設定
- PR 作成
- `TRACKER-024` の verification / release 判定
- `TRACKER-024` 用の evidence / review report 作成
- `dotnet format --verify-no-changes` の成功確認

## 未解決事項

unresolved question:

- `.git` read-only 問題をどう回避するか。
  - ユーザー側通常 shell で commit する
  - Codex CLI を v0.114.0 以前へ downgrade する
  - 将来の `allow_limited_git_writes = true` 対応版を使う
  - など

unresolved question:

- remote が未設定のため、PR 作成先をどうするか。

confirmed risk:

- `TRACKER-023` の implementation は v1 用 diagonal axis model であり、full covariance matrix ではない。
- process noise は既存 contract の観測可能挙動を維持するため内部 scale を掛けて covariance に反映している。
- これらは review r2 で non-blocking held concerns として扱われた。

## 次チャットでやるべき順番

1. `development-orchestrator` を入口にする。
2. `AGENTS.md` と `/home/ibis/AI/CodexSkill` の skill freshness を確認する。
3. `.git` が writable か確認する。
4. まだ commit されていなければ、上記の `TRACKER-023` 対象ファイルだけを commit する。
5. `.gitignore` と `Tracker/Tracker.Server/appsettings.json` は user-owned 差分なので stage しない。
6. `TRACKER-024` を active task として、Kalman 標準準拠後の verification / release 判定へ進む。
7. `TRACKER-024` では focused/full test、必要なら format 再確認、sub-agent review、evidence report、tracking sync を行う。
8. commit / PR は `.git` writable と remote 設定が解決してから進める。

## 次チャットに貼る依頼文

```text
/home/ibis/ssl/IbisDuck で Tracker 作業を再開してください。AGENTS.md に従い、development-orchestrator を入口に進めてください。

現在の branch は `feat/tracker-004-contract-surface` です。

`TRACKER-023` は実装・テスト・GPT-5.5 high sub-agent review r2 まで完了していますが、Codex sandbox の `.git` read-only 問題により commit は未完了です。まず `.git` が writable か確認し、可能なら 023 対象ファイルだけを commit してください。

023 commit 対象:
- `Tracker/Tracker.Core/Design/phases-status.md`
- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
- `reports/task-tracker-023-evidence-20260510124030.md`
- `reports/task-tracker-023-review-20260510124030.md`
- `reports/task-tracker-023-review-r2-20260510124505.md`

`.gitignore` と `Tracker/Tracker.Server/appsettings.json` は user-owned 既存差分なので stage / revert しないでください。

023 commit が済んだら、現在 tracking 上の次タスク `TRACKER-024` (`Kalman 標準準拠の検証と release 判定をやり直す`) に進んでください。024 では focused/full test、必要な release 判定、sub-agent review、evidence report、tracking sync を実施してください。

参照 report:
- `reports/topic-tracker-handover-20260510125446.md`
- `reports/task-tracker-023-evidence-20260510124030.md`
- `reports/task-tracker-023-review-20260510124030.md`
- `reports/task-tracker-023-review-r2-20260510124505.md`
```
