# タスク状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 現在のタスク

- ID: TRACKER-004
- Title: `TrackerFrame` / state 型 / `TrackerUpdateResult` / observer-event 契約を実装する
- Phase: contracts
- Status: pending
- Size: medium
- Dependencies: TRACKER-002, TRACKER-003 が完了していること
- Exit Criteria:
  - 内部フレーム、state 型、`TrackerUpdateResult`、domain event、observer 契約が存在し、契約テストが参照できる。

## タスク一覧

| ID | タスク | フェーズ | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| TRACKER-000 | Tracker の設計書と進捗管理ファイルを作成する | preparation | done | Tracker の事前調査が完了していること | 設計書、task/phase 管理、調査レポート、レビュー報告が揃い、ユーザー承認の上で設計を完了できる。 |
| TRACKER-001 | `Tracker.Tests` から `Tracker.Core` を参照可能にし契約テスト基盤を作る | contracts | done | TRACKER-000 approved | `Tracker.Tests` から `Tracker.Core` を参照でき、contract test 用の fixture と test data 基盤が存在する。 |
| TRACKER-002 | packet generator の契約テストを追加する | contracts | done | TRACKER-001 | 単位変換、primary/secondary ball 並び、capabilities、`kicked_ball` 寿命、timestamp 出力を定義する failing test が存在する。 |
| TRACKER-003 | engine の時系列契約テストを追加する | contracts | done | TRACKER-001 | reorder、`MergeWindow`、`0..N CommittedFrames`、late packet、geometry reset、profile switch、event publish 順を定義する failing test が存在する。 |
| TRACKER-004 | `TrackerFrame` / state 型 / `TrackerUpdateResult` / observer-event 契約を実装する | contracts | pending | TRACKER-002, TRACKER-003 | 内部フレーム、state 型、`TrackerUpdateResult`、domain event、observer 契約が存在し、契約テストが参照できる。 |
| TRACKER-005 | `TrackerPacketGenerator` を実装する | contracts | pending | TRACKER-004 | official tracker proto 出力、primary/secondary ball 並び、timestamp、`kicked_ball`、capabilities がテストを通過する。 |
| TRACKER-006 | `TrackerEngine` の reorder buffer と flush pipeline を実装する | engine | pending | TRACKER-003, TRACKER-004 | event-time buffer、flush 判定、`0..N CommittedFrames`、`WorldFrameCommitted` までの基本 pipeline が決定的に動作する。 |
| TRACKER-007 | `TrackerEngine` の profile switch / geometry reset / event publish 順を実装する | engine | pending | TRACKER-006 | profile switch 要求、pending buffer clear、geometry reset、observer/event publish 順が契約どおりに動作する。 |
| TRACKER-008 | robot tracking と robot merge を実装する | engine | pending | TRACKER-006 | camera-local robot track、位置/角度の別 filter、robot merge、visibility/quality が raw vision 入力から生成される。 |
| TRACKER-009 | ball tracking と primary/secondary ball 選定を実装する | engine | pending | TRACKER-006 | camera-local ball track、uncertainty-weighted merge、primary ball 選定、secondary ball stable sort が raw vision 入力から生成される。 |
| TRACKER-010 | kick と contact metadata を実装する | engine | pending | TRACKER-007, TRACKER-008, TRACKER-009 | `KickEventState`、`BallContactState`、`KickDetected`、`ContactChanged` が生成され、関連契約テストが通る。 |
| TRACKER-011 | ball left field metadata を実装する | engine | pending | TRACKER-007, TRACKER-009 | `BallLeftFieldState` と `BallLeftField` event が生成され、関連契約テストが通る。 |
| TRACKER-012 | `Tracker.Server` へ engine と packet 配信を統合する | integration | pending | TRACKER-005, TRACKER-007, TRACKER-010, TRACKER-011 | raw vision 入力が engine へ流れ、`TrackerUpdateResult` が snapshot store・observer・official packet 配信へ反映される。 |
| TRACKER-013 | tracker/network 設定束縛を統合する | integration | pending | TRACKER-012 | tracker/network 設定が外部設定から束縛され、起動時設定が engine と publisher に反映される。 |
| TRACKER-014 | profile 切替要求経路を統合する | integration | pending | TRACKER-012, TRACKER-013 | profile 切替要求が server から engine へ流れ、切替結果が observer/UI 側へ反映される。 |
| TRACKER-015 | tracked viewer と raw/tracked toggle を追加する | ui | pending | TRACKER-012 | UI で raw/tracked を切り替えられ、tracked field と主要 object を描画できる。 |
| TRACKER-016 | tracked diagnostics 表示を追加する | ui | pending | TRACKER-015 | tracked diagnostics、profile 名、kick/contact/field 状態を表示できる。 |
| TRACKER-017 | runtime profile 表示・操作 UI を追加する | ui | pending | TRACKER-014, TRACKER-016 | profile 名表示と profile 切替要求 UI が表示・操作できる。 |
| TRACKER-018 | Tracker v1 の build/test 証跡を取得する | verification | pending | TRACKER-017 | build/test の証跡が記録され、主要 unit/contract 観点の結果が reports に存在する。 |
| TRACKER-019 | Tracker v1 の integration 観点検証を行う | verification | pending | TRACKER-018 | late packet、geometry reset、profile switch、observer/event、viewer 切替の確認結果が reports に存在する。 |
| TRACKER-020 | Tracker v1 の最終レビューと追跡ファイル同期を行う | review | pending | TRACKER-019 | sub-agent レビュー結果が記録され、致命的な指摘が残っておらず、tracking files が最終状態と一致する。 |
