# フェーズ状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在のフェーズ: preparation
- 現在のタスク: TRACKER-000
- 残りフェーズ: implementation, verification, review

## フェーズ一覧

| フェーズ | 状態 | 完了条件 |
| --- | --- | --- |
| preparation | in_progress | Tracker の設計書、進捗管理ファイル、調査レポートが存在し、TDD と実装の前にユーザーへ設計承認を依頼済みである。 |
| implementation | pending | `Tracker.Core` の契約、複数 ball 対応の engine、`Tracker.Server` との統合、外出し設定、tracked viewer がテストで定義された振る舞いに従って実装される。 |
| verification | pending | 実装した v1 範囲について Tracker の build/test 証跡が存在する。 |
| review | pending | タスク単位の sub-agent レビューが記録され、致命的な指摘が残っていない。 |
