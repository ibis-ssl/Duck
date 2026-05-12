# タスク状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 現在のタスク

- ID: TRACKER-042
- Title: 全 tracker 保存 contract の production 実装を行う
- Phase: comparison-logging
- Status: review_waiting
- TDD Entry: `TrackerConnectionLibAllTrackerSnapshotContractTests.cs` で self / external / unknown を保存対象にする all tracker contract を作成済み。`MultiTrackerManager` の self early return を廃止し、`TrackerState` に `SourceRole` / `SourceLabel` を追加して focused test は成功済み。gpt-5.5 high review 待ち。
- Size: small
- Dependencies: TRACKER-041
- Exit Criteria:
  - `MultiTrackerManager` の self early return を廃止し、self identity 判定を保存除外ではなく source role metadata 判定へ変更する
  - own tracker packet も observed / snapshot state として保持する
  - external tracker packet も observed / snapshot state として保持する
  - `TrackerState` に `SourceRole` / `SourceLabel` を持たせ、self identity と一致する場合は own、その他は external、判別不能でも unknown として保存を落とさない
  - 複数 source は `uuid` / `sourceName` / remote endpoint で識別し、最新状態を保持する
  - `TrackerConnectionLibAllTrackerSnapshotContractTests` の focused test が成功する
  - sidecar JSONL、CaptureOn metadata/session folder、diagnostics replay は本実装しない

## 次の調査タスク

- none

## タスク一覧

