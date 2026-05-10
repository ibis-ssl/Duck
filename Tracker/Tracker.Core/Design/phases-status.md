# フェーズ状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在のフェーズ: engine
- 現在のタスク: TRACKER-030
- 残りフェーズ: ui, verification, review

## フェーズ一覧

| フェーズ | 状態 | 完了条件 |
| --- | --- | --- |
| preparation | done | Tracker の設計書、調査レポート、設計レビュー報告、task/phase 管理が揃い、ユーザー承認の上で設計を完了した。 |
| contracts | done | `TRACKER-001` から `TRACKER-005` が完了し、`Tracker.Core` の内部モデル、`TrackerUpdateResult`、packet generator、observer/event 契約、およびそれらを固定する failing/passing test が揃う。 |
| engine | done | `TRACKER-006` から `TRACKER-011` に加え、`TRACKER-023`、`TRACKER-025`、`TRACKER-027`、`TRACKER-028`、`TRACKER-029` が完了し、camera-local robot/ball tracking が設計どおり線形 Kalman filter を標準として実装され、低 visibility の stale object と Tigers 由来の近接重複 robot / 短命 ball ghost / stale secondary ball が tracked frame へ出続けない。stationary に近い tracked object の小刻みな振動は抑制され、振動抑制 tuning 値は profile 設定から外部調整できる。 |
| integration | done | `Tracker.Server` から engine、snapshot store、observer、official tracker packet 配信、設定束縛、profile 切替要求経路までが接続され、profile-aware な `VisionReceiver` 設定が反映される。 |
| ui | pending | `TRACKER-015` から `TRACKER-017` が完了し、tracked viewer、raw/tracked 切替、tracked diagnostics 表示、runtime profile 切替要求 UI が用意される。`TRACKER-030` で tracked field 表示を raw Vision field geometry と揃える。 |
| verification | pending | `TRACKER-018` と `TRACKER-019` に加え、`TRACKER-024` が完了し、Kalman 標準準拠後および stale object 抑制後の build/test/review 証跡が reports に存在する。`TRACKER-028` の指定 capture replay 証跡が `reports/tracker-028-evidence-20260510215726.md` に記録されている。`TRACKER-029` の振動抑制検証が追加で記録される。 |
| review | pending | `TRACKER-020` に加え、Kalman 標準準拠後および stale object 抑制後の review 結果が記録され、致命的な指摘が残っていない。`TRACKER-028` の review 結果が `reports/tracker-028-review-20260510215726.md` に記録され、blocking finding が残っていない。`TRACKER-029` の review 結果が追加で記録される。 |
| documentation | done | `TRACKER-021` が完了し、`Tracker.Server` の README に起動手順、画面の使い方、主要設定値の意味が記録されている。 |
| investigation | done | `TRACKER-026` が完了し、raw SSL-Vision detection と tracked 出力を同じログで比較できる。 |
