# フェーズ状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在のフェーズ: comparison-logging
- 現在のタスク: TRACKER-041
- 残りフェーズ: none

## フェーズ一覧

| フェーズ | 状態 | 完了条件 |
| --- | --- | --- |
| past-tracker-history | done | `TRACKER-000` から `TRACKER-038` までの完了済みタスクと旧フェーズ詳細は `Tracker/Tracker.Core/Design/tracker-history-000-038.md` に退避済み。tracking 軽量化と履歴退避は PR #9 準備の保守性/運用作業として完了済みで、CaptureOn 比較ログの機能仕様には含めない。 |
| investigation | done | 直近履歴として `TRACKER-039` は PR #8 `https://github.com/ibis-ssl/Duck/pull/8` で `2026-05-12T00:06:33Z` に merge 済み。証跡は `reports/tracker-039-evidence-20260512084929.md`、review は `reports/tracker-039-review-20260512085258.md` と `reports/tracker-039-review-r2-20260512090207.md` に記録済み。 |
| comparison-logging | in_progress | `TRACKER-040` は CaptureOn 比較ログ拡張の設計・tracking・draft PR #9 `https://github.com/ibis-ssl/Duck/pull/9` 更新・gpt-5.5 high review まで完了済みで、blocking findings はない。追加指摘により、機能設計文書は CaptureOn 比較ログの最新仕様だけに整理し、旧巨大ファイル分割や tracking 軽量化は保守性/運用文脈へ分離済み。同一 CaptureOn session で生成される packet capture、metadata、tracker diagnostics、render snapshots、3rdparty tracker comparison sidecar JSONL は一つの session folder 配下へまとめ、異なる CaptureOn タイミングのログは別 folder に分ける方針を追加済み。設計分離・session folder 修正後の r2 review も `reports/tracker-040-design-review-r2-20260512102542.md` に記録済みで、blocking findings はない。PR #9 の機能設計と保守性設計の分離、および CaptureOn session folder 構造を含む設計・tracking 差分は 2026-05-12 にユーザー承認済み。現在の `TRACKER-041` は TDD failing test と production 実装が完了し、`MultiTrackerManager<TrackerPacketAdapter>` の self除外、remote endpoint / receivedAt 保持、`uuid` / `sourceName` / remote endpoint 単位の最新状態保持の focused test は通過済みで、review 待ち。以後 `TRACKER-041` から `TRACKER-045` で契約テスト、CaptureOn session folder / metadata relative path、sidecar JSONL 保存、diagnostics / replay 比較、UI/README/運用証跡を小タスク単位で進める。phase 完了条件は、CaptureOn 中に ibis tracker と同時刻近傍の 3rdparty tracker packet を self除外付きで session folder 配下の sidecar JSONL に保存し、metadata から各 file relative path を辿れ、既存 diagnostics log 互換性を壊さず後から比較でき、review / commit / PR gate が task ごとに閉じていること。 |
