# DOC-LINT-001 prh 導入報告

## 目的

`textlint` に `textlint-rule-prh` を追加し、表記揺れ辞書を文書検査の一部として読み込む。

## 変更

- `textlint-rule-prh` を `devDependencies` に追加した。
- `.textlintrc.json` に `prh` 規則を追加し、`tools/lint/prh.yml` を読むようにした。
- `tools/lint/prh.yml` を追加した。初期状態では具体的な表記統一規則を登録しない。
- `tools/lint/README.md` に環境構築、実行時の読み込み、表記揺れ辞書の更新方針を追記した。
- 利用者の明示確認を受け、`Markdown` と `リンク` を `tools/lint/markdown-whitelist.yaml` に追加した。

## 方針

`prh` 規則は文書全体の表記方針を変えるため、追加や変更は利用者の明示確認を受けてから行う。

## 検証

- `npm run lint:md:text` は成功した。
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files tools/lint/README.md --print0 | xargs -0 -r node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js` は成功した。
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files tools/lint/README.md` は成功した。
- `git diff --check` は成功した。

## 注意

全範囲 `npm run lint:md` は、既存文書の未登録語を大量に許可一覧へ自動追加しない方針のため、DOC-LINT-001 の既知状態として失敗し得る。
