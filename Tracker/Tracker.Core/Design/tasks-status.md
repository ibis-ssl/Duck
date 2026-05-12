# タスク状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 現在のタスク

- ID: TRACKER-041
- Title: 他 tracker packet 受信・識別の契約テストを追加する
- Phase: comparison-logging
- Status: in_progress
- TDD Entry: `Tracker/Tracker.Tests/TrackerConnectionLibThirdPartyTrackerTests.cs` に 3rdparty tracker packet 受信・識別の failing test を追加済み。production 実装で focused test は通過済みで、次作業は review。
- Size: small
- Dependencies: TRACKER-040
- Exit Criteria:
  - PR #9 `https://github.com/ibis-ssl/Duck/pull/9` の `TRACKER-040` 機能設計と保守性設計の分離、および CaptureOn session folder 構造を含む設計・tracking 差分について、2026-05-12 にユーザー設計承認済み
  - `TrackerConnectionLib` を第一候補として、ibis と異なる `uuid` / `sourceName` の `TrackerWrapperPacket` を比較候補として扱う failing test を追加済み
  - ibis 自身の packet は除外し、複数 source を `uuid` / `sourceName` / remote endpoint で識別して最新状態を保持する failing test を追加済み
  - `MultiTrackerManager<TrackerPacketAdapter>` 側で self除外、remote endpoint / receivedAt 保持、source identity 単位の最新状態保持を実装し、focused test は通過済み
  - review report に blocking finding が残らない

## 次の調査タスク

- none

## タスク一覧

