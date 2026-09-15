# 全指摘の対応一覧

対象はPR #20、調査基準は `697fd5d2986d4c7bb774c096bdc35aa383083978`。ローカルの作業一覧草案を含む入力を保存し、再取得した通常のcspell出力435件が保存済み出力と一致することを確認した。レポートは通常対象外。

## 読み方

Cは上限を広げたcspell診断1,619件、Wは長文分割による許可一覧の補助診断22,490件。同じ問題を重複して数える。全24,109診断記録を、正規化語と対応内容が同じ1,650行へ整理した。英語・カタカナ・その他の表では、各行の件数は全出現、場所は代表1件。一般漢字語は全1,053語を共通方針とともに列挙した。語ごとの件数と場所はローカルの japanese.txt に保持する。日本語のW診断は正式な全体検査成功の代用ではない。

| 一覧 | 行数 | 内容 |
| --- | ---: | --- |
| [英語 a〜m](english-a-m.txt) | 165 | 説明文の日本語化。複数案は意味を文脈で選ぶ。 |
| [英語 n〜z](english-n-z.txt) | 150 | 同上。コード上の識別子は改名しない。 |
| [カタカナ](katakana.txt) | 140 | 自然な表記の意味付き許可候補。未登録。 |
| [一般漢字語](japanese-index.txt) | 1,053 | 本文維持と検査契約の見直し案。 |
| [その他の対応](other-actions.txt) | 142 | 識別子、脚注ID、技術表記、誤分割、日本語の推敲。 |

上の行数は表記違い・対応違いをまとめた行数で、異なる不具合の件数ではない。その他の対応では先頭列に分類を付けている。

## 分類

D01は英語の説明文を日本語化。D02は実在の識別子・値を保持。D03は日本語の推敲。D04はリンク先を保持して表示名を日本語化。A01は技術表記の許可候補。K01はカタカナの意味付き許可候補。J01は一般漢字語の本文維持。J02は漢字・カタカナ混在語を維持し、カタカナ部分の検査を残す。M01は誤分割の断片を登録しない。S01は脚注IDだけを構造として扱い、脚注本文を検査。S02は機械用の依存一覧を説明文と分離する案。

草案の通常英語分類には作業IDが混入していたため、作業IDはD02へ修正した。また、漢字・カタカナ混在語22件をJ02へ分け、脚注参照の位置を再照合した。診断記録は削除していない。

許可候補や一般漢字語の検査契約変更は未適用。候補の提示を承認や修正完了とは扱わない。通常の日本語へ寄せる場合も、読みやすさと仕様・過去の検証結果の維持を優先する。

## 代表箇所のファイル番号

- F01: `AGENTS.md`
- F02: `README.md`
- F03: `Tracker/Design/Archive/Core/phases-status.md`
- F04: `Tracker/Design/Archive/Core/tasks-status.md`
- F05: `Tracker/Design/Archive/DebugHost/phases-status.md`
- F06: `Tracker/Design/Archive/DebugHost/tasks-status.md`
- F07: `Tracker/Design/Core/tracker-architecture-plan.md`
- F08: `Tracker/Design/Core/tracker-core-engine-detail-design.md`
- F09: `Tracker/Design/Core/tracker-history-000-038.md`
- F10: `Tracker/Design/Core/tracker-test-maintainability-detail-design.md`
- F11: `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- F12: `Tracker/Design/DebugHost/debug-host-maintainability-design.md`
- F13: `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- F14: `Tracker/Design/RuntimeHost/runtime-host-plan.md`
- F15: `Tracker/Design/phases-status.md`
- F16: `Tracker/Design/tasks-status.md`
- F17: `Tracker/Tracker.CaptureReplay/README.md`
- F18: `Tracker/Tracker.DebugHost/README.md`
- F19: `feedback-points/feedback-points.md`
- F20: `tools/lint/README.md`
- F21: `tools/lint/markdown-whitelist.yaml`
- F22: `tools/lint/requirements.txt`

F21の行番号は個々の説明文字列内の行で、括弧内は許可一覧の項目名。YAMLファイル自体の行番号ではない。

元入力、全検出箇所、再分類済みの24,109件とコマンド出力はRDC端末の `/home/ibis/.local/share/duck-doc-lint/lint-plan-publication-20260915T164549` と、元診断の `lint-disposition-20260915T154427` に保持する。ここに公開する一覧は全語の対応表であり、全検出箇所を1件ずつ並べた原本とは区別する。

## 原文との再照合による補足

作業ID、ファイル名の区切りを示す dot、実在するボールを指す genuine、並列表現、カタカナの正規化形の判断を[補足](clarifications.md)に記録した。対応表の dot / ドット / genuine / appsettings の案にも反映した。

## 検査処理の修正計画

[検査器の問題と修正後の確認条件](checker-plan.md)に、長文処理、脚注本文の見逃し、語の部分一致、日本語隣接、対象列挙の対応をまとめた。一般漢字語の検査契約と許可候補の登録は未適用である。通常のMarkdown検査は未通過のまま。
