# Sub-agent実行レポート

## タスク

- 目的: Active tracking 文書を Markdown lint に通る表記へ修正する。
- タスク種別: 実装

## sub-agentを使う理由

- 理由: ユーザー指定により、lint 修正作業をサブエージェントで分担するため。

## 対象範囲

- 対象:
  - `Tracker/Design/phases-status.md`
  - `Tracker/Design/tasks-status.md`

## 対象外

- 対象外:
  - `tools/lint/markdown-whitelist.yaml`
  - Archive 配下
  - 詳細設計文書
  - lint script

## 実行コマンド

- 実行コマンド:
  - `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/Design/phases-status.md Tracker/Design/tasks-status.md reports/doc-lint-text-active-status-20260517003617.md`
  - `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/Design/phases-status.md Tracker/Design/tasks-status.md reports/doc-lint-text-active-status-20260517003617.md --print0 | xargs -0 -r node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js`
  - `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files Tracker/Design/phases-status.md Tracker/Design/tasks-status.md reports/doc-lint-text-active-status-20260517003617.md`
  - `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files Tracker/Design/phases-status.md Tracker/Design/tasks-status.md reports/doc-lint-text-active-status-20260517003617.md --print0 | xargs -0 -r ./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules`

## 対象ファイル

- 変更または確認したファイル:
  - `Tracker/Design/phases-status.md`
  - `Tracker/Design/tasks-status.md`
  - `reports/doc-lint-text-active-status-20260517003617.md`
  - `tools/lint/markdown-whitelist.yaml` は確認のみ。編集なし。

## 指摘事項

- 指摘要約または「指摘なし」:
  - 指摘なし。
  - 複合語として新規 whitelist 追加候補に残すべき語はなし。

## 結果

- 結果:
  - Active tracking 文書内の一般英語、表見出し、状態値、進捗文を日本語表記へ寄せた。
  - 固有名詞、型名、設定キー、画面表示名、実行コマンド、報告ファイルパスはコード表記または既存表記で保持した。
  - focused cspell は 2 ファイル確認で issues 0。
  - focused whitelist check は成功。
  - focused textlint は成功。

## リスク

- 未解決のリスクまたは後続対応:
  - 残件なし。
  - `reports/doc-lint-text-active-status-20260517003617.md` は対象列挙で除外されるため、lint 実行対象は active tracking 2 ファイルのみだった。
