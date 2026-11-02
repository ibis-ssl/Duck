# Issue #41 再開計画

- 対象: https://github.com/ibis-ssl/Duck/issues/41 (`ASPIRE-006D`)
- 基準: `design/issue18-aspire-test-orchestration` HEAD `0b86a73ac2782603379ce183a250ee8bbf1d9a17`。このHEADにはPR #74の統合コミット `0139a08b675483637083c48367176fdfc44be60b` が含まれる。
- 原条件: 1回のUI render tickで固定した同じsnapshot pairをSplit / Overlay表示と数値差分が共有し、時刻差を別表示する。
- 方針: Issue本文と現在の実装・対象テストを先に照合し、現HEADで対象テストを実行する。未実装時の失敗確認と必要最小限の実装は別コミットにする。実装不足がなければ追加コードを作らず、現在HEADの検証・レビュー証拠を記録する。
- 統合試験を行い失敗した場合は、テスト結果、stdout/stderr、Aspireと各コンテナの診断ログを保存する。
- レビュー範囲はIssue #41の単一完了条件に限る。未検証・失敗・保留は明記する。
- Sub-issue分割は、独立した約30分の作業単位が見つかった場合に限る。現時点で新しい完了条件は追加しない。

この計画は着手時点の範囲と検証順を記録するものであり、Issue完了やPR承認を意味しない。
