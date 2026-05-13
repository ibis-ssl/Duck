# TRACKER-062 review-fix implementation

## 概要

- 種別: review-fix implementation
- 対象 finding: `reports/tracker-062-review-20260513214513.md` の blocking B1
- 結論: Fast Forward 再生中に `等倍速` tab を選んだ場合、selected 表示と実 playback mode が矛盾しないよう `DiagnosticsPlaybackMode.Play` へ切り替える修正を実装した。

## Red

- 追加した regression:
  - `ResolveSpeedChoiceTransition_WhenFastForwardSelectsNormalSpeed_SwitchesToPlay`
  - `ResolveSpeedChoiceTransition_WhenPlaySelectsFastSpeed_SwitchesToFastForward`
  - `ResolveSpeedChoiceTransition_WhenStoppedSelectsNormalSpeed_DoesNotStartPlayback`
- 実行:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter DiagnosticsPlaybackStateTests -m:1 /nr:false`
- 結果:
  - failed
  - `DiagnosticsPlaybackState.ResolveSpeedChoiceTransition` 未定義による `CS0117` を確認した。

## Green

- `DiagnosticsPlaybackState.ResolveSpeedChoiceTransition` を追加し、速度 tab 選択時の selected label、再開 mode、早送り倍率を一か所で決める contract にした。
- `Diagnostics.razor.cs` の `OnPlaybackSpeedChoiceClicked` は同 contract を使い、再生中だけ必要な mode switch / restart を行う。
- Fast Forward 中に `等倍速` を選ぶ場合は `Play` へ切り替える。
- Play 中に fast tab を選ぶ場合は `FastForward` へ切り替え、表示と実 mode を揃える。
- 停止中に `等倍速` を選ぶ場合は選択表示だけを更新し、再生開始しない。

## 変更ファイル

- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- `Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs`
- `Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
- `reports/tracker-062-review-fix-implementation-20260513214808.md`

## 検証

- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter DiagnosticsPlaybackStateTests -m:1 /nr:false`
  - 結果: Passed。27 passed。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "DiagnosticsPlaybackStateTests|TrackerDiagnosticsComparisonViewStateTests|TrackerDiagnosticsReplayTimelineIndexTests" -m:1 /nr:false`
  - 結果: Passed。56 passed。
- `rg -n "1x" Tracker/Tracker.Server/Components/Pages/Diagnostics.razor Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs Tracker/Tracker.Core/Design Tracker/Tracker.Server/README.md`
  - 結果: no hits。
- `git diff --check`
  - 結果: pass。

## 残リスク

- browser manual evidence は未実施。
- full `Tracker.Tests` は実行していない。ユーザー指定の focused / related filter を優先した。
- `Tracker/Tracker.Server/appsettings.json` は既存 dirty のため、変更・revert していない。
