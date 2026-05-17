# トラッカー 履歴: TRACKER-000 から TRACKER-038

この文書は `tasks-status.md` / `phases-status.md` の現行開発文脈を軽量化するため、完了済みの旧履歴を退避したもの。

追跡管理文書の軽量化と履歴退避は PR 準備の保守性/運用作業であり、CaptureOn 比較記録の機能仕様ではない。

## 作業履歴

| ID | 作業 | 段階 | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| TRACKER-000 | トラッカー の設計書と進捗管理文書を作成する | preparation | done | トラッカー の事前調査が完了していること | 設計書、作業/段階管理、調査報告、確認報告が揃い、利用者承認の上で設計を完了できる。 |
| TRACKER-001 | `Tracker.Tests` から `Tracker.Core` を参照可能にし契約試験基盤を作る | contracts | done | TRACKER-000 承認済み | `Tracker.Tests` から `Tracker.Core` を参照でき、契約試験用の固定入力と検証情報基盤が存在する。 |
| TRACKER-002 | 通信単位生成器の契約試験を追加する | contracts | done | TRACKER-001 | 単位変換、第1/第2ボール並び、能力情報、`kicked_ball` 寿命、時刻出力を定義する失敗する試験が存在する。 |
| TRACKER-003 | 処理機構の時系列契約試験を追加する | contracts | done | TRACKER-001 | 並び替え、`MergeWindow`、`0..N CommittedFrames`、遅延通信単位、形状初期化、設定切替、事象発行順を定義する失敗する試験が存在する。 |
| TRACKER-004 | `TrackerFrame` / 状態型 / `TrackerUpdateResult` / 監視者事象契約を実装する | contracts | done | TRACKER-002, TRACKER-003 | 内部時点、状態型、`TrackerUpdateResult`、領域事象、監視者契約が存在し、契約試験が参照できる。 |
| TRACKER-005 | `TrackerPacketGenerator` を実装する | contracts | done | TRACKER-004 | 公式トラッカー形式の出力、第1/第2ボール並び、時刻、`kicked_ball`、能力情報が試験を通過する。 |
| TRACKER-006 | `TrackerEngine` の並び替え保持領域と放出処理経路を実装する | engine | done | TRACKER-003, TRACKER-004 | 事象時刻保持領域、放出判定、`0..N CommittedFrames`、`WorldFrameCommitted` までの基本処理経路が決定的に動作する。 |
| TRACKER-007 | `TrackerEngine` の設定切替 / 形状初期化 / 事象発行順を実装する | engine | done | TRACKER-006 | 設定切替要求、保留保持領域の消去、形状初期化、監視者通知/事象発行順が契約どおりに動作する。 |
| TRACKER-008 | ロボット追跡とロボット統合を実装する | engine | done | TRACKER-006 | 撮像元単位のロボット追跡、位置/角度の別推定器、ロボット統合、可視性/品質が生の視覚入力から生成される。 |
| TRACKER-009 | ボール追跡と第1/第2ボール選定を実装する | engine | done | TRACKER-006 | 撮像元単位のボール追跡、不確かさ重み付き統合、第1ボール選定、第2ボールの安定した並べ替えが生の視覚入力から生成される。 |
| TRACKER-010 | 蹴りと接触付帯情報を実装する | engine | done | TRACKER-007, TRACKER-008, TRACKER-009 | `KickEventState`、`BallContactState`、`KickDetected`、`ContactChanged` が生成され、関連契約試験が通る。 |
| TRACKER-011 | ボール離脱付帯情報を実装する | engine | done | TRACKER-007, TRACKER-009 | `BallLeftFieldState` と `BallLeftField` 事象が生成され、関連契約試験が通る。 |
| TRACKER-012 | `Tracker.Server` へ処理機構と通信単位配信を統合する | integration | done | TRACKER-005, TRACKER-007, TRACKER-010, TRACKER-011 | 生の視覚入力が処理機構へ流れ、`TrackerUpdateResult` が状態保存・監視者・公式通信単位配信へ反映される。 |
| TRACKER-013 | トラッカー/通信設定束縛を統合する | integration | done | TRACKER-012 | トラッカー/通信設定が外部設定から束縛され、起動時設定が処理機構と配信処理に反映される。 |
| TRACKER-014 | 設定切替要求経路を統合する | integration | done | TRACKER-012, TRACKER-013 | 設定切替要求が配信側から処理機構へ流れ、切替結果が監視者/UI 側へ反映される。 |
| TRACKER-015 | 追跡表示と生入力/追跡結果の切替を追加する | ui | done | TRACKER-012 | UI で生入力/追跡結果を切り替えられ、追跡結果の領域と主要対象を描画できる。 |
| TRACKER-016 | 追跡結果の診断表示を追加する | ui | done | TRACKER-015 | 追跡結果の診断、設定名、蹴り/接触/領域状態を表示できる。 |
| TRACKER-017 | 実行時設定表示・操作 UI を追加する | ui | done | TRACKER-014, TRACKER-016 | 設定名表示と設定切替要求 UI が表示・操作できる。 |
| TRACKER-018 | トラッカー v1 の構築/試験証跡を取得する | verification | done | TRACKER-017 | 構築/試験の証跡が記録され、主要な単体/契約観点の結果が `reports/` に存在する。 |
| TRACKER-019 | トラッカー v1 の統合観点検証を行う | verification | done | TRACKER-018 | 遅延通信単位、形状初期化、設定切替、監視者/事象、表示切替の確認結果が `reports/` に存在する。 |
| TRACKER-020 | トラッカー v1 の最終確認と追跡文書同期を行う | review | done | TRACKER-019 | 委譲先確認結果が記録され、致命的な指摘が残っておらず、追跡管理文書が最終状態と一致する。 |
| TRACKER-021 | `Tracker.Server` の使い方文書を追加する | documentation | done | TRACKER-020 | `Tracker/Tracker.Server/README.md` が存在し、起動手順、画面の使い方、主要設定値の意味が記載されている。 |
| TRACKER-022 | `VisionReceiver` を複数設定対応にする | integration | done | TRACKER-021 | `VisionReceiver` 設定が複数の設定を持てて、起動中設定と実行時切替に追従でき、関連検証結果が存在する。 |
| TRACKER-023 | 撮像元単位の追跡を線形 カルマン 推定器標準へ是正する | engine | done | TRACKER-013, TRACKER-022 | ボール / ロボットの撮像元単位の追跡更新が線形 カルマン 推定器基盤になり、`ProcessNoise` / `MeasurementNoise` / `Gate` / `VisibilityHalfLifeSeconds` が実行時挙動へ反映され、既存契約に矛盾しない。 |
| TRACKER-024 | カルマン 標準準拠の検証と公開判定をやり直す | verification | done | TRACKER-023 | カルマン 化後の絞り込み/全体試験と確認報告が存在し、設計書の「v1 は直線運動前提の カルマン 推定器を標準とする」に対して未解決の阻害要因が残っていない。 |
| TRACKER-025 | 追跡表示へ低可視性の古い対象を出さない | engine | done | TRACKER-024 | 欠測で十分減衰したロボット / ボール追跡が `TrackerFrame` に出力されず、1時点程度の短期欠測を残す既存契約は維持される。設定差分は `reports/tracker-025-tigers-config-diff-20260510153510.md`、確認結果は `reports/tracker-025-review-20260510154020.md` に記録済み。 |
| TRACKER-026 | 追跡表示の生入力/追跡結果診断記録を追加する | investigation | done | TRACKER-025 | 生のSSL-Vision検出と追跡出力を同じ記録で比較でき、誤検出の発生源を切り分けられる。`dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore` は警告0件/異常0件。 |
| TRACKER-027 | Tigers 由来の近接重複ロボット / 短命ボール抑制を追加する | engine | done | TRACKER-026 | 近接別 ID ロボットを生検出単位で抑制し、短命の第2ボール誤検出を1時点で出力しない。継続観測された実体のある複数ボールは安定した並べ替えで出力できる。実装・検証は `reports/tracker-027-evidence-20260510161437.md`、確認結果は `reports/tracker-027-review-20260510161549.md` に記録済み。 |
| TRACKER-028 | 取り込み1680付近の複数ボール再発を解析して修正する | engine | done | TRACKER-027 | 指定診断記録の `trackedFrame` 1680付近で複数ボールになる原因を記録し、成長済みの第2ボールが新しい観測を失った後に出続けないよう修正した。実装・検証は `reports/tracker-028-evidence-20260510215726.md`、確認結果は `reports/tracker-028-review-20260510215726.md` に記録済み。 |
| TRACKER-029 | 追跡対象の小刻みな振動を抑制する | engine | done | TRACKER-028 | 静止に近い追跡ボール / ロボットの表示揺れを抑制しつつ、実移動している対象の追従性を過度に落とさない。振動抑制調整値は設定から外部調整できる。実装・検証は `reports/tracker-029-evidence-20260510221200.md`、確認結果は `reports/tracker-029-review-20260510221200.md` に記録済み。 |
| TRACKER-030 | 追跡領域表示を視覚入力の領域形状と揃える | ui | done | TRACKER-029 | 追跡表示でも防御領域 / 得点領域 / 中心 / 領域弧など視覚入力の領域と同等の線を描画し、生の視覚画面との差分を `reports/tracker-030-evidence-20260510222529.md` に記録済み。確認結果は `reports/tracker-030-review-20260510222529.md` に記録済み。 |
| TRACKER-031 | 撮像元間の同一ロボット ID 遠方外れ値でロボットが瞬間移動する問題を修正する | engine | done | TRACKER-030 | 同じ時点の別撮像元に正常な同一ロボット ID 観測がある場合、遠方外れ値の撮像元観測を追跡統合に混ぜない。原因・実装・検証は `reports/tracker-031-evidence-20260510223916.md`、確認結果は `reports/tracker-031-review-20260510223916.md` に記録済み。 |
| TRACKER-032 | トラッカー 保守性改善の詳細設計書を分割作成する | maintenance | done | TRACKER-031 | 中核処理、配信側/CLI/UI、試験保守性改善の詳細設計を日本語の分割文書として作成した。作業報告は `reports/tracker-032-core-design-worker-20260511063428.md`、`reports/tracker-032-server-design-worker-20260511063428.md`、`reports/tracker-032-test-design-worker-20260511063428.md`、確認結果は `reports/tracker-032-review-20260511063428.md` に記録済み。 |
| TRACKER-033 | 中核トラッカー処理機構の巨大文書を責務別に細分化し日本語注釈を追加する | maintenance | done | TRACKER-032 | `TrackerExecutionContracts.cs`、`TrackerModelContracts.cs`、`TrackerPacketGenerator.cs` を中核の責務別文書へ分割し、主要な型 / 属性 / 処理に日本語注釈を追加した。実装・検証は `reports/tracker-033-core-worker-20260511070200.md`、確認結果は `reports/tracker-033-review-20260511072000.md` に記録済み。 |
| TRACKER-034 | 配信側 / CLI / UI の巨大文書を責務別に細分化し日本語注釈を追加する | maintenance | done | TRACKER-032 | `Tracker.CaptureReplay/Program.cs`、`TrackerCoordinator.cs`、`Diagnostics.razor` などを責務別に分割し、主要な型 / 属性 / 処理に日本語注釈を追加した。保守性設計は `Tracker/Design/DebugHost/debug-host-maintainability-design.md`、実装・検証は `reports/tracker-034-server-worker-20260511074000.md`、追加注釈補強は `reports/tracker-034-comment-followup-worker-20260511082000.md`、確認結果は `reports/tracker-034-review-20260511081000.md` と `reports/tracker-034-review-r2-20260511083000.md` に記録済み。 |
| TRACKER-035 | トラッカー試験群を読みやすく分割し確認内容の日本語注釈を追加する | maintenance | done | TRACKER-033, TRACKER-034 | 巨大試験文書を責務別に分割し、対象試験81件に何を確認しているかの日本語注釈を追加した。実装・検証は `reports/tracker-035-test-worker-20260511085000.md`、確認結果は `reports/tracker-035-review-20260511091000.md` に記録済み。 |
| TRACKER-036 | 保守性改善全体の検証・確認・PR 完了通知を行う | verification | done | TRACKER-033, TRACKER-034, TRACKER-035 | 保守性改善全体の最終検証と最終確認を実施した。最終検証は `reports/tracker-036-final-verification-20260511093000.md`、最終確認は `reports/tracker-036-final-review-20260511094000.md` に記録済み。 |
| TRACKER-037 | トラッカー 保守性改善の命名・配置・注釈基準を決めて一貫性を確認する | maintenance | done | TRACKER-036 | 点区切り文書名と階層分割の使い分け、注釈付与対象、試験の XML 注釈化方針を日本語で明文化した。監査は `reports/tracker-037-naming-comment-audit-20260511195008.md`、実装分担は `reports/tracker-037-design-rules-worker-20260511195640.md`、`reports/tracker-037-core-server-worker-20260511195640.md`、`reports/tracker-037-test-xml-comments-worker-20260511195640.md`、修正は `reports/tracker-037-review-fix-worker-20260511200910.md`、最終再確認は `reports/tracker-037-review-r2-20260511201410.md` に記録済み。 |
| TRACKER-038 | 診断記録の `trackedFrame` 3483付近で黄色8番が首振りする原因を調査して修正する | investigation | done | TRACKER-037 | 原因は生の視覚入力、撮像元間統合、表示処理ではなく、ロボット向き推定器が位置 mm 用の共分散を向き軸に流用して過去の角速度を残すことだった。rad 単位の向き共分散と角速度制限を `RobotTracker` 設定へ外出しし、既存 カルマン 尺度契約も維持した。`Tracker.CaptureReplay` に時点詳細抽出とロボット向き / 角速度出力を追加し、`appsettings` / 解決済み付帯情報の再生で カルマン 尺度を保持するようにした。証跡は `reports/tracker-038-evidence-20260512080732.md` に記録済み。絞り込み試験26件、全体試験155件は通過。初回確認と2回目確認の中程度の指摘は対応済み。3回目確認は `reports/tracker-038-review-r3-20260512082903.md` に記録済みで指摘なし。 |

