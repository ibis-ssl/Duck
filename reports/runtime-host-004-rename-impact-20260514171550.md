# Sub-agent実行レポート

## タスク

RUNTIME-HOST-004 の `Tracker.Server` -> `Tracker.DebugHost` rename impact を read-only で確認する。

## sub-agentを使う理由

implementation worker と干渉しない read-only 調査を並列化し、rename で壊れやすい project reference、README、launch settings、namespace、design reference を report-backed evidence として残すため。

## 対象範囲

- `.sln` / `.csproj` project reference
- `Tracker/Tracker.Server` 配下の namespace、launch settings、README、appsettings
- repository root README
- tests / CLI / docs の `Tracker.Server` 参照
- RUNTIME-HOST-004 の exit criteria に対する不足確認

## 対象外

- レポート以外のファイル編集
- test 作成
- build / test 実行
- tracking 更新
- commit / PR update

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `rg -n "RUNTIME-HOST-004|Tracker.DebugHost|Tracker.Server|rename impact|runtime host" /home/ibis/.codex/memories/MEMORY.md`
- `sed -n '1,260p' reports/runtime-host-004-rename-impact-20260514171550.md`
- `git status --short`
- `rg --files -g '*.sln' -g '*.csproj' -g '*.props' -g '*.targets' -g 'README*' -g 'appsettings*.json' -g 'launchSettings.json' -g '*.md' Tracker .`
- `rg -n "Tracker\.Server|Tracker/Tracker\.Server|Tracker.Server|Tracker.DebugHost|DebugHost|RUNTIME-HOST-004" --glob '!reports/**' --glob '!**/bin/**' --glob '!**/obj/**' .`
- `rg -n "Tracker\.Server|Tracker/Tracker\.Server|Tracker.Server|Tracker.DebugHost|DebugHost|RUNTIME-HOST-004" reports/runtime-host-*.md Tracker/Design Tracker/Tracker.Server/Design`
- `find Tracker/Tracker.Server -maxdepth 3 \( -name '*.cs' -o -name '*.razor' -o -name '*.json' -o -name '*.md' -o -name '*.csproj' -o -name '*.cshtml' \) -print | sort`
- `nl -ba Duck.slnx | sed -n '1,80p'`
- `nl -ba Tracker/Tracker.Server/Tracker.Server.csproj | sed -n '1,160p'`
- `nl -ba Tracker/Tracker.Tests/Tracker.Tests.csproj | sed -n '1,100p'`
- `nl -ba Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj | sed -n '1,120p'`
- `nl -ba Tracker/Tracker.Server/Properties/launchSettings.json | sed -n '1,160p'`
- `nl -ba Tracker/Tracker.Server/appsettings.json | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/appsettings.Development.json | sed -n '1,160p'`
- `nl -ba Tracker/Tracker.Server/Program.cs | sed -n '1,220p'`
- `nl -ba README.md | sed -n '1,80p'`
- `nl -ba Tracker/Tracker.Server/README.md | sed -n '1,70p;168,210p;220,232p;312,322p;468,478p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Home.razor | sed -n '1,40p'`
- `nl -ba Tracker/Tracker.Server/Components/Layout/NavMenu.razor | sed -n '1,40p'`
- `rg -n "Tracker\.Server|Tracker/Tracker\.Server|Tracker.Server|Tracker.DebugHost|DebugHost" Tracker/Tracker.CaptureReplay Tracker/Tracker.Tests --glob '!**/bin/**' --glob '!**/obj/**'`
- `rg -n "RUNTIME-HOST-004|Tracker\.Server|Tracker/Tracker\.Server|Tracker.Server|Tracker.DebugHost|DebugHost" Tracker/Design/tasks-status.md Tracker/Design/phases-status.md Tracker/Design/RuntimeHost Tracker/Design/DebugHost --glob '!**/Archive/**'`
- `find Tracker/Design -path '*Archive*' -prune -o -type f -name '*.md' -print | sort`
- `nl -ba Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs | sed -n '1,150p'`
- `nl -ba Tracker/Tracker.Tests/RuntimeHostDiagnosticsSampleBoundaryContractTests.cs | sed -n '1,180p'`
- `nl -ba Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs | sed -n '1,80p'`
- `nl -ba Tracker/Tracker.CaptureReplay/ReplayOptions.cs | sed -n '220,265p'`
- `rg -l "Tracker\.Server|Tracker/Tracker\.Server|Tracker.Server" Tracker/Tracker.Server Tracker/Tracker.Tests Tracker/Tracker.CaptureReplay README.md Duck.slnx --glob '!**/bin/**' --glob '!**/obj/**' | sort`
- `rg -n "namespace Tracker\.Server|using Tracker\.Server|Tracker\.Server\." Tracker/Tracker.Server --glob '!**/bin/**' --glob '!**/obj/**'`
- `rg -n "Tracker/Tracker\.Server|Tracker\.Server|Tracker.Server" Tracker/Tracker.Tests --glob '!**/bin/**' --glob '!**/obj/**'`

build / test は対象外指示のため未実行。

## 対象ファイル

