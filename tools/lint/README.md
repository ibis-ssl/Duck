# Markdown lint setup

この repository では、ユーザーまたは agent が通常編集する Markdown を品質ゲートに載せるため、repository root に `textlint` と `cspell` を置く。

## Setup

初回または `package-lock.json` 更新後は repository root で次を実行する。

```bash
npm install
```

この lint は `.agents/skills/review-enforcer/scripts/` にある共通 script を使う。`.agents/skills` は repository には commit せず、local symlink として `~/AI/CodexSkill/skills` を指している必要がある。

通常の検証は次を実行する。

```bash
npm run lint:md
```

個別確認が必要な場合は次を使う。

```bash
npm run lint:md:text
npm run lint:md:spell
npm run lint:md:whitelist
```

`npm run lint:md` は、repository 内の対象 Markdown 全体に対して `textlint`、`cspell`、専用 whitelist 強制チェックを実行する。`cspell` は `--no-default-configuration` で標準辞書を読まないため、一般英単語も専用 whitelist にない場合は失敗する。

現在の対象ファイルは次で確認する。

```bash
npm run lint:md:targets
```

変更中の Markdown だけを素早く確認したい場合は、次のように対象列挙 script を使う。

```bash
npm run lint:md:targets -- --changed
```

対象ファイルを直接指定したい場合は `--files` を使う。

```bash
node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files README.md Tracker/README.appsettings.md
node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files README.md --print0 | xargs -0 -r ./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules
node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files README.md --print0 | xargs -0 -r node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js
node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files README.md
```

## Scope

通常対象は `**/*.md` のうち、ユーザーが編集対象にする Markdown 全般である。`.agents/skills/review-enforcer/scripts/list-markdown-targets.js` が repository 内の対象 Markdown を列挙する。root documents、`reports/**`、`Tracker/Design/**`、`Tracker/**/*.md`、`feedback-points/**` は対象に含める。

対象外 directory / prefix は `tools/lint/markdown-targets.json` に明示する。`.textlintignore` と `cspell.config.jsonc` も同じ対象外方針に揃える。現時点では dependency、generated output、明示 opt-out folder、vendored upstream reference だけを除外する。

- `node_modules/**`: npm dependency output。
- `.git/**`: Git internal metadata。
- `.codex-dotnet-home/**`、`.codex-nuget-packages/**`: local .NET cache。
- `**/bin/**`、`**/obj/**`: .NET build output。
- `tools/lint/excluded/**`: Markdown lint から明示的に外したいファイルを置く opt-out folder。通常の docs / reports / design はここへ移動しない。
- `Tracker/Design/Core/Ref/**`: copied reference source and build tree。
- `SslProto/src/external/ssl-game-controller/**`: vendored upstream project。
- `SslProto/src/external/ssl-simulation-protocol/**`: vendored upstream protocol project。

## Whitelist

whitelist の source of truth は `tools/lint/markdown-whitelist.yaml` である。この初版は既存 Markdown の脚注から収集したが、脚注収集 script は残していない。以後は新しい project 固有語、tooling 名、略語、外部 project 名、カタカナ語を許可する場合、この YAML に最小限の entry を明示的に追加する。

`tools/lint/markdown-whitelist.yaml` の追加・変更・削除は、ユーザーの明示レビューを必須とする。agent review だけで whitelist 更新を完了扱いしてはならない。

```yaml
entries:
  - term: ExampleTerm
    aliases:
      - example-term
      - example term
    description: ExampleTerm を許可する理由と、この repository での意味。
```

`term` と `aliases` が whitelist 語になる。`description` は人が意味を確認するための説明であり、`description` 内の英単語とカタカナ語も `npm run lint:md:whitelist` の対象になる。

backtick で囲んだ inline code と fenced code は lint 対象外である。ただし、これは code、identifier、command、file path、UI label、明示的な項目名のための例外であり、通常の文章中の英単語・カタカナ語を lint から逃がすために backtick や quotation mark で囲むことは禁止する。review ではこの逃げがないことも確認する。

`npm run lint:md:spell` は `tools/lint/markdown-whitelist.yaml` の `entries.term` と `entries.aliases` から一時辞書と ignore pattern を作って `cspell` を実行する。whitelist の管理ファイルはこの YAML 1 ファイルだけである。`tracker-debug-host` と `tracker debug host` のように hyphen 付き表記と space 区切り表記の両方を許可する場合は、同じ entry の `aliases` に両方を明示する。

`cspell` は標準英語辞書を使わない。さらに `npm run lint:md:whitelist` が、専用 whitelist にない英単語とカタカナ語を追加で失敗させる。既存 Markdown に未登録語がある場合は lint が落ちるため、文章を日本語へ直すか、project 固有語として許可できる理由を `tools/lint/markdown-whitelist.yaml` に追加する。
