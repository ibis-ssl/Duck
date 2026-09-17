# DOC-LINT-003 raw vision 表示設計 524出現 対応表更新報告

## 対象

- PR: `ibis-ssl/Duck#20`
- 文書: `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
- 用語整理前: `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`
- 対応表が読解した本文: `b4711f00d1a9eb45dd8ff6c908737840daa2a5da`
- 本文 SHA-256: `4ef6884f15ffc9224522920ba39fee8e8b983ec2ed85a6cb17d435120ec5a641`

## 実施内容

RAW-VISION に割り当てられた524原出現IDを全件対応表へ統合した。内訳は変更箇所504件と原文同一20件である。変更504件は45ブロックに属し、既存85ブロックの人手照合記録を現行本文へ再投影した。現行本文と旧記録が異なる13ブロックは、公開済みの意味修正と今回の自然さ・意味修正を含むため、現行本文を再読解して理由を更新した。

本文では、単一インスタンスで共有する状態保存先、画面遷移、カメラ別の最新検出情報表示、フィールド形状の補完描画、`VisionRenderOptions`、カーソル位置の条件、UUID単位の代表スナップショット、`MultiTrackerManager` の更新イベント、分割表示・重ね表示、`latest-before snapshot`、画面遷移メニュー、ネットワーク受信の説明を整理した。実UI名、型名、設定名、既存の許可語は維持した。

## 台帳

- `reports/diagnostics/wording-raw-vision-ledger-20260917/raw-vision-occurrence-correspondence.json`: 524 / 524件、未解決0
- `reports/diagnostics/wording-raw-vision-ledger-20260917/raw-vision-block-correspondence-r2.json`: 85ブロック、現行本文との差があった13ブロックを再記録
- `reports/diagnostics/wording-raw-vision-ledger-20260917/validation.json`: ID集合・件数・最終行範囲・focused lint の構造検証
- 共有集計: 1,946 / 2,142件完了、残り196件

共有集計の残件は `Tracker/Design/RuntimeHost/runtime-host-plan.md` の196件だけである。RAW-VISION の524件は `scope-occurrences.json` と `scope-summary.json` に完了として反映した。

## 検証

- 原出現ID: 524件、重複0、欠落0
- 変更箇所: 504件 / 45ブロック
- 原文同一: 20件
- 対応表未解決: 0
- textlint: 終了値0
- cspell: 終了値0、1ファイル検査、Issues 0
- 許可一覧検査: 終了値0
- `git diff --check`: 終了値0
- 中央台帳同期後の再検査: `raw-vision-viewer-plan.md` と `tasks-status.md` の textlint / cspell / 許可一覧検査 / `git diff --check` はすべて終了値0
- 構造検証: 524件、重複0、欠落0、未解決0、`validation.json` = `pass`。中央台帳も1,946 / 2,142件、残り196件で整合
- .NETテスト: 文書のみの変更のため、この作業単位では未実行

検証途中に `アドレス`、`インターフェース`、`ポート`、`バインド` を単独語として使う表現を試したが、未承認片仮名語として許可一覧検査が失敗した。失敗ログは `failed-unapproved-katakana-probe/` に保存した。許可一覧・prh・検査除外は変更せず、既存の許可表現で技術的意味を保つ文へ修正し、再検証で全4検査の終了値0を確認した。

中央台帳同期後の初回再検査では、`tasks-status.md` に追記した `textlint` / `cspell` がコード記法でなかったため cspell 2件が失敗し、system Python では SudachiPy が見つからず許可一覧検査を実行できなかった。この失敗は `final-sync-20260917-115742/` に保存した。表記をコード記法へ修正し、既存の検証用 Python 環境で再実行した `final-sync-20260917-115837/` では、textlint / cspell / 許可一覧検査 / `git diff --check` / 中央台帳構造検証がすべて終了値0となった。

## 境界

これは作業担当の自己点検と台帳更新であり、独立最終レビューの合格ではない。PR全体の完了判定、全体lint、独立最終レビューは別工程とする。この作業では `Tracker/Design/RuntimeHost/runtime-host-plan.md` の本文を変更していない。
