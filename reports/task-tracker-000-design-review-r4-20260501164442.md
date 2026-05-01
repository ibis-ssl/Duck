# Sub-agent実行レポート

## タスク

- 目的: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` の profile 切替責務境界に関する設計修正をレビューし、残指摘の解消有無を確認する
- タスク種別: 設計書レビュー

## sub-agentを使う理由

- 理由: `review-enforcer` に従い、独立した sub-agent 視点で task 単位レビューを記録するため

## 対象範囲

- 対象: `TRACKER-000` の設計書差分、関連レビュー報告、調査メモ

## 対象外

- 対象外: 実装コード変更、tracking files 更新、PR 作成

## 実行コマンド

- 実行コマンド:
  - prompt-only sub-agent review

## 対象ファイル

- 変更または確認したファイル:
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `/home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-r3-20260501125035.md`
  - `/home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md`

## 指摘事項

- `Medium` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:294,531,555`
  - `TrackerCoordinator` が publisher 配信先や UI の active profile 名を切り替える責務を持つ一方で、実際の profile 適用は `ITrackerEngine.Update` の先頭まで遅延する。coordinator 側の engine 外 state を要求受付時に切り替えるのか、`ProfileSwitched` 確認後に切り替えるのかが未規定で、old profile の結果が new profile 向け配信先/UI 表示と食い違うリスクがある。
- `Low` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:228,302,531`
  - `TrackerProfileSwitchRequest` を「次の `Update` に 1 回だけ渡す」とあるが、次の `Update` 前に profile 切替要求が連続した場合の規則がない。最新要求で上書きするのか、FIFO で queue するのか、未適用 request がある間は新規要求を拒否するのかが未定義。

## 結果

- 結果:
  - 2 件の指摘あり。
  - いずれも profile 切替要求の coordinator 側状態反映タイミングと連続要求規則の明文化不足に関するもの。

## リスク

- 未解決のリスクまたは後続対応:
  - coordinator の engine 外 state 切替タイミングを `ProfileSwitched` に揃えないまま実装へ進むと、old profile 出力と new profile 表示/配信先の不整合が起こり得る。
  - rapid switch 時の pending request 規則を固定しないと、最終適用 profile と observer 側の意味解釈が実装依存になる。
