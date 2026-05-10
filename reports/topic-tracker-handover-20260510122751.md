# Tracker Handover

## 目的

- `/home/ibis/ssl/IbisDuck` の `Tracker` 作業を次チャットへ安全に引き継ぐ。
- 現在の主目的は、`TRACKER-022` の commit 後の状態を前提に、設計契約違反として判明した Kalman 未実装を `TRACKER-023` / `TRACKER-024` で是正すること。
- 次チャットでは `development-orchestrator` を入口に、Kalman 是正の failing test 追加から再開する。

## 確定事項

- リポジトリ: `/home/ibis/ssl/IbisDuck`
- branch: `feat/tracker-004-contract-surface`
- handover 作成時刻: `2026-05-10 12:27:51 +0900`
- `AGENTS.md` の強い制約:
  - 実装・調査・設計はまず既存 skill に従う
  - `development-orchestrator` を入口にする
  - skill があるかを常に疑う
- ユーザーの継続指示:
  - 正常系が動くことを優先
  - 早期リリースを優先
  - ユーザーが止めるまで自走
  - Kalman 未実装は正常系未達かつ設計書違反として扱う

## このセッションの結論

- confirmed fact:
  - `VisionReceiver` の profile-aware 化 (`TRACKER-022`) は commit 済み。
  - legacy review report 4 件は commit 済み。
  - handover report 4 件も commit 済み。
  - 一方で、Kalman 是正のための tracking / 設計書変更は working tree に未 commit で残っている。

## 直近の commit

- `7a74e1d` `docs(reports): tracker handover memo を追加する`
- `3e1a3ff` `docs(reports): 既存の tracker review report を追加する`
- `4e2fccf` `feat(visionreceiver): profile-aware な受信設定切替を追加する`
- 参考:
  - `619dde9` `docs(tracker): TRACKER-021でTracker.ServerのREADMEを追加する`
  - `122dd15` `docs(tracker): TRACKER-020で最終reviewとtracking同期を完了する`

## `TRACKER-022` の確定状態

- confirmed fact:
  - `TRACKER-022` は commit 済みで、review/evidence も commit に含まれている。
- commit:
  - `4e2fccf`
- 含まれている主な内容:
  - `VisionReceiver` に profile 解決器を追加
  - runtime options store と profile switch observer を追加
  - tracker active profile に追従して受信 socket を再生成
  - startup active profile と UI 表示ずれを `TrackedSnapshotStore` 初期値で補正
  - `README` / `raw-vision-viewer-plan` / focused test / review / evidence を更新
- 関連 report:
  - `reports/task-tracker-022-evidence-20260510120404.md`
  - `reports/task-tracker-022-review-20260510120435.md`
  - `reports/task-tracker-022-review-r2-20260510121026.md`
- 検証実績:
  - focused: `Passed: 16 / Failed: 0 / Skipped: 0`
  - full: `Passed: 97 / Failed: 0 / Skipped: 0`

## Kalman 是正でやったこと

- tracking を更新した。
  - `Tracker/Tracker.Core/Design/tasks-status.md`
  - `Tracker/Tracker.Core/Design/phases-status.md`
- 設計書を実装前に修正した。
  - `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`

### tracking 変更内容

- `TRACKER-023` を追加:
  - `camera-local tracking を線形 Kalman filter 標準へ是正する`
- `TRACKER-024` を追加:
  - `Kalman 標準準拠の検証と release 判定をやり直す`
- `phases-status.md` を戻した:
  - `engine` を `pending`
  - `verification` を `pending`
  - `review` を `pending`
  - `残りフェーズ` を `engine, integration, verification, review`

### 設計書変更内容

`Tracker/Tracker.Core/Design/tracker-architecture-plan.md` に以下を明記済み。

