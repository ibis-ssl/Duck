# PR #20 独立最終レビュー報告

- 実施日時: 2026-09-17 20:45 JST
- 対象PR: #20 `docs: add RuntimeHost README and shared appsettings guide`
- 独立レビュー対象HEAD: `4d253f2892f58ef9fdba64cc81b21f9ccab5ca42`
- 判定: **held**
- merge: 実施しない

## 対象

本文レビュー対象は次の8文書のみとした。

1. `Tracker/Design/Core/tracker-core-engine-detail-design.md`
2. `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
3. `Tracker/Design/Core/tracker-architecture-plan.md`
4. `Tracker/Design/DebugHost/raw-vision-viewer-plan.md`
5. `Tracker/Design/RuntimeHost/runtime-host-plan.md`
6. `README.md`
7. `Tracker/Tracker.DebugHost/README.md`
8. `Tracker/Tracker.CaptureReplay/README.md`

`Tracker/Design/tasks-status.md`、`Tracker/Design/phases-status.md`、保守案件の設計書、Archive/履歴文書、`reports/**`、`tools/lint/README.md`、lint手順、`feedback-points/feedback-points.md` は本文レビュー件数へ含めていない。

## レビュー方法

既存report・過去レビューの合否は採用せず、対象HEADの本文、各台帳に記録された用語整理前本文、baselineからcurrentまでの台帳外変更行、必要な製品コードを独立して照合した。

台帳は確認漏れ防止の索引としてのみ使用し、台帳が埋まっていることを合格根拠にはしていない。各原出現IDについて原文、現在表現、現在位置、前後文脈を確認した。さらに、原出現IDの行範囲に含まれないbaseline→current変更行も別途抽出して確認した。

## 原出現台帳の整合確認

### 設計書

| 文書 | 原出現 | 確認 | unresolved | ID重複 | 本文SHA一致 |
| --- | ---: | ---: | ---: | ---: | --- |
| `tracker-core-engine-detail-design.md` | 133 | 133 | 0 | 0 | yes |
| `debug-host-cli-ui-detail-design.md` | 724 | 724 | 0 | 0 | yes |
| `tracker-architecture-plan.md` | 565 | 565 | 0 | 0 | yes |
| `raw-vision-viewer-plan.md` | 524 | 524 | 0 | 0 | yes |
| `runtime-host-plan.md` | 196 | 196 | 0 | 0 | yes |
| **合計** | **2,142** | **2,142** | **0** | **0** | **all yes** |

### README

| 文書 | 原出現 | 確認 | unresolved | ID重複 | 本文SHA一致 |
| --- | ---: | ---: | ---: | ---: | --- |
| `README.md` | 62 | 62 | 0 | 0 | yes |
| `Tracker/Tracker.DebugHost/README.md` | 423 | 423 | 0 | 0 | yes |
| `Tracker/Tracker.CaptureReplay/README.md` | 43 | 43 | 0 | 0 | yes |
| **合計** | **528** | **528** | **0** | **0** | **all yes** |

8文書合計2,670件のIDはすべて一意で、欠落0、unresolved 0、台帳の最終本文SHA-256と対象HEAD本文のSHA-256は全件一致した。台帳記録の原文行・現在行と実ファイルの不一致も0件だった。

## 用語整理の本文レビュー結果

2,670原出現の全件と、台帳の出現範囲外にあるbaseline→current変更行を確認した。

- lintを通すためだけの無理な日本語化: 追加指摘なし
- 元の表現へ戻すべき不要変更: 追加指摘なし
- 技術的意味の反転・入力/出力の混同: 追加指摘なし
- 送信と内部通知、出力順と送信順の混同: 追加指摘なし
- raw visionを画像・動画と誤認させる表現: 追加指摘なし
- 設定値と設定プロファイルの混同: 追加指摘なし
- 観測値と推定値、liveとdiagnostics replay、snapshot種別の混同: 下記F1を除き追加指摘なし
- クラス名、型名、API名、設定キー、正式UI名の不自然な翻訳: 追加指摘なし
- 承認済み表現: `フィールド`、`設定プロファイル`、`キャプチャー`、`単体テスト`、`相対パス`、`コミット` を確認。対象8文書に短縮形の「キャプチャ」、旧「設定セット」、「競技場」は残っていない。
- `Tracker Comparison`、`Vision Input`、`Play`、`Fast Forward`、`Stop`、`Split`、`Overlay`、`Layer A` / `Layer B` 等のUI名は正式表記を維持している。

製品コードでは、`KickEventState` / `BallContactState` / `BallLeftFieldState`、diagnostics sampleの書き込み・読み取り、`FlushEachPacket` の実処理、受信先設定の解決など、本文だけでは曖昧になり得る箇所を照合した。

## 未解決指摘

### F1: diagnostics sample導入後の現在仕様が3文書へ同期されておらず、設計・README・実装が矛盾している

**状態: 未解決。独立最終レビューをheldとする理由。**

現行実装と新しい設計では、新規CaptureOnの診断再生・Field sourceの主経路は `diagnostics-samples.jsonl` である。`DiagnosticsSampleHostedService` / `DiagnosticsSampleCaptureLoop` がUI描画周期や追跡フレーム確定周期から独立して採取し、capture metadataは `DiagnosticsSampleSidecarPath` と `DiagnosticsSampleLog` を持つ。`TrackerDiagnosticsLogReader` と `TrackerDiagnosticsComparisonViewStateReader` はdiagnostics sample sidecarを読み、render snapshotだけの旧形式は非対応または機能制限された旧形式として扱う。

この現在仕様は `Tracker/Design/DebugHost/raw-vision-viewer-plan.md` 244行、260行、325行付近と `Tracker/Design/RuntimeHost/runtime-host-plan.md` 102-106行付近では明示されているが、次の記述が同期されていない。

#### A. `Tracker/Design/Core/tracker-architecture-plan.md`

該当例:

- 126行: CaptureOn成果物をcapture metadata / render snapshot / tracker packet snapshot / alignment sidecarとして列挙するが、diagnostics sample sidecarを含めない。
- 148-156行: 新規キャプチャーのreplay timelineとField sourceをrender snapshot中心で説明する。
- 574行: capture metadataの相対パスにdiagnostics sample sidecarとそのログ情報を含めない。
- 582-586行: 新規 `/diagnostics` と診断画面の描画をrender snapshot中心で説明する。586行は「同じ共通名の `*.render-snapshots.jsonl.gz` がある場合」に未加工入力と追跡フレームを描画すると現在仕様として記載している。

この文書には `diagnostics sample` / `diagnostics-samples.jsonl` の記載が0件であり、`raw-vision-viewer-plan.md` / `runtime-host-plan.md` の現在仕様と整合していない。

関連原出現ID例: `O001101`, `O001111`, `O001123`, `O001132`, `O001140`, `O001144`, `O001155`, `O001156`, `O001162`, `O001266`, `O001348`, `O001349`, `O001350`, `O001388`, `O001399`, `O001464`, `O001465`, `O001634`, `O001084`, `O001095`, `O001126`, `O001135`, `O001163`, `O001170`, `O001351`, `O001390`, `O001398`, `O001493`, `O001098`, `O001248`, `O001251`, `O001276`, `O001305`, `O001306`, `O001442`, `O001477`, `O001605`, `O001606`, `O001619`, `O001620`, `O001625`。

#### B. `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`

124-130行の「診断ログ・再生の最新経路と旧形式の表示」は、diagnostics sample sidecarを新規記録の主経路として正しく規定している。一方、その後の現在仕様として読める節に旧render snapshot経路が残る。

該当例:

- 167行付近: 高速表示元に対しraw visionとrender snapshotを保持する説明。
- 197行: `Vision Input` を「選択中の診断記録に対応する render snapshot の `SourceDetections`」から描画すると規定。
- 200行: `ibis tracker` を「選択中の診断記録に対応する render snapshot」から描画すると規定。

これらは旧仕様・変更履歴として明示されておらず、同文書124-130行の「最新経路」と矛盾する。

関連原出現ID例: `O002181`, `O002195`, `O002213`, `O002242`, `O002243`, `O002302`, `O002303`, `O002366`, `O002367`, `O002378`, `O002420`, `O002561`, `O002429`, `O002523`, `O002524`, `O002525`, `O002042`, `O002113`, `O002530`。

#### C. `Tracker/Tracker.DebugHost/README.md`

README前半は `diagnostics-samples.jsonl` と `DiagnosticsSampleIntervalMilliseconds` を正しく説明しているが、後半の操作手順と状態説明が旧tracker snapshot/render snapshot中心のままである。

該当例:

- 140行: capture metadataから辿る項目一覧に `DiagnosticsSampleSidecarPath` / `DiagnosticsSampleLog` がない。現行コード `VisionPacketCaptureSession` は両方を保存する。
- 244行: Capture Off後に確認する生成物から `diagnostics-samples.jsonl` が抜け、metadata確認項目にもdiagnostics sampleのrecord/skipped/error countがない。
- 253行: `Ready` を「capture metadata と `tracker-packet-snapshots.jsonl` を読み、比較を作成できる状態」と限定する。しかし現行 `TrackerDiagnosticsComparisonViewStateReader` はdiagnostics sample indexが存在し、tracker snapshot metadataが無い経路でも `Ready` を返し得る。
- 256行: `SnapshotMetadataMissing` をtracker snapshot metadata欠落だけで説明するが、diagnostics sampleがある現在経路との関係を説明していない。
- 260行: `SidecarCorrupt` を一般的なJSONL破損として説明するが、現行readerはdiagnostics sample sidecarの読み取り失敗でも同statusを返す。
- 245行: 新規経路の `Vision Input` がdiagnostics sampleから復元されることを明示せず、raw vision / render snapshotを同列に説明している。

関連原出現ID例: `O004097`, `O004166`, `O003959`, `O004113`, `O004141`, `O004176`, `O004206`, `O004207`, `O004208`, `O004209`, `O004211`, `O003968`, `O004143`, `O003975`, `O004212`, `O003920`, `O003995`。

#### 推奨する修正方向

- 新規キャプチャーの正規経路を `diagnostics-samples.jsonl` / `DiagnosticsSampleSidecarPath` / `DiagnosticsSampleLog` に統一する。
- `Vision Input` / 自前トラッカーのField sourceをdiagnostics sampleから復元する現在仕様に合わせる。
- render snapshotだけを使う記述は、現在経路ではなく旧形式・機能制限付き経路であることを明示するか、現在仕様から除く。
- Architecture、CLI/UI詳細設計、DebugHost READMEの成果物一覧、metadata一覧、手動検証、status説明を同時に同期する。
- 修正後は関連原出現台帳の最終表現・位置・SHAを更新し、別の独立レビュワーで再確認する。

## READMEと設計書の相互整合

F1以外では、次を相互照合し、矛盾を確認しなかった。

- RuntimeHost: 画面なしでSSL-Visionを受信し、周期処理し、公式形式の `TrackerWrapperPacket` を送信する。
- DebugHost: Web UI、診断、キャプチャー・再生・比較を担当し、RuntimeHostの実時間周期をUI描画や診断保存から分離する。
- CaptureReplay: 保存済みキャプチャーのCLI再生・分析を担当する。
- `sim` 設定プロファイルのRuntimeHost受信先 `224.5.23.2:10020` と送信先 `224.5.23.2:11010`。
- DebugHostのtracker receiveは未指定時に起動時解決済みの送信先へ追従し、実行中の設定プロファイル切替では再構成しない。
- diagnostics sampleの既定採取周期100 ms、0以下は既定値へ戻す。
- 通常再生は毎秒30回相当でwall-clockに追従し、早送りは別契約とする。

## lint / diff check

レビュー開始時の対象HEAD `4d253f2892f58ef9fdba64cc81b21f9ccab5ca42` で実施した。

- `npm run lint:md`: 成功、終了値0、18文書、cspell指摘0。
- `git diff --check`: 成功、終了値0。
- `git diff --check f5482ab85d49832a21ef6029d6aa3354f6c7c4f4..4d253f2892f58ef9fdba64cc81b21f9ccab5ca42`: 成功、終了値0。

最初の隔離環境ではsystem Pythonに `sudachipy` が無くwhitelist段階が終了値2になったため、`tools/lint/requirements.txt` と版が一致する既存レビュー用venvをPATHへ追加して再実行した。textlint/cspellは初回から成功しており、依存解決後の同一HEADで全lintが成功した。

## CI

レビュー対象HEADと完全一致するrunのみを証拠とした。

- reviewed HEAD: `4d253f2892f58ef9fdba64cc81b21f9ccab5ca42`
- workflow: `.NET tests`
- run id: `35190977849`
- run number: `121`
- run head SHA: `4d253f2892f58ef9fdba64cc81b21f9ccab5ca42`
- conclusion: `success`

`main` の `.github/workflows/dotnet-test.yml` には、失敗調査用としてTRX、標準出力、標準エラー、vstest診断ログ、binlogをartifactへ保存する処理があることも確認した。

この報告書だけを追加するreport-only commitをPRへpushする場合、そのcommitは本文レビュー対象HEADとは別の管理用HEADになる。最終PRコメントでは、その新HEADと完全一致するworkflow runの有無・結果を別途確認して記録する。

## held / unexplored

- held: F1 1件（3文書にまたがるdiagnostics sample導入後の仕様同期漏れ）。
- unexplored: 対象8文書の本文レビュー範囲について未確認項目なし。
- 本文修正は独立レビュー担当から行っていない。

## 結論

対象5設計書2,142件、3 README 528件、合計2,670原出現と台帳外変更行を独立して読み直した。用語整理による不自然な日本語化、技術的意味の反転、不要な原文変更について追加の未解決指摘は見つからなかった。

一方、diagnostics sample sidecar導入後の現在仕様が `tracker-architecture-plan.md`、`debug-host-cli-ui-detail-design.md`、`Tracker.DebugHost/README.md` の一部へ同期されておらず、設計書間およびREADME/実装間に矛盾が残っている。したがって、現HEADを独立最終レビュー完了・未解決指摘0とは判定しない。F1修正後、台帳同期・lint・current HEAD一致CIを行い、別の独立レビュワーによる再確認が必要である。
