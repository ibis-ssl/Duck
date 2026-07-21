# Markdown lint 最終レビュー

## タスク

- Markdown lint 対象から `reports/**` を除外し、非 `reports/**` の Markdown が lint を通るようにした変更をレビューする。

## レビュー対象

- `tools/lint/markdown-whitelist.yaml`
- `tools/lint/README.md`
- `cspell.config.jsonc`
- 非 `reports/**` の Markdown 修正
- lint 対象外の report 作成物

## レビュー観点

- `reports/**` が lint 対象から外れていること。
- whitelist の説明文が lint 対象として通ること。
- 製品名は `Tracker.DebugHost` などの複合語で登録され、`Tracker` / `Kalman` の一般語は `トラッカー` / `カルマン` へ寄せられていること。
- `temporary-doc-lint-terms` が一時許可であることが明記され、将来削減できる形になっていること。
- Markdown link address、パス、命令を whitelist 登録でごまかしていないこと。
- `npm run lint:md` が成功していること。

## 検証結果

- `npm run lint:md -- --no-progress`: 成功
- `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files __no_markdown_input__.md --list-unknown`: 成功
- `git diff --check`: 成功

## レビュー結果

- 指摘なし。
- `reports/**` は `tools/lint/markdown-targets.json` の `ignoreDirectories`、`.textlintignore`、`cspell.config.jsonc` の `ignorePaths` に揃って含まれている。`node .agents/skills/review-enforcer/scripts/list-markdown-targets.js | rg '^reports/'` も出力なし。
- whitelist の `description` は `check-markdown-whitelist.js --files __no_markdown_input__.md --list-unknown` と `npm run lint:md -- --no-progress` の両方で検査対象として通過した。
- `Tracker.DebugHost`、`Tracker.RuntimeHost`、`Tracker.CaptureReplay` は複合語として登録され、`DebugHost` / `RuntimeHost` / `CaptureReplay` の単体 alias は見当たらない。一般語の `Tracker` / `Kalman` は `トラッカー` / `カルマン` に寄せられている。
- `temporary-doc-lint-terms` は `reports/doc-lint-whitelist-approved-temporary-20260517084346.md` で一時許可語として記録され、本文の日本語化や設計語整理後に削る前提の暫定登録として扱われている。
- URL 断片、Markdown link address、実パス文字列、実行命令文字列を個別に whitelist へ登録して lint 対象化を回避している差分は確認できない。`command-line` や `path` などの一般語は `temporary-doc-lint-terms` 配下の一時許可として残るが、上記 report で将来削減対象として明記済み。
- 非 `reports/**` の Markdown 差分は、主に一般英語やカタカナ語を日本語へ寄せる修正、製品名・型名・設定名を識別子として残す修正、Markdown link address を本文許可語として扱わない修正であり、今回の Markdown lint 対応範囲と整合している。

## 指摘事項

- なし。

## 対応

- 追加対応不要。
- 確認コマンド:
  - `git diff --stat`: 23 files changed, 1988 insertions(+), 1009 deletions(-)。
  - `git diff -- tools/lint/markdown-whitelist.yaml cspell.config.jsonc tools/lint/README.md`: 対象外設定、whitelist 本体、説明文の変更を確認。
  - `git diff -- <markdown files>`: 非 `reports/**` の Markdown 修正範囲を確認。
  - `npm run lint:md -- --no-progress`: 成功。対象 22 Markdown、CSpell issues 0。
  - `node .agents/skills/review-enforcer/scripts/check-markdown-whitelist.js --files __no_markdown_input__.md --list-unknown`: 成功。
  - `git diff --check`: 成功。

## 最終判断

- Pass。Markdown lint 対応の最終レビューとして blocking findings はない。
