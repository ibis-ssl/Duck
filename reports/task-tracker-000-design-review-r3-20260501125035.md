# Sub-agent実行レポート

## タスク

- 目的: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` のレビューを再実施し、直近のレビュー対応で残指摘が解消されたかを確認する
- タスク種別: 設計書レビュー

## sub-agentを使う理由

- 理由: 設計更新後の独立レビューを別視点で実施し、解消確認と残リスクを記録するため

## 対象範囲

- 対象: `TRACKER-000` の設計書、調査資料、前回レビュー結果、直近レビュー対応

## 対象外

- 対象外: 実装変更、設計書本文の編集、進捗管理ファイル更新

## 実行コマンド

- `sed -n '1,220p' /home/ibis/ssl/IbisDuck/AGENTS.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/development-orchestrator/SKILL.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `git -C /home/ibis/AI/CodexSkill status --short --branch`
- `sed -n '1,260p' /home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-r2-20260501123858.md`
- `sed -n '1,260p' /home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md`
- `sed -n '1,260p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tasks-status.md`
- `sed -n '1,260p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/phases-status.md`
- `sed -n '1,320p' /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '180,420p'`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '420,620p'`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '720,860p'`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '860,940p'`
- `rg -n "TrackerUpdateResult|CommittedFrames|EmittedEvents|ProfileSwitched|GeometryReset|pending|buffer|observer|publish|WorldFrameCommitted|reset|geometry|テスト" /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `rg -n "profile|設定セット|RuntimeOverrides|OnProfileSwitched|ProfileSwitched|再設定|reset" /home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `nl -ba /home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_detection_tracked.proto | sed -n '1,260p'`
- `nl -ba /home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_wrapper_tracked.proto | sed -n '1,220p'`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionReceiverService.cs | sed -n '1,260p'`
- `nl -ba /home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionPacketStore.cs | sed -n '1,260p'`

## 対象ファイル

- 確認:
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `/home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md`
  - `/home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-r2-20260501123858.md`
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tasks-status.md`
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/phases-status.md`
  - `/home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_detection_tracked.proto`
  - `/home/ibis/ssl/IbisDuck/SslProto/src/external/ssl-game-controller/proto/tracker/ssl_vision_wrapper_tracked.proto`
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionReceiverService.cs`
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Server/Vision/VisionPacketStore.cs`
- 変更:
  - `/home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-r3-20260501125035.md`

## 指摘事項

- `Medium` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:228-229,252,287,519,531-533,804-816`
  - `ITrackerEngine` 契約では「現在有効な設定セット」を入力として受け、engine 自身が現在設定と camera-local track / pending buffer / world snapshot を保持する前提になっています。一方で profile 切替規則では `TrackerCoordinator` が新 profile 適用時にそれらの state を clear すると書かれており、設定切替の責務が `engine` と `coordinator` の両方にまたがっています。
  - このままでは、profile 切替を `Update(packet, settings)` のような通常更新で検知するのか、別の再設定 API で先に reset するのかが確定しません。結果として `ProfileSwitched` をいつ `EmittedEvents` に載せるか、reset が次の `WorldFrameCommitted` より前に必ず起きるかが実装依存になり、旧 profile の状態混入や observer テストの期待値分岐を招きます。

## 結果

- 1 件の指摘あり。
- r2 で残っていた 3 件
  - `TrackerUpdateResult` の `0..N` / `CommittedFrames` 契約
  - `ProfileSwitched` / `GeometryReset` を含む observer/event publish 順
  - geometry reset 時の pending buffer clear
  は、現行設計で解消済みと判断した。
- 新規に残った論点は、profile 切替を誰がどの API で適用し、その結果 `ProfileSwitched` をどのタイミングで emit するかの責務境界である。

## リスク

- 本レビューは設計書、調査メモ、前回レビュー、proto、既存受信実装の整合確認までであり、実装や自動テストでの検証は未実施。
- profile 切替の責務境界を明文化しないまま実装へ進むと、reset と event 発火順の解釈が実装者ごとに割れ、`TRACKER-001` 以降の契約テストが不安定になる。
