# Sub-agent実行レポート

## タスク

TRACKER-032 test 保守性改善 詳細設計の作成。

## sub-agentを使う理由

ユーザー指示により、調査と設計書作成は sub-agent に委譲し、親 Codex は manager として report を見て判断するため。

## 対象範囲

- `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
- `Tracker/Tracker.Tests/TrackerCoordinatorTests.cs`
- `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`
- `Tracker/Tracker.Tests/*Tests.cs`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- 新規または更新する test 詳細設計ファイル

## 対象外

- Core engine の詳細設計
- Server / CLI / UI の詳細設計
- test code の分割実装
- development-orchestrator の再実行
- nested Codex / codex exec / 追加 sub-agent 起動

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' reports/tracker-032-test-design-worker-20260511063428.md`
- `rg --files Tracker/Tracker.Tests | sort`
- `wc -l Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs Tracker/Tracker.Tests/TrackerCoordinatorTests.cs Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs Tracker/Tracker.Tests/*Tests.cs`
- `sed -n '1,220p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `rg -n "^(public |internal |private |file )?(sealed |static |partial )?class |\[Fact\]|\[Theory\]|private static|public static|record|enum" Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
- `rg -n "public void|public async Task|public Task|public async ValueTask|public ValueTask" Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
- `rg -n "public void|public async Task|public Task|public async ValueTask|public ValueTask" Tracker/Tracker.Tests/TrackerCoordinatorTests.cs Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs Tracker/Tracker.Tests/*Tests.cs`
- `sed -n '1,680p' Tracker/Tracker.Tests/TrackerCoordinatorTests.cs`
- `sed -n '1,260p' Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`
- `sed -n '1,260p' Tracker/Tracker.Tests/TrackedVisionViewStateTests.cs`
- `sed -n '1,260p' Tracker/Tracker.Tests/VisionPacketCaptureTests.cs`
- `sed -n '1,260p' Tracker/Tracker.Tests/Contracts/TrackerContractFixture.cs`
- `git status --short`
- `rg -n "TRACKER-032|TRACKER-035|test.*保守|maintainability|分割|コメント" Tracker/Tracker.Core/Design reports -g '*.md'`
- `sed -n '1030,1120p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `sed -n '1,110p' Tracker/Tracker.Core/Design/tasks-status.md`
- `sed -n '1,80p' Tracker/Tracker.Core/Design/phases-status.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-test-maintainability-detail-design.md`
- `sed -n '1,220p' reports/tracker-032-test-design-worker-20260511063428.md`
- `git status --short -- Tracker/Tracker.Core/Design/tracker-test-maintainability-detail-design.md reports/tracker-032-test-design-worker-20260511063428.md`
- `wc -l Tracker/Tracker.Core/Design/tracker-test-maintainability-detail-design.md reports/tracker-032-test-design-worker-20260511063428.md`

## 対象ファイル

- 確認:
  - `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs`
  - `Tracker/Tracker.Tests/TrackerCoordinatorTests.cs`
  - `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`
  - `Tracker/Tracker.Tests/TrackedVisionViewStateTests.cs`
  - `Tracker/Tracker.Tests/VisionPacketCaptureTests.cs`
  - `Tracker/Tracker.Tests/Contracts/TrackerContractFixture.cs`
  - `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `Tracker/Tracker.Core/Design/tasks-status.md`
  - `Tracker/Tracker.Core/Design/phases-status.md`
- 変更:
  - `Tracker/Tracker.Core/Design/tracker-test-maintainability-detail-design.md`
  - `reports/tracker-032-test-design-worker-20260511063428.md`

## 指摘事項

- blocking finding はなし。
- `TrackerEngineTemporalContractTests.cs` は 2,281 行かつ 60 個の `[Fact]` が同居しており、TRACKER-035 では最優先で責務別分割する必要がある。
- `TrackerCoordinatorTests.cs` は test 本体と publisher / observer helper が 1 file に同居しているため、coordinator flow / reset-profile / diagnostics capture と support helper へ分ける設計にした。
- `TrackerRenderSnapshotLogReaderTests.cs`、`TrackedVisionViewStateTests.cs`、`VisionPacketCaptureTests.cs` は分割必須ではなく、説明コメント追加と局所 helper 整理を優先する設計にした。

## 結果

- `Tracker/Tracker.Core/Design/tracker-test-maintainability-detail-design.md` を新規作成した。
- 設計書には、巨大 test file と責務の分解候補、test class / fixture / helper の推奨分割、各 test へ「何を確認しているか」の日本語コメントを追加する基準、TRACKER-035 の実行順序、test の意味を変えないための注意点と検証観点を記載した。
- test code は変更していないため、`dotnet test` は実行していない。

## リスク

- TRACKER-035 実装時に xUnit fixture 解決が継承 interface だけでは分かりにくくなる可能性があるため、設計書では concrete class 側にも `IClassFixture<TrackerContractFixture>` を明示する方針にした。
- comment 追加と file 分割の差分が混ざると review が難しくなるため、TRACKER-035 では engine contract 分割、coordinator 分割、コメント追加を順に focused test で区切る必要がある。
- 並行 worker の core / server 詳細設計 file は未確認・未編集であり、最終的な TRACKER-032 親側で詳細設計間の命名や依存関係の整合確認が必要。
