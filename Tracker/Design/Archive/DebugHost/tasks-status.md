# 作業状態

この文書は過去の作業記録の要約です。要約前の条件・検証結果・参照先は[変更前の原文](../../../../reports/history/debughost-tasks-before-terminology.md)に省略せず保存しています。原文の基準は `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4` です。現在の作業状態は[現行の作業一覧](../../tasks-status.md)を参照してください。

更新規則: この文書は `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` のいずれかを通してのみ更新する。

## 現在の作業

- ID: `RAW-VISION-017`
- 表題: 診断処理周期分離の進捗再同期と設計追補を完了する
- 工程: 設計
- 状態: 進行中
- 規模: 小
- 依存関係: `RAW-VISION-016` 完了、診断処理周期分離の調査報告、引き継ぎ報告
- 完了条件:
  - PR #15 統合後の `RAW-VISION-016` 完了状態を進捗管理へ同期する。
  - `Tracker.RuntimeHost` を本番寄りの画面なし実行体、現 `Tracker.Server` を診断用画面として位置づける。
  - 設計資料を `Tracker/Design/` 配下へ移動し、範囲を内部保管先で分ける。
  - 処理周期分離、diagnostics sample tick、旧形式互換を非要件とする性能優先方針、固有名詞脚注を設計へ反映する。
  - `gpt-5.5 high` の設計確認を報告へ残し、重大指摘がないことを確認する。

## 課題 #10 固定残作業

- 固定一覧は `RAW-VISION-013`、`RAW-VISION-014`、`RAW-VISION-015`、`RAW-VISION-016`、`RAW-VISION-017`、`RAW-VISION-018`、`RAW-VISION-019`、`RAW-VISION-020` とする。
- `RAW-VISION-013`: 分割表示と重ね合わせ表示の同時取得方針と設計を確定した。
- `RAW-VISION-014`: 分割表示、重ね合わせ表示、診断時刻同期の TDD 契約を追加した。
- `RAW-VISION-015`: 分割表示、重ね合わせ表示、実行中の source snapshot 接続を実装した。
- `RAW-VISION-016`: 最終検証、文書、確認、PR #15 準備完了化を完了した。PR #15 は統合済み。
- `RAW-VISION-017`: 診断処理周期分離の進捗再同期と設計追補を進行中。
- `RAW-VISION-018`: 診断標本周期と最新スナップショット境界の TDD 契約を追加する。
- `RAW-VISION-019`: 診断記録保存周期分離を実装する。
- `RAW-VISION-020`: 証跡、確認、進捗同期、PR 準備完了を完了する。

## 完了した追加作業

- `RAW-VISION-010`: 診断描画単位時系列の幅をつまみ操作で変更可能にした。
- `RAW-VISION-011`: `Tracker.Server` 共通案内の見た目を表示画面 UI と揃えた。

## 直近完了作業

- `RAW-VISION-015`: 分割表示、重ね合わせ表示、実行中の source snapshot 接続を実装し、対象契約試験と実行体構築、再確認まで完了した。
- `RAW-VISION-014`: 分割表示、重ね合わせ表示、診断時刻同期の TDD 契約を追加し、失敗試験と確認を記録済み。
- `RAW-VISION-013`: 課題 #10 の表示元候補、同時取得方針、診断時刻同期、フィールド形状方針、固有名詞説明を設計へ固定した。

## 作業一覧

