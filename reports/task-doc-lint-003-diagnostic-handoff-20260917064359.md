# DOC-LINT-003 診断設計の引き継ぎ・自己点検

## 対象と状態

- 作業担当による文書修正。独立最終レビューではない。
- 作業開始時の公開HEAD: `982c760b3c7ac8722669a4b1de05fcce704ebbb0`。PR本文の手書きHEADは使わずGitHub APIとRDC上のgitを照合した。
- 未コミットの診断設計と監査資料46ファイル、未完成の採否表を外部の専用退避先へ保存した。詳細は `diagnostics/wording-occurrence-audit-20260916/handoff-start-verification-20260917.json`。
- 診断設計の90変更ブロックを原文・現在本文と照合し、採否表と段落対応を完成した。先頭60件も現在本文と再照合し、残り30件を実際に読んで追記した。
- 残る5文書、全4,336出現箇所の最終台帳、履歴本文の最終照合は未完了。全件確認済みとも、DOC-LINT-003完了とも扱わない。

## 本文修正

`Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md` の継承差分を保持した。source sampleとキャプチャー全体、render snapshotと追跡フレーム番号、capture metadataとsource metadata、保存済みalignmentと旧形式の推定、通常再生と早送りの区別を原文と照合した。条件・対象外・数量・旧更新要求の破棄・任意要件・後続作業の依存を保持した。

追加で、APIの扱いとUUID衝突ケースという二つの確認事項が、APIとUUID自体の衝突に読める構文を訂正した。重複する「受信処理のネットワーク処理」、不自然な倍率ボタンの説明、選択肢一覧のUI名も修正した。許可一覧・aliases・description・prh・検査除外・製品コードは変更していない。

原文の代表選択規則にある「同じtracked frame timestamp」の条件は、実装のalignment索引の並べ替えには見当たらない。`TrackerDiagnosticsComparisonViewStateReader.cs:1375-1387` の時刻差、記録番号、Ordinal順は確認したが、設計と実装全体の一致確認には代用しない。今回、既存の条件は追加・削除せず、原文の意味を維持した。仕様一致の確認は別の未解決事項として残す。

## 証拠

監査資料は `diagnostics/wording-occurrence-audit-20260916/` に保存する。

- `decisions-doc-11.json`: 90ブロックの採否と理由。
- `diagnostic-design-decisions-complete-20260917.json`: 継承した60件を含む完成した採否表。
- `diagnostic-block-correspondence-20260917.json`: 原文から最終本文の段落位置への手動対応。機械diffの位置ずれを補正した。
- `edits-diagnostic-resume-20260917-0640.json`: 今回追加した7変更と内容ハッシュ。
- `validation-diagnostic-resume-20260917-0639.json` と同名単位のログtar: 対象3検査・差分検査・全体lint。
- `diagnostic-resume-poststages-20260917-064130.json` とログtar: 後段を含む全体3検査と依存版。

基準許可一覧は301項目、現在の許可一覧は306項目。後者のSHA-256は `4a9f9b6fa1c7fdcdfc7b6f3b114d8969aaf02b6e986996bb5b59b46bcf9ca654`。固定保存した共有検査器のファイルハッシュはchecker-manifestの全件と一致した。保存元として記録された版は `279b8da9ffe954f78fd8461555be429f1bdcc100` であり、固定保存先自体はgit作業ツリーではない。

## 検証結果

診断設計のtextlint、cspell、whitelist、git diff --checkは終了値0。新しい本文ハッシュは `b3cb9628a72654f2b85a8187496e07eb63db264c997ed4855d833f5fe0e0c894`。

全対象19文書の `npm run lint:md` は終了値123。全体textlintは0、cspellは引用中のexec 1件で123、後段のwhitelistを個別実行するとexec 1件・サブエージェント2件で1。違反はすべて `feedback-points/feedback-points.md` の保存すべき2引用内であり、この時点では引用外の違反は検出されなかった。未実行の後段を成功扱いしていない。

2引用は用語整理前の `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4` の元発言と一致した。引用例外の方針は利用者承認済みだが、この固定検査器には機能がない。CodexSkill issue86は確認時open、コメント0件。機能の実装済みとは判断しない。キャプチャーだけでキャプチャも許可される判定問題も上流作業であり、Duck側へ短縮語を追加して回避しない。

## CIとartifact

開始HEADと一致する.NET testsのrun `35107694283` はsuccess。jobでは失敗診断の収集とartifactアップロードがskippedだった。Markdown lintの成功証拠ではない。取得した現在のPR merge refは `d8dd8ddcfb9d9eb2ead98e5ac64ce0057de97ee5` で、main `d9ca3eef62cc644a37adb8b643cc1cea7ad4a171` と開始HEADを親に持つ。workflow定義はmerge refから確認し、対象HEADそのものの定義とは記録しない。実行当時のworkflow SHAとの完全一致は未確認。

確認したworkflowは.NETテスト失敗時にTRX、stdout、stderr、終了値、vstest診断、binlog、環境情報とソースを保存する。成功時のartifactおよびMarkdown用CIはない。ローカルの文書検証は成否を問わずログを保存した。HEAD更新後は新HEADに一致するrunを別に確認する。

## 次の作業

構成設計、raw vision viewer設計、DebugHost README、作業状況、工程状況の本文・全出現箇所を確認する。Archive 4文書と原文保存版の対応も再確認する。既知の共有検査器問題を理由にDuck本文の確認を止めない。

新しい用語・検査設定の承認依頼は現時点ではない。完了条件は変更せず、独立最終レビューは未実施。mergeは行わない。

## 進捗追記後の再検証

進捗文書への最初の追記では、差分の件数を表す「ブロック」が未登録として検出された。許可一覧は変更せず、「差分90件」として件数の意味を明確にした。このときの失敗を `validation-diagnostic-progress-20260917-0643.json` に保持する。

修正後の `validation-diagnostic-progress-r2-20260917-0644.json` では、診断設計と進捗文書の対象textlint・cspell・whitelist、および差分検査はすべて終了値0。全対象の `npm run lint:md` は引用中のexecによって終了値123のままである。全体成功とは扱わない。
