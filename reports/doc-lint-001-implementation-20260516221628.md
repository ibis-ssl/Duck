# Sub-agent実行レポート

## タスク

- 目的: `DOC-LINT-001` として Markdown 向け `textlint` / `cspell` を導入し、英単語とカタカナ語を意味付き whitelist で管理できるようにする。
- タスク種別: implementation / validation

## sub-agentを使う理由

- 理由: IbisDuck の開発運用では実装・検証を sub-agent に委譲する。加えて standards detection / validation は `codex-delegation-executor` 上で sub-agent 必須カテゴリである。

## 対象範囲

- 対象: repository root の Node tooling、Markdown lint / spellcheck 設定、whitelist 管理ファイル、関連 tracking 更新に必要な実装証跡。

## 対象外

- 対象外: .NET runtime behavior の変更、既存 C# 実装変更、PR #19 の runtime feature scope 変更、vendored/generated/build output の品質改善。

## 実行コマンド

- 実行コマンド:
- `npm run lint:md` 導入前確認: failed。repository root に `package.json` がなく、`ENOENT Could not read package.json` で失敗することを確認した。
- `npm install`: success。root の `package.json` / `package-lock.json` に Markdown lint 用 dependency を導入した。
- 初版 whitelist: 対象 Markdown 内の既存脚注から `tools/lint/markdown-whitelist.yaml` を作成した。生成語は 10 語で、各 entry は `term` と `description` を持つ。収集 script は残していない。
- `npm run lint:md:targets | wc -l`: success。対象 Markdown は 442 files。
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files README.md Tracker/README.appsettings.md`: success。対象ファイルを直接指定する mode を確認した。
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files tools/lint/README.md --list-unknown`: failed。直接指定したファイルだけを whitelist check できることを確認した。
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files node_modules/cspell/README.md | wc -l`: success / `0`。直接指定 mode でも dependency directory が対象外になることを確認した。
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files tools/lint/README.md --print0 | xargs -0 -r ./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules`: success。README に記載した direct-file textlint pipeline が repo root で動くことを確認した。
- `npm run lint:md:text`: success。
- `printf 'tracker-debug-host\ntracker debug host\nTracker.DebugHost\n' | node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --stdin smoke.md`: success。hyphen 付き表記、space 区切り表記、識別子表記の alias 許可を確認した。
- `printf 'debug\nカタカナ\n' | node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --stdin smoke.md`: failed。単独の未登録英単語・カタカナ語が落ちることを確認した。
- `printf '`debug` ... fenced code ...' | node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --stdin smoke.md`: success。inline code / fenced code は対象外になることを確認した。
- `npm run lint:md:whitelist -- --list-unknown | wc -l`: failed / 2293 lines。既存 Markdown と whitelist 説明文に未登録の英単語・カタカナ語が残っていることを確認した。
- `npm run lint:md:spell -- --no-progress --no-summary`: failed。専用 whitelist が未登録語を許可しないため、既存 Markdown の英単語で失敗することを確認した。
- `tmp=codex-md-lint-smoke-allowed.md; printf 'tracker-debug-host\ntracker debug host\nTracker.DebugHost\n' > "$tmp"; node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js "$tmp"`: success。`cspell` 側でも alias 表記を許可できることを確認した。
- `tmp=codex-md-lint-smoke-debug.md; printf 'debug\n' > "$tmp"; node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js "$tmp"`: failed。alias の構成語を単独許可しないことを確認した。
- `git diff --check`: success。

## 対象ファイル

- 変更または確認したファイル:
- `package.json`
- `package-lock.json`
- `.textlintrc.json`
- `.textlintignore`
- `cspell.config.jsonc`
- `tools/lint/README.md`
- `tools/lint/markdown-whitelist.yaml`
- `tools/lint/markdown-targets.json`
- `.agents/skills/review-enforcer/scripts/list-markdown-targets.js`（CodexSkill 側）
- `.agents/skills/review-enforcer/scripts/run-cspell-markdown.js`（CodexSkill 側）
- `.agents/skills/review-enforcer/scripts/check-markdown-whitelist.js`（CodexSkill 側）
- `.agents/skills/review-enforcer/scripts/textlint-rules/no-fullwidth-space.js`（CodexSkill 側）
- `.agents/skills/review-enforcer/scripts/textlint-rules/no-unresolved-placeholder-marker.js`（CodexSkill 側）
- `reports/doc-lint-001-implementation-20260516221628.md`
- `Tracker/Design/tasks-status.md` / `Tracker/Design/phases-status.md` は親 agent 既存差分として確認したが、大きな追記は行っていない。

