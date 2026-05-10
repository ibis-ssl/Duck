# フェーズ状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在のフェーズ: review
- 現在のタスク: TRACKER-020
- 残りフェーズ: なし

## フェーズ一覧

| フェーズ | 状態 | 完了条件 |
| --- | --- | --- |
| preparation | done | Tracker の設計書、調査レポート、設計レビュー報告、task/phase 管理が揃い、ユーザー承認の上で設計を完了した。 |
| contracts | done | `TRACKER-001` から `TRACKER-005` が完了し、`Tracker.Core` の内部モデル、`TrackerUpdateResult`、packet generator、observer/event 契約、およびそれらを固定する failing/passing test が揃う。 |
| engine | done | `TRACKER-006` から `TRACKER-011` が完了し、reorder/reset/profile switch を含む `TrackerEngine` 本体、robot/ball tracking、merge、kick/contact/field metadata が決定的に実装される。 |
| integration | done | `TRACKER-012` から `TRACKER-014` が完了し、`Tracker.Server` から engine、snapshot store、observer、official tracker packet 配信、設定束縛、profile 切替要求経路までが接続される。 |
| ui | done | `TRACKER-015` から `TRACKER-017` が完了し、tracked viewer、raw/tracked 切替、tracked diagnostics 表示、runtime profile 切替要求 UI が用意される。 |
| verification | done | `TRACKER-018` と `TRACKER-019` が完了し、実装した v1 範囲について build/test と integration 観点の証跡が reports に存在する。 |
| review | done | `TRACKER-020` が完了し、最終 sub-agent レビューが記録され、tracking files が実状態へ同期され、致命的な指摘が残っていない。 |
