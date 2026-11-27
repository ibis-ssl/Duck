# Issue #25 / PR #26 独立最終再レビュー報告

## 判定と認証対象

**判定: `pass_with_held`（保留事項付き合格）。前回の必須残指摘2件は解消、新規必須指摘0件、未解消必須指摘0件。**

| 項目 | 固定値 |
|---|---|
| リポジトリ | `ibis-ssl/Duck` |
| 対象 | [Issue #25](https://github.com/ibis-ssl/Duck/issues/25) / [PR #26](https://github.com/ibis-ssl/Duck/pull/26) |
| ブランチ | `fix/diagnostics-sample-runtime-issue25` |
| Base | `main` `849664d5e92b3e5ff805514eca7d221fea9e2c67` |
| reviewed_implementation_head（再確認・凍結HEAD） | `61ddebc98425f79ca898198dc78b29464f234521` |
| 技術修正HEAD | `834479a5595d4e6968e7b11591578360b33b9092` |
| 予約ID | `duck-pr26-issue25-ifr-20261127-v1` |
| 予約パス | `reports/issue-25-independent-final-review-20261127065209.md` |

本報告は独立担当から親担当を経由して受領した確定証拠を転記したものである。再確認は前回と同じ独立担当が行い、同担当は実装・通常修正確認に非関与。報告転記担当は独立レビューを再実施していない。個別の担当者名は提供されていないため記載しない。

初回凍結 `b0b6acca9cf024b4fd612e42f8520a280726bfd9` から通常修正へ戻り、通常報告追記後の `0e3daf0a2c5b9a4a87cb5c7481596a9bacacf283` で残った必須2件を対象に限定再確認した。技術修正 834479a から最終凍結 61ddebc への差分は通常報告のみ。同一予約を維持し、新たな予約・独立レビューを作成しない。

## 確定指摘の解消根拠（静的確認・記録照合）

| 以前の必須指摘 | 静的確認（凍結HEAD） | 回帰確認の記録 | 判定 |
|---|---|---|---|
| 保存された alignment 時刻差が実際の二時刻と一致すること | [Reader 601–607行](https://github.com/ibis-ssl/Duck/blob/61ddebc98425f79ca898198dc78b29464f234521/Tracker/Tracker.DebugHost/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs#L601-L607) にて保存差値と二時刻の絶対差を照合する | 不一致時に外部比較を利用不能とし、正常な sample 主経路を維持する回帰。上限境界の正常 fixture は二時刻と差値が一致する構成に修正 | 解消 |
| sample 本体の読取拒否を明示的な読取失敗にし、旧記録経路へ誤移行しないこと | [Reader 510–520行](https://github.com/ibis-ssl/Duck/blob/61ddebc98425f79ca898198dc78b29464f234521/Tracker/Tracker.DebugHost/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs#L510-L520) にてアクセス拒否を明示的な状態に変換する | 追加回帰で旧 own snapshot への誤移行がないことを確認 | 解消 |

前回必須2件とも解消、新規必須指摘0件、未解消必須指摘0件。今回の限定再確認以外の追加範囲を独立に実行・再確認したとは主張しない。先行の外部データ異常分離と旧記録互換の通常確認は既存記録を根拠とする。

## 検証証拠と確認方法

- **既存記録の照合:** [通常報告](https://github.com/ibis-ssl/Duck/blob/61ddebc98425f79ca898198dc78b29464f234521/reports/issue-25-pr26-normal-review-closure-20261127.md) により対象回帰76件成功、通常修正確認の必須指摘0件を確認。独立担当の環境では元のローカルログを取得できず、報告記録の照合を根拠とした。独立担当・報告転記担当による試験再実行はない。
- **静的確認:** 前節の凍結HEADにおける Reader 差分・対象回帰の確認。独立担当による確定判定に基づく。
- **既存 CI の実行結果の照合:** 以下の run は GitHub Actions の実行結果であり、再試験ではない。報告転記時にも run と job ログを照合した。

| PR HEAD | CI | 確認結果 |
|---|---|---|
| `834479a5595d4e6968e7b11591578360b33b9092` | [#1287 / 45971894765](https://github.com/ibis-ssl/Duck/actions/runs/45971894765) | failure。1649成功・1失敗・4スキップ。既存 `RuntimeIdentityControlNonCooperativeLivenessTests.RelayReturnsWhenFlushIgnoresCancellationToken` が失敗。根本原因は未確定 |
| `61ddebc98425f79ca898198dc78b29464f234521` | [#1289 / 45972400626](https://github.com/ibis-ssl/Duck/actions/runs/45972400626) | success。1650成功・0失敗・4スキップ |

両 run の checkout は当該 PR HEAD と main の PR merge 候補。凍結版 CI は成功したが、技術修正版の先行失敗の原因を特定・修正した証拠ではない。両版間は通常報告のみの差分である。先行失敗を成功へ読み替えない。Aspire 受入2 job は両 run で skipped。

## 保留事項と適用範囲

- Windows 直接試験: 未確認。
- 実際の OS 権限による sample 読取拒否: 未確認。例外注入による回帰試験結果のみを記録する。
- Aspire 実機受入: 未確認。今回の合格証拠に含めない。
- 先行 CI の liveness 試験失敗の根本原因: 未確定。現行 CI 成功と区別する。

以上の保留事項は実機・未解明事項の合格を表すものではない。製品・試験の追加変更、再予約、試験再実行、手動CI、実機操作、マージは行っていない。マージは利用者のみ。

## 報告認証の条件

- Persistence mode: `report_attestation_commit`。
- 技術判定は `reviewed_implementation_head = 61ddebc98425f79ca898198dc78b29464f234521` に適用する。
- 本報告は同一予約を用いた単一の行政的報告認証であり、製品実装の変更・新たなレビュー結果ではない。
- 認証コミットの直接の親は上記 reviewed_implementation_head とし、差分は予約パス1ファイルだけに限定する。
- 記録時点では commit_pending、push_pending。認証コミット自身の SHA は作成後に外部の引継ぎ結果で記録し、本報告内では将来 SHA を検証済み技術 HEAD として主張しない。
- 認証後の HEAD に対する CI は本報告記録時点では未確認であり、凍結版 CI の成功と混同しない。
- 認証コミット以降の追加 Git コミットは、通常修正確認と同一独立担当による限定再確認・CI 差分の確認がない限り、この完了状態を無効にする。
- 同一予約で独立最終報告を認証可能との独立担当の確定判定を受領済み。報告確定後、公開 HEAD と直接の親、予約パス限定の差分確認結果を引き継ぐ。
