# Issue #64 remaining acceptance plan

## 1. 背景と既存成果

Issue #64 は `Tracker/Design/Testing/aspire-simulation-test-environment.md` と `Tracker/Design/tasks-status.md` の全文語彙整理を追跡します。PR #65 は 2026-10-10 に `task/pr61-crane-probe-20261005` へマージされ、対象文書の修正と lint 対応を含みます。PR #65 は非既定ブランチ宛てで、Issue #64 は現在 Open / Project Backlog です。PR #65 の成果を再実装する計画ではありません。

この Issue の残件は、PR #65 の成果が現在の Issue 完了条件を満たすかを対象文書と検証記録で照合し、全文 lint の実行結果と独立した自然な日本語レビューを完了記録にまとめることです。現時点ではこの計画 PR 内でその照合・レビュー・検証は実施していません。

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

この PR は計画文書のみで、対象文書・コード・検査設定を変更しません。文書差分に対する `git diff --check` 以外の Issue 完了検証は行っていません。後続作業ではリポジトリで定義された全文 textlint、CSpell、Sudachi whitelist と独立した日本語レビューを実施し、各結果を正確な対象内容に結び付けて記録します。

## 5. 未実施と対象外

- PR #65 の変更再適用、文書の追加編集、lint 実行、独立レビューは未実施です。
- Issue #31 の完了、親子 Issue の状態変更、Project Status 更新は対象外です。
- whitelist や CSpell 設定の変更、新しい依存、無関係な文書修正は対象外です。
- この Draft PR は Issue #64 を解決せず、自動 close を意図しません。

## 6. Issue と PR の関係

- 対象: [Issue #64](https://github.com/ibis-ssl/Duck/issues/64)
- 既存成果: [PR #65](https://github.com/ibis-ssl/Duck/pull/65)（merged、別の非既定 base）
- この PR は残る受入記録と独立レビューの計画だけを提示します。Issue #64 への参照は非 closing とし、完了条件を削除・短縮しません。
