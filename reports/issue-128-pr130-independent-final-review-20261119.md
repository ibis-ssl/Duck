# PR #130 独立最終レビュー報告

## 判定

**`pass_with_held`**。凍結した実装 HEAD の採用を妨げる必須指摘は **0件**。任意改善は **1件**。保留中の検証制限と最終公開ゲートが残るため、完全合格とは判定しない。

## 対象と保存

| 項目 | 値 |
| --- | --- |
| 種別 | independent final review report |
| PR | [ibis-ssl/Duck #130](https://github.com/ibis-ssl/Duck/pull/130) |
| ブランチ | `task/issue-128-launcher-dotnet` |
| base | `main` |
| 比較元 | merge-base `985ce8e7eb9357bd08c2b76ebb33a85fd304f59f` |
| レビュー対象実装 HEAD | `0eb8f8da3f225c84958833d3bad5be469b705585` |
| 対象 tree | `96e8c7c0453188acb80edd7bed8e3f169c150c03` |
| 公開 PR HEAD | `a9a3a8f27394fd3d515e22535719298e9d6afa57` |
| 公開 HEAD との差 | 追跡文書2件と通常検証報告のみ。実行コード・テストは同一 |
| レビュー担当 | 実装・通常レビューに参加していない新規の単独担当。提供記録に氏名の記載なし |
| 保存方式 | `report_attestation_commit` |
| 予約パス | `reports/issue-128-pr130-independent-final-review-20261119.md` |
| 予約識別子 | `duck128-pr130-final-review-20261119-v1` (`reservation_owner=review-enforcer`, `metadata_only`) |

この報告は、レビュー対象実装 HEAD に対する技術判定を記録するための行政的な単一 attestation commit 用である。コミットはこの予約パスだけを変更し、実装変更を含めない。attestation SHA は保存後に外部で記録する。この報告コミットの後に Git コミットが追加された場合、このレビュー完了状態は無効となり、新しいレビューライフサイクルが必要となる。

## 公開 PR と通常再レビューの照合

公開 PR 本文では、ランチャーの置換、起動ファイル基準の作業コピー、ログ障害時の子プロセス回収、一時起動の資源回収、常駐起動・再起動・明示停止、Windows AppHost ログハンドル継承対策を説明している。対象範囲には `visibility_graph` と既存 C 補助プログラムの準備を含め、Crane オーバーレイ、固定イメージ、RuntimeHost 配置を変更しないこと、Windows RuntimeHost と Docker の通信等を後続 #134〜#136 に残すこと、Issue #128 をこの PR 単独では閉じないことを記している。

[通常再レビューの公開コメント](https://github.com/ibis-ssl/Duck/pull/130#issuecomment-6997772871) は、公開 HEAD `a9a3a8f27394fd3d515e22535719298e9d6afa57` に対する判定を「通過」とし、前回の6件すべての通過を維持している。同コメントは文章検査・差分検査の成功、全体スペル・許可語検査の既存指摘、実機試験を繰り返していないこと、コメント時点で同 HEAD の CI が実行中だったことを明記している。これは通常再レビューと公開 HEAD の記録であり、凍結 HEAD の CI 成功を示すものではない。

## 独立レビューの範囲と結果

| 確認範囲 | 処置 | 根拠・結果 |
| --- | --- | --- |
| 引数なし一時起動・回収 | `checked_no_finding` | `scripts/start-duck-simulation.cs:46`。保存済み同一試行の生ログで終了値0、stdout/stderr双方のEOF、7コンテナとネットワークの消失を確認。 |
| 常駐起動・既定 ID・明示停止 | `checked_no_finding` | 同ファイル `:133`, `:345`。ロック保持、全対象の owner/stack/run 照合後の削除、ID明示必須と末尾LF拒否を確認。既定IDによる再起動の実機記録も整合。 |
| 作業木固定・準備処理 | `checked_no_finding` | 同ファイル `:85`, `:170`, `:319`。起動ファイル起点、発行・構築順序、必須成果物確認を旧スクリプトと照合。 |
| ログ保全・エラー伝播 | `checked_no_finding` | 同ファイル `:208`, `:218`。ログ先行確保、出力障害検出、子孫終了、出力排出後の終了値返却を確認。 |
| Windows Ctrl+C | `checked_no_finding` | `Testing/Duck.Testing.AppHost/LauncherLogHandles.cs:15`, `Program.cs:177`。常駐停止後のハンドル継承による待機を防ぐ修正と、生のプロセス終了値を照合。 |
| テスト・文書・追跡・変更範囲 | `checked_no_finding` | 変更17パスと直接依存を確認。日本語 XML 説明、厳密 UTF-8、物理 LF、差分検査に問題なし。#136 のマルチキャスト実装と後続統合は対象外。 |

### 検証証拠の評価

- [Linux CI 44443697638](https://github.com/ibis-ssl/Duck/actions/runs/44443697638) のログでは1,457合格・0失敗・4 skip。packet-flow/full-stack は skip。これは公開 HEAD `a9a3a8f` の証拠であり、凍結 HEAD の CI 成功とは扱わない。
- FA780 実機の過去ソースと候補の対象コード同一性を確認した記録では、一時起動は約1,964 msで外側 `dotnet` が0となり、資源回収を確認。常駐起動は正常終了後も7コンテナを保持し、明示停止で回収。
- 対話端末の追加計測では AppHost・ランチャー・外側 `dotnet.exe` が各0、親 `pwsh.exe` が1。元の全試行で観測された値1の原因が確定したとは判断しない。根拠は `reports/issue-128-pr130-default-stack-id-verification-20261119.md:41`。
- 凍結 HEAD での Windows 標準全体テストは1,451合格・8失敗・2 skip。Linux verifier 必須8件の失敗は残る。Markdown 全体検査も cspell 744件で失敗しており、全体通過ではない。これらは差分への新規指摘なしという判断と区別する。

## 保留事項・未確認事項

保留事項の担当は親担当 `kero`。次の事項は完了扱いにしない。

- 凍結 HEAD 以降の最終公開と exact-head CI。
- WSL からの補助プログラム生成。
- 常駐資源へのコード変更反映。
- 他端末での確認。
- 実通信と全体受入。

これらは今回の限定レビュー範囲に必須修正を追加する根拠とはしない。#136 のマルチキャスト実装・後続統合も本レビューの対象外。

## 任意改善

低優先度の任意改善が1件ある。`LauncherLogHandles.cs:15` のハンドル非継承を、Docker 不要の短い Windows 子孫プロセス試験として残すと再発検知に役立つ。採用条件ではない。

## 実施していない作業

担当者は実装・通常レビューに参加していない新規担当として、一人で一回の全体評価を実施した。分担、別担当起動、MCP コネクタ利用、追加 Docker 起動、全体テスト再実行、修正、push、PR コメント、マージは行っていない。終了時はレビュー対象 HEAD 不変、作業木 clean と記録されている。

予約 `duck128-pr130-final-review-20261119-v1` を維持する。報告ファイルはレビュー時点で未作成であり、この attestation では当該パスだけを作成する。
