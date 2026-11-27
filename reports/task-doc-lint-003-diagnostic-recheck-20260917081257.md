# DOC-LINT-003 診断設計の追加確認と修正

作成時点: 2026-09-17 08:12:57 JST。文書修正担当による自己点検であり、独立最終レビューではない。

## 対象と結論

作業開始時と公開前のGitHub connector照会で、PRの実際のHEADは `efe76c3dccd3683813415b7fc6f4c6b3a1764007` だった。引き継ぎ文の `982c760` より後の変更を含む。元の監査ツリーには未コミット変更があり、台帳担当も別ツリーで稼働していたため、両者を変更せず、`work/wording-verification-20260917-0758` の専用ツリーを作成した。

診断設計 `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md` の90変更ブロックを、`f5482ab85d49832a21ef6029d6aa3354f6c7c4f4` の原文・前後の文脈と読み比べた。継承した90件の対応表は、開始時本文のハッシュ・位置・IDと全件一致した。しかし、意味の確認では次の3点に追加修正が必要だった。**構造的一致だけを意味の合格とはしない。**

| ID | 修正 | 根拠 |
| --- | --- | --- |
| D11-B062 | 折りたたみボタンを「画面ヘッダー」ではなく、`Tracker Comparison` 自身の見出し行と特定 | 原文176行、`Diagnostics.razor` 121〜130行の比較領域内headerとボタン |
| D11-B074 | 見出し化で消えた「少なくとも」というテスト範囲の条件を復元 | 原文214行 |
| D11-B075 | 100 ms時点で参照する対象を、render snapshotだけでなくraw visionとの両方に復元 | 原文217行と、現本文の時間軸の説明 |

修正は3行で、総行数329行と既存の行番号は変わらない。用語一覧、除外設定、製品コード、共有検査器は変更していない。本文SHA-256は `b3cb9628a72654f2b85a8187496e07eb63db264c997ed4855d833f5fe0e0c894` から `23f0f68faed4b53bc0f95746e0d281cb383a491de49404d0b095ea43f1e07a04` へ変わった。

90件の再確認理由は[個別採否](diagnostics/wording-verification-20260917-0758/diagnostic-semantic-decisions.tsv)、原文・最終文・周辺文脈・行範囲は[対応表](diagnostics/wording-verification-20260917-0758/diagnostic-semantic-block-correspondence.json)に保存した。3件修正、1件未解決、残り86件は維持または自然な日本語として採用。D11-B025の原文条件と実装の差も理由に残し、実装と一致したとはしていない。

D11-B019の `session-relative offset` を「保存記録内の相対位置」とした表現は未解決。現実装の `TrackerSnapshotAlignmentRecord` は番号とUTCの `RenderReceivedAt` を持つが、この代替参照項目の単位・起点の定義は確定できていない。時間・バイト位置のどちらかを推測して固定せず、Duck側の確認残件とする。

これは90変更ブロックの追加確認であり、変更のない箇所を含む4,336出現箇所の最終台帳の完成証拠ではない。元の採否、検証、履歴本文、原文保全版は上書きしていない。

## 再検証

修正前と修正後で別名の検証を実行した。修正後の技術親は上記HEAD、内容は上記SHA-256で識別する。

| 対象 | 結果 |
| --- | --- |
| 診断設計のtextlint / cspell / whitelist | 各終了値0 |
| `git diff --check` | 終了値0 |
| 全19対象の `npm run lint:md` | 終了値123。textlint通過後、cspellで引用内のexecを1件検出して停止 |
| 後段の `npm run lint:md:whitelist` の個別実行 | 終了値1。引用内のexec 1件、サブエージェント2件 |
| 2つの利用者発言の原文一致と履歴行の識別情報 | 終了値0。比較元は用語整理前のf5482abであり、開始時点a2e63f0だけとの比較ではない |
| ローカル.NETテスト | この文書修正では未実行 |

[修正前検証](diagnostics/wording-occurrence-audit-20260916/validation-verification-diagnostic-20260917-0803.json)、[修正後検証](diagnostics/wording-occurrence-audit-20260916/validation-verification-diagnostic-fixed-20260917-0807.json)、各検証単位の `*-validation-logs.tar.gz` にコマンド・全対象のハッシュ・stdout・stderr・終了値を保存した。[補足検証](diagnostics/wording-verification-20260917-0758/supplemental-validation.json)から後段の個別実行と引用保全の結果を追える。実行前に拒否された複合コマンドは未実行と記録し、単独コマンドで得た結果のみ採用した。

