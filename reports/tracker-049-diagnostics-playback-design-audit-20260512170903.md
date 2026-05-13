# Sub-agent実行レポート

## タスク

`TRACKER-049` review で出た diagnostics playback capability gap について、設計・固定一覧・実装範囲を監査する。

## sub-agentを使う理由

レビュー指摘が追加タスクや設計見直しに関わる可能性があるため、調査は sub-agent に委譲し、親エージェントが report を確認して裁定するため。

## 対象範囲

- `tracker-server-cli-ui-detail-design.md` の diagnostics / replay / playback 要件
- `tasks-status.md` / `phases-status.md` の固定一覧と `TRACKER-048` / `TRACKER-049` / `TRACKER-050`
- 現在実装の `Tracker.CaptureReplay` 比較出力
- 現在実装の `/diagnostics` playback / render snapshot / metadata 表示
- `TRACKER-049` README manual evidence
- `TRACKER-049` review report の capability gap

## 対象外

- コード修正
- design / tracking の直接編集
- 追加 `TRACKER` の即時作成
- commit / push / PR 操作

## 実行コマンド

- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/task-consistency-manager/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/progress-sync-manager/SKILL.md`
- `sed -n '1,260p' reports/tracker-049-diagnostics-playback-design-audit-20260512170903.md`
- `rg -n "TRACKER-049|TRACKER-048|CaptureReplay|diagnostics playback|DiagnosticsPlayback" /home/ibis/.codex/memories/MEMORY.md`
- `rg -n "TRACKER-049|TRACKER-048|CaptureReplay|diagnostics playback|DiagnosticsPlayback" Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md Tracker/Tracker.Core/Design/tracker-architecture-plan.md Tracker/Tracker.Core/Design/tasks-status.md Tracker/Tracker.Core/Design/phases-status.md reports/tracker-048-capture-replay-comparison-implementation-20260512155553.md reports/tracker-048-review-20260512160935.md reports/tracker-049-operation-docs-implementation-20260512164929.md reports/tracker-049-review-20260512165902.md Tracker/Tracker.Server/README.md`
- `rg --files Tracker/Tracker.CaptureReplay Tracker/Tracker.Server/Components/Pages Tracker/Tracker.Server/Tracking | rg 'Tracker\\.CaptureReplay|Diagnostics|Tracking'`
- `nl -ba /home/ibis/.codex/memories/MEMORY.md | sed -n '88,142p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '1,145p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-architecture-plan.md | sed -n '540,565p'`
- `nl -ba Tracker/Tracker.Core/Design/tasks-status.md | sed -n '1,70p'`
- `nl -ba Tracker/Tracker.Core/Design/phases-status.md | sed -n '1,45p'`
- `nl -ba reports/tracker-048-capture-replay-comparison-implementation-20260512155553.md | sed -n '1,120p'`
- `nl -ba reports/tracker-048-review-20260512160935.md | sed -n '1,120p'`
- `nl -ba reports/tracker-049-operation-docs-implementation-20260512164929.md | sed -n '1,110p'`
- `nl -ba reports/tracker-049-review-20260512165902.md | sed -n '1,115p'`
- `nl -ba Tracker/Tracker.Server/README.md | sed -n '145,225p'`
- `rg -n "TrackerSnapshotReplayReader|trackerSnapshot|trackerComparison|SnapshotSidecar|ComparisonSummaries|SnapshotInputs|RenderSnapshot|DiagnosticsPlaybackState|Metadata|Settings|Selected|Playback|Play|FastForward" Tracker/Tracker.CaptureReplay Tracker/Tracker.Server/Components/Pages Tracker/Tracker.Server/Tracking -g '*.cs' -g '*.razor'`
- `nl -ba Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs | sed -n '1,230p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '1,190p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '400,470p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs | sed -n '1,270p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs | sed -n '220,300p'`
- `nl -ba Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs | sed -n '120,170p'`
- `git status --short`
- `git diff --name-status`

## 対象ファイル

- 確認:
  - `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
  - `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `Tracker/Tracker.Core/Design/tasks-status.md`
  - `Tracker/Tracker.Core/Design/phases-status.md`
  - `reports/tracker-048-capture-replay-comparison-implementation-20260512155553.md`
  - `reports/tracker-048-review-20260512160935.md`
  - `reports/tracker-049-operation-docs-implementation-20260512164929.md`
  - `reports/tracker-049-review-20260512165902.md`
  - `Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs`
  - `Tracker/Tracker.CaptureReplay/Program.cs`
  - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
  - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
  - `Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs`
  - `Tracker/Tracker.Server/Components/Pages/DiagnosticsProfileMetadataLoader.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
  - `Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs`
  - `Tracker/Tracker.Server/README.md`
- 変更:
  - `reports/tracker-049-diagnostics-playback-design-audit-20260512170903.md`

## 指摘事項

- Normal-path blocker:
  - Medium: `Tracker/Tracker.Core/Design/tasks-status.md:10`、`Tracker/Tracker.Core/Design/tasks-status.md:12`、`Tracker/Tracker.Core/Design/tasks-status.md:13`、`Tracker/Tracker.Core/Design/tasks-status.md:48` は、`TRACKER-049` 実装 report / review report 作成後も `TDD未着手`、Implementation / Review `未着手`、一覧 `planned` のまま残っている。これは `reports/tracker-049-review-20260512165902.md:78` から `reports/tracker-049-review-20260512165902.md:95` の blocking normal-path finding と同じで、docs-only 完了可否とは別に、progress sync なしでは `TRACKER-049` を完了扱いにできない。
