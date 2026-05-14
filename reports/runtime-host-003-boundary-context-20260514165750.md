# Sub-agent実行レポート

## タスク

RUNTIME-HOST-003 の diagnostics sample boundary / legacy degraded contract に必要な既存 code / test context を read-only で確認する。

## sub-agentを使う理由

test authoring worker と干渉しない read-only 調査を並列化し、diagnostics sample boundary の既存入口、false positive / false negative、適切な Red contract 観点を report-backed evidence として残すため。

## 対象範囲

- `Tracker/Tracker.Tests/` の diagnostics / replay / render snapshot / alignment 関連 tests
- `Tracker/Tracker.Server/Tracking` の diagnostics replay / render snapshot / alignment reader
- `Tracker/Tracker.Server/Vision` の raw latest snapshot boundary
- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`

## 対象外

- ファイル編集
- test 作成
- build / test 実行
- tracking 更新
- commit / PR update

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `sed -n '1,260p' reports/runtime-host-003-boundary-context-20260514165750.md`
- `rg -n "RUNTIME-HOST-003|diagnostics sample|sample boundary|legacy degraded|degraded|render snapshot|alignment|RawLatest|latest snapshot" Tracker/Design Tracker/Tracker.Core/Design reports -g '*.md'`（`Tracker/Tracker.Core/Design` は存在せず error。current canonical root は `Tracker/Design`）
- `rg -n "Diagnostics|Replay|RenderSnapshot|Alignment|RawLatest|LatestSnapshot|latest snapshot|degraded|legacy" Tracker/Tracker.Tests Tracker/Tracker.Server/Tracking Tracker/Tracker.Server/Vision`
- `rg --files Tracker/Tracker.Tests Tracker/Tracker.Server/Tracking Tracker/Tracker.Server/Vision Tracker/Design/DebugHost | sort`
- `nl -ba Tracker/Design/tasks-status.md | sed -n '1,90p'`
- `nl -ba Tracker/Design/phases-status.md | sed -n '1,80p'`
- `nl -ba Tracker/Design/DebugHost/raw-vision-viewer-plan.md | sed -n '230,285p'`
- `nl -ba Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md | sed -n '1,90p'`
- `rg -n "latest-before|future|SidecarMissing|SidecarEmpty|SidecarCorrupt|MetadataMissing|VisionInput|ReplayTimeline|saved-session-alignment|CandidateMissing|NoCandidateSnapshot|render snapshot" Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs Tracker/Tracker.Tests/TrackerDiagnosticsReplayTimelineIndexTests.cs Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs Tracker/Tracker.Tests/VisionPacketStoreTests.cs Tracker/Tracker.Tests/TrackerCoordinatorDiagnosticsCaptureTests.cs`
- `rg -n "WorldFrameCommitted|RenderSnapshot|Capture|TrackerRenderSnapshot|Diagnostics|LogTrackerDiagnostics|CaptureRenderSnapshot|Latest|Store" Tracker/Tracker.Server/Tracking Tracker/Tracker.Server/Vision | head -n 240`
- `rg -n "latest-before|saved-session-alignment|unsupported|degraded|legacy|Sidecar|ReplayTimeline|RenderSnapshot|VisionInput|FindRenderSnapshot|TryFind|Load\\(" Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs Tracker/Tracker.Server/Tracking/TrackerDiagnosticsReplayTimelineIndex.cs Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogReader.cs Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
- `rg -n "Latest|Snapshot|CaptureRenderTickSnapshot|RawAggregate|RawCamera|TrackedSnapshot|ThirdParty|lock|Concurrent|Update" Tracker/Tracker.Server/Vision/VisionPacketStore.cs Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs Tracker/Tracker.Server/Vision/VisionReceiverService.cs Tracker/Tracker.Server/Tracking/TrackedSnapshotStore.cs`
- `nl -ba ... | sed -n ...` により、下記「対象ファイル」の該当範囲を line 番号付きで確認した。

## 対象ファイル

