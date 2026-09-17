# DOC-LINT-003 raw vision 表示設計 524出現 対応表更新報告

## 対象

- PR: `ibis-ssl/Duck#20`
- 文書: `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- 用語整理前: `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`
- 対応表が読解した本文: `185d22c9e79663991c71e9b8bbe474c1d99688fb`
- 本文 SHA-256: `c197f26a47ff9194cc4dfa881ffe8380000f4fe43dd8fd913d60fc3963e2b11c`

## 実施内容

RAW-VISION に割り当てられた524原出現IDを全件対応表へ統合した。内訳は変更箇所504件と原文同一20件である。変更504件は45ブロックに属し、既存85ブロックの人手照合記録を現行本文へ再投影した。現行本文と旧記録が異なる9ブロックは、公開済みの意味修正と今回の自然さ・意味修正を含むため、現行本文を再読解して理由を更新した。

今回の本文修正では、singleton の状態保存先、画面遷移、カメラ別の latest-frame 表示、フィールド形状の補完描画、`VisionRenderOptions`、cursor の hover state、UUID単位の代表スナップショット、`MultiTrackerManager` の更新イベント、分割表示・重ね表示、`latest-before snapshot`、画面遷移メニューの表現を整理した。実UI名、型名、設定名、既存の許可語は維持した。

## 台帳

- `reports/diagnostics/wording-raw-vision-ledger-20260917/raw-vision-occurrence-correspondence.json`: 524 / 524件、未解決0
- `reports/diagnostics/wording-raw-vision-ledger-20260917/raw-vision-block-correspondence-r2.json`: 85ブロック、現行本文との差があった9ブロックを再記録
- `reports/diagnostics/wording-raw-vision-ledger-20260917/validation.json`: ID集合・件数・最終行範囲・focused lint の構造検証

## 検証

- 原出現ID: 524件、重複0、欠落0
- 変更箇所: 504件 / 45ブロック
- 原文同一: 20件
- 対応表未解決: 0
- textlint: 終了値0
- cspell: 終了値0、1ファイル検査、Issues 0
- 許可一覧検査: 終了値0
- `git diff --check`: 終了値0
- 構造検証: 524件、重複0、欠落0、未解決0、`validation.json` = `pass`
- .NETテスト: 文書のみの変更のため、この作業単位では未実行

検証途中に `アドレス`、`インターフェース`、`ポート`、`バインド` を使う表現を試したが、未承認片仮名語として許可一覧検査が失敗した。失敗ログは `failed-unapproved-katakana-probe/` に保存し、許可一覧・prh・検査除外を変更せず、本文を検査通過済みの表現へ戻した。

## 境界

これは作業担当の自己点検と台帳更新であり、独立最終レビューの合格ではない。中央台帳へ本作業を反映すると、人が読む設計書の対象は1,946 / 2,142件完了となり、残りは `Tracker/Design/RuntimeHost/runtime-host-plan.md` の196件である。PR全体の完了判定、全体lint、独立最終レビューは別工程とする。
