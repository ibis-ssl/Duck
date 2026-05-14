# タスク状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 現在のタスク

- ID: RUNTIME-HOST-002
- Title: RuntimeHost / DebugHost project dependency boundary contract を追加する
- Phase: verification
- Status: pending
- Size: small
- Dependencies: RUNTIME-HOST-001.
- Exit Criteria:
  - `Tracker.RuntimeHost` が `Tracker.DebugHost` / `Tracker.Server` / Web UI / diagnostics replay UI project を参照しないことを project reference / dependency test で固定する。
  - RuntimeHost 側 code が diagnostics logging / replay / Blazor UI namespace を直接呼ばないことを contract test で固定する。
  - DebugHost が tracker operation loop を主実行責務として持たず、latest immutable snapshot または published output を読む側であることを test 名と assertion で固定する。
  - Red test evidence、task 専用 review、commit、Draft PR #17 update が揃う。

## 完了済みタスク

- `RUNTIME-HOST-001`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了した。`Tracker/Design/` へ設計資料と active tracking を統合し、RuntimeHost / DebugHost の命名、責務境界、AutoRef 将来内包、loop isolation、旧ログ互換非要件、BreakingChanges 不要を設計へ固定した。gpt-5.5 high review は初回 blocking 2 件を修正し、r2 で no findings を確認した。Draft PR #17 を作成した。
  - Review Evidence:
    - `reports/runtime-host-001-design-review-20260514155548.md`
    - `reports/runtime-host-001-design-fix-20260514160144.md`
    - `reports/runtime-host-001-design-review-r2-20260514160734.md`

## 固定残タスク

- 固定一覧は `RUNTIME-HOST-001` から `RUNTIME-HOST-011` とする。RuntimeHost / DebugHost 分離 scope では `RAW-VISION-*` や `TRACKER-*` を追加しない。
- `RUNTIME-HOST-001`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了する。設計資料を `Tracker/Design/` 配下へ移動し、active tracking を統合し、RuntimeHost / DebugHost の責務境界、AutoRef 将来内包、loop isolation、旧ログ互換非要件を設計へ反映する。
- `RUNTIME-HOST-002`: RuntimeHost / DebugHost project dependency boundary contract を追加する。RuntimeHost が DebugHost / Web UI / diagnostics replay UI に依存しないこと、DebugHost が tracker operation loop の主責務を持たず read-side であることを failing tests として固定する。
- `RUNTIME-HOST-003`: diagnostics sample boundary と legacy degraded contract を追加する。diagnostics sample tick が tracker committed frame cadence に依存しないこと、Diagnostics `Vision Input` が diagnostics sample sidecar から復元されること、旧 render snapshot sidecar が unsupported / degraded legacy であることを failing tests として固定する。
- `RUNTIME-HOST-004`: `Tracker.Server` を `Tracker.DebugHost` project / namespace / 起動経路へ rename する。現 `Tracker.Server` の Web UI / diagnostics / replay / capture viewer 責務を DebugHost として明確化し、既存 debug normal path を壊さない。
- `RUNTIME-HOST-005`: tracker operation loop の共有 runtime boundary を抽出する。SSL-Vision input、tracker update、official tracker packet publish、latest tracker snapshot 公開の境界を UI / diagnostics logging から分離し、RuntimeHost から再利用できる形にする。
- `RUNTIME-HOST-006`: DebugHost live display を read-side snapshot 境界へ寄せる。UI render tick ごとに raw / tracked / 3rd party tracker の latest immutable snapshot を固定し、Web rendering tick が tracker operation loop を駆動しない構造にする。
- `RUNTIME-HOST-007`: DebugHost diagnostics sample sidecar fast path を実装する。diagnostics sample tick で latest raw snapshot と latest tracker snapshot を固定して保存し、新規 capture / logging の bounded lookup を主経路にする。
- `RUNTIME-HOST-008`: `Tracker.RuntimeHost` headless project scaffold と configuration を追加する。Web UI / diagnostics replay / capture viewer を持たない headless host として起動できる project / Program / options / DI bootstrap / solution entry を追加する。
- `RUNTIME-HOST-009`: RuntimeHost tracker operation loop と official packet publish normal path を実装する。SSL-Vision input、tracker state update、official tracker packet publish、DebugHost が読める latest tracker snapshot 公開を headless host の正常系として成立させる。
- `RUNTIME-HOST-010`: RuntimeHost / DebugHost split の focused validation と manual evidence を揃える。RuntimeHost / DebugHost build、focused tests、diagnostics sample evidence、legacy degraded evidence、DebugHost UI normal path、RuntimeHost headless normal path の証跡を report に残す。
- `RUNTIME-HOST-011`: RuntimeHost / DebugHost split の final review / tracking sync / PR ready を完了する。gpt-5.5 high review、必要な修正と r2、tracking sync、report references、validation evidence、Draft PR #17 ready 化を完了する。

## 統合済み履歴

