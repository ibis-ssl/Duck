# Issue #18 OS 別ネットワーク動作確認 設計報告

## 目的

Aspire シミュレーション環境を Linux だけでなく Windows / macOS でも開発に使えると判断するため、Docker host network と UDP multicast の実動作を OS ごとに確認する仕様を追加した。

## 変更

- Linux + Docker Engine、Windows + Docker Desktop、macOS + Docker Desktop を個別の受入対象とした。
- .NET のビルド成功だけでは OS 対応済みと判断しない。
- `ASPIRE-NET-001` から `ASPIRE-NET-009` を追加した。
- container → host の SSL-Vision / tracker multicast、container → container の raw vision multicast、同一 group / port の複数受信を実 packet で確認する。
- Crane → `cm4-sim` → Simulator の host-network UDP 経路も確認する。
- `InterfaceAddress` の自動選択と明示 IPv4 address fallback を確認する。
- 同一ホストの固定 port 競合は起動前に検出する。
- Windows / macOS で multicast が成立しない場合、別 OS の成功や暗黙の unicast fallback で代用しない。

## 合格判定

各 OS は、その OS 上で必要な `ASPIRE-NET-*` を実施して成功証跡を残した場合だけ対応済みとする。未実施は「未確認」、失敗は「非対応または要修正」とする。

Simulator の SSL-Vision については、安定起動後5秒以内にホスト receiver の packet count が10件以上増えることを最低条件とした。comparison mode では RuntimeHost / DebugHost / TIGERs / ER-Force の同時受信と、DebugHost の三 tracker source 分離まで確認する。

## 証跡

各 OS で OS / Docker version、Docker Desktop host networking 設定、IPv4 interface、`InterfaceAddress`、Aspire resource 状態、全コンテナと .NET host の stdout / stderr、packet count、source identity、port 競合結果を保存する。

失敗時は既存のテスト診断 artifact に加えて Aspire と各コンテナのログを保存する。

## 実施環境

この文書更新時点では RDC 接続先がすべて offline のため、OS 別の実動作確認そのものは実施していない。今回の作業は受入仕様の追加だけである。

## 変更対象

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`
- `Tracker/Design/Testing/tracker-comparison-debug-design.md`
- `Tracker/Design/tasks-status.md`

## 対象外

- AppHost 実装。
- multicast relay / gateway 実装。
- Duck のコンテナ化。
- merge。
