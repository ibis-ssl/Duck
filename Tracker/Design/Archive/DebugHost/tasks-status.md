# 過去の作業状況

この文書は、当時の作業と完了条件を記録した履歴である。現在の状態は[現行の作業一覧](../../tasks-status.md)を参照する。[変更前の原文](../../../../reports/history/debughost-tasks-before-terminology.md)は照合用に保持し、検証結果・条件・対象外事項・完了判断はこの本文にも記載する。

更新規則: この文書は `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` のいずれかを通してのみ更新する。

## 当時の作業状態

設計中の作業は `RAW-VISION-017`、規模は小。前提、対象外条件、設計・レビューの完了条件、調査・引き継ぎレポートは[作業本文](#raw-vision-017)を参照する。

## 固定作業

課題 #10 の固定一覧は `RAW-VISION-013`、`RAW-VISION-014`、`RAW-VISION-015`、`RAW-VISION-016`、`RAW-VISION-017`、`RAW-VISION-018`、`RAW-VISION-019`、`RAW-VISION-020` とした。進捗管理では補助番号を使わない。

## 作業一覧

| ID | 作業 | 工程 | 当時の状態 | 依存関係 | 条件と検証の本文 |
| --- | --- | --- | --- | --- | --- |
| `RAW-VISION-000` | 設計と進捗管理の文書を作成する | 準備 | 完了 | 利用者の計画 | [履歴](#raw-vision-000) |
| `RAW-VISION-001` | UDP受信とパケットの保存を実装する | 実装 | 完了 | `RAW-VISION-000` | [履歴](#raw-vision-001) |
| `RAW-VISION-002` | フィールド座標を描画座標へ変換する | 実装 | 完了 | `RAW-VISION-001` | [履歴](#raw-vision-002) |
| `RAW-VISION-003` | Blazorでraw visionを表示する | 実装 | 完了 | `RAW-VISION-001`, `RAW-VISION-002` | [履歴](#raw-vision-003) |
| `RAW-VISION-004` | 保存と座標変換のテストを追加する | 検証 | 完了 | `RAW-VISION-001`, `RAW-VISION-002` | [履歴](#raw-vision-004) |
| `RAW-VISION-005` | テスト・ビルド・レビューを実施する | 検証 | 完了 | `RAW-VISION-003`, `RAW-VISION-004` | [履歴](#raw-vision-005) |
| `RAW-VISION-006` | マルチキャスト受信の初期化を改善する | 検証 | 完了 | `RAW-VISION-001` | [履歴](#raw-vision-006) |
| `RAW-VISION-007` | raw visionの集約表示とカメラ別表示を追加する | 実装 | 完了 | `RAW-VISION-001`, `RAW-VISION-003` | [履歴](#raw-vision-007) |
| `RAW-VISION-008` | フィールドを広く表示できるようraw visionのレイアウトを整理する | レビュー | 完了 | `RAW-VISION-007` | [履歴](#raw-vision-008) |
| `RAW-VISION-009` | render snapshotのフィールドと詳細の表示比率をドラッグで変更する | レビュー | 完了 | `RAW-VISION-008` | [履歴](#raw-vision-009) |
| `RAW-VISION-010` | 診断画面の時系列表示の幅をドラッグで変更する | レビュー | 完了 | `RAW-VISION-009` | [履歴](#raw-vision-010) |
| `RAW-VISION-011` | 画面移動用の共通メニューを表示画面と同じ外観にする | レビュー | 完了 | `RAW-VISION-010` | [履歴](#raw-vision-011) |
| `RAW-VISION-012` | 診断再生の開始・停止・早送りを追加する | レビュー | 完了 | `RAW-VISION-010` | [履歴](#raw-vision-012) |
| `RAW-VISION-013` | Vision split / overlayの同時取得方針と設計を確定する | 設計 | 完了 | `RAW-VISION-012` | [履歴](#raw-vision-013) |
| `RAW-VISION-014` | Vision split / overlayと診断再生の時刻同期をTDDで固定する | 検証 | 完了 | `RAW-VISION-013` | [履歴](#raw-vision-014) |
| `RAW-VISION-015` | Vision split / overlayのUIとライブ表示のsource snapshotを接続する | 実装 | 完了 | `RAW-VISION-014` | [履歴](#raw-vision-015) |
| `RAW-VISION-016` | 最終検証・文書・レビューをそろえ、PR #15をレビュー可能な状態にする | レビュー | 完了 | `RAW-VISION-015` | [履歴](#raw-vision-016) |
| `RAW-VISION-017` | 診断保存処理の分離に向けて進捗と設計を更新する | 設計 | 進行中 | `RAW-VISION-016` | [履歴](#raw-vision-017) |
| `RAW-VISION-018` | 診断の保存周期と最新スナップショットの境界をTDDで固定する | 検証 | 未着手 | `RAW-VISION-017` | [履歴](#raw-vision-018) |
| `RAW-VISION-019` | 診断保存をトラッカーの周期処理から分離する | 実装 | 未着手 | `RAW-VISION-018` | [履歴](#raw-vision-019) |
| `RAW-VISION-020` | 診断保存の分離を検証し、レビューとPR準備を完了する | レビュー | 未着手 | `RAW-VISION-019` | [履歴](#raw-vision-020) |

## 作業別の履歴

### `RAW-VISION-000`

設計と進捗管理の文書を作成する。

利用者の計画に基づき、設計用ディレクトリ、設計計画、作業と工程の進捗管理文書を作成した。

### `RAW-VISION-001`

UDP受信とパケットの保存を実装する。

受信設定、常駐する受信処理、保存状態のスナップショット、パケットのデコード成功の扱い、エラーの集計を実装した。

### `RAW-VISION-002`

フィールド座標を描画座標へ変換する。

field geometry の寸法と、形状情報がない場合に使う代替の寸法を、SVG の座標へ正しく対応付けた。

### `RAW-VISION-003`

Blazorでraw visionを表示する。

トップページに状態、フィールドの SVG、表、未加工の JSON、必要な画面へ移動するためのリンクを表示する。

### `RAW-VISION-004`

保存と座標変換のテストを追加する。

保存処理と座標変換の期待する動作を確認できる、意味のある条件のテストを追加した。

### `RAW-VISION-005`

テスト・ビルド・レビューを実施する。

テストとビルドのコマンドが成功し、レビュー結果を下記のレポートに記録した。

記録の保存先:

- `reports/raw-vision-viewer-evidence-20260430165645.md`

### `RAW-VISION-006`

マルチキャスト受信の初期化を改善する。

利用可能な IPv4 の通信アドレスでマルチキャストグループへの参加を試みる。通信に使う NIC を明示指定した場合は、その指定の妥当性を検証する。テストとビルドは成功し、レビュー結果を下記に記録した。

記録の保存先:

- `reports/raw-vision-multicast-join-evidence-20260430174124.md`

### `RAW-VISION-007`

raw visionの集約表示とカメラ別表示を追加する。

カメラごとに最新の観測フレームを保存し、集約した表示とカメラ別の表示を UI から選べるようにした。source selector とフィールド描画は `ssl-vision-client` の操作・表示に合わせる。テストとビルドは成功し、検証とレビューを下記に記録した。

記録の保存先:

- `reports/raw-vision-source-selector-evidence-20260430181252.md`

### `RAW-VISION-008`

フィールドを広く表示できるようraw visionのレイアウトを整理する。

画面ヘッダーを小さくし、source selector をフィールド上部から移動した。座標軸とカーソルをフィールドへ重ねて表示し、デスクトップではサイドバーを折り畳めるようにした。`dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` は成功し、別担当のレビューは指摘なし。

記録の保存先:

- `reports/raw-vision-008-review-20260501101437.md`

### `RAW-VISION-009`

render snapshotのフィールドと詳細の表示比率をドラッグで変更する。

`/diagnostics` の render snapshot 表示で、フィールドと詳細領域の境界をドラッグして表示比率を変更できるようにした。4Kの表示領域でもフィールドを大きく表示できる。詳細領域の最低限の表示とスクロールを維持し、高さの変更の境界値テストと `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` は成功した。PR は `https://github.com/ibis-ssl/Duck/pull/7`。

記録の保存先:

- `reports/raw-vision-009-evidence-20260511231841.md`
- `reports/raw-vision-009-review-20260511231841.md`

### `RAW-VISION-010`

診断画面の時系列表示の幅をドラッグで変更する。

当時の規模: 小。

`/diagnostics` 左側の時系列表示と右側の詳細領域との境界をドラッグし、時系列表示の幅を変更できるようにした。左側を狭くしてフィールドと詳細の表示領域を広げても、最小幅での追跡フレームの選択操作は維持する。長い文字列は省略表示する。幅の境界値を単体テストで確認し、`dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` は成功した。初回から r3 までのレビューは下記に記録した。PR は `https://github.com/ibis-ssl/Duck/pull/7`。

記録の保存先:

- `reports/raw-vision-010-evidence-20260511233242.md`
- `reports/raw-vision-010-review-20260511233242.md`
- `reports/raw-vision-010-review-r2-20260511233631.md`
- `reports/raw-vision-010-review-r3-20260511234000.md`

### `RAW-VISION-011`

画面移動用の共通メニューを表示画面と同じ外観にする。

当時の規模: 小。

旧 `Tracker.Server` の側面メニューとページ一覧を、raw vision と診断画面の濃い緑色の UI と同じ配色・表示密度にそろえた。選択中、マウスを重ねた状態、折り畳んだ状態を視覚的に統一し、既存の画面移動の操作は維持する。モバイルでのメニュー切り替え操作も維持し、既定のテンプレートが画面から浮いて見えるような外観を改めた。`dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` は成功した。

記録の保存先:

- `reports/raw-vision-011-evidence-20260512001259.md`
- `reports/raw-vision-011-review-20260512001259.md`

### `RAW-VISION-012`

診断再生の開始・停止・早送りを追加する。

`/diagnostics` の timeline scrubber 付近へ、再生、停止、早送りの操作を追加した。通常再生はログの時刻差に従って追跡フレームを順方向へ進め、末尾に到達すると停止して先頭の記録へ戻る。再生中・早送り中はそれぞれのボタンが停止ボタンに切り替わり、停止を押した場合は現在の選択位置を維持する。

ログの切り替え、記録がない場合、停止直後に届く古い更新でも状態の不整合を起こさない。再生位置、更新間隔、古い更新を無視する条件のテストと `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` は成功した。初回から r5 までのレビューを下記に記録した。

記録の保存先:

- `reports/raw-vision-012-evidence-20260512002100.md`
- `reports/raw-vision-012-review-20260512002100.md`
- `reports/raw-vision-012-review-r2-20260512002502.md`
- `reports/raw-vision-012-review-r3-20260512002923.md`
- `reports/raw-vision-012-review-r4-20260512003653.md`
- `reports/raw-vision-012-review-r5-20260512004014.md`

### `RAW-VISION-013`

Vision split / overlayの同時取得方針と設計を確定する。

当時の規模: 小。

課題 #10 での利用者の確認に基づき、表示元候補を `Raw Aggregate`、`Raw Camera`、`Tracked`、3rd party tracker とした。ライブ表示の比較では、パケットの時刻が厳密に同じであることを求めず、同じ UI render tick で内容を固定したスナップショットを比較する。

diagnostics replay と比較では selected replay timeline tick を移動しない。対象の表示元の記録がない場合は、同じ表示元から選択時点以前に取得した latest-before snapshot を保持して表示・比較する。形状は raw geometry を優先し、存在しない場合だけ追跡結果の形状を使う。3rd party tracker のパケットから形状を復元しない。

分割・重ね合わせ表示の詳細、凡例、Layer A/B の表示有無、same-source を1層として扱うこと、片側が欠損しても表示できる側を残すことは、診断画面の操作・表示にそろえる。固有名称の説明を `raw-vision-viewer-plan.md` の脚注へ統合した。調査、設計、r2、`gpt-5.5 high` の設計レビューを記録し、完了を妨げる指摘なしを確認して PR #15 で公開した。用語説明を脚注へ統合した後の r3 レビューも指摘なし。

記録の保存先:

- `reports/issue-10-vision-overlay-investigation-20260514080106.md`
- `reports/issue-10-live-same-tick-investigation-20260514081135.md`
- `reports/issue-10-diagnostics-time-sync-audit-20260514081730.md`
- `reports/issue-10-vision-overlay-design-20260514082233.md`
- `reports/issue-10-vision-overlay-design-r2-20260514082755.md`
- `reports/issue-10-raw-vision-013-design-review-20260514083515.md`
- `reports/issue-10-design-terminology-review-r3-20260514103027.md`

### `RAW-VISION-014`

Vision split / overlayと診断再生の時刻同期をTDDで固定する。

当時の規模: 小。

分割・重ね合わせの表示モード、表示元の選択、3rd party tracker、同じ UI render tick のスナップショット、raw geometry の優先、Layer A/B の表示有無を単体テストで固定した。overlay layer contract、geometry contract、same-source は1層にまとめること、片側が欠損しても表示可能な側を残すこと、診断画面に合わせた凡例と詳細の表示も対象に含める。

selected replay timeline tick に対象の表示元の alignment record がない場合も、選択した時点と時刻は表示元ごとに動かさない。同じ表示元の選択時点以前の latest-before snapshot を、直前の状態として表示・比較する。対応付けの規則、表示元の `receivedAt`、選択時点との時刻差、古い状態であることと latest-before snapshot を使ったことの判定情報を確認する。

選択時点以前の同じ表示元のスナップショットが一切ない場合だけ、CandidateMissing / NoCandidateSnapshot に相当する状態とする。選択時点より後の記録を代用しない。実装前の失敗確認では37件中26件成功・11件失敗だった。失敗の内訳はライブ表示の比較 API が未実装の9件、診断再生の latest-before snapshot と後続記録を代用しない処理が未実装の2件。r4 の `gpt-5.5 high` レビューは完了を妨げる指摘なし。

記録の保存先:

- `reports/issue-10-raw-vision-014-tdd-contract-20260514084547.md`
- `reports/issue-10-raw-vision-014-tdd-review-20260514085339.md`
- `reports/issue-10-raw-vision-014-tdd-fix-20260514085712.md`
- `reports/issue-10-raw-vision-014-tdd-review-r2-20260514090315.md`
- `reports/issue-10-raw-vision-014-tdd-fix-r2-20260514090645.md`
- `reports/issue-10-raw-vision-014-tdd-review-r3-20260514091311.md`
- `reports/issue-10-raw-vision-014-tdd-fix-r3-20260514091616.md`
- `reports/issue-10-raw-vision-014-tdd-review-r4-20260514092124.md`

### `RAW-VISION-015`

Vision split / overlayのUIとライブ表示のsource snapshotを接続する。

当時の規模: 中。

`Vision` 画面で左右分割と重ね合わせの表示モードを切り替え、Layer A/B の表示元と表示の有無を診断画面に近い UI で操作できるようにした。`Raw` / `Tracked` / 3rd party tracker の同じ UI render tick で内容を固定したスナップショットを描画し、既存の `Raw` / `Tracked` の単独表示を維持する。

3rd party tracker は `MultiTrackerManager<TrackerPacketAdapter>` から、内容を固定して保持する保存処理と表示の合成処理を通して接続する。形状は raw geometry を優先し、存在しない場合だけ追跡結果の形状を使う。外部トラッカーのパケットから形状を復元しない。

診断再生では selected replay timeline tick と選択時刻を固定する。対象の表示元の対応記録がないときは、同じ表示元の選択時点以前の latest-before snapshot を保持して表示・比較し、それより後の記録を代用しない。`RAW-VISION-014` の対象契約テスト37件と `Tracker/Tracker.Server/Tracker.Server.csproj` のビルドは成功した。実装・修正・初回と r2 のレビューを下記に記録し、r2 は指摘なし。

記録の保存先:

- `reports/issue-10-raw-vision-015-implementation-20260514092635.md`
- `reports/issue-10-raw-vision-015-fix-20260514094259.md`
- `reports/issue-10-raw-vision-015-review-20260514093808.md`
- `reports/issue-10-raw-vision-015-review-r2-20260514095053.md`

### `RAW-VISION-016`

最終検証・文書・レビューをそろえ、PR #15をレビュー可能な状態にする。

検証結果、設計用語の脚注への統合と r3 の確認、`gpt-5.5 high` の最終レビューを記録し、指摘なしを確認した。PR #15 の本文も同期した。重ね合わせ表示の Layer A/B の色分け、ドラッグ時の同期、ライブ表示と診断画面の分割・重ね合わせ表示のフィールド描画の整合も確認し、完了を妨げる指摘はなかった。

当時の最終検証では対象テスト37件と旧 `Tracker.Server` のビルドが成功し、トップページと `/diagnostics` の応答を確認した。ただし、受信パケット、capture metadata 付きの診断ログ、ブラウザの自動操作環境がなかったため、重ね合わせ表示の実操作と、latest-before snapshot を使ったことを示す情報の実画面表示は未確認だった。この制約は最終レビューでも残るリスクとして保持され、当時の下書き解除を妨げる指摘にはしなかった。

確認用サーバーの停止は最初の検証時点では未確認だったが、最終レビューでは通信ポート 18160の待ち受けがないことを確認した。初回の未確認事項と、その後の確認結果を区別する。

ライブ表示の 3rd party tracker は UUID を優先して同じ表示元をまとめる処理を実装した。実装前の失敗と実装後の成功、診断画面の調査を実装レポートに記録し、`gpt-5.5 high` のレビューは指摘なし。`VisionLiveComparisonViewStateTests` 13件、`TrackerDiagnosticsComparisonViewStateTests` 28件、旧 `Tracker.Server` のビルドは成功した。

PR #15 は `2026-05-14T03:29:25Z` に統合済み。統合コミット `785827c62f5f58229f2a2d1e51db0fe529f46cc8` は、当時の `main` と `origin/main` と一致していた。現在の `main` の状態を示す記述ではない。

記録の保存先:

- `reports/issue-10-raw-vision-016-validation-20260514095659.md`
- `reports/issue-10-design-terminology-audit-r3-20260514102338.md`
- `reports/issue-10-design-terminology-review-r3-20260514103027.md`
- `reports/issue-10-raw-vision-016-final-review-20260514103501.md`
- `reports/issue-10-overlay-color-review-20260514110200.md`
- `reports/issue-10-field-render-alignment-review-20260514114210.md`
- `reports/issue-10-third-party-uuid-aggregate-implementation-20260514120949.md`
- `reports/issue-10-third-party-uuid-aggregate-review-20260514122150.md`

### `RAW-VISION-017`

診断保存処理の分離に向けて進捗と設計を更新する。

当時の規模: 小。

当時の状態は設計中。`RAW-VISION-016` の完了と、診断保存処理の分離に関する調査・引き継ぎレポートを前提とする。PR #15 統合後の完了状態を `tasks-status.md` / `phases-status.md` に同期する。

`Tracker.RuntimeHost` をトラッカーと将来の AutoRef を動かす通常運用向けの画面なし実行体、当時の `Tracker.Server` をデバッグ・診断用の Web UI と位置付ける。AutoRef 自体の実装は対象外。設計資料を `Tracker/Design/` に移し、中核処理、デバッグ・診断、通常運用の実行体という範囲をフォルダで分ける。

`raw-vision-viewer-plan.md` に、トラッカーの周期処理、サーバーのライブ表示、診断保存・再生の周期処理を分離する方針を追記する。追跡フレームの確定周期で保存した render snapshot から `Vision Input` を復元する従来の方式は旧形式として扱い、互換性は要件としない。新しいキャプチャーでは diagnostics sample tick で固定した raw vision と最新の tracker snapshot を、性能を優先して保存・再生する。

固有名称の説明は既存形式の脚注へ追加する。`gpt-5.5 high` の設計レビューを `reports/` に残し、完了を妨げる指摘がないことを確認する。これらは当時の完了条件であり、この履歴の修正によって実装済みに変更しない。

記録の保存先:

- `reports/issue-10-diagnostics-loop-isolation-investigation-20260514150641.md`
- `reports/issue-10-diagnostics-loop-isolation-handover-20260514151049.md`

### `RAW-VISION-018`

診断の保存周期と最新スナップショットの境界をTDDで固定する。

当時の状態は未着手。診断再生の `Vision Input` を、追跡フレームの確定周期に依存せず、raw vision と最新のスナップショットを取得する周期で保存・再生できることを、実装前に失敗するテストとして固定する。選択時点より後の記録を代用せず、各表示元の時刻、選択時点との時刻差、古い記録であることの判定情報を保持する。既存のライブ表示の same render tick の契約も維持する。

### `RAW-VISION-019`

診断保存をトラッカーの周期処理から分離する。

当時の状態は未着手。新しいキャプチャーでは、トラッカーの周期処理から render snapshot を直接保存する方式を置き換える。別の周期処理が最新の raw vision、自前トラッカー、外部トラッカーのスナップショットを読み取り、診断保存、保存時の対応付け、再生へ接続する。対象テストと `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` を成功させ、実装レポートを `reports/` に残すことを完了条件とした。

### `RAW-VISION-020`

診断保存の分離を検証し、レビューとPR準備を完了する。

当時の状態は未着手。指定されたキャプチャーまたは同等のログを使い、raw vision と最新のスナップショットを取得する周期、および再生時の `Vision Input` の更新周期の改善を説明できる証拠を残す。再発防止テスト・対象テストとサーバーのビルドを成功させる。`gpt-5.5 high` の専任担当によるレビューを `reports/` に保存し、進捗を完了状態へ同期してコミットし、PR をレビュー可能な状態にする。
