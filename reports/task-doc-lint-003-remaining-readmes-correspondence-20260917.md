# DOC-LINT-003 残る2 README の本文・台帳更新報告

## 対象

- リポジトリ: `ibis-ssl/Duck`
- PR: `#20`
- 作業開始時の current HEAD: `1f93a601780108d79b89faf59cf61e7ea63c83a3`
- 比較元: `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`
- `Tracker/Tracker.CaptureReplay/README.md`
- `Tracker/Tracker.DebugHost/README.md`

最上位 `README.md` は直前の作業で基準62 / 62原出現を対応済みである。本作業では残る2 READMEを処理し、3 README全体を528 / 528原出現まで揃えた。

## CaptureReplay README

基準43原出現を最新本文へ全件対応付けた。内訳は、用語整理で変更対象になった28件と原文同一15件である。既存14変更箇所の採否・理由と、CLI実装の入力解決、遅延分析、設定上書き、出力キーを再確認した。

現在の本文は意味と読みやすさを維持しており、本作業で追加の本文修正は行っていない。最終本文SHA-256は `11f377d24e57be8a825b3286c189c8f027370a7ce2afcfcba36a777ec2f761cb`。

## DebugHost README

基準423原出現を最新本文へ全件対応付けた。内訳は、用語整理で変更対象になった345件と原文同一78件である。既存75変更箇所の判断、後続の初期化対象・観測値の重み修正も含む現在本文を再確認した。

`official tracker packet` と書かれていた10箇所は、実装が送受信する公式形式を曖昧にしないよう、`TrackerPacketGenerator`、`UdpTrackerPacketPublisher`、`TrackerPacketSnapshotLogWriter` 等で使われる `TrackerWrapperPacket` と具体化した。送受信先、CaptureOn条件、保存形式、設定値は変更していない。

最終本文SHA-256は `1a214f35b0c7c8598319c0b3a2d8e0d301a62d1c2f3cafdc7a567d7e11f8be14`。

## 台帳

- `reports/diagnostics/wording-remaining-readmes-ledger-20260917/capture-replay-occurrence-correspondence.json`: 43 / 43件、未解決0
- `reports/diagnostics/wording-remaining-readmes-ledger-20260917/debughost-readme-occurrence-correspondence.json`: 423 / 423件、未解決0
- 各文書の変更箇所対応: `capture-replay-block-correspondence.json`、`debughost-readme-block-correspondence.json`
- `validation.json`: 466 / 466件、両文書とも `pass`
- 直前の最上位README台帳62件とのID集合を合わせ、3 READMEで528 / 528件、一意ID528、未解決0を確認した。

この528件を、人が読む設計書だけを対象とした2,142件の中央台帳へ重複加算しない。また、3 READMEの完了だけで基準4,336件全体の最終台帳完了とは扱わない。

## タスク同期

`Tracker/Design/tasks-status.md` の現在状態を、3 README合計528 / 528件、未解決0へ更新した。4,336件全体の最終台帳、履歴本文との最終照合、独立最終レビューは残件として維持した。

## 検証

DebugHost本文更新後、タスク同期前の `npm run lint:md` と `git diff --check` は終了値0だった。証跡は `reports/diagnostics/wording-remaining-readmes-ledger-20260917/pre-ledger-20260917-153640/`。

タスク同期後の初回検査では、追記した「変更ブロック」の「ブロック」が未承認片仮名語として検出され、`npm run lint:md` は終了値1だった。許可一覧を変更せず「変更箇所」へ直し、失敗ログを `final-sync-20260917-153921/` に保持した。

修正後の `final-sync-r2-20260917-153950/` では、18対象文書の `npm run lint:md`、`git diff --check`、3 README 528件のID集合・本文SHA照合がすべて終了値0となった。cspellは18文書、指摘0件。

製品コード、設定、テストコードを変更していないため、この作業単位ではローカル.NETテストを実行していない。

## 診断artifact workflow

作業終了前に `origin/main` の `.github/workflows/dotnet-test.yml` を再確認した。テスト失敗時にTRX、標準出力、標準エラー、vstest診断ログ、binlogを `artifacts/test-results` に収集し、`actions/upload-artifact@v7` で公開する構成が存在する。今回workflowは変更していない。

## CI

この報告書作成時点では今回の変更をまだpushしていない。push後にGitHubからPRのcurrent HEADを再取得し、そのSHAとworkflow runの `head_sha` が完全一致するrunだけを確認する。別SHAのrunは代用しない。
