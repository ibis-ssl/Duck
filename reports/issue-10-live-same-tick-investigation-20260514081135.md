# Sub-agent実行レポート

## タスク

- 目的: Issue #10 / RAW-VISION-013 の live Vision split / overlay で、Raw / Tracked / 3rd party tracker を比較可能にする同時取得または同一 render tick snapshot が実現できるか調査する。
- タスク種別: 調査

## sub-agentを使う理由

- 理由: ユーザー指示により、調査は gpt-5.5 high の sub-agent に委譲し、親はマネージャーとして report を裁定する。

## 対象範囲

- 対象: `Tracker.Server` の live raw vision store、tracked snapshot store、3rd party tracker receiver/store、Home/Vision 画面更新経路、diagnostics の alignment / overlay 実装の再利用可能性。

## 対象外

- 対象外: コード変更、テスト作成、設計書編集、PR本文更新、仕様決定。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `rg -n "RAW-VISION|raw-vision|Raw Vision|3rd party|diagnostics|VisionPacketStore|VisionReceiverService|CaptureOn|Issue #10|issue-10" /home/ibis/.codex/memories/MEMORY.md`
  - `sed -n '1,220p' /home/ibis/ssl/IbisDuck/reports/issue-10-live-same-tick-investigation-20260514081135.md`
  - `git status --short`
  - `rg -n "class .*Store|record .*Snapshot|VisionPacketStore|Tracked|TrackerConnection|Third|3rd|Diagnostics|Overlay|Raw Aggregate|Raw Camera|camera|snapshot|Capture|Receive|receiver" Tracker/Tracker.Server Tracker/Tracker.Core Tracker/Tracker.Tests -S`
  - `rg --files Tracker/Tracker.Server Tracker/Tracker.Tests Tracker/Tracker.Core | rg "(Vision|Diagnostics|Tracker|Tracked|Capture|Store|Receiver|Home|razor|Tests)"`
  - `find Tracker -maxdepth 4 -type d | rg "Tracker.Server|TrackerConnection|SslProto|Diagnostics|Capture|Tests"`
  - `nl -ba Tracker/Tracker.Server/Vision/VisionPacketStore.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Vision/VisionPacketSnapshot.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Server/Vision/VisionAggregateDetectionSnapshot.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Server/Vision/VisionCameraSnapshot.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackedSnapshotStore.cs | sed -n '1,240p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackedSnapshot.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerConnectionLibReceiverHostedService.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerConnectionLibSnapshotRecorder.cs | sed -n '1,260p'`
  - `rg --files TrackerConnectionLib | rg "(MultiTracker|TrackerState|UdpTracker|TrackerPacket|Manager|Options|Tests)"`
  - `nl -ba Tracker/Tracker.Server/Program.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Home.razor | sed -n '1,340p'`
  - `nl -ba Tracker/Tracker.Server/Components/Vision/TrackedVisionViewState.cs | sed -n '1,260p'`
  - `nl -ba TrackerConnectionLib/src/MultiTrackerManager.cs | sed -n '1,320p'`
  - `nl -ba TrackerConnectionLib/src/TrackerState.cs | sed -n '1,220p'`
  - `nl -ba TrackerConnectionLib/src/TrackerPacketAdapter.cs | sed -n '1,240p'`
  - `nl -ba TrackerConnectionLib/src/UdpTrackerReceiver.cs | sed -n '1,280p'`
  - `rg -n "MultiTrackerManager|TrackerConnectionLibSnapshotRecorder|Trackers|ActiveTracker|SourceRole|SourceLabel|TrackerUpdated|TrackerPacketAdapter|TrackerWrapperPacket" Tracker/Tracker.Server Tracker/Tracker.Tests TrackerConnectionLib -S`
  - `nl -ba Tracker/Tracker.Tests/TrackerLiveExternalTrackerReceiverTddTests.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Tests/TrackerConnectionLibAllTrackerSnapshotContractTests.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayModels.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs | sed -n '1,240p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs | sed -n '260,560p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '840,1130p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '500,650p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor | sed -n '240,410p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs | sed -n '554,620p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '1370,1495p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs | sed -n '520,680p'`
  - `nl -ba Tracker/Tracker.Tests/VisionPacketStoreTests.cs | sed -n '1,280p'`
  - `nl -ba Tracker/Tracker.Tests/TrackedVisionViewStateTests.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs | sed -n '1,260p'`
  - `rg -n "same tick|same-tick|render tick|snapshot|RAW-VISION-013|RAW-VISION-014|split|overlay|3rd party|third party|同時" Tracker/Tracker.Server/Design reports Tracker/Tracker.Tests -S`
  - `nl -ba Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Design/tasks-status.md | sed -n '1,120p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs | sed -n '1,320p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs | sed -n '1,220p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Vision/VisionReceiverService.cs | sed -n '1,280p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs | sed -n '1,180p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs | sed -n '259,360p'`
  - `nl -ba Tracker/Tracker.Tests/TrackerCaptureOnSessionSnapshotContractTests.cs | sed -n '360,410p'`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/issue-10-live-same-tick-investigation-20260514081135.md`
  - 確認: `Tracker/Tracker.Server/Vision/VisionPacketStore.cs`
  - 確認: `Tracker/Tracker.Server/Vision/VisionPacketSnapshot.cs`
  - 確認: `Tracker/Tracker.Server/Vision/VisionAggregateDetectionSnapshot.cs`
  - 確認: `Tracker/Tracker.Server/Vision/VisionCameraSnapshot.cs`
  - 確認: `Tracker/Tracker.Server/Vision/VisionReceiverService.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackedSnapshotStore.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackedSnapshot.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerConnectionLibReceiverHostedService.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerConnectionLibSnapshotRecorder.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
  - 確認: `Tracker/Tracker.Server/Program.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Home.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayModels.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayCanvas.razor`
  - 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
  - 確認: `Tracker/Tracker.Server/Components/Vision/TrackedVisionViewState.cs`
  - 確認: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - 確認: `Tracker/Tracker.Server/Design/tasks-status.md`
  - 確認: `TrackerConnectionLib/src/MultiTrackerManager.cs`
  - 確認: `TrackerConnectionLib/src/TrackerState.cs`
  - 確認: `TrackerConnectionLib/src/TrackerPacketAdapter.cs`
  - 確認: `TrackerConnectionLib/src/UdpTrackerReceiver.cs`
  - 確認: `Tracker/Tracker.Tests/VisionPacketStoreTests.cs`
  - 確認: `Tracker/Tracker.Tests/TrackedVisionViewStateTests.cs`
  - 確認: `Tracker/Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs`
  - 確認: `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
  - 確認: `Tracker/Tracker.Tests/TrackerLiveExternalTrackerReceiverTddTests.cs`
  - 確認: `Tracker/Tracker.Tests/TrackerConnectionLibAllTrackerSnapshotContractTests.cs`
  - 確認: `Tracker/Tracker.Tests/TrackerCaptureOnSessionSnapshotContractTests.cs`

## 指摘事項

- 指摘要約または「指摘なし」:
  - ユーザー確認が必要になり得る点: 「同時取得」が厳密な同一 packet timestamp、または raw / ibis tracked / 3rd party tracker が同じ receive callback で生成されたことを意味するなら、現行構成では満たせない。各 source は別 UDP stream / 別 store で、3rd party tracker は raw vision callback と独立して受信されるため。
  - 確認不要で設計側が選べる点: live UI の比較可能性は「同じ UI render tick で latest snapshot を固定し、各 layer に sample 時刻 / source 受信時刻 / frame timestamp / delta を表示する」方針で足りる。これは現在の `Home.razor` の 100ms refresh 方式、`VisionPacketStore.GetSnapshot()`、`TrackedSnapshotStore.GetSnapshot()`、`MultiTrackerManager<TrackerPacketAdapter>.Trackers` に沿う。
  - 確認不要で不採用にできる点: `TrackerPacketSnapshotLogWriter.GetLatestSnapshotsBySource()` を live Vision 画面の 3rd party tracker store として使う方針は避けるべき。これは CaptureOn session writer に結び付いており、CaptureOff では `Append` が停止し、live UI 用 store ではない。
  - 確認不要で採用できる点: 3rd party tracker の描画 DTO 化は diagnostics の `TrackerPacketSnapshotSemanticSummary` と `DiagnosticsFieldViewFactory.CreateTrackerSource*` の変換方針を再利用できる。ただし diagnostics log / sidecar reader 自体は live UI に持ち込まず、live 専用 snapshot model へ切り出すのが妥当。

## 結果

- 結果:
  - 既存 API 可否:
    - Raw: `VisionPacketStore.GetSnapshot()` が lock 内で latest packet / detection / per-camera / aggregate / geometry / receivedAt を clone して返す。Raw Aggregate / Raw Camera の live snapshot source として利用可能。
    - Tracked: `TrackedSnapshotStore.GetSnapshot()` が latest `TrackerFrame` / receivedAt / profile / publish count を lock 内で返す。`TrackedVisionViewState.FromSnapshot()` で field 描画 DTO へ変換可能。
    - 3rd party tracker: `MultiTrackerManager<TrackerPacketAdapter>` は `Trackers` に own / external / unknown の latest state を保持する。`Program.cs` では manager は常時 singleton 登録され、receiver は `Tracker:Receive:Enabled` の場合だけ hosted service 登録される。Vision 画面から inject して読むことは可能だが、現状は immutable UI snapshot API ではなく、mutable `TrackerState` の concurrent dictionary 公開である。
  - 同時取得粒度の分類:
    - 厳密な同一 packet timestamp: 不採用。Raw SSL-Vision と 3rd party tracker packet は別 stream で、既存 store に同一 timestamp join API もない。
    - 同じ receive callback: Raw と ibis tracked は `VisionReceiverService` が raw store 更新後に同じ callback の `receivedAt` で `TrackerCoordinator.ProcessPacket()` へ渡すため、内部処理上は近い。ただし 3rd party tracker は `TrackerConnectionLibReceiverHostedService` の別 callback なので全 source 共通にはできない。
    - 同じ UI render tick で latest snapshot 固定: 採用推奨。`Home.razor` は refresh tick ごとに raw / tracked を順に取得しており、ここに 3rd party latest snapshot 取得を加えた composite snapshot model を作れば、1 回の render で各 layer がずれない。
    - capture-time alignment: replay / diagnostics / CaptureOn 用には既存の `TrackerSnapshotAlignmentLogWriter` が latest-by-source と render / diagnostics / tracker snapshot timeline の対応を保存している。live Vision の primary 方針ではなく、保存 session の比較根拠として再利用する。
  - 推奨設計:
    - `Home.razor` 直書きの個別 state 更新を、`VisionLiveComparisonSnapshot` または同等の view-state composer に寄せ、1 render tick で `Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` source 一覧を同時に固定する。
    - 3rd party tracker は `MultiTrackerManager<TrackerPacketAdapter>` から latest `external` source を読み、source label が複数ある場合は diagnostics と同じく `External` aggregate と source-label 個別 option を出す。
    - geometry は raw `VisionPacketSnapshot.Geometry` を第一候補、なければ tracked `TrackedVisionViewState.Geometry` を fallback とする。3rd party tracker packet は field geometry の責任を持たせない。
    - split / overlay の UI 状態、Layer A/B visibility、source selector、same-source 1 layer 化は diagnostics の `TrackerDiagnosticsComparisonUiState` と `DiagnosticsFieldOverlay*` の既存 contract を参考にする。ただし live page は diagnostics log reader 依存を持たない。
  - 採用しない方針:
    - 3rd party tracker と Raw / Tracked を同一 receive callback に統合する方針は不採用。source の受信経路が別で、live viewer のために receiver architecture を強く結合する割に、ユーザーが求める比較表示の実効精度は UI tick 固定で満たせる。
    - CaptureOn sidecar writer を live store として使う方針は不採用。CaptureOn 有効時だけ意味を持つ保存機構で、Vision 画面の常時 live source には向かない。
  - 3rd party tracker 接続候補:
    - 最小候補: `MultiTrackerManager<TrackerPacketAdapter>.Trackers` を読み、`TrackerState.LastPacket.Packet` を `TrackerPacketSnapshotSemanticSummary.FromPacket()` 相当で DTO 化する。
    - 推奨候補: `ExternalTrackerSnapshotStore` のような live UI 用 store を追加し、`MultiTrackerManager.TrackerUpdated` を購読して immutable snapshot を返す。既存の `TrackerConnectionLibSnapshotRecorder` は CaptureOn 保存用として維持する。
  - TDD 候補:
    - `Tracker.Tests/VisionPacketStoreTests.cs`: Raw Aggregate / Raw Camera が render tick snapshot source として保持されること、clone 後に描画中 state が変わらないこと。
    - `Tracker.Tests/TrackedVisionViewStateTests.cs`: `Tracked` source の geometry fallback / balls / robots / timestamp metadata の view-state contract。
    - 新規候補 `VisionLiveComparisonSnapshotTests.cs`: 1 回の compose で raw / tracked / 3rd party latest snapshot が同じ `SampledAt` または render tick ID を共有し、個別 store 更新後も当該 snapshot が不変であること。
    - 新規候補 `VisionLiveExternalTrackerSnapshotStoreTests.cs`: `MultiTrackerManager` の own / external / unknown のうち 3rd party source option と per-label option を作り、`TrackerWrapperPacket` の semantic summary から balls / robots を描画 DTO 化すること。
    - 新規候補 `VisionSplitOverlayViewStateTests.cs`: split / overlay mode、Layer A/B source、visibility、same-source 1 layer 化、diagnostics 寄せ legend/details の contract。
    - `Tracker.Tests/DiagnosticsFieldViewFactoryTests.cs`: 3rd party tracker semantic summary から field 用 `SSL_DetectionBall` / `SSL_DetectionRobot` へ変換する既存 contract の再利用または共通化後の移動先 contract。

## リスク

- 未解決のリスクまたは後続対応:
  - `MultiTrackerManager.Trackers` は concurrent dictionary だが値の `TrackerState` は mutable class で、UI がそのまま読むと state の途中更新や protobuf packet の参照共有を抱える。live UI には clone / immutable snapshot 化の薄い store が必要。
  - 3rd party tracker receiver は `Tracker:Receive:Enabled` のときだけ起動する。Vision 画面の source selector では receiver disabled / no packet / CaptureOff を区別して表示しないと、「3rd party tracker がない」のか「受信設定が無効」なのか分からない。
  - strict timestamp alignment を要求する設計にしてしまうと、現行 live architecture では実装が大きくなり、Raw / Tracked / 3rd party の通常比較 UI を先に出す目的から外れる。
  - external tracker packet は geometry を持たないため、raw geometry 未受信かつ tracked geometry も未生成の初期状態では overlay field を描けない。geometry fallback / empty state を設計に明記する必要がある。
  - source label が重複する複数 external tracker、または uuid/sourceName が空の unknown tracker は、label だけでは UI 選択が曖昧になり得る。diagnostics と同様に role / label / remote endpoint を内部 key に含める設計が必要。
  - live UI の「同じ render tick」は表示 snapshot の固定であり、物理的な同時観測ではない。UI 上に sample time と各 source の receivedAt / frame timestamp / delta を出さないと、同時性の誤解が残る。
