# PR #20 独立最終レビュー closure 再確認

- Generated at: 2026-09-17T22:33:55+09:00
- Repository: `ibis-ssl/Duck`
- PR: `#20 docs: add RuntimeHost README and shared appsettings guide`
- Review kind: same independent reviewerによる final closure review
- Reviewed current HEAD: `6732f192d764051377c9695fde51bb18a35bd88b`
- Previous independent review report: `reports/pr20-independent-final-review-20260917.md`
- Previous finding: `F1` diagnostics sample sidecar導入後の現在仕様が設計書・READMEへ同期されていない
- Closure verdict: **incomplete**
- Merge: 実施していない

## 結論

前回F1に対する文書修正は、対象3文書へ `diagnostics-samples.jsonl`、`DiagnosticsSampleSidecarPath`、`DiagnosticsSampleLog`、diagnostics sample tickを反映している。

しかし、現在の文書が要求する通常の新規キャプチャー経路と、現行実装の実際の合成経路が一致していない。特に、実際の `VisionPacketCaptureSession` が生成する metadata では `TrackerSnapshotLog` が常に存在し、tracker snapshot sidecarが作成されていない場合は `IsCreated=false` になる。このmetadataを `TrackerDiagnosticsComparisonViewStateReader` に渡すと、diagnostics sample sidecarが正常でも `Ready` ではなく `SidecarNotCreated` となり、replay timelineも構成されない。

したがって、F1はclosureできない。独立最終レビュー完了条件の「READMEと設計書と実装の整合」「未解決レビュー指摘0」を満たしていない。

## Closureレビュー範囲

前回の独立最終レビューでは対象8文書を一つの範囲として全文確認済みである。今回は同一独立レビュワーによるclosureのため、前回F1の修正差分と影響範囲、台帳同期、lint、focused evidence、current HEAD一致CIだけを再確認した。

F1修正commit:

- `f9f798f302a2b87f0bcad02714403b07df24a90f` `docs(review): sync diagnostics sample sidecar specification`

F1修正後の管理・報告commit:

- `1aca92958fd08ce745964c05c08a9fe3bcac773f` report
- `6732f192d764051377c9695fde51bb18a35bd88b` lint evidence normalization

F1本文修正対象:

