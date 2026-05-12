# フェーズ状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在のフェーズ: comparison-logging
- 現在のタスク: TRACKER-042
- 残りフェーズ: none

## フェーズ一覧

| フェーズ | 状態 | 完了条件 |
| --- | --- | --- |
| past-tracker-history | done | `TRACKER-000` から `TRACKER-038` までの完了済みタスクと旧フェーズ詳細は `Tracker/Tracker.Core/Design/tracker-history-000-038.md` に退避済み。tracking 軽量化と履歴退避は PR #9 準備の保守性/運用作業として完了済みで、CaptureOn 比較ログの機能仕様には含めない。 |
| investigation | done | 直近履歴として `TRACKER-039` は PR #8 `https://github.com/ibis-ssl/Duck/pull/8` で `2026-05-12T00:06:33Z` に merge 済み。証跡は `reports/tracker-039-evidence-20260512084929.md`、review は `reports/tracker-039-review-20260512085258.md` と `reports/tracker-039-review-r2-20260512090207.md` に記録済み。 |
| comparison-logging | in_progress | `TRACKER-040` は CaptureOn 比較ログ拡張の設計・tracking・draft PR #9 `https://github.com/ibis-ssl/Duck/pull/9` 更新・gpt-5.5 high review まで完了済みで、blocking findings はない。追加方針により `TRACKER-041` で保存要件としての self 除外を取り下げ、見えている `TrackerWrapperPacket` をすべて tracker packet snapshot sidecar JSONL へ保存する設計へ修正済み。ibis 自身の official packet と詳細ログの重複保持は許容し、self / 3rdparty / unknown の判別は保存除外ではなく後続表示・比較用の role / label / metadata として扱う。`TRACKER-042` では all tracker 保存 contract へテストを修正し、`MultiTrackerManager` の self early return 廃止と `TrackerState.SourceRole` / `SourceLabel` 追加により own / external / unknown tracker packet の observed / snapshot state 保持と raw payload 復元可能性の focused test が成功済み。現在は gpt-5.5 high review 待ち。以後 `TRACKER-043` から `TRACKER-045` で CaptureOn session folder / metadata relative path、全 tracker packet snapshot 保存、diagnostics / replay / playback 再生・比較、UI/README/運用証跡を小タスク単位で進める。phase 完了条件は、CaptureOn 中に見えている tracker packet を session folder 配下の snapshot sidecar JSONL に保存し、metadata から各 file relative path、source identity、role を辿れ、3rdparty tracker snapshot を `Tracker.CaptureReplay` / diagnostics / playback で再生・比較でき、既存 diagnostics log 互換性を壊さず、review / commit / PR gate が task ごとに閉じていること。 |