- `Duck.slnx`
- `README.md`
- `Tracker/Tracker.Server/Tracker.Server.csproj`
- `Tracker/Tracker.Server/Properties/launchSettings.json`
- `Tracker/Tracker.Server/appsettings.json`
- `Tracker/Tracker.Server/appsettings.Development.json`
- `Tracker/Tracker.Server/Program.cs`
- `Tracker/Tracker.Server/README.md`
- `Tracker/Tracker.Server/Components/_Imports.razor`
- `Tracker/Tracker.Server/Components/App.razor`
- `Tracker/Tracker.Server/Components/Layout/NavMenu.razor`
- `Tracker/Tracker.Server/Components/Pages/Home.razor`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics*.razor*`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics*State*.cs`
- `Tracker/Tracker.Server/Components/Vision/*.razor`
- `Tracker/Tracker.Server/Components/Vision/*.cs`
- `Tracker/Tracker.Server/Tracking/*.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator/*.cs`
- `Tracker/Tracker.Server/Vision/*.cs`
- `Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj`
- `Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs`
- `Tracker/Tracker.CaptureReplay/ReplayOptions.cs`
- `Tracker/Tracker.CaptureReplay/ReplaySettingsOptions.cs`
- `Tracker/Tracker.Tests/Tracker.Tests.csproj`
- `Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs`
- `Tracker/Tracker.Tests/RuntimeHostDiagnosticsSampleBoundaryContractTests.cs`
- `Tracker/Tracker.Tests/*Tests.cs`
- `Tracker/Tracker.Tests/Contracts/TrackerContractFixture.cs`
- `Tracker/Design/tasks-status.md`
- `Tracker/Design/phases-status.md`
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
- historical / archive 判定用: `Tracker/Design/Archive/**`

## 指摘事項

Findings: あり。

1. `Duck.slnx:6` は active solution entry として `Tracker/Tracker.Server/Tracker.Server.csproj` を直接参照している。rename 後は `Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj` に更新しないと solution build / IDE 読み込みの入口が旧 project path のまま残る。

2. `Tracker/Tracker.Tests/Tracker.Tests.csproj:23` と `Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj:12` は `..\Tracker.Server\Tracker.Server.csproj` を参照している。rename 後は tests / CLI の build graph が旧 project path で壊れるため、必須更新対象。

3. `Tracker/Tracker.Server/Program.cs:2-4`、`Tracker/Tracker.Server/Components/_Imports.razor:9-12`、`Tracker/Tracker.Server/Tracking/*.cs`、`Tracker/Tracker.Server/Vision/*.cs`、`Tracker/Tracker.Server/Components/**/*.cs|razor` に `Tracker.Server.*` namespace / using が広く残っている。project directory だけを rename して namespace を残すと RUNTIME-HOST-004 の「project / namespace / 起動経路へ rename」と不一致になり、namespace を変える場合は tests / reflection literal も同時更新が必要。

4. UI / static web asset 名の active reference がある。`Tracker/Tracker.Server/Components/App.razor:11` は `Tracker.Server.styles.css` を参照し、`Tracker/Tracker.Server/Components/Pages/Home.razor:18` は `<PageTitle>Tracker.Server</PageTitle>`、`Tracker/Tracker.Server/Components/Layout/NavMenu.razor:7` は sidebar brand、同 `:11` は collapsed mark `TS` を表示している。rename 後の DebugHost UI normal path で旧名が残りやすい。

5. runtime config / launch path の active reference がある。`Tracker/Tracker.Server/Properties/launchSettings.json:3-22` は profile 名自体は `http` / `https` で旧 project 名を含まないが、directory 移動時に `Properties/launchSettings.json` を移す必要がある。`Tracker/Tracker.Server/appsettings.json:179` と `Tracker/Tracker.Server/README.md:318` は logger category `Tracker.Server.Tracking.TrackerCoordinator` を持つため、namespace rename 後に logging filter が効かなくなる可能性がある。

6. README / user-facing docs の active references が多い。root `README.md:8,18,44-55` は repo structure、前提、起動コマンド、詳細 README link を `Tracker.Server` / `Tracker/Tracker.Server` としている。`Tracker/Tracker.Server/README.md:1-3,19,26,37,168-176,188,203,205,227,318,474` も title、説明、起動コマンド、CaptureReplay の capture path、`Tracker.Server/appsettings.json` 形式説明、manual evidence、logging snippet、注意点に旧名を持つ。

7. `Tracker.CaptureReplay` は project reference だけでなく source でも旧 namespace / wording に依存している。`CaptureReplayRunner.cs:2` は `Tracker.Server.Tracking` を using し、`ReplayOptions.cs:232,258` と `ReplaySettingsOptions.cs:6` は `Tracker.Server appsettings.json` / `Tracker/Tracker.Server/appsettings.json` を user-facing usage として出す。