- Core / tracker engine 系の旧 tracking は `Tracker/Design/Archive/Core/tasks-status.md` と `Tracker/Design/Archive/Core/phases-status.md` に保存する。
- DebugHost / raw vision / diagnostics 系の旧 tracking は `Tracker/Design/Archive/DebugHost/tasks-status.md` と `Tracker/Design/Archive/DebugHost/phases-status.md` に保存する。
- 旧 `RAW-VISION-013` から `RAW-VISION-016` は PR #15 `Issue #10 Vision画面に分割表示とオーバーレイを追加する` として `2026-05-14T03:29:25Z` に merge 済み。
- `RAW-VISION-017` として開始した loop isolation 設計は、RuntimeHost / DebugHost 分離方針へ scope を拡張したため、以後は `RUNTIME-HOST-001` へ統合する。

## タスク一覧

| ID | タスク | フェーズ | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| RUNTIME-HOST-001 | `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了する | design | complete; draft PR #17 | PR #15 merge complete | `Tracker/Design/` へ設計資料と active tracking を統合し、RuntimeHost / DebugHost の命名、責務境界、AutoRef 将来内包、loop isolation、旧ログ互換非要件、BreakingChanges 不要を設計へ固定し、gpt-5.5 high r2 review で blocking findings なしを確認した。 |
| RUNTIME-HOST-002 | RuntimeHost / DebugHost project dependency boundary contract を追加する | verification | pending | RUNTIME-HOST-001 | RuntimeHost が DebugHost / Web UI / diagnostics replay UI に依存しないこと、DebugHost が tracker operation loop の主責務を持たず read-side であることを Red test として固定し、review / commit / Draft PR #17 update まで完了する。 |
| RUNTIME-HOST-003 | diagnostics sample boundary と legacy degraded contract を追加する | verification | pending | RUNTIME-HOST-002 | diagnostics sample tick が tracker committed frame cadence / `WorldFrameCommitted` に依存しないこと、Diagnostics `Vision Input` が diagnostics sample sidecar から復元されること、旧 render snapshot sidecar が unsupported / degraded legacy であることを Red test として固定し、review / commit / Draft PR #17 update まで完了する。 |
| RUNTIME-HOST-004 | `Tracker.Server` を `Tracker.DebugHost` project / namespace / 起動経路へ rename する | implementation | pending | RUNTIME-HOST-003 | 現 `Tracker.Server` の Web UI / diagnostics / replay / capture viewer 責務を `Tracker.DebugHost` として明確化し、既存 debug normal path、README、launch settings、solution / project reference を維持し、review / commit / Draft PR #17 update まで完了する。 |
| RUNTIME-HOST-005 | tracker operation loop の共有 runtime boundary を抽出する | implementation | pending | RUNTIME-HOST-004 | SSL-Vision input、tracker update、official tracker packet publish、latest tracker snapshot 公開の境界を UI / diagnostics logging から分離し、RuntimeHost から再利用できる UI 非依存 shared boundary と focused tests、review / commit / Draft PR #17 update を揃える。 |
| RUNTIME-HOST-006 | DebugHost live display を read-side snapshot 境界へ寄せる | implementation | pending | RUNTIME-HOST-005 | DebugHost live display が UI render tick ごとに latest immutable snapshot を固定し、Web rendering tick が tracker operation loop を駆動しないことを focused tests / build で確認し、review / commit / Draft PR #17 update まで完了する。 |
| RUNTIME-HOST-007 | DebugHost diagnostics sample sidecar fast path を実装する | implementation | pending | RUNTIME-HOST-003, RUNTIME-HOST-006 | diagnostics sample tick で latest raw snapshot と latest tracker snapshot を固定して diagnostics sample sidecar に保存し、新規 capture / logging の bounded lookup を主経路にして RUNTIME-HOST-003 の Red tests を green にし、review / commit / Draft PR #17 update まで完了する。 |
| RUNTIME-HOST-008 | `Tracker.RuntimeHost` headless project scaffold と configuration を追加する | implementation | pending | RUNTIME-HOST-005 | Web UI / diagnostics replay / capture viewer を持たない `Tracker.RuntimeHost` project、Program / options / DI bootstrap / solution entry を追加し、tracker only と将来 tracker + AutoRef mode の境界を表現し、build / focused tests / review / commit / Draft PR #17 update を揃える。 |
| RUNTIME-HOST-009 | RuntimeHost tracker operation loop と official packet publish normal path を実装する | implementation | pending | RUNTIME-HOST-007, RUNTIME-HOST-008 | RuntimeHost が SSL-Vision input を受け、tracker state を更新し、official tracker packet を publish し、DebugHost が読める latest tracker snapshot を公開する正常系を focused tests / build / review / commit / Draft PR #17 update 付きで成立させる。 |
| RUNTIME-HOST-010 | RuntimeHost / DebugHost split の focused validation と manual evidence を揃える | review | pending | RUNTIME-HOST-009 | RuntimeHost / DebugHost の focused tests と build、diagnostics sample evidence、legacy degraded evidence、DebugHost UI normal path、RuntimeHost headless normal path の証跡を report に残し、review / commit / Draft PR #17 update まで完了する。 |
| RUNTIME-HOST-011 | RuntimeHost / DebugHost split の final review / tracking sync / PR ready を完了する | review | pending | RUNTIME-HOST-010 | gpt-5.5 high review、必要な修正と r2、tracking sync、report references、validation evidence、commit 履歴、Draft PR #17 description を最新化し、PR ready 判断を完了する。 |
