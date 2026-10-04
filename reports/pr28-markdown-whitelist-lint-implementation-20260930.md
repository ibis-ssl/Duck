# PR #28 ホワイトリスト式 Markdown lint 導入報告

日付: 2026-09-30

## 目的

PR #28 で未承認の一般英単語が残った原因を受け、Markdown 文書検査を専用許可一覧方式でリポジトリ単体から実行できる状態にする。

## 変更

- `tools/lint/scripts/` へ文書検査の実行物を取り込み、`.agents/skills` と `xargs` への依存を除去した。
- CodexSkill `origin/main` の長文入力修正版を取り込み、SudachiPy の入力を 48,000 byte 以下へ分割する。
- CSpell は `--no-default-configuration` を使い、標準英語辞書を許可根拠にしない。
- Windows でも `.cmd` を直接起動せず、`textlint` / `cspell` の JavaScript CLI を Node から実行する。
- `npm run lint:md:setup` を追加し、`npm ci`、`.venv` 作成、Python 依存導入を一括化した。
- 必須 Python 依存を `sudachipy==0.6.11`、`sudachidict_core==20260428`、`PyYAML==6.0.3` に限定した。
- ChikkarPy はホワイトリスト検査に不要で、現行 Python では配布物の問題で導入できないため必須依存から外した。
- `tools/lint/README.md` を新しい導入・実行方法へ更新した。

## 検証

Windows / Node 24 / Python 3.14 で確認した。

- `npm run lint:md:setup`: 成功。
- Node スクリプトの構文検査: 成功。
- Python スクリプトの構文検査: 成功。
- `tools/lint/README.md` の textlint / CSpell / 専用許可一覧検査: 成功。
- 未登録語 `endpoint` の検査: 期待どおり失敗。
- 登録済み `RuntimeHost` の検査: 成功。
- `Tracker/Design/tasks-status.md` の長文検査: 入力長例外を起こさず未登録語を列挙。
- `git diff --check`: 成功。

## 現在の文書違反

新しい検査器は PR #28 に残る `endpoint`、`interface`、`decode`、`failure`、`success`、`host`、`network`、`geometry`、`realism`、`artifact`、`resource`、`packet`、`comparison`、`match` などを未登録語として検出する。

これらは `tools/lint/markdown-whitelist.yaml` へ追加していない。許可一覧変更には利用者承認が必要なため、この作業では行わない。

## コミット

- `3c5fecf` `chore: vendor strict markdown lint tooling`

## 状態

ホワイトリスト式 Markdown lint と必須依存はリポジトリ内で完結して実行できる。現在の PR 文書には実際の許可一覧違反が残るため、全体 `npm run lint:md` は成功扱いにしない。