## 段階履歴

| 段階 | 状態 | 完了条件 |
| --- | --- | --- |
| preparation | done | トラッカー の設計書、調査報告、設計確認報告、作業/段階管理が揃い、利用者承認の上で設計を完了した。 |
| contracts | done | `TRACKER-001` から `TRACKER-005` が完了し、`Tracker.Core` の内部表現、`TrackerUpdateResult`、通信単位生成器、監視者/事象契約、およびそれらを固定する失敗/成功試験が揃う。 |
| engine | done | `TRACKER-006` から `TRACKER-011` に加え、`TRACKER-023`、`TRACKER-025`、`TRACKER-027`、`TRACKER-028`、`TRACKER-029`、`TRACKER-031` が完了した。撮像元単位のロボット/ボール追跡が設計どおり線形 カルマン 推定器を標準として実装され、低可視性の古い対象と Tigers 由来の近接重複ロボット / 短命ボール誤検出 / 古い第2ボールが追跡時点へ出続けない。静止に近い追跡対象の小刻みな振動は抑制され、撮像元間の同一ロボット ID 遠方外れ値は正常な別撮像元観測がある場合に追跡統合へ混ざらない。 |
| integration | done | `Tracker.Server` から処理機構、状態保存、監視者、公式トラッカー通信単位配信、設定束縛、設定切替要求経路までが接続され、複数設定対応の `VisionReceiver` 設定が反映される。 |
| ui | done | `TRACKER-015` から `TRACKER-017` に加え、`TRACKER-030` が完了し、追跡表示、生入力/追跡結果切替、追跡結果の診断表示、実行時設定切替要求 UI、生の視覚領域形状と揃った追跡領域表示が用意される。 |
| verification | done | `TRACKER-018` と `TRACKER-019` に加え、`TRACKER-024` が完了し、カルマン 標準準拠後および古い対象抑制後の構築/試験/確認証跡が `reports/` に存在する。`TRACKER-028` の指定取り込み再生証跡が `reports/tracker-028-evidence-20260510215726.md`、`TRACKER-029` の振動抑制検証が `reports/tracker-029-evidence-20260510221200.md`、`TRACKER-030` の領域形状表示検証が `reports/tracker-030-evidence-20260510222529.md`、`TRACKER-031` の瞬間移動抑制検証が `reports/tracker-031-evidence-20260510223916.md` に記録済み。`TRACKER-036` で保守性改善後の最終検証を `reports/tracker-036-final-verification-20260511093000.md` に記録済み。 |
| review | done | `TRACKER-020` に加え、カルマン 標準準拠後および古い対象抑制後の確認結果が記録され、致命的な指摘が残っていない。`TRACKER-028`、`TRACKER-029`、`TRACKER-030`、`TRACKER-031` の確認結果は `reports/` に記録済み。`TRACKER-032` 以降も作業ごとの確認報告を作成してきた。`TRACKER-038` は初回確認と2回目確認の中程度の指摘を修正し、3回目確認は `reports/tracker-038-review-r3-20260512082903.md` に記録済みで指摘なし。 |
| documentation | done | `TRACKER-021` が完了し、`Tracker.Server` の説明文書に起動手順、画面の使い方、主要設定値の意味が記録されている。 |
| investigation | done | `TRACKER-026` が完了し、生のSSL-Vision検出と追跡出力を同じ記録で比較できる。`TRACKER-038` で指定診断記録の `trackedFrame=3483` 付近における黄色8番の首振り原因を向き推定器へ切り分け、rad 単位の向き共分散 / 角速度制限と `Tracker.CaptureReplay` の汎用詳細改善を実装した。向き調整値は `RobotTracker` 設定へ外出し済み。`Tracker.CaptureReplay` の再生でも カルマン 尺度を保持する。証跡は `reports/tracker-038-evidence-20260512080732.md` に記録済み。絞り込み試験26件、全体試験155件は通過。3回目確認は指摘なし。 |
| maintenance | done | `TRACKER-032` から `TRACKER-035` で詳細設計書の分割、巨大実装文書の責務別分割、主要な型 / 属性 / 処理の日本語注釈追加、試験の確認内容注釈追加を完了した。`TRACKER-037` で点区切り文書名と階層分割の使い分け、注釈付与対象、試験の XML 注釈化方針を明文化し、現状文書を同じ基準へ揃えた。親 Codex は管理者として作業を管理し、実装・設計書作成・試験編集・確認は `gpt-5.5 high` 委譲先に委譲した。 |
