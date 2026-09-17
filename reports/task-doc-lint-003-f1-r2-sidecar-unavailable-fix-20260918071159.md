# DOC-LINT-003 F1-R2 外部tracker sidecar異常時の指摘対応

- 対象: ibis-ssl/Duck PR #20
- 独立再確認report: `reports/pr20-independent-final-closure-r2-20260918.md`
- 指摘ID: F1-R2
- 独立レビュー対象HEAD: `f5902f9e1de4b3fddaddf7b1d3e6c26d5443ceaa`
- 修正開始時HEAD: `07c49ee9d36e39a5df7e20c7f38a5c282a18dcc8`
- 実装修正commit: `f159232f90c9d486e24c70fd3daf7db951bd514c`
- 役割: 指摘修正担当。独立再レビュー担当ではない。

## 指摘の内容

独立closure再レビュー第2回では、正常な `diagnostics-samples.jsonl` が残っていても、metadataが `TrackerSnapshotLog.IsCreated=true` のまま tracker snapshot sidecar実体だけを失うと、`TrackerDiagnosticsComparisonViewStateReader.Load(...)` が `SidecarMissing` を返して早期終了することがactual compositionで再現された。

現在仕様では、読み取り可能な diagnostics sample sidecar だけでも replay timeline と `Vision Input` / `ibis tracker` を構成できる。そのため外部tracker用sidecarの異常が、sample主経路まで空にする挙動は設計書・READMEと不一致だった。

独立reportは同じコード形状として `SidecarPathMissing`、sidecar読み取り失敗、record 0件も挙げていた。今回production compositionで再現済みのmissingだけでなく、同じ欠陥クラスのempty / corruptも先行テストへ含めた。

## TDD Red

実際の `VisionPacketCaptureSession`、`DiagnosticsSampleLogWriter`、`TrackerPacketSnapshotLogWriter` を同じテストで組み合わせ、diagnostics sampleと外部tracker snapshotを正常保存した後、tracker snapshot sidecarを次の3状態へ変更した。

- missing: file削除
- empty: 0 byte化
- corrupt: 不正JSONへ置換
修正前の結果:

- missing: expected `Ready`, actual `SidecarMissing`
- empty: expected `Ready`, actual `SidecarEmpty`
- corrupt: expected `Ready`, actual `SidecarCorrupt`
- 合計: 0件成功 / 3件失敗

Red証拠:

- `reports/diagnostics/pr20-f1-sidecar-missing-20260918/tdd-red/`
- `reports/diagnostics/pr20-f1-sidecar-missing-20260918/tdd-red-siblings/`

## 実装修正

`TrackerDiagnosticsComparisonViewStateReader` で、diagnostics sample indexを正常に構築できる場合は tracker snapshot sidecarの状態をsample主経路の阻害条件にしないようにした。

- `TrackerSnapshotLog` 未作成: 従来どおりsample-only `Ready`
- tracker sidecar path欠落: sample replayを `Ready` のまま維持
- tracker sidecar実体欠落: sample replayを `Ready` のまま維持
- tracker sidecar読み取り失敗: sample replayを `Ready` のまま維持
- tracker sidecar record 0件: sample replayを `Ready` のまま維持

外部tracker比較が使えない状態は `TrackerDiagnosticsComparisonViewState.Error` の警告文として別表示する。`SourceOptions` の外部候補件数は0のままとし、sample由来の `Vision Input` / `ibis tracker` と replay timelineを維持する。

診断用採取記録が存在しない既存経路では、従来の `SidecarPathMissing` / `SidecarMissing` / `SidecarCorrupt` / `SidecarEmpty` を変更していない。
## Green / 回帰検証

対象を絞った検証では、今回の3ケース、前回F1のactual composition 2ケース、既存のdiagnostics sample境界5件、diagnostics sampleなしのsidecar状態5ケースを合わせて15件成功した。

- focused: 15件成功 / 0件失敗
- 証拠: `reports/diagnostics/pr20-f1-sidecar-missing-20260918/tdd-green/`

current `main` `d9ca3eef62cc644a37adb8b643cc1cea7ad4a171` を実装修正commitへ一時的にmergeした検証専用worktreeでは、`Tracker.Tests` 全体を実行した。

- mergecheck commit: `ea8b039f189f8e2bee1cda304a928fd70ee21af0`
- 334件成功 / 0件失敗 / 0件skip
- mergecheck commitはPRブランチへpushしていない
- 証拠: `reports/diagnostics/pr20-f1-sidecar-missing-20260918/mergecheck-full/`

## 文書・台帳

今回の実装修正では、独立レビュー対象5設計書 + 3 README、および対応する原出現台帳を変更していない。

`07c49ee9d36e39a5df7e20c7f38a5c282a18dcc8..f159232f90c9d486e24c70fd3daf7db951bd514c` の対象文書・台帳差分が0件であることを `reports/diagnostics/pr20-f1-sidecar-missing-20260918/doc-ledger-changes.txt` に保存した。したがって既存の2,670 / 2,670件の本文対応を維持している。

## 診断artifact workflow

作業開始時にcurrent `main` の `.github/workflows/dotnet-test.yml` を確認した。失敗時はTRX、標準出力、標準エラー、vstest診断ログ、binlog、環境情報、source archiveを収集し、artifactとして公開する構成が既に存在するためworkflow変更は不要だった。
## 公開状態と残件

実装修正commit `f159232f90c9d486e24c70fd3daf7db951bd514c` はPRブランチへ通常push済みである。進捗文書・本report・検証証拠を追加した後はPR current HEADが変わるため、最終CIはその新HEADとworkflow runの `head_sha` が完全一致するrunだけを採用する。

修正担当による自己点検ではF1-R2の要求を満たしたが、独立最終レビューのclosureではない。最終HEADで次を別の独立レビュー担当に再確認してもらう必要がある。

- diagnostics sample正常 + tracker sidecar missingでもsample timelineと既定2表示元が維持される
- empty / corruptでも同じsample主経路を維持する
- diagnostics sampleなしでは既存のsidecar状態を維持する
- 外部tracker比較不可が警告として区別される
- current HEAD一致CIが成功する

mergeは行わない。
## 最終文書検査

進捗文書をF1-R2へ同期した後、18対象文書に対してMarkdown検査を再実行した。初回は進捗文書の未登録英単語2件と末尾空行で失敗したため、許可一覧や除外設定を変更せず自然な日本語と通常の末尾改行へ修正した。

最終結果:

- `npm run lint:md`: 終了値0、18文書、cspell指摘0、許可一覧違反0
- `git diff --check`: 終了値0
- Red: `reports/diagnostics/pr20-f1-sidecar-missing-20260918/final-lint/`
- Green: `reports/diagnostics/pr20-f1-sidecar-missing-20260918/final-lint-green/`
