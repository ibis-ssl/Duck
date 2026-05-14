# Sub-agent実行レポート

## タスク

- 目的: RUNTIME-HOST-010 RuntimeHost / DebugHost split の focused validation と manual evidence を揃える。
- タスク種別: validation / evidence collection

## sub-agentを使う理由

- 理由: 親エージェントは context 汚染を避けるため build / test / 起動確認を実行せず、検証と evidence 収集を sub-agent に委譲する。

## 対象範囲

- 対象: RuntimeHost / DebugHost focused tests、RuntimeHost / DebugHost / Tracker.Tests build、diagnostics sample evidence、legacy degraded evidence、DebugHost UI normal path、RuntimeHost headless normal path、既知の DebugHost ownership assertion の扱い判断。

## 対象外

- 対象外: commit、push、PR 更新、tracking file の最終同期、親ワークフローの再実行、互換性維持目的の旧 diagnostics render snapshot 復活。

## 実行コマンド

- 実行コマンド:
  - PASS: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostOperationLoopTests|FullyQualifiedName~RuntimeHostScaffoldContractTests" -m:1 /nr:false`
    - 結果: 10 passed / 0 failed。
  - FAIL: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostDependencyBoundaryContractTests|FullyQualifiedName~RuntimeHostDebugHostReadSideSnapshotBoundaryTests|FullyQualifiedName~RuntimeHostDiagnosticsSampleBoundaryContractTests" -m:1 /nr:false`
    - 結果: 10 passed / 1 failed。失敗は既知の `RuntimeHostDependencyBoundaryContractTests.DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop` のみ。
    - failure message: `Tracker.DebugHost must not own the tracker operation loop... Found loop ownership markers: AddSingleton<TrackerCoordinator>, AddHostedService<VisionReceiverService>, trackerCoordinator.ProcessPacket, ITrackerEngine, TrackerEngine, ITrackerPacketPublisher, TrackerPacketGenerator`。
  - PASS: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "(FullyQualifiedName~RuntimeHostDependencyBoundaryContractTests|FullyQualifiedName~RuntimeHostDebugHostReadSideSnapshotBoundaryTests|FullyQualifiedName~RuntimeHostDiagnosticsSampleBoundaryContractTests)&FullyQualifiedName!~DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop" -m:1 /nr:false`
    - 結果: adjusted boundary focused は 10 passed / 0 failed。
  - PASS: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~VisionPacketCaptureTests|FullyQualifiedName~TrackerDiagnosticsLogReaderTests|FullyQualifiedName~DiagnosticsFieldViewFactoryTests" -m:1 /nr:false`
    - 結果: diagnostics related focused は 15 passed / 0 failed。
  - PASS: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false`
    - 結果: build succeeded、0 warnings、0 errors。
  - PASS: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false`
    - 結果: build succeeded、0 warnings、0 errors。
  - PASS: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`
    - 結果: build succeeded、0 warnings、0 errors。
  - PASS: `git diff --check`
    - 結果: whitespace error なし。
  - PASS: `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" Logging__LogLevel__Default=Information timeout 6s dotnet run --no-build --project Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj --no-launch-profile`
    - 結果: `Application started`、`RuntimeHost receiving SSL-Vision packets from 224.5.23.2:10006 via 192.168.1.105, 127.0.0.1` を確認。`timeout` による終了 code 124 は手動起動確認の停止として扱う。
  - PASS: `ASPNETCORE_URLS=http://127.0.0.1:5127 dotnet run --no-build --project Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj --no-launch-profile` を短時間起動し、`curl http://127.0.0.1:5127/` を実行。
    - 結果: HTTP 200。body に `Ibis` / `Home` / `Diagnostics` / `Tracker` 系文字列のいずれかが含まれることを確認。log は `Now listening on: http://127.0.0.1:5127` と `Application started` を出力。HTTPS port warning は HTTP 手動確認時の既知 warning で、UI normal path の blocker ではない。
  - PASS: `git check-ignore -v packet-captures/session/diagnostics-samples.jsonl packet-captures/session/ssl-vision-packets-20260514.metadata.json packet-captures/session/ssl-vision-packets-20260514.render-snapshots.jsonl.gz tracker-diagnostics-20260514.log tracker-packet-snapshots.jsonl tracker-snapshot-alignment.jsonl diagnostics-samples.jsonl`
    - 結果: 親が追加した `.gitignore` の runtime / diagnostics capture artifact pattern が該当 path を ignore することを確認。

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/runtime-host-010-validation-20260514201701.md`
  - 確認: `.gitignore`
  - 確認: `Tracker/Design/tasks-status.md`
  - 確認: `Tracker/Design/RuntimeHost/runtime-host-plan.md`
  - 確認: `Tracker/Tracker.RuntimeHost/Program.cs`
  - 確認: `Tracker/Tracker.RuntimeHost/appsettings.json`
  - 確認: `Tracker/Tracker.DebugHost/Program.cs`
  - 確認: `Tracker/Tracker.DebugHost/appsettings.json`
  - 確認: `Tracker/Tracker.DebugHost/Vision/VisionReceiverService.cs`
  - 確認: `Tracker/Tracker.DebugHost/Vision/VisionLiveDisplaySnapshotProvider.cs`
  - 確認: `Tracker/Tracker.DebugHost/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - 確認: `Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs`
  - 確認: `Tracker/Tracker.Tests/RuntimeHostDebugHostReadSideSnapshotBoundaryTests.cs`
  - 確認: `Tracker/Tracker.Tests/RuntimeHostDiagnosticsSampleBoundaryContractTests.cs`
  - 確認: `Tracker/Tracker.Tests/VisionPacketCaptureTests.cs`

