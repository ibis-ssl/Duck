# Tracker Handover

## 目的

- `/home/ibis/ssl/IbisDuck` の `Tracker` 実装を次チャットへ安全に引き継ぐ。
- 現在の主目的は、`VisionReceiver` の profile-aware 化 (`TRACKER-022`) を保持したまま、設計契約違反として判明した Kalman 未実装を是正すること。
- 次チャットでは、設計更新済みの契約に従って `TRACKER-023` の failing test 追加から再開する。

## 確定事項

- リポジトリ: `/home/ibis/ssl/IbisDuck`
- branch: `feat/tracker-004-contract-surface`
- 現在日時: `2026-05-10 12:22:50 +0900`
- `AGENTS.md` の強い制約:
  - 実装・調査・設計はまず既存 skill に従う
  - `development-orchestrator` を入口にする
  - skill があるかを常に疑う
- ユーザーの直近の明示指示:
  - 「正常系が動くことを優先。早期リリースを優先。ユーザーが止めるまで自走することを優先」
  - ただし今回、Kalman 未実装は「正常系ではない」「設計書違反」と明示された
  - 「実装前に設計書の修正を行ってください」
  - 「その後 VisionReceiver の変更は commit してください」

## 今回の判断

- confirmed fact:
  - 現行コードは完全な Kalman filter ではなく、camera-local track が「等速外挿 + 観測上書き + uncertainty 手計算」に近い実装だった。
  - `tracker-architecture-plan.md` には以前から「filter 実装は差し替え可能にするが、v1 は直線運動前提の Kalman filter を標準とする」と書かれていた。
  - ユーザーはこの不一致を設計違反として扱うよう求めた。
- confirmed fact:
  - このため、既存の `TRACKER-008/009` を黙って reinterpret せず、是正 task を追加する方針にした。
- inference:
  - 過去に Kalman が抜けた理由は、正常系優先で observable behavior を先に通したためと思われるが、これは推測であり正式決定記録ではない。

## 今回やった変更

- tracking を更新した。
  - `Tracker/Tracker.Core/Design/tasks-status.md`
  - `Tracker/Tracker.Core/Design/phases-status.md`
- 設計書を実装前に修正した。
  - `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`

### tracking 変更

- `TRACKER-023` を追加:
  - `camera-local tracking を線形 Kalman filter 標準へ是正する`
- `TRACKER-024` を追加:
  - `Kalman 標準準拠の検証と release 判定をやり直す`
- `phases-status.md` の状態を戻した:
  - `engine` を `pending`
  - `verification` を `pending`
  - `review` を `pending`
  - `残りフェーズ` を `engine, integration, verification, review`

### 設計書変更

`Tracker/Tracker.Core/Design/tracker-architecture-plan.md` に以下を明記した。

- v1 の Kalman は任意ではなく標準契約である
- camera-local ball / robot track は predict-update を持つ線形 Kalman filter でなければならない
- `ProcessNoise` / `MeasurementNoise` / `Gate` は Kalman 実装へ直接使う
- `VisibilityHalfLifeSeconds` は liveliness 管理であって、Kalman 更新省略の理由にはならない
- world merge の uncertainty は camera-local Kalman filter の事後不確かさを使う
- 単純な観測上書きや手動 uncertainty 加算だけでは v1 契約を満たさない
- robot は位置系 `x, y, vx, vy` と向き系 `theta, omega` を別 filter とする
- ball は track ごとの Kalman filter を持つ

## `TRACKER-022` の現状

- confirmed fact:
  - `VisionReceiver` の profile-aware 化そのものは実装済みで、review r2 まで pass している。
  - ただし commit / tracking sync は未完了。
