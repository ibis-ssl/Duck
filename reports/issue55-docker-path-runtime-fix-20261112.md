# Sub-agent作業レポート

## タスク

- 目的: WindowsでDocker Desktop CLIが通常`PATH`にないとき、AppHostから起動するAspire DCPのDocker runtime検出が失敗する原因を修正する。
- タスク種別: bounded implementation / focused validation。

## sub-agentを使う理由

- 理由: 親の依頼により、原因の境界特定と小規模なコード修正・focused testを担当した。

## 対象範囲

- 対象: Docker executableの既存解決結果をAppHost process `PATH`へ反映し、そのディレクトリがDCP child processへ継承されるようにする。
- 設計: Docker context/Engine IDの解決節に、フルパスCLIの親directoryをAppHostのprocess-local `PATH`へ追加してDCPへ継承する契約を追記。
- 非対象: PC全体のPATH、Docker設定、認証、権限、インストール、稼働中stackの変更。Docker acceptance再実行、commit/push/PR操作。

## Dispatch profile

- selection inputs / selection source: 本人の「AppHostのDocker CLI PATH問題を早急に直す」「実装は担当へ委譲」の指示。対象が限定された原因修正、既存担当が同一Issueの実装担当であるため、その担当をfollow-upで再開した。
- observed decomposability / decomposition policy: PATH境界の確認・実装・focused validation・設計更新が連続する一つの作業単位。新規の並列担当は起動せず、既存実装担当のみを継続。
- requested profile / role plan / planned runtime profile: 既存担当へのfollow-upで新しいprofile指定なし。元dispatchのrole/profile記録はこの報告作成時に確認できず、planned profileはunknown。
- applied profile: null（実適用モデル・reasoningは親から観測できない）。
- application status / runtime profile observability / fork policy: 既存担当を再利用。follow-upの実行は確認できたが、適用profileの最終snapshotは見えず `profile_unverified`。元のfork policyも確認不能。
- reasons / constraints: PC全体のPATH、Docker設定、認証、権限、インストール、稼働中stackを変更しない。Docker acceptanceは起動せず、既存のdirty filesへ触れず、commit/pushは親が管理する。

## 実行コマンド

- 読み取り: `git status --short --branch`; `git rev-parse HEAD`; `Get-Content Testing/Duck.Testing.AppHost/DockerIdentitySettingsResolver.cs`; `Get-Content Testing/Duck.Testing.AppHost/Program.cs`; `Get-Content Tracker/Tracker.Tests/DockerIdentitySettingsResolverTests.cs`; `rg`でDocker CLI/path/DCP起動の参照箇所を確認。
- 実測: `%LOCALAPPDATA%\Programs\DockerDesktop\resources\bin\docker.exe` が存在。現在のPowerShell process `PATH`は同directoryを含まず、`Get-Command docker.exe`は見つからない。
- focused validation: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~DockerIdentitySettingsResolverTests' --logger 'console;verbosity=normal'`。
- focused validation結果: design doc更新後の同コマンドはexit 0、8 passed / 0 failed。`Tracker.Tests`とその依存AppHostを含むproject buildも成功。ログとexit codeは `%TEMP%\issue55-dockerpath-fix-8203b25-20261112\focused-test-after-design.log` と `focused-test-after-design.exit-code` に保存。設計doc変更前にも同じfocused testがexit 0、8 passedだった。
- `git diff --check`: pass。LF→CRLFのGit warningのみ。

## 対象ファイル

- 変更: `Testing/Duck.Testing.AppHost/Program.cs`。Docker executable解決直後、DCP起動前に解決済みexecutableのdirectoryをAppHostのprocess-local `PATH`先頭へ追加する。
- 変更: `Testing/Duck.Testing.AppHost/DockerIdentitySettingsResolver.cs`。既存PATHの他entryを保持し、実行ファイルdirectoryを先頭へ追加・重複除去する純関数と、プロセス環境だけを更新する関数を追加。
- 変更: `Tracker/Tracker.Tests/DockerIdentitySettingsResolverTests.cs`。directory追加、PATH内directoryの先頭移動と重複除去、bare commandでの不変更をテスト。
- 変更: `Tracker/Design/Testing/aspire-simulation-test-environment.md`。Docker context/Engine ID解決の設計節へ、process-local PATH継承を記載。
- 設計ファイルはUTF-8 BOMと既存のCRLF 706行 / bare LF 1行を保ち、1段落中の1文のみを追記。
- 未変更: 既存の`Testing/Duck.Testing.RefereeDriver/RefereeDriver.cs`、`Tracker/Tracker.Tests/RefereeDriverFixtureTests.cs`と既存未追跡reports/evidence。

## 所見

- 現HEAD `8203b25ff78bd731d4d337fc847badb3262d49ba`で、`DockerIdentitySettingsResolver.ResolveExecutable`はWindowsのDocker Desktop CLI絶対pathを既に返す。AppHostはそのexecutableを`DockerCliCommandRunner`で使ってEngine identityを解決できる。一方、Aspire DCP runtime discoveryは`docker`を`PATH`から探す。
- 最初のAppHost起動ログには`Container runtime 'docker' could not be found`があり、Docker Desktop CLI path欠落時にDCP resourcesが作られなかった。AppHost自体はdashboardまで起動した。
- 2回目はAppHost processにCLI directoryを`PATH` prependしてDCP resources 9個が起動した。ただしその起動ではDocker context/Engine IDを環境変数で手動指定していたため、自動identity解決の証拠としては扱わない。
- よって修正は、既存Docker executableの解決結果に基づいてAppHost process PATHを設定し、DCP childに継承させる。OS / registry PATHやDocker configは変更しない。
- PATH setterは `DistributedApplication.CreateBuilder(args)` 後、`builder.Build().Run()` 前に呼ばれる。Aspire.Hosting 13.5.4のXML docsはCreateBuilderをbuilder instance生成と定義し、DCP起動はAppHostのRun経路でログにも `Distributed application starting` 後に記録される。

## 結果

- 結果: 原因に対するコード変更とfocused unit validationを完了。HEADは変更前と同一で、commit/pushなし。
- Docker acceptance / CI: 未実施。稼働中のmanual-override AppHost PID 25912とその9-container stackには触れず、証拠として使用しない。
- AppHost/DCP childの既存環境blockを外部から直接読んだ証拠はない。境界はコード経路、DCP runtime discovery警告、およびPATH差分時のcontainer-start結果から特定した。

## リスク

- 修正後AppHostから起動するDCPでPATH自動補正が実動するDocker acceptanceは未確認。
- テスト実行の実行時profileは観測できずunknown。Docker CLIで`Get-Command`が見つからない状態と、解決済み絶対pathが存在する状態のみ確認した。
