# TRACKER-018 Evidence

## 目的

- Tracker v1 の build/test 証跡を取得し、verification フェーズの current status を固定する

## 実行コマンド

- `dotnet build Duck.slnx --no-restore --disable-build-servers`
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --disable-build-servers`
- `dotnet build Duck.slnx --no-restore --disable-build-servers -v normal`
- `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --no-restore --disable-build-servers`
- `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --disable-build-servers`
- `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --disable-build-servers`
- `dotnet restore SslProto/SslProto.csproj --disable-build-servers -v normal`
- `dotnet build SslProto/SslProto.csproj --no-restore --disable-build-servers`
- `dotnet build Tracker/Tracker.Core/Tracker.Core.csproj --no-restore --disable-build-servers`
- `dotnet restore Tracker/Tracker.Core/Tracker.Core.csproj --disable-build-servers -v normal`
- `dotnet restore Duck.slnx --disable-build-servers -v normal`
- `dotnet restore Tracker/Tracker.Server/Tracker.Server.csproj --disable-build-servers -v normal`
- `dotnet restore Tracker/Tracker.Server/Tracker.Server.csproj --disable-build-servers -v diag > /tmp/tracker-server-restore-diag.log 2>&1`
- `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --no-restore --disable-build-servers -v diag > /tmp/tracker-server-build-diag.log 2>&1`
- `rg -n "FAILED|error|Error|Exception|warning|Warning|GetTargetFrameworks|Task \\\"MSBuild\\\"" /tmp/tracker-server-build-diag.log`
- `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --no-restore --disable-build-servers -m:1 -p:BuildInParallel=false`
- `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --disable-build-servers -m:1 -p:BuildInParallel=false`
- `dotnet build Duck.slnx --no-restore --disable-build-servers -m:1 -p:BuildInParallel=false`

## 結果概要

- `Tracker.Tests` の full test は成功した。`Passed: 91 / Failed: 0 / Skipped: 0`
- `SslProto` と `Tracker.Core` の単体 build は成功した
- `Duck.slnx`、`Tracker.Server.csproj`、`Tracker.Tests.csproj` の build は既定並列設定だと `0 Warning(s) / 0 Error(s)` のまま exit code 1 で失敗した
- `Tracker.Server.csproj` の restore も `0 Warning(s) / 0 Error(s)` のまま exit code 1 で失敗した
- `-m:1 -p:BuildInParallel=false` を付けた build は `Duck.slnx` / `Tracker.Server.csproj` / `Tracker.Tests.csproj` のすべてで成功した

## 詳細

### test

- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --disable-build-servers`
  - 結果: 成功
  - 出力要約: `Tracker.Tests.dll (net10.0)` で `Passed: 91 / Failed: 0 / Skipped: 0`
  - 含意: contract / engine / integration / UI view-state を含む既存 test suite は current branch で通過している

### build success

- `dotnet build SslProto/SslProto.csproj --no-restore --disable-build-servers`
  - 結果: 成功
- `dotnet build Tracker/Tracker.Core/Tracker.Core.csproj --no-restore --disable-build-servers`
  - 結果: 成功

### build / restore anomaly

- `dotnet build Duck.slnx --no-restore --disable-build-servers`
  - 結果: 失敗
  - 出力要約: `ValidateSolutionConfiguration` 後に即終了し、明示 error line は出なかった
- `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --no-restore --disable-build-servers`
  - 結果: 失敗
  - 出力要約: `0 Warning(s) / 0 Error(s)` のまま exit code 1
- `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --disable-build-servers`
  - 結果: 失敗
  - 出力要約: `0 Warning(s) / 0 Error(s)` のまま exit code 1
- `dotnet restore Tracker/Tracker.Server/Tracker.Server.csproj --disable-build-servers -v diag`
  - 結果: 失敗
  - 出力要約: `Tracker.Core.csproj` の `_GenerateRestoreProjectPathWalk` が `Tracker.Server` 配下の restore graph から呼ばれた時だけ失敗していた
- `rg -n "FAILED|error|Error|Exception|warning|Warning|GetTargetFrameworks|Task \\\"MSBuild\\\"" /tmp/tracker-server-build-diag.log`
  - 確認事項: `Tracker.Server` build の `_GetProjectReferenceTargetFrameworkProperties` から `Tracker.Core.csproj` の `GetTargetFrameworks` 呼び出しが失敗していた
  - 補足: 同じ `Tracker.Core.csproj` は単体 build / restore では成功している

### workaround

- `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --no-restore --disable-build-servers -m:1 -p:BuildInParallel=false`
  - 結果: 成功
- `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --disable-build-servers -m:1 -p:BuildInParallel=false`
  - 結果: 成功
- `dotnet build Duck.slnx --no-restore --disable-build-servers -m:1 -p:BuildInParallel=false`
  - 結果: 成功
- 解釈: current environment では parallel project graph build が不安定で、single-process / non-parallel project reference build に落とすと正常完走した

## リスク

- current environment では既定の並列 build が不安定で、安定実行には `-m:1 -p:BuildInParallel=false` が必要
- 一方で full test は通過しており、少なくとも current code path は test 実行経路ではコンパイル・実行できている
- 次タスクでは integration 観点 verification に進めるが、最終 release 判断までに build 実行条件として上記 workaround を使う前提を明示して扱う必要がある