8. tests は compile-time using と string/reflection/path contract の両方が壊れやすい。例: `RuntimeHostDependencyBoundaryContractTests.cs:32-34,60-63,88-92` は旧 project と新 project の両方を contract token として持つ。`RuntimeHostDiagnosticsSampleBoundaryContractTests.cs:2` は `Tracker.Server.Tracking` を using。`TrackerConfigurationBindingTests.cs:179,305,313`、`VisionReceiverConfigurationResolverTests.cs:176,193,201`、`DiagnosticsPlaybackStateTests.cs:67-127`、`VisionFieldRenderContractTests.cs:50-53,86,94`、`TrackerMulticastReceiverReviewFixTddTests.cs:56,238,246` は repository path literal `Tracker/Tracker.Server/...` を読む。`TrackerComparisonSourceTddTests.cs:58-59,165`、`TrackerCaptureOnSessionSnapshotContractTests.cs:127,141,165`、`TrackerReplayIntegrationTddTests.cs:148`、`VisionLiveComparisonViewStateTests.cs:529-530` は `Tracker.Server.*` reflection literal / assertion message を持つ。

9. active design docs では RUNTIME-HOST-004 の必須更新対象と historical reference を分ける必要がある。`Tracker/Design/tasks-status.md:7-14,50,73` は現在 task として `Tracker.Server` -> `Tracker.DebugHost` rename を明示しており、RUNTIME-HOST-004 完了時に status / evidence を同期する対象。`Tracker/Design/RuntimeHost/runtime-host-plan.md:10,90` と `Tracker/Design/DebugHost/raw-vision-viewer-plan.md:5,390` の「現 `Tracker.Server` の後継名」は設計説明としては rename 完了後に「旧名」表現へ更新するか、historical note として残すかを親が判断する対象。`Tracker/Design/Archive/**` の `Tracker.Server` は過去 tracking / historical evidence なので原則残してよい。

10. focused contract test に含めるべき assertions は次の通り。`Duck.slnx` が `Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj` を含み `Tracker/Tracker.Server/Tracker.Server.csproj` を含まないこと。`Tracker.Tests` と `Tracker.CaptureReplay` の ProjectReference が DebugHost project を参照すること。DebugHost assembly / root namespace が `Tracker.DebugHost` で、DebugHost source に `namespace Tracker.Server` / `using Tracker.Server` が残らないこと。root README と DebugHost README の起動例が `dotnet run --project Tracker/Tracker.DebugHost` へ更新されること。`appsettings.json` の logger category が rename 後 namespace に合うこと。path literal を読む tests が `Tracker/Tracker.DebugHost/...` へ更新され、reflection literal が `Tracker.DebugHost.*` 型を探すこと。

11. rename 後に壊れやすい build/runtime path は、solution load、`dotnet run --project Tracker/Tracker.Server` 形式の user command、`Tracker.CaptureReplay` / `Tracker.Tests` の ProjectReference、Razor scoped CSS asset `Tracker.Server.styles.css`、logger category、tests の `FindRepositoryFile("Tracker/Tracker.Server/...")` / `AddJsonFile("Tracker/Tracker.Server/appsettings.json")` / reflection literal。

12. no findings として記録できる点: `Tracker/Tracker.Server/appsettings.Development.json` は確認範囲では `Tracker.Server` literal を持たない。`launchSettings.json` は profile 名 / URL に旧 project 名を持たないため、directory 移動に追従すれば内容自体の rename 必須箇所は見当たらない。

## 結果

read-only 調査を完了した。レポート以外のファイル編集、test 作成、build / test、tracking 更新、commit / PR update、nested agent spawn は実施していない。

RUNTIME-HOST-004 で必ず更新すべき active references は、solution entry、`Tracker.Tests` / `Tracker.CaptureReplay` の ProjectReference、`Tracker.Server` 配下の root namespace / using / Razor import / static asset 名、root README と DebugHost README の起動経路・説明、appsettings logger category、tests / CLI の path literal と reflection literal。historical / archive references は `Tracker/Design/Archive/**` と過去 report 類であり、原則として rename task の実装対象から除外してよい。

focused contract は「旧 project path が active build graph に残らない」「DebugHost namespace / assembly / Razor asset が新名に揃う」「user-facing run command と config path が新名に揃う」「tests / CaptureReplay の参照と文字列 literal が新名に揃う」を最小 assertion にすると、rename の normal path regression を拾いやすい。

## リスク

- `rg` 結果では `Tracker/Tracker.Server` 配下の大半の source file が namespace rename 対象になる。mechanical rename だけで通りやすい一方、reflection literal / test path literal / static web asset name は漏れやすい。
- `RuntimeHostDependencyBoundaryContractTests` は現時点で `Tracker.Server` と `Tracker.DebugHost` の両方を forbidden token として扱う箇所がある。RUNTIME-HOST-004 後の contract intent と実装名の整合を review で確認しないと、rename 後に false positive / stale assertion になる可能性がある。
- docs では active README と historical archive が混在する。`Tracker/Design/Archive/**` や過去 `reports/**` を一括置換すると履歴 evidence を破壊するため、active docs に限定する必要がある。
- `Tracker.CaptureReplay` の `--settings` 説明は「DebugHost appsettings 形式」を受けるという UX wording に変わるが、metadata shape と legacy capture path の説明は残る。単純置換で意味が崩れないか確認が必要。
