# DOC-LINT-003 診断設計の参照範囲と進捗の追加修正

## 対象

2026-09-17の作業担当による追加確認。技術親は `99929bcf90c58aea0b28d7356c42652b6c87c90a`、作業ブランチは `work/wording-verification-20260917-0758`、公開先は `docs/runtimehost-readme-appsettings`。独立最終レビューではなく、文書の意味と検証の自己点検である。製品コード・共有検査器・用語許可一覧・検査除外は変更していない。

## D11-B019の追加判断

前回の[90件の再確認](task-doc-lint-003-diagnostic-recheck-20260917081257.md)で保留した相対位置の説明について、原文、関連する保存・再生の説明、現行の対応付けデータ型を調べた。

原文の `session-relative offset` は、直前の説明で同じCaptureOnの記録単位へ対応付ける参照の選択肢として挙げられている。「保存記録内の相対位置」では1件の保存記録の内部へ参照範囲を狭めて読めるため、**「そのキャプチャー内の相対位置」**へ修正した。保存順の位置番号か相対位置かを選ぶ原文の関係も保ち、読点を整理した。

原文はoffsetの単位を明記していない。現行第2版の `TrackerSnapshotAlignmentRecord` には `RenderFrameNumber` とUTCの `RenderReceivedAt` があるが、それを初期設計のoffset代替案の単位の証明には使わない。時間差・バイト位置のどちらかを推測して補足せず、原文の未規定事項として区別した。**参照範囲の修正と、単位の仕様確定は同じではない。**

- [原文と修正・判断・未確認点](diagnostics/wording-verification-20260917-0758/diagnostic-offset-scope-followup.json)
- [現在の本文へ更新した90件の対応表](diagnostics/wording-verification-20260917-0758/diagnostic-semantic-block-correspondence-r2.json)

診断設計のSHA-256は `416fd7d0692a324164b631e480e510f65bcda6052452b802db066950c75b1b3a`。329行のまま。残る89変更ブロックの本文は前回の確認時から変わっておらず、その読解理由を継承している。周辺文脈と本文ハッシュは現在の内容から再取得した。原文の未規定点を解消した、全4,336出現箇所の最終照合が済んだ、という意味ではない。

`Tracker/Design/tasks-status.md` と `Tracker/Design/phases-status.md` には追加修正と本報告への参照を反映した。完了条件は変えず、対応中の状態を保持した。元の検証・承認・過去の完了判断は上書きしていない。両文書とも行数は変更していない。

## 検証

今回の修正後、引き継ぎ対象の6文書を明示して同じ検査単位で再実行した。診断設計に加え、構成設計、検出情報の表示設計、DebugHost README、進捗2文書を対象とする。これは6文書の機械検証であり、他担当の全出現箇所台帳の完成を保証するものではない。

| 検証 | 結果 |
| --- | --- |
| 6文書のtextlint / cspell / whitelist | 各終了値0 |
| `git diff --check` | 終了値0 |
| 全19文書の `npm run lint:md` | 終了値123。textlintを通過した後、cspellが引用内のexec 1件で停止 |
| 後段の `npm run lint:md:whitelist` を個別実行 | 終了値1。引用内のexec 1件、サブエージェント2件 |
| 検証中の全対象本文ハッシュ | 変更なし |
| 保存版検査器のファイルハッシュ | 初回確認時と全件一致 |
| 利用者2発言を含む文書のハッシュ | 原文一致を検証した時点と一致 |

[6文書の検証記録](diagnostics/wording-occurrence-audit-20260916/validation-verification-six-docs-20260917-0823.json)と同名のログアーカイブにコマンド・stdout・stderr・終了値を保存した。[後段の個別実行](diagnostics/wording-verification-20260917-0758/six-docs-later-stage-validation.json)には全対象のハッシュ、依存物の再照合、ログアーカイブとメンバーのハッシュを含めた。使用したPython・Node.js・各依存版と設定は[初回環境記録](diagnostics/wording-verification-20260917-0758/validation-environment-initial.json)から追える。引用例外を導入した検証ではない。

## 公開状態とCI

前回の3点の修正は `99929bcf90c58aea0b28d7356c42652b6c87c90a` としてcommit/push済み。PR本文と簡易報告コメントもGitHub connectorで更新した。PRの実際のHEADとrunのhead_shaが同じ [.NET tests / 35161773324](https://github.com/ibis-ssl/Duck/actions/runs/35161773324) は成功している。job 105013926099のテスト実行は成功、失敗診断の収集・公開はスキップされた。[一致確認記録](diagnostics/wording-verification-20260917-0758/ci-published-diagnostic-fixes.json)を保存した。

今回の相対位置と進捗文書の追加修正は、そのCIには含まれない。本報告を含む公開前の状態は `commit_pending` / `push_pending` / `ci_wait_pending` とする。新しい公開HEADのCIは、push後にPR APIからHEADを再取得し、そのSHAに一致するrunのみで確認する。古い成功や.NETテストをMarkdown lintの成功へ読み替えない。

台帳の全出現箇所対応、履歴本文の最終整合、PR差分と対象範囲の確認、独立最終レビューは継続中。相対位置の単位は原文にも定義がなく、今回の表記修正で新しい仕様に固定していない。共有検査器側の引用機能は別作業で、承認は済んでいる。追加の用語登録や検査除外を必要とする変更はなく、今回利用者へ新たな承認を求める項目はない。mergeは行わない。
