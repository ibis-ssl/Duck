# Sub-agent実行レポート

## タスク

RUNTIME-HOST-004: `Tracker.Server` を `Tracker.DebugHost` project / namespace / 起動経路へ rename する。

## sub-agentを使う理由

`codex-delegation-executor` と `implementation-executor` に従い、project rename、namespace/reference 更新、focused Red/Green evidence を bounded な implementation sub-agent に委譲するため。

## 対象範囲

- `Tracker/Tracker.Server` から `Tracker/Tracker.DebugHost` への project / folder / namespace / launch path rename
- solution / project reference / README / launch settings / appsettings logger category の更新
- RUNTIME-HOST-004 の focused contract tests
- `Tracker/Design/tasks-status.md` の RUNTIME-HOST-004 evidence 同期

## 対象外

- RuntimeHost project scaffold
- tracker operation loop 抽出
- diagnostics sample sidecar production 実装
- AutoRef logic
- 既存 debug UI の機能変更
- commit / PR update

## 実行コマンド

- `mkdir -p .codex-dotnet-home .codex-nuget-packages`
- Red: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDebugHostRenameContractTests -m:1 /nr:false`
  - 結果: 3 failed / 0 passed。`Tracker.DebugHost` folder/project 未存在、solution / project reference 未更新を assertion failure として確認した。
- Green: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDebugHostRenameContractTests -m:1 /nr:false`
  - 結果: 3 passed / 0 failed。
- Build: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false`
  - 結果: Build succeeded / 0 warnings / 0 errors。
- Build: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -m:1 /nr:false`
  - 結果: Build succeeded / 0 warnings / 0 errors。
- `git diff --check`
  - 結果: 成功。

## 対象ファイル

- 追加:
  - `Tracker/Tracker.Tests/RuntimeHostDebugHostRenameContractTests.cs`
- rename / namespace 更新:
  - `Tracker/Tracker.Server/**` -> `Tracker/Tracker.DebugHost/**`
  - `Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj`
  - `Tracker/Tracker.DebugHost/Program.cs`
  - `Tracker/Tracker.DebugHost/Components/**`
  - `Tracker/Tracker.DebugHost/Tracking/**`
  - `Tracker/Tracker.DebugHost/Vision/**`
  - `Tracker/Tracker.DebugHost/Properties/launchSettings.json`
  - `Tracker/Tracker.DebugHost/appsettings.json`
  - `Tracker/Tracker.DebugHost/README.md`
- 参照更新:
  - `Duck.slnx`
  - `README.md`
  - `Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj`
  - `Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs`
  - `Tracker/Tracker.CaptureReplay/ReplayOptions.cs`
  - `Tracker/Tracker.CaptureReplay/ReplaySettingsOptions.cs`
  - `Tracker/Tracker.Tests/Tracker.Tests.csproj`
  - `Tracker/Tracker.Tests/*`
- 進捗同期:
  - `Tracker/Design/tasks-status.md`

## 指摘事項

- blocking 指摘なし。
- `RuntimeHostDependencyBoundaryContractTests` の旧名禁止トークンは意図した contract のため残した。
- `Tracker/Design/Archive/**` と歴史説明の `Tracker.Server` 記述は機械的置換対象外として維持した。

## 結果

- `Tracker.Server` の active project / namespace / launch path を `Tracker.DebugHost` へ rename した。
- `Tracker.CaptureReplay` と `Tracker.Tests` の project reference と namespace using を `Tracker.DebugHost` へ更新した。
- repository root README と DebugHost README の起動手順、`appsettings.json` の logger category、Blazor CSS isolation asset 名を `Tracker.DebugHost` へ更新した。
- RUNTIME-HOST-004 focused contract test は Red で旧状態の不足を固定し、rename 後 Green になった。
- DebugHost build と CaptureReplay build は成功した。

## リスク

- task 専用 review、commit、Draft PR #17 update は親 workflow 側の後続 gate として未実施。
- full `Tracker.Tests` は RUNTIME-HOST-002 / RUNTIME-HOST-003 の既存 Red contract を含むため、この implementation sub-agent では実行していない。
- large rename のため git status 上は delete/add が多数出る。commit 時は rename として扱われるか確認が必要。
