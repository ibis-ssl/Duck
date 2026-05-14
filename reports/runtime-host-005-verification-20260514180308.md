# RUNTIME-HOST-005 検証レポート

## タスク

`Tracker.DebugHost` から tracker operation loop の共有 runtime boundary を抽出し、`Tracker.Core/Runtime` の UI 非依存実装として動作することを確認した。

## 検証対象

- `Tracker/Tracker.Core/Runtime`
- `Tracker/Tracker.Tests/RuntimeHostSharedOperationLoopBoundaryTests.cs`
- 既存 coordinator / profile switch / diagnostics capture 境界 tests
- `Tracker.Tests` project build

## 実行コマンド

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostSharedOperationLoopBoundaryTests|FullyQualifiedName~TrackerCoordinatorFrameFlowTests|FullyQualifiedName~TrackerCoordinatorResetAndProfileTests|FullyQualifiedName~TrackerProfileRequestServiceTests|FullyQualifiedName~TrackerCoordinatorDiagnosticsCaptureTests" -m:1 /nr:false
```

結果: 成功。15 passed / 0 failed / 0 skipped。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false
```

結果: 成功。0 warnings / 0 errors。

```bash
git diff --check
```

結果: 成功。出力なし。

## 確認結果

- Core runtime source が DebugHost / Blazor / diagnostics writer / capture writer 境界を参照しない contract は green。
- shared `TrackerCoordinator` は `Tracker.Core` assembly / namespace に存在する。
- committed frame の latest snapshot 更新、official packet publish、observer event order は green。
- profile switch の control-only drain、publisher config 反映、snapshot clear の順序は green。
- publisher 例外時に loop を落とさず publish failure count を増やす contract は green。
- 旧 DebugHost diagnostics file / render snapshot sidecar 生成は Core loop から外れている。新 diagnostics sample sidecar は RUNTIME-HOST-007 の対象として残る。

## リスク

- RUNTIME-HOST-005 後、DebugHost の旧 coordinator diagnostics logging は operation loop から外れているため、旧 diagnostics log 互換を前提にした運用は対象外となる。
- `Tracker.RuntimeHost` project はまだ存在しないため、RuntimeHost DI 結合は RUNTIME-HOST-008 で別途確認する。
