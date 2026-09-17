# DOC-LINT-003 表示設計とREADMEの意味の追加修正

## 文脈と修正

2026-09-17の文書修正担当の自己点検であり、独立最終レビューではない。raw visionの表示設計394行とDebugHost README490行を読み、次の5行を修正した。原文の比較元は `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`。

| 対象 | 行・ブロック | 変更と根拠 |
| --- | --- | --- |
| 表示設計 | 380 / D13-B085 | 原文の `TrackerFrame` をpublishする周期はUDP通信の有無と同義ではないため、「送信」を「出力」に修正。追跡結果の確定周期をraw visionの保存周期にしない条件は保持。 |
| README | 378 / D18-B060 | 「小刻みな揺れを弱く信用する」を、観測値の重みを小さくして揺れの影響を抑える説明へ修正。係数の対象と大小の方向を保持。 |
| README | 380 / D18-B061 | 形状変更時に消去する追跡フレームを、`TrackedSnapshotStore` に保持する最新のものと特定。キャプチャー履歴全体を消す意味にしない。 |
| README | 389 / D18-B063 | measurement noiseが大きいほど「観測値の重みを小さくする」と説明。原文からある「弱く信用する」を自然な表現に修正。 |
| README | 394 / D18-B064 | ロボットの向きの観測についても重みで説明し、対象と調整方向を保持。 |

[原文・最終文・周辺文脈・判断理由](diagnostics/wording-verification-20260917-0758/display-and-readme-semantic-corrections.json)に記録した。表示設計のraw visionと画像・動画の区別、latest-beforeで未来のスナップショットを採用しない条件、選択時点を動かさない条件も本文を読んで確認したが、それだけで原出現の全件台帳の完了とは扱わない。今回、原文まで追加照合した範囲は5行が属する5ブロックと関連する契約であり、既存85件・75件の採否を全部再審査したとはしない。

実装照合では、`TrackerCoordinator.Dispatch.cs`、`TrackedSnapshotStore.ClearLatestFrame()`、`FrameCommit.cs` により初期化と最新フレームの消去を確認した。`Settings.cs` の観測分散計算と `Kalman.cs` のgain・状態更新から、観測の重みによる説明が調整方向を保つことを確認した。実装、用語許可一覧、検査器、検査除外は変更していない。

表示設計の修正後SHA-256は `da14baa2e2de802d034a3b6521272bbeb93c4de0557b005c386e837cafc41395`、READMEは `ae2664ce14d4edd11ef0d38dc7a775aaae4504d7abc1ca876ef3f33f05ddb0eb`。両方とも行数と行番号は変わっていない。

## 並行変更と検証

初回の技術親は `6965b8e3ffa9a64ed728d2c8224136e8a89c27a4`。その後、別担当の `c1b28955b653d65788f8581834c0cf5b56910b64` を差分確認後にfast-forwardで取り込み、未コミットの5行を保持した。相手のCore詳細設計の `summary` 要素の復元と133出現箇所の証拠はそのまま残している。取り込み前と後の検証は別名で保存した。

| 検証 | 結果 |
| --- | --- |
| 取り込み前の表示設計・READMEのtextlint / cspell / whitelist / diff-check | 各終了値0 |
| 取り込み後の表示設計・README・Core詳細設計のtextlint / cspell / whitelist / diff-check | 各終了値0 |
| 全19対象の `npm run lint:md` | 終了値123。引用内exec 1件でcspellが停止 |
| 全体のtextlint / cspell / whitelist / diff-checkの個別実行 | 0 / 123 / 1 / 0。cspellはexec 1件、whitelistはexec 1件とサブエージェント2件 |
| 利用者2発言と用語整理前の原文との一致 | 一致 |
| 検証中の本文とHEAD | 変更なし |

[取り込み前の検証](diagnostics/wording-occurrence-audit-20260916/validation-verification-viewer-readme-20260917-0841.json)、[取り込み後の検証](diagnostics/wording-occurrence-audit-20260916/validation-verification-display-integrated-20260917-0842.json)、[後段の個別検査](diagnostics/wording-verification-20260917-0758/display-integrated-poststages-20260917-0842.json)、各検証単位のログアーカイブを保存した。[公開前の記録](diagnostics/wording-verification-20260917-0758/display-and-readme-publication.json)には全対象のハッシュ、環境・依存物の参照、stdout・stderr・終了値を含む30ファイルのアーカイブと各ハッシュを記載した。ログの空行なども削らず原本のまま保存している。

GitHub connectorで取得した公開HEAD `c1b28955b653d65788f8581834c0cf5b56910b64` と、run 35163301537のhead_shaは完全一致し、.NET testsは成功だった。これは同SHAの証拠であり、今回の5行の未コミット変更やその後の並行更新のCIではない。Markdown lintの未通過を.NETテストで代用しない。

本報告作成時点は `commit_pending` / `push_pending` / `ci_wait_pending`。公開前に更新があれば差分を確認して安全に取り込み、公開後は実際のPR HEADに一致するrunのみでCIを確認する。force pushや他担当の作業ツリーの変更はしない。

## 残件

全原出現箇所の最終台帳、履歴本文と採否の最終整合、全体lint、独立最終レビューは別の完了条件として残す。この5行を台帳へ取り込む際は、位置が同じでも表現と内容ハッシュが変わったことを確認する。共有検査器の引用機能は別担当の作業で、引用例外の方針は承認済み。新たな用語登録や検査除外は不要であり、今回の修正に新しい利用者承認は必要ない。

公開前追記: 範囲担当の報告・証拠だけを含む `a0069d9b675852f58ddf405642e15a7192925247` も差分確認後に取り込んだ。検査対象19文書の全ハッシュは直前の検証記録と一致した。[取り込み時の照合](diagnostics/wording-verification-20260917-0758/display-publication-integration.json)を保存し、報告追加だけによるHEAD変更を本文変更や新しいCI成功と混同していない。
