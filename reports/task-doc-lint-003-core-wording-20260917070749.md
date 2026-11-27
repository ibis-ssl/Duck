# DOC-LINT-003 構成設計の原文照合

## 実施範囲

作業担当として `Tracker/Design/Core/tracker-architecture-plan.md` の全本文1,172行と、用語整理前の原文・差分247件を照合した。独立最終レビューではない。機械処理で抽出した差分を自動承認せず、各差分に採否と理由を記録した。

採否表は `diagnostics/wording-occurrence-audit-20260916/decisions-doc-07.json`、原文と最終段落の対応は `core-block-correspondence-20260917.json` に保存する。原文989行の「は分けて保持する」は現在986行のゴール判定の導入に統合されている。削除扱いにせず、続く991行以降の競技判定の説明へ誤って対応付けないようにした。

## 修正内容と確認

source sampleをキャプチャー全体とする誤訳、raw vision/render snapshotが追跡フレームを保持するように読める説明、APIとUUIDの衝突を混同する構文を修正した。source metadata、capture metadata、保存JSONL、実UI名、入力元の観測フレーム番号と出力の追跡フレーム番号を区別した。

Kalman filter、process noise、measurement noiseの方式・設定名を保ち、状態推定の重複表現を修正した。secondary ballのgrowthは、ボールの物理的な成長や数の増加ではなく、観測を重ねて追跡状態が確立する説明とした。3回以上の観測という条件と未確立の外部出力禁止を保持した。

原文particle filterは広い意味の粒子法にせず、許可一覧に既にある「パーティクルフィルター」を使用した。作業途中で英語表記を追加候補と考えたが、全許可一覧の確認で承認不要と判明した。許可一覧、aliases、description、prh、検査除外、製品コードは変更していない。

フレームカウンターは単なる件数でなく追跡フレーム番号の採番と明記した。形状変更で消去する状態を実装で確認し、カメラごとの追跡状態、接触/場外/統合後の識別状態、キック、直前primary、ボールIDの採番と停止判定回数、保存中の最新TrackerFrame/受信時刻を説明した。未処理入力の消去とframe_number/実行識別の維持も原文どおり残した。

## 元からある設計と実装の相違

原文はフィールド長・幅・ゴール形状が設定閾値以上に変わった場合を初期化条件としている。一方、現行 `Tracker.Core/Engine/TrackerEngine/Geometry.cs` は、長さと幅だけを設定閾値で比較し、ゴール幅と奥行きは値の不一致で判定する。今回の用語整理で生じた差ではなく、条件を無断で追加・削除して解決した扱いにはしない。初期化対象の説明は実装と照合したが、この条件の一致確認は未解決として残す。

## 検証

最終本文SHA-256: `cf6ad3ead602757c76e6e1d21c3e273ccac31bf769526f1de6abaf994d879843`。

`validation-core-architecture-final-20260917-0707.json` で対象textlint・cspell・whitelist、git diff --checkは終了値0。全対象の `npm run lint:md` は引用中のexec 1件により終了値123であり、全体成功とはしない。直前までの失敗・修正・再検証も別名のJSONと成否にかかわらないログtarへ保持した。後段の全体whitelistは、最終一括検証時に再実行して記録する。

開始時の共有検査器ハッシュとPython/Node依存版はchecker-manifestと保存ログに記録済みであり、この作業では変更していない。引用例外は方針承認済みだが、固定した共有検査器に機能がない。引用を改変して回避していない。

前の公開HEAD `d3952dc9057174bdffeb4c8e30d06aa8d477f5d1` と一致する.NET testsのrun `35154434370` はsuccessを確認した。これは本修正後HEADの証拠でもMarkdown lint成功の証拠でもない。次のHEADのrunは別途確認する。

## 残作業

表示画面設計、DebugHost README、作業状況、工程状況の4文書、全4,336出現箇所の最終台帳、履歴本文と原文保存版の最終照合は継続中。DOC-LINT-003は完了にしない。新たな用語承認が必要な事項は現時点ではない。
