# Issue #25 / PR #26 通常レビュー指摘対応・検証報告（2026-11-27）

## 対象と判定

- Repository: `ibis-ssl/Duck`。Issue [#25](https://github.com/ibis-ssl/Duck/issues/25)、既存 [PR #26](https://github.com/ibis-ssl/Duck/pull/26)。
- Branch: `fix/diagnostics-sample-runtime-issue25`。
- Base: `main` `849664d5e92b3e5ff805514eca7d221fea9e2c67`。
- Technical HEAD / 通常再レビュー確認対象: `fe38aed89c26ddc50c2d63169bb8e8b6d58b36c2`。
- 本報告は通常報告（consolidated / normal persistence）であり、独立最終レビューの報告・予約・合格判定ではない。
- 同一通常レビュー担当が現行 technical HEAD で必須4件すべて解消と確認した旨を親担当から受領した。レビュー担当の個別識別子・原文は本作業に提供されていないため創作しない。
- 報告 commit: `commit_pending`、push: `push_pending`。本報告追加後の SHA と CI は記録時点では未確定。技術 HEAD までの push と CI は完了。

## 要求・範囲

`diagnostics-samples.jsonl` の採取時点を新規記録の replay timeline と既定2表示元の主経路とし、外部 tracker 比較の snapshot / alignment 経路と分離する（`Tracker/Tracker.DebugHost/README.md` の診断再生・Ready 契約、`Tracker/Design/Core/tracker-architecture-plan.md`）。旧記録の own snapshot 描画は保持する。PR #20 の文書範囲、旧退避ブランチ、Aspire 実機受入は本課題に含めない。

## 通常レビュー指摘と解消内容

| 指摘（親担当からの必須指摘、番号は本報告の整理用） | 実装・試験の対応 | 確認された状態 |
|---|---|---|
| 1. sample がない旧記録で own tracker snapshot 描画経路を失う | Reader の ibis tracker 主経路を sample が存在する場合に限定。旧経路のフレーム番号・時刻・球とロボット・payload 復元を `LoadFieldSourceFrame_WithoutDiagnosticsSample_UsesOwnSnapshot` で直接確認 | 解消 |
| 2. 空の sample に tick がないのに Ready になる | Reader で空 sample を `SidecarEmpty` と判定。実 metadata の空 sample で timeline が空であることを確認 | 解消 |
| 3. 正常外部比較の内容・時点と metadata path 欠落が未検証 | 実構成で既定2表示元の内容と同一 sample tick、外部 snapshot 5400 の内容・latest-before・時刻を確認。未来の5401を選択しない。path 欠落時の Ready、両表示、外部比較不可警告を検証。再レビュー残件として選択 tick を渡した SelectedEntryComparison も Field source と同じ5400、同じ受信時刻・選択時刻を指すことを追加 | 解消 |
| 4. Windows で故障注入時に writer ファイルハンドルが残る | 故障注入前に双方の writer を dispose。再レビュー残件として正常な外部ケースも双方 dispose 後に読出し。製品の FileShare は未変更 | Linux 対象試験合格。Windows直接試験は未実施 |

関連差分: `Tracker/Tracker.DebugHost/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`、`Tracker/Tracker.Tests/VisionPacketCaptureTests.cs`、`Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`。正常系仕上げ commit では既存の試験メソッド1件に16行追加したのみ。

## ローカル検証（再実行なし）

実行環境: Codex Cloud Linux、bash、隔離 worktree `/workspace/scratch/duck25-vgzoaX`、.NET SDK 10.0.401。検証能力: local_execution_available。以下のログは同環境の作業場に保存済みで、GitHub artifact ではない。各結果を別々の技術状態へ紐付け、後続版への成功の流用はしない。

| 実行対象・技術状態 | 結果 | 証拠（`/workspace/scratch/duck25-vgzoaX-logs/` 以下） |
|---|---|---|
| main 取り込み候補（merge commit `884ff93152b13659bc9ee532272c48e1860ee431` の tree、commit 前に実行） | 15 passed / 0 failed / 0 skipped、exit 0 | `test-output.txt`、`focused.trx`、`filter.txt` |
| 通常レビュー修正サイクル（公開修正 `29be5c05f24f38bd9c4f90d74edae4394d8086ae`）：旧 HEAD 884ff931 の製品コードに回帰試験を先行追加した Red 状態 | 6 passed / 2 failed / 0 skipped、exit 1。旧記録: Expected Ready / Actual SidecarUnavailable。空 sample: Expected SidecarEmpty / Actual Ready | `review-red/output.txt`、`review-red/red.trx` |
| 同サイクルの製品修正後（29be5c05 の tree、commit 前） | 52 passed / 0 failed / 0 skipped、exit 0 | `review-green/output.txt`、`review-green/green.trx` |
| 正常系2残件修正後（fe38aed8 の tree、commit 前） | 1 passed / 0 failed / 0 skipped、exit 0、1回のみ | `review-normal/output.txt`、`review-normal/normal.trx` |

実行したコマンドは `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj` を基礎とする。取り込み時は既存 actual composition 5件、旧記録、sidecar異常、外部比較の計15件のフィルター（保存済み filter.txt）。レビュー Red は `VisionPacketCaptureTests.DiagnosticsReader_` と `TrackerDiagnosticsComparisonViewStateTests.LoadFieldSourceFrame_WithoutDiagnosticsSample_UsesOwnSnapshot`、Green は `VisionPacketCaptureTests|TrackerDiagnosticsComparisonViewStateTests|RuntimeHostDiagnosticsSampleBoundaryContractTests` の3クラス、正常系仕上げは完全名 `Tracker.Tests.VisionPacketCaptureTests.DiagnosticsReader_WithActualMetadataAndTrackerReceiveEnabled_PrefersDiagnosticsSampleForIbisTracker` に限定（後三者は --no-restore）。既存依存パッケージ警告等は発生したが対象試験は合格。全体ローカル試験は追加実施していない。

## PR 自動 CI（版別）

GitHub の commit 指定 workflow run と job ログを2026-11-27に照合した。PR CI の実際の checkout は各 HEAD と対応 base の merge 候補であり、単独 HEAD checkout と混同しない。

| PR HEAD | Run | 結果 | Tracker.Tests の実測 |
|---|---|---|---|
| `a2f5b7bbbf144d06d37658a11579567a23b643ce` | [#133 / 35286456213](https://github.com/ibis-ssl/Duck/actions/runs/35286456213) | success（歴史的 base d9ca3eef） | 334 passed / 0 failed / 0 skipped |
| `884ff93152b13659bc9ee532272c48e1860ee431` | [#1262 / 45952030751](https://github.com/ibis-ssl/Duck/actions/runs/45952030751) | success、checkout merge 381f462 | 1623 passed / 0 failed / 4 skipped |
| `29be5c05f24f38bd9c4f90d74edae4394d8086ae` | [#1263 / 45953106135](https://github.com/ibis-ssl/Duck/actions/runs/45953106135) | success、checkout merge 069c910 | 1626 passed / 0 failed / 4 skipped |
| `fe38aed89c26ddc50c2d63169bb8e8b6d58b36c2` | [#1265 / 45953620978](https://github.com/ibis-ssl/Duck/actions/runs/45953620978) | success、checkout merge 77d1c5b | 1626 passed / 0 failed / 4 skipped |

後三版の base は 849664d5。test job と Dockerなしの desktop acceptance policy step は成功、ASPIRE-NET-002 Linux packet flow と ASPIRE full-stack Linux acceptance job は skipped。失敗時診断・artifact upload は skipped。Aspire 実機受入の成功とは扱わない。

## 追跡・残件・境界

- `Tracker/Design/tasks-status.md` と `Tracker/Design/phases-status.md` の #25 記載は PR #20 の文書範囲を独立に完了させた際の分離履歴。#25 の現在の作業は Issue #25 と PR #26 で独立管理され、まだ独立最終レビュー・利用者のマージ前である。PR #20 の歴史的保留を完了に書き換える必要はない。今回の報告のみの変更では追跡文書に新規の不一致を認めず、編集しない。
- 初回の `reports/issue-25-diagnostics-sample-runtime-fix-20260918082023.md` と `reports/diagnostics/issue25-diagnostics-runtime-20260918/verification.json` は歴史証拠として保持する。実装・テスト・設定・旧報告は本 commit で変更しない。
- Windows 直接実行は未実施。Aspire 実機受入、実機、Docker、手動CI は本課題対象外・未実施。依存警告は対象外。
- 次は本通常報告 commit を含む候補を独立最終レビューの担当が固定すること。独立報告は未作成。マージは利用者のみ。

Persistence mode: repository_file / normal report。対象パス: `reports/issue-25-pr26-normal-review-closure-20261127.md`。本報告は technical HEAD の証拠を保存するもので、自身の将来 commit SHA を検証済み実装 SHA として主張しない。

## 追記：独立最終レビューから通常修正へ戻った2指摘の解消確認（2026-11-27）

初回固定 HEAD は `b0b6acca9cf024b4fd612e42f8520a280726bfd9`。独立最終レビューの必須2指摘を受けて通常修正へ戻った。以下は追加通常報告であり、独立最終レビュー合格を宣言しない。

| 時系列・技術 HEAD | 指摘と修正 | ローカル証拠 |
|---|---|---|
| `40df23c03fefb57042ceaf99965e61a79d002634` | (1) 外部記録の欠損構造・path・alignment 索引を検証し、正常 sample の timeline と既定2表示元を保持。(2) 新規 sample の未作成・path/ファイル欠落・空・破損と真正旧記録を区別し、後者だけ own snapshot に戻す | 先行 Red（b0b6acca 製品コード＋追加12ケース）1 passed / 11 failed / 0 skipped。修正後 Green 64 passed / 0 failed / 0 skipped。正常な旧記録・新規欠落の区別は通常再確認で解消 |
| `a24c1fb3136219d5f0474a8bf7417f25df243a66` | (1) の残り：snapshot・ready alignment の必須時刻/差分を検証し、ナノ秒変換上限・負値・選択時点との差分超過を明示的に判定。外部読取のアクセス拒否を限定処理し、sample 主経路には影響させない | 先行 Red（40df23c0 製品コード＋追加10ケース）2 passed / 8 failed / 0 skipped。修正後 Green 74 passed / 0 failed / 0 skipped。選択 tick の Load、外部 Field と既定2表示元を確認 |

両回とも Linux/.NET 10.0.401 の隔離 worktree で実行。対象は `VisionPacketCaptureTests`、`TrackerDiagnosticsComparisonViewStateTests`、`RuntimeHostDiagnosticsSampleBoundaryContractTests`。保存済みログ: `/workspace/scratch/duck25-vgzoaX-logs/` 以下の `independent-closure-red/`、`independent-closure-green/`、`external-time-red/`、`external-time-green/` の output.txt、TRX、exit-code.txt。今回の報告追記で試験は再実行していない。

| PR HEAD | 実際に確認した自動 CI（各 checkout は main 849664d5 との PR merge 候補） |
|---|---|
| `b0b6acca9cf024b4fd612e42f8520a280726bfd9` | [#1267 / 45954172137](https://github.com/ibis-ssl/Duck/actions/runs/45954172137) success、merge a3333c3、1626 passed / 0 failed / 4 skipped |
| `40df23c03fefb57042ceaf99965e61a79d002634` | [#1269 / 45955761399](https://github.com/ibis-ssl/Duck/actions/runs/45955761399) success、merge 1a6bb8f、1638 passed / 0 failed / 4 skipped |
| `a24c1fb3136219d5f0474a8bf7417f25df243a66` | [#1273 / 45956829238](https://github.com/ibis-ssl/Duck/actions/runs/45956829238) success、merge cf9e207、1648 passed / 0 failed / 4 skipped |

3版とも test job 成功、Aspire 受入2 job は skipped。親担当から現行 technical HEAD a24c1fb3136219d5f0474a8bf7417f25df243a66 の通常確認で必須指摘0件と受領した。既存報告2件、予約済み独立報告、製品・試験・追跡文書は変更しない。Windows 直接試験・実機は未確認、Aspire 実機受入は対象外、手動CI・マージは未実施（マージは本人のみ）。本追記の commit は commit_pending、push_pending、追記後 HEAD の CI は未確認で、独立最終レビューは同じ担当へ戻す。

## 追記：同一独立予約からの追加2指摘・限定通常確認（2026-11-27）

- 修正前公開 HEAD: `0e3daf0a2c5b9a4a87cb5c7481596a9bacacf283`。修正後の technical HEAD: `834479a5595d4e6968e7b11591578360b33b9092`。
- alignment の保存差値を二時刻の絶対差と照合し、不一致なら正常 sample と既定2表示元を維持しつつ外部比較を利用不能とする。境界正常 fixture は実際の二時刻が差値と一致する構成へ修正。sample 本体の読取拒否は `UnauthorizedAccessException` を読取境界で扱い、`SidecarCorrupt` / `SidecarUnavailable` を返して旧 own snapshot へ誤移行しない。
- 回帰先行 Red（0e3daf0 の製品挙動＋試験・注入点）10 passed / 2 failed / 0 skipped、修正後 Green（834479a の tree、commit 前）**76 passed / 0 failed / 0 skipped**。対象は既存の VisionPacketCaptureTests、TrackerDiagnosticsComparisonViewStateTests、RuntimeHostDiagnosticsSampleBoundaryContractTests。ログは `/workspace/scratch/duck25-vgzoaX-logs/alignment-sample-red/` と `alignment-sample-green/` の output.txt、TRX、exit-code.txt。今回の文書追記で再試験なし。
- 別担当の限定通常修正確認は technical HEAD 834479a5595d4e6968e7b11591578360b33b9092 に対して必須指摘0件と親担当から受領。独立最終レビュー全体の合格判定ではない。
- 読取拒否は注入した例外による確認であり、OS 権限を変更した実測は未確認。Windows 直接試験・実機は未確認、Aspire 実機受入は対象外。手動CI・マージは未実施、マージは本人のみ。
- 実際に確認した自動CI: 0e3daf0a2c5b9a4a87cb5c7481596a9bacacf283 の [#1274 / 45957345046](https://github.com/ibis-ssl/Duck/actions/runs/45957345046) は success（main 849664d5 との merge 4e788d0）、1648 passed / 0 failed / 4 skipped、Aspire 受入2 job skipped。834479a5595d4e6968e7b11591578360b33b9092 の [#1287 / 45971894765](https://github.com/ibis-ssl/Duck/actions/runs/45971894765) は確認時点で in_progress、結果未確定。前版の成功を現行版へ流用しない。
- 親担当の Skill 判断: **追加変更不要。既存レビューで捕捉して既存工程で閉鎖**。正式課題・既存タスクの関連と初回独立凍結予約は維持。追跡文書の PR #20 分離履歴に新しい変更を要する不一致なし。旧報告2件・予約済み独立報告は未変更。
- 引継ぎ: 同じ独立担当に、本追記を含む後続候補 HEAD と technical HEAD の差分（通常報告のみ）を渡す。予約報告は未作成。commit_pending / push_pending（追記前の technical HEAD は公開済み）、追記後の CI 未確認。独立レビューの再確認・最終報告は本追記の対象外。
