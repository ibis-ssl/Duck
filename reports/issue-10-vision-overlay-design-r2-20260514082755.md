# Sub-agent実行レポート

## タスク

- 目的: ユーザー追加指示を受け、RAW-VISION-013 の diagnostics time-sync 方針を missing 表示から latest-before 表示維持へ修正する。
- タスク種別: 設計修正

## sub-agentを使う理由

- 理由: ユーザー指示により、設計修正は gpt-5.5 high の sub-agent に委譲し、親はマネージャーとして report と差分を裁定する。

## 対象範囲

- 対象: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`、必要最小限の `Tracker/Tracker.Server/Design/tasks-status.md` / `phases-status.md` 同期、設計修正 report。

## 対象外

- 対象外: production code、test code、README、PR本文更新、レビュー、実装。

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/codex-delegation-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-executor/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
  - `rg -n "RAW-VISION|raw-vision|VisionPacketStore|latest-before|diagnostics" /home/ibis/.codex/memories/MEMORY.md`
  - `git status --short`
  - `sed -n '1,240p' reports/issue-10-vision-overlay-design-r2-20260514082755.md`
  - `sed -n '1,260p' reports/issue-10-vision-overlay-design-20260514082233.md`
  - `rg -n "CandidateMissing|NoCandidateSnapshot|missing|snapshot|alignment|RAW-VISION-014|RAW-VISION-015|receivedAt|delta|stale|latest" Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `sed -n '1,260p' Tracker/Tracker.Server/Design/tasks-status.md`
  - `sed -n '1,220p' Tracker/Tracker.Server/Design/phases-status.md`
  - `sed -n '170,335p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `nl -ba Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md | sed -n '220,330p'`
  - `nl -ba Tracker/Tracker.Server/Design/tasks-status.md | sed -n '1,80p;120,165p'`
  - `nl -ba Tracker/Tracker.Server/Design/phases-status.md | sed -n '1,80p'`
  - `nl -ba reports/issue-10-vision-overlay-design-r2-20260514082755.md`
  - `nl -ba /home/ibis/.codex/memories/MEMORY.md | sed -n '191,193p;297,324p'`
  - `git diff -- Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `git diff -- Tracker/Tracker.Server/Design/tasks-status.md Tracker/Tracker.Server/Design/phases-status.md`
  - `rg -n "CandidateMissing|NoCandidateSnapshot|future|later|latest-before|diagnostics-line|nearest timestamp|missing 表示" Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker/Tracker.Server/Design/tasks-status.md Tracker/Tracker.Server/Design/phases-status.md`
  - `sed -n '228,325p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `git diff --check -- Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker/Tracker.Server/Design/tasks-status.md Tracker/Tracker.Server/Design/phases-status.md reports/issue-10-vision-overlay-design-r2-20260514082755.md`
  - `git diff --check --no-index /dev/null reports/issue-10-vision-overlay-design-r2-20260514082755.md`
  - `sed -n '1,220p' reports/issue-10-vision-overlay-design-r2-20260514082755.md`
  - `git diff --stat -- Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker/Tracker.Server/Design/tasks-status.md Tracker/Tracker.Server/Design/phases-status.md reports/issue-10-vision-overlay-design-r2-20260514082755.md`
  - `git status --short`
  - `rg -n "latest-before|selected tick|selected replay|selected time|hold|delta|時間軸|slide|スライド|future|later|RAW-VISION-014|RAW-VISION-015" Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `sed -n '224,305p' Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `sed -n '20,32p;92,100p' Tracker/Tracker.Server/Design/tasks-status.md`
  - `sed -n '13,20p' Tracker/Tracker.Server/Design/phases-status.md`
  - `sed -n '1,220p' reports/issue-10-vision-overlay-design-r2-20260514082755.md`
  - `git diff --check -- Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker/Tracker.Server/Design/tasks-status.md Tracker/Tracker.Server/Design/phases-status.md reports/issue-10-vision-overlay-design-r2-20260514082755.md`
  - `git diff -- Tracker/Tracker.Server/Design/tasks-status.md reports/issue-10-vision-overlay-design-r2-20260514082755.md`
  - `git diff --stat -- Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker/Tracker.Server/Design/tasks-status.md Tracker/Tracker.Server/Design/phases-status.md reports/issue-10-vision-overlay-design-r2-20260514082755.md`
  - `git status --short`

## 対象ファイル

- 変更または確認したファイル:
  - 変更: `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - 変更: `Tracker/Tracker.Server/Design/tasks-status.md`
  - 変更: `Tracker/Tracker.Server/Design/phases-status.md`
  - 変更: `reports/issue-10-vision-overlay-design-r2-20260514082755.md`
  - 確認: `reports/issue-10-vision-overlay-design-20260514082233.md`
  - 非対象維持: `Tracker/Tracker.Server/appsettings.json` の既存差分は触っていない。

## 指摘事項

- 指摘要約または「指摘なし」:
  - 指摘なし。

## 結果

- 結果:
  - `raw-vision-viewer-plan.md` の `Diagnostics time-sync 方針` を更新し、selected tick に対象 `3rd party tracker` source の alignment record が無い場合の `CandidateMissing` / `NoCandidateSnapshot` 方針を撤回した。
  - 新方針として、同じ source の selected tick 以前に存在する最新の `latest-before snapshot` を Field source と comparison に使い、表示が消えないことを採用した。
  - latest-before 採用時は UI / comparison に matching rule、source snapshot の実際の `receivedAt`、selected tick との差分 delta、stale / latest-before 状態を明示する方針にした。
  - selected tick 以前に同じ source の snapshot が一切無い場合のみ `CandidateMissing` / `NoCandidateSnapshot` 相当の missing 表示とし、その場合も ready layer と Field 全体は残す方針にした。
  - future / later snapshot fallback は、未来 tick の tracker 状態を現在 tick の比較へ混ぜて replay timeline の因果関係を崩すため不採用と明記した。
  - `テスト方針` と RAW-VISION-014 を、missing regression ではなく latest-before fallback regression を先に固定する TDD 対象へ更新した。
  - RAW-VISION-015 を、diagnostics selected replay timeline tick の target source missing 時に latest-before snapshot を探して表示/比較する実装方針へ更新した。
  - `用語` に `latest-before snapshot` を追加した。
  - 追加指示を受け、親追記により `raw-vision-viewer-plan.md` へ selected replay timeline tick / selected time を source ごとに動かさず、latest-before は直前 sample の hold として扱う方針が入っていることを確認した。
  - `tasks-status.md` の RAW-VISION-014 / RAW-VISION-015 にも、selected replay timeline tick / selected time を固定したまま latest-before snapshot を hold として表示/比較する TDD / 実装条件を最小追記した。

## リスク

- 未解決のリスクまたは後続対応:
  - RAW-VISION-014 では、selected tick に record が無いが過去 snapshot はあるケースと、selected tick 以前に snapshot が一切無いケースを分けた failing test が必要。
  - RAW-VISION-014 / RAW-VISION-015 では、latest-before 採用時の delta 計算基準を selected replay timeline tick と source snapshot `receivedAt` で一貫させる必要がある。
  - RAW-VISION-015 では、latest-before 探索が future / later snapshot を候補に含めないことを実装とテストの両方で確認する必要がある。
  - RAW-VISION-014 / RAW-VISION-015 では、latest-before 採用時に source ごとの selected time を更新していないことをテストと UI 表示の両方で確認する必要がある。
