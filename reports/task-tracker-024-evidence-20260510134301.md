# TRACKER-024 Evidence

## 対象

- Task: `TRACKER-024`
- Title: Kalman 標準準拠の検証と release 判定をやり直す
- Branch: `feat/tracker-004-contract-surface`
- Base commit: `3142e5c feat(tracker): camera-local trackingをKalman標準へ是正する`

## 目的

- `TRACKER-023` の Kalman 化後に、focused / full test と review 証跡を取り直す。
- 設計書の「v1 は直線運動前提の Kalman filter を標準とする」に対して、release 判定上の blocker が残っていないか確認する。

## 実行コマンド

```bash
dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerEngineTemporalContractTests" --no-restore
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
dotnet format Tracker/Tracker.Tests/Tracker.Tests.csproj --verify-no-changes --no-restore
git diff --check
```

## 結果

- Explicit build:

```text
dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore
MSBUILD : error MSB1025: An internal failure occurred while running MSBuild.
System.IO.IOException: Read-only file system
   at System.IO.Directory.CreateTempSubdirectory(...)
   at Microsoft.Build.Shared.FileUtilities.CreateFolderUnderTemp(...)
```

- Explicit build retry with project-local `TMPDIR` / `DOTNET_CLI_HOME`:

```text
TMPDIR=/home/ibis/ssl/IbisDuck/.codex-tmp DOTNET_CLI_HOME=/home/ibis/ssl/IbisDuck/.codex-dotnet-home MSBUILDDISABLENODEREUSE=1 dotnet build ...
```

  - MSBuild first-run setup started, then remained blocked with no build result for more than 90 seconds.
  - The temporary directories were removed after the retry.

- Focused test:

```text
Passed!  - Failed:     0, Passed:    51, Skipped:     0, Total:    51, Duration: 141 ms - Tracker.Tests.dll (net10.0)
```

- Full test:

```text
Passed!  - Failed:     0, Passed:   101, Skipped:     0, Total:   101, Duration: 105 ms - Tracker.Tests.dll (net10.0)
```

- `dotnet format --verify-no-changes`:

```text
Unhandled exception: System.TimeoutException: The operation has timed out.
   at System.IO.Pipes.NamedPipeClientStream.ConnectInternal(...)
   at Microsoft.CodeAnalysis.MSBuild.BuildHostProcessManager.BuildHostProcess..ctor(...)
```

- `DOTNET_CLI_HOME=/home/ibis/ssl/IbisDuck/.codex-dotnet-home MSBUILDDISABLENODEREUSE=1 dotnet format ... --verbosity diagnostic` retry:

```text
Formatting code files in workspace '/home/ibis/ssl/IbisDuck/Tracker/Tracker.Tests/Tracker.Tests.csproj'.
Loading workspace.
Unhandled exception: System.TimeoutException: The operation has timed out.
   at System.IO.Pipes.NamedPipeClientStream.ConnectInternal(...)
   at Microsoft.CodeAnalysis.MSBuild.BuildHostProcessManager.BuildHostProcess..ctor(...)
```

- `git diff --check`: passed with no output.

## Release 判定

- Focused/full test は green。
- `TRACKER-023` review r2 は no findings で、Kalman 契約に対する normal-path blocker は見つかっていない。
- `dotnet test` は build を含む経路で focused/full ともに green。
- `dotnet format` は MSBuild/Roslyn build host の named pipe timeout で失敗しており、コード差分上の formatting failure は確認できていない。
- `DOTNET_CLI_HOME` を project root 配下へ移し、MSBuild node reuse を無効化しても同じ timeout だった。
- 明示 `dotnet build` は MSBuild temp / host 経路の環境制限で成功証跡を追加取得できなかったが、ユーザー判断により `dotnet test` green を build/test 通過証跡として採用する。
- Release 判定: Kalman 標準準拠後の正常系 release blocker は残っていない。

## 保留事項

- Diagonal axis model / process noise scale は `TRACKER-023` evidence と review r2 に記録済みの保留リスク。v1 の「直線運動前提の Kalman filter」標準には反していない前提で、release blocker ではなく今後の tuning / model 改善候補として扱う。
- `dotnet format --verify-no-changes` と明示 `dotnet build --no-restore` は MSBuild/Roslyn の sandbox 内実行制限で成功確認は未取得。ただし focused/full `dotnet test` green と `git diff --check` green により、今回の release 判定では blocker としない。
