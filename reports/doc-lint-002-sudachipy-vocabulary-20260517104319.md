# DOC-LINT-002 SudachiPy 語彙抽出導入報告

## 目的

既存文書を全走査し、英字語、片仮名語、漢字を含む日本語語彙を頻度付きで抽出する。許可一覧検査の日本語解析も SudachiPy の形態素単位へ寄せ、許可一覧再構築の材料を得る。

## 変更

- `tools/lint/requirements.txt` を追加し、`sudachipy`、`sudachidict_core`、`PyYAML` を固定した。
- `.gitignore` に `.venv/` と `.codex-doc-lint-venv/` を追加した。
- `package.json` の `lint:md:whitelist` を SudachiPy 版の許可一覧検査へ切り替えた。
- 従来の JavaScript 版は `lint:md:whitelist:legacy` として残した。
- `lint:md:vocab` を追加し、SudachiPy 版の語彙抽出を `npm` 実行名から呼べるようにした。
- `tools/lint/README.md` に Python 依存物の準備、語彙抽出、SudachiPy 版許可一覧検査、従来版との比較方法を追記した。
- 利用者の明示確認を受け、`SudachiPy`、`Python`、`JavaScript` を `tools/lint/markdown-whitelist.yaml` に追加した。

## 共有スクリプト

共有実装は `/home/ibis/AI/CodexSkill/skills/review-enforcer/scripts/` に追加した。

- `extract-markdown-vocabulary-sudachi.py`
- `check-markdown-whitelist-sudachi.py`

どちらも `tools/lint/markdown-targets.json` の除外設定、直接指定、変更分指定を読む。

## 検証

- `.codex-doc-lint-venv/bin/python -m py_compile .agents/skills/review-enforcer/scripts/extract-markdown-vocabulary-sudachi.py .agents/skills/review-enforcer/scripts/check-markdown-whitelist-sudachi.py` は成功した。
- `PATH="$PWD/.codex-doc-lint-venv/bin:$PATH" npm run lint:md:vocab -- --files tools/lint/README.md` は成功し、頻度降順の語彙一覧を出力した。
- `PATH="$PWD/.codex-doc-lint-venv/bin:$PATH" npm run lint:md:whitelist -- --files tools/lint/README.md --list-unknown` は失敗した。これは SudachiPy 版が漢字語を形態素として検出し、現行許可一覧が再構築前であるための意図した失敗である。
- 初回レビュー指摘を受け、通常文書を `--stdin` で渡す focused check では入力本文だけを検査するように修正した。
- `PATH="$PWD/.codex-doc-lint-venv/bin:$PATH" printf 'SudachiPy\n' | PATH="$PWD/.codex-doc-lint-venv/bin:$PATH" npm run lint:md:whitelist -- --stdin tools/lint/README.md --list-unknown` は成功した。
- `node .agents/skills/review-enforcer/scripts/list-markdown-targets.js --files tools/lint/README.md --print0 | xargs -0 -r node .agents/skills/review-enforcer/scripts/run-cspell-markdown.js` は成功した。
- `PATH="$PWD/.codex-doc-lint-venv/bin:$PATH" npm run lint:md:text` は成功した。
- `git diff --check` は成功した。

## 注意

SudachiPy 版の許可一覧検査は、従来の英字語と片仮名語だけでなく漢字語も検出する。現行許可一覧はまだ再構築前のため、`npm run lint:md:whitelist` と全体 `npm run lint:md` は失敗する。失敗結果を語彙棚卸しとして使い、最終的な許可語と説明は利用者の明示確認後に `tools/lint/markdown-whitelist.yaml` へ反映する。
