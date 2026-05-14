# RUNTIME-HOST-007 検証レポート

## 対象

DebugHost diagnostics sample sidecar fast path 実装後の親 agent 検証。

## 結果

- R003/R007 focused tests: 成功。
- affected diagnostics / capture tests: 成功。
- `Tracker.DebugHost` build: 成功。
- `Tracker.Tests` build: 成功。
- `git diff --check`: 成功。

## 実行コマンド

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostDiagnosticsSampleBoundaryContractTests|FullyQualifiedName~DiagnosticsSample" -m:1 /nr:false
```

結果: 4 passed。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests|FullyQualifiedName~VisionPacketCaptureTests|FullyQualifiedName~DiagnosticsFieldViewFactoryTests|FullyQualifiedName~DiagnosticsPlaybackStateTests" -m:1 /nr:false
```

結果: 82 passed。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false
```

結果: 成功。0 warnings / 0 errors。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false
```

結果: 成功。0 warnings / 0 errors。

```bash
git diff --check
```

結果: 成功。

## 親 agent 所見

実装は R003 の diagnostics sample boundary contract と writer metadata focused test を green にしている。

レビューでは、`Home.razor` refresh tick で diagnostics sample を保存する設計が、`raw-vision-viewer-plan.md` の「UI render tick は diagnostics logging cadence を決めない」という記述および performance priority と矛盾しないかを明示確認する。