| ID | 作業 | 工程 | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| `RAW-VISION-000` | 設計と進捗管理文書を作成する | 準備 | 完了 | 利用者計画 | 設計、計画、作業進捗、工程進捗の文書が存在する。 |
| `RAW-VISION-001` | UDP 受信器と通信内容保存を実装する | 実装 | 完了 | `RAW-VISION-000` | 受信設定、常駐処理、保存、復号成功、異常集計を実装済み。 |
| `RAW-VISION-002` | フィールド投影を実装する | 実装 | 完了 | `RAW-VISION-001` | フィールド寸法と代替寸法を座標へ正しく対応させた。 |
| `RAW-VISION-003` | Blazor raw vision UI を実装する | 実装 | 完了 | `RAW-VISION-001`, `RAW-VISION-002` | 状態、フィールド表示、表、未加工 JSON、案内を表示する。 |
| `RAW-VISION-004` | 試験を追加する | 検証 | 完了 | `RAW-VISION-001`, `RAW-VISION-002` | 保存と投影の試験が存在する。 |
| `RAW-VISION-005` | 検証と確認を完了する | 検証 | 完了 | `RAW-VISION-003`, `RAW-VISION-004` | 試験、構築、確認報告まで完了。 |
| `RAW-VISION-006` | 多宛先通信受信器の初期化を堅牢化する | 検証 | 完了 | `RAW-VISION-001` | 多宛先通信参加、接続面検証、試験、構築、確認まで完了。 |
| `RAW-VISION-007` | 集約表示と撮影元別の raw vision 表示を追加する | 実装 | 完了 | `RAW-VISION-001`, `RAW-VISION-003` | 撮影元別保存、集約表示、撮影元別表示、試験、構築、確認まで完了。 |
| `RAW-VISION-008` | フィールド優先の raw vision 配置と重ね合わせ表示を小型化する | 確認 | 完了 | `RAW-VISION-007` | 小型見出し、表示元選択配置、重ね描画、側面欄、構築、確認まで完了。 |
| `RAW-VISION-009` | render snapshot のフィールドと詳細の比率を変更可能にする | 確認 | 完了 | `RAW-VISION-008` | 境界つまみ操作、4K 表示領域、境界値試験、構築、確認まで完了。 |
| `RAW-VISION-010` | 診断描画単位時系列の幅を変更可能にする | 確認 | 完了 | `RAW-VISION-009` | 境界つまみ操作、最小幅、境界値試験、構築、確認まで完了。 |
| `RAW-VISION-011` | `Tracker.Server` 共通案内の見た目を表示画面 UI と揃える | 確認 | 完了 | `RAW-VISION-010` | 側面案内、画面一覧、選択状態、小画面切替、構築、確認まで完了。 |
| `RAW-VISION-012` | 診断時系列へ再生、停止、早送り操作を追加する | 確認 | 完了 | `RAW-VISION-010` | 再生、停止、早送り、末尾到達、古い時点防止、構築、確認まで完了。 |
| `RAW-VISION-013` | 課題 #10 の同時取得方針と設計を確定する | 設計 | 完了 | `RAW-VISION-012` | 表示元候補、同時取得、診断時刻同期、フィールド形状、確認まで完了。 |
| `RAW-VISION-014` | 分割表示、重ね合わせ表示、診断時刻同期の TDD 契約を追加する | 検証 | 完了 | `RAW-VISION-013` | 契約試験、失敗確認、確認修正、再確認まで完了。 |
| `RAW-VISION-015` | 分割表示、重ね合わせ表示、実行中表示元接続を実装する | 実装 | 完了 | `RAW-VISION-014` | 画面接続、表示元接続、対象契約試験、構築、再確認まで完了。 |
| `RAW-VISION-016` | 最終検証、文書、確認、PR 準備完了を完了する | 確認 | 完了 | `RAW-VISION-015` | 検証、設計用語、最終確認、PR #15 統合まで完了。 |
| `RAW-VISION-017` | 診断処理周期分離の進捗再同期と設計追補を完了する | 設計 | 進行中 | `RAW-VISION-016` | 進捗同期、設計追補、設計確認を完了する。 |
| `RAW-VISION-018` | 診断標本周期と最新スナップショット境界の TDD 契約を追加する | 検証 | 未着手 | `RAW-VISION-017` | 保存と再生が最新スナップショット周期で動くことを失敗試験で固定する。 |
| `RAW-VISION-019` | 診断記録保存周期分離を実装する | 実装 | 未着手 | `RAW-VISION-018` | 別周期の保存、対応付け、再生接続、試験、構築、実装報告を完了する。 |
| `RAW-VISION-020` | 証跡、確認、進捗同期、PR 準備完了を完了する | 確認 | 未着手 | `RAW-VISION-019` | 改善証跡、試験、構築、専用確認、進捗同期、PR 準備完了を完了する。 |

