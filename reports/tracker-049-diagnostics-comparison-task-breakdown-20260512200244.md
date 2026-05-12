# Sub-agent実行レポート

## タスク

3rd party tracker と ibis tracker の差分を `/diagnostics` 画面でも比較できるようにするため、終了までの固定タスク一覧と設計変更範囲を洗い出す。

## sub-agentを使う理由

設計・調査・タスク分解は sub-agent に委譲し、親エージェントは report を確認して固定一覧と設計へ反映するため。

## 対象範囲

- 既存 `Tracker.CaptureReplay` の CLI 比較実装を残す前提
- `/diagnostics` 画面で tracker snapshot comparison をユーザーが確認するための UI / state / reader / test / evidence
- `TrackerSnapshotReplayReader` / snapshot sidecar / metadata / diagnostics log / render snapshot の既存契約
- `tasks-status.md` / `phases-status.md` の固定一覧見直し
- `tracker-server-cli-ui-detail-design.md` / `tracker-architecture-plan.md` の設計更新候補
- `TRACKER-049` README / review / design audit の既存差分

## 対象外

- コード実装
- design / tracking の直接編集
- commit / push / PR 操作
- CLI 比較実装の削除や置き換え

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/task-breakdown-planner/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/task-consistency-manager/SKILL.md`
- `nl -ba reports/tracker-049-diagnostics-comparison-task-breakdown-20260512200244.md | sed -n '1,240p'`
- `rg -n "diagnostics|CaptureReplay|TRACKER-049|TRACKER-050|comparison|raw-vision" /home/ibis/.codex/memories/MEMORY.md`
- `git status --short`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tasks-status.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/phases-status.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `sed -n '1,260p' Tracker/Tracker.Server/README.md`
- `sed -n '1,240p' reports/tracker-049-operation-docs-implementation-20260512164929.md`
- `sed -n '1,220p' reports/tracker-049-review-20260512165902.md`
- `sed -n '1,240p' reports/tracker-049-diagnostics-playback-design-audit-20260512170903.md`
- `sed -n '1,240p' reports/tracker-048-capture-replay-comparison-implementation-20260512155553.md`
- `rg --files Tracker/Tracker.CaptureReplay Tracker/Tracker.Server/Components/Pages Tracker/Tracker.Server/Tracking Tracker/Tracker.Tests | rg '(Diagnostics|Replay|CaptureReplay|Tracker\\.CaptureReplay|TrackerSnapshot)'`
- `rg -n "TrackerSnapshotReplayReader|trackerSnapshot|trackerComparison|TrackerSnapshotSidecarPath|RenderSnapshot|Diagnostics|Comparison|nearest|source|sidecar|Settings" Tracker/Tracker.CaptureReplay Tracker/Tracker.Server/Components/Pages Tracker/Tracker.Server/Tracking Tracker/Tracker.Tests`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '220,520p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor | sed -n '1,340p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs | sed -n '1,360p'`
- `nl -ba Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs | sed -n '1,230p'`
- `nl -ba Tracker/Tracker.CaptureReplay/Program.cs | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Tests/CaptureReplayTests.cs | sed -n '1,520p'`
- `nl -ba Tracker/Tracker.Tests/TrackerReplayIntegrationTddTests.cs | sed -n '1,320p'`
- `nl -ba Tracker/Tracker.Tests/TrackerDiagnosticsLogReaderTests.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs | sed -n '1,300p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs | sed -n '1,280p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsProfileMetadataLoader.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css | sed -n '1,340p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogReader.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '1,150p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '90,180p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '520,570p'`
- `nl -ba Tracker/Tracker.Core/Design/tasks-status.md | sed -n '1,80p' && nl -ba Tracker/Tracker.Core/Design/phases-status.md | sed -n '1,50p'`
- dotnet build / test は未実行。今回は調査・設計案・タスク分解のみで、コード修正を行っていないため。

## 対象ファイル

- 変更: `reports/tracker-049-diagnostics-comparison-task-breakdown-20260512200244.md`
- 確認: `Tracker/Tracker.Core/Design/tasks-status.md`
- 確認: `Tracker/Tracker.Core/Design/phases-status.md`
- 確認: `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- 確認: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- 確認: `Tracker/Tracker.Server/README.md`
- 確認: `reports/tracker-049-operation-docs-implementation-20260512164929.md`
- 確認: `reports/tracker-049-review-20260512165902.md`
- 確認: `reports/tracker-049-diagnostics-playback-design-audit-20260512170903.md`
- 確認: `reports/tracker-048-capture-replay-comparison-implementation-20260512155553.md`
- 確認: `Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs`
- 確認: `Tracker/Tracker.CaptureReplay/Program.cs`
- 確認: `Tracker/Tracker.CaptureReplay/ReplaySummary.cs`
- 確認: `Tracker/Tracker.CaptureReplay/ReplayOptions.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.css`
- 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsProfileMetadataLoader.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsProfileMetadataView.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogReader.cs`
- 確認: `Tracker/Tracker.Tests/CaptureReplayTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerReplayIntegrationTddTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerDiagnosticsLogReaderTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerRenderSnapshotLogReaderTests.cs`
- 確認: `Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`

