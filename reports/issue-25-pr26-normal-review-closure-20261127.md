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
