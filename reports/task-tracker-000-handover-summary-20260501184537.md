# TRACKER-000 Handover Summary

## 対象

- Tracker 側のみ
- 対象 task: `TRACKER-000`
- 作成時刻: `2026-05-01 18:45:37 JST`

## 現在位置

- tracking 上の current task は `TRACKER-000`
- `Tracker/Tracker.Core/Design/tasks-status.md` では `Status: in_progress`
- `Tracker/Tracker.Core/Design/phases-status.md` では current phase は `preparation`
- 次フェーズ以降の task (`TRACKER-001` 以降) は未着手のまま

## 正本ファイル

- 設計書: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- task tracking: `Tracker/Tracker.Core/Design/tasks-status.md`
- phase tracking: `Tracker/Tracker.Core/Design/phases-status.md`
- 調査メモ: `reports/TRACKER-000-tigers-investigation-20260501115618.md`
- review reports:
  - `reports/task-tracker-000-design-review-20260501120622.md`
  - `reports/task-tracker-000-design-review-r2-20260501123858.md`
  - `reports/task-tracker-000-design-review-r3-20260501125035.md`
  - `reports/task-tracker-000-design-review-r4-20260501164442.md`
  - `reports/task-tracker-000-design-review-r5-20260501165300.md`
  - `reports/task-tracker-000-design-review-r6-20260501165622.md`
  - `reports/task-tracker-000-design-review-r7-20260501165941.md`
  - `reports/task-tracker-000-design-review-r8-20260501170354.md`
  - `reports/task-tracker-000-design-review-r9-20260501170835.md`
  - `reports/task-tracker-000-design-review-r11-20260501171540.md`
  - `reports/task-tracker-000-design-review-r12-20260501172011.md`
  - `reports/task-tracker-000-design-review-r13-20260501172412.md`

## この task で実施済みのこと

- `tracker-architecture-plan.md` を新規作成済み
- Tigers / official proto 調査を `TRACKER-000-tigers-investigation-20260501115618.md` に分離済み
- `tasks-status.md` と `phases-status.md` に `TRACKER-000` から `TRACKER-020` までの分割を反映済み
- profile switch 周りの設計をかなり詳細まで詰めた
  - `TrackerProfileSwitchRequest`
  - `desired target snapshot` / `pending request` / `in-flight request` / `現在適用済み snapshot`
  - control-only `Update`
  - `ProfileSwitched` / `GeometryReset` / `WorldFrameCommitted` の処理順
  - `TrackedSnapshotStore` clear と active profile / publisher 切替順
- current design diff には r13 までの指摘対応が入っている
  - 同一 `TrackerUpdateResult` 内で event local-state 適用を先に行い、その後に `CommittedFrame` / official packet を処理する規則
  - `ProfileSwitched` と `GeometryReset` が共存する場合は `EmittedEvents` 順を正とする規則

## まだ終わっていないこと

- `TRACKER-000` は tracking 上まだ完了していない
- 設計承認依頼を正式に出して、`approved` 相当の状態へ移していない
- Tracker 側では commit をまだ作っていない
- Tracker 側では PR も未作成
- final no-findings review は取っていない
  - review report は多段で存在するが、`r13` で止まっている
  - ただし current design diff には `r13` 指摘に対応する追記が入っている

## 作業木の状態

`git status --short --branch` 時点:

- branch: `docs/tracker-000-design-closeout`
- modified:
  - `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `.gitignore` ただしこれは Tracker task と直接無関係な可能性があるので commit 前に要確認
- untracked:
  - `reports/task-tracker-000-design-review-r4-20260501164442.md`
  - `reports/task-tracker-000-design-review-r5-20260501165300.md`
  - `reports/task-tracker-000-design-review-r6-20260501165622.md`
  - `reports/task-tracker-000-design-review-r7-20260501165941.md`
  - `reports/task-tracker-000-design-review-r8-20260501170354.md`
  - `reports/task-tracker-000-design-review-r9-20260501170835.md`
  - `reports/task-tracker-000-design-review-r11-20260501171540.md`
  - `reports/task-tracker-000-design-review-r12-20260501172011.md`
  - `reports/task-tracker-000-design-review-r13-20260501172412.md`

## 再開時の推奨手順

1. `tracker-architecture-plan.md` の current diff を基準に、`r13` までの review 指摘が反映されていることを前提に読み直す。
2. `.gitignore` が Tracker task と無関係なら混ぜない方針を維持する。
3. ユーザーへ `TRACKER-000` 設計承認を依頼する。
4. 承認後に tracking を同期する。
   - `tasks-status.md`: `TRACKER-000` を done に進める
   - `phases-status.md`: `preparation` 完了条件を満たした状態へ更新する
5. Tracker 側の report と設計書だけを含む commit を作る。
6. 次 task は `TRACKER-001`
   - `Tracker.Tests` から `Tracker.Core` を参照可能にし、contract test 基盤を作る

## 再開時に気を付ける点

- 以前の review 運用には細切れ再 review が多かったため、次回 review を再開するなら一度にまとめて評価する方がよい
- report は既存 format を崩さない運用へ切り替わっているが、Tracker 側の旧 review reports はその前の実行履歴を含む
- `tasks-status.md` / `phases-status.md` はまだ承認待ち前提の文言なので、設計承認なしに `TRACKER-001` へ進めない

## 即時 next action

- 実務上の次の 1 手は `TRACKER-000` の設計承認依頼
