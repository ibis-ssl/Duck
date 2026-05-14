# タスク状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 現在のタスク

- ID: RUNTIME-HOST-002
- Title: RuntimeHost / DebugHost 境界と diagnostics sample boundary の TDD contract を追加する
- Phase: verification
- Status: pending
- Size: medium
- Dependencies: RUNTIME-HOST-001.
- Exit Criteria:
  - RuntimeHost が Web UI / diagnostics logging に依存しないことを failing test で固定する。
  - DebugHost が published output / latest snapshot を読む側であることを failing test で固定する。
  - diagnostics sample tick が tracker committed frame cadence に依存しないことを failing test で固定する。
  - 旧 render snapshot sidecar 互換を主経路へ昇格させず、最新 capture / 最新 logging 経路の性能を優先する contract を固定する。

## 完了済みタスク

- `RUNTIME-HOST-001`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了した。`Tracker/Design/` へ設計資料と active tracking を統合し、RuntimeHost / DebugHost の命名、責務境界、AutoRef 将来内包、loop isolation、旧ログ互換非要件、BreakingChanges 不要を設計へ固定した。gpt-5.5 high review は初回 blocking 2 件を修正し、r2 で no findings を確認した。Draft PR #17 を作成した。
  - Review Evidence:
    - `reports/runtime-host-001-design-review-20260514155548.md`
    - `reports/runtime-host-001-design-fix-20260514160144.md`
    - `reports/runtime-host-001-design-review-r2-20260514160734.md`

## 固定残タスク

- 固定一覧は `RUNTIME-HOST-001`、`RUNTIME-HOST-002`、`RUNTIME-HOST-003`、`RUNTIME-HOST-004`、`RUNTIME-HOST-005` とする。RuntimeHost / DebugHost 分離 scope では `RAW-VISION-*` や `TRACKER-*` を追加しない。
- `RUNTIME-HOST-001`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了する。設計資料を `Tracker/Design/` 配下へ移動し、active tracking を統合し、RuntimeHost / DebugHost の責務境界、AutoRef 将来内包、loop isolation、旧ログ互換非要件を設計へ反映する。
- `RUNTIME-HOST-002`: RuntimeHost / DebugHost 境界と diagnostics sample boundary の TDD contract を追加する。RuntimeHost が Web UI / diagnostics logging に依存しないこと、DebugHost が latest snapshot / published output を読む側であること、diagnostics sample tick が tracker committed frame cadence に依存しないことを failing tests として固定する。
- `RUNTIME-HOST-003`: `Tracker.DebugHost` への project / namespace / documentation rename と debug host 起動経路を実装する。現 `Tracker.Server` の Web UI / diagnostics / replay 責務を DebugHost として明確化し、既存 debug normal path を壊さない。
- `RUNTIME-HOST-004`: `Tracker.RuntimeHost` の headless scaffold と tracker operation loop 境界を実装する。SSL-Vision 入力、tracker update、tracker packet publish の本番寄り実行体を Web UI なしで起動できる形にし、将来 AutoRef mode を同一 process に入れられる構成にする。
- `RUNTIME-HOST-005`: RuntimeHost / DebugHost 分離の validation、review、progress sync、PR ready を完了する。focused tests、`Tracker.RuntimeHost` / `Tracker.DebugHost` build、diagnostics sample evidence、gpt-5.5 high review、tracking sync、commit / PR ready を揃える。

## 統合済み履歴

- Core / tracker engine 系の旧 tracking は `Tracker/Design/Archive/Core/tasks-status.md` と `Tracker/Design/Archive/Core/phases-status.md` に保存する。
- DebugHost / raw vision / diagnostics 系の旧 tracking は `Tracker/Design/Archive/DebugHost/tasks-status.md` と `Tracker/Design/Archive/DebugHost/phases-status.md` に保存する。
- 旧 `RAW-VISION-013` から `RAW-VISION-016` は PR #15 `Issue #10 Vision画面に分割表示とオーバーレイを追加する` として `2026-05-14T03:29:25Z` に merge 済み。
- `RAW-VISION-017` として開始した loop isolation 設計は、RuntimeHost / DebugHost 分離方針へ scope を拡張したため、以後は `RUNTIME-HOST-001` へ統合する。

## タスク一覧

| ID | タスク | フェーズ | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| RUNTIME-HOST-001 | `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了する | design | complete; draft PR #17 | PR #15 merge complete | `Tracker/Design/` へ設計資料と active tracking を統合し、RuntimeHost / DebugHost の命名、責務境界、AutoRef 将来内包、loop isolation、旧ログ互換非要件、BreakingChanges 不要を設計へ固定し、gpt-5.5 high r2 review で blocking findings なしを確認した。 |
| RUNTIME-HOST-002 | RuntimeHost / DebugHost 境界と diagnostics sample boundary の TDD contract を追加する | verification | pending | RUNTIME-HOST-001 | RuntimeHost が Web UI / diagnostics logging に依存しないこと、DebugHost が published output / latest snapshot を読む側であること、diagnostics sample tick が tracker committed frame cadence に依存しないことを failing tests として固定する。 |
| RUNTIME-HOST-003 | `Tracker.DebugHost` rename と debug host 起動経路を実装する | implementation | pending | RUNTIME-HOST-002 | 現 `Tracker.Server` の Web UI / diagnostics / replay 責務を `Tracker.DebugHost` として明確化し、既存 debug normal path と build を維持する。 |
| RUNTIME-HOST-004 | `Tracker.RuntimeHost` headless scaffold と tracker operation loop 境界を実装する | implementation | pending | RUNTIME-HOST-003 | Web UI なしで tracker operation loop を起動でき、SSL-Vision 入力、tracker update、tracker packet publish を担う RuntimeHost scaffold を追加する。AutoRef 実装は入れないが、将来 mode として同一 process に入れられる境界を残す。 |
| RUNTIME-HOST-005 | RuntimeHost / DebugHost 分離の validation / review / PR ready を完了する | review | pending | RUNTIME-HOST-004 | focused tests、RuntimeHost / DebugHost build、diagnostics sample evidence、gpt-5.5 high review、tracking sync、commit / PR ready を揃える。 |
