# Sub-agent実行レポート

## タスク

- 目的: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` の profile 切替 0-frame / no-op / control-only 契約について、r7 指摘反映後の残指摘有無を確認する
- タスク種別: 設計書レビュー

## sub-agentを使う理由

- 理由: `review-enforcer` に従い、設計修正後の独立レビュー結果を task 単位で記録するため

## 対象範囲

- 対象: `TRACKER-000` の設計書差分、r7 レビュー指摘、関連調査メモ

## 対象外

- 対象外: 実装コード変更、tracking files 更新、PR 作成

## 実行コマンド

- 実行コマンド:
  - prompt-only sub-agent review

## 対象ファイル

- 変更または確認したファイル:
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `/home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-r7-20260501165941.md`
  - `/home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md`

## 指摘事項

- `High` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:551`
  - `current applied` と同値な request を常に no-op にすると、pending / in-flight に積まれた別 snapshot を打ち消せず、ユーザーの取消操作を失う。
- `Medium` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:230,538`
  - `TrackerProfileSwitchRequest` が profile 名だけでは、同名 profile の内容変更時に request immutability が有効設定全体へ及ばない。resolved settings revision または snapshot が必要。
- `Medium` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:269,314`
  - `GeometryReset` の 0-frame 例外は許可されたが、`TrackedSnapshotStore` / UI をどこまで clear するかの具体規則がなく、旧 geometry 世代表示が残る余地がある。

## 結果

- 結果:
  - 3 件の指摘あり。
  - request の duplicate 判定は `current applied` ではなく `desired target snapshot` 基準へ切り替える必要があると判明した。

## リスク

- 未解決のリスクまたは後続対応:
  - current/pending/in-flight の相対関係を誤ると、latest-wins のつもりでも実際には取消不能な pending request が残る。
  - request が resolved settings snapshot を持たないと、同名 profile の更新と dedupe 判定がずれる。
  - `GeometryReset` の downstream clear 規則が曖昧だと、profile switch と同様に stale 表示が残る。