## 詳細な検証・証跡履歴

上の作業一覧は読みやすさのため要約し、代表的な検証結果と報告書参照を以下に示す。省略された詳細は冒頭から参照できる原文に保持する。

- `RAW-VISION-000`: 設計、計画、作業進捗、工程進捗の文書作成を完了。
- `RAW-VISION-001`: UDP 受信設定、常駐処理、保存、復号成功、異常集計を実装。
- `RAW-VISION-002`: フィールド形状と代替寸法を SVG 座標へ対応付け。
- `RAW-VISION-003`: 状態、フィールド表示、表、JSON、案内を表示する画面を実装。
- `RAW-VISION-004`: 保存と投影の試験を追加し、意味のある検証条件を固定。
- `RAW-VISION-005`: 試験と構築に成功し、確認結果を証跡へ記録。
  - 証跡: `reports/raw-vision-viewer-evidence-20260430165645.md`
- `RAW-VISION-006`: 複数の利用可能な IPv4 接続面で多宛先通信参加を試し、明示接続面の検証を追加。試験・構築成功、専用確認記録済み。
  - 証跡: `reports/raw-vision-multicast-join-evidence-20260430174124.md`
- `RAW-VISION-007`: 撮影元ごとの最新状態保持と集約 / 撮影元別表示を実装。試験・構築成功、表示元選択の証跡と確認を記録。
  - 証跡: `reports/raw-vision-source-selector-evidence-20260430181252.md`
- `RAW-VISION-008`: フィールド優先の小型配置、座標・カーソル重ね描画、側面欄折りたたみを追加。`Tracker.Server` 構築成功、確認は指摘なし。
  - 証跡: `reports/raw-vision-008-review-20260501101437.md`
- `RAW-VISION-009`: render snapshot のフィールド / 詳細境界をドラッグ可能化。4K 表示と境界値試験を確認し、`Tracker.Server` 構築成功。
  - 証跡: `reports/raw-vision-009-evidence-20260511231841.md`、`reports/raw-vision-009-review-20260511231841.md`
- `RAW-VISION-010`: 診断時系列幅のドラッグ変更と最小幅動作を境界値試験で確認し、`Tracker.Server` 構築成功。初回から r3 まで確認記録あり。
  - 証跡: `reports/raw-vision-010-evidence-20260511233242.md`、`reports/raw-vision-010-review-20260511233242.md`、`reports/raw-vision-010-review-r2-20260511233631.md`、`reports/raw-vision-010-review-r3-20260511234000.md`
- `RAW-VISION-011`: 共通案内の配色・密度・選択 / 折りたたみ / モバイル操作を維持。`Tracker.Server` 構築成功。
  - 証跡: `reports/raw-vision-011-evidence-20260512001259.md`、`reports/raw-vision-011-review-20260512001259.md`
- `RAW-VISION-012`: timeline scrubber 付近の再生 / 停止 / 早送り、末尾処理、古い時点防止を試験し、`Tracker.Server` 構築成功。初回から r5 まで確認記録あり。
  - 証跡: `reports/raw-vision-012-evidence-20260512002100.md`、`reports/raw-vision-012-review-20260512002100.md`、`reports/raw-vision-012-review-r2-20260512002502.md`、`reports/raw-vision-012-review-r3-20260512002923.md`、`reports/raw-vision-012-review-r4-20260512003653.md`、`reports/raw-vision-012-review-r5-20260512004014.md`
- `RAW-VISION-013`: 表示元候補、同一 UI render tick、diagnostics replay の選択時点以前の最新記録を使う方針、field geometry 基準を設計。専用設計確認は阻害指摘なし、用語脚注の再確認も指摘なし。
  - 証跡: `reports/issue-10-design-terminology-review-r3-20260514103027.md`、`reports/issue-10-diagnostics-time-sync-audit-20260514081730.md`、`reports/issue-10-live-same-tick-investigation-20260514081135.md`、`reports/issue-10-raw-vision-013-design-review-20260514083515.md`、`reports/issue-10-vision-overlay-design-20260514082233.md`、`reports/issue-10-vision-overlay-design-r2-20260514082755.md`、`reports/issue-10-vision-overlay-investigation-20260514080106.md`
