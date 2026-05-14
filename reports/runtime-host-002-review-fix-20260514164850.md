# Sub-agent実行レポート

## タスク

RUNTIME-HOST-002 review finding の修正。

## sub-agentを使う理由

RUNTIME-HOST-002 の task review で blocking finding が出たため、bounded な implementation sub-agent に test helper 修正と Red evidence 再取得を委譲するため。

## 対象範囲

- `Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs`
- `reports/runtime-host-002-review-20260514164528.md`
- focused Red test evidence

## 対象外

- RuntimeHost project scaffold
- DebugHost rename
- diagnostics sample sidecar 実装
- AutoRef logic
- commit / PR update

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/implementation-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `rg -n "RUNTIME-HOST-002|RuntimeHostDependencyBoundaryContractTests|runtime-host-002|RuntimeHost" /home/ibis/.codex/memories/MEMORY.md`
- `git status --short`
- `sed -n '1,240p' reports/runtime-host-002-review-20260514164528.md`
- `sed -n '1,260p' reports/runtime-host-002-review-fix-20260514164850.md`
- `sed -n '1,260p' reports/runtime-host-002-boundary-context-20260514164124.md`
- `sed -n '1,260p' Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs`
- `mkdir -p .codex-dotnet-home .codex-nuget-packages`
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDependencyBoundaryContractTests -m:1 /nr:false`
- `git diff -- Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs reports/runtime-host-002-review-fix-20260514164850.md`
- `nl -ba Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs | sed -n '1,190p'`
- `nl -ba reports/runtime-host-002-review-fix-20260514164850.md`

## 対象ファイル

- 変更:
  - `Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs`
  - `reports/runtime-host-002-review-fix-20260514164850.md`
- 読み取り:
  - `reports/runtime-host-002-review-20260514164528.md`
  - `reports/runtime-host-002-boundary-context-20260514164124.md`
  - `/home/ibis/AI/CodexSkill/skills/implementation-executor/SKILL.md`
  - `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`

## 指摘事項

- blocking finding のとおり、従来の `ProjectReference Include` 判定は `\` を `/` に置換するだけで、`..\Tracker.Server\Tracker.Server.csproj` や `..\Tracker.DebugHost\Tracker.DebugHost.csproj` を `Tracker/Tracker.Server/...` suffix として検出できない false negative があった。
- `ProjectReference Include` を `Tracker.RuntimeHost.csproj` の directory 基準で full path に解決し、禁止 project も repository root から full path 化して、同じ normalized absolute path で完全一致比較するように修正した。
- Linux 上で `..\Tracker.Server\...` の backslash が path separator として解決されない問題を避けるため、full path 解決前に `\` と `/` を `Path.DirectorySeparatorChar` に寄せている。

## 結果

- focused test は compile error ではなく、意図した Red assertion failure として再取得できた。
- 結果は `Failed: 3, Passed: 0, Skipped: 0, Total: 3`。
- 失敗理由は現在 `Tracker.RuntimeHost` project、`Tracker.RuntimeHost` source root、`Tracker.DebugHost` root が未存在であること。
- 代表 failure:
  - `RuntimeHostProject_DoesNotReferenceDebugHostServerBlazorOrDiagnosticsReplayProjects`: `RUNTIME-HOST-002 contract requires Tracker.RuntimeHost project to exist before dependency boundaries can be checked.`
  - `RuntimeHostSource_DoesNotDirectlyReferenceDiagnosticsReplayOrBlazorUiNamespaces`: `RUNTIME-HOST-002 contract requires Tracker.RuntimeHost source root to exist before source boundary references can be checked.`
  - `DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop`: `RUNTIME-HOST-002 contract requires Tracker.DebugHost to replace the current Tracker.Server debug UI host before read-side boundaries can be checked.`

## リスク

- `Tracker.RuntimeHost` / `Tracker.DebugHost` がまだ未存在のため、今回修正した forbidden `ProjectReference` 判定は focused Red test の現失敗地点より後で評価される。後続 scaffold 後に同 test を再実行して、`..\Tracker.Server\...` / `..\Tracker.DebugHost\...` が実際に検出されることを確認する必要がある。
- `ProjectReference Include` に MSBuild property expression が含まれる場合は、現在の path 解決では property 展開しない。RUNTIME-HOST-002 の sibling project boundary contract としては現時点で許容する。
