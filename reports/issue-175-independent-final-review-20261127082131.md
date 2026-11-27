# Duck #175／Draft PR #168 独立最終再レビュー

**判定：`pass_with_held`（保留事項付き合格）**

**前回必須2件は解消。未解消の必須指摘は0件。**

前回と同じ独立担当・予約で、IFR-175-01／02 と CI 差分に限定した閉鎖確認の確定証拠を、報告担当が転記する。報告担当による独立レビューの再実行ではない。

## 1. 対象版

| 項目 | 独立担当の確認結果 |
| --- | --- |
| [PR #168](https://github.com/ibis-ssl/Duck/pull/168) | Draft・OPEN・MERGEABLE |
| 初回独立レビュー HEAD | `a176627ff0ecb786a9fa5b4b7c1175b518489546` |
| 修正技術 HEAD | `5c59eceeaa2443a938f0fb11f7b2abccbca13a1e` |
| 今回の固定 HEAD | `86bc905b919eb5b24b48f7f3479b3bd19d5158c3` |
| 固定 tree | `8ffc6f9057908b180ec0ee36d52d580b9fd1ed57` |

修正技術 HEAD から固定 HEAD への差分は通常報告書1ファイルの追補のみ。

## 2. 必須指摘の閉鎖確認

| 指摘 | 判定 | 根拠 |
| --- | --- | --- |
| IFR-175-01 | 解消 | 共通一時配置を使う受入試験4件すべてで、終了処理の成否と分離して回収・主例外保持を行う。 |
| IFR-175-02 | 解消 | 個別回収障害を収集して他の所有ファイルも回収を試み、配置時の元例外を維持する。 |

### IFR-175-01

独立担当は[受入試験の実装](https://github.com/ibis-ssl/Duck/blob/86bc905b919eb5b24b48f7f3479b3bd19d5158c3/Tracker/Tracker.Tests/AspireFullStackAcceptanceTests.cs)を静的に確認した。

- ロック競合試験：2674–2728行。内側・外側とも元例外保持。
- NET-003：3184–3299行。従来修正を保持。
- Crane 起動失敗試験：3647–3763行。共通回収処理へ接続。
- 起動中キャンセル試験：3970–4096行。破棄処理の失敗にかかわらず所有ファイルの回収を試行。

4経路の接続を静的に確認し、共通処理の終了失敗・試験本体との重複失敗の既存回帰2件を含む検証記録と照合した。4件の実際の全受入試験を再実行したのではない。

### IFR-175-02

独立担当は[共通配置・回収実装](https://github.com/ibis-ssl/Duck/blob/86bc905b919eb5b24b48f7f3479b3bd19d5158c3/Tracker/Tracker.Tests/LocalAppHostAssetStaging.cs)と[追加回帰試験](https://github.com/ibis-ssl/Duck/blob/86bc905b919eb5b24b48f7f3479b3bd19d5158c3/Tracker/Tracker.Tests/LocalAppHostAssetStagingTests.cs)を照合した。

個別読取失敗、個別削除失敗、配置失敗と巻き戻し失敗の重複を扱う新規3件があり、変更済みファイル・既存ファイルの保護も維持されている。

通常修正確認の必須指摘0件、隔離試験12件成功の[既存記録](issue14-cm4-helper-preflight-20261127.md)を照合した。独立担当による再実行はしていない。

## 3. CI と保留事項

| 対象 | 結果 |
| --- | --- |
| [技術版 CI 45966578955・初回](https://github.com/ibis-ssl/Duck/actions/runs/45966578955/job/140396575389) | 失敗：Game Controller の初回準備待ちタイムアウト |
| [同 CI・本人許可の1回再実行](https://github.com/ibis-ssl/Duck/actions/runs/45966578955/job/140406471182) | 失敗：同期出力の呼出し到達待ちタイムアウト |
| [固定版 CI 45972691135](https://github.com/ibis-ssl/Duck/actions/runs/45972691135) | 成功 |

独立担当は両失敗の実行履歴を API で確認した。タイムアウトの詳細は通常報告の既存記録による。固定版 CI は正確な固定 HEAD と一致し、試験統合コミット `b0fd17d0f843727a5a85bff2aae7ba42cc5cf49a` の tree も固定 tree と一致している。

保留事項：

- 過去2回のタイムアウトの根本原因は未確定。
- 実機起動、通信、フル試合、初期配置、比較値は未検証。
- Linux packet flow と full-stack acceptance は固定版 CI でもスキップ。

固定版 CI の成功を過去のタイムアウトの原因解明や親課題 #14 の実機受入成功とは扱わない。正式な終了対象は子課題 #175 のみであり、親 #14 は OPEN のままであることを独立担当が読み戻した。

## 4. 既存予約と報告確定

```yaml
review_mode: independent_final_closure
initial_reviewed_head: a176627ff0ecb786a9fa5b4b7c1175b518489546
reviewed_implementation_head: 86bc905b919eb5b24b48f7f3479b3bd19d5158c3
verdict: pass_with_held
closed_required_findings: 2
remaining_required_findings: 0
verification_capability: remote_ci_only
reservation_owner: review-enforcer
reservation_identity: duck-pr168-issue175-ifr-20261127-v1
reservation_state_at_review: metadata_only
pre_reserved_report_path: reports/issue-175-independent-final-review-20261127082131.md
report_attestation_allowed: true
report_attestation_head: null
persistence_mode: report_attestation_commit
```

技術判定は上記 reviewed_implementation_head のみに適用される。本報告は既存予約に基づく単一の事務的な報告確定コミットであり、報告コミット自体をレビュー済み実装とは扱わない。その直接の親を固定 HEAD とし、変更は予約パスの1ファイルだけに限定する。報告確定 SHA はコミット後にブランチ外の引継ぎに記録する。以後のコミットは通常修正確認と同じ独立担当の限定再確認なしには完了状態を無効にする。

新規予約、手動 CI、実機操作、マージは行わない。
