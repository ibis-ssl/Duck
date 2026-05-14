# RUNTIME-HOST-007 diagnostics sample interval 設定化レポート

## 対象

ユーザー指摘「制御周期は設定値に持つようにしてください。極力設定値はマジックナンバーとしてコード内にある状態ではなく外に出してください」に対する追加修正。

対象範囲は RUNTIME-HOST-007 の diagnostics sample loop / writer / sidecar 周辺に限定した。

## 修正内容

- `DiagnosticsSampleHostedService` の固定 `TimeSpan.FromMilliseconds(100)` を廃止し、`VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` から周期を解決するようにした。
- `VisionPacketCaptureOptions` に `DiagnosticsSampleIntervalMilliseconds` と既定値定数 `DefaultDiagnosticsSampleIntervalMilliseconds = 100` を追加した。
- 0 以下の値は invalid と扱い、通常 path を止めずに既定値 `100` ms へフォールバックする。
- `Tracker.DebugHost/appsettings.json` に `DiagnosticsSampleIntervalMilliseconds: 100` を追加した。
- `Tracker.DebugHost/README.md` と RuntimeHost / raw-vision design docs に設定名、既定値、invalid 値の扱いを記載した。
- writer / sidecar 周辺の固定値を見直した。`diagnostics-samples.jsonl` は metadata / replay contract の sidecar 名として設計で固定される値であり、実運用で周期調整する値ではないため今回の設定化対象から外した。flush は既存の `FlushEachPacket` 設定を引き続き使う。

## 変更ファイル

- `Tracker/Tracker.DebugHost/Vision/VisionReceiverOptions.cs`
- `Tracker/Tracker.DebugHost/Tracking/DiagnosticsSampleHostedService.cs`
- `Tracker/Tracker.DebugHost/appsettings.json`
- `Tracker/Tracker.DebugHost/README.md`
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `Tracker/Tracker.Tests/VisionReceiverConfigurationResolverTests.cs`
- `reports/runtime-host-007-configurable-sample-interval-20260514191628.md`

## 設定

- 設定名: `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds`
- 既定値: `100`
- 単位: milliseconds
- invalid 値: `0` 以下は既定値 `100` ms へフォールバックする

## テスト / build / diff-check

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~VisionReceiverConfigurationResolverTests|FullyQualifiedName~DiagnosticsSample" -m:1 /nr:false
```

結果: 成功。17 passed。

```bash
DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostDiagnosticsSampleBoundaryContractTests|FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests|FullyQualifiedName~VisionPacketCaptureTests|FullyQualifiedName~DiagnosticsFieldViewFactoryTests|FullyQualifiedName~DiagnosticsPlaybackStateTests|FullyQualifiedName~VisionReceiverConfigurationResolverTests" -m:1 /nr:false
```

結果: 成功。98 passed。

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

## Serena 使用有無

- Serena 使用: あり。
- `initial_instructions` を読了し、`/home/ibis/ssl/IbisDuck` を `activate_project` 済み。
- symbolic tools 使用:
  - `get_symbols_overview`: `DiagnosticsSampleHostedService.cs`、`DiagnosticsSampleCaptureLoop.cs`、`DiagnosticsSampleLog.cs`、`VisionReceiverOptions.cs`、`VisionPacketCaptureSession.cs`
  - `find_symbol`: `DiagnosticsSampleHostedService`、`DiagnosticsSampleCaptureLoop`、`DiagnosticsSampleLogWriter`、`VisionPacketCaptureOptions`、`VisionPacketCaptureSession` constructor
  - `search_for_pattern`: diagnostics sample interval / hosted service / PacketCapture / sidecar 周辺の固定値確認

## 未実施

- commit / PR / tracking final sync / r2 review はユーザー指定どおり未実施。
