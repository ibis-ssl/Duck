# Sub-agent実行レポート

## タスク

RUNTIME-HOST-002: RuntimeHost / DebugHost project dependency boundary contract を追加する。

## sub-agentを使う理由

`codex-delegation-executor` と `tdd-executor` に従い、test authoring と Red evidence 取得を bounded な implementation sub-agent に委譲するため。

## 対象範囲

- `Tracker/Tracker.Tests/` 配下の RuntimeHost / DebugHost dependency boundary contract tests
- 必要に応じた test helper
- `Tracker/Design/tasks-status.md` の RUNTIME-HOST-002 状態同期

期待する Red contract:

- `Tracker.RuntimeHost` が `Tracker.DebugHost` / `Tracker.Server` / Web UI / diagnostics replay UI project を参照しないことを project reference / dependency test で固定する。
- RuntimeHost 側 code が diagnostics logging / replay / Blazor UI namespace を直接呼ばないことを contract test で固定する。
- DebugHost が tracker operation loop を主実行責務として持たず、latest immutable snapshot または published output を読む側であることを test 名と assertion で固定する。
- 現時点で RuntimeHost / DebugHost 分離実装が未完了のため、focused test は意図通り Red になることを evidence として残す。

## 対象外

- RuntimeHost project scaffold の実装
- `Tracker.Server` から `Tracker.DebugHost` への rename 実装
- diagnostics sample sidecar 実装
- AutoRef logic 実装
- 旧 diagnostics log / 旧 render snapshot sidecar 完全互換の復活
- commit / PR update

## 実行コマンド

```bash
mkdir -p .codex-dotnet-home .codex-nuget-packages
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDependencyBoundaryContractTests -m:1 /nr:false
```

結果: failed。`RuntimeHostDependencyBoundaryContractTests` 3 件が assertion failure として Red になった。

## 対象ファイル

- `Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs`
- `Tracker/Design/tasks-status.md`
- `reports/runtime-host-002-implementation-20260514163841.md`

## 指摘事項

なし。今回は task 専用 review ではなく、RUNTIME-HOST-002 の Red contract test authoring と Red evidence 取得のみを実施した。

## 結果

- `RuntimeHostProject_DoesNotReferenceDebugHostServerBlazorOrDiagnosticsReplayProjects`: `Tracker.RuntimeHost` project が未存在のため assertion failure。
- `RuntimeHostSource_DoesNotDirectlyReferenceDiagnosticsReplayOrBlazorUiNamespaces`: `Tracker.RuntimeHost` source root が未存在のため assertion failure。
- `DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop`: `Tracker.DebugHost` root が未存在で、現状は `Tracker.Server` のままのため assertion failure。

focused test は compile error ではなく、意図した Red contract の assertion failure として失敗した。実装 scope は contract tests 追加までに留め、RuntimeHost scaffold / DebugHost rename / diagnostics sample sidecar / AutoRef logic は実装していない。

## リスク

- `Tracker.RuntimeHost` / `Tracker.DebugHost` の実体が後続 task で追加された後、read-side boundary の具体名が test の marker とずれる可能性がある。その場合も contract の意図は「DebugHost が tracker operation loop を主実行しないこと」であり、後続実装時に assertion を実装名へ合わせる必要がある。
- review / commit / Draft PR #17 update はこの sub-agent scope 外のため未実施。
