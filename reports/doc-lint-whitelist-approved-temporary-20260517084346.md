# Sub-agent実行レポート

## タスク

- 目的: ユーザーが一時許可した whitelist 候補を反映し、Markdown lint の残件を減らす。
- タスク種別: 実装

## sub-agentを使う理由

- 理由: ユーザー指定により、lint 修正作業をサブエージェントで分担するため。

## 対象範囲

- 対象:
  - `tools/lint/markdown-whitelist.yaml`
  - `Tracker/Design/**` の `Kalman` / 一般語 `Tracker` 表記
  - `reports/doc-lint-whitelist-approved-temporary-20260517084346.md`

## 対象外

- 対象外:
  - lint script の変更
  - `reports/**` の lint 対象化
  - commit、push、PR 作成

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
  - `sed -n '1,220p' reports/doc-lint-whitelist-approved-temporary-20260517084346.md`
  - `sed -n '1,260p' reports/doc-lint-text-design-details-20260517003617.md`
  - `sed -n '1,260p' tools/lint/markdown-whitelist.yaml`
  - `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files __no_markdown_input__.md --list-unknown`
  - `npm run lint:md:spell -- --no-progress`
  - `npm run lint:md:whitelist`
  - `npm run lint:md:text`
  - `git diff --check`
  - `npm run lint:md -- --no-progress`

## 対象ファイル

- 変更または確認したファイル:
  - `tools/lint/markdown-whitelist.yaml`
  - `Tracker/Design/Archive/Core/phases-status.md`
  - `Tracker/Design/Archive/Core/tasks-status.md`
  - `Tracker/Design/Archive/DebugHost/phases-status.md`
  - `Tracker/Design/Archive/DebugHost/tasks-status.md`
  - `Tracker/Design/Core/tracker-architecture-plan.md`
  - `Tracker/Design/Core/tracker-core-engine-detail-design.md`
  - `Tracker/Design/Core/tracker-history-000-038.md`
  - `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
  - `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
  - `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
  - `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
  - `Tracker/Design/RuntimeHost/runtime-host-plan.md`
  - `Tracker/Design/phases-status.md`
  - `Tracker/Design/tasks-status.md`
  - `reports/doc-lint-whitelist-approved-temporary-20260517084346.md`

## 指摘事項

- 指摘要約または「指摘なし」:
  - 指摘なし。
  - 追加確認で、製品名は `Tracker.DebugHost`、`Tracker.RuntimeHost`、`Tracker.CaptureReplay`、`Tracker.Server` のような複合語で登録する方針へ寄せた。
  - `DebugHost`、`RuntimeHost`、`CaptureReplay` の単体 alias は外し、本文側の短縮表記を `Tracker.*` へ寄せた。

## 結果

- 結果:
  - `Kalman` の一般説明を `カルマン` へ寄せ、`カルマン` を whitelist に登録した。
  - `Tracker` / `tracker` の一般説明を `トラッカー` へ寄せ、`トラッカー` を whitelist に登録した。
  - 型名、設定名、プロジェクト名、ファイル名、パス、命令、識別子の `Tracker.*` / `Kalman*` は壊さないように残した。
  - ユーザーが一時許可した追加候補、単語単体の一時許可、一般英語、日本語化候補を `temporary-doc-lint-terms` として登録した。
  - `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files __no_markdown_input__.md --list-unknown` は成功した。
  - `npm run lint:md:spell -- --no-progress` は成功した。
  - `npm run lint:md:whitelist` は成功した。
  - `npm run lint:md:text` は成功した。
  - `git diff --check` は成功した。
  - 追加確認後の `npm run lint:md -- --no-progress` は成功した。

## リスク

- 未解決のリスクまたは後続対応:
  - 今回は lint 通過を優先し、一時許可語を広めに登録した。
  - `temporary-doc-lint-terms` は後で本文の日本語化や設計語の整理が進んだ段階で削る前提の暫定登録である。
