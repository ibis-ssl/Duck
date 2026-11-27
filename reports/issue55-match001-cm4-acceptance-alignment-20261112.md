# MATCH-001 CM4 経由への整合報告

## 対象

- Repository: `ibis-ssl/Duck`
- Branch: `task/issue55-aspire-007a`
- Base: `design/issue18-aspire-test-orchestration`
- 作業開始時 HEAD: `464b5c0a2d58e1ee92d667d68c80352976615217`
- Issue / PR: [#55](https://github.com/ibis-ssl/Duck/issues/55) / [#123](https://github.com/ibis-ssl/Duck/pull/123)
- 承認: 本人の「cm4経由に揃えて下さい」。

## 変更

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`: MATCH-001 に `cm4-sim` を追加し、Crane から Simulator への経路を CM4 simulator 経由と明記。
- `Tracker/Design/Testing/aspire-simulation-test-environment-history.md`: 2026-11-12 の承認と改訂範囲を追記。旧 #28 固定設計は当時の履歴として保持。
- `Tracker/Design/tasks-status.md`: MATCH-001 関連の起動構成と依存を `cm4-sim` に整合。
- Issue #55 本文: 元本文を保持したまま、承認済み MATCH-001 改訂を追記して読み戻し確認済み。

変更範囲は MATCH-001 の CM4 資源構成・通信経路に限定。MATCH-002〜005、NET-001〜009、開始・終了・結果・ログ保存など他の条件は削除、緩和、追加していない。旧設計文書の記録も書き換えていない。

## 再利用した実行証拠

Linux match run [43133171207](https://github.com/ibis-ssl/Duck/actions/runs/43133171207)、HEAD `464b5c0a2d58e1ee92d667d68c80352976615217` で `cm4-sim` の起動、715-byte mode 4 入力から715-byte mode 3 出力への CM4 経路、同一実行中の両チームの active motion を確認した。この証拠を MATCH-001 の CM4 経由設計に再利用した。

同 run は15分の上限で停止し、`fullMatchPassed=false`。原因記録はボール接近判定（Yellow #1、Blue #3）。したがって、この run は試合全体の成功を証明しない。他の受入項目も合格扱いにしない。MATCH-005 の終了点・記録保存 gate の結果は試合全体の合否と区別する。

## 検証

- `git diff --check`: 成功（exit 0）。既存設計ファイルの LF→CRLF に関する Git 警告は出たが、差分検査は成功。
- 対象 Markdown の列挙: 成功（exit 0）。既存設定では `reports/**` が対象外。
- textlint: 実行不可、`textlint` module 不足。
- spell checker: 実行不可、Node `yaml` module 不足。
- whitelist checker: 実行不可、Python `yaml` module 不足。
- 依存取得・環境変更は行っていない。build、テスト、CI、Docker、追加 run はこの文書改訂では実施していない。

## 状態と残件

設計・Issue の MATCH-001 改訂は反映済み。Issue #55 と PR #123 は OPEN、PR は Draft。Markdown checker は依存不足で未完了であり、受入全体も未完了。既存 run は MATCH-001 の CM4 経路の実行証拠だが、#55 全条件の成功を示すものではない。PR #123 の本文も承認済み改訂と既存証拠に合わせて更新し、読み戻して確認済み。

既存の未コミット RefereeDriver 関連変更と他の未追跡ファイルは、この改訂の対象外として保持する。設計・状態表・本報告だけを一つの文書変更単位にまとめ、コードや無関係な作業ファイルを含めない。
