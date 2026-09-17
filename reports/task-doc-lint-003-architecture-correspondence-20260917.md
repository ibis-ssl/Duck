# DOC-LINT-003 構成設計 565出現 対応表更新報告

## 対象

- PR: `ibis-ssl/Duck#20`
- 文書: `Tracker/Design/Core/tracker-architecture-plan.md`
- 用語整理前: `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`
- 対応表が読解した本文: `5a72850d25fa934bcb9d3e4c587bb8d88688fd59`
- 本文 SHA-256: `5df79b7bd607baa39b7fad0f9a5abc3e5fb39b9c1a78786a88427048b50b21a5`

## 実施内容

ARCHITECTURE に割り当てられた565原出現IDを全件対応表へ統合した。内訳は変更箇所496件と原文同一69件である。変更496件は131ブロックに属し、既存247ブロックの人手照合記録を現行本文へ再投影した。現行本文と旧記録が異なる26ブロックは、公開済みの出力順・通知順修正と今回の自然さ・意味修正を含むため、現行本文を再読解して補足理由を記録した。

今回、`tracking 軽量化` が `進捗管理の軽量化` に変化していた箇所を `追跡処理の軽量化` へ戻した。また、位置/向きの filter を曖昧な状態推定へ一般化せず `Kalman filter` と明示し、設定外出し等の不自然な表現も意味を変えずに整理した。

## 台帳

- `reports/diagnostics/wording-architecture-ledger-20260917/architecture-occurrence-correspondence.json`: 565 / 565件、未解決0
- `reports/diagnostics/wording-architecture-ledger-20260917/architecture-block-correspondence-r2.json`: 247ブロック、現行本文との差があった26ブロックを再記録
- `reports/diagnostics/wording-architecture-ledger-20260917/validation.json`: ID集合・件数・最終行範囲の構造検証
- 共有集計: 1,422 / 2,142件完了、残り720件

## 検証

- 原出現ID: 565件、重複0、欠落0
- 変更箇所: 496件 / 131ブロック
- 原文同一: 69件
- 対応表未解決: 0
- textlint: 終了値0
- cspell: 終了値0、1ファイル検査、Issues 0
- 許可一覧検査: 終了値0
- `git diff --check`: 終了値0
- 構造検証: 565件、重複0、欠落0、未解決0、`validation.json` = `pass`

## 境界

これは作業担当の自己点検と台帳更新であり、独立最終レビューの合格ではない。RAW-VISION 524件、RUNTIME-HOST 196件の計720件は未完了のままである。`Tracker/Design/tasks-status.md` は変更していない。
