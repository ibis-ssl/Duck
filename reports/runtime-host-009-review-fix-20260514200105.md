# RUNTIME-HOST-009 review-fix レポート

## 対象

`RuntimeTrackerConfigurationResolver` の missing active profile fallback 修正。

## 変更内容

- `Tracker/Tracker.RuntimeHost/RuntimeTrackerConfigurationResolver.cs`
  - `Tracker:ActiveProfileName` が `Tracker:Profiles` に存在しない場合、`default` profile へ silently fallback せず、DebugHost `TrackerConfigurationResolver` と同じ形式の `InvalidOperationException` を投げるように変更した。
  - empty / blank `ActiveProfileName` は従来どおり `default` profile 名として扱う。ただし `default` profile が存在しない場合は `Tracker active profile 'default' was not found in Tracker:Profiles.` で明示失敗する。
- `Tracker/Tracker.Tests/RuntimeHostOperationLoopTests.cs`
  - RuntimeHost の missing active profile が `TrackerRuntimeResolvedOptions` 解決時に失敗する regression test を追加した。
  - blank active profile が `default` 扱いのまま、`default` profile がない場合に明示失敗する regression test を追加した。

## Red proof

- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostTrackerOptions_WithMissingActiveProfile_Throws" -m:1 /nr:false`
  - 修正前に実行。
  - 1 failed。
  - `Assert.Throws() Failure: No exception was thrown` により、missing active profile が失敗していないことを確認した。

## 検証

- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostOperationLoopTests" -m:1 /nr:false`
  - Passed。5 passed。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false`
  - Build succeeded。0 warnings / 0 errors。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`
  - Build succeeded。0 warnings / 0 errors。
- `git diff --check`
  - Passed。

## Serena 使用有無

- Serena 使用あり。
- 作業開始時に Serena MCP `initial_instructions` を読み、`/home/ibis/ssl/IbisDuck` を activate した。
- `check_onboarding_performed` では onboarding 未実施と表示されたため `onboarding` を起動した。ただし今回 scope 外の Serena memory 書き込みは行っていない。
- コード調査では `get_symbols_overview` / `find_symbol` / `search_for_pattern` で `RuntimeTrackerConfigurationResolver`、`RuntimeHostOperationLoopTests`、DebugHost `TrackerConfigurationResolver`、既存 `TrackerConfigurationBindingTests` を確認した。
- コード編集では Serena `insert_after_symbol` と `replace_symbol_body` を使用した。
- 追加後に Serena `get_diagnostics_for_file` で変更対象 2 ファイルの error / warning がないことを確認した。

## 残リスク

- RuntimeHost の実 UDP receive / publish を使った manual evidence は RUNTIME-HOST-010 scope として残る。
- 既存 review report に記録されている DebugHost read-side ownership marker の out-of-scope failure は今回触っていない。
- `RuntimeTrackerConfigurationResolver.cs` と `RuntimeHostOperationLoopTests.cs` は既存 R009 の未追跡ファイルであり、今回の修正はそのファイル内に重ねた。既存 R009 差分の commit / PR / tracking sync は実施していない。
