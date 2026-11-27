# PR #28 ASPIRE-002 独立レビュー

日付: 2026-09-30

## 対象

- PR: #28
- タスク: `ASPIRE-002`
- レビュー対象 HEAD: `0c75e99ecebcda4289292f1433726b8b381aef57`
- 対象: AppHost 骨格、`duck` = `Tracker.RuntimeHost` の application model 契約、TDD 証跡、Windows 単独起動証跡。

## 結果

阻害指摘はない。低重要度の差分品質指摘が 1 件ある。

### R1 Low: 実装報告末尾の追加空行で diff check が失敗する

`git diff --check origin/main...HEAD` が次を報告する。

`reports/pr28-aspire-002-apphost-implementation-20260929.md:94: new blank line at EOF.`

実装・テスト・AppHost 起動には影響しないが、PR 差分検査を成功させるには末尾の余分な空行を除去する必要がある。

## 実装照合

`Testing/Duck.Testing.AppHost` は `Aspire.AppHost.Sdk/13.5.4` / `net10.0` の AppHost として追加され、`Tracker.RuntimeHost.csproj` を project reference に持つ。`Program.cs` は `builder.AddProject<Projects.Tracker_RuntimeHost>("duck")` で `duck` を登録する。

`AppHostApplicationModelTests.BaseModelContainsDuckRuntimeHostProject` は `duck` が `ProjectResource` であり、project metadata が `Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj` を指すことを固定する。後続タスクで resource が追加されても継続利用できる契約である。

## TDD / CI 証跡

既存 `.github/workflows/dotnet-test.yml` は失敗時に TRX、stdout、stderr、vstest diagnostics、MSBuild binlog、環境情報、source snapshot を artifact として保存する。

- 赤 `1d4dffd87fc22b21917217783f7294eba05cc6d0`: run `36570519797`、head SHA 一致、329 passed / 1 failed。`BaseModelContainsDuckRuntimeHostProject` が空 resource collection の `Assert.Single` で失敗。failure artifact は 10 ファイルを upload 済み。
- 緑 `4ce48820442cc3e67cc1e1313d35c1f62c76eee9`: run `36570813380`、head SHA 一致、330 / 330 passed。
- レビュー対象 HEAD `0c75e99ecebcda4289292f1433726b8b381aef57`: run `36614692972`、head SHA 一致、330 / 330 passed。

別 SHA の workflow run は判定に使用していない。

## 独立ローカル確認

Windows / .NET SDK 10 環境で current HEAD を確認した。

- focused test: `AppHostApplicationModelTests` 1 / 1 passed。
- `dotnet build Duck.slnx -m:1 /nr:false`: 0 errors。
- AppHost を `dotnet run --project Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-build` で起動。
- `Tracker.RuntimeHost.exe` が起動し UDP `0.0.0.0:10020` を bind。
- AppHost 停止後、RuntimeHost process と UDP 10020 bind は残存しなかった。

build / test では `ASPIRE010` が 1 件出る。現行 Aspire では `AspireUseCliBundle=false` でも NuGet 由来 orchestration dependencies で `dotnet run` を継続でき、今回の範囲で実害は確認していない。将来の Aspire 更新では CLI bundle が唯一の対応経路になる予定だが、ASPIRE-002 の阻害指摘にはしない。

## 判定

ASPIRE-002 の AppHost 骨格、application model 契約、TDD、current HEAD CI、Windows 単独起動を確認した。R1 を除き、実装上の阻害指摘はない。
