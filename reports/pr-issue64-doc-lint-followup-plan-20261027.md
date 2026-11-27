# Issue #64 remaining acceptance plan

## 1. 背景と既存成果

Issue #64 は `Tracker/Design/Testing/aspire-simulation-test-environment.md` と `Tracker/Design/tasks-status.md` の全文語彙整理を追跡します。PR #65 は 2026-10-10 に `task/pr61-crane-probe-20261005` へマージされ、対象文書の修正と lint 対応を含みます。PR #65 は非既定ブランチ宛てで、Issue #64 は現在 Open / Project Backlog です。PR #65 の成果を再実装する計画ではありません。

この Issue の残件は、PR #65 の成果が現在の Issue 完了条件を満たすかを対象文書と検証記録で照合し、全文 lint の実行結果と独立した自然な日本語レビューを完了記録にまとめることです。この段落の未実施状況は本計画書の作成時点を示します。

## 2. 対象範囲

- PR #65 の変更を現在の対象ブランチへ適用した状態で、両文書の最終内容と Issue #64 の完了条件を対応付ける。
- textlint、CSpell、Sudachi whitelist を両文書全体に対して実行し、コマンド、対象、結果を記録する。
- 機械検査とは分けて、技術的意味・試験条件・コード上の契約を保った自然な日本語かを独立レビューで確認する。
- 残る指摘があれば、既存の PR #65 成果を重ねて直さず、具体的な差分と承認が必要な語彙を切り分ける。

## 3. 完了条件

- 両文書の現在の全文が Issue #64 の内容要件を満たす。
- textlint、CSpell、Sudachi whitelist の全文検査がすべて成功し、実行対象と結果が再確認できる。
- 独立した日本語レビューで自然さと意味保持が確認される。
- whitelist、CSpell 設定、検査範囲、依存関係を許可なく拡大・緩和しない。
- 実施した差分と残件がレビュー可能な記録にまとまり、完了状態を証拠に基づいて判断できる。

## 4. 検証計画

本計画書を追加した時点では対象文書・コード・検査設定を変更せず、文書差分に対する `git diff --check` 以外の Issue 完了検証も行っていませんでした。後続の実装・検証状況は第7節に記録します。

## 5. 未実施と対象外

- PR #65 の変更は再適用していません。対象文書の追加編集と全文 lint は本計画書の作成後に実施し、今回の追補差分は独立レビュー待ちです。
- Issue #31 の完了、親子 Issue の状態変更、Project Status 更新は対象外です。
- ユーザーが承認した語句の追加を除く whitelist 変更、CSpell 設定変更、新しい依存、無関係な文書修正は対象外です。
- この Draft PR は Issue #64 を解決せず、自動 close を意図しません。

## 6. Issue と PR の関係

- 対象: [Issue #64](https://github.com/ibis-ssl/Duck/issues/64)
- 既存成果: [PR #65](https://github.com/ibis-ssl/Duck/pull/65)（merged、別の非既定 base）
- 本計画書を追加した時点のPRは、残る受入記録と独立レビューの計画だけを提示していました。後続の実装・検証状況は第7節に記録します。Issue #64 への参照は非 closing とし、完了条件を削除・短縮しません。

## 7. 実装・検証の追記（2026-11-01）

本計画書を作成した後、PR #95 の既存ブランチで対象二文書の編集と全文検査を進めました。最終追補では、ユーザー承認済みの「ループ」「バック」「コンパイル」の3語だけを既存 whitelist に追加しました。AutoRef は既存 alias があるため重複登録していません。

修正前の全体検査では、textlint と CSpell は exit 0、Sudachi whitelist は exit 1 でした。Sudachi の4件は「ループ」「バック」（`tasks-status.md` 51行目）、`de`（同39行目の未引用起点コミットID `88cb20f2b5cea46eb90bff80ba8d535639625de0`）、コンパイル（`aspire-simulation-test-environment.md` 461行目）です。この失敗記録は修正前の履歴です。起点IDへのインラインコード追加と承認済み3語の登録後、最終の両文書全文検査は textlint、CSpell、Sudachi whitelist の全てが exit 0 となり、CSpell は2ファイル・指摘0件でした。`git diff --check` も exit 0 です。各終了値は実行時の exec_command 戻り値で取得し、一覧を `C:\Users\donabe\AppData\Local\Temp\issue64-approved3-final4-exit-codes.txt` に保存しました。個別 stdout/stderr は `C:\Users\donabe\AppData\Local\Temp\issue64-approved3-final4-{text,spell,whitelist,diffcheck}.{stdout,stderr}.log` にあります。以前の Yomiyasu 実行では、シミュレーション設計書が100点・指摘なし、状態台帳が0点・40件の助言でした（後者は361非空行中306行が箇条書き）。

最終追補の文書・whitelist差分は、同じ独立レビュアーが pass と確認しました。レビュー対象差分 SHA256 は `01C0BB6E6EB5B0F1C62BC3B81F3456B29DB4402569D20C831F044CB24A7EAD80` です。コミット `98a088c40054ad68c45762282dfc34a453c6044e` を PR #95 の既存ブランチへ公開し、remote HEAD の一致を確認しました。Issue #31、Project、PRコメント、PR merge は対象外です。
