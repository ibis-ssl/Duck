# Sub-agent実行レポート

## タスク

- 目的: 詳細設計文書を Markdown lint に通る表記へ修正する。
- タスク種別: 実装

## sub-agentを使う理由

- 理由: ユーザー指定により、lint 修正作業をサブエージェントで分担するため。

## 対象範囲

- 対象:
  - `Tracker/Design/Core/tracker-architecture-plan.md`
  - `Tracker/Design/Core/tracker-core-engine-detail-design.md`
  - `Tracker/Design/Core/tracker-history-000-038.md`
  - `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
  - `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
  - `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
  - `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
  - `Tracker/Design/RuntimeHost/runtime-host-plan.md`

## 対象外

- 対象外:
  - `tools/lint/markdown-whitelist.yaml`
  - 進捗文書
  - README 群
  - lint script

## 実行コマンド

- 実行コマンド:
  - `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
  - `sed -n '1,260p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
  - `sed -n '1,240p' reports/doc-lint-text-design-details-20260517003617.md`
  - `sed -n '1,220p' tools/lint/markdown-whitelist.yaml`
  - `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files <対象9ファイル>`
  - `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files <対象9ファイル> --list-unknown`
  - `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files <対象9ファイル> --print0 | xargs -0 -r node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js`
  - `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files <対象9ファイル> --print0 | xargs -0 -r ./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules`

## 対象ファイル

- 変更または確認したファイル:
  - 確認:
    - `Tracker/Design/Core/tracker-architecture-plan.md`
    - `Tracker/Design/Core/tracker-core-engine-detail-design.md`
    - `Tracker/Design/Core/tracker-history-000-038.md`
    - `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
    - `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
    - `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
    - `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
    - `Tracker/Design/RuntimeHost/runtime-host-plan.md`
    - `tools/lint/markdown-whitelist.yaml`
  - 変更:
    - `reports/doc-lint-text-design-details-20260517003617.md`

## 指摘事項

- 指摘要約または「指摘なし」:
  - cspell / whitelist の残件は未解消。
  - 対象設計文書には一般英語の説明語が大量に残っている。
  - 単純な機械置換では型名、設定キー、リンク先、設計語を破壊するため、安全な修正として採用しなかった。
  - 複合語として whitelist 候補に残す価値がある語:
    - `same-process`
    - `headless host`
    - `tracker operation loop`
    - `runtime host`
    - `debug host`
    - `read-side snapshot`
    - `diagnostics sample sidecar`
    - `capture viewer`
    - `raw vision viewer`
    - `camera-local`
    - `multi-camera`
    - `event-time reorder`
    - `latest immutable snapshot`

## 結果

- 結果:
  - `textlint`: 対象ファイルのみで通過。
  - `cspell`: 対象設計文書 8 ファイルで失敗。例: `debug`, `diagnostics`, `raw`, `detection`, `geometry`, `official`, `proto`, `snapshot`, `tracker`, `packet`。
  - `whitelist`: 対象ファイルのみで失敗。例: `Tracker/Design/Core/tracker-architecture-plan.md:1 'Tracker'`, `:5 'debug'`, `:9 'raw'`, `:10 'official'`。
  - whitelist は編集していない。
  - 安全に意味を保持できる本文修正は完了していない。

## リスク

- 未解決のリスクまたは後続対応:
  - 詳細設計文書は Markdown lint 残件をまだ持つ。
  - 設計語と一般英語の境界が広く、文単位での人手判断が必要。
  - 単語単体の whitelist 追加は禁止されているため、本文日本語化と複合語候補の利用者確認を分けて継続する必要がある。
