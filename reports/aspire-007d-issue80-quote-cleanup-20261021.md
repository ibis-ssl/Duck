# Issue #80 設計文書の一般語引用整理

## 対象

- Issue #80 / PR #83、ブランチ task/aspire-docker-desktop-496。
- 編集前の完全 HEAD: 074be4a8aa0ece890759fa49095ffc2a2ad0dec4。
- 設計変更対象は Tracker/Design/Testing/aspire-simulation-test-environment.md のみ。報告記録はこのレポートへ分離し、履歴ファイル、tasks-status.md、第三者レビュアーの指摘や受入記録は変更しない。既存の本人作成報告コメントはレビュー後に更新する。

## 問題と修正

見出しの Aspire、冒頭説明の Windows / Docker Desktop / WSL / UDP、本文の DCP / Vision / API など、一般的な技術用語までバッククォートで囲っていた。一般語の囲みを76箇所除去し、許可一覧にない WSL、Vision、TCP、Issue は文意を保つ日本語へ言い換えた。

実コード識別子、実資源名、ファイルパス、メソッド名、コマンドと引数、設定キー、URL、IP/ポート、状態値、commit SHA は行内コードのまま保持した。設計上の方式、受入条件、安全境界、未実装・未検証の記述は変更していない。旧履歴文書はバイト単位で保存するため編集していない。

## 検証

対象ファイルは Tracker/Design/Testing/aspire-simulation-test-environment.md。

- git diff --check: 成功。
- textlint: node tools/lint/scripts/run-markdown-targets.js text --files Tracker/Design/Testing/aspire-simulation-test-environment.md、exit 0。
- CSpell: 既存の run-cspell-markdown.js と cspell.config.jsonc、markdown-whitelist.yaml を使用。WSL共有パスでは対象0件となるため、対象文書を一時ミラーへバイト一致で複製して実行。1 file checked、0 issues、exit 0。元と複製の SHA-256 は 04F6A43A5F46803506567F4D3CD4CF59ADA9F812C3F13382267D1BB6C047143B で一致。
- Sudachi: python tools/lint/scripts/check-markdown-whitelist-sudachi.py --files Tracker/Design/Testing/aspire-simulation-test-environment.md、exit 0。
- Yomiyasu: 100/100、指摘なし。
- Build: 文書だけの変更なので実施していない。製品コード・依存・設定に変更はない。

## 未完了事項

Windows AppHost / DCP の実動作、Docker Desktop 内の UDP / マルチキャスト、外部接続先契約、起動停止と所有者限定 cleanup の受入は引き続き未検証。今回の文書整理は実装・実機受入・mergeを承認するものではない。