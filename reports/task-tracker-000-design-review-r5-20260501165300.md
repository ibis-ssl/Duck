# Sub-agent実行レポート

## タスク

- 目的: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` の profile 切替責務境界について、r4 指摘反映後の残指摘有無を確認する
- タスク種別: 設計書レビュー

## sub-agentを使う理由

- 理由: `review-enforcer` に従い、設計修正後の独立レビュー結果を task 単位で記録するため

## 対象範囲

- 対象: `TRACKER-000` の設計書差分、r4 レビュー指摘、関連調査メモ

## 対象外

- 対象外: 実装コード変更、tracking files 更新、PR 作成

## 実行コマンド

- 実行コマンド:
  - prompt-only sub-agent review

## 対象ファイル

- 変更または確認したファイル:
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `/home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-r4-20260501164442.md`
  - `/home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md`

## 指摘事項

- `Medium` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:302,533`
  - `pending request` を最新で上書きする規則は入ったが、`Update` に渡した直後から `ProfileSwitched` を受けるまでの `in-flight request` 識別が未規定で、どの request に対応する engine 外 state へ切り替えるかが曖昧。
- `Medium` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:228,533`
  - `TrackerProfileSwitchRequest` に `RuntimeOverrides` を載せる方針になった一方で、pending / in-flight 中の override 更新をどう扱うかが未定義で、engine 適用値と UI / 保存領域が持つ最新値がずれる余地がある。

## 結果

- 結果:
  - 2 件の指摘あり。
  - いずれも coordinator が request snapshot をどの粒度で固定し、`ProfileSwitched` と突き合わせるかの規則不足に関するもの。

## リスク

- 未解決のリスクまたは後続対応:
  - `in-flight request` を固定しないまま実装すると、rapid switch / rapid override apply 時に publisher 配信先や active profile 表示が誤った request に追従する恐れがある。
  - override snapshot の不変条件を定めないまま進むと、engine 内の有効設定と UI 側の表示・保存状態の整合が取りにくい。
