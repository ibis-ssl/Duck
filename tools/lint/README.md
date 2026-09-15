# 文書検査設定

このリポジトリでは、ユーザーまたはエージェントが編集する文書を `textlint` と `cspell` で検査する。`textlint` は独自規則に加えて `textlint-rule-prh` を使い、`tools/lint/prh.yml` の辞書で表記揺れを検出する。

## 準備

初回、`package-lock.json` 更新後、または `tools/lint/requirements.txt` 更新後はリポジトリの最上位で次を実行する。標準の `npm run lint:md` は SudachiPy 版の許可一覧検査も実行するため、`npm install` だけでは足りない。

```bash
npm install
python3 -m venv .venv
. .venv/bin/activate
PIP_NO_BUILD_ISOLATION=1 python3 -m pip install -r tools/lint/requirements.txt
```

`ChikkarPy` は現在の `Python` 環境ではビルド時の環境分離を有効にした通常のインストールに失敗することがあるため、文書検査用環境では `PIP_NO_BUILD_ISOLATION=1` を付ける。`tools/lint/requirements.txt` には、その前提で必要なビルド用の補助パッケージも含めている。

この検査は `.agents/skills/review-enforcer/scripts/` にある共通処理を使う。`.agents/skills` は `ln -s` で作成するリンクとし、`~/AI/CodexSkill/skills` を参照させる。このリンクは Git の管理対象に含めない。

通常の検証は次を実行する。

```bash
npm run lint:md
```

個別確認が必要な場合は次を使う。

```bash
npm run lint:md:text
npm run lint:md:spell
npm run lint:md:whitelist
npm run lint:md:vocab
```

