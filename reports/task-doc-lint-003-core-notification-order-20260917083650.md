# DOC-LINT-003 構成設計の出力順・通知順の修正

## 修正内容と根拠

2026-09-17の文書修正担当による追加確認。独立最終レビューではない。構成設計の現本文1172行を読み、通知と通信を混同しやすい6箇所を原文と照合して修正した。

`ITrackerEngine` と `TrackerUpdateResult` は、確定した追跡フレームとイベントの列を返す。`TrackerCoordinator.Dispatch.cs` では `EmittedEvents` の順で処理し、通知先への同期呼び出しとUDP配信用の `PublishFrame` を別の処理として持つ。用語整理で原文のpublish順を「送信順」にした説明は、この責務の区別を読み取りにくくするため、返却する追跡フレームは「出力順」、イベントは「通知順」とした。

| 変更ブロック | 現本文の行 | 修正 |
| --- | --- | --- |
| D07-B058 | 271 | 追跡結果と通知の返却を「出力順」とする |
| D07-B061 | 293 | `CommittedFrames` を「出力順」に並べる |
| D07-B062 | 296 | `EmittedEvents` を「通知順」に並べる |
| D07-B217 | 1013 | トラッカーで確定した順に「通知する」とする |
| D07-B218 | 1016 | 制御通知、追跡フレーム、派生通知の「通知順」とする |
| D07-B241 | 1135 | 作業分割の表記も「通知順」に揃える |

[6件の原文・最終文・理由・実装参照](diagnostics/wording-verification-20260917-0758/core-output-order-corrections.json)に証拠を保存した。比較元は `f5482ab85d49832a21ef6029d6aa3354f6c7c4f4`。修正前に、既存247ブロックの対応表の全本文とハッシュが現在のファイルに一致したことも確認した。ただし、その機械的一致は意味の合格ではない。今回、原文まで再読した範囲は修正する6ブロックと関連する契約の文脈であり、他の241ブロックの再審査や全出現箇所の確認完了を主張しない。

本文は1172行のままで、位置を動かしていない。SHA-256は `cf6ad3ead602757c76e6e1d21c3e273ccac31bf769526f1de6abaf994d879843` から `9bfa207c07ea317cb8a32b075a24ce4f9e8e8456140f2b98a006c0e37afb27b3` へ変わった。診断設計の先行4点の修正、進捗の対応中状態、引用原文、履歴原文の保存、許可一覧は保持した。製品コードと共有検査器は変更していない。

## 並行作業の取り込み

確認開始時の技術親は `978369a6df97975a5b0cecc1944bca4e113b946c`。範囲担当が公開した `32e6b082fdf47e5b114a0aef3201ca262bea7574` の差分を読み、他担当の作業ツリーではなく、この担当の専用ツリーへfast-forwardで取り込んだ。構成設計の6行の未コミット修正は保持した。取り込んだ変更は、検査案内の実在する2文書を選ぶ例、進捗末尾の補足、範囲確認の証拠である。

## 検証結果

取り込み前と後を別単位で実行した。公開対象の技術親は `32e6b082fdf47e5b114a0aef3201ca262bea7574` であり、構成設計6行の修正を含む本文ハッシュで検証内容を特定する。

| 検証 | 結果 |
| --- | --- |
| 取り込み前の構成設計のtextlint / cspell / whitelist / diff-check | 各終了値0 |
| 取り込み後の構成設計・進捗2文書・検査案内のtextlint / cspell / whitelist / diff-check | 各終了値0 |
| 全19対象の `npm run lint:md` | 終了値123。cspellの引用内exec 1件で停止 |
| 全体textlintの個別実行 | 終了値0 |
| 全体cspellの個別実行 | 終了値123。引用内exec 1件 |
| 全体whitelistの個別実行 | 終了値1。引用内exec 1件、サブエージェント2件 |
| 利用者2発言と用語整理前の原文との一致 | 一致 |
| 各検証中の本文とHEAD | 変更なし |

[取り込み前検証](diagnostics/wording-occurrence-audit-20260916/validation-verification-core-order-20260917-0834.json)、[取り込み後検証](diagnostics/wording-occurrence-audit-20260916/validation-verification-core-integrated-20260917-0835.json)、[後段を含む各検査の個別実行](diagnostics/wording-verification-20260917-0758/core-integrated-poststages-20260917-0836.json)からコマンドと結果を追える。各単位のログアーカイブと[公開前の証拠一覧](diagnostics/wording-verification-20260917-0758/core-order-publication.json)にはstdout・stderr・終了値・全対象の内容ハッシュ・アーカイブのハッシュを保存した。

個別実行補助は、取り込んだ範囲担当の `validate_poststages.py` とバイト単位で同一のコピーをこの担当の証拠保存先に置いた。共有検査器の改造ではない。Python・Node.js・検査器の依存版と設定は[環境記録](diagnostics/wording-verification-20260917-0758/validation-environment-initial.json)、凍結された検査器のハッシュ再確認は[再照合記録](diagnostics/wording-verification-20260917-0758/six-docs-later-stage-validation.json)を参照する。上流PR #81の未マージ修正を利用した結果ではない。

## 公開と残件

本報告の作成時点は `commit_pending` / `push_pending` / `ci_wait_pending`。新しいPR HEADと完全一致するCIは、push後にGitHub connectorで確認する。直前のHEAD一致CIや.NETテスト成功を、新HEADの証拠やMarkdown lint成功に流用しない。

原出現の全件台帳、履歴本文と最終表現の整合、追加の意味確認、全体lint、独立最終レビューの完了条件は緩めない。台帳担当には6ブロックのID・行・新しいハッシュをPRコメントで通知した。今回の表現には新しい用語登録や検査除外は不要で、利用者の追加承認を求める事項はない。共有引用機能の対応待ちとは分離し、Duck側の文書確認を続ける。mergeは行わない。
