# Sub-agent実行レポート

## タスク

`TRACKER-047` review findings を受けて、設計資料と固定残タスク一覧の妥当性を監査する。

## sub-agentを使う理由

親エージェントは実装・調査・設計監査を直接行わず、サブエージェントのレポートを読んで判断するため。

## 対象範囲

- `reports/tracker-047-review-20260512150929.md` の blocking findings
- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`
- `reports/tracker-captureon-fixed-task-sync-20260512145820.md`
- `reports/topic-tracker-captureon-handover-20260512144815.md`
- review findings を `TRACKER-047` 内で閉じるべきか、設計や固定一覧を作り直すべきかの判定

## 対象外

- production code の変更
- test code の変更
- design / tracking の編集
- 新規 `TRACKER` の追加
- build / test 実行
- PR body 更新
- commit / push

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/task-consistency-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/task-breakdown-planner/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/progress-sync-manager/SKILL.md`
- `sed -n '1,260p' reports/tracker-047-design-audit-after-review-20260512151541.md`
- `rg -n "TRACKER-047|captureon|fixed task|固定一覧|TrackerSnapshotReplayReader|source shape|review finding" /home/ibis/.codex/memories/MEMORY.md`
- `nl -ba reports/tracker-047-review-20260512150929.md | sed -n '1,240p'`
- `rg -n "TRACKER-047|TRACKER-048|TRACKER-049|TRACKER-050|TRACKER-051|capture|Capture|snapshot|Snapshot|replay|Replay|timestamp|Timestamp|receivedAt|ReceivedAt|XML|DTO|documentation|public" Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md Tracker/Tracker.Core/Design/tracker-architecture-plan.md Tracker/Tracker.Core/Design/tasks-status.md Tracker/Tracker.Core/Design/phases-status.md reports/tracker-captureon-fixed-task-sync-20260512145820.md reports/topic-tracker-captureon-handover-20260512144815.md`
- `git status --short`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '70,135p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '112,148p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '190,208p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '548,558p'`
- `nl -ba Tracker/Tracker.Core/Design/tasks-status.md | sed -n '1,130p'`
- `nl -ba Tracker/Tracker.Core/Design/phases-status.md | sed -n '1,80p'`
- `nl -ba /home/ibis/AI/CodexSkill/skills/review-enforcer/references/session-review-shape-policy.md | sed -n '1,220p'`
- `nl -ba /home/ibis/AI/CodexSkill/skills/review-enforcer/references/source-documentation-policy.md | sed -n '1,260p'`
- `nl -ba /home/ibis/AI/CodexSkill/skills/review-enforcer/references/source-layout-policy.md | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs | sed -n '1,280p'`
- `nl -ba Tracker/Tracker.Tests/TrackerReplayIntegrationTddTests.cs | sed -n '1,320p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs | sed -n '1,90p'`
- `nl -ba Tracker/Tracker.Core/Model/TrackerFrame.cs | sed -n '1,45p'`
- `nl -ba Tracker/Tracker.Core/Proto/TrackerPacketGenerator.cs | sed -n '35,50p'`
- `nl -ba reports/tracker-captureon-fixed-task-sync-20260512145820.md | sed -n '1,105p'`
- `nl -ba reports/topic-tracker-captureon-handover-20260512144815.md | sed -n '1,220p'`
- `nl -ba reports/tracker-047-design-audit-after-review-20260512151541.md | sed -n '1,220p'`
- build / test / review は指示どおり未実行。

## 対象ファイル

- 変更:
  - `reports/tracker-047-design-audit-after-review-20260512151541.md`
- 確認:
  - `reports/tracker-047-review-20260512150929.md`
  - `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `Tracker/Tracker.Core/Design/tasks-status.md`
  - `Tracker/Tracker.Core/Design/phases-status.md`
  - `reports/tracker-captureon-fixed-task-sync-20260512145820.md`
  - `reports/topic-tracker-captureon-handover-20260512144815.md`
  - `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
  - `Tracker/Tracker.Tests/TrackerReplayIntegrationTddTests.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs`
  - `Tracker/Tracker.Core/Model/TrackerFrame.cs`
  - `Tracker/Tracker.Core/Proto/TrackerPacketGenerator.cs`
  - `/home/ibis/AI/CodexSkill/skills/review-enforcer/references/session-review-shape-policy.md`
  - `/home/ibis/AI/CodexSkill/skills/review-enforcer/references/source-documentation-policy.md`
  - `/home/ibis/AI/CodexSkill/skills/review-enforcer/references/source-layout-policy.md`

## 指摘事項

