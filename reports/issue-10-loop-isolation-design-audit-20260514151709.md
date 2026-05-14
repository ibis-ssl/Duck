# Sub-agent実行レポート

## タスク

- 目的: diagnostics loop isolation の設計影響、`raw-vision-viewer-plan.md` への追記範囲、固有名詞脚注の必要項目を監査する。
- タスク種別: design impact audit

## sub-agentを使う理由

- 理由: ユーザー指示により設計調査レビューは sub-agent に委譲し、親は manager として設計方針と task tracking の採否判断を行うため。

## 対象範囲

- 対象: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`、`reports/issue-10-diagnostics-loop-isolation-handover-20260514151049.md`、`reports/issue-10-diagnostics-loop-isolation-investigation-20260514150641.md`、diagnostics / live / tracker loop separation に関係する設計記述と脚注候補。

## 対象外

- 対象外: code implementation、test authoring、設計書本文の直接編集、tracking file の直接編集。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,240p' reports/issue-10-loop-isolation-design-audit-20260514151709.md`
  - `sed -n '1,260p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `sed -n '1,260p' reports/issue-10-diagnostics-loop-isolation-handover-20260514151049.md`
  - `sed -n '1,260p' reports/issue-10-diagnostics-loop-isolation-investigation-20260514150641.md`
  - `rg -n "TrackerCoordinator|Dispatch|SnapshotAlignment|ReplayTimeline|Diagnostics|VisionPacketStore|VisionLiveComparison|Footnotes|脚注|\\[\\^" Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker -g '*.cs' -g '*.razor' -g '*.md'`
  - `nl -ba Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md | sed -n '235,360p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs | sed -n '1,260p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs | sed -n '1,320p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsReplayTimelineIndex.cs | sed -n '1,280p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '500,590p'`
  - `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '860,990p'`
  - `nl -ba Tracker/Tracker.Server/Vision/VisionPacketStore.cs | sed -n '1,320p'`
  - `nl -ba Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs | sed -n '1,760p'`
  - `rg -n "CaptureDiagnosticsEntry|CaptureRenderSnapshot|renderSnapshotCaptureWriter|trackerSnapshotAlignmentLogWriter|RenderSnapshot" Tracker/Tracker.Server -g '*.cs'`
  - `rg -n "class TrackerRenderSnapshot|record TrackerRenderSnapshot|render-snapshots|CaptureFrame\\(" Tracker/Tracker.Server -g '*.cs'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs | sed -n '120,180p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs | sed -n '1,170p'`
  - `nl -ba Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs | sed -n '88,215p'`
  - `git status --short`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `reports/issue-10-loop-isolation-design-audit-20260514151709.md`
  - 確認: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - 確認: `reports/issue-10-diagnostics-loop-isolation-handover-20260514151049.md`
  - 確認: `reports/issue-10-diagnostics-loop-isolation-investigation-20260514150641.md`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs`
  - 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsReplayTimelineIndex.cs`
  - 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - 確認: `Tracker/Tracker.Server/Vision/VisionPacketStore.cs`
  - 確認: `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs`

## 指摘事項

