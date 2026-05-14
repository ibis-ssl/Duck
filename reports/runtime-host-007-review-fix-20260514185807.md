# RUNTIME-HOST-007 review-fix レポート

## 対象

`reports/runtime-host-007-review-20260514184501.md` の blocking findings 2 件と Stop ownership hold を対象にした修正。

## 修正方針

### Blocking 1: diagnostics sample writer の UI 依存解消

- `Home.razor` から `DiagnosticsSampleLogWriter` の injection と `CaptureLiveDisplaySnapshot()` 内の `CaptureSample()` 呼び出しを外した。
- `DiagnosticsSampleCaptureLoop` と `DiagnosticsSampleHostedService` を追加し、DebugHost 側の hosted service が CaptureOn 中に UI 表示有無と無関係に `VisionLiveDisplaySnapshotProvider` から sample を固定して `diagnostics-samples.jsonl` に追記する経路へ移した。
- `runtime-host-plan.md` の RUNTIME-HOST-007 記述を Home refresh tick ではなく UI 非依存 diagnostics sample loop に修正した。

### Blocking 2: Diagnostics UI field rendering の sample semantic summary 化

- `TrackerDiagnosticsComparisonUiState` は `Vision Input` / `ibis tracker` も reader から `TrackerDiagnosticsFieldSourceFrame` を読むようにした。
- `TrackerDiagnosticsComparisonViewStateReader` は diagnostics sample sidecar がない場合、`Vision Input` / `ibis tracker` を旧 render snapshot fallback に戻さず `SidecarUnavailable` / `CandidateMissing` として返す。
- `Diagnostics.razor.cs` と `DiagnosticsFieldOverlayRenderModelFactory` は `Vision Input` / `ibis tracker` の balls / robots も `TrackerDiagnosticsFieldSourceFrame.SemanticSummary` から描画するようにした。

### Stop ownership

- `DiagnosticsSampleLogWriter.Stop()` は writer handle と writer-local counters だけを reset し、shared `VisionPacketCaptureSession.Stop()` を呼ばないようにした。
- CaptureOff 時は従来どおり `Home.razor` が packet/render writer を止めるため、shared session 停止経路は保持される。

## 変更ファイル

- `Tracker/Tracker.DebugHost/Tracking/DiagnosticsSampleLog.cs`
- `Tracker/Tracker.DebugHost/Tracking/DiagnosticsSampleCaptureLoop.cs`
- `Tracker/Tracker.DebugHost/Tracking/DiagnosticsSampleHostedService.cs`
- `Tracker/Tracker.DebugHost/Program.cs`
- `Tracker/Tracker.DebugHost/Tracking/TrackerDiagnosticsComparisonUiState.cs`
- `Tracker/Tracker.DebugHost/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
- `Tracker/Tracker.DebugHost/Components/Pages/Diagnostics.razor.cs`
- `Tracker/Tracker.DebugHost/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs`
- `Tracker/Tracker.Tests/RuntimeHostDiagnosticsSampleBoundaryContractTests.cs`
- `Tracker/Tracker.Tests/VisionPacketCaptureTests.cs`
- `Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs`
- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`

`Home.razor` は review-fix 作業で sample writer 依存を除去し、現時点では HEAD 差分なし。

## 検証結果

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostDiagnosticsSampleBoundaryContractTests|FullyQualifiedName~DiagnosticsSample" -m:1 /nr:false
```

結果: 成功。8 passed。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests|FullyQualifiedName~VisionPacketCaptureTests|FullyQualifiedName~DiagnosticsFieldViewFactoryTests|FullyQualifiedName~DiagnosticsPlaybackStateTests" -m:1 /nr:false
```

結果: 成功。84 passed。

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

## 未完了点

- commit / PR / tracking final sync はユーザー指定どおり未実施。
