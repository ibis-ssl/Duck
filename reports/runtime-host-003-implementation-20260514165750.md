# Sub-agent実行レポート

## タスク

RUNTIME-HOST-003: diagnostics sample boundary と legacy degraded contract を追加する。

## sub-agentを使う理由

`codex-delegation-executor` と `tdd-executor` に従い、diagnostics sample boundary の Red contract test authoring と Red evidence 取得を bounded な implementation sub-agent に委譲するため。

## 対象範囲

- `Tracker/Tracker.Tests/` 配下の diagnostics sample boundary / legacy degraded contract tests
- 必要に応じた test helper
- `Tracker/Design/tasks-status.md` の RUNTIME-HOST-003 状態同期

期待する Red contract:

- diagnostics sample tick が tracker committed frame cadence / `WorldFrameCommitted` に依存しないことを Red test で固定する。
- Diagnostics `Vision Input` が legacy render snapshot sidecar ではなく diagnostics sample sidecar から復元されることを Red test で固定する。
- 旧 render snapshot sidecar だけを持つ session は unsupported / degraded legacy として扱い、高コストな完全互換 fallback や tick / scrub ごとの sidecar 全再読込を主経路にしないことを Red test で固定する。

## 対象外

- diagnostics sample sidecar の production 実装
- RuntimeHost project scaffold
- DebugHost rename
- AutoRef logic
- 旧 diagnostics log / 旧 render snapshot sidecar 完全互換の復活
- commit / PR update

## 実行コマンド

```bash
sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md
sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md
sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/implementation-executor/SKILL.md
sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md
sed -n '1,260p' Tracker/Design/tasks-status.md
sed -n '1,220p' Tracker/Design/phases-status.md
sed -n '1,260p' Tracker/Design/DebugHost/raw-vision-viewer-plan.md
sed -n '1,320p' Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md
sed -n '1,260p' reports/runtime-host-003-implementation-20260514165750.md
rg --files Tracker/Tracker.Tests | rg 'Diagnostics|Replay|Render|RuntimeHost|Snapshot|Vision'
rg -n "Vision Input|render snapshot|renderSnapshot|sidecar|legacy|degraded|unsupported|ReplayTimeline|WorldFrameCommitted|diagnostics sample|sample" Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs
rg -n "class .*Diagnostics|TrackerDiagnosticsComparison|RenderSnapshot|ReplayTimeline|Sidecar|Alignment|FieldSource|VisionInput" Tracker/Tracker.Server Tracker/Tracker.Core Tracker/Tracker.CaptureReplay Tracker/Tracker.Tests -g '*.cs'
mkdir -p .codex-dotnet-home .codex-nuget-packages
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDiagnosticsSampleBoundaryContractTests -m:1 /nr:false
```

focused test 結果:

- `Tracker.Tests.RuntimeHostDiagnosticsSampleBoundaryContractTests.LoadReplayTimeline_UsesDiagnosticsSampleTicksEvenWhenWorldFrameCommittedDoesNotAdvance`: assertion failure。期待 3 ticks、実際 0 ticks。
- `Tracker.Tests.RuntimeHostDiagnosticsSampleBoundaryContractTests.LoadFieldSourceFrame_ForVisionInputRestoresFromDiagnosticsSampleSidecar`: assertion failure。期待 `Ready`、実際 `VisionInput`。
- `Tracker.Tests.RuntimeHostDiagnosticsSampleBoundaryContractTests.Load_WithOnlyLegacyRenderSnapshotSidecarReportsUnsupportedDegradedLegacy`: assertion failure。期待 error に `unsupported`、実際 `Tracker snapshot metadata was not found.`。

## 対象ファイル

- `Tracker/Tracker.Tests/RuntimeHostDiagnosticsSampleBoundaryContractTests.cs`
- `Tracker/Design/tasks-status.md`
- `reports/runtime-host-003-implementation-20260514165750.md`

## 指摘事項

- production 実装は未実施。
- 既存 `TrackerDiagnosticsComparisonViewStateReader.LoadReplayTimeline(...)` は diagnostics sample sidecar metadata をまだ読まず、tracker snapshot / alignment 経路が無い session では timeline を返さない。
- 既存 `LoadFieldSourceFrame(..., TrackerDiagnosticsFieldSource.VisionInput)` は metadata を読む前に `VisionInput uses the selected render snapshot.` として返るため、diagnostics sample sidecar 由来の復元契約に未到達。
- render snapshot sidecar だけの legacy session は `unsupported / degraded legacy` として明示されず、現状は `Tracker snapshot metadata was not found.` になる。

## 結果

RUNTIME-HOST-003 の Red contract test を `RuntimeHostDiagnosticsSampleBoundaryContractTests` として追加した。focused test project は compile し、指定 filter で 3 tests がすべて assertion failure として失敗した。

失敗は production 実装前の期待どおりで、compile error ではない。

## リスク

- 今回の Red test は future diagnostics sample sidecar schema を test fixture で先に固定しているため、RUNTIME-HOST-007 実装時に production schema と test fixture の命名を再確認する必要がある。
- `Vision Input` の復元は既存 `TrackerDiagnosticsFieldSourceFrame` model では raw vision 用 payload を十分に表現しない可能性がある。Green 実装時に UI render model 側へ橋渡しする場合でも、sample sidecar 主経路であることを崩さないようにする。
- review / commit / Draft PR #17 update は対象外のため未実施。
