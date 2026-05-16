# Sub-agent実行レポート

## タスク

- 目的: PR #19 の `Tracker.RuntimeHost` 起動時 profile 指定と checked-in `sim` profile 設定を code review する。
- タスク種別: review

## sub-agentを使う理由

- 理由: `RUNTIME-HOST-012` の dedicated review gate として、親実装者とは別視点で CLI parsing、configuration override、設定値、test 妥当性を確認するため。

## 対象範囲

- 対象: `Tracker/Tracker.RuntimeHost/RuntimeHostCommandLine.cs`、`Program.cs`、`Tracker.RuntimeHost.csproj`、`appsettings.json`、`Directory.Packages.props`、`RuntimeHostOperationLoopTests` の該当差分、`reports/runtime-host-012-cli-profile-20260516195943.md`。

## 対象外

- 対象外: `Tracker.CaptureReplay` の replay / latency tooling、DebugHost UI、PR 作成操作、レビュー結果に基づく修正実装。

## 実行コマンド

- 実行コマンド:
  - `git diff --stat origin/main...HEAD -- Directory.Packages.props Tracker/Tracker.RuntimeHost/Program.cs Tracker/Tracker.RuntimeHost/RuntimeHostCommandLine.cs Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj Tracker/Tracker.RuntimeHost/appsettings.json Tracker/Tracker.Tests/RuntimeHostOperationLoopTests.cs reports/runtime-host-012-cli-profile-20260516195943.md reports/pr19-review-runtimehost-profile-20260516200807.md`
  - `git diff --unified=80 origin/main...HEAD -- Directory.Packages.props README.md Tracker/Tracker.RuntimeHost/Program.cs Tracker/Tracker.RuntimeHost/RuntimeHostCommandLine.cs Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj Tracker/Tracker.RuntimeHost/appsettings.json Tracker/Tracker.Tests/RuntimeHostOperationLoopTests.cs reports/runtime-host-012-cli-profile-20260516195943.md`
  - `rg -n "ReorderWindowNs|10000000|profile" -g 'README*' -g '*.md' Tracker reports .`
  - `nl -ba Tracker/Tracker.RuntimeHost/RuntimeHostCommandLine.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.RuntimeHost/Program.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.RuntimeHost/appsettings.json | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Tests/RuntimeHostOperationLoopTests.cs | sed -n '1,320p'`
  - `nl -ba reports/runtime-host-012-cli-profile-20260516195943.md | sed -n '1,240p'`
  - `nl -ba README.md | sed -n '52,78p'`
  - `timeout 8s dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile -- --profile`
  - `timeout 8s dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile -- --profile=`
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostOperationLoopTests|FullyQualifiedName~RuntimeHostScaffoldContractTests" -m:1 /nr:false`

## 対象ファイル

- 変更または確認したファイル:
  - `Directory.Packages.props`
  - `README.md`
  - `Tracker/Tracker.RuntimeHost/Program.cs`
  - `Tracker/Tracker.RuntimeHost/RuntimeHostCommandLine.cs`
  - `Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj`
  - `Tracker/Tracker.RuntimeHost/RuntimeTrackerConfigurationResolver.cs`
  - `Tracker/Tracker.RuntimeHost/appsettings.json`
  - `Tracker/Tracker.Tests/RuntimeHostOperationLoopTests.cs`
  - `reports/runtime-host-012-cli-profile-20260516195943.md`
  - `reports/pr19-review-runtimehost-profile-20260516200807.md`

## 指摘事項

- 指摘要約または「指摘なし」:
  - [Blocking normal-path problem] `Tracker/Tracker.RuntimeHost/Program.cs:4-6`, `Tracker/Tracker.RuntimeHost/RuntimeHostCommandLine.cs:30-33`, `Tracker/Tracker.Tests/RuntimeHostOperationLoopTests.cs:95-99,266-277`
    `Host.CreateApplicationBuilder(args)` は checked-in `appsettings.json` を含む既定 configuration source を先に読み込んだうえで `ApplyOverrides` を呼んでいます。ところが `ApplyOverrides` の値なし検証は `configuration["Tracker:ActiveProfileName"]` が空かどうかだけを見ているため、`--profile` / `--profile=` に値がなくても既存の `Tracker:ActiveProfileName = "sim"` が見えてしまい、例外になりません。実起動でも `timeout 8s dotnet run --project Tracker/Tracker.RuntimeHost --no-launch-profile -- --profile` と `-- --profile=` の両方が timeout まで走り続け、fail-fast しないことを確認しました。追加 test は `BuildHost` で `DisableDefaults = true` にして appsettings を載せていないため、この production path をカバーできていません。ユーザーは値なし指定を明示失敗として扱うつもりでも、現状は `sim` profile でそのまま headless 起動して publish 宛先と engine 設定を誤って使えてしまうので blocker です。
  - [Non-blocking concern] それ以外の review 観点では指摘なし。`--profile <name>` / `--profile=<name>` の named override 自体は test で固定されており、CLI override 優先順位の意図、`sim.Engine.ReorderWindowNs=10000000` と README / 実装 report の整合、source layout / XML documentation / test summary policy への違反は見当たりませんでした。

## 結果

- 結果:
  - blocking finding 1 件のため、この scope の review gate は未クローズです。
  - ユーザー指示に従い nested Codex / 追加 sub-agent は使わず、workspace 直接確認による built-in review behavior で判定しました。
  - focused test `RuntimeHostOperationLoopTests|RuntimeHostScaffoldContractTests` は 14 passed でしたが、上記 blocker は test harness と実起動 path の差で取りこぼされています。

## リスク

- 未解決のリスクまたは後続対応:
  - `--profile` の値なし指定を本番起動 path で fail-fast に直し、checked-in `appsettings.json` を含む configuration defaults ありのケースで regression test を追加しない限り、CLI typo / 空値指定で `sim` profile が silently 起動するリスクが残ります。
