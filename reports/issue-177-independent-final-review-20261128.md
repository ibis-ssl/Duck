# Issue #177 独立最終レビュー報告

## 判定

**合格（pass_with_held）**。必須指摘は0件。Windows Docker実機での録画生成、ホスト側への保存、正常停止、単体MCAPの再生は未実施であり、保留する。この判定は実機受入の完了やマージ承認を意味しない。

## 対象と独立性

- Issue / Draft PR: Duck #177 / #178
- ブランチ: `task/issue-177-match-recording-mcap`
- base: `main` / `a89f4a753a9e84b9df880e1c064178ed180da6fb`
- reviewed_implementation_head: `bb91f994adcaf64e05bf2c4765f6ea6af1a03599`
- 直前の実装 HEAD: `b0792559db57370e7ea9d5d17e05290ed74809f1`
- 基盤の通常レビュー済み HEAD: `de71ef5be984b2ed584ec9eca4d868702acf8a15`
- 独立レビュー担当は実装・通常レビューに参加していない。同じ担当による独立確認を1回実施した。実適用モデル・reasoning は確認できない。
- 通常レビューの NR-177-01〜05 は解消済み。独立確認との矛盾や再オープンを要する指摘はない。限定再レビュー・重大度変更はなし。

差分は実装基盤、AppHost接続追補、通常報告・引継ぎを含む29ファイル。直前の実装 HEAD から凍結 HEAD までの差分は実装報告、通常レビュー報告、引継ぎの3ファイルのみである。Issue #177 と設計書、Duck / CodexSkill の AGENTS.md および開発・レビュー手順と整合することを確認した。

## 独立確認

| 観点 | 判定 | 根拠 |
| --- | --- | --- |
| 生入力全件と採用入力 | 合格 | RuntimeVisionReceiverService は復号・カメラ別最新値バッファ格納前にrawを保存する。置き換えられた入力には未処理理由を記録する。 |
| 処理順・実効設定 | 合格 | RuntimeHostOperationLoop と RuntimeMatchRecordingSession は受信連番と実際のengine update orderを区別し、設定世代へ相関づける。全域・領域選別無効の状態、座標系、単位を明示する。 |
| 形状変更・停止 | 合格 | 形状更新・reset、late-drop時の形状処理、受付済み入力のdrain、完成前の検証を確認した。 |
| 必須情報の欠損 | 合格 | Writer、Reader、CLIは共通replay contractを使用する。設定、相関、必要な形状イベント、late-drop時のengine update orderの欠損を拒否する。 |
| 単体MCAP・CLI | 合格 | MCAPだけを別ディレクトリに移し、記録内設定と処理順で再計算する経路と試験を確認した。 |
| direct AppHost | 合格 | `Testing:MatchRecording:Enabled=true` を指定したmatch-modeのみ、試行固有の `duck-recordings` を `/recordings` に writable mountし、RuntimeHostに出力先を渡す。未指定時には録画設定とmountを追加しない。 |
| 報告・引継ぎ | 合格 | 基盤、AppHost接続追補、凍結直前の報告更新の各対象SHAと検証結果が整合する。 |

## 検証証跡

- 実装報告・通常レビュー報告に記載された焦点試験31件、同じ通常レビュー担当の関連確認13件、AppHost接続追補のモデル試験2件は成功。
- 基盤実装 HEAD `de71ef5be984b2ed584ec9eca4d868702acf8a15` の自動CI run [46136348535](https://github.com/ibis-ssl/Duck/actions/runs/46136348535): success。
- AppHost接続追補 HEAD `b0792559db57370e7ea9d5d17e05290ed74809f1` の自動CI run [46137484219](https://github.com/ibis-ssl/Duck/actions/runs/46137484219): success。
- 凍結 HEAD `bb91f994adcaf64e05bf2c4765f6ea6af1a03599` の自動pull_request CI run [46137937709](https://github.com/ibis-ssl/Duck/actions/runs/46137937709): completed / success。Run dotnet test、Dockerを使用しない録画保存composition試験、Linux publish出力検証が成功した。
- 上記凍結HEADのASPIRE-NET-002 Linux packet flowとASPIRE full-stack Linux acceptanceはskipped。実機検証済みとは扱わない。
- 独立レビューでは固定コミット、差分、試験コードおよび対象SHAのCI結果を読み取り専用で確認した。新規のローカルテスト、手動CI、実機・Docker起動は実施していない。

## 残件と対象外

- 必須修正: なし。
- 保留: Windows Docker実機での正常な録画生成・保存・停止・単体再生。長時間録画の処理負荷と停止時の確定時間は実機で確認する余地がある。
- 対象外: #160 native `--play-match`、#27の領域選別機能、#151の起動時診断ログ。

## 証跡保存の境界

予約 owner は `review-enforcer`、予約IDは `duck-177-pr178-bb91f994-20261128`、予約パスは本ファイル `reports/issue-177-independent-final-review-20261128.md`。独立レビュー開始前はmetadata-onlyであり、合格判定後に初めて保存する。

技術的判定は上記 reviewed_implementation_head に付く。本報告の追加は実装変更のない管理用記録（administrative attestation）であり、第一親を同HEADとする報告のみの1コミットに限定する。他のファイルを変更しない。証跡コミット自身のSHAとその自動CI結果は、作成・公開後に外部で記録する。

この後の別コミットに判定は移転しない。実装や他のファイルに変更が必要な場合は通常の修正検証と同じ独立レビュー担当による限定確認を経る。PR本文・コメントの更新、マージ、手動CI、実機確認は本報告の保存に含めない。
