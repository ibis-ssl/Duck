# Sub-agent実行レポート

## タスク

- 目的: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` の設計レビューを再実施し、前回指摘の解消確認と新規の設計リスク洗い出しを行う
- タスク種別: 設計書レビュー

## sub-agentを使う理由

- 理由: 設計更新後の独立レビューを別視点で実施し、指摘事項を報告書として固定するため

## 対象範囲

- 対象: `TRACKER-000` の設計書、調査資料、前回レビュー結果

## 対象外

- 対象外: 実装変更、設計書本文の編集、進捗管理ファイル更新

## 実行コマンド

- `sed -n '1,220p' /home/ibis/ssl/IbisDuck/AGENTS.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/development-orchestrator/SKILL.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `git -C /home/ibis/AI/CodexSkill status --short --branch`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/references/sub-agent-report-template.md`
- `sed -n '1,240p' /home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md`
- `sed -n '1,260p' /home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-20260501120622.md`
- `sed -n '1,220p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tasks-status.md`
- `sed -n '1,220p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/phases-status.md`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '1,820p'`
- `nl -ba /home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_detection_tracked.proto | sed -n '1,240p'`
- `nl -ba /home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_wrapper_tracked.proto | sed -n '1,160p'`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionReceiverService.cs | sed -n '1,240p'`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionPacketStore.cs | sed -n '1,220p'`
- `rg -n "UDP 配信|publish|observer|WorldFrameCommitted|ProfileSwitched|GeometryReset|テスト" /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`

## 対象ファイル

- 確認:
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `/home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md`
  - `/home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-20260501120622.md`
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tasks-status.md`
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/phases-status.md`
  - `/home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_detection_tracked.proto`
  - `/home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_wrapper_tracked.proto`
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionReceiverService.cs`
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionPacketStore.cs`
- 変更:
  - `/home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-r2-20260501123858.md`

## 指摘事項

- `High` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:222-247,268-273,298-304,318`
  - 設計は `ITrackerEngine` が raw packet 1 件を受けて「最新 `TrackerFrame` を返す」単一戻り値契約のままですが、実際の処理規則は `pending buffer` を flush して 1 入力から `0..N` 個の world frame を確定しうる形です。
  - このままだと `TrackerCoordinator` は no-flush packet で古い frame を再配信するか、multi-flush packet で中間 frame を落とすかの二択になり、official packet 配信、UI 更新、observer 通知のどこかが欠落または重複します。buffered flush を外部へどう露出するかを契約として固定する必要があります。

- `Medium` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:759-788`
  - rule 側へ渡す高レベル event 一覧には `ProfileSwitched` と `GeometryReset` が含まれていますが、最小 `ITrackerObserver` には対応コールバックがありません。また、`WorldFrameCommitted` と派生 event を同一 frame 内でどの順に publish するかも未固定です。
  - rule が frame 履歴を保持する前提なのに reset/profile 切替通知を受け取れないと stale state を捨てられず、同一 frame の event 発火順も実装依存になります。observer/event 契約をこの粒度で分けるなら、購読可能な event 集合と publish 順を明文化すべきです。

- `Medium` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:299-304,345-348`
  - geometry 大変更時の reset 規則は camera-local track、kick/contact state、world snapshot の clear までは定義されていますが、`pending buffer` をどう扱うかが抜けています。
  - そのままでは reset 前に溜まっていた detection が新 geometry の下で後続 flush され、field 座標系の切替境界をまたいだ frame が混入します。geometry reset を stale state 除去として扱うなら、未確定 detection の破棄または epoch 分離を設計に含める必要があります。

## 結果

- 3 件の指摘あり。
- 前回レビューで挙がっていた 6 件の主指摘
  - 時系列バッファ/統合窓
  - timestamp 基準
  - world 側 filter の扱い
  - profile 切替時の `uuid` / state 規則
  - `kicked_ball` の寿命
  - secondary ball の安定順
  は、現行設計で解消済みと判断した。
- 残課題は、buffered flush を返却/配信/通知契約へどう落とすかと、observer/event/reset 境界の固定である。

## リスク

- 本レビューは設計書、前回レビュー、proto、既存受信実装の整合確認までであり、実装や自動テストでの検証は未実施。
- `ITrackerObserver` を同期実行する前提は書かれているが、observer 例外隔離や配信失敗時の扱いは未設計のため、実装時に追加設計が必要になる可能性がある。
