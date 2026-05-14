# Sub-agent実行レポート

## タスク

- 目的: RAW-VISION-014 r3 review の blocking finding を受け、Vision live comparison の raw geometry 優先 fallback contract を TDD Red として補強する。
- タスク種別: TDD review-fix / test authoring

## sub-agentを使う理由

- 理由: ユーザー指示により直前の実装修正 agent として小修正を担当し、test authoring と report 記入を report-backed に残すため。

## 対象範囲

- 対象: `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs` の geometry contract 補強と、この report の空欄補完。

## 対象外

- 対象外: production code、設計書、PR本文、README、`Tracker/Tracker.Server/appsettings.json`、`Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' reports/issue-10-raw-vision-014-tdd-fix-r3-20260514091616.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/implementation-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `rg -n "RAW-VISION-014|raw-vision|Vision split|geometry contract|raw geometry" /home/ibis/.codex/memories/MEMORY.md`
  - `sed -n '1,260p' reports/issue-10-raw-vision-014-tdd-review-r3-20260514091311.md`
  - `nl -ba Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md | sed -n '208,220p'`
  - `nl -ba Tracker/Tracker.Server/Design/tasks-status.md | sed -n '10,18p;64,72p;90,98p'`
  - `sed -n '1,520p' Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
  - `sed -n '520,760p' Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
  - `rg -n "class TrackedSnapshotStore|record TrackedSnapshot|TrackedSnapshotStore|ThirdParty|ExternalTracker|TrackerPacket|MultiTrackerManager|GeometrySource|Geometry" Tracker/Tracker.Server Tracker/Tracker.Tests -g '*.cs' -g '*.razor'`
  - `sed -n '1,130p' Tracker/Tracker.Server/Tracking/TrackedSnapshotStore.cs`
  - `sed -n '1,170p' TrackerConnectionLib/src/MultiTrackerManager.cs`
  - `git diff --check`
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests|FullyQualifiedName~VisionLiveComparisonViewStateTests" -m:1 /nr:false`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
  - 変更: `reports/issue-10-raw-vision-014-tdd-fix-r3-20260514091616.md`
  - 確認: `reports/issue-10-raw-vision-014-tdd-review-r3-20260514091311.md`
  - 確認: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - 確認: `Tracker/Tracker.Server/Design/tasks-status.md`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackedSnapshotStore.cs`
  - 確認: `TrackerConnectionLib/src/MultiTrackerManager.cs`
  - 非対象維持: `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
  - 非対象維持: `Tracker/Tracker.Server/appsettings.json` の既存 unrelated 差分には触れていない。

## 指摘事項

- 指摘要約または「指摘なし」:
  - 指摘なし。r3 review の geometry blocking finding に対する TDD contract 補強を完了した。

## 結果

- 結果:
  - `VisionLiveComparisonRenderSnapshot` に `GeometrySource` / `GeometrySourceLabel` の contract を追加した。geometry が null でないだけではなく、どの source を overlay 全体の field 基準にしたかを UI が説明できることを要求する。
  - `VisionLiveComparisonSnapshotComposer_CapturesImmutableRenderSnapshotAndCreatesSourceCandidates` に raw geometry の値検証を追加した。raw packet に `SSL_GeometryData` がある場合、`renderSnapshot.Geometry.Field.FieldLength=9000`、`FieldWidth=6000`、`GeometrySource=RawAggregate`、`GeometrySourceLabel=Raw Aggregate` を要求する。
  - `VisionLiveComparisonSnapshotComposer_UsesTrackedGeometryOnlyWhenRawGeometryIsMissing` を追加した。raw detection に geometry が無く、`TrackedSnapshotStore` に tracked geometry がある場合だけ `FieldLength=12000`、`FieldWidth=9000`、`GeometrySource=Tracked`、`GeometrySourceLabel=Tracked` へ fallback することを要求する。
  - `VisionLiveComparisonSnapshotComposer_WhenOnlyThirdPartyTrackerHasFrame_DoesNotUseItAsGeometrySource` を追加した。raw/tracked geometry が無く、3rd party tracker frame だけがある場合、`Geometry` は null、`GeometrySource=Missing` であり、`ThirdPartyTracker` / `3rd party tracker` を geometry source として採用しないことを要求する。
  - focused test は expected failing。最終実行結果は 37 件中 26 pass / 11 fail。fail は Vision live comparison API 未実装 9 件、diagnostics latest-before が現状 `saved-session-alignment` になる 1 件、future-only source が現状 `Ready` になる 1 件。
  - `git diff --check` は pass。

## リスク

- 未解決のリスクまたは後続対応:
  - RAW-VISION-015 では composer が raw geometry を最優先し、raw geometry が無い場合だけ tracked geometry を `SSL_GeometryData` へ変換して fallback する必要がある。
  - 3rd party tracker live source は比較対象の object state として扱い、field geometry の復元元または geometry source として採用しない必要がある。
  - geometry source metadata は overlay / details / legend 側で説明可能な値として残す必要がある。
  - diagnostics 側の既存 Red 2 件は今回の変更対象外で、RAW-VISION-015 実装時に latest-before / future fallback contract として回収する必要がある。
