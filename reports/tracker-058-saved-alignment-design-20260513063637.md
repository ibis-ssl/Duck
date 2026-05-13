# Sub-agent実行レポート

## タスク

- 目的: TRACKER-058 の保存時対応付け方式を設計し、必要な設計ドキュメントを更新する。
- タスク種別: design

## sub-agentを使う理由

- 理由: ユーザー指示により設計は gpt-5.5 high sub-agent に任せる。今回の変更は capture sidecar / metadata / diagnostics replay の外部挙動とファイル形式に影響するため、設計を実装より先に固定する。

## 対象範囲

- 対象: ER-Force のように `TrackedFrame.timestamp` が ibis own と異なる外部 tracker を、新規 capture で replay / scrub / Field source 表示できるようにする保存時対応付け設計。既存ログ救済は必須ではない。

## 対象外

- 対象外: 実装コード変更、テストコード変更、既存 capture の完全救済、大規模 socket abstraction、外部 ER-Force プロセス制御。

## 実行コマンド

- 実行コマンド:
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
- `sed -n '1,260p' reports/tracker-058-er-force-replay-investigation-20260513062747.md`
- `sed -n '1,260p' reports/tracker-058-saved-alignment-design-20260513063637.md`
- `git status --short`
- `rg -n "CaptureOn|capture|tracker-packet-snapshots|tracker snapshot|snapshot sidecar|sidecar|diagnostics|replay|metadata|TrackerFrame|TrackedFrame|receivedAt|comparison" Tracker/Tracker.Core/Design Tracker/Tracker.Server README.md reports -g '*.md'`
- `sed -n '1,220p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `sed -n '120,190p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `sed -n '1,240p' Tracker/Tracker.Server/README.md`
- `sed -n '1,220p' Tracker/Tracker.Core/Design/tasks-status.md`
- `sed -n '1,160p' Tracker/Tracker.Core/Design/phases-status.md`
- `rg -n --glob '!**/bin/**' --glob '!**/obj/**' "TrackerSnapshotSidecarPath|TrackerSnapshotLog|TrackerSnapshotSources|tracker-packet-snapshots|RenderSnapshotPath|DiagnosticsLogPath|metadata|SessionFolder|TrackerPacketSnapshot|TrackerDiagnosticsComparisonSelectedEntry|LoadFieldSourceFrame|FindNearest|ComparisonSnapshotIndex|SemanticSummary|sourceLabel|remoteEndpoint" Tracker/Tracker.Server Tracker/Tracker.CaptureReplay Tracker/Tracker.Tests -g '*.cs'`
- `rg -n --glob '!**/bin/**' --glob '!**/obj/**' "tracker-packet-snapshots|TrackerSnapshotSidecarPath|TrackerSnapshotAlignment|alignment|CaptureOn|TrackerPacketSnapshot" Tracker -g '*.csproj' -g '*.cs'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '35,180p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '180,320p'`
- `rg -n "nearest timestamp|own / nearest|nearest-timestamp|ibis own timestamp|最も近い" Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md Tracker/Tracker.Server/README.md Tracker/Tracker.Core/Design/tracker-architecture-plan.md reports/tracker-058-saved-alignment-design-20260513063637.md`
- `git diff --check`
- `git status --short`

## 対象ファイル

- 変更または確認したファイル:
- 変更: `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- 変更: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- 変更: `Tracker/Tracker.Server/README.md`
- 変更: `Tracker/Tracker.Core/Design/tasks-status.md`
- 変更: `reports/tracker-058-saved-alignment-design-20260513063637.md`
- 確認: `reports/tracker-058-er-force-replay-investigation-20260513062747.md`
- 確認: `Tracker/Tracker.Core/Design/phases-status.md`
- 確認: `Tracker/Tracker.Server/Vision/VisionPacketCaptureFile.cs`
- 確認: `Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotRecord.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogReader.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
- 確認: `Tracker/Tracker.Tests/TrackerCaptureOnSessionSnapshotContractTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerComparisonSourceTddTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerReplayIntegrationTddTests.cs`
- 確認: `Tracker/Tracker.Tests/CaptureReplayTests.cs`

## 指摘事項

- 指摘要約または「指摘なし」:
- 設計対象 doc は `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`、`Tracker/Tracker.Core/Design/tracker-architecture-plan.md`、`Tracker/Tracker.Server/README.md`、進捗反映として `Tracker/Tracker.Core/Design/tasks-status.md` と判定した。`phases-status.md` はすでに `TRACKER-058` の ER-Force Field 表示を完了条件に含んでおり、大きな追加タスク番号は不要と判定した。
- 調査 report の A案は既存ログ救済に強いが、ユーザー方針「既存ログは救わなくていい」に反するため主案にしない。新規 capture の保存時に diagnostics entry / render snapshot / tracker source snapshot の対応付けを保存する方式を主案にする。
- `tracker-packet-snapshots.jsonl` へ diagnostics entry 対応を埋め込む案は、受信 packet の主記録と replay 用 index の責務が混ざり、破損時に snapshot 保存の破損か alignment 破損かを切り分けにくい。別 sidecar `tracker-snapshot-alignment.jsonl` を metadata から辿る方式を採用する。
- alignment record は diagnostics log line number、tracked frame number、diagnostics entry timestamp、ibis `TrackerFrame.data_timestamp_ns`、render snapshot frame number、session-relative time、capture-time `receivedAt`、source key、tracker snapshot record index、tracker snapshot `receivedAt`、matching rule、delta、aggregate/tie-break、status を持つ設計にした。
- ER-FORCE のように同一 source label / uuid が複数 remote endpoint に分かれる場合、保存上の source key は `sourceRole + sourceLabel + sourceUuid + remoteEndpoint` とし、UI の `External` / source label は aggregate として代表 snapshot を選ぶ。tie-break は absolute receivedAt delta、record index、remote endpoint ordinal の順に固定し、alignment record に理由を残す。
- 新規 capture の `/diagnostics` / `Tracker.CaptureReplay` は保存済み alignment を優先する。alignment がない既存 capture は unsupported または `legacy-nearest-timestamp` best-effort として明示し、既存ログ救済を主経路へ昇格しない。
- 100MB 超 / 長時間ログ対策として、log open 時に alignment sidecar を軽量 index 化し、diagnostics entry stable key と Field source key から record を直接引く。scrub / playback tick / source selector 変更時に snapshot sidecar または alignment sidecar 全体を再読込しない。

