# 文書検査設定

この作業一式では、利用者または委譲先が通常編集する文書を品質確認に載せるため、最上位に `textlint` と `cspell` を置く。

## 準備

初回または `package-lock.json` 更新後は最上位で次を実行する。

```bash
npm install
```

この検査は `.agents/skills/review-enforcer/scripts/` にある共通処理を使う。`.agents/skills` は記録対象には含めず、手元の記号参照として `~/AI/CodexSkill/skills` を指している必要がある。

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

`npm run lint:md` は、対象文書全体に対して `textlint`、`cspell`、専用許可一覧の強制検査を実行する。`cspell` は `--no-default-configuration` で標準辞書を読まないため、一般英単語も専用許可一覧にない場合は失敗する。

現在の対象文書は次で確認する。

```bash
npm run lint:md:targets
```

変更中の文書だけを素早く確認したい場合は、次のように対象列挙処理を使う。

```bash
npm run lint:md:targets -- --changed
```

対象文書を直接指定したい場合は `--files` を使う。

```bash
node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files README.md Tracker/README.appsettings.md
node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files README.md --print0 | xargs -0 -r ./node_modules/.bin/textlint --config .textlintrc.json --rulesdir .agents/skills/review-enforcer/scripts/textlint-rules
node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files README.md --print0 | xargs -0 -r node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js
node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files README.md
```

## 対象範囲

通常対象は `**/*.md` のうち、利用者が編集対象にする文書全般である。`.agents/skills/review-enforcer/scripts/list-markdown-targets.js` が作業一式内の対象文書を列挙する。最上位文書、`Tracker/Design/**`、`Tracker/**/*.md`、`feedback-points/**` は対象に含める。`reports/**` は暫定的に対象外にする。

対象外の置き場や接頭辞は `tools/lint/markdown-targets.json` に明示する。`.textlintignore` と `cspell.config.jsonc` も同じ対象外方針に揃える。現時点では依存物、生成物、明示的な除外置き場、取り込み済みの外部参照だけを除外する。

- `node_modules/**`: npm 依存物の出力。
- `.git/**`: Git の内部情報。
- `.codex-dotnet-home/**`、`.codex-nuget-packages/**`: 手元の .NET 一時保存領域。
- `**/bin/**`、`**/obj/**`: .NET 構築出力。
- `reports/**`: 調査、点検、引き継ぎ、検証の報告書。現時点では対象外。
- `tools/lint/excluded/**`: 文書検査から明示的に外したい文書を置く除外置き場。通常の文書、報告書、設計文書はここへ移動しない。
- `Tracker/Design/Core/Ref/**`: 複写された参照元と構築木。
- `SslProto/src/external/ssl-game-controller/**`: 取り込み済みの外部作業一式。
- `SslProto/src/external/ssl-simulation-protocol/**`: 取り込み済みの外部通信形式作業一式。

## 許可一覧

許可一覧の正本は `tools/lint/markdown-whitelist.yaml` である。この初版は既存文書の脚注から収集したが、脚注収集処理は残していない。以後は新しい固有語、機能名、略語、外部作業一式名、片仮名語を許可する場合、この設定文書に最小限の項目を明示的に追加する。

`tools/lint/markdown-whitelist.yaml` の追加、変更、削除は、利用者の明示確認を必須とする。委譲先の点検だけで許可一覧更新を完了扱いしてはならない。

```yaml
entries:
  - term: ExampleTerm
    aliases:
      - example-term
      - example term
    description: ExampleTerm を許可する理由と、この作業一式での意味。
```

`term` と `aliases` が許可語になる。`description` は人が意味を確認するための説明であり、`description` 内の英単語と片仮名語も `npm run lint:md:whitelist` の対象になる。

逆引用符で囲んだ行内の識別子と囲み付き符号片は検査対象外である。ただし、これは本物の識別子、命令、文書の場所、画面表示名、明示的な項目名のための例外であり、通常の文章中の英単語や片仮名語を検査から逃がすために逆引用符や引用符で囲むことは禁止する。点検ではこの逃げがないことも確認する。

`npm run lint:md:spell` は `tools/lint/markdown-whitelist.yaml` の `entries.term` と `entries.aliases` から一時辞書と無視条件を作って `cspell` を実行する。許可一覧の管理文書はこの 1 つだけである。`tracker-debug-host` と `tracker debug host` のように連結符付き表記と空白区切り表記の両方を許可する場合は、同じ項目の `aliases` に両方を明示する。

`cspell` は標準英語辞書を使わない。さらに `npm run lint:md:whitelist` が、専用許可一覧にない英単語と片仮名語を追加で失敗させる。既存文書に未登録語がある場合は検査が落ちるため、文章を日本語へ直すか、固有語として許可できる理由を `tools/lint/markdown-whitelist.yaml` に追加する。
