# ASPIRE-005 実装状況報告

## 対象

- Issue: #37 / ASPIRE-005
- 基点: PR #59 の `f6024cacc06a84ddf8bc325be924c35439fb338d`
- 実装 branch: `task/pr28-aspire-005`
- この報告はPR #28の設計規約ではなく、現在の実装と検証状況を記録する。

## 実装

`StackOwnershipLease` を追加し、AppHost が resource model を構築する前に既定のホストローカル lock を排他的に取得するようにした。lock handle は AppHost の実行期間中保持する。既存 lock file が残っていても、前所有者が終了していれば再取得できる。2つ目の所有者は、資源の起動処理へ進む前に明示的な `InvalidOperationException` で拒否する。

lock file path は `Testing:StackOwnership:LockPath` で上書き可能とし、既定値は `Path.GetTempPath()/duck-aspire-stack.lock`。

## TDD / 検証

- テスト先行 commit: `3c634ddbaba769890ab545b3552da234a6dc6eb3`。Docker 上の .NET SDK 10.0.401 で focused test を実行し、2つ目の取得が拒否されないため期待どおり失敗した。
- 実装 commit: `a38c1061ff7421804e213b41bb94ae34769f1fa6`。同じ focused test は成功した。テストはlock fileの残存後の再取得も確認する。
- Draft PR head `d2b8dabb1f3dbe6c35801d401184c1c0e8312499` のCI run `37177317149` は失敗した。352件中348件成功、4件失敗。原因は複数のAppHost application-model testが並行実行され、同じ既定lock pathを実stackとして取り合ったこと。
- model testは各AppHostに一意な一時lock pathを渡すよう修正した。修正後のexact-head CI run `37177602372` は成功。ASPIRE-NET-002 jobはworkflow_dispatch限定のためskip。
- SDK 10.0.401 containerでのfocused AppHost model testsは11/11成功。focused ownership testと`git diff --check`も成功した。
- ローカルには .NET SDK がないため、公式 `mcr.microsoft.com/dotnet/sdk:10.0` コンテナを使用した。NuGet接続にはホストの信頼済み証明書束をread-only mountした。

## 未完了の受入項目

この作業環境では、Issue #37 の全受入条件は未検証であり、完了とは扱わない。

- 既定の Crane image `ghcr.io/ibis-ssl/crane:scenario-a544db92b72b137c8974285b36940d0d4b5e7e69` は `manifest unknown` で取得できなかった。
- 代替の `scenario-develop` は manifest を取得できたが、Docker daemon が image layer 展開時に `no space left on device` を返した。ホストの `/workspace` には23GBの空きが報告されるため、これはDocker daemon側の利用可能容量制限とみられる。既存image削除やdaemon設定変更は行っていない。
- そのため、Simulator / Game Controller / Crane / cm4-sim / Duck の一括起動、実SSL-Vision受信と tracker UDP出力、HALT→active referee遷移、active motion、Crane→cm4-sim→Simulatorの位置変化、2つ目のAppHost processの起動前拒否をこのHEADでは実測していない。
- Docker対応環境で上記のLinux `ASPIRE-NET-*` を実施する必要がある。`scenario-develop` による試験を行う場合は、既定の固定tagを検証した結果と混同しない。
