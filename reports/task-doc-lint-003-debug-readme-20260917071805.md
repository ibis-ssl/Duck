# DOC-LINT-003 DebugHost利用手順の原文照合

作業担当として `Tracker/Tracker.DebugHost/README.md` の全490行を原文と照合し、75差分の採否・理由・段落対応を記録した。独立最終レビューではない。

入力の観測フレーム番号、出力の追跡フレーム番号、replay timelineの位置番号、保持中のrender snapshotの追跡フレーム番号を区別した。手動証跡の自前側時刻・対応付け側時刻・時刻差から落ちていた単位nsを復元した。Layerのvisibilityは追跡の可視性スコアではなく表示/非表示として説明した。

受信に使う端末側のIPv4とマルチキャスト宛先を区別し、multicast interfaceを宛先アドレスとする誤訳を修正した。形状変更時の初期化はフィールド形状自体ではなく追跡状態であり、未処理の検出情報と保存中の旧形状の追跡フレームを消去することを実装に照合した。

source label、capture metadata、render snapshot、diagnostics sample sidecar、Kalman filter、process noise、measurement noise、実UI名、単体テスト、相対パスの意味と参照対象を保持した。flushはバッファ内データを書き出すことと明記し、保存庫全体という誤訳は実在する `Duck.slnx` を対象として説明した。許可一覧、検査設定、製品コードは変更していない。

新規/旧形式capture、受信有効化とCaptureOnの別条件、起動時固定のtracker受信先、公開送信とUIカウンターの違い、30回/秒と早送りの全時点進行、64倍を超える倍率、初回だけの索引化、未知profileのserver error、CLIがUI確認の補助である境界を保持した。用語を戻した後の助詞/空白/定義の重複も自己点検して修正した。

証拠は `diagnostics/wording-occurrence-audit-20260916/decisions-doc-18.json`、`debug-readme-block-correspondence-20260917.json`、`edits-debug-readme-20260917.json`、`edits-debug-readme-final-selfcheck-20260917.json`。最終本文SHA-256は `ebaa2c2eeabe715c2a9c9eab734b31200ab113bbee1f583ff0631521fe1e6141`。

`validation-debug-readme-r2-20260917-0717.json` では対象textlint/cspell/whitelistと差分検査が終了値0。全対象の `npm run lint:md` は引用中execで終了値123。初回の不成功も別名の検証JSONとログtarに保持し、成功記録で上書きしていない。コマンド、対象ハッシュ、stdout/stderr/終了値を保存した。

残作業は作業状況・工程状況の本文、全4,336出現箇所の最終台帳、履歴本文の最終確認。全件確認済みとも全体lint成功ともしない。新たな用語承認を要する事項はない。公開HEAD一致のCIはpush後に確認し、過去HEADの.NET成功を代用しない。
