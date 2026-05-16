# フェーズ状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在のフェーズ: PR
- 現在のタスク: CAPTURE-REPLAY-001 / RUNTIME-HOST-012 / DOC-LINT-001
- 残りフェーズ: docs-tooling PR / PR review / merge

## フェーズ一覧

| フェーズ | 状態 | 完了条件 |
| --- | --- | --- |
| preparation | complete | 旧 `Tracker.Core/Design` と `Tracker.Server/Design` の設計資料を確認し、`Tracker/Design/Archive/` に旧 tracking を保存した。 |
| design | complete; draft PR #17 | `Tracker/Design/` を canonical design root とし、Core / DebugHost / RuntimeHost の設計範囲をフォルダで分ける。`Tracker.RuntimeHost` を tracker / 将来 AutoRef の本番寄り headless 実行体、`Tracker.DebugHost` を Web UI / diagnostics / replay / capture viewer 用 debug host として設計し、loop isolation と旧ログ互換非要件を固定した。`reports/runtime-host-001-design-review-r2-20260514160734.md` で blocking findings なしを確認済み。 |
| verification | complete; draft PR #17 | `RUNTIME-HOST-002` と `RUNTIME-HOST-003` で RuntimeHost / DebugHost dependency boundary、read-side responsibility、diagnostics sample boundary、legacy degraded contract の Red tests を追加し、task ごとの review で blocking findings なしを確認した。RUNTIME-HOST-002 は r2 review、RUNTIME-HOST-003 は `reports/runtime-host-003-review-20260514170652.md` で完了した。 |
| implementation | complete; draft PR #17 | `RUNTIME-HOST-004` から `RUNTIME-HOST-009` で DebugHost rename、共有 operation loop boundary、DebugHost read-side 化、diagnostics sample sidecar fast path、RuntimeHost scaffold、RuntimeHost normal path を focused tests / build / task review 付きで green にした。RUNTIME-HOST-004 は `reports/runtime-host-004-review-20260514172921.md`、RUNTIME-HOST-005 は `reports/runtime-host-005-review-20260514180308.md`、RUNTIME-HOST-006 は `reports/runtime-host-006-review-20260514182549.md`、RUNTIME-HOST-007 は `reports/runtime-host-007-review-r4-20260514192425.md`、RUNTIME-HOST-008 は `reports/runtime-host-008-review-r2-20260514194042.md`、RUNTIME-HOST-009 は `reports/runtime-host-009-review-r2-20260514200945.md` で no findings を確認済み。 |
| review | complete; PR #17 ready | `RUNTIME-HOST-010` で RuntimeHost / DebugHost build、diagnostics sample evidence、legacy degraded evidence、DebugHost UI normal path、RuntimeHost headless normal path の validation evidence と task review を完了した。`RUNTIME-HOST-011` では final review の blocking finding を受けて checked-in failing contract を現設計へ修正し、`reports/runtime-host-011-final-review-r2-20260514204526.md` で blocking findings なし / PR ready 可を確認した。 |
| capture-replay-investigation | PR #19 open | `CAPTURE-REPLAY-001` で `Tracker.CaptureReplay` に raw vision / ibis tracker の cadence と `ReceivedAt` ベース lag を比較する汎用出力を追加し、指定 capture session の遅延原因を `reports/capture-replay-001-latency-investigation-20260516185833.md` に記録した。focused tests 11 passed、`Tracker.CaptureReplay` build は成功。dedicated review は `reports/pr19-review-capturereplay-20260516200807.md` で blocking findings なし、docs/tracking review は `reports/pr19-review-docs-tracking-20260516200807.md` で blocking findings なし。 |
| runtime-host-cli-profile | PR #19 open | `RUNTIME-HOST-012` で `Tracker.RuntimeHost` 起動時に `--profile <name>` / `--profile=<name>` から active profile を指定できるようにした。command-line parsing は `Microsoft.Extensions.Configuration.CommandLine` provider と switch mapping を使う。review finding 修正後 focused tests 17 passed、`Tracker.RuntimeHost` build は成功。初回 review の High finding は修正し、`reports/pr19-review-runtimehost-profile-r2-20260516201757.md` で指摘なしを確認した。 |
| docs-tooling | review complete; PR pending | `DOC-LINT-001` で repository root に Markdown 向け `textlint` / `cspell` を導入し、ユーザー編集対象の `*.md` 全般を品質ゲートに載せる。英単語・カタカナ語 whitelist は既存 Markdown 脚注から初版を収集した専用 YAML 1 ファイルを source of truth とし、単語名と説明の対を保持する。環境構築メモ、`npm` script、除外対象、sub-agent validation、dedicated review を `reports/doc-lint-001-review-20260516233048.md` で closed にした。Full-scope lint は未登録語を大量 whitelist で自動許可しないため意図的に failed の状態。 |