## 指摘事項

- 指摘要約または「指摘なし」:
  - Blocking 指摘なし。R010 の validation / evidence collection としては、build、focused tests、diagnostics sample / legacy degraded の executable evidence、DebugHost UI normal path、RuntimeHost headless normal path を揃えた。
  - 既知の broad focused failure `RuntimeHostDependencyBoundaryContractTests.DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop` は、今回の実行でも再現した。ただし R010 blocker ではなく、過去 task 範囲外 hold のままでよいと判断する。
  - 判断理由: failure は `Tracker.DebugHost/Program.cs` の `TrackerCoordinator` / `VisionReceiverService` / publisher 系登録と `VisionReceiverService` の `trackerCoordinator.ProcessPacket` を検出している。一方で `Tracker/Design/RuntimeHost/runtime-host-plan.md` の RUNTIME-HOST-005 は DebugHost の `VisionReceiverService` が UDP decode、raw store、capture の後に `Tracker.Core.TrackerCoordinator.ProcessPacket` を呼ぶ adapter として残ることを許容している。RUNTIME-HOST-006 の焦点は Web rendering tick が operation loop を駆動しない read-side snapshot 化であり、`RuntimeHostDebugHostReadSideSnapshotBoundaryTests` はこの点を pass している。
  - 判断理由: `RuntimeHostDependencyBoundaryContractTests` の RuntimeHost project/source 境界は adjusted focused で pass しており、RuntimeHost 側が DebugHost / diagnostics replay UI / Blazor に依存しないことは確認できた。DebugHost 全体が standalone debug host として Core loop adapter を持つこと自体は、現行 design とコードの通常経路に残るため、R010 で最小修正できる blocker ではなく、test contract の再整理または別 task の設計判断として hold するのが妥当。
  - diagnostics sample evidence: `RuntimeHostDiagnosticsSampleBoundaryContractTests` と `VisionPacketCaptureTests` により、`diagnostics-samples.jsonl` sidecar、metadata の `DiagnosticsSampleSidecarPath` / `DiagnosticsSampleLog`、UI 非依存 sample loop、sample sidecar からの `Vision Input` / `ibis tracker` 復元を確認した。
  - legacy degraded evidence: `RuntimeHostDiagnosticsSampleBoundaryContractTests.Load_WithOnlyLegacyRenderSnapshotSidecarReportsUnsupportedDegradedLegacy` と `LoadFieldSourceFrame_ForVisionInputWithoutDiagnosticsSampleDoesNotFallbackToRenderSnapshot` により、旧 render snapshot sidecar only session が `unsupported degraded legacy` 扱いになり、Vision Input が旧 render snapshot fallback に戻らないことを確認した。

## 結果

- 結果:
  - RUNTIME-HOST-010 の focused validation / evidence collection は完了。report 以外のコード / test 修正は実施していない。
  - RuntimeHost focused normal path は `RuntimeHostOperationLoopTests` / `RuntimeHostScaffoldContractTests` と `Tracker.RuntimeHost` build、短時間 headless 起動で pass。
  - DebugHost UI normal path は `Tracker.DebugHost` build、read-side snapshot boundary tests、短時間 HTTP 起動と `/` の HTTP 200 で pass。実ブラウザ操作は行っていないが、R010 の最低限の手動または CLI evidence としては HTTP 200 と host startup log で代替可能と判断する。
  - UDP 実機 / SSL-Vision 実データ送信はこの環境では実施していない。代替 evidence として、RuntimeHost は `RuntimeHostOperationLoopTests` の fake SSL-Vision input が coordinator / publisher / latest snapshot store へ到達する executable proof と、headless 起動時の multicast receive 待ち log を採用した。
  - 親の `.gitignore` 差分は revert していない。capture artifact ignore pattern は `git check-ignore -v` で確認済み。

## リスク

- 未解決のリスクまたは後続対応:
  - 既知 broad focused failure は hold。R011 final review では、`RuntimeHostDependencyBoundaryContractTests.DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop` を現行 design に合わせて狭めるのか、DebugHost から Core loop adapter も除去する別 task を立てるのかを明示判断する必要がある。
  - 実ブラウザでの UI操作、SSL-Vision 実機 / simulator からの実 UDP packet 流入、official tracker packet の外部受信確認は未実施。今回の R010 では focused tests と短時間起動確認で代替した。
