# フェーズ状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在のフェーズ: maintenance
- 現在のタスク: TRACKER-034
- 残りフェーズ: maintenance, verification, review

## フェーズ一覧

| フェーズ | 状態 | 完了条件 |
| --- | --- | --- |
| preparation | done | Tracker の設計書、調査レポート、設計レビュー報告、task/phase 管理が揃い、ユーザー承認の上で設計を完了した。 |
| contracts | done | `TRACKER-001` から `TRACKER-005` が完了し、`Tracker.Core` の内部モデル、`TrackerUpdateResult`、packet generator、observer/event 契約、およびそれらを固定する failing/passing test が揃う。 |
| engine | done | `TRACKER-006` から `TRACKER-011` に加え、`TRACKER-023`、`TRACKER-025`、`TRACKER-027`、`TRACKER-028`、`TRACKER-029`、`TRACKER-031` が完了した。camera-local robot/ball tracking が設計どおり線形 Kalman filter を標準として実装され、低 visibility の stale object と Tigers 由来の近接重複 robot / 短命 ball ghost / stale secondary ball が tracked frame へ出続けない。stationary に近い tracked object の小刻みな振動は抑制され、camera 間の同一 robot ID 遠方 outlier は正常な別 camera 観測がある場合に tracked merge へ混ざらない。 |
| integration | done | `Tracker.Server` から engine、snapshot store、observer、official tracker packet 配信、設定束縛、profile 切替要求経路までが接続され、profile-aware な `VisionReceiver` 設定が反映される。 |
| ui | done | `TRACKER-015` から `TRACKER-017` に加え、`TRACKER-030` が完了し、tracked viewer、raw/tracked 切替、tracked diagnostics 表示、runtime profile 切替要求 UI、raw Vision field geometry と揃った tracked field 表示が用意される。 |
| verification | pending | `TRACKER-018` と `TRACKER-019` に加え、`TRACKER-024` が完了し、Kalman 標準準拠後および stale object 抑制後の build/test/review 証跡が reports に存在する。`TRACKER-028` の指定 capture replay 証跡が `reports/tracker-028-evidence-20260510215726.md`、`TRACKER-029` の振動抑制検証が `reports/tracker-029-evidence-20260510221200.md`、`TRACKER-030` の field geometry 表示検証が `reports/tracker-030-evidence-20260510222529.md`、`TRACKER-031` の瞬間移動抑制検証が `reports/tracker-031-evidence-20260510223916.md` に記録済み。`TRACKER-036` で保守性改善後の full test と必要な focused test を追加記録する。 |
| review | pending | `TRACKER-020` に加え、Kalman 標準準拠後および stale object 抑制後の review 結果が記録され、致命的な指摘が残っていない。`TRACKER-028`、`TRACKER-029`、`TRACKER-030`、`TRACKER-031` の review 結果は reports に記録済み。`TRACKER-032` 以降は task ごとに review report を作成し、blocking finding を残さない。 |
| documentation | done | `TRACKER-021` が完了し、`Tracker.Server` の README に起動手順、画面の使い方、主要設定値の意味が記録されている。 |
| investigation | done | `TRACKER-026` が完了し、raw SSL-Vision detection と tracked 出力を同じログで比較できる。 |
| maintenance | in_progress | `TRACKER-032` から `TRACKER-035` で詳細設計書の分割、巨大ソースファイルの責務別分割、主要 class / property / method の日本語コメント追加、test の確認内容コメント追加を行う。親 Codex は manager として作業を管理し、実装・設計書作成・test 編集は worker sub-agent に委譲する。 |