[依存物確認](diagnostics/wording-verification-20260917-0758/inherited-checker-file-verification.json)では、保存済み検査器の対象ファイル全件が記録ハッシュと一致した。[環境記録](diagnostics/wording-verification-20260917-0758/validation-environment-initial.json)にPython 3.12.3、SudachiPy 0.6.11、辞書20260428、ChikkarPy 0.1.1、Node.js 22.13.1、cspell 9.8.0、textlint 15.7.0、設定ハッシュを保存した。検査器は記録上のCodexSkill `279b8da9ffe954f78fd8461555be429f1bdcc100` の保存版。許可一覧は306項目で、今回追加していない。

## CIと診断保存

[開始時HEAD一致のCI記録](diagnostics/wording-verification-20260917-0758/ci-initial-head.json)では、run 35159496695のhead_shaは `efe76c3dccd3683813415b7fc6f4c6b3a1764007` と完全一致し、.NETテストは329成功・失敗0・スキップ0だった。Markdown lintを実行するworkflowではない。

実際のチェックアウト先は合成マージコミット `bbe3cf3cc9b88c5dcb8b311dcd1d0463833065cb` で、親は `d9ca3eef62cc644a37adb8b643cc1cea7ad4a171` と対象PR HEADだった。そこにある[workflow本文](diagnostics/wording-verification-20260917-0758/ci-checkout-workflow.yml)をgitから取得し保存した。PR HEADツリーに同ファイルがないことも区別している。`github.workflow_sha` 自体はログに出ておらず、その値まで取得できたとはしない。

workflowはテスト失敗時にTRX、stdout、stderr、終了値、vstest診断、MSBuild binlog、生成されたblame情報、環境記録、ソースをartifactへ保存する。成功時の収集・公開はスキップするため、この成功runのartifactは0件。失敗時の保存設定がないとは扱わない。このrunは開始時HEADの証拠であり、今回のpush後HEADのCIへ流用しない。

## 上流対応との区別

2026-09-17 08:08 JSTにGitHub connectorで確認したCodexSkillのPR #81は未マージで、実際のHEADは `7417ac0da68202ff81f5cdf0c91417f6d8bb290b` だった。そのSHAの検査器では、実表記を正規化した値と許可一覧を照合するコードが存在し、Sudachiの正規形・読みだけで許可する分岐は変更されている。ただし、今回のDuck検証は従来の保存版を使っており、この上流修正を取り込んで再検証したわけではない。

- [上流の表記一致修正](https://github.com/ssaattww/CodexSkill/blob/7417ac0da68202ff81f5cdf0c91417f6d8bb290b/skills/review-enforcer/scripts/check-markdown-whitelist-sudachi.py)
- [追加対応の記録](https://github.com/ssaattww/CodexSkill/pull/81#issuecomment-5704475283)
- [承認済みの直接引用の機械検証](https://github.com/ssaattww/CodexSkill/issues/86)

Issue #86は確認時点で未完了。発言者・宛先・出典・範囲・原文一致を確認できる引用本文だけを例外にする方針は承認済みであり、再承認待ちではない。通常本文への未許可語の登録や、引用符だけによる一律除外は行っていない。

## 継続事項

Duck側ではD11-B019の相対位置の意味、4,336出現箇所の最終対照台帳、履歴本文との最終整合、PR差分と検査対象の範囲整合、独立最終レビューが残る。今回の修正を台帳へ取り込む際は、診断設計の行数が変わらなくても内容ハッシュが変わったことを確認する。本文側の作業は上流の引用機能待ちとは分離する。

台帳の作成担当とはPRコメントで作業範囲を分けた。08:17 JSTの確認では、さらに別担当がPR差分と検査対象の範囲整合を専用ツリーで調べている。未公開の資料や他担当の作業ツリーには書き込まない。

この文書はコミット前の検証結果を記録する。公開コミットとpush後のcurrent HEAD一致CIは、取得後のPRコメントと後続記録へ残す。現段階ではcommit・push・push後CIの完了を先取りしない。今回の3点の修正には、新しい用語登録や利用者の追加承認は必要ない。