## 指摘事項

- 指摘要約または「指摘なし」:
- 指摘なし。

## 結果

- 結果:
- repository root に `npm run lint:md`、`npm run lint:md:text`、`npm run lint:md:spell`、`npm run lint:md:whitelist`、`npm run lint:md:targets` を追加した。
- lint script 本体は repo-local `tools/lint/*.js` ではなく、CodexSkill の `review-enforcer/scripts/` に配置した。IbisDuck 側は repo 固有の whitelist / target config / README だけを持つ。
- 対象 Markdown 全体、変更中 Markdown、直接指定した Markdown の 3 mode で対象ファイルを列挙できるようにした。
- textlint は custom rule 最小構成とし、全角スペースと未解決 placeholder marker を検出する。
- 対象 Markdown は repository 内の Markdown 全般とし、`reports/**` や `Tracker/Design/Archive/**` も含める。dependency、build output、vendored upstream reference、明示 opt-out folder だけを除外する。
- 環境構築メモは `tools/lint/README.md` に追加した。導入意図、`npm install`、`npm run lint:md`、個別 script、対象と除外、専用 whitelist 更新手順を記載した。
- whitelist の source of truth は `tools/lint/markdown-whitelist.yaml` とした。初版は既存 Markdown 脚注から収集し、以後は専用 YAML を明示的に編集する。
- whitelist 説明文に含まれる英単語とカタカナ語も `npm run lint:md:whitelist` の対象にした。
- カタカナ語も英単語と同じく whitelist 未登録なら失敗する対象にした。
- whitelist が巨大化した場合に備え、独自 whitelist check は whitelist value ごとの `replace` loop ではなく、単一の combined regular expression で許可語・許可 phrase を mask する実装にした。
- 対象 directory / prefix の source of truth は `tools/lint/markdown-targets.json` に外出しした。
- backtick で囲まれた inline code と fenced code は `cspell` と独自 whitelist check の両方で対象外にした。ただし README と review-enforcer には、lint 回避目的で通常語を backtick / quotation mark へ逃がすことを禁止するレビュー観点を追加した。
- review-enforcer には、`tools/lint/markdown-whitelist.yaml` の変更はユーザー明示レビュー必須であることを追加した。

## Review findings / fixes

- Initial review High: `--files` / `--changed` が `ignoreDirectories` を見ず、dependency / build / cache 配下の Markdown を直接指定できる問題があった。
  - Fix: `list-markdown-targets.js` と `check-markdown-whitelist.js` の直接指定 / changed path filtering を `isIgnored()` に統一し、`ignoredPrefixes` と path segment 単位の `ignoreDirectories` を両方適用した。
  - Evidence: `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files node_modules/cspell/README.md | wc -l` は `0`。
- Initial review Medium: README の direct-file `textlint` pipeline が bare `textlint` を使い、npm script 外では PATH に存在しない問題があった。
  - Fix: README の例を `./node_modules/.bin/textlint` に修正した。
  - Evidence: README 記載と同等の `tools/lint/README.md` direct-file textlint pipeline は success。

## リスク

- 未解決のリスクまたは後続対応:
- textlint は保守しやすい最小構成に留めており、文章表記ルールの強化は後続で段階的に追加する必要がある。
- `tools/lint/markdown-whitelist.yaml` は IbisDuck 側の唯一の whitelist 管理ファイルである。`npm run lint:md:spell` は `entries.term` / `entries.aliases` から一時辞書と ignore pattern を作り、`npm run lint:md:whitelist` は同じ `entries` と `description` を検査する。
- 既存 Markdown には未登録の英単語・カタカナ語が多数残っているため、現時点の `npm run lint:md` は失敗する。これは大量 whitelist を勝手に作らない方針に従った結果であり、通すには各 Markdown の本文を日本語化するか、project 固有語として許可できる理由を `tools/lint/markdown-whitelist.yaml` に追加する必要がある。
- dedicated review gate はこの implementation worker の対象外であり、親 agent 側で別途 review を閉じる必要がある。
