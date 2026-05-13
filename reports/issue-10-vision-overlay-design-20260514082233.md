# Sub-agent実行レポート

## タスク

- 目的: RAW-VISION-013 の設計編集として、Issue #10 の Vision split / overlay、3rd party tracker source、same render tick 方針、diagnostics time-sync gap 修正方針、geometry 基準、diagnostics 寄せ UI、固有名詞説明を設計書へ反映する。
- タスク種別: 設計編集

## sub-agentを使う理由

- 理由: ユーザー指示により、設計編集は gpt-5.5 high の sub-agent に委譲し、親はマネージャーとして report と差分を裁定する。

## 対象範囲

- 対象: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`、必要最小限の `Tracker/Tracker.Server/Design/tasks-status.md` / `phases-status.md` 同期、設計 report。

## 対象外

- 対象外: production code、test code、README、PR本文更新、レビュー、実装。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `rg -n "RAW-VISION|raw-vision|Issue #10|Vision overlay|diagnostics" /home/ibis/.codex/memories/MEMORY.md`
  - `pwd && git status --short`
  - `nl -ba /home/ibis/.codex/memories/MEMORY.md | sed -n '191,197p'`
  - `nl -ba /home/ibis/.codex/memories/MEMORY.md | sed -n '297,324p'`
  - `sed -n '1,220p' reports/issue-10-vision-overlay-design-20260514082233.md`
  - `sed -n '1,260p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `sed -n '1,220p' Tracker/Tracker.Server/Design/tasks-status.md`
  - `sed -n '1,220p' Tracker/Tracker.Server/Design/phases-status.md`
  - `sed -n '1,240p' reports/issue-10-vision-overlay-investigation-20260514080106.md`
  - `sed -n '1,240p' reports/issue-10-live-same-tick-investigation-20260514081135.md`
  - `sed -n '1,260p' reports/issue-10-diagnostics-time-sync-audit-20260514081730.md`
  - `git diff -- Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `git diff -- Tracker/Tracker.Server/Design/tasks-status.md Tracker/Tracker.Server/Design/phases-status.md`
  - `sed -n '120,260p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md && sed -n '260,380p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `git status --short`
  - `git diff --stat -- Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker/Tracker.Server/Design/tasks-status.md Tracker/Tracker.Server/Design/phases-status.md reports/issue-10-vision-overlay-design-20260514082233.md`
  - `sed -n '1,260p' reports/issue-10-vision-overlay-design-20260514082233.md`
  - `git diff --check -- Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker/Tracker.Server/Design/tasks-status.md Tracker/Tracker.Server/Design/phases-status.md reports/issue-10-vision-overlay-design-20260514082233.md`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - 変更: `Tracker/Tracker.Server/Design/tasks-status.md`
  - 変更: `Tracker/Tracker.Server/Design/phases-status.md`
  - 変更: `reports/issue-10-vision-overlay-design-20260514082233.md`
  - 確認: `reports/issue-10-vision-overlay-investigation-20260514080106.md`
  - 確認: `reports/issue-10-live-same-tick-investigation-20260514081135.md`
  - 確認: `reports/issue-10-diagnostics-time-sync-audit-20260514081730.md`
  - 非対象維持: `Tracker/Tracker.Server/appsettings.json` の既存差分は触っていない。

## 指摘事項

- 指摘要約または「指摘なし」:
  - 指摘なし。

## 結果

- 結果:
  - `raw-vision-viewer-plan.md` に `Issue #10 split / overlay source 設計` を追加し、source 候補を `Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` に固定した。
  - live 比較は、厳密な同一 packet timestamp / 同一 receive callback ではなく、同じ `UI render tick` で latest immutable snapshot を固定する方針として記録した。
  - 3rd party tracker は `MultiTrackerManager<TrackerPacketAdapter>` から受けるが、UI は immutable snapshot store / composer を挟む方針にし、CaptureOn sidecar writer を live store にする方針は不採用とした。
  - geometry は raw geometry 優先、なければ tracked geometry fallback とし、3rd party tracker packet から geometry を復元する方針は不採用とした。
  - `Diagnostics time-sync 方針` を追加し、通常経路では Vision/Input と ibis tracker は selected tick の render frame、3rd party tracker は同じ `ReplayTimelineIndex` の `saved-session-alignment` を使う根拠を明記した。対象 source の alignment record が無い場合は `CandidateMissing` / `NoCandidateSnapshot` 相当の missing 表示にし、diagnostics-line alignment / nearest timestamp / future-later snapshot へ fallback しない方針にした。
  - split / overlay の details、legend、layer visibility、same-source 1 layer 化、missing layer でも ready layer を残す挙動は diagnostics に寄せる方針として記録した。
  - `用語` 節を追加し、`Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker`、`UI render tick`、`immutable snapshot`、`MultiTrackerManager`、`TrackerPacketAdapter`、`ReplayTimelineIndex`、`saved-session-alignment`、`CandidateMissing`、`NoCandidateSnapshot`、`VisionFieldCanvas` を説明した。
  - `テスト方針` に Vision split / overlay contract、live comparison immutable snapshot、3rd party tracker live source、diagnostics time-sync regression を追加した。
  - tracking は `RAW-VISION-014` / `RAW-VISION-015` と verification phase の文言だけ最小同期した。

## リスク

- 未解決のリスクまたは後続対応:
  - RAW-VISION-014 では、設計どおり failing test で split / overlay contract と diagnostics time-sync regression を先に固定する必要がある。
  - RAW-VISION-015 では、`MultiTrackerManager` の mutable state を UI から直接読まない store / composer 境界を実装時に崩さないこと。
  - diagnostics の legacy fallback は既存 session 互換のため残す必要があるため、selected `ReplayTimelineIndex` がある経路だけ fallback を抑止するように実装範囲を限定すること。
  - raw geometry が無い初期状態では tracked geometry fallback または missing 表示の UI が発生するため、empty state と timestamp metadata の表示を実装・manual evidence で確認すること。
