# フェーズ状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在のフェーズ: comparison-logging
- 現在のタスク: TRACKER-041
- 残りフェーズ: none

## フェーズ一覧

| フェーズ | 状態 | 完了条件 |
| --- | --- | --- |
| past-tracker-history | done | `TRACKER-000` から `TRACKER-038` までの完了済みタスクと旧フェーズ詳細は `Tracker/Tracker.Core/Design/tracker-history-000-038.md` に退避済み。 |
| investigation | done | 直近履歴として `TRACKER-039` は PR #8 `https://github.com/ibis-ssl/Duck/pull/8` で `2026-05-12T00:06:33Z` に merge 済み。証跡は `reports/tracker-039-evidence-20260512084929.md`、review は `reports/tracker-039-review-20260512085258.md` と `reports/tracker-039-review-r2-20260512090207.md` に記録済み。 |
| comparison-logging | in_progress | `TRACKER-040` は CaptureOn 比較ログ拡張の設計・tracking・draft PR #9 `https://github.com/ibis-ssl/Duck/pull/9`・gpt-5.5 high review まで完了済みで、blocking findings はない。現在は `TRACKER-041` 未着手で、実装開始前に PR #9 の設計・tracking 差分についてユーザーの設計承認が必要。以後 `TRACKER-041` から `TRACKER-045` で契約テスト、CaptureOn session metadata、sidecar JSONL 保存、diagnostics / replay 比較、UI/README/運用証跡を小タスク単位で進める。phase 完了条件は、CaptureOn 中に ibis tracker と同時刻近傍の 3rdparty tracker packet を self除外付きで sidecar JSONL に保存し、既存 diagnostics log 互換性を壊さず後から比較でき、review / commit / PR gate が task ごとに閉じていること。 |