| ID | タスク | フェーズ | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| TRACKER-039 | diagnostics log の trackedFrame 3448 付近で青1番が11番へ化ける原因を調査して修正する | investigation | done | TRACKER-038 | 原因は raw Vision で青1番 / 青11番が同一位置近傍に重複し、さらに青1番が別 robot 位置にも現れたとき、merge window 内の後続同一 ID 候補が既存 track 近傍候補を上書きし、突然の ID 入れ替わりを位置ズレより低確率として扱っていなかったことだった。既存同一 ID track 近傍候補の優先と、既存別 ID track 近傍への突然の ID 入れ替わり抑制を `RobotTracker.IdentitySwitchDistanceMm` として外出しして実装した。番号ワープを失敗条件にした再発防止テストは stash で旧実装が失敗し、修正後に成功した。証跡は `reports/tracker-039-evidence-20260512084929.md`、初回 review は `reports/tracker-039-review-20260512085258.md`、r2 review は `reports/tracker-039-review-r2-20260512090207.md` に記録済み。初回 review の Medium 指摘は進捗ファイル同期漏れで対応済み。r2 review は指摘なし。PR #8 `https://github.com/ibis-ssl/Duck/pull/8` は `2026-05-12T00:06:33Z` に merge 済み。 |
| TRACKER-040 | CaptureOn 比較ログ拡張の設計と tracking を追加する | comparison-logging | done | TRACKER-039 | `comparison-logging` phase と後続小タスクを追加し、`TrackerConnectionLib` を tracker packet 傍受の第一候補統合点、`Tracker.Server` を CaptureOn session への比較ログ統合層、`Tracker.Core` を傍受・比較保存対象外とする責務境界を設計書に明記した。sidecar JSONL 主記録、diagnostics 互換参照、source 識別、timestamp近傍比較、Capture Off 再On、他 tracker 不在時の扱いを文書化し、同一 CaptureOn session で生成される packet capture、metadata、tracker diagnostics、render snapshots、tracker packet snapshot sidecar JSONL を一つの session folder 配下にまとめる仕様を追加した。`tracker-server-cli-ui-detail-design.md` は最新の CaptureOn 比較ログ機能設計、`tracker-server-cli-ui-maintainability-design.md` は旧 `TRACKER-034` 保守性設計として分離済み。tracking 軽量化と履歴退避は PR 準備の保守性/運用作業として完了済みで、CaptureOn 比較ログの機能仕様には含めない。実装前 draft PR #9 `https://github.com/ibis-ssl/Duck/pull/9` を作成・更新済み。実装コード・テストコードは未変更。初回 gpt-5.5 high review は `reports/tracker-040-design-review-20260512094448.md` に記録済みで blocking findings なし。設計分離・session folder 修正後の r2 review は `reports/tracker-040-design-review-r2-20260512102542.md` に記録済みで blocking findings なし。PR #9 の機能設計と保守性設計の分離、および CaptureOn session folder 構造を含む設計・tracking 差分は 2026-05-12 にユーザー承認済み。進捗同期は `reports/tracker-040-progress-sync-20260512094809.md`、設計分離修正は `reports/tracker-040-design-separation-fix-20260512100723.md`、session folder 設計修正は `reports/tracker-040-session-folder-design-fix-20260512101934.md`、r2後進捗同期は `reports/tracker-040-r2-progress-sync-20260512102917.md`、承認後同期は `reports/tracker-040-approval-sync-20260512105353.md` に記録する。 |
| TRACKER-041 | 全 tracker packet 保存方針へ設計と tracking を修正する | comparison-logging | done | TRACKER-040 | 追加方針により、既存の self 除外前提を取り下げた。見えている `TrackerWrapperPacket` はすべて snapshot sidecar へ保存し、ibis 自身の official packet と詳細ログの重複保持を許容する設計へ更新済み。self / 3rdparty / unknown の判別は保存除外ではなく後続表示・比較用の role / label / metadata として扱い、判別不能でも保存を落とさない方針へ変更済み。3rdparty tracker snapshot は raw payload 参照または復元可能情報、source uuid / sourceName / remote endpoint、receivedAt、tracked frame number / timestamp、summary を持ち、`Tracker.CaptureReplay` / diagnostics / playback が session folder 内の snapshot log から再生・比較表示できる設計へ更新済み。設計修正は `reports/tracker-041-all-trackers-design-fix-20260512111628.md`、監査レポート回収は `reports/tracker-041-all-trackers-design-audit-20260512111218.md` と `reports/tracker-041-all-trackers-implementation-audit-20260512111218.md` に記録済み。 |
| TRACKER-042 | 全 tracker 保存 contract の production 実装を行う | comparison-logging | review_waiting | TRACKER-041 | `TrackerConnectionLibAllTrackerSnapshotContractTests.cs` の all tracker 保存 contract に対し、`MultiTrackerManager` の self early return を廃止し、own / external / unknown を保存除外ではなく `SourceRole` / `SourceLabel` metadata として `TrackerState` に保持する production 実装を追加済み。own tracker packet と external tracker packet は `uuid` / `sourceName` / remote endpoint 単位で observed / snapshot state として保持され、保存 state から official packet payload を復元できる。focused test は成功済み。sidecar JSONL、CaptureOn metadata/session folder、diagnostics replay は未実装で後続 `TRACKER-043` 以降の範囲。作業レポートは `reports/tracker-042-all-trackers-tdd-contract-20260512112546.md` と `reports/tracker-042-all-trackers-implementation-20260512113459.md`。gpt-5.5 high review 待ち。 |
| TRACKER-043 | CaptureOn session folder と metadata relative path を追加する | comparison-logging | todo | TRACKER-042 | CaptureOn session metadata に session folder、packet capture、tracker diagnostics、render snapshots、tracker packet snapshot sidecar JSONL の relative path、source identity 一覧、role / label、snapshot log 設定を記録し、Capture Off / 再On で新しい session folder へ切り替わる契約を test で固定する。既存 basename 同期の考え方は session folder 名または folder 内 file 名で維持する。 |
| TRACKER-044 | CaptureOn 中に全 tracker packet snapshot を保存する | comparison-logging | todo | TRACKER-043 | CaptureOn 中に受信した tracker packet を self 除外せず、`receivedAt`、remote endpoint、`uuid`、`sourceName`、role / label、tracked frame number / timestamp、raw payload 参照または復元可能情報、summary として session folder 配下の sidecar JSONL に保存し、metadata の relative path から参照できるようにする。flush、壊れた packet の skipped/error count、判別不能 packet の保存継続を満たす。 |
| TRACKER-045 | tracker snapshot を diagnostics / replay / playback で再生・比較可能にする | comparison-logging | todo | TRACKER-044 | 既存 diagnostics log reader の互換性を壊さず、metadata の relative path から snapshot sidecar を reader または `Tracker.CaptureReplay` が解決できるようにし、ibis committed frame の timestamp 近傍にある tracker snapshot の source、role、frame number、timestamp、ball/robot count、raw payload 復元状態を出せる。`/diagnostics` playback は session folder 内の snapshot log から 3rdparty tracker を再生・比較表示できる。README/運用証跡、manual evidence、focused/full test、必要な gpt-5.5 high review report が揃い、blocking finding が残っていない。 |