- `RAW-VISION-014`: TDD の失敗確認で 37 件中 26 件成功 / 11 件失敗を確認。失敗内訳はライブ表示の比較 API 未実装 9 件、診断再生の直前記録利用 / 後続記録の代用禁止が未実装 2 件。r4 確認は阻害指摘なし。
  - 証跡: `reports/issue-10-raw-vision-014-tdd-contract-20260514084547.md`、`reports/issue-10-raw-vision-014-tdd-fix-20260514085712.md`、`reports/issue-10-raw-vision-014-tdd-fix-r2-20260514090645.md`、`reports/issue-10-raw-vision-014-tdd-fix-r3-20260514091616.md`、`reports/issue-10-raw-vision-014-tdd-review-20260514085339.md`、`reports/issue-10-raw-vision-014-tdd-review-r2-20260514090315.md`、`reports/issue-10-raw-vision-014-tdd-review-r3-20260514091311.md`、`reports/issue-10-raw-vision-014-tdd-review-r4-20260514092124.md`
- `RAW-VISION-015`: 対象契約テスト 37 件と `Tracker.Server` 構築成功。表示元の対応付け欠落時は同一表示元の latest-before snapshot を使い、選択時点より後のスナップショットを代用しない契約を維持。r2 確認は指摘なし。
  - 証跡: `reports/issue-10-raw-vision-015-fix-20260514094259.md`、`reports/issue-10-raw-vision-015-implementation-20260514092635.md`、`reports/issue-10-raw-vision-015-review-20260514093808.md`、`reports/issue-10-raw-vision-015-review-r2-20260514095053.md`
- `RAW-VISION-016`: 用語監査 / 再確認、最終確認、Layer A/B 色分け、フィールド描画整合、3rd party tracker UUID 優先統合を確認。`VisionLiveComparisonViewStateTests` 13 件、`TrackerDiagnosticsComparisonViewStateTests` 28 件、`Tracker.Server` 構築成功。PR #15 は 2026-05-14T03:29:25Z に統合済みで、統合時点の変更記録 `785827c62f5f58229f2a2d1e51db0fe529f46cc8` は手元 / 遠隔の `main` と一致。
  - 証跡: `reports/issue-10-design-terminology-audit-r3-20260514102338.md`、`reports/issue-10-design-terminology-review-r3-20260514103027.md`、`reports/issue-10-field-render-alignment-review-20260514114210.md`、`reports/issue-10-overlay-color-review-20260514110200.md`、`reports/issue-10-raw-vision-016-final-review-20260514103501.md`、`reports/issue-10-raw-vision-016-validation-20260514095659.md`、`reports/issue-10-third-party-uuid-aggregate-implementation-20260514120949.md`、`reports/issue-10-third-party-uuid-aggregate-review-20260514122150.md`
- `RAW-VISION-017`: 進行中。調査・引き継ぎ証跡を保持し、設計確認で阻害指摘がないことを完了条件としている。
  - 証跡: `reports/issue-10-diagnostics-loop-isolation-handover-20260514151049.md`、`reports/issue-10-diagnostics-loop-isolation-investigation-20260514150641.md`
- `RAW-VISION-018`: 未着手。diagnostics sample tick を raw vision / 最新 tracker snapshot 周期で保存・再生し、後続記録の代用を禁止する失敗テストを先に固定する計画。
- `RAW-VISION-019`: 未着手。新規記録でトラッカーの周期処理に直結した render snapshot 保存を置き換え、別の周期処理へ分離する計画。対象限定試験と `Tracker.Server` 構築成功、実装報告作成が完了条件。
- `RAW-VISION-020`: 未着手。提供記録相当の改善証跡、回帰 / 対象限定試験、サーバー構築、専用確認、進捗同期、PR 準備完了が完了条件。
