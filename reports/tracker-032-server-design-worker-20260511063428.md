# Sub-agent実行レポート

## タスク

TRACKER-032 Server / CLI / UI 詳細設計の作成。

## sub-agentを使う理由

ユーザー指示により、調査と設計書作成は sub-agent に委譲し、親 Codex は manager として report を見て判断するため。

## 対象範囲

- `Tracker/Tracker.CaptureReplay/Program.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `Tracker/Tracker.Server/Tracking/*`
- `Tracker/Tracker.Server/Vision/*`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- 新規または更新する Server / CLI / UI 詳細設計ファイル

## 対象外

- Core engine の詳細設計
- test file の分割設計
- 実装コードの分割
- development-orchestrator の再実行
- nested Codex / codex exec / 追加 sub-agent 起動

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `rg -n "TRACKER-032|server design|tracker-server-cli-ui|巨大ファイル|Diagnostics" /home/ibis/.codex/memories/MEMORY.md`
- `sed -n '1,240p' reports/tracker-032-server-design-worker-20260511063428.md`
- `ls -l Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `git status --short`
- `wc -l Tracker/Tracker.CaptureReplay/Program.cs Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs Tracker/Tracker.Server/Components/Pages/Diagnostics.razor Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `find Tracker/Tracker.Server/Tracking Tracker/Tracker.Server/Vision -maxdepth 1 -type f | sort`
- `rg -n "TRACKER-032|巨大|分割|コメント|CaptureReplay|TrackerCoordinator|Diagnostics" Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `sed -n '240,340p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `sed -n '430,535p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `sed -n '590,715p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `sed -n '1030,1105p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `rg -n "^(internal|private|public|static|sealed|record|class|enum)|^async|^await|^var |^using|^namespace|^static .*\\(|^[A-Z][A-Za-z0-9_]+\\(" Tracker/Tracker.CaptureReplay/Program.cs`
- `rg -n "^(public|internal|private|protected|sealed|class|record|enum|readonly|static)|^    (public|private|internal|protected).*\\(|^    (public|private|internal|protected).*\\{|^[{}]" Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `rg -n "^@|<PageTitle|<div|<section|@code|private|protected override|public|record|class|Render|Load|Select|Refresh|Format|Toggle|Capture|Diagnostics|Snapshot" Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `sed -n '1,120p' Tracker/Tracker.CaptureReplay/Program.cs`
- `sed -n '120,360p' Tracker/Tracker.CaptureReplay/Program.cs`
- `sed -n '360,700p' Tracker/Tracker.CaptureReplay/Program.cs`
- `sed -n '700,1010p' Tracker/Tracker.CaptureReplay/Program.cs`
- `sed -n '1,220p' Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `sed -n '220,470p' Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `sed -n '470,690p' Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `wc -l Tracker/Tracker.Server/Tracking/*.cs Tracker/Tracker.Server/Vision/*.cs | sort -nr | head -30`
- `sed -n '1,220p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `sed -n '220,460p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `sed -n '460,640p' Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `find Tracker/Tracker.Core/Design -maxdepth 1 -type f | sort`
- `rg -n "^#|^##|TRACKER-0|詳細設計|巨大ファイル|コメント" Tracker/Tracker.Core/Design/*.md`
- `sed -n '990,1028p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `sed -n '1,120p' Tracker/Tracker.Core/Design/tasks-status.md`
- `sed -n '1,80p' Tracker/Tracker.Core/Design/phases-status.md`

## 対象ファイル

- 確認ファイル:
  - `Tracker/Tracker.CaptureReplay/Program.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
  - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - `Tracker/Tracker.Server/Tracking/*`
  - `Tracker/Tracker.Server/Vision/*`
  - `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `Tracker/Tracker.Core/Design/tasks-status.md`
  - `Tracker/Tracker.Core/Design/phases-status.md`
  - `/home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
  - `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- 変更ファイル:
  - `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `reports/tracker-032-server-design-worker-20260511063428.md`

## 指摘事項

- `Tracker.CaptureReplay/Program.cs` は 1001 行で、entrypoint、runner、capture reader、settings factory、DTO、CLI parser、condition parser が同居している。`Program.cs` は entrypoint に縮小し、責務別に分割する必要がある。
- `Tracker.Server/Tracking/TrackerCoordinator.cs` は 672 行で、packet 処理、profile switch drain、event dispatch、diagnostics log、publish、clone/equality helper が同居している。profile switch と event dispatch の順序を維持しながら協力型へ分ける必要がある。
- `Tracker.Server/Components/Pages/Diagnostics.razor` は 613 行で、markup、page state、log loading、timeline、render snapshot、metadata modal、geometry 変換が同居している。markup を維持しつつ partial class と helper へ分ける必要がある。
- `TrackerDiagnosticsLogReader.cs` と `VisionReceiverService.cs` も中規模だが、`TRACKER-034` では主対象 3 ファイルを優先し、中規模ファイルはコメント補強と依存確認に留めるのが安全。

## 結果

- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md` を新規作成した。
- 設計書には、現状の巨大ファイル一覧、CaptureReplay / TrackerCoordinator / Diagnostics UI の推奨分割、class / property / method の日本語コメント追加基準、`TRACKER-034` の実行順序、UI / capture / diagnostics の挙動維持の注意点と検証観点を記載した。
- 実装コード、Core engine 詳細設計、test 詳細設計、tracking files は変更していない。

## リスク

- 今回は詳細設計のみで build / test は実行していない。
- `TrackerCoordinator` の分割では profile switch、publisher 設定反映、snapshot clear、observer 通知の順序を崩すリスクが最も高い。
- CaptureReplay の標準出力と error message は調査・自動検証で使われる可能性が高く、文言整理でも互換性リスクがある。
- Diagnostics UI の partial 化では `selectedEntry`、`profileMetadata`、`selectedRenderSnapshot` の同期順序を崩すと画面挙動が変わる。
