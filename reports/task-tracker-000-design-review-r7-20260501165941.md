# Sub-agent実行レポート

## タスク

- 目的: `Tracker/Tracker.Core/Design/tracker-architecture-plan.md` の profile 切替 downstream 契約について、r6 指摘反映後の残指摘有無を確認する
- タスク種別: 設計書レビュー

## sub-agentを使う理由

- 理由: `review-enforcer` に従い、設計修正後の独立レビュー結果を task 単位で記録するため

## 対象範囲

- 対象: `TRACKER-000` の設計書差分、r6 レビュー指摘、関連調査メモ

## 対象外

- 対象外: 実装コード変更、tracking files 更新、PR 作成

## 実行コマンド

- 実行コマンド:
  - prompt-only sub-agent review

## 対象ファイル

- 変更または確認したファイル:
  - `/home/ibis/ssl/IbisDuck/Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - `/home/ibis/ssl/IbisDuck/reports/task-tracker-000-design-review-r6-20260501165622.md`
  - `/home/ibis/ssl/IbisDuck/reports/TRACKER-000-tigers-investigation-20260501115618.md`

## 指摘事項

- `High` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:264,304,335`
  - `CommittedFrames == 0` では UI 更新を行わない規則と、control-only `Update` で `ProfileSwitched` に伴う active profile 更新や `TrackedSnapshotStore` clear を行う規則が衝突している。0-frame 時でも state clear と表示状態更新は許可する例外が必要。
- `Medium` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:544,554,576`
  - override 単独更新も `TrackerProfileSwitchRequest` に流す方針になった一方で、同値 request の再適用を no-op にする規則がなく、実質変更なしでも state reset と tracked 空白が発生し得る。
- `Medium` `Tracker/Tracker.Core/Design/tracker-architecture-plan.md:228,335,347`
  - control-only `Update` を許可したのに、入力契約先頭では `SSL_WrapperPacket` が必須入力のように残っており、API 形状が optional なのか別入力型なのか曖昧。

## 結果

- 結果:
  - 3 件の指摘あり。
  - profile switch の制御経路を成立させるには、0-frame 例外、no-op request、control-only 入力の API 契約まで前段で揃える必要があると判明した。

## リスク

- 未解決のリスクまたは後続対応:
  - 0-frame 入力の UI / store 更新例外が曖昧なままだと、実装者ごとに `ProfileSwitched` 時の downstream clear 振る舞いが割れる。
  - 同値 request の no-op 抑止が無いと、不要な reset により tracked 空白や observer ノイズが増える。
  - control-only `Update` の入力契約が optional に揃っていないと、API 形状と実装戦略がぶれる。
