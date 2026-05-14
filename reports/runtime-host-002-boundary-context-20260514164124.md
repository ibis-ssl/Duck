# Sub-agent実行レポート

## タスク

RUNTIME-HOST-002 の dependency boundary contract に必要な既存 code / test context を read-only で確認する。

## sub-agentを使う理由

実装 worker が test authoring を進めている間に、干渉しない read-only 調査を並列化し、review と親側判断に使える確認観点を report-backed evidence として残すため。

## 対象範囲

- `Tracker/Tracker.Tests/` の既存 contract tests
- `Tracker/Tracker.Server/` の project / namespace / diagnostics / UI 境界
- `Tracker/Tracker.Core/` の public/shared boundary
- solution / project references
- RUNTIME-HOST-002 の test 観点に関係する既存ファイル

## 対象外

- ファイル編集
- test 作成
- build / test 実行
- tracking 更新
- commit / PR update
- RUNTIME-HOST-003 以降の実装調査

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `rg -n "RUNTIME-HOST|RuntimeHost|DebugHost|dependency boundary" /home/ibis/.codex/memories/MEMORY.md`
- `sed -n '1,240p' reports/runtime-host-002-boundary-context-20260514164124.md`
- `sed -n '1,240p' Tracker/Design/tasks-status.md`
- `sed -n '1,260p' Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `sed -n '1,220p' Tracker/Tracker.Server/Tracker.Server.csproj`
- `sed -n '1,220p' Tracker/Tracker.Core/Tracker.Core.csproj`
- `rg --files -g '*.sln' -g '*.slnx' -g '*.csproj'`
- `rg --files Tracker/Tracker.Tests | sort`
- `rg -n "ProjectReference|RuntimeHost|DebugHost|Tracker\\.Server|Tracker\\.Core|diagnostics|Diagnostics|Contract|boundary|Boundary|Dependency|Assembly|csproj|sln" Tracker/Tracker.Tests`
- `rg -n "Project\\(|ProjectReference|Tracker\\.Server|Tracker\\.Core|Tracker\\.RuntimeHost|Tracker\\.DebugHost" -g '*.sln' -g '*.slnx' -g '*.csproj'`
- `git status --short`
- `sed -n '1,220p' Tracker/Tracker.Tests/Tracker.Tests.csproj`
- `sed -n '1,260p' Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs`
- `sed -n '1,260p' Tracker/Tracker.Tests/Contracts/TrackerCoreContractSurfaceTests.cs`
- `sed -n '1,260p' Tracker/Tracker.Tests/Contracts/TrackerContractFixture.cs`
- `sed -n '1,260p' Tracker/Tracker.Server/Program.cs`
- `rg -n "class TrackerCoordinator|record.*Snapshot|Latest|Immutable|WorldFrameCommitted|ITrackerObserver|ProcessPacket|AddHostedService|TrackerPacketGenerator|ITrackerPacketPublisher|TrackerDiagnostics|DiagnosticsPlayback|TrackerRenderSnapshot|TrackerPacketSnapshotLog" Tracker/Tracker.Server Tracker/Tracker.Core Tracker/Tracker.CaptureReplay`
- `find Tracker -maxdepth 2 -type d | sort`
- `sed -n '1,220p' Duck.slnx`
- `sed -n '1,280p' Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `sed -n '1,180p' Tracker/Tracker.Server/Vision/VisionReceiverService.cs`
- `sed -n '1,180p' Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs`
- `rg -n "class TrackedSnapshotStore|record TrackedSnapshotStore|TrackedSnapshotStore" Tracker/Tracker.Server Tracker/Tracker.Tests`
- `sed -n '1,220p' Tracker/Tracker.Server/Tracking/TrackedSnapshotStore.cs`
- `sed -n '1,180p' Tracker/Tracker.Tests/TrackerCoordinatorFrameFlowTests.cs`
- `sed -n '1,220p' Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
- `sed -n '500,720p' Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
- `sed -n '1,220p' Tracker/Tracker.Tests/Contracts/TrackerCoreReferenceTests.cs`
- `rg -n "XDocument|ProjectReference|RepositoryPath|FindRepositoryRoot|ReadRepositoryFile|DirectoryInfo\\(AppContext.BaseDirectory\\)" Tracker/Tracker.Tests`
- `find Tracker/Tracker.Server -maxdepth 3 -type f \\( -name '*.cs' -o -name '*.razor' \\) | sort`
- `nl -ba Tracker/Tracker.Server/Program.cs | sed -n '1,110p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs | sed -n '1,120p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackedSnapshotStore.cs | sed -n '1,120p'`
- `nl -ba Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs | sed -n '450,535p'`
- `nl -ba Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs | sed -n '1,180p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Home.razor | sed -n '1,40p'`
- build / test は指示により未実行。