1. `Tracker/Design/Core/tracker-architecture-plan.md`
2. `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
3. `Tracker/Tracker.DebugHost/README.md`

前回対象8文書の残り5文書はclosure差分で本文変更されていない。

## 原出現台帳の再確認

current HEADで8文書の対応台帳を独立に再計算した。

| 文書 | 件数 | unresolved | ID重複 | 本文SHA一致 |
| --- | ---: | ---: | ---: | --- |
| `tracker-core-engine-detail-design.md` | 133 | 0 | 0 | yes |
| `debug-host-cli-ui-detail-design.md` | 724 | 0 | 0 | yes |
| `tracker-architecture-plan.md` | 565 | 0 | 0 | yes |
| `raw-vision-viewer-plan.md` | 524 | 0 | 0 | yes |
| `runtime-host-plan.md` | 196 | 0 | 0 | yes |
| `README.md` | 62 | 0 | 0 | yes |
| `Tracker.DebugHost/README.md` | 423 | 0 | 0 | yes |
| `Tracker.CaptureReplay/README.md` | 43 | 0 | 0 | yes |
| **合計** | **2,670** | **0** | **0** | **all yes** |

台帳構造は整合しているが、F1の合否根拠にはしていない。

## F1 closure completeness matrix

| 要素 | 要求 | 確認結果 | 判定 |
| --- | --- | --- | --- |
| Required action | 新規診断再生を diagnostics sample sidecar基準へ同期する | 3文書は修正済み | pass |
| Production path | 実際のCaptureOn metadataとreaderが同じ契約で動く | `TrackerSnapshotLog.IsCreated=false` でsample pathが `SidecarNotCreated` に遮断される | **mismatch** |
| Actual composition fixture | 実際の `VisionPacketCaptureSession` 生成metadataをreaderへ渡すテスト | committed testには存在しない | **missing** |
| Focused evidence | sample timeline / Field source / legacy degradedを検証する | `RuntimeHostDiagnosticsSampleBoundaryContractTests` 5件は成功。ただし手書きmetadataで `TrackerSnapshotLog` 自体を省略している | partial |
| CI delta | current HEAD一致run | run `35225057809` は成功、329件成功 | pass |

Production pathとactual composition fixtureが揃っていないため、closure条件を満たさない。

## F1: 未解決内容

### 文書が現在仕様として要求している内容

`Tracker/Design/Core/tracker-architecture-plan.md`:

- line 148: 新規キャプチャーでは `Vision Input` と自前トラッカーを同じ diagnostics sample tick の概要から復元する。
- line 150: replay timelineは `diagnostics-samples.jsonl` の `sampleReceivedAt` / diagnostics sample tickを選択単位にする。
- line 582: 新規診断画面は diagnostics sample sidecarを replay timeline と `Vision Input` / `ibis tracker` の主読み取り元にする。
- line 586: render snapshotを新規記録の物体表示や replay timeline の主入力にしない。

`Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`:

- line 193: 新規記録の既定 `Vision Input` / `ibis tracker` を同一 diagnostics sample tickから復元し、tracker packet snapshotにown記録がなくても表示できる。
- line 221: 新規 `Vision Input` / `ibis tracker` をdiagnostics sample tickから直接解決する。
- line 318-321: 外部トラッカーが存在しなくてもsample sidecarで既定2表示元を再生可能とする。

`Tracker/Tracker.DebugHost/README.md`:

- line 87: 新規キャプチャーは `diagnostics-samples.jsonl` から `Vision Input` / `ibis tracker` を描画する。
- line 94: replay timelineは diagnostics sampleの採取時系列を使う。
- line 253: 読み取り可能な `diagnostics-samples.jsonl` **だけでも** `Vision Input` / `ibis tracker` の replay timeline / Field sourceを構成でき、`Ready` になると説明している。

### 現行実装の実際の経路

`Tracker/Tracker.DebugHost/Vision/VisionPacketCaptureSession.cs`:

- line 191: metadataへ `TrackerSnapshotLog = new ...` を常に書く。
- line 194: tracker snapshot sidecarがなければ `TrackerSnapshotLog.IsCreated=false` とする。
- 同じmetadataへ `DiagnosticsSampleSidecarPath` / `DiagnosticsSampleLog` も書く。

`Tracker/Tracker.DebugHost/Program.cs`:

- line 70: `TrackerConnectionLibReceiverHostedService` は `Tracker:Receive:Enabled=true` の場合だけ登録される。
- README記載どおりReceive既定は無効なので、通常の新規キャプチャーで diagnostics sampleは存在しても tracker snapshot sidecarが存在しない構成は正規に発生する。

`Tracker/Tracker.DebugHost/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`:

- line 128: diagnostics sampleだけで `Ready` にする分岐は `metadata.TrackerSnapshotLog is null` の場合に限定される。
- line 182: `TrackerSnapshotLog` が存在して `IsCreated=false` の場合は `SidecarNotCreated` を返し、replay timelineは空になる。
- line 392以降: `ibis tracker` が diagnostics sampleへフォールバックするのは tracker snapshot sidecarが利用不可の場合で、sidecarが存在する場合はcomparison index側を使う。

このため、修正文書の「新規キャプチャーでは diagnostics sample sidecarが主経路」という説明は、現行production pathと一致していない。

## 独立actual-composition probe

レビュー専用worktreeに一時的な未追跡テストを作成し、結果保存後に削除した。PRへは含めていない。

probeは次を実際のproduction classで構成した。

1. `VisionPacketCaptureSession`
2. `DiagnosticsSampleLogWriter`
3. `VisionLiveDisplaySnapshotProvider`
4. diagnostics sampleを1件保存
5. `VisionPacketCaptureSession` が生成した実metadataを読む
6. `TrackerDiagnosticsComparisonViewStateReader.Load(...)` へそのmetadataに対応する診断パスを渡す

metadataの事実:

- `DiagnosticsSampleLog.IsCreated == true`
- `TrackerSnapshotLog.IsCreated == false`

文書どおりなら期待値は `Ready` + diagnostics sample timeline 1件。

実結果:

```text
Expected: Ready
Actual:   SidecarNotCreated
Total tests: 1
Failed: 1
```

よってactual composition上でF1を再現した。

## Focused test確認

committed `RuntimeHostDiagnosticsSampleBoundaryContractTests` はrestore込みで独立再実行し、5 / 5成功した。

成功した5件:

- `Load_WithOnlyLegacyRenderSnapshotSidecarReportsUnsupportedDegradedLegacy`
- `LoadReplayTimeline_UsesDiagnosticsSampleTicksEvenWhenWorldFrameCommittedDoesNotAdvance`
- `LoadFieldSourceFrame_ForVisionInputRestoresFromDiagnosticsSampleSidecar`
- `LoadFieldSourceFrame_ForVisionInputWithoutDiagnosticsSampleDoesNotFallbackToRenderSnapshot`
- `UiState_ForVisionInputAndIbisTrackerLoadsDiagnosticsSampleFrames`

ただし `CreateDiagnosticsSampleSession(...)` が手書きmetadataで `TrackerSnapshotLog` プロパティ自体を省略している。そのためactual producerが出す `TrackerSnapshotLog.IsCreated=false` の分岐を検証していない。

## lint / diff check

current reviewed HEAD `6732f192d764051377c9695fde51bb18a35bd88b`:

- `npm run lint:md`: 終了値0
  - 対象18文書
  - cspell指摘0
  - textlint / whitelist成功
- `git diff --check f5482ab85d49832a21ef6029d6aa3354f6c7c4f4..HEAD`: 終了値0
- `git diff --check 0716ae21279a6d8ca907e500fba98664567b4a7a..HEAD`: 終了値0

最初のlint試行はreview worktreeにgit管理外 `.agents` toolingが無いため終了値2だった。本文起因ではない。主worktreeにある同一toolingを一時symlinkで参照し、current HEAD本文へ再実行した結果が上記終了値0である。symlinkは実行後に削除した。

## CI

PR current HEAD:

`6732f192d764051377c9695fde51bb18a35bd88b`

このSHAと完全一致するworkflow runだけを確認した。

- workflow: `.NET tests`
- run id: `35225057809`
- run number: `123`
- run head SHA: `6732f192d764051377c9695fde51bb18a35bd88b`
- status: `completed`
- conclusion: `success`
- job log: `Passed: 329, Failed: 0, Skipped: 0, Total: 329`

別SHAのrunは証拠に使用していない。

## 診断artifact workflow

GitHubの`main`にある `.github/workflows/dotnet-test.yml` を再確認した。

失敗時に少なくとも次を保存する。

- TRX (`dotnet-test.trx`)
- 標準出力 (`dotnet-test.stdout.log`)
- 標準エラー (`dotnet-test.stderr.log`)
- vstest diagnostics (`vstest-diagnostics.log`)
- binlog (`dotnet-test.binlog`)
- environment情報
- source archive

`actions/upload-artifact` により失敗artifactとして公開する構成である。

## 修正に必要な方向

F1を閉じるには、少なくとも次が必要。

1. `TrackerDiagnosticsComparisonViewStateReader.Load(...)` が、実際の新規capture metadataで `DiagnosticsSampleLog.IsCreated=true` なら、`TrackerSnapshotLog` が存在して `IsCreated=false` でも diagnostics sample timeline /既定Field sourceを正常に構成できるようにする。
2. 新規キャプチャーの `ibis tracker` が同じ diagnostics sample tick のtracked summaryを主経路とする設計を実装に一致させる。tracker snapshot sidecarのown packet有無で意味が切り替わらないようにするか、意図が異なるなら3文書をその実装契約へ戻して整合させる。
3. 実際の `VisionPacketCaptureSession` + `DiagnosticsSampleLogWriter` + `TrackerDiagnosticsComparisonViewStateReader` を同じfixtureで使うactual-composition testを追加する。
4. `Tracker:Receive:Enabled=false` とtrueの双方をfixtureで確認し、sample-only正常系と外部tracker比較追加経路を分離して固定する。
5. 修正後に関連台帳SHAを同期し、lint、focused test、全CI、別独立closure reviewを実施する。

## Held / unexplored

- 今回は同一独立レビュワーによるF1 closure reviewであり、前回の8文書全文レビューをゼロから繰り返していない。
- F1と無関係な新しい設計論点は追加していない。
- 本文・製品コードは変更していない。
- mergeはしていない。

## 最終判定

**incomplete**

F1は未解決。台帳・lint・current HEAD一致CIは通過しているが、actual production compositionが修正文書の現在仕様を満たさず、committed focused testもそのcompositionを検証していないため、独立最終レビュー完了とは扱えない。