## 結果

- 結果:
- 採用方式: `tracker-snapshot-alignment.jsonl` を新設する別 sidecar 方式。metadata には `TrackerSnapshotAlignmentPath` と `TrackerSnapshotAlignmentLog` 相当の状態を追加し、snapshot sidecar と alignment sidecar の状態を別々に表示・診断する。
- 保存時対応付け仕様: CaptureOn 中に diagnostics entry / render snapshot / tracker source snapshot を同一 session timeline へ載せ、external tracker の `TrackedFrame.timestamp` が ibis own と同じ時刻系でなくても、capture-time `receivedAt`、session-relative time、diagnostics entry time で replay Field に対応 snapshot を選べるようにする。
- 既存ログ方針: 既存 capture に alignment がない場合は、外部 tracker Field source の正確な時刻対応を保証しない。UI / CLI は `unsupported-alignment-missing` または明示的な `legacy-nearest-timestamp` best-effort として扱い、既存 diagnostics log / render snapshot 表示は壊さない。
- TDD対象: 実装 sub-agent は Red test を先に追加する。主対象は `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs` で、保存済み alignment を持つ fixture から `LoadFieldSourceFrame` 相当の Field source frame が selected diagnostics entry / render frame に対応する ER-FORCE snapshot を返すことを固定する。
- TDD expected assertion: fixture では own `TrackedFrame.timestamp` range と external `TrackedFrame.timestamp` range を非重複にし、nearest data timestamp だけでは誤る状態にする。その上で、external source label `ER-FORCE` または `External` を選んだ Field source frame が alignment record の tracker snapshot record index / frame number / semantic summary を返すこと、matching rule が `saved-session-alignment` であること、status が ready であることを assert する。
- TDD expected assertion: 時間軸検査として、selected diagnostics entry の session-relative time または `receivedAt` と chosen external snapshot の capture-time `receivedAt` の差分が許容範囲内であることを assert する。許容範囲は fixture の packet 間隔より十分小さい値に固定し、nearest data timestamp へ戻る regression を検出できるようにする。
- 追加 test 候補: `Tracker/Tracker.Tests/TrackerCaptureOnSessionSnapshotContractTests.cs` で metadata が `TrackerSnapshotAlignmentPath` と alignment log metadata を持つこと、`Tracker/Tracker.Tests/TrackerComparisonSourceTddTests.cs` または新規 writer-focused test で writer が alignment sidecar を session folder 配下へ出すこと、`Tracker/Tracker.Tests/TrackerReplayIntegrationTddTests.cs` / `CaptureReplayTests.cs` で CLI 表示が `saved-session-alignment` を出すことを固定する。
- 実装候補ファイル: `Tracker/Tracker.Server/Vision/VisionPacketCaptureFile.cs`、`Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs`、`Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`、新規 `TrackerSnapshotAlignment*` writer/reader/model、`Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`、`Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`、必要なら `Tracker.CaptureReplay/CaptureReplayRunner.cs`、`Diagnostics.razor.cs` / `DiagnosticsFieldViewFactory.cs`。
- 設計 doc 更新: `tracker-server-cli-ui-detail-design.md` に保存形式、source key / aggregate、timestamp 比較、diagnostics replay / Field source、focused tests を追記した。`tracker-architecture-plan.md` に architecture 上の alignment sidecar と timestamp 非一致前提を追記した。`Tracker.Server/README.md` に operational path、metadata、manual evidence、CLI evidence を追記した。`tasks-status.md` に Red test の前提と time-axis assertion を反映した。

## リスク

- 未解決のリスクまたは後続対応:
- alignment writer が diagnostics entry、render snapshot、tracker snapshot のどの event stream を authoritative にするかは実装時に固定が必要。設計上は diagnostics entry stable key と render snapshot frame number を保存し、Field source は alignment を優先する。
- 長時間 capture では alignment sidecar 自体も大きくなるため、全 source x 全 diagnostics entry を無制限に増やすと index memory が増える。実装では source key / entry key の最小 record と semantic summary 参照に留め、snapshot payload は既存 snapshot index から参照する必要がある。
- ER-FORCE の複数 endpoint aggregate は代表 snapshot を選ぶため、endpoint ごとの差を UI で完全に比較したい場合は後続で endpoint 別 source option が必要になる可能性がある。ただし今回の正常系は source label aggregate で Field に表示することを優先する。
- 既存 capture は alignment を持たないため、今回の主設計では救済対象外。UI / CLI の unsupported / best-effort 表示が曖昧だと、既存ログで修正が効かないことを不具合と誤認するリスクがある。
- `Tracker/Tracker.Server/appsettings.json` の `Tracker:Receive:Enabled=true` は既存 dirty diff であり、ユーザー実行用ローカル設定の可能性があるため変更していない。
