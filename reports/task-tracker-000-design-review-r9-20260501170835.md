# Sub-agent実行レポート

## タスク
- 目的: `TRACKER-000` の設計差分について、r8 指摘への対応で新たな設計欠陥や未定義動作が残っていないかを prompt-only でレビューする
- タスク種別: 設計書レビュー

## sub-agentを使う理由
- 理由: `tool/exec` を使わず、与えられた差分とコンテキストだけを根拠に独立したレビュー結果を固定するため

## 対象範囲
- 対象: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` の提示差分、および r8 指摘 1-3 への今回の設計対応

## 対象外
- 対象外: 実装コード確認、既存レポート本文の再読、差分に含まれない周辺設計の再調査、テスト実行

## 実行コマンド
- 実行コマンド:
  - prompt-only sub-agent review

## 対象ファイル
- 変更または確認したファイル:
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `/home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-r8-20260501170354.md`
  - `/home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md`

## 指摘事項
- 指摘要約または「指摘なし」:
  - High: `TrackerProfileSwitchRequest` が `resolved base settings revision または snapshot` を保持する契約のままだと、revision だけを持つ実装が合法になり、`Update` 実行時に外部設定保存領域を再参照する余地が残る。これは r8 の「request immutability が有効設定全体に及ばない」問題を再導入する。少なくとも request 自体に immutable な resolved settings snapshot 全体を必須搭載するか、revision だけで完全再現できる保存領域と保持期間を設計上で必須化する必要がある。参照: `tracker-architecture-plan.md` diff around lines 225-233, 550-580
  - Medium: packet 未到着時の request drain が「control-only `Update` を即時呼び出してよい」という任意規則に留まっており、実装によっては pending request を次の raw packet まで無期限に残せる。今回の設計では `ProfileSwitched` を受けるまで publisher 配信先や active profile 表示を切り替えないため、camera idle 中に profile/override apply が停滞する liveness 欠陥になる。control-only `Update` の発火条件を MUST として固定するか、別の即時 drain 契約を明示する必要がある。参照: `tracker-architecture-plan.md` diff around lines 309-317, 341-353, 550-566

## 結果
- 結果: 2 件の指摘あり。現状のままでは request immutability と reconfigure liveness の契約がなお不十分で、設計確定には追加修正が必要

## リスク
- 未解決のリスクまたは後続対応:
  - request payload の不変性が曖昧なままだと、pending/in-flight 中に設定保存領域が変わった場合に別内容が適用される
  - camera input が止まっている時間帯に profile 切替や override apply が UI 上だけ pending のまま残り、外部 state 切替が進まない実装差が入りうる
