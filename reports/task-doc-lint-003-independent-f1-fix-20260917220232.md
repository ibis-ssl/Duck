# DOC-LINT-003 独立最終レビュー F1 対応報告

- 対象: ibis-ssl/Duck PR #20
- 独立レビュー記録: `reports/pr20-independent-final-review-20260917.md`
- 指摘ID: F1
- 修正開始時のPR HEAD: `0716ae21279a6d8ca907e500fba98664567b4a7a`
- 独立レビューの実装確認対象HEAD: `4d253f2892f58ef9fdba64cc81b21f9ccab5ca42`
- 役割: 指摘修正担当。独立再レビュー担当ではない。

## 指摘の要旨

独立最終レビューでは、`diagnostics sample sidecar` 導入後の実装と、構成設計、DebugHost詳細設計、DebugHost READMEの説明に不整合があると指摘された。

実装では新規キャプチャーの `Vision Input` と `ibis tracker` を `diagnostics-samples.jsonl` の同一採取記録から復元し、diagnostics sample tick を再生位置の選択軸にしている。一方、対象文書の一部には render snapshot や最速の表示元の更新時点を新規経路の主な入力・選択軸とする古い説明が残っていた。

## 実装との再照合

次の製品コードを読み、現在の契約を確認した。

- `Tracker/Tracker.DebugHost/Tracking/DiagnosticsSampleLog.cs`
- `Tracker/Tracker.DebugHost/Tracking/DiagnosticsSampleCaptureLoop.cs`
- `Tracker/Tracker.DebugHost/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
- `Tracker/Tracker.DebugHost/Vision/VisionPacketCaptureSession.cs`
確認した現在仕様は次のとおり。

- `DiagnosticsSampleHostedService` / `DiagnosticsSampleCaptureLoop` はUI表示に依存せず採取を行う。
- `diagnostics-samples.jsonl` は未加工入力概要と自前トラッカーの追跡結果概要を同じ採取記録へ保存する。
- 新規記録の replay timeline は diagnostics sample tick を選択単位にする。
- `Vision Input` は選択中の採取記録にある未加工入力概要から復元する。
- `ibis tracker` は同じ採取記録にある追跡結果概要から復元し、自前の tracker packet snapshot を必須にしない。
- 外部トラッカーは tracker packet snapshot と alignment sidecar、または選択時点以前の latest-before snapshot を使う。
- diagnostics sample sidecar が読めれば、tracker snapshot metadataがなくても既定2表示元について `Ready` を構成できる。
- diagnostics sample sidecar がない旧記録では、旧形式または機能制限付きの表示へ落とす。render snapshot を新規経路の主な物体表示・再生位置へ戻さない。

## 本文修正

次の3文書だけをF1の本文修正対象にした。

- `Tracker/Design/Core/tracker-architecture-plan.md`
- `Tracker/Design/DebugHost/debug-host-cli-ui-detail-design.md`
- `Tracker/Tracker.DebugHost/README.md`

新規記録の保存物へ `diagnostics-samples.jsonl` を明示し、capture metadata の `DiagnosticsSampleSidecarPath` / `DiagnosticsSampleLog` と tracker snapshot / alignment の状態を区別した。新規 replay timeline、既定Field source、外部トラッカーの比較経路を現在実装へ揃えた。
render snapshot自体は削除せず、旧形式の表示、フィールド形状などの補助情報として残した。Field source selectorの配置、選択状態保持、通常再生の表示更新上限、報告項目などF1と無関係な既存契約も維持した。

## 台帳同期

本文は行数を変えずに修正し、対応位置を維持したうえで、3文書の原出現台帳と変更単位の台帳を修正後本文へ再同期した。

| 文書 | 原出現 | 変更単位 | 構造検証 |
| --- | ---: | ---: | --- |
| 構成設計 | 565 | 247 | エラー0 |
| DebugHost詳細設計 | 724 | 90 | エラー0 |
| DebugHost README | 423 | 75 | エラー0 |

F1に関係する台帳行では最終表現、文脈、判断理由、実装根拠を更新した。その他の行も現在本文の内容と本文SHA-256へ再同期した。

独立レビュー対象の5設計書と3 READMEを改めて集計すると、2,670 / 2,670件、ID重複0、欠落0、本文SHA-256不一致0だった。この結果は修正担当の構造自己点検であり、独立再レビューの合格証明ではない。

構造証跡:

- `reports/diagnostics/pr20-f1-diagnostics-sidecar-20260917/ledger-sync-validation.json`
- `reports/diagnostics/pr20-f1-diagnostics-sidecar-20260917/review-scope-validation.json`

## 検証
F1本文修正直後の対象3文書では、意味修正の文章中で既存の許可済み複合語を単独英単語へ分解したため、最初のfocused検証でcspellと許可一覧検査が失敗した。許可一覧や除外設定は変更せず、既存の許可済み複合語、正式識別子、自然な日本語へ整理した。

最終結果:

| 検証 | 結果 |
| --- | --- |
| 対象3文書 textlint | 終了値0 |
| 対象3文書 cspell | 終了値0、指摘0 |
| 対象3文書 許可一覧検査 | 終了値0 |
| `RuntimeHostDiagnosticsSampleBoundaryContractTests` | 終了値0 |
| 全18対象 `npm run lint:md` | 終了値0、cspell指摘0 |
| `git diff --check` | 終了値0 |

進捗文書追記後の全体lintも途中で3回失敗した。原因は追記した進捗説明の未登録表記であり、許可一覧を増やさず自然な既存表現へ修正した。失敗記録を成功ログで上書きしていない。

検証ログ:

- `reports/diagnostics/pr20-f1-diagnostics-sidecar-20260917/focused-red/`
- `reports/diagnostics/pr20-f1-diagnostics-sidecar-20260917/focused-green/`
- `reports/diagnostics/pr20-f1-diagnostics-sidecar-20260917/full-lint-red/`
- `reports/diagnostics/pr20-f1-diagnostics-sidecar-20260917/full-lint-r2-red/`
- `reports/diagnostics/pr20-f1-diagnostics-sidecar-20260917/full-lint-r3-red/`
- `reports/diagnostics/pr20-f1-diagnostics-sidecar-20260917/full-lint-green/`
- `reports/diagnostics/pr20-f1-diagnostics-sidecar-20260917/tests/`
## 公開と再レビュー

F1の本文・台帳修正commitは `f9f798f302a2b87f0bcad02714403b07df24a90f`。

この報告書の作成時点では、F1修正はまだ公開前であり、修正後の公開HEADに一致するCIは未確認である。push後にPR current HEADをGitHub connectorから再取得し、そのSHAと一致するworkflow runだけをCI証拠としてPRコメントへ記録する。

F1は修正担当の自己点検では閉じない。本文修正、台帳同期、lint、契約テスト、公開HEAD一致CIが揃った後も、別の独立レビュー担当による再レビューが必要である。再レビューでF1の解消と新しい阻害指摘0が確認されるまでは、PR #20の独立最終レビュー完了とは扱わない。

mergeは行わない。