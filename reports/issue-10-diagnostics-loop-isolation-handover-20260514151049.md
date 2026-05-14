# Issue #10 / RAW-VISION diagnostics loop isolation 引き継ぎ

## 目的

次のチャットで、`/diagnostics` overlay 表示において `トラッカーなし` が `ER-FORCE` より遅れて見える問題を、単なる表示補正ではなく `tracker 処理ループ`、`server live 表示ループ`、`diagnostics logging/replay ループ` の分離として設計・実装できる状態へ引き継ぐ。

## 対象リポジトリとブランチ

- repository: `/home/ibis/ssl/IbisDuck`
- active branch: `feat/raw-vision-diagnostics-loop-isolation`
- branch base: `785827c Issue #10 Vision画面に分割表示とオーバーレイを追加する (#15)`
- current worktree:
  - 未追跡: `reports/issue-10-diagnostics-loop-isolation-investigation-20260514150641.md`
  - この handover report 以外の tracking / design / code 変更はまだ入れていない。

## 重要な workflow 前提

- `AGENTS.md` に従い、開発再開時は `development-orchestrator` を最初に実行し、作業内容をユーザーに確認する。
- IbisDuck の開発では既存 Skill を優先する。関連 Skill がありそうなら即興で進めず、Skill を読む。
- tracking は作業前に同期する。`Tracker/Tracker.Server/Design/tasks-status.md` と `Tracker/Tracker.Server/Design/phases-status.md` をまとめて後追い更新しない。
- 実装前に `task-consistency-manager` と `design-doc-maintainer` を通す。
- 変更は TDD で進める。先に regression test を固定してから実装する。
- 調査・実装・レビューは原則 sub-agent / worker へ委譲し、親は manager として tracking、設計判断、report 確認、git 操作を担う。
- review は task ごとに dedicated review を行い、report を `reports/` に残す。
- レポートは日本語。review / 実装 report は鵜呑みにせず、親が visible evidence に照らして裁定する。

## 現在の tracking 状態

`Tracker/Tracker.Server/Design/tasks-status.md` はまだ `RAW-VISION-016` が current task のまま。

- `RAW-VISION-013`: Issue #10 Vision split / overlay の設計完了。
- `RAW-VISION-014`: Vision split / overlay と diagnostics time sync の TDD contract 完了。
- `RAW-VISION-015`: Vision split / overlay UI と live source snapshot 接続完了。
- `RAW-VISION-016`: final validation / docs / review / PR ready が `in-progress` 扱い。

ただし、現在の git log では `origin/main` / `main` が `785827c ... (#15)` を指しており、PR #15 相当の内容は main に入っているように見える。tracking には「PR #15 draft 維持」「ユーザー UI 確認後に解除判断」という古い記述が残っているため、次回は `RAW-VISION-017` を追加する前に tracking の現在性を確認し、必要なら `RAW-VISION-016` / phase 状態の resync を行う。

## このチャットで起きたこと

1. ユーザーが作業中内容を保留し、`main` へ切り替えたいと依頼。
2. 当時の branch は `feat/tracker-simulator-docker`、未コミット差分は `Tracker/Tracker.Server/appsettings.json` のみだった。
3. `git stash push -m "codex-hold-before-main-switch-2026-05-14" -- Tracker/Tracker.Server/appsettings.json` で差分を退避。
4. `git switch main` で `main` へ切り替えた。stash は `stash@{0}: On master: codex-hold-before-main-switch-2026-05-14` として残った。
5. ユーザーが `/diagnostics` overlay で `トラッカーなし` が遅れて見えると報告。
6. ユーザー仮説: logging 処理の周期が ibis-tracker の制御周期に引っ張られている。
7. ユーザーが遅れて見える例として capture folder を提示。
8. capture を使って周期統計を確認し、仮説を支持する結果を得た。
9. `feat/raw-vision-diagnostics-loop-isolation` branch を作成。
10. 調査 report `reports/issue-10-diagnostics-loop-isolation-investigation-20260514150641.md` を作成。
11. ユーザーが「一旦停止」と指示したため、tracking / design / 実装には進んでいない。

