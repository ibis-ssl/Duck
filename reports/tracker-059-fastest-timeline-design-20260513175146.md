# Sub-agent実行レポート

## タスク

- 目的: TRACKER-059 の fastest cadence replay / 保存時 alignment 生成 cadence を設計し、必要な設計ドキュメントを更新する。
- タスク種別: design

## sub-agentを使う理由

- 理由: ユーザー指示により設計は gpt-5.5 high sub-agent に任せる。今回の変更は user-visible playback contract と alignment sidecar 保存形式に影響するため、実装より先に設計を固定する。

## 対象範囲

- 対象: `/diagnostics` Play / Fast Forward / scrub の unified replay timeline、保存時 alignment sidecar の fastest source cadence 対応、Vision/render frame hold、TDD acceptance。

## 対象外

- 対象外: 実装コード変更、テストコード変更、外部 ER-Force プロセス操作、既存ローカル差分 `Tracker/Tracker.Server/appsettings.json` の変更。

## 実行コマンド

- 実行コマンド:
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,260p' reports/tracker-059-fastest-timeline-investigation-20260513173834.md`
- `sed -n '1,260p' reports/tracker-059-fastest-timeline-design-20260513175146.md`
- `rg -n 'diagnostics|replay|Play|Fast Forward|scrub|alignment|snapshot|tracker-snapshot-alignment|ReceivedAt|TrackedFrame.timestamp|TRACKER-059' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md Tracker/Tracker.Core/Design/tracker-architecture-plan.md Tracker/Tracker.Server/README.md Tracker/Tracker.Core/Design/tasks-status.md Tracker/Tracker.Core/Design/phases-status.md`
- `git status --short`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '120,275p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/README.md | sed -n '75,145p'`
- `nl -ba Tracker/Tracker.Server/README.md | sed -n '220,235p'`
- `nl -ba Tracker/Tracker.Core/Design/tasks-status.md | sed -n '1,230p'`
- `nl -ba Tracker/Tracker.Core/Design/phases-status.md | sed -n '1,45p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs | sed -n '1,260p'`
- `rg -n 'TrackerSnapshotAlignmentRecord|DiagnosticsLineNumber|TrackerSnapshotAlignment' Tracker/Tracker.Server/Tracking Tracker/Tracker.Tests -g '*.cs'`
- `rg -n 'TRACKER-059|schema version 2|optional|v1|互換 fallback|性能第一|ボトルネック|unified replay timeline|fastest' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md Tracker/Tracker.Core/Design/tracker-architecture-plan.md Tracker/Tracker.Server/README.md Tracker/Tracker.Core/Design/tasks-status.md Tracker/Tracker.Core/Design/phases-status.md`
- `git diff --check`
- `git diff --stat`

## 対象ファイル

- 変更または確認したファイル:
- 変更: `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- 変更: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- 変更: `Tracker/Tracker.Server/README.md`
- 変更: `Tracker/Tracker.Core/Design/tasks-status.md`
- 変更: `Tracker/Tracker.Core/Design/phases-status.md`
- 変更: `reports/tracker-059-fastest-timeline-design-20260513175146.md`
- 確認: `reports/tracker-059-fastest-timeline-investigation-20260513173834.md`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs`
- 確認: `Tracker/Tracker.Server/appsettings.json` は既存 dirty diff があることだけ確認し、変更していない。

## 指摘事項

- 指摘要約または「指摘なし」:
- 調査 report の結論どおり、現行 `/diagnostics` Play / Fast Forward / scrub は diagnostics entry / selected entry を主 timeline としており、ER-FORCE の fast tracker samples は replay tick にならない。
- `TrackedFrame.timestamp` は source 間で時刻系が違う場合があるため、unified replay timeline の時刻軸として使わない。timeline ordering は capture-time `ReceivedAt` / session-relative received offset に固定する。
- 保存時 alignment が diagnostics line 単位だけだと、Vision / render 0ms / 100ms に対して ER-FORCE 20 / 40 / 60 / 80ms の比較点が保存されず、正確な replay comparison が後から作れない。
- ユーザー指示により互換性より性能を優先する。既存 schema / reader / selected diagnostics entry 前提の実装がボトルネックになる場合は温存せず、unified replay timeline 用の v2 index を主経路にする。

## 結果

