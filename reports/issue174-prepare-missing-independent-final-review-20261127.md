# 課題174 固定入力準備の独立最終レビュー再確認報告

## 判定と対象

**判定：保留事項付き合格（`pass_with_held`）。前回の必須指摘2件を解消し、未解消の必須指摘は0件。実機受入は未確認。**

対象は [課題174](https://github.com/ibis-ssl/Duck/issues/174) と [下書き PR #176](https://github.com/ibis-ssl/Duck/pull/176)。作業ブランチは `task/issue-128-prepare-missing`、統合先は PR #160 の `task/issue-128-windows-match`（`32926a24cf9915654f01b12f1a9d5af6c612ed7c`）。親課題128と課題173の完了判定は対象外。

初回の独立レビュー対象は `8626f953fdf9119c99792ecfad09974a4afe1d64`、指摘対応の実装版は `d3c15e5f3562b9f86840a5bac39e7a19da22f517`。同じ独立担当が前回の必須指摘と CI の差分に限定して、`2dc92a5e208c5bff732b28f7353a30c93a158190` を再確認した。この報告は親から受領した確定証拠の転記であり、公開担当による独立レビューの実施ではない。

## 必須指摘の解消確認

| 指摘 | 重要度 | 判定 | 確認した証拠 |
| --- | --- | --- | --- |
| I174-IFR-01：実際に使用する Dockerfile の完全性 | 中 | 解消 | 製品の期待バイト列、試験用の内容、固定 Dockerfile が完全一致。取得前と Compose 生成時の両方で検査し、変更時には拒否する。[製品コード](https://github.com/ibis-ssl/Duck/blob/2dc92a5e208c5bff732b28f7353a30c93a158190/Testing/Duck.Testing.AppHost/WindowsDevelopmentMatchInputs.cs#L110)、[回帰試験](https://github.com/ibis-ssl/Duck/blob/2dc92a5e208c5bff732b28f7353a30c93a158190/Tracker/Tracker.Tests/FixedInputPreparationTests.cs#L189) |
| I174-IFR-02：Proto 取得失敗時の製品経路の回帰試験 | 中 | 解消 | `PrepareAndValidateProtoSubmodulesAsync` を直接実行し、模擬取得失敗後の未配置、一時領域の回収、既存保存先と作業木の維持を検証する試験を確認。[回帰試験](https://github.com/ibis-ssl/Duck/blob/2dc92a5e208c5bff732b28f7353a30c93a158190/Tracker/Tracker.Tests/LauncherProcessContractTests.cs#L11) |

確認方法は、製品コード・試験・固定内容の静的照合と、既存の実行証拠の確認である。実際の外部入力取得や実機ビルドによる確認ではない。両指摘の重要度は変更していない。

## 基点更新と検証

- 静的確認：指摘対応の実装版 `d3c15e5f3562b9f86840a5bac39e7a19da22f517` から再確認対象への変更は文書、通常報告、台帳のみ。製品コード、試験、ワークフローは変更されていない。
- 基点：PR #160 の文書一般化版 `32926a24cf9915654f01b12f1a9d5af6c612ed7c`。
- 既存の実行記録：[通常修正確認報告](https://github.com/ibis-ssl/Duck/blob/2dc92a5e208c5bff732b28f7353a30c93a158190/reports/issue174-prepare-missing-normal-review-20261127.md) に軽量回帰19件成功、必須2件解消、新規必須0件の記録があり、再確認と整合する。軽量試験は模擬取得と接続型を使用しており、独立担当が新たに実行したことを意味しない。
- 既存の自動検証：[再確認対象の CI 45972332209](https://github.com/ibis-ssl/Duck/actions/runs/45972332209) は成功1703件、失敗0件、除外4件。パケット転送とフル試合は省略された。旧基点での [CI 45969074682](https://github.com/ibis-ssl/Duck/actions/runs/45969074682) の失敗記録は通常報告に保持されている。

## 保留事項

実入力取得、実際の Docker ビルド時の挙動、Windows での試合完走、独立レビュー時点の文書検査の実行証拠は未確認。標準5イメージによる試合完走も証明済みとは扱わない。旧基点での既存タイミング試験の失敗原因は未確定であり、再確認対象の自動検証が成功しても原因解明を意味しない。

## 予約と公開

受領した確定証拠の予約メタデータは次のとおり。

```yaml
reservation_owner: review-enforcer
reservation_identity: Duck-174-176-independent-8626f953-20261127
reservation_state: metadata_only
initial_independent_reviewed_head: 8626f953fdf9119c99792ecfad09974a4afe1d64
reviewed_implementation_head: 2dc92a5e208c5bff732b28f7353a30c93a158190
pre_reserved_report_path: reports/issue174-prepare-missing-independent-final-review-20261127.md
report_attestation_head: null
verdict: pass_with_held
```

既存予約を再利用する。公開前の照合で予約パスの未作成および公開版の一致を確認した。本報告は上記のレビュー済み実装を直接の親とする一つの管理上の報告コミット（`report_attestation_commit`）にのみ格納する。このコミットの変更範囲は予約済み報告1ファイルに限定する。報告コミット自身の識別子はレビュー対象の実装ではなく、作成後に報告の外で記録する。

公開担当は固定版の対象列挙を実行し、文書検査の適用範囲を確認した。既存の検査規則では `reports/**` は対象外であるため、通常の文書語彙検査を成功扱いにしない。報告内容と予約、差分の範囲を確認した。後続コミットによる変更が発生した場合は通常修正確認と同じ独立担当の限定再確認が必要となる。再予約、製品修正、手動 CI、実機操作、マージは行わない。