| ID | タスク | フェーズ | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| TRACKER-039 | diagnostics log の trackedFrame 3448 付近で青1番が11番へ化ける原因を調査して修正する | investigation | done | TRACKER-038 | 原因は raw Vision で青1番 / 青11番が同一位置近傍に重複し、さらに青1番が別 robot 位置にも現れたとき、merge window 内の後続同一 ID 候補が既存 track 近傍候補を上書きし、突然の ID 入れ替わりを位置ズレより低確率として扱っていなかったことだった。既存同一 ID track 近傍候補の優先と、既存別 ID track 近傍への突然の ID 入れ替わり抑制を `RobotTracker.IdentitySwitchDistanceMm` として外出しして実装した。番号ワープを失敗条件にした再発防止テストは stash で旧実装が失敗し、修正後に成功した。証跡は `reports/tracker-039-evidence-20260512084929.md`、初回 review は `reports/tracker-039-review-20260512085258.md`、r2 review は `reports/tracker-039-review-r2-20260512090207.md` に記録済み。初回 review の Medium 指摘は進捗ファイル同期漏れで対応済み。r2 review は指摘なし。PR #8 `https://github.com/ibis-ssl/Duck/pull/8` は `2026-05-12T00:06:33Z` に merge 済み。 |
| TRACKER-040 | CaptureOn 比較ログ拡張の設計と tracking を追加する | comparison-logging | done | TRACKER-039 | `comparison-logging` phase と後続小タスクを追加し、`TrackerConnectionLib` を 3rdparty tracker 傍受の第一候補統合点、`Tracker.Server` を CaptureOn session への比較ログ統合層、`Tracker.Core` を傍受・比較保存対象外とする責務境界を設計書に明記した。sidecar JSONL 主記録、diagnostics 互換参照/self除外/timestamp近傍比較/Capture Off 再On/他 tracker 不在時の扱いを文書化し、同一 CaptureOn session で生成される packet capture、metadata、tracker diagnostics、render snapshots、3rdparty tracker comparison sidecar JSONL を一つの session folder 配下にまとめる仕様を追加した。`tracker-server-cli-ui-detail-design.md` は最新の CaptureOn 比較ログ機能設計、`tracker-server-cli-ui-maintainability-design.md` は旧 `TRACKER-034` 保守性設計として分離済み。tracking 軽量化と履歴退避は PR 準備の保守性/運用作業として完了済みで、CaptureOn 比較ログの機能仕様には含めない。実装前 draft PR #9 `https://github.com/ibis-ssl/Duck/pull/9` を作成・更新済み。実装コード・テストコードは未変更。初回 gpt-5.5 high review は `reports/tracker-040-design-review-20260512094448.md` に記録済みで blocking findings なし。設計分離・session folder 修正後の r2 review は `reports/tracker-040-design-review-r2-20260512102542.md` に記録済みで blocking findings なし。PR #9 の機能設計と保守性設計の分離、および CaptureOn session folder 構造を含む設計・tracking 差分は 2026-05-12 にユーザー承認済み。進捗同期は `reports/tracker-040-progress-sync-20260512094809.md`、設計分離修正は `reports/tracker-040-design-separation-fix-20260512100723.md`、session folder 設計修正は `reports/tracker-040-session-folder-design-fix-20260512101934.md`、r2後進捗同期は `reports/tracker-040-r2-progress-sync-20260512102917.md`、承認後同期は `reports/tracker-040-approval-sync-20260512105353.md` に記録する。 |
| TRACKER-041 | 他 tracker packet 受信・識別の契約テストを追加する | comparison-logging | in_progress | TRACKER-040 | PR #9 `https://github.com/ibis-ssl/Duck/pull/9` の `TRACKER-040` 機能設計と保守性設計の分離、および CaptureOn session folder 構造を含む設計・tracking 差分について、2026-05-12 にユーザー設計承認済み。`TrackerConnectionLib` を第一候補として、ibis と異なる `uuid` / `sourceName` の `TrackerWrapperPacket` を比較候補として保持し、ibis 自身の packet は除外し、複数 source を `uuid` / `sourceName` / remote endpoint で識別して最新状態を保持する failing test を `TrackerConnectionLibThirdPartyTrackerTests` に追加済み。`MultiTrackerManager<TrackerPacketAdapter>` の self除外 constructor、remote endpoint / receivedAt 付き `ProcessPacket` overload、`TrackerState` の remote endpoint / receivedAt 保持を実装し、focused test は通過済み。次作業は review report に blocking finding が残らない状態にすること。 |
| TRACKER-042 | CaptureOn session folder と metadata relative path を追加する | comparison-logging | todo | TRACKER-041 | CaptureOn session metadata に session folder、packet capture、tracker diagnostics、render snapshots、comparison sidecar JSONL の relative path と比較ログ設定を記録し、Capture Off / 再On で新しい session folder へ切り替わる契約を test で固定する。既存 basename 同期の考え方は session folder 名または folder 内 file 名で維持する。 |
| TRACKER-043 | CaptureOn 中に他 tracker packet を比較 sidecar JSONL へ保存する | comparison-logging | todo | TRACKER-042 | CaptureOn 中に受信した他 tracker packet を `receivedAt`、remote endpoint、`uuid`、`sourceName`、tracked frame number/timestamp、payload または summary として session folder 配下の sidecar JSONL に保存し、metadata の relative path から参照できるようにする。self除外、flush、壊れた packet の skipped/error count を満たす。 |
| TRACKER-044 | ibis committed frame と他 tracker 最新 packet を diagnostics / replay で比較可能にする | comparison-logging | todo | TRACKER-043 | 既存 diagnostics log reader の互換性を壊さず、metadata の relative path から比較 sidecar を reader または `Tracker.CaptureReplay` が解決できるようにし、ibis committed frame の timestamp 近傍にある他 tracker frame の source、frame number、ball/robot count を出せる。 |
| TRACKER-045 | 比較ログの UI/README/運用証跡を整える | comparison-logging | todo | TRACKER-044 | `/diagnostics` または README から CaptureOn session folder と比較ログの場所・読み方が分かり、既存 capture / diagnostics / render snapshot 表示を壊さない。異なる CaptureOn タイミングのログが別 folder に分かれる manual evidence、focused/full test、必要な gpt-5.5 high review report が揃い、blocking finding が残っていない。 |
