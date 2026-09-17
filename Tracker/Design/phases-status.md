# 工程の状況

規則: この文書は `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 全体状況

- 現在の段階: 設計書・READMEのレビュー指摘対応中、PR #20 公開中。製品実装と試験実装の修正は別分岐へ分離済み。
- 現在の作業: `DOC-LINT-003`
- 残りの作業: 全出現箇所の最終照合、履歴本文の再確認、承認済みの直接引用の例外機能の取り込み、全体の文書検査、独立最終レビュー。
- 証跡: 再開時は `reports/doc-lint-resume-20260915.md`、診断設計の追加照合は `reports/task-doc-lint-003-diagnostic-scope-followup-20260917082311.md` を参照する。
- 2026-09-15 当時の本文修正: 設計書7文書を文書ごとの7回の変更で PR #20 へ反映した。この時点の本文修正では、製品のソースコードと許可一覧は変更していなかった。
- 過去の検証: レビュー前の `42e0637` では、2026-09-16 に全19文書の `npm run lint:md` が終了値0だった。`cspell` 0件、許可一覧違反0件、`textlint` 成功。これは現在の `HEAD`の成功証拠ではない。詳細は `reports/markdown-lint-completion-20260916.md` を参照する。

## 工程一覧（個別の完了記録は当時の状態）

| 工程 | 状態 | 完了条件 |
| --- | --- | --- |
| 準備 | 完了 | 旧 `Tracker.Core/Design` と `Tracker.Server/Design` の設計資料を確認し、`Tracker/Design/Archive/` に旧進捗文書を保存した。 |
| 設計 | 完了、下書き PR #17 | `Tracker/Design/` を設計資料の正本の保存先とし、`Tracker.Core` / `Tracker.DebugHost` / `Tracker.RuntimeHost` の設計範囲をフォルダで分けた。RuntimeHost はトラッカーと将来の自動レフェリーを持つ本番寄りの画面なし実行用、DebugHost は Web UI / 診断 / 再生 / キャプチャー確認用とし、処理周期の分離と旧ログとの互換性を必須としない方針を固定した。`reports/runtime-host-001-design-review-r2-20260514160734.md` で阻害指摘なしを確認した。 |
| 検証 | 完了、下書き PR #17 | `RUNTIME-HOST-002` / `RUNTIME-HOST-003` で、依存境界、読み取り側の責務、diagnostics sample tick の境界、旧形式の機能制限について、実装に先行する失敗テストを追加した。前者は2回目のレビュー、後者は `reports/runtime-host-003-review-20260514170652.md` で阻害指摘なしを確認した。各3件失敗 / 0件成功という当時の結果は、作業状況の各本文に保持している。 |
| 実装 | 完了、下書き PR #17 | `RUNTIME-HOST-004` から `RUNTIME-HOST-009` で、DebugHost への名称変更、共通の周期処理の責務分離、ライブ表示の読み取り側への分離、diagnostics sample sidecar の高速な読み書き、RuntimeHost の最小構成と正常経路を実装し、対象テスト、ビルド、作業ごとのレビューを行った。各作業のレビュー結果は `reports/runtime-host-004-review-20260514172921.md`、`reports/runtime-host-005-review-20260514180308.md`、`reports/runtime-host-006-review-20260514182549.md`、`reports/runtime-host-007-review-r4-20260514192425.md`、`reports/runtime-host-008-review-r2-20260514194042.md`、`reports/runtime-host-009-review-r2-20260514200945.md` で指摘なし。作業008・009で広い範囲のテストに各1件失敗が残った条件と保留理由は、作業状況の各本文に保持している。 |
| 確認 | 完了、PR #17 提出可能 | `RUNTIME-HOST-010` で両プロジェクトのビルド、diagnostics sample sidecar と旧形式の機能制限の証跡、DebugHost の UI と RuntimeHost の画面なしの正常動作を検証し、作業ごとのレビューを完了した。`RUNTIME-HOST-011` では最終レビューの阻害指摘を受け、リポジトリに残る失敗した契約テストを当時の設計へ修正した。`reports/runtime-host-011-final-review-r2-20260514204526.md` で阻害指摘なしとなり、PR を提出できると確認した。 |
| キャプチャー再生の調査 | 当時は PR #19 公開中 | `CAPTURE-REPLAY-001` で raw vision と `ibis tracker` の周期、`ReceivedAt` を基準とする遅延を比較する汎用出力を追加した。指定キャプチャーの原因は `reports/capture-replay-001-latency-investigation-20260516185833.md` に記録した。対象テスト11件成功、CaptureReplay のビルド成功。専用レビューは `reports/pr19-review-capturereplay-20260516200807.md`、文書と進捗のレビューは `reports/pr19-review-docs-tracking-20260516200807.md` で阻害指摘なし。 |
| 起動時の設定選択 | 当時は PR #19 公開中 | `RUNTIME-HOST-012` で、起動時に `--profile <name>` / `--profile=<name>` から適用する設定プロファイルを指定できるようにした。`Microsoft.Extensions.Configuration.CommandLine` の設定読込機能と、引数名から設定キーへの対応表を使う。初回レビューの重大指摘を修正した後の対象テスト17件成功、RuntimeHost のビルド成功。`reports/pr19-review-runtimehost-profile-r2-20260516201757.md` で指摘なし。 |
| 文書検査整備 | 指摘対応中、PR #20 公開中 | `DOC-LINT-001` でリポジトリの最上位に文書用の `textlint` / `cspell` を導入し、利用者の編集対象である `*.md` 全般を品質検査の対象にした。英単語と片仮名語の許可一覧は`tools/lint/markdown-whitelist.yaml` の1ファイルを正本とする。`DOC-LINT-002` で SudachiPy による語彙抽出と許可一覧検査を追加した。`DOC-LINT-003` は検査器の長文入力、脚注、語境界、日本語に隣接する英語、対象列挙の欠陥を修正し、未承認語の一括許可や検査除外への退避をせずに説明文を整理する。全対象の `npm run lint:md` の終了値0と、PRの最新の `HEAD`に一致する `head_sha` のCI成功、および検証証跡の記録を完了条件とする。本文の照合と全体完了は区別し、独立最終レビューの代わりに自己点検を使わない。 |

## 文書検査の補足（2026-09-17）

`DOC-LINT-003` のMarkdown差分と検査範囲を照合し、検査手順の実行例を訂正した。原文の位置情報と履歴保存版の一致を確認したが、全出現箇所の最終照合、全体の文書検査、独立最終レビューの完了とは区別する。詳細は `reports/task-doc-lint-003-scope-check-20260917082040.md` を参照する。

## 追跡エンジンの照合記録（2026-09-17）

追跡エンジンの詳細設計について、原文133出現と最終表現の対応を記録し、`summary` 要素の指定を復元した。文脈を読んだ86行の判断と、入力番号・時刻・初期化対象・追跡の確立条件を実装と照合した証拠は、`reports/task-doc-lint-003-engine-correspondence-20260917084612.md` に記録した。全出現箇所の最終台帳、全体の文書検査、独立最終レビューは未完了のままである。

## 独立最終レビュー F1 対応（2026-09-17）

独立最終レビューで、diagnostics sample sidecar の現在仕様と3文書の説明に不整合があるF1が見つかり、完了判定は保留となった。構成設計、DebugHost詳細設計、DebugHost READMEを実装へ同期し、新規記録の再生位置と `Vision Input` / `ibis tracker` の復元元を diagnostics sample sidecar に統一した。外部トラッカーは tracker packet snapshot と alignment sidecar、または latest-before snapshot を使い、render snapshot は旧形式・補助用途として区別する。

関連する原出現台帳と変更単位の台帳を修正後本文へ同期し、独立レビュー対象8文書の2,670件について構造自己点検は欠落・重複・本文内容不一致0。対象3文書の検査と `RuntimeHostDiagnosticsSampleBoundaryContractTests` も終了値0。修正後の独立再レビューは未実施であり、工程は引き続き「指摘対応中」とする。証跡は `reports/diagnostics/pr20-f1-diagnostics-sidecar-20260917/` とF1対応報告書を参照する。

## 実装修正の分離（2026-09-18）

PR #20 は設計書・README・台帳・文書検査の修正に限定する。独立確認から派生した製品実装と試験実装の変更は `fix/pr20-diagnostics-sample-runtime` へ退避し、PR #20 の対象外とした。設計側は実装修正前の状態を基点に継続し、実装側の完了判定は別分岐で扱う。