- 変更済みファイル:
  - `Tracker/Tracker.Server/Vision/VisionReceiverOptions.cs`
  - `Tracker/Tracker.Server/Vision/VisionReceiverService.cs`
  - `Tracker/Tracker.Server/Vision/VisionReceiverConfigurationResolver.cs`
  - `Tracker/Tracker.Server/Vision/VisionReceiverRuntimeOptionsStore.cs`
  - `Tracker/Tracker.Server/Vision/VisionReceiverProfileSwitchObserver.cs`
  - `Tracker/Tracker.Server/Program.cs`
  - `Tracker/Tracker.Server/Tracking/TrackedSnapshotStore.cs`
  - `Tracker/Tracker.Tests/VisionReceiverConfigurationResolverTests.cs`
  - `Tracker/Tracker.Tests/TrackerProfileControlViewStateTests.cs`
  - `Tracker/Tracker.Server/README.md`
  - `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
- review / evidence:
  - `reports/task-tracker-022-evidence-20260510120404.md`
  - `reports/task-tracker-022-review-20260510120435.md`
  - `reports/task-tracker-022-review-r2-20260510121026.md`
- focused / full test 実績:
  - focused: `Passed: 16 / Failed: 0 / Skipped: 0`
  - full: `Passed: 97 / Failed: 0 / Skipped: 0`

## 現在の未 commit 差分

- `git status --short` 時点:
  - modified:
    - `.gitignore`
    - `Tracker/Tracker.Core/Design/phases-status.md`
    - `Tracker/Tracker.Core/Design/tasks-status.md`
    - `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
    - `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
    - `Tracker/Tracker.Server/Program.cs`
    - `Tracker/Tracker.Server/README.md`
    - `Tracker/Tracker.Server/Tracking/TrackedSnapshotStore.cs`
    - `Tracker/Tracker.Server/Vision/VisionReceiverOptions.cs`
    - `Tracker/Tracker.Server/Vision/VisionReceiverService.cs`
    - `Tracker/Tracker.Server/appsettings.json`
    - `Tracker/Tracker.Tests/TrackerProfileControlViewStateTests.cs`
  - untracked:
    - `Tracker/Tracker.Server/Vision/VisionReceiverConfigurationResolver.cs`
    - `Tracker/Tracker.Server/Vision/VisionReceiverProfileSwitchObserver.cs`
    - `Tracker/Tracker.Server/Vision/VisionReceiverRuntimeOptionsStore.cs`
    - `Tracker/Tracker.Tests/VisionReceiverConfigurationResolverTests.cs`
    - `reports/task-tracker-022-evidence-20260510120404.md`
    - `reports/task-tracker-022-review-20260510120435.md`
    - `reports/task-tracker-022-review-r2-20260510121026.md`
    - 既存 legacy / handover reports

## 触ってはいけない差分

- confirmed fact:
  - `.gitignore` は user-owned 既存差分
  - `Tracker/Tracker.Server/appsettings.json` は user-owned 既存差分
  - user は `sim` profile を手で持っている
- したがって、次チャットでもこれらを巻き戻さないこと

## 今回の設計変更後にまだやっていないこと

- `TRACKER-023` 用の failing test はまだ追加していない
- Kalman 実装はまだ開始していない
- 設計書変更後の test はまだ回していない
- `TRACKER-022` はコード上 pass 状態で、ユーザー指示としてこの変更は後続作業のあと commit する

## 次チャットでやるべき順番

1. `development-orchestrator` を入口に再開する
2. `TRACKER-022` の途中状態と `TRACKER-023/024` 追加済み tracking を確認する
3. `tdd-executor` に従い、Kalman 是正を要求する failing test を先に追加する
4. 追加候補:
   - ball track が観測値を即上書きせず、predict/update 後の推定値になること
   - robot position / orientation が別 filter として更新されること
   - `ProcessNoise` / `MeasurementNoise` / `Gate` が runtime 挙動に効くこと
   - 欠測 frame で predict のみ進み、visibility 管理と covariance 更新が両立すること
   - merge に使う uncertainty が観測 confidence 逆数ではなく filter 事後不確かさ由来であること
5. failing を確認してから `Tracker/Tracker.Core/TrackerExecutionContracts.cs` を実装する
6. `TRACKER-022` と `TRACKER-023` の整合を見ながら focused test を回す
7. review と evidence を更新する
8. `VisionReceiver` の変更 (`TRACKER-022` 差分) を commit する
9. `TRACKER-024` の verification / release 判定へ進む

## 実装候補メモ

- 最初の実装対象として自然なのは `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- ここに最低でも以下の分離が必要そう:
  - ball 用の状態推定 struct / helper
  - robot position 用 Kalman helper
  - robot orientation 用 Kalman helper
- 既存の `BallTrackState` / `RobotTrackState` に covariance 相当をどう持たせるかの設計判断が必要
- design では「covariance 相当の不確かさを保持」としたので、完全行列を持つか、最小限の対角近似で始めるかは未決

## 未解決事項

- unresolved question:
  - covariance を full matrix で持つか、v1 として diagonal 近似で始めるか
- unresolved question:
  - 既存 contract test のどこまでを「Kalman 専用の観測可能要件」に引き上げるか

## 再開時に参照すべき主要ファイル

- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
- `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
- `Tracker/Tracker.Server/README.md`
- `reports/task-tracker-022-evidence-20260510120404.md`
- `reports/task-tracker-022-review-20260510120435.md`
- `reports/task-tracker-022-review-r2-20260510121026.md`
- `reports/topic-tracker-handover-20260510104304.md`

## 次チャットに貼る依頼文

以下をそのまま次チャットの先頭に貼れば再開できる。

```text
/home/ibis/ssl/IbisDuck で Tracker 作業を再開してください。AGENTS.md に従い、development-orchestrator を入口に進めてください。

直近で `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` を修正し、v1 の camera-local robot/ball tracking は線形 Kalman filter を標準とすること、単純な観測上書きでは契約違反であることを明記済みです。tracking には `TRACKER-023` (Kalman 是正) と `TRACKER-024` (再検証) を追加済みです。

まず `TRACKER-022` の未 commit 差分を壊さずに保持したまま、`TRACKER-023` の failing test 追加から始めてください。その後 `Tracker/Tracker.Core/TrackerExecutionContracts.cs` に Kalman 実装を入れ、focused test、review、evidence まで自走してください。ユーザー指示として、その後 `VisionReceiver` の変更は commit してください。

注意:
- `.gitignore` と `Tracker/Tracker.Server/appsettings.json` は user-owned 差分なので巻き戻さないこと
- 参照レポートは `reports/task-tracker-022-evidence-20260510120404.md`、`reports/task-tracker-022-review-20260510120435.md`、`reports/task-tracker-022-review-r2-20260510121026.md`、`reports/topic-tracker-handover-20260510122250.md`
```
