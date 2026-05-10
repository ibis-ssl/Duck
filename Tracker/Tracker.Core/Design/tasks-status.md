# タスク状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 現在のタスク

- ID: TRACKER-036
- Title: 保守性改善全体の検証・レビュー・PR 完了通知を行う
- Phase: verification
- Status: in_progress
- Size: small
- Dependencies: TRACKER-033, TRACKER-034
- Exit Criteria:
  - 保守性改善全体の full test と必要な focused test が通る
  - TRACKER-032 から TRACKER-035 の report / review / commits が PR に反映されている
  - PR コメントに作業完了が記録されている

## 次の調査タスク

- none

## タスク一覧

| ID | タスク | フェーズ | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| TRACKER-000 | Tracker の設計書と進捗管理ファイルを作成する | preparation | done | Tracker の事前調査が完了していること | 設計書、task/phase 管理、調査レポート、レビュー報告が揃い、ユーザー承認の上で設計を完了できる。 |
| TRACKER-001 | `Tracker.Tests` から `Tracker.Core` を参照可能にし契約テスト基盤を作る | contracts | done | TRACKER-000 approved | `Tracker.Tests` から `Tracker.Core` を参照でき、contract test 用の fixture と test data 基盤が存在する。 |
| TRACKER-002 | packet generator の契約テストを追加する | contracts | done | TRACKER-001 | 単位変換、primary/secondary ball 並び、capabilities、`kicked_ball` 寿命、timestamp 出力を定義する failing test が存在する。 |
| TRACKER-003 | engine の時系列契約テストを追加する | contracts | done | TRACKER-001 | reorder、`MergeWindow`、`0..N CommittedFrames`、late packet、geometry reset、profile switch、event publish 順を定義する failing test が存在する。 |
| TRACKER-004 | `TrackerFrame` / state 型 / `TrackerUpdateResult` / observer-event 契約を実装する | contracts | done | TRACKER-002, TRACKER-003 | 内部フレーム、state 型、`TrackerUpdateResult`、domain event、observer 契約が存在し、契約テストが参照できる。 |
| TRACKER-005 | `TrackerPacketGenerator` を実装する | contracts | done | TRACKER-004 | official tracker proto 出力、primary/secondary ball 並び、timestamp、`kicked_ball`、capabilities がテストを通過する。 |
| TRACKER-006 | `TrackerEngine` の reorder buffer と flush pipeline を実装する | engine | done | TRACKER-003, TRACKER-004 | event-time buffer、flush 判定、`0..N CommittedFrames`、`WorldFrameCommitted` までの基本 pipeline が決定的に動作する。 |
| TRACKER-007 | `TrackerEngine` の profile switch / geometry reset / event publish 順を実装する | engine | done | TRACKER-006 | profile switch 要求、pending buffer clear、geometry reset、observer/event publish 順が契約どおりに動作する。 |
| TRACKER-008 | robot tracking と robot merge を実装する | engine | done | TRACKER-006 | camera-local robot track、位置/角度の別 filter、robot merge、visibility/quality が raw vision 入力から生成される。 |
| TRACKER-009 | ball tracking と primary/secondary ball 選定を実装する | engine | done | TRACKER-006 | camera-local ball track、uncertainty-weighted merge、primary ball 選定、secondary ball stable sort が raw vision 入力から生成される。 |
| TRACKER-010 | kick と contact metadata を実装する | engine | done | TRACKER-007, TRACKER-008, TRACKER-009 | `KickEventState`、`BallContactState`、`KickDetected`、`ContactChanged` が生成され、関連契約テストが通る。 |
| TRACKER-011 | ball left field metadata を実装する | engine | done | TRACKER-007, TRACKER-009 | `BallLeftFieldState` と `BallLeftField` event が生成され、関連契約テストが通る。 |
| TRACKER-012 | `Tracker.Server` へ engine と packet 配信を統合する | integration | done | TRACKER-005, TRACKER-007, TRACKER-010, TRACKER-011 | raw vision 入力が engine へ流れ、`TrackerUpdateResult` が snapshot store・observer・official packet 配信へ反映される。 |
| TRACKER-013 | tracker/network 設定束縛を統合する | integration | done | TRACKER-012 | tracker/network 設定が外部設定から束縛され、起動時設定が engine と publisher に反映される。 |
| TRACKER-014 | profile 切替要求経路を統合する | integration | done | TRACKER-012, TRACKER-013 | profile 切替要求が server から engine へ流れ、切替結果が observer/UI 側へ反映される。 |
| TRACKER-015 | tracked viewer と raw/tracked toggle を追加する | ui | done | TRACKER-012 | UI で raw/tracked を切り替えられ、tracked field と主要 object を描画できる。 |
| TRACKER-016 | tracked diagnostics 表示を追加する | ui | done | TRACKER-015 | tracked diagnostics、profile 名、kick/contact/field 状態を表示できる。 |
| TRACKER-017 | runtime profile 表示・操作 UI を追加する | ui | done | TRACKER-014, TRACKER-016 | profile 名表示と profile 切替要求 UI が表示・操作できる。 |
| TRACKER-018 | Tracker v1 の build/test 証跡を取得する | verification | done | TRACKER-017 | build/test の証跡が記録され、主要 unit/contract 観点の結果が reports に存在する。 |
| TRACKER-019 | Tracker v1 の integration 観点検証を行う | verification | done | TRACKER-018 | late packet、geometry reset、profile switch、observer/event、viewer 切替の確認結果が reports に存在する。 |
| TRACKER-020 | Tracker v1 の最終レビューと追跡ファイル同期を行う | review | done | TRACKER-019 | sub-agent レビュー結果が記録され、致命的な指摘が残っておらず、tracking files が最終状態と一致する。 |
| TRACKER-021 | `Tracker.Server` の使い方 README を追加する | documentation | done | TRACKER-020 | `Tracker/Tracker.Server/README.md` が存在し、起動手順、画面の使い方、主要設定値の意味が記載されている。 |
| TRACKER-022 | `VisionReceiver` を profile-aware にする | integration | done | TRACKER-021 | `VisionReceiver` 設定が複数 profile を持てて、起動中 profile と runtime switch に追従でき、関連検証結果が存在する。 |
| TRACKER-023 | camera-local tracking を線形 Kalman filter 標準へ是正する | engine | done | TRACKER-013, TRACKER-022 | ball / robot の camera-local track 更新が線形 Kalman filter ベースになり、`ProcessNoise` / `MeasurementNoise` / `Gate` / `VisibilityHalfLifeSeconds` が runtime 挙動へ反映され、既存 contract に矛盾しない。 |
| TRACKER-024 | Kalman 標準準拠の検証と release 判定をやり直す | verification | done | TRACKER-023 | Kalman 化後の focused/full test と review report が存在し、設計書の「v1 は直線運動前提の Kalman filter を標準とする」に対して未解決 blocker が残っていない。 |
| TRACKER-025 | Tracked 表示へ低 visibility の stale object を出さない | engine | done | TRACKER-024 | 欠測で十分 decayed した robot / ball track が `TrackerFrame` に出力されず、1 frame 程度の短期欠測を残す既存契約は維持される。設定差分は `reports/tracker-025-tigers-config-diff-20260510153510.md`、review は `reports/tracker-025-review-20260510154020.md` に記録済み。 |
| TRACKER-026 | Tracked 表示の raw/tracked diagnostics log を追加する | investigation | done | TRACKER-025 | raw SSL-Vision detection と tracked 出力を同じログで比較でき、誤検出の発生源を切り分けられる。`dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore` は 0 warning / 0 error。 |
| TRACKER-027 | Tigers 由来の近接重複 robot / 短命 ball 抑制を追加する | engine | done | TRACKER-026 | 近接別 ID robot を raw detection 単位で抑制し、短命 secondary ball ghost を 1 frame で出力しない。継続観測された genuine な複数 ball は stable sort で出力できる。実装・検証は `reports/tracker-027-evidence-20260510161437.md`、review は `reports/tracker-027-review-20260510161549.md` に記録済み。 |
| TRACKER-028 | capture 1680 付近の複数 ball 再発を解析して修正する | engine | done | TRACKER-027 | 指定 diagnostics log の trackedFrame 1680 付近で複数 ball になる原因を記録し、成長済み secondary ball が fresh observation を失った後に出続けないよう修正した。実装・検証は `reports/tracker-028-evidence-20260510215726.md`、review は `reports/tracker-028-review-20260510215726.md` に記録済み。 |
| TRACKER-029 | tracked object の小刻みな振動を抑制する | engine | done | TRACKER-028 | stationary に近い tracked ball / robot の表示揺れを抑制しつつ、実移動している object の追従性を過度に落とさない。振動抑制 tuning 値は profile 設定から外部調整できる。実装・検証は `reports/tracker-029-evidence-20260510221200.md`、review は `reports/tracker-029-review-20260510221200.md` に記録済み。 |
| TRACKER-030 | Tracked field 表示を Vision field geometry と揃える | ui | done | TRACKER-029 | tracked view でも defense area / goal / center / field arcs など Vision field と同等の線を描画し、raw Vision 画面との差分を `reports/tracker-030-evidence-20260510222529.md` に記録済み。review は `reports/tracker-030-review-20260510222529.md` に記録済み。 |
| TRACKER-031 | camera 間の同一 robot ID 遠方 outlier で robot が瞬間移動する問題を修正する | engine | done | TRACKER-030 | 同じ frame の別 camera に正常な同一 robot ID 観測がある場合、遠方 outlier camera 観測を tracked merge に混ぜない。原因・実装・検証は `reports/tracker-031-evidence-20260510223916.md`、review は `reports/tracker-031-review-20260510223916.md` に記録済み。 |
| TRACKER-032 | Tracker 保守性改善の詳細設計書を分割作成する | maintenance | done | TRACKER-031 | Core engine、Server/CLI/UI、test 保守性改善の詳細設計を日本語の分割ファイルとして作成した。worker report は `reports/tracker-032-core-design-worker-20260511063428.md`、`reports/tracker-032-server-design-worker-20260511063428.md`、`reports/tracker-032-test-design-worker-20260511063428.md`、review は `reports/tracker-032-review-20260511063428.md` に記録済み。 |
| TRACKER-033 | Core tracker engine の巨大ファイルを責務別に細分化し日本語コメントを追加する | maintenance | done | TRACKER-032 | `TrackerExecutionContracts.cs`、`TrackerModelContracts.cs`、`TrackerPacketGenerator.cs` を Core の責務別ファイルへ分割し、主要 class / property / method に日本語コメントを追加した。実装・検証は `reports/tracker-033-core-worker-20260511070200.md`、review は `reports/tracker-033-review-20260511072000.md` に記録済み。 |
| TRACKER-034 | Server / CLI / UI の巨大ファイルを責務別に細分化し日本語コメントを追加する | maintenance | done | TRACKER-032 | `Tracker.CaptureReplay/Program.cs`、`TrackerCoordinator.cs`、`Diagnostics.razor` などを責務別に分割し、主要 class / property / method に日本語コメントを追加した。実装・検証は `reports/tracker-034-server-worker-20260511074000.md`、追加コメント補強は `reports/tracker-034-comment-followup-worker-20260511082000.md`、review は `reports/tracker-034-review-20260511081000.md` と `reports/tracker-034-review-r2-20260511083000.md` に記録済み。 |
| TRACKER-035 | Tracker tests を読みやすく分割し確認内容の日本語コメントを追加する | maintenance | done | TRACKER-033, TRACKER-034 | 巨大 test file を責務別に分割し、対象 test 81 件に何を確認しているかの日本語コメントを追加した。実装・検証は `reports/tracker-035-test-worker-20260511085000.md`、review は `reports/tracker-035-review-20260511091000.md` に記録済み。 |
| TRACKER-036 | 保守性改善全体の検証・レビュー・PR 完了通知を行う | verification | in_progress | TRACKER-033, TRACKER-034, TRACKER-035 | full test と必要な focused test を実行し、review report を揃え、PR コメントに作業完了を記録する。 |