## 確認済みの事実

対象 capture:

`Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260514T055027883Z-465bba2cd89345bfb13014e2390c4dd5`

確認した file:

- `ssl-vision-packets-20260514T055027883Z-465bba2cd89345bfb13014e2390c4dd5.jsonl.gz`
- `ssl-vision-packets-20260514T055027883Z-465bba2cd89345bfb13014e2390c4dd5.render-snapshots.jsonl.gz`
- `ssl-vision-packets-20260514T055027883Z-465bba2cd89345bfb13014e2390c4dd5.tracker-diagnostics.log`
- `tracker-packet-snapshots.jsonl`
- `tracker-snapshot-alignment.jsonl`
- metadata json

周期統計:

- raw SSL-Vision packet: 9,810 packets / 79.284s、平均 interval 8.083ms。
- ER-FORCE unique tracked frame: 7,927 frames / 79.300s、平均 interval 10.005ms。
- render snapshot: 2,469 frames / 79.223s、平均 interval 32.100ms。
- ibis own tracker unique frame: 2,469 frames / 79.223s、平均 interval 32.100ms。
- tracker timeline tick 上の render snapshot hold: 52,491 ticks、平均 17.872ms、最大 104ms。

判断:

- Diagnostics の `Vision Input` は raw packet capture の 8ms cadence ではなく、`TrackerCoordinator.DispatchResult` の `WorldFrameCommitted` で保存される render snapshot を読んでいる。
- render snapshot は ibis tracker の committed frame cadence と同じ 32.1ms で更新される。
- そのため、ER-FORCE の 10ms 前後 cadence と overlay すると raw/no-tracker 側だけが stale に見える。
- live overlay は `VisionPacketStore` / `TrackedSnapshotStore` / external tracker latest snapshot を UI render tick で固定するため、保存 replay の render snapshot cadence に縛られない。

## 関連コードの読み取りポイント

- `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs`
  - `WorldFrameCommitted` で `renderSnapshotCaptureWriter?.CaptureFrame(committedFrame, receivedAt)` と `trackerSnapshotAlignmentLogWriter?.CaptureRenderSnapshot(committedFrame, receivedAt)` を呼んでいる。
  - ここが ibis tracker committed frame cadence に Diagnostics の render snapshot 保存を縛っている主因。
- `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs`
  - `SnapshotAppended` で tracker snapshot tick を alignment timeline に入れる。
  - `latestRenderSnapshot` を hold して `RenderFrameNumber` / `RenderReceivedAt` / `RenderMatchRule` を alignment record に載せる。
- `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsReplayTimelineIndex.cs`
  - alignment v2 から replay timeline tick を構築し、render snapshot は latest-before fallback で保持される。
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - `selectedReplayTimelineTick.RenderFrameNumber` から render snapshot を読み、`Vision Input` を作る。
- `Tracker/Tracker.Server/Vision/VisionPacketStore.cs`
  - raw Vision の latest packet / camera / aggregate snapshot は tracker loop とは別に store されている。
- `Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs`
  - live overlay は `VisionPacketStore`、`TrackedSnapshotStore`、external tracker manager から UI render tick snapshot を合成している。

## 決定済みの方針

ユーザー発言:

- 「ロギングは別スレッドで回したほうが良いね。」
- 「別スレッド側でトラッカー処理へ最新を取得するような処理の書き方にしないと影響が乗る」
- 「トラッカーの処理ループと、サーバーとしての表示処理ループ Diagnosticの処理ループすべて隔離させてほしいところ・・・」

採用方針:

