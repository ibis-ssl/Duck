# フェーズ状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在のフェーズ: implementation
- 現在のタスク: RUNTIME-HOST-008
- 残りフェーズ: implementation, review

## フェーズ一覧

| フェーズ | 状態 | 完了条件 |
| --- | --- | --- |
| preparation | complete | 旧 `Tracker.Core/Design` と `Tracker.Server/Design` の設計資料を確認し、`Tracker/Design/Archive/` に旧 tracking を保存した。 |
| design | complete; draft PR #17 | `Tracker/Design/` を canonical design root とし、Core / DebugHost / RuntimeHost の設計範囲をフォルダで分ける。`Tracker.RuntimeHost` を tracker / 将来 AutoRef の本番寄り headless 実行体、`Tracker.DebugHost` を Web UI / diagnostics / replay / capture viewer 用 debug host として設計し、loop isolation と旧ログ互換非要件を固定した。`reports/runtime-host-001-design-review-r2-20260514160734.md` で blocking findings なしを確認済み。 |
| verification | complete; draft PR #17 | `RUNTIME-HOST-002` と `RUNTIME-HOST-003` で RuntimeHost / DebugHost dependency boundary、read-side responsibility、diagnostics sample boundary、legacy degraded contract の Red tests を追加し、task ごとの review で blocking findings なしを確認した。RUNTIME-HOST-002 は r2 review、RUNTIME-HOST-003 は `reports/runtime-host-003-review-20260514170652.md` で完了した。 |
| implementation | in-progress | `RUNTIME-HOST-004` から `RUNTIME-HOST-009` で DebugHost rename、共有 operation loop boundary、DebugHost read-side 化、diagnostics sample sidecar fast path、RuntimeHost scaffold、RuntimeHost normal path を focused tests / build / task review 付きで green にする。RUNTIME-HOST-004 は `reports/runtime-host-004-review-20260514172921.md`、RUNTIME-HOST-005 は `reports/runtime-host-005-review-20260514180308.md`、RUNTIME-HOST-006 は `reports/runtime-host-006-review-20260514182549.md` で blocking findings なしを確認済み。RUNTIME-HOST-007 は diagnostics sample sidecar fast path、UI 非依存 sample loop、設定値 `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds`、Diagnostics UI sample summary path を実装し、`reports/runtime-host-007-review-r4-20260514192425.md` で no findings を確認済み。現在は RUNTIME-HOST-008 へ進み、`RuntimeHost:OperationLoopIntervalMilliseconds` で RuntimeHost 実行周期を設定化し、0 以下を起動時 validation error として固定する。 |
| review | pending | `RUNTIME-HOST-010` と `RUNTIME-HOST-011` で focused validation、RuntimeHost / DebugHost build、diagnostics evidence、legacy degraded evidence、gpt-5.5 high review、必要な r2 review、tracking sync、commit / PR ready を完了する。 |
