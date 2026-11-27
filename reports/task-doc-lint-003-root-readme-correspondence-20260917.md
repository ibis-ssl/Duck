# DOC-LINT-003 最上位 README 62出現 対応表更新報告

## 対象

- リポジトリ: `ibis-ssl/Duck`
- PR: `#20`
- PR ブランチ: `docs/runtimehost-readme-appsettings`
- 対象文書: `README.md`
- 用語整理前: `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`
- 作業開始時の PR HEAD: `5dca21f66bf6b88f44ec98ca1e93da291c95ecd1`
- README SHA-256: `82c4d69d29c56a61e39f642b0977a727f291b26625b02d9caf755ba1e3a26a9d`

## 実施内容

最上位 `README.md` を現在の RuntimeHost 実装と直近の設計照合結果へ合わせた。`Tracker/Tracker.RuntimeHost` をリポジトリ構成へ追加し、画面なしで SSL-Vision を受信してトラッカーを周期実行し、公式形式の `TrackerWrapperPacket` を送信する責務を記載した。

SSL-Vision の送信元が必要となる条件は DebugHost だけに限定せず、RuntimeHost または DebugHost が SSL-Vision を受信する場合とした。本文2箇所の `official tracker packet` は、実装と RuntimeHost 設計で使っている公式形式の `TrackerWrapperPacket` へ具体化した。設定値、起動コマンド、送受信先は変更していない。

`Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj` の存在と `Tracker/Tracker.RuntimeHost/appsettings.json` を確認し、README に追加した責務と既存の `sim` 設定説明が実体と矛盾しないことを確認した。

## 台帳

`reports/diagnostics/wording-occurrence-audit-20260916/occurrences-start.json` から `README.md` の基準62原出現IDを抽出し、最新本文へ対応付けた。

- `reports/diagnostics/wording-root-readme-ledger-20260917/root-readme-occurrence-correspondence.json`: 62 / 62件、未解決0
- `reports/diagnostics/wording-root-readme-ledger-20260917/validation.json`: 62件、ID重複0、未解決0、結果 `pass`
- 最終本文の行・列・表現と台帳の対応を62件すべて再検証し、終了値0

今回の62件は2,142件の「人が読む設計書」中央台帳へ混在させない。4,336出現箇所の最終台帳への統合、履歴本文との最終照合、独立最終レビューは別の残作業であり、今回の62件だけで完了扱いにしない。

## タスク同期

`Tracker/Design/tasks-status.md` の `DOC-LINT-003` を更新し、最上位 README の62 / 62件完了、18対象文書の現在の文書検査成功、残る4,336件の統合・履歴照合・独立最終レビュー・最終HEAD一致CIを区別した。

承認済みの直接引用例外は上流の追跡事項として残す。ただし `feedback-points/feedback-points.md` は利用者の明示指定で文書検査対象外となっているため、現在のDuck側18文書の検査失敗理由にはしていない。

## 検証

README更新後、タスク同期前の18対象文書に対する `npm run lint:md` は終了値0、`git diff --check` も終了値0だった。ログは `reports/diagnostics/wording-root-readme-ledger-20260917/pre-task-sync-20260917-1409/` に保存した。

タスク同期後の初回 `npm run lint:md` は終了値123だった。追記文中のコード記法でない `current HEAD` と、日本語に空白なしで隣接した `SSL-Vision受信` が cspell に検出された。許可一覧は変更せず、既存の記法へ合わせて修正した。失敗ログは `reports/diagnostics/wording-root-readme-ledger-20260917/final-sync-20260917-1410/` に保持した。

修正後の `final-sync-r2-20260917-1411/` では、18対象文書の `npm run lint:md`、`git diff --check`、README台帳62件の本文SHA・行・列・表現照合がすべて終了値0となった。cspellは18文書を検査し、指摘0件だった。

.NET テストは、製品コード・設定・テストコードを変更していないため、この作業単位では実行していない。

## 失敗診断 workflow

作業開始時に現在の `main` の `.github/workflows/dotnet-test.yml` を確認した。テスト失敗時に TRX、標準出力、標準エラー、vstest診断ログ、binlog と診断情報を `artifacts/test-results` へ保存し、`actions/upload-artifact` で公開する構成が存在する。PR HEADツリーに同workflowが存在しないことと、base側のPR実行で利用されるworkflowであることは区別する。このREADME作業ではworkflowを変更していない。

## 変更境界

今回変更したのは、最上位 `README.md`、README 62件の対応台帳と検証ログ、`Tracker/Design/tasks-status.md`、本報告書だけである。許可一覧、文書検査規則・除外、製品コード、設定、テスト、設計書中央台帳は変更していない。PRのmergeは行わない。

## CI

この報告書作成時点では、今回の変更を含む最終コミットをまだpushしていない。push後にGitHubからPRの `current HEAD` を再取得し、そのSHAとworkflow runの `head_sha` が完全一致する実行だけを確認する。一致するrunがなければCI未実施として扱い、別SHAのrunは代用しない。
