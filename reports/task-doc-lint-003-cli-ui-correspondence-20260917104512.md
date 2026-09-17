# DOC-LINT-003 CLI-UI 724出現 対応表更新報告

## 対象

- PR: `ibis-ssl/Duck#20`
- 文書: `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- 用語整理前: `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`
- 対応表が読解した本文: `4eff5c3733fc5e543f7f96c35e97d1de2e505797`
- 本文 SHA-256: `ecd9419d25abec0c29cf280078c97695c38c32456f68a8d375d5df43a63d3484`

## 実施内容

`scope-occurrences.json` で CLI-UI に割り当てられた724原出現IDを全件対応表へ統合した。内訳は変更ブロックに属する706件と、原文行が同一の18件である。変更706件は80個の出現対象ブロックに属し、既存90ブロックの人手自己点検を現行本文へ再投影した。`4eff5c3` で本文が変わった18ブロックは旧理由の単純流用をせず、現行本文を再読解して理由を追記した。

台帳は各原出現について、原ID・元表記・元位置・元文脈・最終表現・最終位置・最終文脈・判断・理由を保存している。位置対応だけや lint 通過だけを意味保持の根拠にはしていない。

## 成果物

- `reports/diagnostics/wording-cli-ui-ledger-20260917-1045/cli-ui-occurrence-correspondence.json`: 724 / 724件、未解決0
- `reports/diagnostics/wording-cli-ui-ledger-20260917-1045/cli-ui-block-correspondence-r3.json`: 現行90ブロック自己点検、うち18ブロックを `4eff5c3` に対して再記録
- `reports/diagnostics/wording-cli-ui-ledger-20260917-1045/validation.json`: ID集合・件数・最終行範囲・ハッシュの構造検証
- `reports/diagnostics/wording-human-design-scope-20260917-1011/scope-occurrences.json` / `scope-summary.json`: CLI-UI を724 / 724へ更新し、対象範囲全体を857 / 2,142、残り1,285へ更新

## 本文修正との整合

直前の本文修正では、特に tracking 軽量化の誤訳、`Tracker:Receive:InterfaceAddress`、aggregate sourceの `receivedAt` 比較、旧形式欠落、`Tracker Comparison` UI名、再生時点・早送り、重ね表示の表現を修正した。これらを含む18ブロックは今回の r3 記録で現行本文に対して再確認した。

## 検証

- 原出現ID: 724件、重複0、欠落0
- 変更ブロック由来: 706件 / 80ブロック
- 原文同一: 18件
- 対応表未解決: 0
- 構造検証: `validation.json` = `pass`
- textlint: 終了値0
- cspell（共有wrapper、現行PR設定・許可一覧）: 終了値0、1ファイル検査、Issues 0
- 許可一覧検査（現行PR root）: 終了値0
- `git diff --check`: 終了値0
- 検証途中の cspell 絶対パス指定失敗（0 files checked）、SudachiPy不足、誤ったroot参照による許可一覧失敗は成功ログへ上書きせず、同じ診断ディレクトリへ保存した。

## 境界

これは実装担当の自己点検と台帳更新であり、独立最終レビューの合格ではない。ARCHITECTURE 565件、RAW-VISION 524件、RUNTIME-HOST 196件の計1,285件は未完了のままである。PRのmergeは行わない。