`npm run lint:md` は、対象文書全体に対して `textlint`、`cspell`、専用許可一覧の強制検査を実行する。`textlint` は `tools/lint/prh.yml` の `prh` 辞書も読み込む。`cspell` は `--no-default-configuration` で標準辞書を読まないため、一般英単語も専用許可一覧にない場合は失敗する。

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
npm run lint:md:whitelist -- --files README.md
```

SudachiPy と ChikkarPy を使って既存文書から語彙と同義語候補を抽出する場合は次を使う。出力は `TSV` が既定で、必要なら `--format json` も指定できる。

```bash
npm run lint:md:vocab
npm run lint:md:vocab -- --files README.md tools/lint/README.md
npm run lint:md:vocab -- --format json
npm run lint:md:vocab -- --synonyms none
```

`--synonyms none` は、ChikkarPy の同義語候補だけを外して SudachiPy の読み、正規形、品詞、頻度を確認したい場合に使う。

SudachiPy 版の許可一覧検査は `npm run lint:md:whitelist` から実行する。従来の JavaScript 版と比較したい場合は `npm run lint:md:whitelist:legacy` を使う。

```bash
npm run lint:md:whitelist -- --files tools/lint/README.md
npm run lint:md:whitelist:legacy -- --files tools/lint/README.md
```

## 対象範囲

通常対象は `**/*.md` のうち、利用者が編集対象にする文書全般である。`.agents/skills/review-enforcer/scripts/list-markdown-targets.js` がリポジトリ内の対象文書を列挙する。最上位にある文書、`Tracker/Design/**`、`Tracker/**/*.md`、`feedback-points/**` は対象に含める。`reports/**` は暫定的に対象外にする。

検査対象から除外するディレクトリとファイルパスの接頭辞は `tools/lint/markdown-targets.json` に明示する。`.textlintignore` と `cspell.config.jsonc` も同じ対象外方針に揃える。現時点では依存物、生成物、明示的な除外用ディレクトリ、取り込み済みの外部参照だけを除外する。

- `node_modules/**`: npm 依存パッケージの保存先。
- `.git/**`: Git の内部情報。
- `.codex-dotnet-home/**`、`.codex-nuget-packages/**`: 手元の .NET 一時保存領域。
- `**/bin/**`、`**/obj/**`: .NET のビルド出力。
- `reports/**`: 調査、レビュー、引き継ぎ、検証の報告書。現時点では対象外。
- `tools/lint/excluded/**`: 文書検査から明示的に外したい文書を置く除外用ディレクトリ。通常の文書、報告書、設計文書はここへ移動しない。
- `Tracker/Design/Core/Ref/**`: コピーした参照元のソースコードとビルド用ディレクトリ。
- `SslProto/src/external/ssl-game-controller/**`: 取り込み済みの外部リポジトリ。
- `SslProto/src/external/ssl-simulation-protocol/**`: 通信形式を定義した外部リポジトリの取り込み先。

## 許可一覧

許可一覧の正本は `tools/lint/markdown-whitelist.yaml` である。初版の一覧は既存文書の脚注から収集したが、脚注収集処理は残していない。以後は新しい固有語、機能名、略語、外部リポジトリ名、片仮名語を許可する場合、この設定文書に最小限の項目を明示的に追加する。

`tools/lint/markdown-whitelist.yaml` の追加、変更、削除は、利用者の明示確認を必須とする。エージェントのレビューだけで許可一覧更新を完了扱いしてはならない。

```yaml
entries:
  - term: ExampleTerm
    aliases:
      - example-term
      - example term
    description: ExampleTerm を許可する理由と、このリポジトリでの意味。
```

`term` と `aliases` が許可語になる。`description` は人が意味を確認するための説明であり、`description` 内の英単語と片仮名語も `npm run lint:md:whitelist` の対象になる。

`TrackerFrame` のような行内の識別子や、複数行のソースコードを囲んだ部分は検査対象外である。ただし、これは本物の識別子、コマンド、ファイルパス、画面表示名、明示的な項目名のための例外であり、通常の文章中の英単語や片仮名語を検査から逃がすためにこれらの記法や引用符で囲むことは禁止する。レビューでは、検査の回避がないことも確認する。

`npm run lint:md:spell` は `tools/lint/markdown-whitelist.yaml` の `entries.term` と `entries.aliases` から一時辞書と無視条件を作って `cspell` を実行する。許可一覧の設定ファイルはこの 1 つだけである。`tracker-debug-host` と `tracker debug host` のように `-` でつないだ表記と空白で区切った表記の両方を許可する場合は、同じ項目の `aliases` に両方を明示する。

`cspell` は標準英語辞書を使わない。さらに `npm run lint:md:whitelist` が、専用許可一覧にない英単語と片仮名語を追加で失敗させる。既存文書に未登録語がある場合は検査が落ちるため、文章を日本語へ直すか、固有語として許可できる理由を `tools/lint/markdown-whitelist.yaml` に追加する。

SudachiPy 版の抽出と検査では、日本語を文字種だけではなく形態素として扱う。漢字語、片仮名語、混在語を `surface`、正規形、読み、品詞、候補集合、頻度、出現元で集計し、許可一覧再構築の候補にする。英字語は従来どおり専用の厳しい抽出規則で扱う。ChikkarPy が返す同義語候補は、候補集合を作るための補助情報として `synonyms` に出力する。SudachiPy の正規形や読みが同じ語、または ChikkarPy の同義語候補に入った語は近くに出せるが、`namespace`、`ネームスペース`、`名前空間` のような英日意味対応は自動確定しない。最終的に許可する語と説明は利用者の明示確認を受けて `tools/lint/markdown-whitelist.yaml` に反映する。

## 表記揺れ辞書

表記揺れ辞書の正本は `tools/lint/prh.yml` である。`textlint-rule-prh` はこの `YAML` 文書を読み、期待する表記と誤表記の組み合わせを検出する。初期状態では具体的な表記統一規則を登録しない。

```yaml
version: 1
rules:
  - expected: 期待する表記
    pattern:
      - 避けたい表記
```

`prh` 規則は文書全体の表記方針を変えるため、追加や変更は利用者の明示確認を受けてから行う。`textlint-rule-prh` は `textlint` の Markdown 解析を通るため、リンクなどの Markdown 構造を素朴な文字列検索より安全に扱える。
