# トラッカー履歴: `TRACKER-000` から `TRACKER-038`

このファイルは `tasks-status.md` / `phases-status.md` の現行開発情報を軽量化するため、完了済みの旧履歴を退避したもの。

進捗管理の軽量化と履歴退避は PR 準備の保守・運用作業であり、CaptureOn 比較ログの機能仕様ではない。


本書で raw vision は SSL-Vision の検出情報を指す。カメラの画像や動画そのものではない。
## 作業履歴

| ID | 作業 | 段階 | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| `TRACKER-000` | トラッカーの設計書と進捗管理ファイルを作成する | 準備 | 完了 | トラッカーの事前調査が完了していること | 設計書、作業 / 段階管理、調査レポート、レビュー報告が揃い、ユーザー承認の上で設計を完了できる。 |
| `TRACKER-001` | `Tracker.Tests` から `Tracker.Core` を参照可能にし契約テスト基盤を作る | 契約 | 完了 | `TRACKER-000` 承認済み | `Tracker.Tests` から `Tracker.Core` を参照でき、契約テスト用の共通準備とテストデータ基盤が存在する。 |
| `TRACKER-002` | パケット生成処理の契約テストを追加する | 契約 | 完了 | `TRACKER-001` | 単位変換、主対象 / 補助対象のボールの並び、対応機能、`kicked_ball` の寿命、時刻出力を定義し、実装前に失敗を確認できるテストが存在する。 |
| `TRACKER-003` | 追跡エンジンの時系列契約テストを追加する | 契約 | 完了 | `TRACKER-001` | 並べ替え、`MergeWindow`、`0..N CommittedFrames`、遅延到着パケット、フィールド形状の初期化、設定プロファイルの切り替え、イベント通知順を定義し、実装前に失敗を確認できるテストが存在する。 |
| `TRACKER-004` | `TrackerFrame` / 状態型 / `TrackerUpdateResult` / イベントと通知先の契約を実装する | 契約 | 完了 | `TRACKER-002`, `TRACKER-003` | 内部の追跡フレーム、状態型、`TrackerUpdateResult`、イベントデータの `TrackerEvent` と通知先の `ITrackerObserver` の契約が存在し、契約テストから参照できる。 |
| `TRACKER-005` | `TrackerPacketGenerator` を実装する | 契約 | 完了 | `TRACKER-004` | 公式トラッカー形式の出力、主対象 / 補助対象のボールの並び、時刻、`kicked_ball`、対応機能がテストを通過する。 |
| `TRACKER-006` | `TrackerEngine` の並べ替え用バッファと確定処理列を実装する | 追跡処理 | 完了 | `TRACKER-003`, `TRACKER-004` | イベント時刻用バッファ、確定判定、`0..N CommittedFrames`、`WorldFrameCommitted` までの基本処理が決定的に動作する。 |
| `TRACKER-007` | `TrackerEngine` の設定プロファイルの切り替え / フィールド形状の初期化 / イベント通知順を実装する | 追跡処理 | 完了 | `TRACKER-006` | 設定プロファイルの切り替え要求、適用待ちバッファの消去、フィールド形状の初期化、通知処理とイベント通知の順序が契約どおりに動作する。 |
| `TRACKER-008` | ロボット追跡とロボット情報の統合を実装する | 追跡処理 | 完了 | `TRACKER-006` | camera-local robot track、位置 / 角度を別々に扱う状態推定、ロボット情報の統合、可視性 / 品質が raw vision から生成される。 |
| `TRACKER-009` | ボール追跡と主対象 / 補助対象のボール選定を実装する | 追跡処理 | 完了 | `TRACKER-006` | camera-local ball track、不確かさで重み付けした統合、主対象のボール選定、補助対象のボールの安定した並べ替えが raw vision から生成される。 |
| `TRACKER-010` | キックと接触の付随情報を実装する | 追跡処理 | 完了 | `TRACKER-007`, `TRACKER-008`, `TRACKER-009` | `KickEventState`、`BallContactState`、`KickDetected`、`ContactChanged` が生成され、関連契約テストが通る。 |
| `TRACKER-011` | ボールのフィールド外判定の付随情報を実装する | 追跡処理 | 完了 | `TRACKER-007`, `TRACKER-009` | `BallLeftFieldState` と `BallLeftField` イベントが生成され、関連契約テストが通る。 |
| `TRACKER-012` | `Tracker.Server` へ追跡エンジンとパケット配信を統合する | 統合 | 完了 | `TRACKER-005`, `TRACKER-007`, `TRACKER-010`, `TRACKER-011` | raw vision が追跡エンジンへ流れ、`TrackerUpdateResult` がスナップショット保存処理、通知処理、公式形式のパケット配信へ反映される。 |
| `TRACKER-013` | 設定値のバインドを統合する（トラッカー / ネットワーク） | 統合 | 完了 | `TRACKER-012` | 外部設定の値がトラッカー / ネットワーク設定へ反映され、起動時設定が追跡エンジンと送信処理に反映される。 |
| `TRACKER-014` | 設定プロファイルの切り替え要求経路を統合する | 統合 | 完了 | `TRACKER-012`, `TRACKER-013` | 設定プロファイルの切り替え要求がサーバーから追跡エンジンへ流れ、切り替え結果が通知処理 / UI 側へ反映される。 |
| `TRACKER-015` | 追跡結果の表示画面と未加工入力 / 追跡結果の切り替えを追加する | UI | 完了 | `TRACKER-012` | UI で未加工入力 / 追跡結果を切り替えられ、追跡結果のフィールド表示と主要な追跡対象を描画できる。 |
| `TRACKER-016` | 追跡結果の診断表示を追加する | UI | 完了 | `TRACKER-015` | 追跡結果の診断、設定プロファイルの名前、キック / 接触 / フィールドの状態を表示できる。 |
| `TRACKER-017` | 実行時の設定プロファイルの表示・操作 UI を追加する | UI | 完了 | `TRACKER-014`, `TRACKER-016` | 設定プロファイルの名前の表示と設定プロファイルの切り替え要求の UI を表示・操作できる。 |
| `TRACKER-018` | トラッカー v1 のビルド / テスト証跡を取得する | 検証 | 完了 | `TRACKER-017` | ビルド / テストの証跡が記録され、主要な単体 / 契約観点の結果が報告書に存在する。 |
| `TRACKER-019` | トラッカー v1 の統合観点検証を行う | 検証 | 完了 | `TRACKER-018` | 遅延到着パケット、フィールド形状の初期化、設定プロファイルの切り替え、通知処理 / イベント、表示画面切り替えの確認結果が報告書に存在する。 |
| `TRACKER-020` | トラッカー v1 の最終レビューと進捗管理ファイル同期を行う | レビュー | 完了 | `TRACKER-019` | 別担当のレビュー結果が記録され、致命的な指摘が残っておらず、進捗管理ファイルが最終状態と一致する。 |
| `TRACKER-021` | `Tracker.Server` の使い方 README を追加する | 文書化 | 完了 | `TRACKER-020` | `Tracker/Tracker.Server/README.md` が存在し、起動手順、画面の使い方、主要設定値の意味が記載されている。 |
| `TRACKER-022` | `VisionReceiver` を設定プロファイルに対応にする | 統合 | 完了 | `TRACKER-021` | `VisionReceiver` 設定が複数の設定プロファイルを持てて、起動中の設定プロファイルと実行時の切り替えに追従でき、関連検証結果が存在する。 |
| `TRACKER-023` | カメラ単位の追跡を線形 Kalman filter へ是正する | 追跡処理 | 完了 | `TRACKER-013`, `TRACKER-022` | ボール / ロボットのカメラ単位の追跡更新が線形 Kalman filter を基準とし、`ProcessNoise` / `MeasurementNoise` / `Gate` / `VisibilityHalfLifeSeconds` が実行時の動作へ反映され、既存契約に矛盾しない。 |
| `TRACKER-024` | カルマン標準準拠の検証と公開可否判定をやり直す | 検証 | 完了 | `TRACKER-023` | カルマン化後の対象限定 / 全体テストとレビュー報告書が存在し、設計書の「v1 は直線運動前提の Kalman filter を標準とする」に対して未解決の進行阻害要因が残っていない。 |
| `TRACKER-025` | `Tracked` 表示へ可視性が低い古い追跡対象を出さない | 追跡処理 | 完了 | `TRACKER-024` | 欠測で十分に減衰したロボット / ボールの追跡状態が `TrackerFrame` に出力されず、1 回程度の短期欠測を残す既存契約は維持される。設定差分は `reports/tracker-025-tigers-config-diff-20260510153510.md`、レビューは `reports/tracker-025-review-20260510154020.md` に記録済み。 |
| `TRACKER-026` | `Tracked` 表示の未加工入力 / 追跡結果の診断ログを追加する | 調査 | 完了 | `TRACKER-025` | 未加工の SSL-Vision 検出情報と追跡結果を同じログで比較でき、誤検出の発生源を切り分けられる。`dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore` は警告 0 件 / エラー 0 件。 |
| `TRACKER-027` | Tigers 由来の近接重複ロボット / 短命ボール抑制を追加する | 追跡処理 | 完了 | `TRACKER-026` | 近接する別 ID のロボットを未加工の検出情報単位で抑制し、短命な補助対象ボールの誤った残像を 1 回の観測だけで出力しない。継続観測された実際の複数ボールは安定した並べ替えで出力できる。実装・検証は `reports/tracker-027-evidence-20260510161437.md`、レビューは `reports/tracker-027-review-20260510161549.md` に記録済み。 |
| `TRACKER-028` | キャプチャー 1680 付近の複数ボール再発を解析して修正する | 追跡処理 | 完了 | `TRACKER-027` | 指定診断ログの `trackedFrame` 1680 付近で複数ボールになる原因を記録し、成長済みの補助対象ボールが新しい観測を失った後に出続けないよう修正した。実装・検証は `reports/tracker-028-evidence-20260510215726.md`、レビューは `reports/tracker-028-review-20260510215726.md` に記録済み。 |
| `TRACKER-029` | 追跡対象の小刻みな振動を抑制する | 追跡処理 | 完了 | `TRACKER-028` | 静止に近い追跡中のボール / ロボットの表示揺れを抑制しつつ、実移動している対象の追従性を過度に落とさない。振動抑制の調整値は設定プロファイルから外部調整できる。実装・検証は `reports/tracker-029-evidence-20260510221200.md`、レビューは `reports/tracker-029-review-20260510221200.md` に記録済み。 |
| `TRACKER-030` | `Tracked` のフィールド表示を SSL-Vision のフィールド形状と揃える | UI | 完了 | `TRACKER-029` | 追跡結果の表示でも守備領域、ゴール、中央線、フィールドの円弧など SSL-Vision と同等の線を描画し、未加工入力画面との差分を `reports/tracker-030-evidence-20260510222529.md` に記録済み。レビューは `reports/tracker-030-review-20260510222529.md` に記録済み。 |
| `TRACKER-031` | カメラ間の同一ロボット ID の遠方外れ値でロボットが瞬間移動する問題を修正する | 追跡処理 | 完了 | `TRACKER-030` | 同じ観測フレームの別カメラに正常な同一ロボット ID 観測がある場合、遠方外れ値となるカメラ観測を追跡結果の統合へ混ぜない。原因・実装・検証は `reports/tracker-031-evidence-20260510223916.md`、レビューは `reports/tracker-031-review-20260510223916.md` に記録済み。 |
| `TRACKER-032` | トラッカー保守性改善の詳細設計書を分割作成する | 保守 | 完了 | `TRACKER-031` | 中核処理、サーバー / CLI / UI、テストの保守性改善に関する詳細設計を日本語の分割ファイルとして作成した。作業報告書は `reports/tracker-032-core-design-worker-20260511063428.md`、`reports/tracker-032-server-design-worker-20260511063428.md`、`reports/tracker-032-test-design-worker-20260511063428.md`、レビューは `reports/tracker-032-review-20260511063428.md` に記録済み。 |
| `TRACKER-033` | 中核の追跡エンジンにある巨大ファイルを責務別に細分化し日本語コメントを追加する | 保守 | 完了 | `TRACKER-032` | `TrackerExecutionContracts.cs`、`TrackerModelContracts.cs`、`TrackerPacketGenerator.cs` を責務別ファイルへ分割し、主要クラス / プロパティ / メソッドに日本語コメントを追加した。実装・検証は `reports/tracker-033-core-worker-20260511070200.md`、レビューは `reports/tracker-033-review-20260511072000.md` に記録済み。 |
| `TRACKER-034` | サーバー / CLI / UI の巨大ファイルを責務別に細分化し日本語コメントを追加する | 保守 | 完了 | `TRACKER-032` | `Tracker.CaptureReplay/Program.cs`、`TrackerCoordinator.cs`、`Diagnostics.razor` などを責務別に分割し、主要クラス / プロパティ / メソッドに日本語コメントを追加した。保守性設計は `Tracker/Design/DebugHost/debug-host-maintainability-design.md`、実装・検証は `reports/tracker-034-server-worker-20260511074000.md`、追加コメント補強は `reports/tracker-034-comment-followup-worker-20260511082000.md`、レビューは `reports/tracker-034-review-20260511081000.md` と `reports/tracker-034-review-r2-20260511083000.md` に記録済み。 |
| `TRACKER-035` | トラッカーのテストを読みやすく分割し確認内容の日本語コメントを追加する | 保守 | 完了 | `TRACKER-033`, `TRACKER-034` | 巨大なテストファイルを責務別に分割し、対象テスト 81 件に何を確認しているかの日本語コメントを追加した。実装・検証は `reports/tracker-035-test-worker-20260511085000.md`、レビューは `reports/tracker-035-review-20260511091000.md` に記録済み。 |
| `TRACKER-036` | 保守性改善全体の検証・レビュー・PR 完了通知を行う | 検証 | 完了 | `TRACKER-033`, `TRACKER-034`, `TRACKER-035` | 保守性改善全体の最終検証と最終レビューを実施した。最終検証は `reports/tracker-036-final-verification-20260511093000.md`、最終レビューは `reports/tracker-036-final-review-20260511094000.md` に記録済み。 |
| `TRACKER-037` | トラッカー保守性改善の命名・配置・コメント基準を決めて一貫性を確認する | 保守 | 完了 | `TRACKER-036` | `.` 区切りファイル名とフォルダ分割の使い分け、コメント付与対象、テストの XML コメント化方針を日本語で明文化した。監査は `reports/tracker-037-naming-comment-audit-20260511195008.md`、実装分担は `reports/tracker-037-design-rules-worker-20260511195640.md`、`reports/tracker-037-core-server-worker-20260511195640.md`、`reports/tracker-037-test-xml-comments-worker-20260511195640.md`、修正は `reports/tracker-037-review-fix-worker-20260511200910.md`、最終再レビューは `reports/tracker-037-review-r2-20260511201410.md` に記録済み。 |
| `TRACKER-038` | 診断ログの `trackedFrame` 3483 付近で黄色8番が首振りする原因を調査して修正する | 調査 | 完了 | `TRACKER-037` | 原因は raw vision、カメラ間の統合、表示処理ではなく、ロボット向きの状態推定が位置 mm 用の共分散を向きの軸に流用して過去の角速度を残すことだった。rad 単位の向きの共分散と角速度の上限制限を `RobotTracker` 設定へ外出しし、既存のカルマン係数契約も維持した。`Tracker.CaptureReplay` に追跡結果の詳細絞り込みとロボット向き / 角速度出力を追加し、設定ファイル / 解決済み付随情報による再生でもカルマン係数を保持するようにした。証跡は `reports/tracker-038-evidence-20260512080732.md` に記録済み。対象限定テスト 26 件、全体テスト 155 件は成功。初回レビューと r2 レビューの中程度の指摘は対応済み。r3 レビューは `reports/tracker-038-review-r3-20260512082903.md` に記録済みで指摘なし。 |

