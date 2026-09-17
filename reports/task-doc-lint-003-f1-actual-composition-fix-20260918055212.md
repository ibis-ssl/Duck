# DOC-LINT-003 F1 実構成の指摘対応報告

- 対象: ibis-ssl/Duck PR #20
- 独立再確認: `reports/pr20-independent-final-closure-20260917.md`
- 指摘: F1 の実構成不一致
- 修正開始時のPR HEAD: `868fe673093679639b0cc072b7c670b8b48cddda`
- 実装修正commit: `bd0b08c15309e3815d7720ffd16db6b5c11eb130`
- 役割: 指摘修正担当。独立再レビュー担当ではない。

## 指摘の再現

独立再確認では、実際の `VisionPacketCaptureSession` が生成するmetadataで、`DiagnosticsSampleLog.IsCreated=true` と `TrackerSnapshotLog.IsCreated=false` が共存すると、`TrackerDiagnosticsComparisonViewStateReader.Load(...)` が `Ready` ではなく `SidecarNotCreated` を返すことが報告された。

また、tracker packet snapshot sidecar が存在する場合、`ibis tracker` の Field source が diagnostics sample の同一採取記録ではなくtracker snapshot側へ流れ、own snapshotが無い構成では `CandidateMissing` になる経路も残っていた。

レビュー報告の独立probeだけを根拠にせず、実際のproduction classを同じテスト内で組み合わせた回帰テストを追加して再現した。

追加した2テスト:

- `DiagnosticsReader_WithActualMetadataAndTrackerReceiveDisabled_UsesDiagnosticsSamples`
- `DiagnosticsReader_WithActualMetadataAndTrackerReceiveEnabled_PrefersDiagnosticsSampleForIbisTracker`
## TDD 記録

最初の実行は専用worktreeのsubmodule未初期化により、`SslProto` の生成型を解決できずビルドで停止した。この環境失敗は `tdd-environment-red` として分離保存した。submodule初期化後、実装を変更せず同じ2テストを再実行した。

本来のRed:

- tracker packet受信無効相当: expected `Ready`, actual `SidecarNotCreated`
- tracker packet受信有効相当: expected `Ready`, actual `CandidateMissing`
- 2件失敗 / 0件成功

このRedを確認してからproduction codeを変更した。

最初のGreen試行では、エラーメッセージ内で存在しない `fieldSource.Label` を参照したためコンパイル失敗した。この失敗も `tdd-green-build-red` として保存し、動作契約を変えずメッセージだけを修正した。

最終Green:

- 2件成功 / 0件失敗
- `Vision Input` と `ibis tracker` は `diagnostics-sample-sidecar` を使用
- tracker snapshot未作成でもsample timelineを `Ready` として構成

## 実装修正

`TrackerDiagnosticsComparisonViewStateReader` の2箇所だけを修正した。
### 1. sample-only metadataを正常経路にする

`DiagnosticsSampleLog` を正常に読み取れた場合、`TrackerSnapshotLog` が存在しない場合だけでなく、実producerが出す `TrackerSnapshotLog.IsCreated=false` でもsample-onlyの正常経路へ入るようにした。

これにより、`Tracker:Receive:Enabled=false` 相当の通常構成でtracker packet snapshot sidecarが作成されなくても、diagnostics sample timelineと既定Field sourceを構成できる。

### 2. `ibis tracker` の主経路をdiagnostics sampleへ固定する

`LoadFieldSourceFrame(...)` は `Vision Input` と `ibis tracker` の両方について、diagnostics sample sidecarが存在する場合はtracker snapshot sidecarの有無より先にsample recordを使う。

- `Vision Input`: raw summaryを使用
- `ibis tracker`: tracked summaryを使用
- `External` / `Unknown` / source label: 従来どおりtracker snapshot / alignment経路を使用

これにより、外部trackerのsnapshot sidecarが存在しても、`ibis tracker` の意味がown tracker packetの有無によって変わらない。

## 文書・台帳

F1対象3文書を含む独立レビュー対象8文書は今回変更していない。対応する原出現台帳も変更していない。

`868fe673093679639b0cc072b7c670b8b48cddda..bd0b08c15309e3815d7720ffd16db6b5c11eb130` で、対象8文書とその対応台帳の変更ファイルが0件であることを `doc-ledger-unchanged.txt` に保存した。既存の2,670 / 2,670件の本文対応と本文SHAはそのまま有効である。
## 回帰検証

