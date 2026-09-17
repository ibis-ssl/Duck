# PR #20 独立最終レビュー closure 再確認（第2回）

- 実施日: 2026-09-18 JST
- 対象PR: #20 `docs: add RuntimeHost README and shared appsettings guide`
- レビュー対象HEAD: `f5902f9e1de4b3fddaddf7b1d3e6c26d5443ceaa`
- 前回closure report-only HEAD: `868fe673093679639b0cc072b7c670b8b48cddda`
- 判定: **incomplete**
- merge: 実施しない

## 今回のclosure範囲

前回の独立closureレビューで未解決だったF1の実構成問題に対する修正を再確認した。
新しい評価基準を追加する全面レビューではなく、F1の根本契約である「diagnostics sample sidecar を新規記録の replay timeline と `Vision Input` / `ibis tracker` の主経路にする」がproduction classの組み合わせで成立するかを確認した。

確認対象は主に次の差分である。

- `Tracker/Tracker.DebugHost/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
- `Tracker/Tracker.Tests/VisionPacketCaptureTests.cs`
- commit `bd0b08c15309e3815d7720ffd16db6b5c11eb130`

進捗・検証記録のみのcommit `f5902f9e1de4b3fddaddf7b1d3e6c26d5443ceaa` も含め、PR current HEADまでを確認した。

## 前回指摘2点の再確認

### 1. tracker packet受信無効時のsample-only経路

`TrackerDiagnosticsComparisonViewStateReader.Load(...)` は、diagnostics sample index が存在し、`TrackerSnapshotLog` がnullまたは `IsCreated=false` の場合に `Ready` を返し、diagnostics sample timelineと既定Field sourceを構成するよう修正されている。

実際の `VisionPacketCaptureSession` が生成するmetadataを使う追加テスト `DiagnosticsReader_WithActualMetadataAndTrackerReceiveDisabled_UsesDiagnosticsSamples` を独立再実行し、成功を確認した。

### 2. tracker sidecar存在時の `ibis tracker` 主経路

`LoadFieldSourceFrame(...)` は `VisionInput` と `IbisTracker` を先にdiagnostics sample sidecarから解決し、`IbisTracker` ではtracked summaryを使うよう修正されている。

実producer metadataと実 `TrackerPacketSnapshotLogWriter` を組み合わせる `DiagnosticsReader_WithActualMetadataAndTrackerReceiveEnabled_PrefersDiagnosticsSampleForIbisTracker` を独立再実行し、成功を確認した。

既存の `RuntimeHostDiagnosticsSampleBoundaryContractTests` 5件も合わせ、closure対象の7件は **7 / 7成功**した。

## 未解決指摘

### F1-R2: diagnostics sampleが正常でも、tracker sidecarの欠落がsample主経路まで無効化する

**状態: 未解決。今回のclosure判定をincompleteとする理由。**

現行文書は、読み取り可能な `diagnostics-samples.jsonl` だけでも `Vision Input` / `ibis tracker` の replay timeline と Field source を構成できることを現在仕様としている。

- `Tracker/Tracker.DebugHost/README.md` 253行: `Ready` は読み取り可能な diagnostics sample sidecar だけでも既定2表示元とreplay timelineを構成できる状態と説明する。
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md` 187-189行: diagnostics sample sidecarをreplay timelineと既定2表示元の読み取り元とし、tracker snapshot / alignment sidecarは外部トラッカー用として状態を区別する。既定2表示元ではdiagnostics sampleの状態を優先すると規定する。

しかし `TrackerDiagnosticsComparisonViewStateReader.Load(...)` は、diagnostics sample indexが正常でも `TrackerSnapshotLog.IsCreated=true` の場合にはtracker sidecarの検査へ進む。
その後、tracker sidecarが欠落していると `SidecarMissing` を返し、`FieldSourceOptions=[]`、`ReplayTimeline=[]` として早期returnする。

今回、実際のproduction classを使う一時probeをレビュー専用worktreeで作成した。

1. `VisionPacketCaptureSession` を開始する。
2. `DiagnosticsSampleLogWriter` でdiagnostics sampleを1件保存する。
3. `TrackerPacketSnapshotLogWriter` で外部tracker snapshotを1件保存する。
4. flush後のactual metadataで `DiagnosticsSampleLog.IsCreated=true` と `TrackerSnapshotLog.IsCreated=true` を確認する。
5. tracker packet snapshot sidecarだけを削除し、diagnostics sample sidecarは残す。
6. 同じmetadataとdiagnostics logを `TrackerDiagnosticsComparisonViewStateReader.Load(...)` へ渡す。

結果は **1件実行 / 1件失敗**だった。

- expected: `TrackerDiagnosticsComparisonSidecarStatus.Ready`
- actual: `TrackerDiagnosticsComparisonSidecarStatus.SidecarMissing`
- replay timelineはsample主経路として維持されない。