- 指摘要約または「指摘なし」:
  - Blocking: `raw-vision-viewer-plan.md` の `Diagnostics time-sync 方針` は、現状「Vision/Input と ibis tracker は selected tick の render frame から得た snapshot を使う」と定義している。今回の意図は `tracker 処理ループ`、`server live 表示ループ`、`diagnostics logging/replay ループ` の分離であり、Diagnostics replay の `Vision Input` を tracker committed frame cadence から外す user-visible / workflow contract 変更に当たるため、実装前に設計追記が必要。
  - Blocking: 現コードでは `TrackerCoordinator.DispatchResult` の `WorldFrameCommitted` で `TrackerRenderSnapshotCaptureWriter.CaptureFrame` と `TrackerSnapshotAlignmentLogWriter.CaptureRenderSnapshot` が呼ばれ、render snapshot 保存が tracker committed frame に従う。`Diagnostics.razor.cs` は `selectedReplayTimelineTick.RenderFrameNumber` から `renderSnapshotsByFrame` を引き、Vision Input を `selectedRenderSnapshot.Frame` で作るため、設計がこの依存解消を明文化しないと修正範囲が曖昧になる。
  - Blocking: `TrackerSnapshotAlignmentLogWriter` と `TrackerDiagnosticsReplayTimelineIndex` は render snapshot の `latest-before` fallback を持つが、これは tracker committed frame から保存された render snapshot の hold であり、raw SSL-Vision cadence の diagnostics sample ではない。新設計では replay timeline の基準、raw/latest tracker snapshot の保存単位、legacy sidecar との互換読み取りを明確にする必要がある。
  - Non-blocking: `VisionPacketStore` と `VisionLiveComparisonSnapshotComposer` は live 表示向けに latest immutable snapshot を固定する境界を既に持つ。設計追記ではこの live loop は維持し、diagnostics logging/replay loop が同じ思想で latest raw / latest tracker snapshot を読むが UI render tick と混同しない、と分けて書くのが妥当。
  - 推奨設計挿入位置: `raw-vision-viewer-plan.md` の `## Diagnostics time-sync 方針` 直後に `## Diagnostics loop isolation 方針` を追加するか、同 section の先頭段落を置換してから loop isolation の段落を続ける。既存 footnote は本文の初出語に `[^term]` を付け、末尾の脚注一覧へ `[^term]: Term: 説明。` 形式で追記する。
  - 推奨テスト方針追記位置: `## テスト方針` の diagnostics time-sync regression 項目の後に、diagnostics sampling loop が tracker committed frame cadence に依存しないこと、replay の Vision Input が sample tick の raw/latest snapshot から復元されること、旧 render snapshot sidecar は legacy path として扱うことを追加する。
  - 脚注候補:
    - `[^loop-isolation]`: loop isolation: tracker 更新、live 表示、diagnostics 保存/replay の周期と責務を分け、片方の cadence が別 loop の表示や保存を支配しないようにする方針。
    - `[^tracker-processing-loop]`: tracker processing loop: raw packet や profile switch request を tracker engine に渡し、tracker state の更新、publish、latest tracker snapshot の公開までを担当する処理 loop。
    - `[^server-live-display-loop]`: server live display loop: 通常 Vision 画面が `UI render tick` ごとに latest immutable snapshot を固定して描画する loop。CaptureOn replay 保存とは別扱いにする。
    - `[^diagnostics-logging-replay-loop]`: diagnostics logging/replay loop: CaptureOn 中に raw/latest tracker snapshot を独立した sample として保存し、Diagnostics 画面でその sample timeline を replay する loop。
    - `[^diagnostics-sample-tick]`: diagnostics sample tick: diagnostics logging loop が latest raw snapshot と latest tracker snapshot を同じ保存単位として固定する tick。tracker committed frame と同義にしない。
    - `[^latest-raw-snapshot]`: latest raw snapshot: `VisionPacketStore` 相当の raw SSL-Vision latest detection / geometry を snapshot 化したもの。Diagnostics の Vision Input はこの snapshot 系から復元する。
    - `[^latest-tracker-snapshot]`: latest tracker snapshot: ibis tracker または 3rd party tracker の最新出力を diagnostics sample に含めるために snapshot 化したもの。
    - `[^tracker-committed-frame-cadence]`: tracker committed frame cadence: ibis tracker が `WorldFrameCommitted` を出し、`TrackerFrame` を publish する周期。raw Vision Input の保存周期として扱わない。
    - `[^raw-snapshot-cadence]`: raw snapshot cadence: SSL-Vision packet / raw latest snapshot が更新される周期。Diagnostics の `Vision Input` 表示はこの cadence を失わない保存経路を持つ。
    - `[^legacy-render-snapshot-sidecar]`: legacy render snapshot sidecar: 既存 `.render-snapshots.jsonl.gz` のように tracker render frame 単位で保存された sidecar。新しい diagnostics sample path と区別して互換読み取り対象にする。
    - `[^diagnostics-sample-sidecar]`: diagnostics sample sidecar: loop isolation 後に diagnostics logging loop が保存する raw/latest tracker snapshot の sidecar。名称や schema は設計で確定する。
  - design task acceptance points:
    - `raw-vision-viewer-plan.md` が 3 loop の責務を明記し、tracker processing loop から diagnostics render/alignment sidecar へ tracker committed frame cadence で直接書き込む方針を不採用または legacy として明記する。
    - Diagnostics replay の `Vision Input` が tracked frame number keyed render snapshot ではなく diagnostics sample tick の raw/latest snapshot から作られることを設計で固定する。
    - replay timeline の基準、source ごとの latest-before / missing / future 不採用ルールが loop isolation 後も維持されることを明記する。
    - live 表示は既存どおり `UI render tick` の composite snapshot とし、diagnostics logging sample tick と混同しないことを明記する。
    - 新旧 capture format の扱いを明記する。既存 render snapshot sidecar は legacy replay path として扱うのか、新 sample sidecar が無い session は degraded 表示にするのかを設計判断として残す。
    - テスト方針に、raw/latest snapshot cadence が tracker committed frame cadence に縛られない regression、Diagnostics Vision Input 復元、legacy sidecar 互換または degraded 表示を追加する。

## 結果

- 結果:
  - 設計更新は必要。今回の変更は Diagnostics の保存/replay contract と user-visible overlay 表示の時刻基準を変えるため、`design-doc-maintainer` の条件に該当する。
  - 推奨対象は新規 design doc ではなく、既存の `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`。既に Issue #10 split / overlay、Diagnostics time-sync、テスト方針、脚注が同一ファイルにまとまっているため、loop isolation も同ファイルへ追記するのが最小で一貫する。
  - `Design/BreakingChanges.md` の更新要否は親判断。監査範囲では public external API 破壊は確認していないが、CaptureOn 既存 session の replay 互換や sidecar schema を変更するなら、互換方針を設計本文に明記する必要がある。

## リスク

- 未解決のリスクまたは後続対応:
  - capture format / sidecar schema 名称が未決定。ここを曖昧にしたまま実装すると、Diagnostics reader と writer が別々の前提で進む可能性がある。
  - 既存 capture session の `.render-snapshots.jsonl.gz` をどう扱うかが未決定。legacy 表示を維持する場合は旧 path と新 path の reader selection が必要。
  - `TrackerSnapshotAlignmentLogWriter` の timeline kind と `ReplayTimelineIndex` を新 sample tick に再利用するか、新 record kind を追加するかが未決定。既存 latest-before / missing contract を壊さない設計が必要。
  - diagnostics logging loop の sampling cadence を raw packet arrival、fixed interval、または event-driven latest snapshot のどれにするかが未決定。ユーザー意図は tracker loop からの隔離だが、保存量と replay 精度の tradeoff は設計で固定する必要がある。
  - 本監査では実装・テスト・tracking 編集は行っていない。
