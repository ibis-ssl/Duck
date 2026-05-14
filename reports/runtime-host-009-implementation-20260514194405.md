# RUNTIME-HOST-009 実装レポート

## 対象

RuntimeHost tracker operation loop と official packet publish normal path。

## Executor

Codex worker sub-agent `019e2616-8cc3-71b1-9ba8-118fe90616b9`。

`development-orchestrator` を入口として確認し、RUNTIME-HOST-009 は active tracking 上で `in-progress`、依存 RUNTIME-HOST-007 / RUNTIME-HOST-008 は完了済みであることを確認した。実装作業は `implementation-executor` 相当の worker 実行として進めた。CodexSkill repo は開始時点で `feedback-points/feedback-points.md` に既存差分があったため、Skill 側は更新せず、今回 scope 外として保持した。

## Scope

- RuntimeHost 側だけに SSL-Vision receiver / latest packet buffer / operation loop / tracker configuration resolver / DI 登録を追加した。
- `RuntimeHost:OperationLoopIntervalMilliseconds` を `RuntimeHostPeriodicTickSource` の周期として使い、operation loop は tick ごとに latest packet を `TrackerCoordinator.ProcessPacket` へ渡す。
- `Tracker` section から `TrackerRuntimeResolvedOptions` を解決し、`TrackerCoordinator`、`TrackedSnapshotStore`、`UdpTrackerPacketPublisher`、`TrackerPacketGenerator` を組み立てる normal path を追加した。
- DebugHost / Blazor / diagnostics replay UI / capture viewer への RuntimeHost project 依存は追加していない。
- RUNTIME-HOST-010 の manual evidence / final validation には踏み込んでいない。

## Changes

- `Tracker/Design/RuntimeHost/runtime-host-plan.md`
  - RUNTIME-HOST-009 の receiver、operation loop interval、Core coordinator / publisher / snapshot store normal path、DebugHost 非依存境界を追記した。
- `Tracker/Tracker.RuntimeHost/`
  - `RuntimeHostLifecycleService` を no-op scaffold から周期 operation loop hosted service へ変更した。
  - `RuntimeVisionReceiverService`、`RuntimeVisionPacketBuffer`、`RuntimeHostOperationLoop`、`RuntimeHostPeriodicTickSource` を追加した。
  - `RuntimeVisionReceiverOptions`、`RuntimeTrackerOptions`、`RuntimeTrackerConfigurationResolver` を追加した。
  - `RuntimeHostServiceCollectionExtensions` で Core tracker runtime normal path を DI 登録した。
  - `appsettings.json` に `VisionReceiver` と `Tracker` の default normal path 設定を追加した。
- `Tracker/Tracker.Tests/RuntimeHostOperationLoopTests.cs`
  - operation loop interval binding、publish destination / metadata binding、fake SSL-Vision input から coordinator / publisher / latest snapshot store へ到達する normal path を追加した。

## Tests / Build

Red:

- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostOperationLoopTests" -m:1 /nr:false`
  - 実装前: build failed。`RuntimeHostPeriodicTickSource`、`IRuntimeHostTickSource`、`RuntimeVisionPacketBuffer`、`RuntimeHostOperationLoop` 未実装を確認した。

Green / validation:

- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostOperationLoopTests" -m:1 /nr:false`
  - 3 passed。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostScaffoldContractTests|FullyQualifiedName~RuntimeHostDependencyBoundaryContractTests|FullyQualifiedName~RuntimeHost" -m:1 /nr:false`
  - 26 passed / 1 failed。
  - failed: `RuntimeHostDependencyBoundaryContractTests.DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop`。
  - failure 内容は DebugHost 側に既存の loop ownership marker `AddSingleton<TrackerCoordinator>`、`AddHostedService<VisionReceiverService>`、`trackerCoordinator.ProcessPacket` などが残っているという contract failure。R008 report でも既存の R008 範囲外 failure として扱われていたもので、RUNTIME-HOST-009 の RuntimeHost normal path 実装 scope 外。
- adjusted focused:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "(FullyQualifiedName~RuntimeHostScaffoldContractTests|FullyQualifiedName~RuntimeHostDependencyBoundaryContractTests|FullyQualifiedName~RuntimeHost)&FullyQualifiedName!~DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop" -m:1 /nr:false`
  - 26 passed。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false`
  - succeeded。0 warnings / 0 errors。
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`
  - succeeded。0 warnings / 0 errors。
- `git diff --check`
  - passed。

## Serena

- 使用あり。
- 開始時に Serena MCP `initial_instructions` を読み、`/home/ibis/ssl/IbisDuck` を activate した。
- `check_onboarding_performed` では onboarding 未実施と表示されたため `onboarding` を起動した。ただし今回の task scope 外の memory 書き込みは行っていない。
- コード調査では Serena `search_for_pattern` で `TrackerCoordinator`、`TrackedSnapshotStore`、`UdpTrackerPacketPublisher`、`RuntimeHostLifecycleService`、`VisionReceiverService` 周辺を探索し、`find_symbol` で `VisionReceiverService` と `TrackerPacketGenerator` の symbol body を確認した。

## Risks / Remaining Work

- 指定 focused filter のうち `RuntimeHostDependencyBoundaryContractTests.DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop` は既存の DebugHost read-side 移行未完了 contract として fail している。今回の RUNTIME-HOST-009 では RuntimeHost normal path の追加に限定し、DebugHost 側の loop ownership 解消は tracking 上の後続 review / final split 作業側で扱う。
- RuntimeHost の実 UDP receive / publish を使った manual evidence は RUNTIME-HOST-010 の scope として残した。
- RuntimeHost は latest packet buffer を tick ごとに消費するため、tick 間に複数 SSL-Vision packet が来た場合は latest packet を優先する。この挙動は R009 設計へ記録済みだが、高頻度 packet を全件 queueing する要否は実機 evidence 後に判断する余地がある。
