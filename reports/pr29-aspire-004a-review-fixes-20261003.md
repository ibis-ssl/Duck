# PR #29 ASPIRE-004A レビュー指摘対応報告

## 対象

PR #29 の独立レビューで示された I29-004A-REV-001〜003 に対応した。
指摘対応コミットは `f2e9a4f`。

## I29-004A-REV-001 設計文書表記

`Tracker/Design/Testing/aspire-004a-workflow.md` と `Tracker/Design/tasks-status.md` の ASPIRE-004A 追加部分を修正した。

- 一般語として残っていた英語・片仮名表記を自然な日本語へ変更した。
- `Game Controller`、`AppHost`、`Crane`、設定値、CLI 引数、クラス名など、正式名称・識別子だけをコード表記で保持した。
- `fixture`、`host network`、`producer`、`publish`、`active command`、`focused test`、`adapter`、`client` などの説明語を本文から除いた。
- 作業名を囲っていた不要な引用符を削除した。
- `tools/lint/markdown-whitelist.yaml` は変更していない。

## I29-004A-REV-002 差分検査

`Tracker/Tracker.Tests/RefereeDriverFixtureTests.cs` の末尾余分空行を削除した。
PR 差分全体で同じ問題があった `reports/pr29-aspire-004a-review-20260930.md` の末尾余分空行も削除した。

次の2コマンドは終了値 0。

```text
git diff --check
git diff design/issue18-aspire-test-orchestration --check
```

## I29-004A-REV-003 Markdown 文書検査

PR #30 `chore: link review-enforcer skill for Markdown lint` の検査器を一時作業ツリーへ重ね、PR #29 本体へ lint 基盤を重複追加せず検証した。
利用した PR #30 HEAD は `eeac918f2601f6e8c985c388e2915ec903bbd11f`。さらに PR #30 の CI が固定している `CodexSkill` commit `583a9594d8157fc101dff3c6b3338238809d9779` へ symlink 参照を張り替えて再確認した。

`Tracker/Design/Testing/aspire-004a-workflow.md` は次の3検査すべて成功した。

```text
npm run lint:md:text -- --files Tracker/Design/Testing/aspire-004a-workflow.md
npm run lint:md:spell -- --files Tracker/Design/Testing/aspire-004a-workflow.md
npm run lint:md:whitelist -- --files Tracker/Design/Testing/aspire-004a-workflow.md
```

CSpell 指摘 0、厳格許可一覧違反 0。

`Tracker/Design/tasks-status.md` 全体には PR #28 ベース側の既存未許可表記が残るため、PR ベースとの差分から ASPIRE-004A の今回追加5行だけを UTF-8 Markdown として抽出し、同じ3検査を実行した。
追加5行も textlint、CSpell、厳格許可一覧検査のすべてに成功し、CSpell 指摘 0、厳格許可一覧違反 0。
許可一覧の追加・変更は行っていない。

PR #30 の検査器を重ねた状態で全文 `npm run lint:md` も実行し、`tasks-status.md`、`aspire-simulation-test-environment.md`、`tracker-comparison-debug-design.md` など PR #28 ベース側の既存表記で失敗することを確認した。今回追加差分の成功結果とは分離して記録する。

## 対象テスト

RDMCP セッションの PATH から `dotnet` が外れていたため、SDK 10.0.401 の実体を短縮パスで指定して個別実行した。

- `AppHostApplicationModelTests`: 5 / 5 成功
- `RefereeDriverFixtureTests`: 5 / 5 成功
- `RefereeDriverNetworkAdapterTests`: 4 / 4 成功

合計 14 / 14 成功。

複合 filter の一括起動は RDMCP 側で `Process start failed` となったため、3群を個別実行した。各テスト自体は成功している。

## ファイル形式

今回修正した4ファイルは UTF-8 BOM 付きであることを確認した。

## CI

本報告を push 後、PR #29 current HEAD と workflow run の `headSha` が完全一致する `.NET tests` だけを最終 CI 判定対象とする。
別 SHA の run は代用しない。merge は行わない。
