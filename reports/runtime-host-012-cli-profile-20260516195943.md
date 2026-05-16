# RUNTIME-HOST-012 RuntimeHost CLI profile 指定レポート

## 対象

`Tracker.RuntimeHost` 起動時に CLI 引数で active profile を指定できるようにした。

## 実装内容

- `Tracker.RuntimeHost` の起動時に `--profile <name>` と `--profile=<name>` を受け付けるようにした。
- `--profile` は `Tracker:ActiveProfileName` へ上書き適用し、profile 定義は既存の `Tracker:Profiles:<name>` を使う。
- command-line parsing は手書き parser ではなく `Microsoft.Extensions.Configuration.CommandLine` の provider と switch mapping を使う形に変更した。
- `--profile` の値なし指定は起動時に明示失敗し、誤って `default` profile へ fallback しないことを test で固定した。
- README の RuntimeHost `sim` profile 起動例を `--profile sim` に更新した。

## 変更ファイル

- `Directory.Packages.props`
- `README.md`
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `Tracker/Tracker.RuntimeHost/Program.cs`
- `Tracker/Tracker.RuntimeHost/RuntimeHostCommandLine.cs`
- `Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj`
- `Tracker/Tracker.RuntimeHost/appsettings.json`
- `Tracker/Tracker.Tests/RuntimeHostOperationLoopTests.cs`

## 検証

- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostOperationLoopTests|FullyQualifiedName~RuntimeHostScaffoldContractTests" -m:1 /nr:false`
  - review finding 修正後は 17 passed。
- `dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false`
  - 0 warnings / 0 errors。

## 補足

test と build を並列実行した最初の試行では、出力ファイル `SslProto.deps.json` の file lock で `Tracker.RuntimeHost` build が失敗した。build を単独で再実行して成功を確認したため、実装起因の compile failure ではない。

`Tracker/Tracker.RuntimeHost/appsettings.json` の checked-in `sim` profile は `ReorderWindowNs=10000000` に変更し、README にも 10 ms の reorder window として記録した。

初回 review で `--profile` / `--profile=` の値なし指定が checked-in `appsettings.json` の `Tracker:ActiveProfileName` に隠れて fail-fast しない問題が見つかった。`RuntimeHostCommandLine` は command-line configuration provider へ渡す前に argv 自体を検証するよう修正し、既存 active profile がある場合でも値なし指定を `ArgumentException` にする regression test を追加した。

追加確認:

- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" timeout 8s dotnet run --no-restore --project Tracker/Tracker.RuntimeHost --no-launch-profile -- --profile`
  - `ArgumentException: --profile requires a profile name.` で即終了。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" timeout 8s dotnet run --no-restore --project Tracker/Tracker.RuntimeHost --no-launch-profile -- --profile=`
  - `ArgumentException: --profile requires a profile name.` で即終了。

## Review

初回 dedicated review は `reports/pr19-review-runtimehost-profile-20260516200807.md` に記録した。blocking finding 1 件を修正し、r2 review は `reports/pr19-review-runtimehost-profile-r2-20260516201757.md` で確認する。
