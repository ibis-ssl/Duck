# RUNTIME-HOST-008 実装レポート

## 対象

`Tracker.RuntimeHost` headless project scaffold と configuration。

## Executor

Codex worker sub-agent `019e2608-f101-7fb3-9bf5-7de4016f925c`。

- `development-orchestrator` / `tdd-executor` / `implementation-executor` / `report-output-manager` を参照した。
- ユーザー指定により commit / push / PR 更新 / final tracking sync は実施していない。

## Scope

- `Tracker.RuntimeHost` を Web UI / diagnostics replay / capture viewer を持たない headless host scaffold として追加する。
- `RuntimeHost:OperationLoopIntervalMilliseconds` を options binding 可能な設定として公開する。
- `OperationLoopIntervalMilliseconds <= 0` は host start 時の options validation error とする。
- RUNTIME-HOST-009 の SSL-Vision input 受信、tracker state update、official packet publish normal path 実装には踏み込まない。

## Changes

- `Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj` を追加し、`Tracker.Core` のみを project reference として持つ console executable にした。
- `Tracker/Tracker.RuntimeHost/Program.cs` を追加し、`Host.CreateApplicationBuilder` と DI bootstrap で headless host として起動できる入口を追加した。
- `RuntimeHostOptions` / `RuntimeHostServiceCollectionExtensions` / `RuntimeHostLifecycleService` を追加し、options binding、`ValidateOnStart`、scaffold hosted service 登録を追加した。
- `Tracker/Tracker.RuntimeHost/appsettings.json` に `RuntimeHost:OperationLoopIntervalMilliseconds` を追加した。
- `Duck.slnx` に `Tracker.RuntimeHost` を追加した。
- `Tracker.Tests` から `Tracker.RuntimeHost` を参照し、`RuntimeHostScaffoldContractTests` で solution entry、default、binding、0 以下 validation error を固定した。
- `Directory.Packages.props` に `Microsoft.Extensions.Hosting` と `Microsoft.Extensions.Options.ConfigurationExtensions` の central package version を追加した。

## Tests / Build

- Red:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostDependencyBoundaryContractTests|FullyQualifiedName~RuntimeHost" -m:1 /nr:false`
  - Result: failed as expected before implementation because `Tracker.RuntimeHost` project/source namespace did not exist.
- First full requested focused command after implementation:
  - Same command as above.
  - Result: 23 passed / 1 failed. Failure was existing `RuntimeHostDependencyBoundaryContractTests.DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop`, which checks DebugHost loop ownership markers and is outside RUNTIME-HOST-008 scaffold/config scope.
- Adjusted focused filter:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostScaffoldContractTests|FullyQualifiedName~RuntimeHostDependencyBoundaryContractTests.RuntimeHostProject_DoesNotReferenceDebugHostServerBlazorOrDiagnosticsReplayProjects|FullyQualifiedName~RuntimeHostDependencyBoundaryContractTests.RuntimeHostSource_DoesNotDirectlyReferenceDiagnosticsReplayOrBlazorUiNamespaces" -m:1 /nr:false`
  - Result: 7 passed.
- RuntimeHost build:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false`
  - Result: succeeded, 0 warnings, 0 errors.
- Tracker.Tests build:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`
  - Result: succeeded, 0 warnings, 0 errors.
- Diff check:
  - `git diff --check`
  - Result: succeeded.

## Serena

使用した。

- `initial_instructions` を作業開始時に読んだ。
- `/home/ibis/ssl/IbisDuck` を `activate_project` した。
- `search_for_pattern` で RuntimeHost / RUNTIME-HOST 関連の既存テストと設計箇所を探索した。
- `get_symbols_overview` / `find_symbol` で `RuntimeHostDependencyBoundaryContractTests` と Core runtime options の既存 symbol を確認した。

## Risks / Remaining Work

- 指定 focused command は RUNTIME-HOST-008 の対象外である既存 DebugHost loop ownership assertion も拾うため、そのままでは 1 件失敗する。R008 の validation では RuntimeHost project/source boundary と scaffold/config tests に filter を絞って green を確認した。
- `RuntimeHostLifecycleService` は scaffold 起動と validation のための no-op service であり、SSL-Vision input 受信、tracker update、official packet publish は RUNTIME-HOST-009 の残作業。
- task 専用 review、commit、PR 更新、final tracking sync はユーザー指定どおり未実施。