## 対象ファイル

- `reports/runtime-host-002-boundary-context-20260514164124.md`
- `Tracker/Design/tasks-status.md`
- `Tracker/Design/RuntimeHost/runtime-host-plan.md`
- `Duck.slnx`
- `Tracker/Tracker.Tests/Tracker.Tests.csproj`
- `Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs`
- `Tracker/Tracker.Tests/Contracts/TrackerCoreReferenceTests.cs`
- `Tracker/Tracker.Tests/Contracts/TrackerCoreContractSurfaceTests.cs`
- `Tracker/Tracker.Tests/Contracts/TrackerContractFixture.cs`
- `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
- `Tracker/Tracker.Tests/TrackerCoordinatorFrameFlowTests.cs`
- `Tracker/Tracker.Tests/TrackerCoordinatorTestFactory.cs`
- `Tracker/Tracker.Server/Tracker.Server.csproj`
- `Tracker/Tracker.Core/Tracker.Core.csproj`
- `Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj`
- `Tracker/Tracker.Server/Program.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs`
- `Tracker/Tracker.Server/Tracking/TrackedSnapshotStore.cs`
- `Tracker/Tracker.Server/Tracking/TrackedSnapshot.cs`
- `Tracker/Tracker.Server/Vision/VisionReceiverService.cs`
- `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs`
- `Tracker/Tracker.Server/Components/Pages/Home.razor`
- `Tracker/Tracker.Core/Engine/ITrackerObserver.cs`
- `Tracker/Tracker.Core/Engine/TrackerUpdateResult.cs`

## 指摘事項

- RUNTIME-HOST-002 の設計上の boundary は、`Tracker.RuntimeHost` が `Tracker.DebugHost` / `Tracker.Server` / Web UI / diagnostics replay UI project を参照しないこと、RuntimeHost source が diagnostics logging / replay / Blazor UI namespace を直接参照しないこと、DebugHost が tracker operation loop を主責務として持たず latest immutable snapshot または published output を読む側であること。
- 現在の solution は `Duck.slnx` に `SslProto`、`TrackerConnectionLib`、`TrackerConnectionLibExample`、`Tracker.Core`、`Tracker.Server` だけを含む。`Tracker.RuntimeHost` / `Tracker.DebugHost` project はまだ存在しない。
- 現在の project reference は、`Tracker.Server` が `Tracker.Core`、`TrackerConnectionLib`、`SslProto` を参照し、`Tracker.Core` は `SslProto` のみを参照する。`Tracker.CaptureReplay` は `Tracker.Core` と `Tracker.Server` を参照するため、RuntimeHost の禁止参照候補に入れるのが妥当。
- 既存 test style は xUnit + repository root 探索 + reflection / source text / XML 解析の組み合わせが既にある。`Tracker.Tests` は `Xunit` global using を csproj で持ち、contract test には `何を確認しているか:` XML doc が付いている。
- 並列 worker の作業中と思われる `Tracker/Tracker.Tests/RuntimeHostDependencyBoundaryContractTests.cs` は、`XDocument` で `ProjectReference` を読む test、RuntimeHost source token scan、DebugHost source token scan の 3 本で構成されている。`git status --short` では同ファイルは untracked だったため、この調査では読み取りのみで扱った。
- DebugHost read-side assertion が現時点で触れそうな既存責務は `Tracker.Server.Tracking.TrackedSnapshotStore`、`Tracker.Server.Tracking.TrackedSnapshot`、`Tracker.Server.Vision.VisionLiveComparisonSnapshotComposer`、`Tracker.Server.Components.Pages.Home`。`TrackedSnapshotStore.GetSnapshot()` は latest frame / receivedAt / active profile / publish count を一貫 snapshot として返し、`VisionLiveComparisonSnapshotComposer.CaptureRenderTickSnapshot()` は raw / tracked / third party snapshot を 1 回の render tick で固定する。
- 現行 `Tracker.Server` はまだ operation loop を所有している。`Program.cs` は `ITrackerEngine`、`TrackerPacketGenerator`、`TrackerCoordinator`、`VisionReceiverService` を登録し、`VisionReceiverService` が受信 packet を `trackerCoordinator.ProcessPacket(...)` に渡す。`TrackerCoordinator.DispatchResult` は `WorldFrameCommitted` で `TrackedSnapshotStore.UpdateLatestFrame`、render snapshot capture、tracker packet publish、observer 通知を行う。
- false positive を避けるには、単純な `"TrackerDiagnostics"` token scan は DebugHost 内では正当な diagnostics UI 実装まで失敗させる可能性がある。RuntimeHost source に限定するか、DebugHost 側は loop ownership marker と read-side marker を分ける必要がある。
- false negative を避けるには、`ProjectReference` だけでなく source token も見る必要がある。NuGet / framework reference 経由の Blazor UI、`Microsoft.AspNetCore.Components`、`AddRazorComponents`、`MapRazorComponents`、diagnostics replay 型名の直接参照は project reference だけでは検出できない。
- DebugHost read-side assertion では、`TrackedSnapshotStore` という現在名だけを許可 token にすると rename 後のより良い名前、例えば `LatestImmutableSnapshot` / `PublishedTrackerOutput` への移行を false negative にする恐れがある。逆に `TrackedSnapshotStore` だけを read-side evidence とすると、DebugHost が同時に loop を所有していても read-side token が存在するだけで通るため、loop ownership marker の禁止とセットにする必要がある。

## 結果

- RUNTIME-HOST-002 の test が見るべき既存 boundary は、project reference graph と source namespace / token boundary の二層で確認できた。
- RuntimeHost 側の許容参照は現状の設計からは `Tracker.Core`、`SslProto`、必要に応じた runtime 入出力境界に寄せるべきで、`Tracker.Server`、将来の `Tracker.DebugHost`、`Tracker.CaptureReplay`、Blazor / diagnostics replay UI は禁止参照として扱うのが自然。
- DebugHost 側の read-side responsibility は、現時点のコードでは `Tracker.Server.Tracking.TrackedSnapshotStore.GetSnapshot()` と `Tracker.Server.Vision.VisionLiveComparisonSnapshotComposer.CaptureRenderTickSnapshot()` が最も近い既存境界である。ただし現行 `Tracker.Server` はまだ `TrackerCoordinator` と `VisionReceiverService` を所有しているため、RUNTIME-HOST-002 の contract は現時点では Red として成立する。
- 既存 helper style として、`FindRepositoryRoot()` で repo root を探して repository file を読む pattern、`XDocument` による project file 解析、reflection による contract surface assertion、`TrackerContractFixture` / `TrackerCoordinatorTestFactory` による runtime object 組み立てが確認できた。
- 指示どおり build / test / tracking 更新 / commit / PR update は実行していない。

## リスク

- `Tracker.RuntimeHost` / `Tracker.DebugHost` がまだ存在しない段階の Red contract は存在確認で落ちるため、禁止参照の細部まで同時に検証されない。実装後に token scan が本当に目的の違反を拾うか再確認が必要。
- source text token scan はコメント、XML doc、文字列 literal でも hit するため、禁止 token を広くしすぎると false positive になる。特に DebugHost は diagnostics UI を持つ責務があるため、diagnostics token は RuntimeHost 側に限定するのが安全。
- DebugHost が `TrackerCoordinator` を直接持たないだけでは、別名 wrapper / hosted service / factory 経由で operation loop を所有する false negative が残る。DI 登録、hosted service、`ProcessPacket` 呼び出し、`ITrackerEngine` / `ITrackerPacketPublisher` / `TrackerPacketGenerator` の配置を合わせて見る必要がある。
- `TrackedSnapshotStore` は現在 `Tracker.Server.Tracking` namespace にあり、rename 後に `Tracker.DebugHost` 側へ残るか、shared boundary へ移るかは RUNTIME-HOST-005/006 で変わり得る。RUNTIME-HOST-002 では read-side token の名前を固定しすぎない方が後続 rename の邪魔になりにくい。
- `Tracker.CaptureReplay` は現在 `Tracker.Server` に依存しているため、RuntimeHost の禁止参照には含めやすい一方、DebugHost 側では正当な debug / replay 責務として残る可能性がある。RuntimeHost と DebugHost の禁止対象を混同しないこと。
