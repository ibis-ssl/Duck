# Sub-agent実行レポート

## タスク

TRACKER-032 Core engine 詳細設計の作成。

## sub-agentを使う理由

ユーザー指示により、調査と設計書作成は sub-agent に委譲し、親 Codex は manager として report を見て判断するため。

## 対象範囲

- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Core/TrackerModelContracts.cs`
- `Tracker/Tracker.Core/TrackerPacketGenerator.cs`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- 新規または更新する Core 詳細設計ファイル

## 対象外

- Server / CLI / UI の詳細設計
- test file の分割設計
- 実装コードの分割
- development-orchestrator の再実行
- nested Codex / codex exec / 追加 sub-agent 起動

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `rg -n "TRACKER-032|Core engine|巨大ファイル|コメント" /home/ibis/.codex/memories/MEMORY.md`
- `sed -n '1,240p' reports/tracker-032-core-design-worker-20260511063428.md`
- `test -f Tracker/Tracker.Core/Design/tracker-core-engine-detail-design.md && sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-core-engine-detail-design.md || true`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `sed -n '260,620p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `wc -l Tracker/Tracker.Core/TrackerExecutionContracts.cs Tracker/Tracker.Core/TrackerModelContracts.cs Tracker/Tracker.Core/TrackerPacketGenerator.cs`
- `rg -n "^(public|internal|file|partial|sealed|readonly|record|enum|class|namespace)|^///|^    public|^    internal|^    private|^    protected" Tracker/Tracker.Core/TrackerExecutionContracts.cs Tracker/Tracker.Core/TrackerModelContracts.cs Tracker/Tracker.Core/TrackerPacketGenerator.cs`
- `sed -n '1,260p' Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `sed -n '260,620p' Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `sed -n '620,980p' Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `sed -n '980,1340p' Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `sed -n '1340,1720p' Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `sed -n '1720,2265p' Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `sed -n '1,260p' Tracker/Tracker.Core/TrackerModelContracts.cs`
- `sed -n '1,240p' Tracker/Tracker.Core/TrackerPacketGenerator.cs`
- `rg -n "///|<summary>|<remarks>" Tracker/Tracker.Core/TrackerExecutionContracts.cs Tracker/Tracker.Core/TrackerModelContracts.cs Tracker/Tracker.Core/TrackerPacketGenerator.cs`
- `rg -n "TrackerExecutionContracts|TrackerModelContracts|TrackerPacketGenerator|TrackerEngineSettings|TrackerRuntimeOverrides|ITrackerObserver" Tracker/Tracker.Tests Tracker -g '*.cs'`
- `rg -n "分割|詳細設計|コメント|XML|public API|巨大" Tracker/Tracker.Core/Design reports -g '*.md'`
- `rg -n "GenerateDocumentationFile|NoWarn|WarningsAsErrors|DocumentationFile|nullable|ImplicitUsings" Tracker -g '*.csproj' -g 'Directory.Build.*'`
- `rg --files Tracker/Tracker.Core | sort`
- `git status --short`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-core-engine-detail-design.md`
- `sed -n '1,220p' reports/tracker-032-core-design-worker-20260511063428.md`
- `git diff -- Tracker/Tracker.Core/Design/tracker-core-engine-detail-design.md reports/tracker-032-core-design-worker-20260511063428.md`

## 対象ファイル

確認したファイル:

- `/home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
- `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `reports/tracker-032-core-design-worker-20260511063428.md`
- `Tracker/Tracker.Core/TrackerExecutionContracts.cs`
- `Tracker/Tracker.Core/TrackerModelContracts.cs`
- `Tracker/Tracker.Core/TrackerPacketGenerator.cs`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`

変更したファイル:

- `Tracker/Tracker.Core/Design/tracker-core-engine-detail-design.md`
- `reports/tracker-032-core-design-worker-20260511063428.md`

## 指摘事項

- `TrackerExecutionContracts.cs` は 2261 行で、公開契約、engine orchestration、detection buffer、geometry、ball / robot tracking、contact / kick / ball-left-field、Kalman、settings、private state record が混在している。
- `TrackerModelContracts.cs` は 233 行で、行数は小さいが public DTO が多く、日本語コメント追加時は frame、geometry、tracked object、meta state、source detection に分けた方が読みやすい。
- `TrackerPacketGenerator.cs` は 188 行で巨大ではないため、TRACKER-033 では移動と日本語コメント追加を優先し、過剰な partial 分割は避けるのが妥当。
- 対象 Core ファイルには XML documentation comment が現状存在しない。

## 結果

- `Tracker/Tracker.Core/Design/tracker-core-engine-detail-design.md` を新規作成した。
- 設計書には、現状の巨大ファイルと責務の分解候補、分割後の推奨ファイル構成、class / property / method の日本語コメント追加基準、TRACKER-033 の実行順序、挙動を変えないための注意点と検証観点を記載した。
- 実装コードの分割、コメント追加、Server / CLI / UI 詳細設計、test 詳細設計は行っていない。

## リスク

- TRACKER-033 で `TrackerEngine` を `partial` 化する際、private nested record / comparer の配置を誤ると compile error になりやすい。
- 実装移動と日本語コメント追加を一度に広く行うと review が難しくなるため、責務単位で分割し、focused test を挟む必要がある。
- `TrackerPacketGenerator` は小さいため、必要以上に分割すると逆に可読性が下がる。
- 本作業は設計書作成のみであり、dotnet build / test は実行していない。
