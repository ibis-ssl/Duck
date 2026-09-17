# DOC-LINT-003 RuntimeHost 設計 196出現 対応表更新報告

## 対象

- リポジトリ: `ibis-ssl/Duck`
- PR: `#20`
- PR ブランチ: `docs/runtimehost-readme-appsettings`
- 対象文書: `Tracker/Design/RuntimeHost/runtime-host-plan.md`
- 用語整理前: `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`
- 作業開始時の PR HEAD: `2a41cdee22ceffb329083596a34142873453c7db`
- 本文修正コミット: `19555135394db736cb8c5eaded0eeec9417cc8c7`
- 本文 SHA-256: `a237ed17cb59300322787c7decbdd135fd06f541bf6df5663ff750bd4e959ba8`

## 実施内容

RUNTIME-HOST に割り当てられた196原出現IDを全件対応表へ統合した。内訳は変更箇所186件と原文同一10件で、変更箇所は34ブロックに属する。既存の43ブロックの人手照合記録を現行本文へ再投影し、今回本文が変わった9ブロックは現行文を原文と実装へ再照合して判断理由を更新した。未解決は0件である。

本文では、AutoRef を一般的な自動判定へぼかしていた箇所を AutoRef の判定処理へ戻した。`VisionReceiver` の説明は実装と照合し、`MulticastAddress`、`Port`、`InterfaceAddress` の役割を区別した。CaptureOn 中に作成するキャプチャーと diagnostics sample sidecar の関係、追跡スナップショット、公式形式の `TrackerWrapperPacket` の説明も整理した。型名、設定名、実UI名、承認済み表記は維持した。

## 台帳

- `reports/diagnostics/wording-runtime-host-ledger-20260917/runtime-host-occurrence-correspondence.json`: 196 / 196件、未解決0
- `reports/diagnostics/wording-runtime-host-ledger-20260917/runtime-host-block-correspondence.json`: 43ブロック、変更出現を含む34ブロックを対応付け、今回本文が変わった9ブロックを再記録
- `reports/diagnostics/wording-runtime-host-ledger-20260917/validation.json`: 196件、ID重複0、未解決0、結果 `pass`
- `reports/diagnostics/wording-human-design-scope-20260917-1011/scope-occurrences.json`: RuntimeHost 196件を `completed` に同期
- `reports/diagnostics/wording-human-design-scope-20260917-1011/scope-summary.json`: 人が読む設計書の共有集計を2,142 / 2,142件、残り0件に同期

人が読む設計書の中央台帳は完了したが、4,336出現箇所の最終台帳、履歴本文との最終照合、全体の文書検査、独立最終レビューは別工程として残る。

## 検証

- 原出現ID: 196件、重複0、欠落0
- 変更箇所: 186件 / 34ブロック
- 原文同一: 10件
- 対応表未解決: 0
- 対象文書の `textlint`: 終了値0
- 対象文書の `cspell`: 終了値0、1ファイル検査、Issues 0
- 対象文書の許可一覧検査: 終了値0
- 本文修正コミットの `git diff --check`: 終了値0
- 対応表構造検証: 終了値0
- 中央台帳構造検証: 2,142件完了、残り0、RuntimeHost 196 / 196、ハッシュ一致、終了値0
- 進捗同期後の再検査: `runtime-host-plan.md` と `tasks-status.md` の `textlint` / `cspell` / 許可一覧検査 / `git diff --check` はすべて終了値0。証跡は `reports/diagnostics/wording-runtime-host-ledger-20260917/final-sync-20260917-122129/`
- .NET テスト: 文書・台帳・進捗文書のみの変更のため、この作業単位では未実行

初回の対象検査では、本文へ新たに書いた未承認片仮名語「アドレス」を許可一覧検査が検出した。許可一覧へ追加せず、実際の設定名 `MulticastAddress` / `InterfaceAddress` を明記し、日本語側を既存の許可表現へ修正した。初回 `cspell` は専用worktreeに `node_modules` がないため検査器の依存物解決で失敗し、次の実行では検査器がworktree直下の `node_modules/.bin/cspell` を固定参照することを確認した。既存の検証用依存物を一時シンボリックリンクし、実行終了時に削除したうえで最終検査を行い、Issues 0 を確認した。失敗と再実行のログは `reports/diagnostics/wording-runtime-host-ledger-20260917/` に保存した。

## 変更境界

この作業では次を変更していない。

- `tools/lint/markdown-whitelist.yaml`
- prh 規則、文書検査除外、共有検査器
- 製品ソースコード、設定ファイル、テストコード
- RuntimeHost 以外の設計本文
- 4,336出現箇所の全体台帳の完了判定

これは作業担当の自己点検と台帳更新であり、独立最終レビューの合格ではない。

## CI

この報告書作成時点では、報告書・進捗更新を含む最終 PR HEAD がまだ確定していないため、CIは未確認である。最終 push 後に PR の current HEAD SHA を再取得し、その SHA と workflow run の `head_sha` が一致する実行だけを確認する。別SHAの実行結果は代用しない。