- v1 の Kalman は任意ではなく標準契約
- camera-local ball / robot track は predict-update を持つ線形 Kalman filter 必須
- `ProcessNoise` / `MeasurementNoise` / `Gate` は Kalman 実装へ直接使う
- `VisibilityHalfLifeSeconds` は liveliness 管理であり Kalman 省略理由にはならない
- world merge の uncertainty は camera-local Kalman filter の事後不確かさを使う
- 単純な観測上書きや手動 uncertainty 加算だけでは v1 契約を満たさない
- robot は位置系と向き系を別 filter
- ball は track ごとの Kalman filter

## 現在の未 commit 差分

- `git status --short` 時点:
  - modified:
    - `.gitignore`
    - `Tracker/Tracker.Core/Design/phases-status.md`
    - `Tracker/Tracker.Core/Design/tasks-status.md`
    - `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
    - `Tracker/Tracker.Server/appsettings.json`

## 触ってはいけない差分

- confirmed fact:
  - `.gitignore` は user-owned 既存差分
  - `Tracker/Tracker.Server/appsettings.json` は user-owned 既存差分
  - user は `sim` profile を手で持っている
- 次チャットでもこれらを巻き戻さないこと

## 今回まだやっていないこと

- `TRACKER-023` 用の failing test は未追加
- Kalman 実装は未着手
- 設計書変更後の test は未実行
- tracking / 設計書変更はまだ commit していない

## 次チャットでやるべき順番

1. `development-orchestrator` を入口に再開する
2. `Tracker/Tracker.Core/Design/tasks-status.md` と `phases-status.md` の未 commit 変更を確認する
3. `tdd-executor` に従い、`TRACKER-023` の failing test を先に追加する
4. failing test の候補:
   - ball track が観測値を即上書きせず、predict/update 後の推定値になること
   - robot position / orientation が別 filter として更新されること
   - `ProcessNoise` / `MeasurementNoise` / `Gate` が runtime 挙動に効くこと
   - 欠測 frame で predict のみ進み、visibility 管理と covariance 更新が両立すること
   - merge に使う uncertainty が観測 confidence 逆数ではなく filter 事後不確かさ由来であること
5. failing を確認してから `Tracker/Tracker.Core/TrackerExecutionContracts.cs` を実装する
6. focused test、review、evidence を更新する
7. `TRACKER-024` の verification / release 判定へ進む
8. tracking / 設計書変更を適切な単位で commit する

## 実装候補メモ

- 主対象は `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- 最低限必要そうな分離:
  - ball 用の状態推定 helper
  - robot position 用 Kalman helper
  - robot orientation 用 Kalman helper
- `BallTrackState` / `RobotTrackState` に covariance 相当をどう持たせるかの判断が必要

## 未解決事項

- unresolved question:
  - covariance を full matrix で持つか、v1 として diagonal 近似で始めるか
- unresolved question:
  - 既存 contract test のどこまでを Kalman 専用の観測可能要件に引き上げるか

## 再開時に参照すべき主要ファイル

- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
- `reports/task-tracker-022-evidence-20260510120404.md`
- `reports/task-tracker-022-review-20260510120435.md`
- `reports/task-tracker-022-review-r2-20260510121026.md`
- `reports/topic-tracker-handover-20260510122751.md`

## 次チャットに貼る依頼文

```text
/home/ibis/ssl/IbisDuck で Tracker 作業を再開してください。AGENTS.md に従い、development-orchestrator を入口に進めてください。

`TRACKER-022` の VisionReceiver profile-aware 化は commit 済みです。最新 commit 群は `4e2fccf`、`3e1a3ff`、`7a74e1d` です。

未 commit で残っているのは、Kalman 是正のための tracking / 設計書変更と user-owned 差分です。`.gitignore` と `Tracker/Tracker.Server/appsettings.json` は user-owned なので巻き戻さないでください。

次は `TRACKER-023` の failing test 追加から始め、`Tracker/Tracker.Core/TrackerExecutionContracts.cs` に camera-local ball / robot tracking の線形 Kalman 実装を入れてください。その後 focused test、review、evidence、`TRACKER-024` の verification まで進めてください。
```
