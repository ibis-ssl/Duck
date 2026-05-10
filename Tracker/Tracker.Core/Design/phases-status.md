# フェーズ状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在のフェーズ: verification
- 現在のタスク: TRACKER-024
- 残りフェーズ: verification, review

## フェーズ一覧

| フェーズ | 状態 | 完了条件 |
| --- | --- | --- |
| preparation | done | Tracker の設計書、調査レポート、設計レビュー報告、task/phase 管理が揃い、ユーザー承認の上で設計を完了した。 |
| contracts | done | `TRACKER-001` から `TRACKER-005` が完了し、`Tracker.Core` の内部モデル、`TrackerUpdateResult`、packet generator、observer/event 契約、およびそれらを固定する failing/passing test が揃う。 |
| engine | done | `TRACKER-006` から `TRACKER-011` に加え、`TRACKER-023` が完了し、camera-local robot/ball tracking が設計どおり線形 Kalman filter を標準として実装される。 |
| integration | done | `Tracker.Server` から engine、snapshot store、observer、official tracker packet 配信、設定束縛、profile 切替要求経路までが接続され、profile-aware な `VisionReceiver` 設定が反映される。 |
| ui | done | `TRACKER-015` から `TRACKER-017` が完了し、tracked viewer、raw/tracked 切替、tracked diagnostics 表示、runtime profile 切替要求 UI が用意される。 |
| verification | pending | `TRACKER-018` と `TRACKER-019` に加え、`TRACKER-024` が完了し、Kalman 標準準拠後の build/test/review 証跡が reports に存在する。 |
| review | pending | `TRACKER-020` に加え、Kalman 標準準拠後の review 結果が記録され、致命的な指摘が残っていない。 |
| documentation | done | `TRACKER-021` が完了し、`Tracker.Server` の README に起動手順、画面の使い方、主要設定値の意味が記録されている。 |