- `/home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `/home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `reports/runtime-host-003-boundary-context-20260514165750.md`
- `Tracker/Design/tasks-status.md`
- `Tracker/Design/phases-status.md`
- `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
- `Tracker/Tracker.Tests/TrackerDiagnosticsReplayTimelineIndexTests.cs`
- `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`
- `Tracker/Tracker.Tests/VisionPacketStoreTests.cs`
- `Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs`
- `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs`
- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsReplayTimelineIndex.cs`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs`
- `Tracker/Tracker.Server/Vision/VisionPacketStore.cs`
- `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs`

## 指摘事項

### RUNTIME-HOST-003 の exit criteria ごとの参照先

1. diagnostics sample tick が tracker committed frame cadence / `WorldFrameCommitted` に依存しないこと
   - task の該当 exit criteria は `Tracker/Design/tasks-status.md:13`-`17`、固定残タスク側は `Tracker/Design/tasks-status.md:42` と `:65`。
   - 現状の legacy 経路は `TrackerCoordinator.ExecuteUpdates` が engine update 後に `LogTrackerDiagnostics` を呼び、diagnostics frame がある場合だけ `CaptureDiagnosticsEntry` する構造。参照先は `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs:145`-`155`。
   - `LogTrackerDiagnostics` は `result.CommittedFrames.Count == 0` で return し、最新 committed frame の `SourceDetections` から raw details を組み立てる。参照先は `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs:14`-`29`、`:31`-`:43`、`:67`-`:68`。
   - diagnostics sample sidecar の Red contract は、この legacy committed frame path を肯定しない形にする必要がある。例えば「raw latest が更新されても committed frame が無ければ diagnostics sample が進まない」現状を Red として固定するなら、`CommittedFrames` や `ShouldLogTrackerDiagnostics` に依存しない expected API / record 名を使うべき。

2. Diagnostics `Vision Input` が diagnostics sample sidecar から復元されること
   - 設計は `Tracker/Design/DebugHost/raw-vision-viewer-plan.md:240` で、`Vision Input` は selected tick の render frame ではなく diagnostics sample tick に保存された latest raw snapshot から復元すると定義している。
   - 現状 reader の `VisionInput` は sidecar ではなく selected render snapshot 扱い。`TrackerDiagnosticsComparisonViewStateReader.LoadFieldSourceFrame` は `VisionInput` を `TrackerDiagnosticsFieldSourceFrameStatus.VisionInput` で返し、message も "selected render snapshot"。参照先は `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs:279`-`:285`。
   - `IbisTracker` も render snapshot 扱いで `IbisTrackerRenderSnapshot` を返す。参照先は `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs:287`-`:293`。
   - raw latest snapshot の既存境界は `VisionPacketStore.GetSnapshot` と `StorePacket`。snapshot clone / per-camera latest / aggregate を扱う参照先は `Tracker/Tracker.Server/Vision/VisionPacketStore.cs:19`-`:39`、`:54`-`:81`。UI render tick 用 immutable snapshot は `VisionLiveComparisonSnapshotComposer.CaptureRenderTickSnapshot` で `visionPacketStore.GetSnapshot()` から固定される。参照先は `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs:472`-`:526`。
   - 既存 tests では `VisionPacketStoreTests` が detection/geometry/multiple camera latest を固定し、`VisionLiveComparisonViewStateTests` が same render tick immutable snapshot を固定している。参照先は `Tracker/Tracker.Tests/VisionPacketStoreTests.cs:12`-`:45`、`:50`-`:86`、`:106`-`:160`、`Tracker/Tracker.Tests/VisionLiveComparisonViewStateTests.cs:50`-`:71`、`:140`-`:191`。

3. 旧 render snapshot sidecar が unsupported / degraded legacy であること
   - 設計は `Tracker/Design/DebugHost/raw-vision-viewer-plan.md:238` で旧形式 / current limitation として render snapshot 経路が `WorldFrameCommitted` / tracker committed frame cadence に制限されると明記し、`:258` で旧 render snapshot sidecar の高コスト互換 layer を設計せず unsupported / degraded legacy session と扱ってよいと明記している。
   - CLI/UI 詳細設計も `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md:51`-`:55` で旧 diagnostics log / 旧 render snapshot sidecar は legacy / best-effort / degraded 表示に留め、新規 write cadence / bounded lookup / diagnostics sample sidecar を犠牲にしないと定義している。
   - 現状の旧 render snapshot reader はまだ正常 reader として frame keyed index を返す。参照先は `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs:37`-`:52`、`:58`-`:76`、`:122`-`:160`。既存 tests も旧 render snapshot を復元できることを肯定している。参照先は `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs:20`-`:57`、`:59`-`:101`。
   - Red contract では「旧 render snapshot reader を削除する」ではなく、「diagnostics sample sidecar がない新規機能の正常系として扱わない」「UI/state が degraded legacy status/message を出す」を固定する方が設計に合う。

### false positive / false negative になりやすい点

- `TrackerDiagnosticsComparisonViewStateTests` には unified replay timeline / latest-before / no future fallback の既存契約がある。参照先は `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs:322`-`:385`、`:391`-`:438`、`:444`-`:511`、`:517`-`:559`。これらは RUNTIME-HOST-003 の 2nd/3rd criteria に近いが、現状は alignment v2 / tracker snapshot sidecar 中心で、diagnostics sample sidecar 由来の `Vision Input` を固定していない。ここだけを流用すると false positive になる。
- sidecar unavailable の non-blocking status test は `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs:1018`-`:1065` にあるが、これは tracker snapshot sidecar の missing / empty / corrupt / metadata missing / not-created を区別する契約であり、旧 render snapshot sidecar の degraded legacy status とは別。ここをそのまま degraded legacy の証拠にすると false positive になる。
- `TrackerDiagnosticsReplayTimelineIndexTests` は fastest tracker cadence と render latest-before hold を固定している。参照先は `Tracker/Tracker.Tests/TrackerDiagnosticsReplayTimelineIndexTests.cs:12`-`:27`、`:29`-`:49`、`:51`-`:72`。ただし sample tick が tracker committed frame cadence から独立すること自体は、alignment records を test data で直接与えているだけなので false positive に注意。
- `TrackerDiagnosticsComparisonViewStateReader` の selected replay timeline path は aligned tick、latest-before、NoCandidate の順に評価する。参照先は `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs:568`-`:590`、`:650`-`:670`、`:801`-`:862`、`:1125`-`:1136`。future fallback を避ける既存根拠にはなるが、sample sidecar の存在確認にはならない。
- `VisionLiveComparisonSnapshotComposer` の immutable render tick は read-side UI 境界の根拠にはなるが、diagnostics logging/replay sample sidecar の保存境界ではない。`CaptureRenderTickSnapshot` の test だけで RUNTIME-HOST-003 を満たした扱いにすると false positive になる。

### 設計文書と task status のずれ

- blocking になる大きなずれは見つからない。`Tracker/Design/tasks-status.md:13`-`:17`、`:42`、`:65` と `Tracker/Design/phases-status.md:17` は、RUNTIME-HOST-003 を verification の Red contract task として一致している。
- 軽微な stale 表現として、`Tracker/Design/DebugHost/raw-vision-viewer-plan.md:246` は「この挙動は RAW-VISION-014 の TDD contract と RAW-VISION-015 の修正対象」と書いたまま。現在の active tracking は `Tracker/Design/tasks-status.md:39` で RuntimeHost / DebugHost 分離 scope では `RUNTIME-HOST-001` から `RUNTIME-HOST-011` に固定し、`RAW-VISION-*` を追加しないとしているため、将来設計 cleanup 候補。ただし同段落の latest-before / no future fallback 内容自体は既存実装・tests と整合している。
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md:5` は `TRACKER-040` 以降という歴史的表現を残しているが、同文書 `:11`-`:15` と `:51`-`:55` は DebugHost / legacy degraded 方針と整合しており、RUNTIME-HOST-003 の blocker ではない。

