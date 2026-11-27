# Sub-agent実行レポート

## タスク

Markdown lint の出力に出た語だけを対象に、文書またはホワイトリストを最小修正する。

## sub-agentを使う理由

ユーザー指定により `gpt-5.5 high` で作業させ、親側の広すぎる本文置換を避けるため。

## 対象範囲

`npm run lint:md:whitelist -- --changed --list-unknown` で検出された項目。

## 対象外

lint に出ていない文言の言い換え、設計本文の全文整形、コード変更。

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/review-enforcer/SKILL.md`
- `npm run lint:md:whitelist -- --changed --list-unknown`
- `npm run lint:md:whitelist -- --changed`
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files __no_markdown_input__.md --list-unknown`
- `git diff --check -- tools/lint/markdown-whitelist.yaml reports/doc-lint-gpt55-high-lint-only-20260517094500.md Tracker/Design/tasks-status.md`

## 対象ファイル

- `tools/lint/markdown-whitelist.yaml`
- `Tracker/Design/tasks-status.md`
- `reports/doc-lint-gpt55-high-lint-only-20260517094500.md`

## 指摘事項

- 初回 `npm run lint:md:whitelist -- --changed --list-unknown` で `AutoRef` 3 件、`ID` 4 件、`ソースコード` 3 件を確認した。
- 通常出力では指摘対象 Markdown は `Tracker/Design/tasks-status.md` のみだった。
- `ID` は `- ID:` と作業一覧の列名として単独出現しており、複合語では登録できない文脈だったため単独登録した。
- `AutoRef` は既存の `AutoRef mode` の別名として登録し、`ソースコード` は一般表記として登録した。

## 結果

- `tools/lint/markdown-whitelist.yaml` に `AutoRef`、`ID`、`ソースコード` の許可を最小追加した。
- `npm run lint:md:whitelist -- --changed --list-unknown` は成功した。
- `npm run lint:md:whitelist -- --changed` は成功した。
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files __no_markdown_input__.md --list-unknown` は成功した。
- `git diff --check -- tools/lint/markdown-whitelist.yaml reports/doc-lint-gpt55-high-lint-only-20260517094500.md Tracker/Design/tasks-status.md` は成功した。

## リスク

- `ID` は単独登録のため受け入れ範囲が広い。ただし今回の lint 指摘では作業識別子ラベルと列名として単独出現しており、本文の広い置換を避けるため最小の許可として扱った。
- `Tracker/Design/tasks-status.md` には既存差分があるが、この作業では本文を追加変更していない。
