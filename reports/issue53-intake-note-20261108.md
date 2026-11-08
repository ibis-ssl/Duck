# Issue #53 / ASPIRE-006G3 作業範囲・受入条件

## 対象

- Issue: [#53](https://github.com/ibis-ssl/Duck/issues/53)
- 親 Issue: [#50](https://github.com/ibis-ssl/Duck/issues/50)
- 依存: #52（完了）
- 子ブランチ: `task/issue53-aspire-006g3`
- PR の base: `task/issue50-aspire-006g`

## 作業範囲と受入条件

`ASPIRE-MATCH-004` を実パケットで検証し、受信件数と送信元の記録から次を確認する。

- Sumatra が UDP 11010 に送信する。
- AutoRef が UDP 10020、11003、11010 に送信する。
- Duck は UDP 11010 に送信しない。

実行時の OS、Docker バージョン、ネットワーク設定、各経路の受信件数、送信元識別、関連プロセスの標準出力・標準エラーを記録する。失敗時も得られた途中の証跡を保存する。未実施または失敗を合格として扱わず、完了時には独立レビューと検証対象 HEAD の一致を記録する。

## 境界

この文書は Issue #53 の作業範囲と受入条件を整理する準備資料であり、製品実装、設定変更、実通信の実行、受入証跡の作成を含まない。Issue #50 の統合作業や Issue #54 の試合完了・記録の受入条件は変更しない。

## 開始時点

- 親ブランチの基準 HEAD: `9756f9dd35ce05cf18d6ad91eaf277ad917fcfe9`
- Issue #53 は Open、Project status は In progress。
- この準備 PR は本書のみを含む。
