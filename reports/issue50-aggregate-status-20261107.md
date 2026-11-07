# ASPIRE-006G 統合PRの初期記録

日付: 2026-11-07

## 目的

このブランチは、Issue #50 `ASPIRE-006G` の集約PRを作り、子Issue #51〜#54 の変更と証跡を統合するための親ターゲットである。親設計ブランチは `design/issue18-aspire-test-orchestration`。本PRは draft とし、main へはマージしない。

## Issue の関係

- #50 は #51〜#54 を集約し、`ASPIRE-MATCH-001`〜`005` の Linux 実通信証跡をまとめる親Issue。
- #51 は #50 の正式な子Issueで、`ASPIRE-006G1` と `ASPIRE-MATCH-001/002` を扱う。
- #52〜#54 はそれぞれ G2〜G4 の作業を追跡する。今回の変更・検証には含めない。

## #51 の状況

- 既存の GitHub Actions run `42357169977` は Linux の readiness 中に Game Controller state-store と recorder 出力への `permission denied` で失敗し、実パケット受入へ進まなかった。
- #51 の Linux bind group 権限と 11003 の実受信 source-tuple capture の実装を追加した。
- 独立した focused Green: Python 10 tests 成功、Linux .NET 113 passed / 2 Windows-only skipped / 0 failed。
- これらはコード検証であり、Linux Docker コンテナの実受入ではない。#51 の Linux MATCH-001/002 と実パケット証跡は、同一コミットを対象にした CI が完了するまで未完了とする。
- 実測した `address:port` tuple が一つであることと、その送信元プロセス / コンテナを特定できたことは別に記録する。host network のため、後者は未確認のまま扱う。
- G1 の完了判定は MATCH-001/002 と Linux AppHost 証跡による。match-controller が `maximum_time` で終わっても、全試合の成功は主張しない。

## 残作業

1. #51 PR をこの親ブランチに向け、Issue #50/#51 と子PR/親PRのリンクを双方から確認する。
2. 通常レビュー、同一ソースの Linux コンテナ受入、独立最終レビューを完了する。
3. #51 の子PRをこの親ブランチへマージする。親PRは main へマージせず、#52〜#54 の証跡を待つ。
4. #50 は G1〜G4 と MATCH-001〜005 が揃うまで open のままにする。

## 判断

この記録は統合作業開始時点の状態である。Focused Green を Linux 実受入の代わりに扱わず、CI や後続Issueの未実施を完了扱いしない。
