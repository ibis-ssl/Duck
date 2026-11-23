# Duck #155 独立最終レビュー

## 判定と対象

**合格（`pass_with_held`）。** 前回残った初回準備の前提説明を確認し、5件の指摘をすべて解消と判定しました。クラウド切り替え後の受入確認は保留です。

- 対象は `ibis-ssl/Duck` の Issue #155、Draft PR #156、公開ブランチ `task/issue155-cloud-setup-boundary` です。
- `reviewed_implementation_head` と `technical_head` は `31fb5ac20300ff6f1b01b1acb3859f350f216f5c`。base は `7c8e860899f08acd4b02a0cdf1b44eba180a9f21`、前回確認対象は `b6140a860455d61636606a848b1ed0b3dede928a` です。
- 共有インストーラーは CodexSkill `124c52054c65058e1f240f4ae28ef6a45514e77f`、`scripts/install-cloud-tools.sh` の blob は `5c1b491567e8a45ea8f119682e5eb94198c74e18` です。公開タグ `1.1.11-pre` は同じコミットを指します。
- レビューは前回と同じ独立レビュー担当が継続しました。実装と修正は別担当です。今回の差分で変更されたファイルは `tools/cloud/README.md` のみです。

## 指摘の確認

1. **高・解消。** `prepare.sh` に `Tracker.Tests` の個別復元があり、`--no-restore` の構築より前に実行します。
2. **高・解消。** 開始用の固定案内は Duck が版管理する README を参照し、子担当の役割を明示します。通常開始では導入や構築を繰り返しません。
3. **高・解消。** SDK の実行ファイルのパスと版を通常シェルで確認し、文書検査は別に扱います。追記はロックファイルに対応する Node.js と npm、共有側で使う `python3`、文書検査の準備に使う `venv` と `pip` を明記しています。記載された CSpell 9.8.0 の Node.js 要件 `>=20.18` は固定 HEAD のロックファイルと一致します。前提が不足した場合の扱いも示されています。
4. **中・解消。** DebugHost は必要時に手動で起動します。HTTP と映像受信の宛先はループバックで、UDP 再配信は無効です。
5. **中・解消。** 変更後の README は textlint、CSpell、SudachiPy の許可一覧検査に通過しました。辞書と検査設定に変更はありません。

## 検証と保留

- 今回は対象 README の3種の文書検査、`git diff --check`、UTF-8 と LF を確認しました。既存ツールを使用し、依存は追加していません。
- 直接呼び出しと異常終了時の停止、準備順序と起動設定の模擬確認、既存の実 SDK の再利用、公式 SHA-512 との一致は前回までの証拠を引き継ぎました。
- 前回 HEAD の `.NET tests` は成功しました。今回の技術 HEAD でも `.NET tests` の `test` は成功し、Linux packet flow と full-stack acceptance はスキップでした。証跡コミットの必須 CI は公開した証跡 HEAD に一致する `pull_request` 実行で確認します。
- 公開後の新規 Cloud 環境の復元、実環境での SDK 選択、初回準備・再実行と文書依存を含む全体確認は切り替え後の受入事項です。既存 SDK の再利用と模擬確認の記録を、それらの受入証拠には数えません。

関連資料

- [前回の独立再レビュー](https://github.com/ibis-ssl/Duck/pull/156#issuecomment-7091568742)
- [今回の README 差分](https://github.com/ibis-ssl/Duck/commit/31fb5ac20300ff6f1b01b1acb3859f350f216f5c)
- [前回 HEAD の CI](https://github.com/ibis-ssl/Duck/actions/runs/45111193870)
- [今回の技術 HEAD の CI](https://github.com/ibis-ssl/Duck/actions/runs/45111998072)
- [CodexSkill 固定版](https://github.com/ssaattww/CodexSkill/releases/tag/1.1.11-pre)

## 報告の保存

- 予約 ID は `duck-155-independent-final-31fb5ac20300`、予約時刻は `2026-11-23T04:29:52Z`、予約者は `review-enforcer` です。最終判定前に `metadata_only` で1件を予約し、対象の Git ツリーと確認した作業ツリーの両方にファイルが存在しないことを確認しました。
- 予約先は `reports/issue-155-independent-final-review-20261123042159.md` です。
- 保存前の状態は `commit_pending`、`administrative_parent` は `31fb5ac20300ff6f1b01b1acb3859f350f216f5c` です。この報告だけを追加する管理上の証跡1コミットとして保存し、実装に対する判定は上記の技術 HEAD に適用します。
- 証跡コミットの SHA は作成と照合の後、この報告の外で記録します。後続の Git コミットがある場合は、改めてレビュー工程が必要です。
