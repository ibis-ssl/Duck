# DOC-LINT-003 feedback-points 文書検査除外

## 結論

利用者の明示指定により `feedback-points/feedback-points.md` をMarkdown文書検査の恒久対象外へ変更した。

この文書は今回の設計書照合スコープ外だったが、変更前はlint設定上の対象には残っていた。したがって「対象外ファイルでlintが失敗した」という過去の報告は、作業スコープ外とlint設定上の除外を混同した不正確な表現だった。

## 変更

除外方針を次の3設定で一致させた。

- `tools/lint/markdown-targets.json` の `ignoredPrefixes`
- `.textlintignore`
- `cspell.config.jsonc` の `ignorePaths`

`tools/lint/README.md` に利用者の明示指定による除外であることを記録した。許可一覧・検査規則・設計本文は変更していない。

## 変更前の確認

current HEAD `0b29c23f7b55749d4182c3f4c117e51749eb2858` では対象列挙が19文書で、`feedback-points/feedback-points.md` を含んでいた。
変更前の全体 `npm run lint:md` は終了値123で、cspellが直接引用中の `exec` 1件を報告した。許可一覧検査も `exec` 1件と `サブエージェント` 2件を報告した。

## 変更後の検証

2026-09-17 13:05 JST にRDC上で検証した。

- 対象列挙: 18文書。`feedback-points/feedback-points.md` は含まれない。
- `npm run lint:md`: 終了値0。
- textlint: 通過。
- cspell: 18文書、指摘0。
- 許可一覧検査: 通過。
- `git diff --check`: 通過。
- 3設定すべてに完全一致する除外パスを確認。

初回の変更後検証ではREADMEに新しく書いた片仮名語が許可一覧違反となり終了値1だった。この失敗は保持し、許可一覧を変更せず既存の自然な表現へ直した後に全体lintが終了値0となった。

## 対象外

今回の変更では設計書5文書、製品コード、用語許可一覧、引用本文を変更していない。PRのmergeも行わない。