### 旧 legacy render snapshot path を degraded 扱いにする注意点

- `TrackerRenderSnapshotLogReaderTests` は旧 render snapshot が読めることを既存正常系として固定しているため、RUNTIME-HOST-003 の Red test で既存 test と直接矛盾させると、設計より強い破壊変更になる。degraded 扱いは reader の復元可否ではなく、diagnostics sample sidecar 主経路の state/status/message 側に寄せるのが安全。
- `TrackerRenderSnapshotLogReader.ReadFrame` は diagnostics log が `TrackerDiagnosticsLogReader.ListFiles()` に含まれることを前提に同名 `.render-snapshots.jsonl.gz` を解決する。参照先は `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs:58`-`:76`、`:156`-`:160`。degraded legacy の contract は、この basename 解決を新規 sample sidecar lookup の代替主経路にしないことを確認すべき。
- 旧 render snapshot 由来の `Vision Input` は selected render frame の source detections であり、raw SSL-Vision latest snapshot そのものではない。`LogTrackerDiagnostics` も `newestFrame.SourceDetections` を使うため、raw packet cadence を失う可能性がある。参照先は `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs:24`-`:43`。

## 結果

read-only 調査として、RUNTIME-HOST-003 の 3 exit criteria に対する既存 code/test context を確認した。build / test 実行、test 作成、tracking 更新、commit / PR update は実施していない。

findings:

- 現状の diagnostics `Vision Input` / ibis tracker Field source は selected render snapshot 扱いであり、diagnostics sample sidecar 由来ではない。Red contract の主な対象は `TrackerDiagnosticsComparisonViewStateReader` の `VisionInput` / `IbisTrackerRenderSnapshot` shortcut と、`TrackerCoordinator` の committed frame driven diagnostics path。
- raw latest snapshot boundary の既存参照先は `VisionPacketStore` と `VisionLiveComparisonSnapshotComposer`。ただしこれは live UI render tick 用で、diagnostics sample sidecar 保存 / replay 用の境界はまだ未実装に見える。
- 旧 render snapshot sidecar は既存 reader/test で「読める」ことが固定されている。RUNTIME-HOST-003 では reader 破壊ではなく、sample sidecar がない session を unsupported / degraded legacy として status/message で区別する contract が妥当。
- 設計と active task status は概ね整合。軽微な stale 表現として `raw-vision-viewer-plan.md` に `RAW-VISION-014` / `RAW-VISION-015` 参照が残る。

## リスク

- Red tests が既存 alignment v2 / replay timeline tests を流用しすぎると、diagnostics sample sidecar 未実装でも通る false positive になる。
- `VisionPacketStore` / `VisionLiveComparisonSnapshotComposer` の immutable snapshot contract をそのまま diagnostics sample sidecar contract とみなすと、UI render tick と diagnostics sample tick の責務境界が混ざる。
- 旧 render snapshot reader の既存正常系を壊す Red test にすると、設計上の「legacy / best-effort / degraded 表示」と「完全削除」を混同する false negative になる。
- 今回は read-only 調査のため、実際の compile 結果や focused test の Red 状態は未確認。