- High finding の timestamp 軸不一致は、設計資料で既に禁止・要求が明確である。`tracker-server-cli-ui-detail-design.md:75-79` は ibis `TrackerFrame.data_timestamp_ns` と snapshot 側 `TrackedFrame.timestamp` の近傍比較を要求し、`tracker-architecture-plan.md:143` も exact frame number ではなく同じ 2 つの timestamp の nearest / latest-before 規則を明示している。さらに `tracker-architecture-plan.md:196-205` は `TrackerFrame.data_timestamp_ns` を観測基準時刻とし、receive time / processing time を使わないこと、および `TrackerPacketGenerator` がその値を `TrackedFrame.timestamp` へ変換することを固定している。したがって設計更新を待つ案件ではなく、`TRACKER-047` 内で wall-clock `receivedAt` minute-relative と snapshot timestamp を比較している実装を直す review fix と判定する。
- repository 確認でも、finding の根拠は実装側にある。`TrackerSnapshotReplayReader.cs:111-115` は diagnostics log の行頭 timestamp を `ToMinuteRelativeTimestampNs` に変換して nearest を選び、`TrackerSnapshotReplayReader.cs:188-190` は wall-clock の minute-relative ns を作っている。一方、diagnostics log 行頭は `TrackerCoordinator/Diagnostics.cs:42-43` の `receivedAt` であり、ibis committed frame の data timestamp ではない。`TrackerFrame.cs:14-17` と `TrackerPacketGenerator.cs:41-45` は data timestamp が official `TrackedFrame.timestamp` へ流れる契約を示しているため、review finding は設計通りの通常経路不具合である。
- Medium finding の public replay DTO positional properties の XML doc 不足は、既存 source shape policy で十分に扱える。`session-review-shape-policy.md:28-49` は public / internal API surface、configuration schema、DTO property の XML documentation 不足を blocking とする。`source-documentation-policy.md:10-25` も public / protected / internal properties、DTO properties、serialization schema properties に XML summary を要求している。これは設計機能仕様というより source shape / coding standard の review gate なので、`tracker-server-cli-ui-detail-design.md` や `tracker-architecture-plan.md` へ API/DTO documentation contract を重複追記する必要は低い。
- `TRACKER-047..050` の固定一覧は review findings 後も妥当である。`tasks-status.md:16-19` と `phases-status.md:21-26` は `TRACKER-047` の finding を修正・再検証・r2 review まで閉じる前提を既に持ち、`TRACKER-048` は user-visible な diagnostics / replay / playback 接続、`TRACKER-049` は運用ドキュメント、`TRACKER-050` は PR ready 化として分離されている。今回の High / Medium はいずれも既存 `TrackerSnapshotReplayReader` / replay DTO の review fix であり、新規 `TRACKER` 追加理由にはならない。
- 現在の設計・tracking は「review finding を鵜呑みにして TRACKER を増やす」リスクをかなり抑制できている。`tracker-server-cli-ui-detail-design.md:109-115`、`tasks-status.md:25-32`、`phases-status.md:19-26` は固定一覧と `TRACKER-051` 以降の追加制約を明記している。`reports/tracker-captureon-fixed-task-sync-20260512145820.md:80-84` も固定一覧同期の意図を記録し、`topic-tracker-captureon-handover-20260512144815.md:152-157` は `TRACKER-051` 以降をユーザー承認または今回 PR への hardening 明示判断がある場合だけに限定している。
- 追加残件が将来見つかった場合でも、すぐ `TRACKER-051` を追加するのではなく、設計やり直しと固定一覧再作成の要否を先に判定する必要がある。今回の findings についてはその条件に達しておらず、固定一覧再作成は不要である。

## 結果

- 設計は High finding を判定するには十分であり、不足ではない。nearest timestamp は ibis `TrackerFrame.data_timestamp_ns` と snapshot `TrackedFrame.timestamp` の同一時間軸で比較する契約が既にあるため、親は design update ではなく implementation fix を委譲するのが妥当である。
- Medium finding も既存 source shape policy で十分であり、機能設計書側への API/DTO documentation contract 追記は不要と判定する。親は `TRACKER-047` review fix の一部として public replay DTO positional properties に XML documentation を追加させればよい。
- 固定一覧の作り直しは不要である。`TRACKER-047` は review-fix、focused / related / 必要な full test 再検証、r2 review、progress sync まで閉じる。`TRACKER-048..050` は現行の固定残タスクとして維持する。
- 次に親が委譲すべき作業は、`TRACKER-047` implementation fix である。内容は timestamp matching を同じ data timestamp 軸へ修正し、wall-clock `receivedAt` と data timestamp を意図的にずらした regression test を追加し、public replay DTO positional properties の XML documentation を追加すること。その後、focused / related / 必要な full test、r2 review、progress sync の順で閉じる。
- build / test / review は非目標のため未実行。

## リスク

- 現時点の tracking は review report 作成後の状態へまだ同期されていないため、親が implementation fix を委譲する前後で `tasks-status.md` / `phases-status.md` の Review Entry と status を progress-sync-manager 経由で更新する必要がある。
- timestamp fix では diagnostics log から ibis committed frame の data timestamp をどう取得するかが実装上の焦点になる。現行 diagnostics log 行頭は `receivedAt` のため、その値を data timestamp として扱う修正は不可である。
- source shape policy は CodexSkill 側の reviewer policy としては十分だが、IbisDuck の機能設計書だけを読む実装担当には Medium finding の理由が見えにくい。implementation fix の委譲プロンプトでは `source-documentation-policy.md` を明示的に読ませる必要がある。