- marker の位置補正や UI 側だけの fallback ではなく、loop isolation として扱う。
- tracker 処理ループは tracker state 更新と publish を担当する。
- server live 表示ループは latest immutable snapshot を UI render tick で固定する。
- diagnostics logging/replay ループは tracker 処理ループから直接書き込まず、別 loop で latest raw / latest tracker snapshot を読んで保存する。
- Diagnostics replay の `Vision Input` は tracker committed frame cadence ではなく、保存された raw/latest snapshot cadence に基づく。

## 未解決事項

- `RAW-VISION-017` 以降の task breakdown はまだ作っていない。
- `Tracker.Server/Design/raw-vision-viewer-plan.md` への loop isolation 設計追記はまだ行っていない。
- `Tracker.Server/Design/tasks-status.md` / `phases-status.md` への反映はまだ行っていない。
- implementation / tests はまだ未着手。
- `reports/issue-10-diagnostics-loop-isolation-investigation-20260514150641.md` は未追跡のまま。
- PR #15 / `RAW-VISION-016` の tracking 記述が git 現在状態とずれている可能性があるため、再開時に resync が必要。

## 推奨タスク分割案

tracking へ入れる前に user confirmation を取ること。

- `RAW-VISION-017`: diagnostics overlay 遅延の原因調査と loop isolation 設計追補。
  - exit: 調査 report、設計更新、review。
- `RAW-VISION-018`: diagnostics sampling loop / latest snapshot boundary の TDD contract。
  - exit: tracker committed frame cadence に依存せず、raw/latest snapshot cadence で Diagnostics Vision Input を保存・replay できる failing tests。
- `RAW-VISION-019`: diagnostics logging loop isolation 実装。
  - exit: background sampling loop、latest raw / latest tracker snapshot 読み取り、alignment/replay 接続、focused tests pass。
- `RAW-VISION-020`: validation / review / progress sync / PR。
  - exit: provided capture 相当の周期差を説明できる evidence、build/test、gpt-5.5 high review、tracking sync、commit/PR。

この分割は案であり、次回 `task-breakdown-planner` / `task-consistency-manager` で確定する。

## 次チャットへの依頼文

```text
/home/ibis/ssl/IbisDuck で作業を再開してください。まず AGENTS.md に従って development-orchestrator を実行し、関連 Skill を確認してください。

現在の branch は feat/raw-vision-diagnostics-loop-isolation です。未追跡 report として reports/issue-10-diagnostics-loop-isolation-investigation-20260514150641.md と reports/issue-10-diagnostics-loop-isolation-handover-20260514151049.md があります。

目的は、/diagnostics overlay で「トラッカーなし」が ER-FORCE より遅れて見える問題を、tracker 処理ループ / server live 表示ループ / diagnostics logging/replay ループの分離として設計・実装することです。表示補正だけで済ませないでください。

最初に Tracker/Tracker.Server/Design/tasks-status.md と phases-status.md を確認し、PR #15 / RAW-VISION-016 の tracking が current git state とずれていないか resync してください。その後、RAW-VISION-017 以降の固定タスクを task-breakdown-planner / task-consistency-manager で作り、raw-vision-viewer-plan.md に loop isolation 方針を追記してください。

調査済み evidence:
- raw SSL-Vision packet は平均 8.083ms
- ER-FORCE unique frame は平均 10.005ms
- render snapshot と ibis own tracker は平均 32.100ms
- tracker timeline 上の render snapshot hold は平均 17.872ms、最大 104ms

対象 capture:
Tracker/Tracker.Server/bin/Debug/net10.0/packet-captures/ssl-vision-packets-20260514T055027883Z-465bba2cd89345bfb13014e2390c4dd5

関連コード:
- Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs
- Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs
- Tracker/Tracker.Server/Tracking/TrackerDiagnosticsReplayTimelineIndex.cs
- Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs
- Tracker/Tracker.Server/Vision/VisionPacketStore.cs
- Tracker/Tracker.Server/Vision/VisionLiveComparisonViewState.cs

実装前に TDD contract を追加し、調査・実装・レビューは原則 sub-agent に委譲してください。review report は日本語で reports/ に残してください。
```
