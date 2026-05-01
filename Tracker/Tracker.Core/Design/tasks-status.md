# タスク状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 現在のタスク

- ID: TRACKER-000
- Title: Tracker の設計書と進捗管理ファイルを作成する
- Phase: preparation
- Status: in_progress
- Size: medium
- Dependencies: Tracker の事前調査が完了していること
- Exit Criteria:
  - `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` が存在する。
  - `reports/TRACKER-000-tigers-investigation-*.md` として調査結果が別ファイルに存在する。
  - `Tracker.Core` の責務、proto 入力、official 出力、内部モデル、アルゴリズム方針、設定方針、テスト方針が明記される。
  - `TRACKER-001` 以降の作業分割が `tasks-status.md` と `phases-status.md` に反映される。
  - ユーザーへ設計承認を依頼し、承認待ち状態に移れる。

## タスク一覧

| ID | タスク | フェーズ | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| TRACKER-000 | Tracker の設計書と進捗管理ファイルを作成する | preparation | in_progress | Tracker の事前調査が完了していること | 設計用ディレクトリ、計画書、タスク管理、フェーズ管理、調査レポートが存在し、ユーザーへ承認依頼できる。 |
| TRACKER-001 | `Tracker.Core` の契約テストを追加する | implementation | pending | TRACKER-000 approved | パケット生成、複数 ball の並び順、フレーム生成、設定注入、速度推定の振る舞いを定義する失敗テストまたは新規必須テストが存在する。 |
| TRACKER-002 | `Tracker.Core` の内部モデル、設定契約、パケット生成器を実装する | implementation | pending | TRACKER-001 | 内部フレームと状態型、設定型が存在し、パケット生成がテストを通過し、複数 ball が primary 先頭で出力され、proto の単位変換が正しい。 |
| TRACKER-003 | `TrackerEngine` v1 を実装する | implementation | pending | TRACKER-001 | 複数 ball、追跡済み robot、kick 検出の基礎、基本速度推定が raw vision 入力から決定的に生成される。 |
| TRACKER-004 | `Tracker.Core` を `Tracker.Server` の実行経路へ統合し、設定を外出しする | implementation | pending | TRACKER-002, TRACKER-003 | raw vision 入力が tracker へ流れ、最新の tracked snapshot が保持され、official tracker packet を配信でき、tracker と network の設定が外部設定から束縛される。 |
| TRACKER-005 | tracked viewer、raw/tracked 切替、実行時設定変更の下地を追加する | implementation | pending | TRACKER-004 | UI で raw と tracked を切り替えられ、主要な tracked 診断情報を表示でき、将来の動的調整へ接続できる実行時設定経路がある。 |
| TRACKER-006 | Tracker v1 の検証とレビューを行う | verification | pending | TRACKER-005 | build/test の証跡が記録され、sub-agent によるレビューが記録され、致命的な指摘が残っていない。 |
