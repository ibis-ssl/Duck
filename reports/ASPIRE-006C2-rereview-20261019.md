# Duck PR #73 独立再レビュー報告（2026-10-19 JST）

## 対象と結論

- PR: https://github.com/ibis-ssl/Duck/pull/73
- 指定および確認済み実装HEAD: `4c0c2eb1f73c974806815fa3141a31283b5792f9`
- 前回レビューHEAD: `452d2682cc0fd3f7b5554bd640f84fc4c2ea9d41`、前回コメント: https://github.com/ibis-ssl/Duck/pull/73#issuecomment-6255559356
- 直前の修正: `85e169f`（Linux入力・製品経路計測）、`620fd78`（複数受信元endpointへの対応）、`4c0c2eb`（計測記録）。変更4ファイル。
- **判定: 要修正（fail）。** 前回指摘R01・R02・R03は解消を確認。新規指摘R04として設計Markdownのホワイトリスト検査失敗を確認。
- 独立レビューでは製品ソース・既存設計を編集しない。レポートを実装HEADから派生する別ブランチ `review/pr73-rereview-20261019-4c0c2eb` に記録する。mergeは行わない。

## 前回指摘の照合

### R01: 実測更新頻度がない → 解消（accepted）

- 確認範囲: `Tracker/Tracker.Tests/AspireFullStackAcceptanceTests.cs` の `MeasureLiveTrackerDifferencePathAsync` とLinux CI artifactの `acceptance-result.json` / TRX。
- 5秒の実際の受信窓を計時し、UDPで到着した `TrackerWrapperPacket` を3役割に分類して件数・平均物体数を集計する実装が追加された。
- **最終HEADに一致するRun #39171690308の実測**: 入力窓5001.2648ms。Duckは138 packets（27.59 packets/s）、TIGERsは704（140.76/s）、ER-Forceは1434（286.73/s）。各役割の受信フレームは平均22ロボット・1ボール。Vision受信298パケット。
- これは**受信したデータグラム数の更新頻度**であって、複数endpointを経由する同一frameを排除した発生源の一意な更新Hzではない。`ReceivedPackets` と `TrackedFrames` はデータグラム単位で集計される。資料に `packets/s` と観測境界が明記されているため、今回の入力実測条件は満たすと判断。今後、発生源固有のHzを報告するなら重複frameの区別が必要。

### R02: snapshot取得・複製が性能測定から除外されている → 解消（accepted）

- `TrackerDifferenceSnapshotEvaluatorTests.HealthEndpoint_ReportsSyntheticStoreToEvaluatorTimeAndAllocationCost` は `MultiTrackerManager`→`ExternalTrackerSnapshotStore`→`DebugHostHealthEndpoints.GetComparisonHealthResult` を測定対象へ変更。
- Linux full-stackも実データを `MultiTrackerManager` へ投入し、同じhealthメソッドを100回予熱・1000回反復して時間と割当量を記録。
- **最終HEADのRun #39171690308の実測**: p50 **202.796µs**、p95 **717.051µs**、割当 **94,064.04 bytes/呼出**、評価区間311.2347ms（.NET 10.0.13 / Ubuntu 24.04.5 / x64 / 4論理プロセッサ）。
- 含む: tracker storeのprotobuf `Clone()`、source resolver、3組のdifference evaluator、Vision storeのsnapshot取得・複製、`IResult`生成。含まない: HTTP JSONシリアライズと通信。性能目標達成を主張していない。
- `reports/ASPIRE-006C2-performance.md` のp50 257.466µs・p95 896.112µsは、明示した旧Run #39170608485 attempt 2（HEAD `620fd785029c9e84006b6d6702e34cff204574e1`）に由来する。最終HEADの同じ経路にも別の成功実測が存在する。報告書が旧実行を参照していることは明示的であり、虚偽とはしない。

### R03: role衝突・frame欠損の結合試験が不足 → 解消（accepted）

- `TrackerDifferenceSnapshotEvaluatorTests` に、異なる役割が同一identityを共有する衝突、同一役割にidentityが2種類ある衝突の2件を追加。
- `[Theory]` に参照frame欠損、比較frame欠損、両方欠損の3ケースを追加し、`Status`・null差分・frame番号を検査。
- レビュー用固定HEADのローカルfocusedテストで **12/12成功、0失敗、0スキップ**。正常経路、missing Robot/unmatched Ball、複数endpointの最新packet選択も維持。

## 新たな指摘

### R04 / 中（要修正）: 対象設計Markdownが既存のホワイトリスト検査に失敗