- 結果:
- 設計方針: `/diagnostics` Play / Fast Forward / scrub は diagnostics entry count ではなく unified replay timeline を使う。unified replay timeline は capture-time `ReceivedAt` を軸に、diagnostics entry / render snapshot / tracker packet snapshot の union、または同等に fastest available source cadence を含む index とする。
- Vision / render 解決方針: fast tracker tick では tick timestamp に対する latest-before render snapshot を保持する。先頭だけ prior render snapshot がない場合は nearest-after fallback を許容する。Vision / render 0ms / 100ms、ER-FORCE 0 / 20 / 40 / 60 / 80 / 100ms の場合、20 / 40 / 60 / 80ms は同じ Vision / render 0ms frame、100ms は Vision / render 100ms frame を参照する。
- schema 方針: 別 sidecar は作らず、既存 file 名 `tracker-snapshot-alignment.jsonl` を schema version 2 の clean record へ置き換える。互換 fallback、v1 reader fallback、optional-field fallback は非要件。v2 record は `replayTimelineIndex`、`replayTimelineReceivedAt`、`replayTimelineKind`、`diagnosticsLineNumber?`、`renderFrameNumber?`、`renderReceivedAt?`、`renderMatchRule`、`sourceKey`、`sourceRole`、`sourceLabel`、`remoteEndpoint`、`trackerSnapshotRecordIndex?`、`trackerSnapshotReceivedAt?`、`receivedAtDeltaTicks`、`status` を明示 field として持つ。
- 性能方針: log open 時に v2 alignment JSONL、tracker packet snapshot sidecar、render snapshot index から replay timeline tick array、source-key index、render latest-before index、tracker source index を構築する。Play / Fast Forward / scrub / Field source selector 変更時は sidecar 全再読込をしない。既存 reader がこの条件を満たせない場合は新規 `TrackerDiagnosticsReplayTimelineIndex` 相当へ置き換える。
- 保存時 alignment 方針: fast tracker sample ごとに alignment record を保存し、同じ Vision / render frame を複数 fast tracker records から参照できるようにする。低速 Vision / render tick でも、その時点の latest/current tracker snapshot と対応する alignment record を残す。
- TDD acceptance:
- Vision / render snapshots は 0ms / 100ms、ER-FORCE snapshots は 0 / 20 / 40 / 60 / 80 / 100ms の fixture を使う。
- alignment sidecar record count が diagnostics line 2 件に退化しないことを固定する。
- 20 / 40 / 60 / 80ms record が同じ Vision / render 0ms frame を参照し、100ms record が Vision / render 100ms frame を参照することを固定する。
- ER-FORCE の `TrackedFrame.timestamp` を ibis own と非重複にしても、timeline ordering と render hold は `ReceivedAt` で決まることを固定する。
- UI replay timeline が fast ticks を含み、Vision Field が held render snapshot を表示することを固定する。
- tick / scrub / source selector 変更で alignment sidecar や tracker packet snapshot sidecar を再読込しないことを固定する。
- 実装候補ファイル:
- `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs`
- `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs`
- `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs`
- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsReplayTimelineIndex.cs` または同等の新規 pure index
- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs` または置換 adapter
- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
- `Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
- `Tracker/Tracker.Tests/TrackerCaptureOnSessionSnapshotContractTests.cs`
- `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
- `Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
- 必要なら新規 `Tracker/Tracker.Tests/TrackerDiagnosticsReplayTimelineIndexTests.cs`
- 実装順序:
- 1. 保存時 alignment v2 schema と fastest cadence record 生成を TDD で固定する。
- 2. v2 alignment / render / tracker snapshots から log open 時に unified replay timeline index を作る pure index を TDD で固定する。
- 3. `/diagnostics` Play / Fast Forward / scrub を unified replay timeline index へ接続する。
- 4. Field source / overlay / comparison が selected diagnostics entry ではなく selected replay timeline tick から解決されることを接続する。
- 5. 既存 reader が性能上のボトルネックになる場合は adapter 化または削除し、新 index を主経路にする。

## リスク

- 未解決のリスクまたは後続対応:
- schema version 2 は互換 fallback を持たない方針のため、旧 alignment sidecar を今回実装後の `/diagnostics` で読むことは TRACKER-059 の非ゴールになる。必要なら UI status として schema mismatch を表示する。
- Fast Forward の体感速度は timeline tick 数増加で変わる可能性がある。設計では tick skip ではなく capture-time delta / multiplier を優先する。
- log open 時の index 構築コストは sidecar サイズに比例するため、長時間 capture では初回選択時の待ち時間が出る。tick / scrub 時の性能を最優先し、必要なら index build の進捗表示やキャンセルは後続で扱う。
- `Tracker/Tracker.Server/appsettings.json` の既存 dirty diff (`Tracker:Receive:Enabled=true`) はユーザー実行用ローカル設定の可能性があるため変更していない。