## 段階履歴

| 段階 | 状態 | 完了条件 |
| --- | --- | --- |
| 準備 | 完了 | トラッカーの設計書、調査レポート、設計レビュー報告、作業 / 段階管理が揃い、ユーザー承認の上で設計を完了した。 |
| 契約 | 完了 | `TRACKER-001` から `TRACKER-005` が完了し、`Tracker.Core` の内部状態表現、`TrackerUpdateResult`、パケット生成処理、通知 / イベント契約、およびそれらを固定する失敗 / 成功確認テストが揃う。 |
| 追跡処理 | 完了 | `TRACKER-006` から `TRACKER-011` に加え、`TRACKER-023`、`TRACKER-025`、`TRACKER-027`、`TRACKER-028`、`TRACKER-029`、`TRACKER-031` が完了した。カメラ単位のロボット / ボール追跡が設計どおり線形 Kalman filter を基準として実装され、可視性が低い古い追跡対象、Tigers 由来の近接重複ロボット、短命ボールの誤った残像、古い補助対象ボールが追跡フレームへ出続けない。静止に近い追跡対象の小刻みな振動は抑制され、カメラ間の同一ロボット ID の遠方外れ値は正常な別カメラ観測がある場合に追跡結果の統合へ混ざらない。 |
| 統合 | 完了 | `Tracker.Server` から追跡エンジン、スナップショット保存処理、通知処理、official tracker packet 配信、設定値のバインド、設定プロファイルの切り替え要求経路までが接続され、設定プロファイルに対応した `VisionReceiver` 設定が反映される。 |
| UI | 完了 | `TRACKER-015` から `TRACKER-017` に加え、`TRACKER-030` が完了し、追跡結果の表示画面、未加工入力 / 追跡結果の切り替え、追跡結果の診断表示、実行時の設定プロファイルの切り替え要求 UI、SSL-Vision のフィールド形状と揃った追跡結果のフィールド表示が用意される。 |
| 検証 | 完了 | `TRACKER-018` と `TRACKER-019` に加え、`TRACKER-024` が完了し、カルマン標準準拠後および古い追跡対象の抑制後のビルド / テスト / レビュー証跡が報告書に存在する。`TRACKER-028` の指定キャプチャーの再生証跡が `reports/tracker-028-evidence-20260510215726.md`、`TRACKER-029` の振動抑制検証が `reports/tracker-029-evidence-20260510221200.md`、`TRACKER-030` のフィールド形状表示検証が `reports/tracker-030-evidence-20260510222529.md`、`TRACKER-031` の瞬間移動抑制検証が `reports/tracker-031-evidence-20260510223916.md` に記録済み。`TRACKER-036` で保守性改善後の最終検証を `reports/tracker-036-final-verification-20260511093000.md` に記録済み。 |
| レビュー | 完了 | `TRACKER-020` に加え、カルマン標準準拠後および古い追跡対象の抑制後のレビュー結果が記録され、致命的な指摘が残っていない。`TRACKER-028`、`TRACKER-029`、`TRACKER-030`、`TRACKER-031` のレビュー結果は報告書に記録済み。`TRACKER-032` 以降も作業ごとのレビュー報告書を作成してきた。`TRACKER-038` は初回レビューと r2 レビューの中程度の指摘を修正し、r3 レビューは `reports/tracker-038-review-r3-20260512082903.md` に記録済みで指摘なし。 |
| 文書化 | 完了 | `TRACKER-021` が完了し、`Tracker.Server` の README に起動手順、画面の使い方、主要設定値の意味が記録されている。 |
| 調査 | 完了 | `TRACKER-026` が完了し、未加工の SSL-Vision 検出情報と追跡結果を同じログで比較できる。`TRACKER-038` で指定診断ログの `trackedFrame=3483` 付近における黄色8番の首振り原因を向きの状態推定へ切り分け、rad 単位の向きの共分散 / 角速度の上限制限と `Tracker.CaptureReplay` の汎用詳細改善を実装した。向きの調整値は `RobotTracker` 設定へ外出し済み。CaptureReplay の再生でもカルマン係数を保持する。証跡は `reports/tracker-038-evidence-20260512080732.md` に記録済み。対象限定テスト 26 件、全体テスト 155 件は成功。r3 レビューは指摘なし。 |
| 保守 | 完了 | `TRACKER-032` から `TRACKER-035` で詳細設計書の分割、巨大ソースファイルの責務別分割、主要クラス / プロパティ / メソッドの日本語コメント追加、テストの確認内容コメント追加を完了した。`TRACKER-037` で`.` 区切りファイル名とフォルダ分割の使い分け、コメント付与対象、テストの XML コメント化方針を明文化し、現状ファイルを同じ基準へ揃えた。親 Codex は管理役として作業を管理し、実装・設計書作成・テスト編集・レビューは `gpt-5.5 high` の別担当へ委譲した。 |
