# Sub-agent実行レポート

## タスク

- 目的: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` の profile 切替 request 管理について、r5 指摘反映後の残指摘有無を確認する
- タスク種別: 設計書レビュー

## sub-agentを使う理由

- 理由: `review-enforcer` に従い、設計修正後の独立レビュー結果を task 単位で記録するため

## 対象範囲

- 対象: `TRACKER-000` の設計書差分、r5 レビュー指摘、関連調査メモ

## 対象外

- 対象外: 実装コード変更、tracking files 更新、PR 作成

## 実行コマンド

- 実行コマンド:
  - prompt-only sub-agent review

## 対象ファイル

- 変更または確認したファイル:
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `/home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-r5-20260501165300.md`
  - `/home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md`

## 指摘事項

- `High` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:295,303,537,569`
  - profile switch で engine 内 state は clear されるが、downstream 側の `TrackedSnapshotStore` / UI 表示を clear する規則がなく、`ProfileSwitched` だけ先に出て `WorldFrameCommitted` がまだ無い場合に old profile frame が new profile 表示へ残る。
- `Medium` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:266,304,538`
  - reconfigure request を「次の `Update` に 1 回だけ渡す」とした結果、入力停止中は request が無期限に pending のまま残り得る。入力が無くても `Update` を継続的に呼ぶか、control-only の drain 経路を別に持つかの明記が必要。

## 結果

- 結果:
  - 2 件の指摘あり。
  - いずれも profile switch を engine 内だけでなく downstream/UI と request drain まで閉じた契約にする必要を示した。

## リスク

- 未解決のリスクまたは後続対応:
  - `TrackedSnapshotStore` clear 規則がないと `ProfileSwitched` 後に old profile frame が UI に残留する。
  - control-only の request drain がないと、raw packet 停止中に profile / override apply が適用待ちのまま固まる。
