# TRACKER-061 進捗同期

## 概要

- 対象: TRACKER-061 diagnostics playback UI で `等倍速` と `4x` / `16x` / `64x` を分離する
- 同期理由: 実装、review follow-up、r2 review が完了し、blocking findings が残っていないため
- 同期担当: parent / progress-sync-manager

## 同期内容

- `Tracker/Tracker.Core/Design/tasks-status.md`
  - 現在のタスクを `none` に更新した。
  - 次の調査タスクを `なし` に更新した。
  - `TRACKER-061` の固定残タスク記述とタスク一覧を `done` へ更新した。
  - `等倍速`、`4x`、`16x`、`64x` の独立した playback choices、`停止` 表記、数値の等倍表記不使用、旧 speed select 不使用を記録した。
- `Tracker/Tracker.Core/Design/phases-status.md`
  - 現在のフェーズ / タスクを `none` に更新した。
  - `comparison-logging` を `done` に更新した。
  - `TRACKER-061` 完了により、CaptureOn 比較ログ拡張の固定残タスクがすべて完了したことを記録した。

## 根拠

- 設計: `reports/tracker-061-playback-ui-separation-design-20260513204405.md`
- 実装: `reports/tracker-061-playback-ui-separation-implementation-20260513205042.md`
- 初回 review: `reports/tracker-061-review-20260513205647.md`
- review-fix: `reports/tracker-061-review-fix-implementation-20260513210059.md`
- r2 review: `reports/tracker-061-review-r2-20260513210407.md`

## 検証

- `DiagnosticsPlaybackStateTests`: 23 passed
- related validation: 51 passed
- `git diff --check`: pass
- full `Tracker.Tests`: 240 passed / 1 failed
  - 失敗は今回 commit 外のローカル `Tracker/Tracker.Server/appsettings.json` 差分 (`Tracker:Receive:Enabled=true`) による `TrackerConfigurationBindingTests.AppsettingsJson_ExposesTigersAlignedTrackerDefaults` の default-off contract failure。

## 残リスク

- `Tracker/Tracker.Server/appsettings.json` はユーザー操作に由来する既存 dirty として変更・revert・stage しない。
- Blazor component interaction test は未追加。source text 検査寄りの held concern は r2 review で blocking ではないと裁定済み。