## 指摘事項

- Medium: 現 design は `/diagnostics` playback が tracker packet snapshot timeline を読み、3rd party snapshot と ibis committed frame を並べて比較表示できることまで要求している。`tracker-server-cli-ui-detail-design.md` は `Tracker.CaptureReplay` と `/diagnostics` playback の両方を完了条件にしており、`tracker-architecture-plan.md` も diagnostics viewer / playback が同じ snapshot log から source identity / role ごとの timeline、frame number / timestamp、ball / robot count、raw payload 復元状態を表示する入力契約として読める。
- Medium: 現実装で比較表示が接続されているのは `Tracker.CaptureReplay` 側だけである。`CaptureReplayRunner` は `TrackerSnapshotReplayReader` を使って `trackerSnapshot` / `trackerComparison` 行を出すが、`Diagnostics.razor` / `Diagnostics.razor.cs` は diagnostics log、render snapshot、profile metadata、playback 選択を同期するだけで、`TrackerSnapshotReplayReader` を注入・利用していない。
- Medium: `TRACKER-049` README docs 差分は現実装に忠実な manual correlation 手順としては妥当だが、UI 内比較を実装する方針では完了形の説明として不足する。docs-only のまま進めると、design literal と tracking / README の差が `TRACKER-050` ready review で再発する可能性が高い。
- Medium: `TRACKER-049` は実装 report / review report が存在するにもかかわらず、tracking 上は `TDD未着手`、Implementation / Review `未着手`、一覧 `planned` のまま残っている。これは UI 比較の設計ギャップとは別に、どの方針でも progress sync が必要である。
- Design finding: UI 比較には既存 `TrackerSnapshotReplayReader` の reader / record contract を再利用できる。ただし現在の `TrackerSnapshotComparisonSummary` は全 source から「own 以外の最寄り 1 件」を返す形で、source filtering、source ごとの comparison、delta 表示、sidecar missing / empty 表示、metadata の skipped/error count 表示には足りない。UI 用 view-state か reader 拡張で、`SnapshotInputs` から source 一覧と selected source filter に応じた nearest summary を作る必要がある。
- Design finding: diagnostics render snapshot と同様に、選択中 `TrackerDiagnosticsLogEntry` への snapshot 対応付けが必要である。ただし比較の基準は render snapshot ではなく、既存 reader と同じく selected entry の tracked frame number から own snapshot の `TrackedFrame.timestamp` を引き、source filter 後の tracker snapshot へ nearest timestamp で対応付けるのが妥当である。own snapshot がない場合は UI で「ibis timestamp unavailable」として表示し、render snapshot 欠落とは別状態にする。
- Design finding: sidecar missing は正常系として扱う必要がある。metadata がない、metadata に sidecar path がない、sidecar file がない、metadata `TrackerSnapshotLog.IsCreated=false`、record count 0、読み取り error は別 status に分け、既存 diagnostics / render snapshot 表示を壊さない。

## 結果

