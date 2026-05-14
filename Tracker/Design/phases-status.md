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
| verification | pending | `RUNTIME-HOST-002` と `RUNTIME-HOST-003` で RuntimeHost / DebugHost dependency boundary、read-side responsibility、diagnostics sample boundary、legacy degraded contract の Red tests を追加し、task ごとの review / commit / Draft PR #17 update を完了する。 |
| implementation | pending | `RUNTIME-HOST-004` から `RUNTIME-HOST-009` で DebugHost rename、共有 operation loop boundary、DebugHost read-side 化、diagnostics sample sidecar fast path、RuntimeHost scaffold、RuntimeHost normal path を focused tests / build / task review 付きで green にする。 |
| review | pending | `RUNTIME-HOST-010` と `RUNTIME-HOST-011` で focused validation、RuntimeHost / DebugHost build、diagnostics evidence、legacy degraded evidence、gpt-5.5 high review、必要な r2 review、tracking sync、commit / PR ready を完了する。 |
