# PR #28 ASPIRE-003 独立レビュー

日付: 2026-09-30
レビュー対象 HEAD: `65ff0bc7032b614d75776eca480cb87fa3ea4239`

## 対象

`ASPIRE-003A` の RuntimeHost SSL-Vision 受信診断、`ASPIRE-003B` の Simulator 資源、Linux `ASPIRE-NET-002`、TDD 証跡、CI、変更された設計台帳を確認した。実装 report は文体レビュー対象外とし、事実関係と差分品質だけを確認した。

## 結論

要修正。003 の受信診断・Simulator 構成・Linux パケット受信は確認できたが、設計台帳の内容・表現、テスト API、差分検査、PR 本文の整合性に指摘がある。

## 指摘

### R1 要修正: 設計台帳の現在状態が自己矛盾している

`Tracker/Design/tasks-status.md:10` は「後続の実装は未着手」と記載する一方、同じ文書の 41〜45 行では `ASPIRE-003A/B` 完了と Linux 受入完了を記録している。現在状態を 003 完了後の内容へ同期する必要がある。

### R2 要修正: 003 追加行の日本語が英語一般語に偏っている

`Tracker/Design/tasks-status.md:41-45` では `endpoint / interface / decode / commit / failure / success / focused test / host network / geometry / realism / run / packet flow / artifact / network / resource / packet / comparison / match` が日本語文中に連続する。

識別子や CLI 引数ではない一般語は、接続先、ネットワークインターフェース、デコード、コミット、失敗、成功、対象テスト、実行、パケット、アーティファクト、ネットワーク、リソース、比較モード、対戦モードなどへ寄せる。geometry / realism は一般語として書くより `-g` / `--realism` の実引数名を示す方が意味が明確である。

今回追加した 41〜45 行では、識別子・SHA・パス以外を過剰にコード引用して lint を回避する記述は確認しなかった。

### R3 要修正: 003 追加テストが Aspire の obsolete API を使用している

`Tracker/Tracker.Tests/AppHostApplicationModelTests.cs:38,67,94` の `GetArgumentValuesAsync` / `GetEnvironmentVariableValuesAsync` は Aspire 13.5.4 で `CS0618` を出し、`ExecutionConfigurationBuilder` の使用を要求している。

current HEAD の GitHub Actions と Windows ローカル実行の両方で同じ 3 警告を再現した。テストは成功するが、新規差分として obsolete API を追加しているため移行が必要である。

### R4 要修正: 実装差分の diff check が失敗する

`git diff --check adc1b3a..65ff0bc` は `reports/pr28-aspire-003-implementation-20260930.md:205: new blank line at EOF.` を報告する。実装 report の「git diff --check は成功した」という記録とも一致しない。文体はレビュー対象外だが、末尾空行は差分品質として修正が必要である。

### R5 要修正: PR 本文の Final HEAD / CI 記録が古い

PR #28 本文は Final HEAD を `f34ea9fe...`、CI run を `36346741241` と記載したままである。レビュー時の PR current HEAD は `65ff0bc7032b614d75776eca480cb87fa3ea4239` であり、同一 SHA の workflow_dispatch run は `36627447524` である。PR 本文を現状へ同期する必要がある。

## 検証

- Windows focused/application-model test: 7 / 7 成功。
- current HEAD と一致する run `36627447524`: `.NET tests` 336 / 336 成功、`ASPIRE-NET-002 Linux packet flow` 成功。
- current HEAD の成功 artifact `aspire-net-002-36627447524-1`: `VisionPacketsReceivedTotal` は 0 → 187、3941 ms、delta 187。19 ファイルを保存。
- TDD 赤/緑: `4245400...` failure → `efdcd79...` success、`2cbab11...` failure → `067be55...` success を、それぞれ同一 head SHA の run で確認。
- `npm run lint:md` は Windows の `xargs` 不在で停止。個別 textlint も checkout に `.agents/.../textlint-rules` がないため実行不能。機械的な Markdown lint 成功とは扱わない。

## 判定

003 の主要機能について current HEAD のテストと Linux 実パケット受信は成功している。R1〜R5 を修正後に再レビューする。