- User-confirmation-required design gap:
  - `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md:95` は diagnostics log reader、`Tracker.CaptureReplay`、diagnostics playback が metadata relative path から snapshot sidecar を読むと書き、同 `:107` は `Tracker.CaptureReplay` と `/diagnostics` playback が snapshot log を読み、ibis committed frame と並べて再生・比較表示できる必要があると書く。さらに同 `:125` と `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:556` は diagnostics viewer / playback でも同じ snapshot log の timeline、frame number / timestamp、ball / robot count、raw payload 復元状態を表示できる入力契約として読める。
  - これに対して current fixed task list は、`Tracker/Tracker.Core/Design/tasks-status.md:29`、`:47` と `Tracker/Tracker.Core/Design/phases-status.md:23` で `TRACKER-048` の完了実績を `Tracker.CaptureReplay` の `trackerSnapshot` / `trackerComparison` 出力へ寄せて記録している。`reports/tracker-048-capture-replay-comparison-implementation-20260512155553.md:18` から `:24` は diagnostics playback UI の本格接続を対象外にしており、同 `:88` から `:99` は CLI 出力接続を成果としている。`reports/tracker-048-review-20260512160935.md:88` から `:101` も、この CLI 実装に対して blocking finding なしと判定している。
  - 現実装でも snapshot / comparison 行の接続は `Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs:90`、`:148`、`:155`、`:162`、`:165` にあり、`/diagnostics` は `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs:110` から `:116`、`:409` から `:440` の diagnostics log / render snapshot / profile metadata 同期と、`Tracker/Tracker.Server/Components/Pages/Diagnostics.razor:192` から `:252` の既存 diagnostics entry playback に留まる。`TrackerSnapshotReplayReader` 自体は `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs:7` から `:15`、`:229` から `:242` で diagnostics / replay / playback 用入力契約を返せるが、Diagnostics component からは参照されていない。
  - したがって、`TRACKER-048` は「固定一覧を narrow に読むなら `Tracker.CaptureReplay` だけで完了済み」、一方で「design 完了条件を literal に読むなら diagnostics playback UI 接続が未実装」である。これは追加 `TRACKER` を即時作成するより先に、design の完了条件と固定一覧のどちらを正とするかを親・ユーザーが裁定すべき design gap である。
- Held concern:
  - `Tracker/Tracker.Server/README.md:213` から `:214` は `Tracker.CaptureReplay` の `trackerComparison` 行と `/diagnostics` の frame 時刻 / output frame を手動で対応付ける手順になっており、現在の実装能力とは整合する。ただし `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md:112` の `Tracker.CaptureReplay` または diagnostics playback という task 文言と、同 `:107` / `:125` の `Tracker.CaptureReplay` と diagnostics playback という phase / completion 文言が混在しているため、PR ready 前に wording を揃えないと後続 review で同じ capability gap が再発し得る。
  - `Tracker/Tracker.Server/README.md:211` は sidecar file の存在確認を書いているが、実装上は `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs:233` から `:243` で最初の snapshot record 書き込み時に file を作り、metadata は `Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs:140` から `:145` で `IsCreated` を表現する。外部 tracker packet が見えていない調査時は、file 不在かつ metadata `IsCreated=false` / `RecordCount=0` も正常系になり得る。

## 結果

- `TRACKER-049` を docs-only で完了できるかは、progress sync blocker 解消後でも design 裁定に依存する。README 自体は現実装に忠実で、CLI で比較出力を確認し、`/diagnostics` では同じ log/frame/settings を手動対応付けする運用説明になっている。
- ただし現 design は、単なる manual correlation ではなく diagnostics playback UI が tracker packet snapshot timeline / comparison を読む能力まで要求しているように読める。この読みを維持するなら、`TRACKER-049` docs-only 完了ではなく、design / tracking を修正して diagnostics playback UI 接続を追加作業として固定一覧に入れる必要がある。
- 親が次に取る選択肢:
  - 推奨: 早期 release 優先で、現 PR の normal path は `Tracker.CaptureReplay` による比較表示、`/diagnostics` は manual correlation と明文化する。`tracker-server-cli-ui-detail-design.md` と `tracker-architecture-plan.md` の完了条件を README / fixed task list に合わせ、`tasks-status.md` / `phases-status.md` を同期してから `TRACKER-049` review gate を閉じる。追加実装は作らない。
  - 代替: design literal を維持し、`TRACKER-050` の前に diagnostics playback UI が `TrackerSnapshotReplayReader` の `SnapshotInputs` / `ComparisonSummaries` を読み、source role / label、tracked timestamp、ball / robot count、raw payload restored、nearest timestamp summary を UI 表示・playback 入力へ接続する task を固定一覧に追加する。その場合、`TRACKER-048` は CLI partial として記録を修正し、`TRACKER-049` docs-only 完了は保留する。
  - 保留案: `TRACKER-049` は progress sync のみ行って docs review を閉じるが、design mismatch を `TRACKER-050` の PR ready risk として残す。この案は同じ指摘が final review で再発する可能性が高いため非推奨。
- 今回は監査のみで、design / tracking / code は編集していない。

## リスク

- 本監査は repository 読み合わせに基づく。dotnet build / test は実行していない。
- 監査 report 自体のみを更新した。`Tracker/Tracker.Server/README.md` など、親エージェントまたは他 sub-agent の既存変更は revert していない。
- design literal を維持する場合、追加 UI 実装のサイズは docs follow-up ではなく新規 task 相当になる。Diagnostics component は現在 `TrackerSnapshotReplayReader` を注入・利用していないため、UI 表示、playback 選択同期、test、manual evidence の再設計が必要になる。
