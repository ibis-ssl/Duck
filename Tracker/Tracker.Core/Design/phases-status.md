# フェーズ状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在のフェーズ: comparison-logging
- 現在のタスク: TRACKER-040
- 残りフェーズ: none

## フェーズ一覧

| フェーズ | 状態 | 完了条件 |
| --- | --- | --- |
| preparation | done | Tracker の設計書、調査レポート、設計レビュー報告、task/phase 管理が揃い、ユーザー承認の上で設計を完了した。 |
| contracts | done | `TRACKER-001` から `TRACKER-005` が完了し、`Tracker.Core` の内部モデル、`TrackerUpdateResult`、packet generator、observer/event 契約、およびそれらを固定する failing/passing test が揃う。 |
| engine | done | `TRACKER-006` から `TRACKER-011` に加え、`TRACKER-023`、`TRACKER-025`、`TRACKER-027`、`TRACKER-028`、`TRACKER-029`、`TRACKER-031` が完了した。camera-local robot/ball tracking が設計どおり線形 Kalman filter を標準として実装され、低 visibility の stale object と Tigers 由来の近接重複 robot / 短命 ball ghost / stale secondary ball が tracked frame へ出続けない。stationary に近い tracked object の小刻みな振動は抑制され、camera 間の同一 robot ID 遠方 outlier は正常な別 camera 観測がある場合に tracked merge へ混ざらない。 |
| integration | done | `Tracker.Server` から engine、snapshot store、observer、official tracker packet 配信、設定束縛、profile 切替要求経路までが接続され、profile-aware な `VisionReceiver` 設定が反映される。 |
| ui | done | `TRACKER-015` から `TRACKER-017` に加え、`TRACKER-030` が完了し、tracked viewer、raw/tracked 切替、tracked diagnostics 表示、runtime profile 切替要求 UI、raw Vision field geometry と揃った tracked field 表示が用意される。 |
| verification | done | `TRACKER-018` と `TRACKER-019` に加え、`TRACKER-024` が完了し、Kalman 標準準拠後および stale object 抑制後の build/test/review 証跡が reports に存在する。`TRACKER-028` の指定 capture replay 証跡が `reports/tracker-028-evidence-20260510215726.md`、`TRACKER-029` の振動抑制検証が `reports/tracker-029-evidence-20260510221200.md`、`TRACKER-030` の field geometry 表示検証が `reports/tracker-030-evidence-20260510222529.md`、`TRACKER-031` の瞬間移動抑制検証が `reports/tracker-031-evidence-20260510223916.md` に記録済み。`TRACKER-036` で保守性改善後の final verification を `reports/tracker-036-final-verification-20260511093000.md` に記録済み。 |
| review | done | `TRACKER-020` に加え、Kalman 標準準拠後および stale object 抑制後の review 結果が記録され、致命的な指摘が残っていない。`TRACKER-028`、`TRACKER-029`、`TRACKER-030`、`TRACKER-031` の review 結果は reports に記録済み。`TRACKER-032` 以降も task ごとの review report を作成してきた。`TRACKER-038` は初回 review と r2 review の Medium finding を修正し、r3 review は `reports/tracker-038-review-r3-20260512082903.md` に記録済みで no findings。 |
| documentation | done | `TRACKER-021` が完了し、`Tracker.Server` の README に起動手順、画面の使い方、主要設定値の意味が記録されている。 |
| investigation | done | `TRACKER-026` が完了し、raw SSL-Vision detection と tracked 出力を同じログで比較できる。`TRACKER-038` で指定 diagnostics log の `trackedFrame=3483` 付近における黄色8番の首振り原因を orientation filter へ切り分け、rad 単位の orientation covariance / angular velocity clamp と `Tracker.CaptureReplay` の汎用 detail 改善を実装した。orientation tuning parameter は `RobotTracker` 設定へ外出し済み。CaptureReplay replay でも Kalman scale を保持する。証跡は `reports/tracker-038-evidence-20260512080732.md` に記録済み。focused test 26 件、full test 155 件は passed。r3 review は no findings。`TRACKER-039` では青1番が11番へ化ける原因を robot identity association へ切り分け、既存同一 ID track 近傍候補の優先と `RobotTracker.IdentitySwitchDistanceMm` による突然の ID 入れ替わり抑制を実装した。番号ワープ再発防止テストは stash で旧実装が失敗し、修正後に成功した。証跡は `reports/tracker-039-evidence-20260512084929.md`、初回 review は `reports/tracker-039-review-20260512085258.md`、r2 review は `reports/tracker-039-review-r2-20260512090207.md` に記録済み。初回 review の Medium 指摘は進捗ファイル同期漏れで対応済み。r2 review は指摘なし。PR #8 `https://github.com/ibis-ssl/Duck/pull/8` は `2026-05-12T00:06:33Z` に merge 済み。 |
| maintenance | done | `TRACKER-032` から `TRACKER-035` で詳細設計書の分割、巨大ソースファイルの責務別分割、主要 class / property / method の日本語コメント追加、test の確認内容コメント追加を完了した。`TRACKER-037` で dot 区切りファイル名とフォルダ分割の使い分け、コメント付与対象、test の XML コメント化方針を明文化し、現状ファイルを同じ基準へ揃えた。親 Codex は manager として作業を管理し、実装・設計書作成・test 編集・レビューは `gpt-5.5 high` sub-agent に委譲した。 |
| comparison-logging | in_progress | `TRACKER-040` で CaptureOn 比較ログ拡張の設計と tracking を追加し、以後 `TRACKER-041` から `TRACKER-045` で契約テスト、CaptureOn session metadata、sidecar JSONL 保存、diagnostics / replay 比較、UI/README/運用証跡を小タスク単位で進める。phase 完了条件は、CaptureOn 中に ibis tracker と同時刻近傍の 3rdparty tracker packet を self除外付きで sidecar JSONL に保存し、既存 diagnostics log 互換性を壊さず後から比較でき、review / commit / PR gate が task ごとに閉じていること。 |
