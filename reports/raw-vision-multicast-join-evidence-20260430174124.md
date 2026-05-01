# Raw Vision Multicast Join Evidence

## 対象

- Task: RAW-VISION-006
- Scope: `Tracker.Server` の multicast receiver 初期化を Linux/ローカル simulator 環境でも起動しやすい形に補強する。

## 発端

- `VisionReceiver:Port` を simulator 用の `10020` に変更して `dotnet run` した際、`VisionReceiverService.CreateUdpClient` の `JoinMulticastGroup` で `SocketException (Unknown socket error)` が発生した。
- 失敗箇所はポート依存ではなく、OS 既定 interface に依存した multicast join 初期化にあった。

## 変更概要

- `VisionReceiverService` が `JoinMulticastGroup(group)` の単発呼び出しに依存せず、ローカルの viable な IPv4 interface 候補を列挙して multicast join を試すようにした。
- `VisionReceiver:InterfaceAddress` が設定されている場合は、その IPv4 address のみを使うようにし、無効値は明示的に `InvalidOperationException` として扱うようにした。
- 一部 interface で join が失敗しても、少なくとも 1 つ成功していれば receiver 起動を継続し、失敗した interface は warning log に残すようにした。
- interface 候補選択ロジックの回帰テストを追加した。

## 検証

- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj`
  - Result: passed
  - Tests: 9 passed
- `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj`
  - Result: passed
  - Warnings: 0
  - Errors: 0

## レビュー結果

- Reviewer: parent-side review
- Finding: no actionable findings

## 未解決リスク

- この sandbox では実際の multicast socket join / receive を継続実行して確認していないため、手元環境で `dotnet run` 再確認は必要。
- 複数 NIC がある環境では、join 自体は成功しても送信元 simulator が使う NIC と一致するかは実機ネットワーク構成に依存する。