- 推奨する固定タスク一覧:
- `TRACKER-049`: diagnostics comparison の design / tracking 再同期タスクへ変更する。既存 README docs 差分は保留し、削除せず、UI 比較方針に合わせて後続 docs task で再利用・修正する。依存: `TRACKER-048`。完了条件: `tracker-server-cli-ui-detail-design.md` と `tracker-architecture-plan.md` が CLI 比較と `/diagnostics` UI 比較の責務を明確に分け、`tasks-status.md` / `phases-status.md` に `TRACKER-050` 以降の固定一覧、dependencies、exit criteria、review / commit gate が記録され、design review blocking findings がない。
- `TRACKER-050`: diagnostics comparison reader / view-state contract を追加する。依存: `TRACKER-049`。完了条件: 既存 `TrackerSnapshotReplayReader` / `TrackerPacketSnapshotLogReader` を再利用し、diagnostics log path から metadata / sidecar を解決する UI 用 index、source list、selected source filter、selected entry comparison、sidecar status、skipped/error count を pure model として固定する。source filtering は all / external / own / unknown / source label 単位を扱い、missing / empty / corrupt sidecar を既存 diagnostics 表示の blocker にしない。focused tests は新規 `DiagnosticsTrackerComparison*Tests` と既存 `TrackerReplayIntegrationTddTests` / `TrackerDiagnosticsLogReaderTests` / `TrackerRenderSnapshotLogReaderTests` を対象にする。
- `TRACKER-051`: `/diagnostics` UI へ comparison 表示と source filtering を接続する。依存: `TRACKER-050`。完了条件: `Diagnostics.razor` / `Diagnostics.razor.cs` が selected log / selected entry / playback tick と comparison view-state を同期し、source role / label、tracked frame / timestamp、timestamp delta、matching rule、ball / robot count、raw payload restored、sidecar missing / empty / error を表示できる。既存 raw / tracked render snapshot、profile settings modal、timeline scrubber、Play / Fast Forward / Stop、4K 向け resize layout を壊さない。focused tests は view-state helper と existing playback / render layout tests を中心にし、必要なら Blazor 表示は manual evidence に回す。
- `TRACKER-052`: README / manual evidence / review を UI 比較完了後の運用手順へ更新する。依存: `TRACKER-051`。完了条件: `Tracker.Server/README.md` が CaptureReplay CLI を agent / 検証用として残すこと、通常確認では `/diagnostics` の source filter と comparison panel で差を見ること、sidecar missing / record 0 の読み方、manual evidence に残す frame / source / timestamp / delta / rawPayloadRestored を説明する。既存 `reports/tracker-049-operation-docs-implementation-20260512164929.md` の内容は再利用してよいが、現 UI 実装能力に合わせて読み替える。gpt-5.5 high review blocking findings がない。
- `TRACKER-053`: PR #9 ready 化タスクにする。依存: `TRACKER-052`。完了条件: final focused / related / 必要な full test、manual evidence、全 task review reports、tracking sync、PR body 更新、risk 整理、draft 解除判断材料が揃っている。
- 既存 `TRACKER-050` の扱い: 現在の `TRACKER-050` は PR ready 化として定義済みだが、UI 比較を PR ready 前に入れるなら補助番号なしで中間タスクを追加するため、PR ready 化は `TRACKER-053` へ後ろ倒しするのが一貫する。`TRACKER-050` を PR ready のまま固定したい場合、UI 比較実装を canonical tracking に入れられず、ユーザー指示の「ちょい出し追加を避ける」と衝突する。
- design doc 更新方針:
- `tracker-server-cli-ui-detail-design.md` は `diagnostics / replay / playback 互換追加` と `後続タスクへの固定事項` を更新し、CLI は `Tracker.CaptureReplay` の検証用比較出力、UI は `/diagnostics` の source-filtered comparison panel と明記する。`Tracker.CaptureReplay` と `/diagnostics` playback の両方が session folder 内 sidecar を読む、source filter は保存除外ではなく表示選択、sidecar missing / empty は正常系 status、selected entry 対応付けは own snapshot timestamp から nearest timestamp で行う、という契約にする。
- `tracker-architecture-plan.md` は `CaptureOn 比較ログがある場合...` の段落を更新し、diagnostics viewer が CLI と同じ reader contract を再利用すること、Core には比較処理を入れないこと、UI では source identity / role / label、frame number / timestamp、ball / robot count、raw payload restored、matching rule / delta を表示することを追記する。
- `tasks-status.md` / `phases-status.md` は固定残タスクを `TRACKER-049` から `TRACKER-053` に再定義し、`TRACKER-049` を design / tracking 再同期として active にする。既存 docs-only review findings と design audit report を参照し、README 差分は完了済み扱いではなく後続 `TRACKER-052` の入力として扱う。
- 親が次に編集すべきファイル:
- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
- `Tracker/Tracker.Core/Design/tasks-status.md`
- `Tracker/Tracker.Core/Design/phases-status.md`
- 必要なら `Tracker/Tracker.Server/README.md` は後続 docs task まで保留し、今すぐ編集する場合も「manual correlation」から「UI comparison 実装予定」に先走らない文言へ限定する。
- 親が今は編集しないべきファイル:
- `Tracker/Tracker.CaptureReplay/*`: 既存 CLI 比較実装は agent / 検証用に残す。削除・置換しない。
- `Tracker/Tracker.Core/*`: official packet 傍受、snapshot 保存、比較表示は Core に入れない。
- `Tracker/Tracker.Server/Components/Pages/Diagnostics*`、`Tracker/Tracker.Server/Tracking/*`、`Tracker/Tracker.Tests/*`: design / tracking が固定されるまで実装へ進めない。
- 既存 reports: 過去 report は監査証跡として保持し、今回 report 以外は rewrite しない。
- 今回の変更有無: 変更したのは `reports/tracker-049-diagnostics-comparison-task-breakdown-20260512200244.md` のみ。コード、design、tracking、README、commit、push、PR 操作は行っていない。

## リスク

- 本調査は repository 読み合わせに基づく。dotnet build / test、実機 multicast、ブラウザ manual evidence は実行していない。
- 追加 UI 実装は docs follow-up ではなく新規 implementation task 相当である。`Diagnostics.razor` は現在 `TrackerSnapshotReplayReader` を利用しておらず、source filtering と sidecar status を UI に出すには view-state / CSS / tests / evidence が必要になる。
- `TrackerSnapshotReplayReader` を UI と CLI で共用する場合、CLI 出力互換を壊さずに source-filtered comparison を増やす設計が必要である。既存 `trackerSnapshot` / `trackerComparison` 行は維持する。
- Existing README diff は現実装能力には合っているが、UI 比較を実装する方針では途中状態の説明になる。早く commit すると後で docs churn が増えるため、design / tracking 裁定まで保留するのが安全である。
- PR ready の既存 ID `TRACKER-050` を後ろへずらす提案は tracking 再定義を伴う。親側でユーザー承認または明示裁定を取り、design / tracking へ反映してから実装に入る必要がある。