PR HEAD単体では、今回と無関係な既存テスト `Load_WithSelectedReplayTimeline_WhenOnlyFutureSourceSnapshotExists_ReturnsMissingWithoutFutureFallback` が1件失敗した。このテストは修正前の `868fe673...` をcheckoutした独立review worktreeでも同じ失敗を再現したため、今回の変更による新規回帰とは扱わない。

GitHub Actionsのpull_request checkoutに合わせ、修正commit `bd0b08c...` とcurrent `main` `d9ca3eef...` を一時worktreeだけでmergeした。merge commitは `fa522618...` であり、PRブランチにはpushしていない。

一時merge環境の結果:

| 検証 | 結果 |
| --- | --- |
| diagnostics / capture / comparison周辺 | 43件成功 / 0件失敗 |
| `Tracker.Tests` 全体 | 331件成功 / 0件失敗 |
| 今回追加した実構成テスト | 2件成功 / 0件失敗 |

この一時mergeにより、HEAD単体で見えた既存1件の差異もcurrent mainを含むCI相当条件では解消することを確認した。

## 診断artifact workflow

作業開始時にcurrent `main` の `.github/workflows/dotnet-test.yml` を確認した。テスト失敗時にはTRX、標準出力、標準エラー、vstest診断ログ、binlog、environment情報、source archiveを収集し、`actions/upload-artifact` で公開する構成が存在する。今回このworkflow自体の変更は不要だった。

## 証拠
- `reports/diagnostics/pr20-f1-actual-composition-20260918/tdd-environment-red/`: submodule未初期化時の環境失敗
- `reports/diagnostics/pr20-f1-actual-composition-20260918/tdd-red/`: 実装変更前の2件失敗
- `reports/diagnostics/pr20-f1-actual-composition-20260918/tdd-green-build-red/`: 最初のGreen試行のコンパイル失敗
- `reports/diagnostics/pr20-f1-actual-composition-20260918/tdd-green/`: 実構成2件成功
- `reports/diagnostics/pr20-f1-actual-composition-20260918/focused-regression/`: PR HEAD単体の周辺検証
- `reports/diagnostics/pr20-f1-actual-composition-20260918/baseline-head-known-failure/`: 同じ既存失敗を修正前HEADで再現した記録
- `reports/diagnostics/pr20-f1-actual-composition-20260918/mergecheck-focused/`: current main一時統合で43件成功
- `reports/diagnostics/pr20-f1-actual-composition-20260918/mergecheck-full/`: current main一時統合で331件成功
- `reports/diagnostics/pr20-f1-actual-composition-20260918/environment.json`: commitと検証条件
- `reports/diagnostics/pr20-f1-actual-composition-20260918/doc-ledger-unchanged.txt`: 対象文書・台帳が今回未変更である証拠

## 公開後に必要な確認

このreport作成時点の実装修正commitは `bd0b08c15309e3815d7720ffd16db6b5c11eb130`。進捗文書とreportを追加するとPR HEADが更新されるため、最終CIはその新しいcurrent HEADとworkflow runの`head_sha`が完全一致するrunだけを採用する。

F1の修正担当による自己点検は完了したが、独立最終レビューのclosureではない。最終HEADのCI成功後も、別の独立レビュー担当によるF1再確認で未解決指摘0を確認する必要がある。

mergeは行わない。

## 最終文書検査

進捗文書を同期した後、保存済みcheckerとPython環境を使って18対象文書へ `npm run lint:md` を実行した。初回は追記した進捗説明の不要な英単語で綴り検査が失敗し、次の実行では「回帰テスト」が許可一覧違反として1件残った。許可一覧・検査除外は変更せず、自然な日本語へ直した。

最終結果:

- `npm run lint:md`: 終了値0、18文書、cspell指摘0、許可一覧違反0
- `git diff --check`: 終了値0
- 失敗記録: `final-lint-red/`、`final-lint-whitelist-red/`
- 成功記録: `final-lint-green/`

最終CIはreportと検証資料をcommit/pushした後のPR current HEADをGitHub connectorで再取得し、そのSHAと完全一致するworkflow runだけをPRコメントへ記録する。

公開前の最終再検証は `reports/diagnostics/pr20-f1-actual-composition-20260918/final-validation-green/` に保存した。進捗文書の最終表現を含む18対象文書で `npm run lint:md` 終了値0、`git diff --check` 終了値0を確認している。
