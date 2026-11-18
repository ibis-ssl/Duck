# Duck #128: Detection と UDP 境界中継の続報（2026-11-18）

## 作業範囲

Windows native RuntimeHost を維持し、Simulator の Linux multicast を Crane と Game Controller に残したまま RuntimeHost へ Vision を複製し、Tracker の Windows unicast を Linux multicast に戻す構成を実装した。Firewall、Docker Desktop、Windows ネットワーク設定は変更していない。

作業開始時の `RuntimeVisionReceiverForwardingTests.cs` の既存差分と、未追跡だった前回報告・証跡を保持した。現在のソース変更はこの報告に列挙した AppHost / テストファイルである。

## 実装

- Windows RuntimeHost profile で Simulator の `--localhost` を付けず、Linux multicast を維持する。
- 既存 Crane pinned image にある Perl `Socket` API を使う relay を追加した。host network namespace の relay が `224.5.23.2:10020` を受信し、Windows RuntimeHost の `host.docker.internal:10020` と Linux Game Controller 用 `224.5.23.2:10021` に複製する。
- Tracker の送信先を Windows loopback の動的公開 UDP ポートにする。bridge ingress が受けた Tracker datagram を bridge の default gateway 経由で host namespace に届け、relay が `224.5.23.2:11010` に multicast する。
- Compose の混在ネットワーク対応を追加し、ホスト共有 network namespace の service と bridge ingress service を同一 stack に置けるようにした。公開ポートの割当は ingress service を対象に解決する。

主な変更:

- `Testing/Duck.Testing.AppHost/UdpBoundaryRelayScripts.cs`
- `Testing/Duck.Testing.AppHost/ComposeSimulationStack.cs`
- `Testing/Duck.Testing.AppHost/Program.cs`
- `Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs`
- `Tracker/Tracker.Tests/ComposeSimulationStackTests.cs`
- `Tracker/Tracker.Tests/AppHostApplicationModelTests.cs`

## 既存の実機証跡

このターンで新たな Docker 実行はできなかったため、以下は 2026-11-18 に記録済みの実機試行の結果であり、新しい実装の動作確認を意味しない。

- `reports/evidence/issue128-detection-path-20261118/reverse2-vision.json`: 834/834 が Detection。各 datagram に Yellow 11、Blue 11、Ball 1 を含む。
- `reverse2-runtimehost.log`: RuntimeHost の Vision decoded packet count が 0 から 674 へ増加。
- `reverse2-bridge-relay.log` / `reverse2-host-relay.log`: bridge ingress が Tracker 383 datagrams を転送し、Linux host relay が 353 datagrams を multicast。
- `reverse2-gamecontroller.log`: 黄色 team `ibis` の変更を処理し、`Switched tracker source to ibis (ibis)` を記録。その後 publisher 停止後に `Tracker source ibis timed out` を記録。
- `final-tracker-windows.json`: Tracker protobuf サイズの UDP datagram 434 件を観測。
- Crane ログは Tracker multicast の参加を示す一方、Tracker timeout も記録している。`world_model` の初回受信ログだけでは、有効な Tracker packet を使った Crane 処理の継続を確定できない。

従って既存の検証では Detection、RuntimeHost decode、Tracker の両境界 relay、GC の Tracker source 選択までは確認できる。Crane が Tracker を安定して受信・処理したこと、および今回実装した Compose/relay の統合は未確認である。

## 検証と制約

- build: `dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore --verbosity minimal` succeeded with 0 errors.
- 追加実行: `FullyQualifiedName~RuntimeVisionReceiverForwardingTests` は 1 passed / 0 failed。
- 最終再実行: dynamic bridge ingress の Compose test と Windows RuntimeHost model test は 4 passed / 0 failed。host relay は Docker gateway への UDP route probe から multicast interface を選び、IP を固定していない。
- 追加実行: `FullyQualifiedName~RuntimeVisionReceiverForwardingTests` は 1 passed / 0 failed。
- host relay の multicast interface は `gateway.docker.internal` への UDP route probe で得た local address から選ぶ。固定 IP は埋め込んでいない。

- 実行: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~ComposeSimulationStackTests|FullyQualifiedName~WindowsHostRuntimeProfileUsesRequiredUdpEndpointsAndDoesNotRegisterDuckImage"`
- 結果: 10 passed / 0 failed。対象は Compose の混在 network mode・dynamic port と Windows RuntimeHost の AppHost model。
- `git diff --check`: 成功。
- AppHost model 全体を含む広い実行は 126 件中 123 成功、3 失敗。少なくとも確認できた失敗は `docker` が PATH 上に無く、AppHost の Docker context 解決が起動できないため。後続の絞り込み検証は上記 10 件すべて成功。
- 実機を再確認しようとしたところ、`Test-Path` が `C:\Users\donabe\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe` へのアクセスを拒否した。Docker CLI を呼び出せず、コンテナ状態も再確認していない。過去記録では一時コンテナは停止・削除済みだが、このターンから独立に再確認できていない。アクセス拒否を迂回していない。
- NuGet 脆弱性データ照会はソケット access denied となり NU1900 警告が出たが、`--no-restore` の対象テストは通過した。

## 残件

1. Docker CLI が利用可能な環境で pinned image を再利用し、新しい relay / mixed Compose を実起動する。
2. Detection から RuntimeHost の受信・decode、Crane の Tracker 受信・world model、GC の Tracker source 選択、Windows から bridge ingress を通る Tracker 戻りを同一試行で確認する。
3. Crane の Tracker timeout が続く場合、送信・Linux host relay・multicast membership・Crane socket の各観測点を同じ時間窓で採取する。

新規 Docker 起動、Firewall 変更、外部公開、Git commit / push は行っていない。
