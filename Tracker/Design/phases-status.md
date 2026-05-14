# フェーズ状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在のフェーズ: verification
- 現在のタスク: RUNTIME-HOST-002
- 残りフェーズ: verification, implementation, review

## フェーズ一覧

| フェーズ | 状態 | 完了条件 |
| --- | --- | --- |
| preparation | complete | 旧 `Tracker.Core/Design` と `Tracker.Server/Design` の設計資料を確認し、`Tracker/Design/Archive/` に旧 tracking を保存した。 |
| design | complete; draft PR #17 | `Tracker/Design/` を canonical design root とし、Core / DebugHost / RuntimeHost の設計範囲をフォルダで分ける。`Tracker.RuntimeHost` を tracker / 将来 AutoRef の本番寄り headless 実行体、`Tracker.DebugHost` を Web UI / diagnostics / replay / capture viewer 用 debug host として設計し、loop isolation と旧ログ互換非要件を固定した。`reports/runtime-host-001-design-review-r2-20260514160734.md` で blocking findings なしを確認済み。 |
| verification | pending | RuntimeHost / DebugHost 境界と diagnostics sample boundary の TDD contract を追加し、tracker operation が Web UI / diagnostics logging に依存しないことを failing tests で固定する。 |
| implementation | pending | `Tracker.DebugHost` rename、`Tracker.RuntimeHost` headless scaffold、tracker operation loop 境界、diagnostics sample boundary を実装する。 |
| review | pending | focused validation、RuntimeHost / DebugHost build、diagnostics evidence、gpt-5.5 high review、tracking sync、commit / PR ready を完了する。 |
