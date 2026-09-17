# DOC-LINT-003 設計と利用手順の文脈確認

記録時刻: 2026-09-17T07:48:29.757117+09:00。技術的な親HEADは `74631ef4bf42345819893b08d984de79ac733650`。この担当は文書修正担当であり、独立最終レビュアーではない。

## 読み比べた範囲

診断設計の原文310行と最終329行・90変更、Core構成設計の原文と最終各1172行・247変更、表示設計の原文392行と最終394行・85変更と62脚注、DebugHost利用手順の原文488行と最終490行・75変更を、変更のない段落も含めて通読した。合計497変更の採否理由を確認し、節ごとの条件・否定・数量・対象・用語の理由を `diagnostics/wording-ledger-20260917-0709/` の4つの `*-context-review.json` へ記録した。

診断設計では、原文のsource sampleをキャプチャー全体に広げず、受信時刻の対応と表示30fps・非間引き早送りの違いを保持していることを確認した。Coreでは観測フレーム番号と出力番号、Kalman filterと2種類のnoise、形状変更時に初期化する対象、secondary ballの追跡状態の確立、途中のゴール判定文の移動を確認した。表示設計と利用手順では実UI名、同じ描画時点での固定、latest-before、欠損時の表示、時刻の単位を確認した。

## 今回の追加修正

表示設計のimmutable snapshotの脚注で、原文の「clone / DTO化」を「複製してDTOにした」と連続した必須手順へ狭めていたため、「複製やDTO化により」と直した。目的である描画中の値の不変性は保持した。

利用手順ではprocessed timestampが「処理完了時刻」とされていた。実装の `Tracker/Tracker.Core/Engine/TrackerEngine/FrameCommit.cs` では、確定処理の冒頭で時刻を取得してから追跡状態を更新するため、「追跡フレームの確定処理中に取得した時刻」と直した。`DataTimestampNs` の観測時刻との区別を保持し、製品コードは変更していない。

各変更の旧文・新文・元SHA・ファイルハッシュ・理由は `raw-viewer-footnote-correction.json` と `debug-readme-timestamp-correction.json` に保存した。旧採否表は上書きせず、今回の最終位置と内容は `raw-viewer-final-correspondence.json` と `debug-readme-final-correspondence.json` に別保存した。

## 以前からある設計と実装の差

診断設計では、絶対時刻差が同じ場合のrecord index比較を同じTrackedFrameTimestamp内に限定しているが、実装はその条件なしに比較している。Core構成設計ではゴール寸法の変更にも閾値を述べるが、実装はゴール寸法の不一致で判定している。いずれも用語整理前に存在した差であり、原文の意味を消して整合したことにはしていない。承認のない仕様変更や実装変更は行わない。具体的な場所は各context-review.jsonと既存の対応表に残した。

## 検証

対象4文書のtextlint/cspell/whitelistと差分検査は `validation-ledger-design-readme-20260917-0747.json` で終了値0。全19文書の `npm run lint:md` は123で未通過。後段も個別に再実行し、textlintは0、cspellは引用中exec 1件で123、whitelistは引用中exec 1件とサブエージェント2件で1だった。結果は `poststages-0750.json`、stdout/stderr/終了値は対応するtarと各検証単位に保存した。元の2引用は変更していない。

共有検査器と依存物はintake.jsonの固定版、許可一覧は306項目を維持する。新しい用語承認・検査除外・共有検査器変更はしていない。採否集計用のシェル呼び出し2回は安全性確認不可で遮断されたため成功扱いせず、read_fileで既存対応表を読んだ。本文修正とその後の検証は成功した操作を記録している。

## 完了としない範囲

履歴939出現箇所の対照台帳は既に公開済みだが、残る全出現箇所の統合台帳は作成中。この497変更の確認を、全4,336件の対応付け完了や独立最終レビューの合格とはしない。全体lint、進捗・PR同期、最終公開HEAD一致CIは最終時点で再確認する。`74631ef` の.NET CI成功は `ci-checkpoint-74631ef.json` に保存したが、今回これから公開するHEADの証拠には代用しない。
