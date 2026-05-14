# Sub-agent実行レポート

## タスク

RUNTIME-HOST-004 の親側追加修正後 verification。

## sub-agentを使う理由

`codex-delegation-executor` の固定 sub-agent category に従い、test / build execution used as verification evidence を implementation から独立した report-backed evidence として取得するため。

## 対象範囲

- `RuntimeHostDebugHostRenameContractTests`
- `Tracker.DebugHost` build
- `Tracker.CaptureReplay` build
- active source / docs の旧 `Tracker.Server` 参照確認
- parent が追加修正した `NavMenu.razor`、RUNTIME-HOST-004 contract test、active design docs

## 対象外

- ファイル編集
- review
- commit / PR update
- RuntimeHost scaffold
- tracker operation loop 抽出
- diagnostics sample sidecar production 実装

## 実行コマンド

- `mkdir -p .codex-dotnet-home .codex-nuget-packages`
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDebugHostRenameContractTests -m:1 /nr:false`
  - Pass: `RuntimeHostDebugHostRenameContractTests` は 3 tests passed。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false`
  - Pass: Build succeeded、0 warnings、0 errors。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -m:1 /nr:false`
  - Pass: Build succeeded、0 warnings、0 errors。
- `git diff --check`
  - Pass: output なし。
- `rg -n -F -e 'Tracker.Server' -e 'Tracker/Tracker.Server' -e 'Tracker\Tracker.Server' . --glob '!reports/**' --glob '!Tracker/Design/Archive/**' --glob '!**/bin/**' --glob '!**/obj/**' --glob '!/.git/**' --glob '!*.md'`
  - Hit は `Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs` と `Tracker/Tracker.Tests/RuntimeHostDebugHostRenameContractTests.cs` のみ。
  - production source には旧 `Tracker.Server` 参照 hit なし。
- 追加確認: `rg -n -F -e 'Tracker.Server' -e 'Tracker/Tracker.Server' -e 'Tracker\Tracker.Server' README.md Tracker/Design --glob '!Tracker/Design/Archive/**'`
  - active docs の hit は RUNTIME-HOST-004 task 記述、旧名説明、history 文脈のみ。

## 対象ファイル

- Checked:
  - `Tracker/Tracker.Tests/RuntimeHostDebugHostRenameContractTests.cs`
  - `Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs`
  - `Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj`
  - `Tracker/Tracker.DebugHost/Components/Layout/NavMenu.razor`
  - `Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj`
  - `Duck.slnx`
  - `README.md`
  - `Tracker/Design/tasks-status.md`
  - `Tracker/Design/RuntimeHost/runtime-host-plan.md`
  - `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
  - `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
  - `Tracker/Design/Core/tracker-architecture-plan.md`
- Edited:
  - `reports/runtime-host-004-verification-20260514172634.md`

## 指摘事項

- Blocking finding なし。
- `RuntimeHostDebugHostRenameContractTests` の focused test は pass し、DebugHost rename contract は現在の差分上で満たされている。
- `Tracker.DebugHost` / `Tracker.CaptureReplay` はどちらも build pass。
- active non-md 旧名検索の hit は contract tests の禁止トークン・旧名存在確認に限定され、production source には残っていない。
- active docs には `旧 Tracker.Server` としての履歴・rename 文脈の記述が残っているが、現行 project / namespace / 起動経路を `Tracker.DebugHost` とする説明であり、今回の verification では blocker としない。

## 結果

- Pass。
- focused rename contract、DebugHost build、CaptureReplay build、diff whitespace check、active non-md 旧名検索のすべてが期待どおり。
- 編集したファイルはこの verification report のみ。

## リスク

- 今回の確認は指定された focused test / build / static search に限定しており、full `Tracker.Tests` や RuntimeHost scaffold / tracker operation loop 抽出 / diagnostics sample sidecar production 実装は対象外。
- active docs には履歴・旧名説明として `Tracker.Server` が残るため、将来の search gate が docs も全面禁止に変わる場合は、history 文脈の許容ルールを別途決める必要がある。