- ファイル: `Tracker/Design/Testing/aspire-006c2-integration-plan.md`。変更はPR初回コミットで導入され、今回の3コミットでは修正されていない。
- ファイルは `node tools/lint/scripts/list-markdown-targets.js` の正式なMarkdown lint対象に含まれる。
- 実行: `Duck-issue66-yomiyasu/.venv/Scripts/python.exe -B tools/lint/scripts/check-markdown-whitelist-sudachi.py --files Tracker/Design/Testing/aspire-006c2-integration-plan.md`（cwdは本PRのレビュー用worktree）。**exit code 1**。
- 診断: **106行（english 59・katakana分類47、重複検出あり）**。例: L5 `source resolver` と `snapshot`、L6 `frame`・`missing`・`unmatched`、L11 `logical role`・`resolved snapshot`、L13 `identity conflict`、L18 `harness`、L19 `source update cadence`、L25 `seam`。一部は正式UI名（`Split` / `Overlay`）も含まれる。
- 状態: **再現済み失敗**。静的文書の既存品質gateを通せない。新しいC#製品経路の不具合とは分けて扱う。
- 必要対応: 説明用の英語は意味・型名・単位・条件を保持して自然な日本語にする。UIの正式表記とソース識別子は勝手に変更せず、必要なら根拠を明示して既存許可一覧の承認手順へ回す。通常文を引用符やコード表記で囲むlint回避、検査除外設定の拡大、一括ホワイトリスト追加は不可。
- 修正後は正式な対象ファイルに再度 whitelist を適用し、exit 0 と人による自然な文章・技術的意味の照合を確認する。

## yomiyasu実行・指摘採否

- 対象: 上記設計Markdownの**1ファイルのみ**（SHA-256 `3990d7e9c12c701735dee9417ad148000230e19e79844a8ef24f5c6e4f4d5d0f`）。`reports/`は対象外。
- ツール: `nanaism/yomiyasu` v1.1.0、Git SHA `0df47749139dfd64ad3d55e7d53d3839e5874848`。`yomiyasu_lint.py` SHA-256 `61f0b0b1dcfc8cc734c69fc1f4aa2546d36ed846d42e3efceea1379b389e3e21`（Duck #66で監査された固定版を再利用）。
- 実行: `python -B .../skills/yomiyasu/scripts/yomiyasu_lint.py <対象Markdown> --json`。exit 0、**score 95**、`is_clean=false`、findings 1件。
- 警告 `excess_list` (warn、L1): 箇条書き15/非空行20=75%、ツール推奨15%以下。
- **採否: 不採用**。実装範囲、テスト条件、性能測定、検証順序はそれぞれ独立した要件。文章を連結して箇条書き比率だけ下げると、条件と担当の追跡性が悪くなる。意味を保つ上で現行構造を維持する。点数95を文書品質gate成功とはみなさない。
- **別判定**: 上述R04のホワイトリスト失敗は実際に修正が必要。yomiyasuが英単語検査を代替するものではない。

## 検証と証拠

| 検証 | 結果 |
| --- | --- |
| 対象HEAD確認 | `4c0c2eb1f73c974806815fa3141a31283b5792f9`、作業treeは変更前clean |
| focused `.NET` test | ローカル Release、12 passed / 0 failed / 0 skipped。TRX出力あり |
| `dotnet build Duck.slnx -c Release` | 成功、0エラー。既存のMessagePack NU1902とASPIRE010警告は残存 |
| Linux CI `test` | run `39171690308` / headSha一致 / success |
| Linux CI full-stack | 同run / success。`acceptance-result.json` status passed、stage `tracker_comparison_measurement`、TRX 2/2 passed |
| ASPIRE-NET-002 Linux packet flow | 同runでskipped（成功とは報告しない） |
| yomiyasu | score95、warn1、上記の理由で警告不採用 |
| Markdown whitelist focused | exit1、106件の診断、R04要修正 |

- CI run: https://github.com/ibis-ssl/Duck/actions/runs/39171690308 。同runのGitHub artifact `aspire-full-stack-39171690308-1` を実際に取得・解析。test-results/TRX、acceptance-result.json、stdout/stderr、コンテナログを確認した。
- CI workflow `.github/workflows/dotnet-test.yml` は失敗時にテスト結果、stdout、stderr、診断ログをartifactへ保存する段階を既に持つ。新規追加は不要。
- 手元のログ: `C:\Users\donabe\RemoteDesktopWorkspace\review-artifacts\Duck-pr73-rereview-20261019\`。`rereview-focused.trx`、`focused.stdout.log`、`focused.stderr.log`、`build.*.log`、`yomiyasu-plan.stdout.json`、`yomiyasu-plan.stderr.log`、`whitelist.*.log`、`ci-final-head/`。
- 動作検証はWindows環境でのローカルテストおよびGitHub Linux CIを使用。ユーザーのUbuntu Docker環境のsudoers/設定変更は行わない。

## レビュー対象外と留保

- Issue #41のSplit/Overlay共有描画配線は今回の対象外。
- Linux測定はCI runnerの一実測であり、HTTP JSON通信を含む全端末・全環境への一般化や性能目標達成を意味しない。
- 同じ論理トラッカーが複数endpointから届く場合の一意なframe生成頻度は別指標。入力到着率と混同しない。
- 設計書以外のMarkdownやreportsについて、yomiyasu合格・不合格を推定しない。reportsはユーザー指定により除外。

## 引継ぎ

R01・R02・R03は再指摘しない。新規R04として対象設計Markdownの用語・自然さを修正し、ホワイトリスト検査のexit 0を確認して再レビューする。指摘修正後はその時点のPR HEAD SHAと同じSHAのCI runだけを有効とする。mergeはユーザーに任せる。
