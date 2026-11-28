# Duck #144／PR #164 独立最終レビュー結果

**判定：`pass_with_held`（保留事項付き合格）**
**固定対象の必須修正：0件。**

対象3文書の技術的意味、操作手順、過去の検証記録は維持されています。Issue #144 全体の完了や文書検査の全件合格とは判定しません。

本報告は親から受領した独立担当の原結果を記録する行政的な証跡です。記録担当自身による独立レビューではありません。技術判定は下記の reviewed implementation HEAD にのみ適用します。

## 1. 固定対象と独立性

| 項目 | 確認結果 |
|---|---|
| PR | [#164](https://github.com/ibis-ssl/Duck/pull/164) |
| base | `849664d5e92b3e5ff805514eca7d221fea9e2c67` |
| reviewed implementation HEAD | `d2f9db4c49e43274009493e315514a0f94f28758` |
| 対象 tree | `b618068f6d066984b17e382c92c8ddb13109c075` |
| CodexSkill main | `dfe07e81d6a1c209f99b908acc8109f70b38a773` |
| 独立性 | PR #164 の実装・修正・通常レビューへの関与なし |
| 検証経路 | `remote_ci_only`、事前公開済み |

独立担当は指定の CodexSkill 手順、Duck の `AGENTS.md` を参照し、別担当を起動せず確認しました。

[固定差分](https://github.com/ibis-ssl/Duck/compare/849664d5e92b3e5ff805514eca7d221fea9e2c67...d2f9db4c49e43274009493e315514a0f94f28758) は指定の4パスだけです。許可一覧、製品コード、CI 設定に変更はありません。

## 2. 観点別判定

| 観点 | 判定 | 根拠 |
|---|---|---|
| README の仕様資料・起動例 | `checked_no_finding` | サブモジュール、固定コミット、接続先、起動引数、RuntimeHost の10 ms 設定を維持 |
| Testing README の技術的意味 | `checked_no_finding` | 編集用原図、発行物、起動前検査、復元・構築の責務が実装・設計と一致 |
| 工程台帳の歴史的記録・正本 | `checked_no_finding` | #53／#54、#78 の工程、07F の当時の検証範囲を保持 |
| リンク・コマンド・URL | `checked_no_finding` | 文書3件で基点との同一性を確認 |
| 変更範囲・互換性・依存 | `checked_no_finding` | 文書限定差分。実行契約や固定入力の変更なし |
| 通常報告と残件 | `checked_no_finding` | 対象 SHA、文書の Git blob、検査件数、残件の扱いを照合 |
| 現行 CI | `checked_no_finding` | 完全一致する HEAD の成功を確認 |
| セキュリティ・秘密情報 | `checked_no_finding` | 新たな秘密情報の追加や起動前検査の緩和なし |

### 技術的な確認根拠

- [`README.md:21,76–100`](https://github.com/ibis-ssl/Duck/blob/d2f9db4c49e43274009493e315514a0f94f28758/README.md)：仕様資料の固定参照、両 URL、`sim` の `ReorderWindowNs=10000000` を Git tree、起動設定、製品設定と照合。
- [`Testing/README.md:13–21,85–87`](https://github.com/ibis-ssl/Duck/blob/d2f9db4c49e43274009493e315514a0f94f28758/Testing/README.md)：起動前検査の責務を `scripts/start-duck-simulation.cs` および #158 の設計と照合。
- [`phases-status.md:33,187,356`](https://github.com/ibis-ssl/Duck/blob/d2f9db4c49e43274009493e315514a0f94f28758/Tracker/Design/phases-status.md)：課題・工程対応の意味を維持。特に07Fの14件、Python 6件、追加2入力前の75件、Release ビルド、固定イメージ起動未確認は[当時の検証記録](https://github.com/ibis-ssl/Duck/blob/98e27ea3417ebb54acbaa88111e673c082d52aa1/reports/evidence/issue81-crane-launch-overlay-20261029/verification.json)と一致します。

3文書に含まれる17個のコードブロック、48個の Markdown リンク先、明示 URL は基点から不変です。37件のリポジトリ内相対リンクは固定 tree 上に存在しました。

## 3. 文書検査と残件

[通常報告](https://github.com/ibis-ssl/Duck/blob/d2f9db4c49e43274009493e315514a0f94f28758/reports/issue-144-pr164-normal-review-20261127054502.md) の検証対象 `f49bcadd8266d274c75c96cecffb1e87495036a0` と現行 HEAD の間は報告書追加のみで、対象3文書の Git blob は一致しています。

| 検査 | 基点 | 候補 | 残件 |
|---|---:|---:|---|
| Yomiyasu・対象5文書 | 50 | 43 | あり |
| 許可一覧・対象5文書 | 1,691 | 1,673 | あり |

許可一覧検査は5文書とも終了値1です。集計の算術整合を確認しました。生の文書検査出力については独立再実行しておらず、既存検証の記録を根拠としています。

**外部メタデータに関する任意訂正：** PR 本文には許可一覧の指摘が9件減少とありますが、現行の通常報告では基点から18件減少です。2段階の変更でそれぞれ9件減少した履歴とも整合するため、PR 本文では計測段階を明示することを推奨します。これは固定対象の再修正を必要とする指摘ではありません。

## 4. 現行 CI

[CI 45947656100](https://github.com/ibis-ssl/Duck/actions/runs/45947656100) の成功を確認しました。

- `test` ジョブ：**1,618成功、0失敗、4スキップ**
- CM4 ネイティブ検査、AutoRef JAR ビルド、Linux 配布確認：成功
- Aspire 通信と全体受入：スキップ
- CI の試験統合コミット `c3e69c91d1a395ee3580fa1405e80b850f430973` は固定候補と同一 tree

通常報告の [旧 CI 45944942153](https://github.com/ibis-ssl/Duck/actions/runs/45944942153) も報告対象 HEAD と一致しています。旧 CI を現行 CI の代わりにはしていません。

## 5. 保留事項

- Issue #144 の残る文書指摘および許可語候補の合意。
- 工程台帳の個別検査出力の全件確認。
- 文書検査原本の独立再実行、既存構成図の実画面視認。
- FA780、Aspire 全体受入、全検査成功の確認。

今回の部分対応では、これらを合格条件に含めません。

## 6. 親への引き渡し

```yaml
review_mode: independent_final_review
reviewed_implementation_head: d2f9db4c49e43274009493e315514a0f94f28758
verdict: pass_with_held
required_findings: 0

verification_capability: remote_ci_only
decomposition_policy: forbidden
parallelism_mode: single_agent
report_persistence_mode: deferred_attestation

reservation_owner: review-enforcer
reservation_identity: duck-pr164-issue144-ifr-20261127-v1
reservation_state: metadata_only
pre_reserved_report_path: reports/issue-144-independent-final-review-20261127055057.md

runtime_profile:
  applied: null
  application_status: profile_unverified

report_attestation_allowed: true
report_attestation_head: null
```

実際のモデル・推論設定については確認可能な実行時プロファイルがないため、推測で補いません。上記は独立担当の引き渡し時点の状態です。独立担当はリポジトリの編集、push、マージ、PR コメント、手動 CI、実機操作を行っていません。

## 7. 報告保存の証跡と境界

```yaml
persistence_mode: report_attestation_commit
persistence_state: commit_pending
technical_head: d2f9db4c49e43274009493e315514a0f94f28758
administrative_parent: d2f9db4c49e43274009493e315514a0f94f28758
reservation_identity: duck-pr164-issue144-ifr-20261127-v1
reserved_report_path: reports/issue-144-independent-final-review-20261127055057.md
```

同じ予約 ID の下で本報告だけを追加する単一の管理上の証跡コミットとし、他のパスを変更しません。その SHA は生成後にブランチ外の引き渡しで記録します。証跡コミット自体はレビュー済み実装ではありません。その後に Git コミットが追加されれば、通常修正確認と同じ独立担当による限定指摘・CI 差分の確認を行わない限り完了判定は無効になります。証跡版の CI とマージ判断は別途確認が必要です。本報告はマージ許可や Issue 終了を認定しません。
