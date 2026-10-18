# Issue #25 diagnostics sample 診断再生実装修正

- Repository: `ibis-ssl/Duck`
- Issue: #25 `diagnostics sample 主経路の診断再生実装を設計へ整合する`
- Base: `main` `d9ca3eef62cc644a37adb8b643cc1cea7ad4a171`
- Branch: `fix/diagnostics-sample-runtime-issue25`
- 作成時刻: 2026-09-18 08:20:23 JST

## 背景

PR #20 の独立レビューで、diagnostics sample sidecar が正常でも tracker snapshot sidecar の状態によって診断再生が `Ready` にならない実装不整合が確認された。利用者指示により、PR #20 は文書修正だけに限定し、C#実装は #25 へ分離した。

元の退避ブランチ `fix/pr20-diagnostics-sample-runtime` は PR #20 の文書履歴を含んでいたため、そのままPRには使っていない。current `main` からクリーンな分岐 `fix/diagnostics-sample-runtime-issue25` を作成し、実装変更2コミットだけを移植した。

## 変更

### `TrackerDiagnosticsComparisonViewStateReader`

- `TrackerSnapshotLog.IsCreated=false` でも、読み取り可能な diagnostics sample があれば sample replay を維持する。
- `Vision Input` は diagnostics sample の raw summary を使用する。
- `ibis tracker` は diagnostics sample の tracked summary を既定経路として扱う。
- tracker snapshot sidecar の path欠落、file欠落、empty、corrupt があっても、diagnostics sample が正常なら既定2表示元と replay timeline を維持する。
- 外部tracker比較不可は、sample replay自体を失敗させず警告として分離する。
- diagnostics sample がない従来経路では既存の sidecar 状態判定を維持する。

### `VisionPacketCaptureTests`

実際の `VisionPacketCaptureSession`、`DiagnosticsSampleLogWriter`、`TrackerPacketSnapshotLogWriter`、`TrackerDiagnosticsComparisonViewStateReader` を組み合わせる実構成テストを追加した。

対象条件:

- tracker受信無効相当 + diagnostics sample 正常
- tracker受信有効相当 + diagnostics sample / tracker snapshot 正常
- tracker snapshot sidecar missing
- tracker snapshot sidecar empty
- tracker snapshot sidecar corrupt

## TDD由来

移植元の実装作業では、実装変更前にactual-compositionテストを追加し、次の失敗を確認してから製品実装を修正した。

- tracker snapshot未作成: expected `Ready` / actual `SidecarNotCreated`
- tracker snapshot存在時の `ibis tracker`: expected diagnostics sample / actual `CandidateMissing`
- tracker snapshot file missing: actual `SidecarMissing`
- empty: actual `SidecarEmpty`
- corrupt: actual `SidecarCorrupt`

このPRは、そのTDD済み実装コミットだけを current `main` へ移植したクリーンな実装PRである。

## 検証

focused:

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~Tracker.Tests.VisionPacketCaptureTests.DiagnosticsReader_
5 passed / 0 failed / 0 skipped
```

全体:

```text
dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj
334 passed / 0 failed / 0 skipped
```

`main...HEAD` の変更対象はC# 2ファイルだけで、PR #20 の設計書・README・台帳・lint設定は含まない。検証要約は `reports/diagnostics/issue25-diagnostics-runtime-20260918/verification.json` に保存した。

## CI失敗診断

`main` の `.github/workflows/dotnet-test.yml` には、失敗時に次を保存する仕組みが既に存在するため追加変更は不要だった。

- TRX
- dotnet test 標準出力
- dotnet test 標準エラー
- vstest diagnostics
- MSBuild binlog
- 環境情報
- GitHub Actions artifact upload

## PR範囲

このPRでは #25 のC#実装と回帰テストだけを扱う。PR #20 の文書変更は含めない。mergeは利用者が行う。
