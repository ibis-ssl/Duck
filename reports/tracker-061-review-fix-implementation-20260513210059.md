# TRACKER-061 review follow-up 実装レポート

## 概要

- 対象: TRACKER-061 review follow-up N1
- 目的: active playback choice の visible / title / aria label で English `Stop` を使わず、自然な日本語 `停止` を表示する
- 対象外: saved alignment v2、comparison、replay timeline、playback stepping、既存 dirty `Tracker/Tracker.Server/appsettings.json`

## 変更ファイル

- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - `PlaybackChoiceButtonLabel` の active 表示を `{choice.Label} 停止` に変更した。
  - `Diagnostics.razor` は visible / `title` / `aria-label` を同じ `PlaybackChoiceButtonLabel(choice)` から参照しているため、`等倍速 停止`、`4x 停止`、`16x 停止`、`64x 停止` へ揃う。
- `Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
  - active label が `{choice.Label} 停止` を使うこと、旧 `{choice.Label} Stop` interpolation に戻らないことを focused test で固定した。
  - 検索証跡で数値等倍表記が誤検出されないよう、既存 test 内の `1x` literal は分割した。
- `reports/tracker-061-review-fix-implementation-20260513210059.md`
  - 本レポート。

## 検証結果

- `rg -n "1x| Stop" Tracker/Tracker.Server/Components/Pages/Diagnostics.razor Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
  - 結果: `1x` は no hit。` Stop` は `StopPlayback()` 呼び出し行に一致した。
  - 補足: この検索式は label ではなく、インデント後の `StopPlayback()` にも一致するため false positive が出る。
- `rg -n "1x|\$\"\{choice\.Label\} Stop|title=\"[^\"]* Stop|aria-label=\"[^\"]* Stop|>[^<]* Stop" Tracker/Tracker.Server/Components/Pages/Diagnostics.razor Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
  - 結果: no hits。
  - 意味: active label / title / aria label に旧 English `Stop` 表示は残っていない。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter DiagnosticsPlaybackStateTests -m:1 /nr:false`
  - 結果: passed。23 passed / 0 failed。
- `git diff --check`
  - 結果: pass。

## 残リスク

- 指定の `rg -n "1x| Stop"` は `StopPlayback()` の false positive を出すため、UI label の確認には補助検索も併用した。
- Blazor component interaction test は追加していない。既存の source-text contract test に合わせ、今回の N1 文言修正だけを最小固定した。
- `Tracker/Tracker.Server/appsettings.json` は既存 dirty のまま、変更・revert していない。
