# PR #28 ASPIRE-002 AppHost 実装報告

日付: 2026-09-29

## 対象

`ASPIRE-002` の AppHost 骨格と application model 契約だけを実装した。

完了条件は、`Testing/Duck.Testing.AppHost` を追加し、`duck` を `Tracker.RuntimeHost` の .NET project resource として登録し、Docker を使わない application model test で契約を確認できることとした。

Simulator、Game Controller、Crane、`cm4-sim`、comparison / match 資源、RuntimeHost の受信診断、OS 別実 packet 受入は後続タスクの範囲であり、今回変更していない。

## 開始前の診断 artifact 確認

既存の `.github/workflows/dotnet-test.yml` は、`dotnet test` 失敗時に次を `artifacts/test-results` へ保存し、GitHub Actions artifact として upload する。

- TRX テスト結果
- 標準出力
- 標準エラー
- vstest diagnostics
- MSBuild binlog
- test exit code
- runner / .NET / git / submodule 情報
- source snapshot

したがって `ASPIRE-002` の開始時点で失敗原因調査に必要な artifact workflow は存在しており、workflow の追加変更は行っていない。

## TDD: 赤

commit: `1d4dffd87fc22b21917217783f7294eba05cc6d0`

先に次を追加した。

- `Testing/Duck.Testing.AppHost` の最小 AppHost project
- `Aspire.Hosting.Testing` を使う `AppHostApplicationModelTests`
- `duck` resource が一つ存在すること
- `duck` が `ProjectResource` であること
- `IProjectMetadata.ProjectPath` が `Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj` を指すこと

この段階の `Program.cs` は resource を登録していない状態とした。

PR #28 current HEAD が上記 commit と一致することを確認し、同じ head SHA の `.NET tests` run `36570519797` だけを失敗確認に使用した。

結果は 330 件中 329 件成功、1 件失敗だった。失敗は `BaseModelContainsDuckRuntimeHostProject` の `Assert.Single()` で、`duck` resource が 0 件だったためであり、未実装状態を意図どおり検出した。失敗時 artifact の upload も成功した。

## 実装: 緑

commit: `4ce48820442cc3e67cc1e1313d35c1f62c76eee9`

`Testing/Duck.Testing.AppHost/Program.cs` に次の project resource を追加した。

`builder.AddProject<Projects.Tracker_RuntimeHost>("duck");`

また、AppHost project は `Tracker.RuntimeHost.csproj` を `ProjectReference` し、`Duck.slnx` に AppHost を追加した。`Tracker.Tests` から AppHost application model を構築できるよう `Aspire.Hosting.Testing` を参照している。

PR #28 current HEAD が上記 commit と一致することを確認し、同じ head SHA の `.NET tests` run `36570813380` だけを緑確認に使用した。

結果は 330 / 330 件成功だった。

## ローカル検証環境

初回作業時は RDMCP の PATH から `dotnet` を解決できなかったため、TDD の赤・緑は各 commit の current HEAD SHA と一致する GitHub Actions run で確認した。その後 .NET SDK 10.0.401 を導入し、RDMCP 再起動後の 2026-09-30 に Windows 実機確認を追加した。

ローカル worktree は `SslProto` の submodule が未初期化だったため最初の focused test が既存 Protobuf 生成型不足でビルド失敗した。`git submodule update --init --recursive` で CI と同じ submodule SHA へ揃えた後、`AppHostApplicationModelTests` は 1 / 1 件成功した。

続いて `dotnet run --project Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-build` で AppHost 13.5.4 を起動し、`duck` 資源から `Tracker.RuntimeHost.exe` が子プロセスとして起動することを確認した。RuntimeHost の PID 24612 が `0.0.0.0:10020` を bind しており、AppHost 停止後は `Tracker.RuntimeHost.exe` とその `dotnet` wrapper が終了し、UDP 10020 も解放された。これは `ASPIRE-002` の単独 Duck 起動範囲の確認であり、Docker / Simulator / multicast packet の OS 別受入確認ではない。

`git diff --check` は赤コミット前と実装コミット前に成功している。

文書更新後の `npm run lint:md` は Windows 作業環境に `xargs` が存在しないため `lint:md:text` で終了値 255 となり、全文検査を完走できなかった。変更した `Tracker/Design/tasks-status.md` へ `npx cspell` を直接実行した結果は、既存箇所の `Sudachi` / `Blazor` 6件のみで、今回の追加行には新規指摘がない。`reports` は現行 CSpell 対象外である。

## 既知の警告

CI の AppHost build では `ASPIRE010` が warning として出ている。

`Duck.Testing.AppHost is configured with AspireUseCliBundle=false`

この warning は test failure ではなく、`ASPIRE-002` の application model 契約と `duck` project resource 登録は成功している。`AspireUseCliBundle` の設定変更は今回の完了条件に含まれないため、本タスクでは変更していない。

## 変更ファイル

- `Directory.Packages.props`
- `Duck.slnx`
- `Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj`
- `Testing/Duck.Testing.AppHost/Program.cs`
- `Tracker/Tracker.Tests/Tracker.Tests.csproj`
- `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs`
- `Tracker/Design/tasks-status.md`
- `reports/pr28-aspire-002-apphost-implementation-20260929.md`

## 完了状態

`ASPIRE-002` の AppHost 骨格と application model 契約は実装済み。次の実装単位は `ASPIRE-003A` の RuntimeHost SSL-Vision 受信診断である。