このprobe sourceは検証後に `git restore` で削除し、PRへは含めていない。標準出力・標準エラー・終了値はレビュー端末の次へ保存した。

`/home/ibis/.local/share/duck-pr20-independent-closure-r2-evidence-20260918-0619/`

コード上は `SidecarPathMissing`、`SidecarMissing`、tracker sidecar読み取り失敗、record 0件でも同様にsample timelineよりtracker sidecar側の早期returnを優先する分岐がある。今回production compositionで独立再現したのは `SidecarMissing` であり、他状態はコード確認に留める。

### 修正に必要な契約

diagnostics sample sidecarが正常に読める新規記録では、外部tracker用sidecarの欠落・空・破損があっても次を失わないことが必要である。

- diagnostics sample tickによるreplay timeline
- `Vision Input`
- `ibis tracker`

tracker packet snapshot / alignment側の異常は外部トラッカーの比較可否として別に表現する必要がある。状態表現をどの型・statusへ分離するかは実装担当の判断とするが、現在文書を維持するなら外部sidecar異常によってsample主経路全体を空にしてはならない。

実装修正時には、今回のactual compositionを恒久テストへ追加することを推奨する。少なくとも「diagnostics sample正常 + tracker sidecar metadataは作成済み + tracker sidecar実体欠落」で既定2表示元とsample timelineが維持されることを固定する。

## 文書・原出現台帳

`868fe673093679639b0cc072b7c670b8b48cddda..f5902f9e1de4b3fddaddf7b1d3e6c26d5443ceaa` で、前回独立レビュー対象8文書と対応する8原出現台帳の変更ファイルは0件だった。

current HEADで台帳を独立再集計した結果は次の通り。

| 区分 | 件数 | 確認結果 |
| --- | ---: | --- |
| 5設計書 | 2,142 | SHA一致、unresolved 0、文書内ID重複0 |
| 3 README | 528 | SHA一致、unresolved 0、文書内ID重複0 |
| 合計 | **2,670** | **一意ID 2,670、重複0** |

したがって、今回のコード修正による本文・台帳の再同期漏れは確認していない。

## Markdown lint / diff check

レビュー用テスト実行時、最初はworktree内へ作成した一時NuGet cache配下の第三者Markdownまでlint対象になり、cspellが終了値123になった。この実行はレビュー環境由来のため無効とした。

レビュー用cacheをworktreeから削除し、tracked文書だけを対象に再実行した最終結果:

- `npm run lint:md`: 終了値0
- 対象: 18文書
- cspell: 指摘0
- 許可一覧検査: 成功
- `git diff --check`: 終了値0
- `git diff --check f5482ab85d49832a21ef6029d6aa3354f6c7c4f4..f5902f9e1de4b3fddaddf7b1d3e6c26d5443ceaa`: 終了値0

lint用の `.agents` / `node_modules` 一時symlinkは検証後に削除し、report作成前のworktreeはcleanであることを確認した。

## CI

レビュー対象HEADと完全一致するworkflow runだけを証拠とした。

- reviewed HEAD: `f5902f9e1de4b3fddaddf7b1d3e6c26d5443ceaa`
- workflow: `.NET tests`
- run id: `35274097440`
- run number: `126`
- job id: `105380366853`
- conclusion: `success`
- GitHub Actions test result: **331 passed / 0 failed / 0 skipped**

runはPRのmerge commit `48ab104eb3d2200632812c1b20b3985e3340f8fd` をcheckoutし、head SHA `f5902f9e...` に紐付くrunとしてGitHub connectorから取得した。別SHAのrunは代用していない。

mainの `.github/workflows/dotnet-test.yml` も再確認し、失敗時にTRX、標準出力、標準エラー、vstest診断ログ、binlog、環境情報、source archiveを収集してartifactへ公開する処理が存在することを確認した。

## 結論

前回closureで具体的に再現した2問題は修正され、actual producer metadataを使う2テストと既存境界5テストの計7件はすべて成功した。

一方、F1の根本契約である「diagnostics sampleが新規記録の主経路」はまだ完全には成立していない。tracker packet snapshot sidecarのmetadataが作成済みで、その実体だけが欠落した場合、正常なdiagnostics sampleが存在しても `SidecarMissing` がsample timelineと既定Field sourceを上書きすることをactual compositionで再現した。

そのためPR #20を独立最終レビュー完了とは判定しない。F1は未解決のまま **incomplete** とする。

本文修正やproduction code修正は独立レビュワーから行っていない。今回の追加物はこの詳細reportだけとし、mergeは行わない。

このreport-only commitをpushした後はPR current HEADが変わるため、新HEADと完全一致するCIの結果はPRコメント側で改めて確認・記録する。
