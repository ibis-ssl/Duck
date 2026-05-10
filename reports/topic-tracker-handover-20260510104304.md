# Tracker Handover

## 目的

- `/home/ibis/ssl/IbisDuck` の `Tracker` 実装を `TRACKER-017` 以降も止まらず完了まで進める。
- 最終目標は `TRACKER-017` から `TRACKER-020` を完了し、review/evidence report と tracking 更新を commit に含めること。

## 確定事項

- リポジトリ: `/home/ibis/ssl/IbisDuck`
- 現在 branch: `feat/tracker-004-contract-surface`
- `git remote -v` は空で、PR 作成はできない。
- ユーザーの常設指示:
  - 止まれと言われるまで止まらない。
  - `Tracker/Tracker.Core/Design/tasks-status.md` と `Tracker/Tracker.Core/Design/phases-status.md` を常に最新に保つ。
  - review 結果は commit に含める。
  - 正常系で動くことを最優先にし、非 blocker は report に残す。
- tracking の現在値:
  - `Tracker/Tracker.Core/Design/tasks-status.md`: `TRACKER-017`, `ui`, `in_progress`
  - `Tracker/Tracker.Core/Design/phases-status.md`: current phase `ui`, current task `TRACKER-017`

## 完了済みタスクと commit

- `TRACKER-010`: `475b9ce`
- `TRACKER-011`: `acf9b62`
- `TRACKER-012`: `bc032a8`
- `TRACKER-013`: `74b197d`
- `TRACKER-014`: `b2e6f49`
- `TRACKER-015`: `48cf4ff`
- `TRACKER-016`: `e5f5bab`

## 直近までの実装結果

- `TRACKER-015` で tracked viewer と raw/tracked toggle を追加済み。
- `TRACKER-016` で tracked diagnostics と profile/kick/contact/field 表示を追加済み。
- `TRACKER-016` の review/evidence sub-agent は `503 Service Unavailable: No available accounts` で失敗したため、親が truthfully fallback して report に記録済み。

## 現在の作業状態

- `TRACKER-017` は test-first 着手済み。
- 未 commit の新規 test:
  - `Tracker/Tracker.Tests/TrackerProfileControlViewStateTests.cs`
- この test は未実装の `TrackerProfileControlViewState` を参照している。

## 失敗している確認コマンド

```bash
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter FullyQualifiedName~TrackerProfileControlViewStateTests
```

失敗内容:

- `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Tests/TrackerProfileControlViewStateTests.cs(27,25): error CS0103: The name 'TrackerProfileControlViewState' does not exist in the current context`
- `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Tests/TrackerProfileControlViewStateTests.cs(50,25): error CS0103: The name 'TrackerProfileControlViewState' does not exist in the current context`

## 次にやるべきこと

1. `TrackerProfileControlViewState` を `Tracker/Tracker.Server/Components/Vision/` 配下へ実装する。
2. `TrackerProfileControlViewStateTests` を通す。
3. `Home.razor` と `TrackedDetailsPanel.razor` に profile 表示と切替要求 UI を配線する。
4. `TrackerProfileRequestService.RequestProfileSwitch(...)` を UI 操作から呼ぶ。
5. `appsettings.json` に UI から切替可能な 2 つ目の profile を追加するか判断する。
6. `TRACKER-017` 完了後に review/evidence report を作り、tracking を `TRACKER-018` へ進めて commit する。

## 実装方針メモ

- 既に確認済みの関連コード:
  - `Tracker/Tracker.Server/Tracking/TrackerProfileRequestService.cs`
  - `Tracker/Tracker.Server/Program.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerOptions.cs`
  - `Tracker/Tracker.Server/appsettings.json`
  - `Tracker/Tracker.Server/Components/Vision/TrackedDetailsPanel.razor`
- 想定設計:
  - `TrackerProfileControlViewState.FromOptions(TrackerOptions, TrackedSnapshot)` を作る。
  - active profile は `TrackedSnapshot.ActiveProfileName` を優先し、未設定時は `TrackerOptions.ActiveProfileName` を使う。
  - profile 一覧は options から生成し、空の場合は active profile 1 件を返す。
  - `TrackedDetailsPanel` に `ProfileControl` と profile switch callback を渡す。

## worktree 注意点

- 触らないこと:
  - `.gitignore` の既存変更
  - 以下の untracked legacy/handover reports
    - `reports/task-tracker-001-review-20260501192139.md`
    - `reports/task-tracker-002-review-20260501192140.md`
    - `reports/task-tracker-003-review-20260501192141.md`
    - `reports/task-tracker-004-review-20260501192142.md`
    - `reports/topic-tracker-handover-20260509200949.md`
    - `reports/topic-tracker-handover-20260510073812.md`

## 推測

- `Tracker.Server/appsettings.json` は現在 profile が `default` だけなので、UI の切替操作を意味のある形で確認するには 2 つ目の profile を足す可能性が高い。

## 未解決事項

- `TRACKER-017` の review/evidence を sub-agent で取り直せるかは次回のアカウント状態次第。
- profile 切替 UI を `Home.razor` に置くか `TrackedDetailsPanel.razor` に置くかは未確定。ただし既存構造的には後者が自然。

## 次チャット依頼文

```text
/home/ibis/ssl/IbisDuck で Tracker の続きを進めてください。現在 task は TRACKER-017 in_progress、phase は ui です。Tracker/Tracker.Core/Design/tasks-status.md と phases-status.md を常に最新に保ち、review/evidence report を commit に含めたまま、止まれと言われるまで自走してください。まず Tracker/Tracker.Tests/TrackerProfileControlViewStateTests.cs を通すために TrackerProfileControlViewState を実装し、その後 runtime profile 表示・切替 UI を配線して TRACKER-017 を commit まで進めてください。`.gitignore` と legacy/handover report 群には触らないでください。
```
