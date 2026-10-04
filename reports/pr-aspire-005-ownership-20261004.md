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

- テスト先行 commit: `682de96`。Docker 上の .NET SDK 10.0.401 で focused test を実行し、2つ目の取得が拒否されないため期待どおり失敗した。
- 実装 commit: `475b38c`。同じ focused test は成功した。テストはlock fileの残存後の再取得も確認する。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 /nr:false`: SDKコンテナ内で exit code 0。
- `git diff --check`: 成功。
- ローカルには .NET SDK がないため、公式 `mcr.microsoft.com/dotnet/sdk:10.0` コンテナを使用した。NuGet接続にはホストの信頼済み証明書束をread-only mountした。

## 未完了の受入項目

この作業環境では、Issue #37 の全受入条件は未検証であり、完了とは扱わない。

- 既定の Crane image `ghcr.io/ibis-ssl/crane:scenario-a544db92b72b137c8974285b36940d0d4b5e7e69` は `manifest unknown` で取得できなかった。
- 代替の `scenario-develop` は manifest を取得できたが、Docker daemon が image layer 展開時に `no space left on device` を返した。ホストの `/workspace` には23GBの空きが報告されるため、これはDocker daemon側の利用可能容量制限とみられる。既存image削除やdaemon設定変更は行っていない。
- そのため、Simulator / Game Controller / Crane / cm4-sim / Duck の一括起動、実SSL-Vision受信と tracker UDP出力、HALT→active referee遷移、active motion、Crane→cm4-sim→Simulatorの位置変化、2つ目のAppHost processの起動前拒否をこのHEADでは実測していない。
- このPRの通常CIでテスト結果を確認し、Docker対応環境で上記のLinux `ASPIRE-NET-*` を実施する必要がある。`scenario-develop` による試験を行う場合は、既定の固定tagを検証した結果と混同しない。

