# Issue #128 調査報告: Windows ホスト RuntimeHost と CM4 中継の代替構成

## 対象と実行状態

- Repository: `ibis-ssl/Duck`
- Issue: [#128](https://github.com/ibis-ssl/Duck/issues/128)
- Branch: `task/issue-128-host-runtimehost-cm4-topology`
- Base: `main` / 調査基準 HEAD `985ce8e7eb9357bd08c2b76ebb33a85fd304f59f`
- 関連する固定 CM4 source: `ibis-ssl/Orion_CM4@d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a`
- 調査担当: ユーザー指定により `gpt-6.1-sol / medium` を要求して子エージェントを起動。起動呼出しは成功し、読み取り専用の調査結果を取得した。親から確認できる最終 runtime profile metadata はないため、実適用モデルは未検証として記録する。
- 子への指示: Duck の CM4 helper を直接通信へ置き換えられるか、Windows ホストで `Tracker.RuntimeHost` を動かし周辺を Docker に置く構成が成立するか、実コード・既存レポート・互換性・ネットワーク・再起動を調査する。製品変更、Issue/branch/PR 操作、Docker 実行はしない。子エージェントはファイル変更や Docker 操作を行っていない。
- 親の作業: Issue #128 と調査ブランチを作成し、`Tracker/Design/tasks-status.md` に調査タスク `ASPIRE-009` を追加した。製品コード、固定イメージ、設計本文は変更していない。
- Docker: 実行・pull・build なし。静的根拠で今回の判断に足りる。固定 image の実測が必要になれば、取得・起動前に正確な参照名と版を示す。

## 結論

**CM4 helper を無くす案は実現可能性があるが、`--out-addr` だけの変更では成立しない。** CM4 の Simulator feedback 受信 bind が `127.0.0.1` 固定であり、両コンテナの動的 IP を起動・再作成時にどう更新するかも必要になる。最初に評価する価値があるのは、CM4 が受信 bind と送信先を設定可能にし、Duck AppHost が相手 IP を安全に解決・更新できるかである。loopback を既定値に残すか、既存 helper を維持するかは未決定とする。

**Windows ホスト RuntimeHost も、Aspire の resource 種別を変えるだけでは動かない。** 現行の UDP 参加者は DCP bridge 内に集約され、現在の packet contract は Vision multicast `224.5.23.2:10020` の受信と TrackerWrapperPacket multicast `224.5.23.2:11010` の送信である。RuntimeHost の socket 実装は source-level では numeric unicast endpoint も扱えるが、Simulator と downstream consumers を含む Windows end-to-end 経路は未確認である。ホストに移すには両方向のネットワーク経路が必要で、unicast 設定・boundary relay・host networking のどれで満たすかを現行受入構成から分けた開発プロファイルとして検討するのが妥当である。

これらは調査上の提案であり、どの構成を実装するかは決定していない。

## Duck / CM4 の現行経路

### helper と固定イメージ

Duck AppHost は `cm4-sim` を固定 tag `d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a` のイメージで登録する。linux image lock は OCI index `sha256:aef193194cce7c1c3729ac344065945b3d92c0a7ccdca0d5036cc472a8bfa61a` と linux/amd64 manifest `sha256:e67b773e8b1391d755ec93d4dce30af074481fe02c1cdca21754aa1273c6223b` を記録する。helper は image 内の実行物ではなく、標準 DCP resource の `cm4-sim` コンテナへ `/opt/duck/cm4-transport-control` として読み取り専用 mount される。起動用 PowerShell script は中間ファイルがない場合、既存 WSL distribution 内の C compiler で作成する。

根拠: [BaseSimulationResourceRegistration.cs](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs)、[AppHost project file](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj)、[start-duck-simulation.ps1](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/scripts/start-duck-simulation.ps1)、[linux-image-lock.json](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Testing/Duck.Testing.AppHost/linux-image-lock.json)。

### helper が担う両方向の転送

現在の C core は次の2経路を持つ。

1. Simulator feedback は、source IPv4 が現行 Simulator のアドレスと一致する場合のみ受理し、CM4 コンテナ内の `127.0.0.1:50100+id` へ転送する。
2. CM4 の mode 4 command は loopback source だけを受け付け、保存済みの現行 Simulator IPv4 の `12346` へ mode 3 として転送する。

AppHost は、container ID・DCP resource ID・engine・run・generation を照合してから target を適用し、CM4 の再起動・交換時に旧 target を invalidate する。C helper の target update には Docker exec の JSONL protocol を使う。現行 `OwnedCm4SimulatorReconfiguration` の CM4 再起動経路は、旧 target の invalidation を確認してから CM4 を再起動し、新しい CM4 世代を待ち、Simulator も再起動して新しい pair を確認した後に helper target を再適用する。必要なら最後に現行所有証明を確認して Crane も再起動する。これはソース上の制御順序であり、今回の Docker 実行による実測ではない。この仕組みは固定の両 peer address を一度与えるだけでなく、所有中の実 container と再作成世代に結びつけて宛先を変える。

根拠: [transport_core.c](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/native/cm4-transport/transport_core.c)、[OwnedCm4TransportTargetController.cs](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Testing/Duck.Testing.AppHost/OwnedCm4TransportTargetController.cs)、[OwnedCm4SimulatorReconfiguration.cs](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Testing/Duck.Testing.AppHost/OwnedCm4SimulatorReconfiguration.cs)。

### Orion_CM4 で直接化する際の差分

固定 image の source `cm4/bridge/cm4_sim.cpp` は、`--out-addr` と `--out-port` を受け付け、`inet_addr` で送信先 IPv4 を構成する。一方、各 robot ID の feedback socket は `127.0.0.1:(feedback_port_base + id)` に bind し、bind address の CLI option は確認できなかった。つまり送信先 address は変更できるが、feedback の受信 bind は現在 loopback 固定である。hostname による service discovery も、この `inet_addr` 経路からは使えない。

根拠: [fixed-image cm4_sim.cpp](https://github.com/ibis-ssl/Orion_CM4/blob/d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a/cm4/bridge/cm4_sim.cpp)、[current main cm4_sim.cpp](https://github.com/ibis-ssl/Orion_CM4/blob/e03b0f69a7ccd18073a01c6df7e3327bfe7dde2e/cm4/bridge/cm4_sim.cpp)。

Duck AppHost は現在、CM4 の起動後に Simulator を待たせ、Simulator の引数へ現行 CM4 address を足す。直接化する場合は少なくとも次が必要になる。

- CM4 に feedback bind address の設定を加える。既定を `127.0.0.1` に保てば、既存の同一ネットワーク利用者との互換性を維持できる候補になる。
- CM4 の `--out-addr` に Simulator の現在 IPv4 を渡す。
- Simulator feedback の送信先を CM4 container の現在 IPv4 にする。AppHost はすでに Simulator 側へ現在 CM4 address を渡している。
- Simulator または CM4 の交換・再作成時に、相手 IP をどう取得し、どちらを再起動・再構成するか定める。固定 image の CM4 CLI は数値 IPv4 を起動引数で受けるため、直接化では helper が現在担う CM4→Simulator の宛先更新と同等のライフサイクル手段が必要となる。
- 現行 Crane overlay は起動時に `cm4-sim` を解決して launch XML に一度書き込む。CM4 container address が変わる場合は Crane 側 target の再解決も必要で、helperless mode での協調 restart / runtime refresh は未設計。

**推論:** 現行の start dependency は「CM4 起動後に Simulator の feedback address を設定」するための順序である。直接化では逆向きに CM4 が Simulator IP を必要とし得るため、両方の IP を揃える staged startup、再起動、固定 IP、または Orion 側の DNS/runtime reconfiguration が必要となる可能性がある。これは静的コードからの lifecycle 上の懸念で、まだ実測されていない。

Orion_CM4 の `cm4_sim` 内にある feedback multicast 再配信 (`224.5.20.(100+id)`) は、この C helper の unicast loopback relay とは別の経路であり、helper を除いても別途扱いが必要である。

### Crane の command target と feedback multicast

Crane の宛先は C helper の loopback relay とは別契約である。現行 `CraneTargetAddressSettings` は標準 DCP base と CM4 packet-adapter / visibility-graph 構成で `cm4-sim` を選び、`DUCK_CRANE_TARGET_ADDRESS` と期待する resource 名を設定する。`CraneLaunchOverlay` は Crane 起動時にその resource 名を DNS 解決し、3 秒以内に唯一の安全な unicast IPv4 が得られた場合にだけ launch XML の target address/port を差し替える。通常の CM4 target port は `12345` で、固定 CM4 source も command input を port `12345` に bind する。overlay は Crane 子プロセスの起動前に一度だけ解決する実装で、実行中の宛先 refresh loop は見当たらない。

固定 #81 G1 report は旧版 Crane の loopback target と CM4 / Simulator の固定 address 契約を記録した静的調査である。現行 main には上記の Crane launch overlay が追加されており、その歴史的記録を現行実装や実測証拠として扱わない。現行 overlay の起動時 DNS 解決も、稼働中 Crane が CM4 container 再作成後に追従する証拠ではない。

CM4 simulator の `224.5.20.(100+id)` feedback multicast は Crane の robot feedback 受信経路であり、C helper の unicast loopback relay とは独立している。CM4 helper の撤去だけを理由にこの multicast 経路を削ることはできない。

したがって直接接続案は CM4 ↔ Simulator の2方向だけでなく、Crane → CM4 command destination を含めて扱う必要がある。CM4 の address が変わる再作成では、現在の launch overlay による Crane 再起動・再解決、安定 endpoint、または実行中再構成のどれを採るか決める必要がある。現行再起動 coordinator は helper 経路で optional Crane restart を持つが、helper なしの endpoint 更新については設計・実測がない。

根拠: [CraneTargetAddressSettings.cs](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Testing/Duck.Testing.AppHost/CraneTargetAddressSettings.cs)、[CraneLaunchOverlay Program.cs](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Testing/Duck.Testing.CraneLaunchOverlay/Program.cs)、[fixed-image cm4_sim.cpp](https://github.com/ibis-ssl/Orion_CM4/blob/d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a/cm4/bridge/cm4_sim.cpp)、[historical #81 G1 static report](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/reports/issue81-bridge-contract-investigation-20261022.md)。

## Windows ホスト RuntimeHost と Docker 周辺資源

### 現行構成と変更差

Windows 上にあるのは AppHost/Aspire で、`Tracker.RuntimeHost` は publish 済み成果物から作る `duck-runtime` image 内の .NET process として起動する。RuntimeHost の変更を反映するには publish 後に AppHost を起動し直し、現行構成には `dotnet watch` hot reload がない。ホストの `ProjectResource` へ移せば IDE 上で直接起動・debug しやすくなる可能性はあるが、その resource 登録や実際の hot reload/debug 経路は現在実装・確認されていない。

根拠: [Testing/README.md](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Testing/README.md)、[Program.cs](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Testing/Duck.Testing.AppHost/Program.cs)、[RuntimeHost appsettings.json](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Tracker/Tracker.RuntimeHost/appsettings.json)。

### ネットワーク境界と選択肢

Aspire の container resource は AppHost ごとの bridge network に接続し、service name で相互に通信する。host project / executable はその container network の参加者ではなく、host endpoint は公開された container port 経由で通信する。DCP container tunnel は `WithReference` を使う container-to-host **TCP** endpoint を提供する。これは UDP multicast の中継ではない。

出典: [Aspire inner-loop networking overview](https://aspire.dev/fundamentals/networking-overview/)。

RuntimeHost を host process に移すと、現在の multicast contract を保つなら両方向にネットワーク境界が生じる。

- Simulator bridge 内の SSL-Vision `224.5.23.2:10020` を Windows RuntimeHost に届ける。
- Windows RuntimeHost の tracker multicast `224.5.23.2:11010` を Docker 内の Crane / DebugHost 等へ届ける。

ただし境界を越える方式は multicast bridge に限られない。現行 `RuntimeVisionReceiverService` は `IPAddress.Any` の UDP port に bind し、設定値が multicast の場合だけ group join を行う。現行 `UdpTrackerPacketPublisher` も設定された numeric IP:port へ UDP 送信し、実装上は unicast endpoint を使える。したがって producer を Windows host の unicast endpoint へ向け、RuntimeHost 側で受信する案と、RuntimeHost の tracker output を個別 consumer へ unicast する案は source-level の候補になる。

これは現在の end-to-end 対応や Docker Desktop の実測を意味しない。Simulator が host 向け unicast 送信先を設定できるか、Crane / DebugHost などの全 consumer が unicast を受けるか、既存 multicast fan-out をどう維持するかは未確認である。RuntimeHost publisher は一つの `IPEndPoint` だけを保持するため、複数 consumer への unicast fan-out は別の送信処理または relay を要する。Docker Desktop の標準 bridge は仮想 Linux 環境内にあり、Windows host がその bridge を通常の host NIC と同じように参加・受信する構成ではない。port publish は container service を host port に公開する方法であり、multicast group の双方向 bridge とは同義でない。したがって単に RuntimeHost の Aspire resource type を `ProjectResource` に変えてもこの経路は解決しない。

根拠: [RuntimeVisionReceiverService.cs](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Tracker/Tracker.RuntimeHost/RuntimeVisionReceiverService.cs)、[UdpTrackerPacketPublisher.cs](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Tracker/Tracker.Core/Runtime/UdpTrackerPacketPublisher.cs).

候補を比較すると次のとおり。

| 案 | 静的根拠・実現性 | 主な残件 |
| --- | --- | --- |
| 現行 DCP bridge 内に RuntimeHost を置く | 現在の Windows 受入が使った方式。UDP participants が同じ bridge 内にいる。 | 今回の「ホストで直接 debug」の希望は満たさない。 |
| Host RuntimeHost と意図的な unicast endpoint を使う | RuntimeHost の receiver は unicast datagram を受けられる socket を作り、publisher は設定 IP:port へ送信する。multicast tunnel の代替となる技術的候補。 | Simulator の host 宛先設定、全 consumer の unicast 対応、既存 group fan-out、Windows からの到達経路と readiness を実測する必要がある。現在の AppHost / Simulator end-to-end 契約ではない。 |
| CM4 endpoint を設定可能にし C helper を外す | CM4 の outbound IPv4 option は既存。Simulator は current CM4 feedback address を受け取っている。中継撤去の第一候補。 | CM4 feedback bind option、両 peer の IP lifecycle、container recreation、loopback compatibility、packet path を実装・実測する必要がある。 |
| CM4 と Simulator の network namespace を共有する | loopback address を維持したまま同じ namespace の利用を試す案。 | 過去の I81-07D は依存側の起動失敗を記録したが、Docker の実エラーは保存されず原因は未確定。DCP の標準 network と共有指定の組合せ、所有・再作成・全 packet path は未検証。既存失敗を成功扱いせず再評価が必要。 |
| RuntimeHost と bridge の間に UDP boundary relay を置く | multicast を境界内で受信し、公開 port / unicast endpoint を使って host 側へ届ける構成は設計候補になる。host の出力も bridge 側で multicast に戻す必要がある。 | 双方向 adapter、port ownership、readiness、source identity、packet freshness、cleanup/restart の設計と Windows 実測が必要。relay が新たに一つ増える。 |
| publisher が multicast と host 向け unicast を二重送信 | bridge に multicast を残したまま host RuntimeHost にも packet を送る案。 | 現行 Simulator が同時に両方へ送る設定を持つか確認できていない。Tracker 出力についても bridge への逆方向経路が別途要る。 |
| Docker Desktop host networking を使う別 profile | Docker docs は Desktop 4.34+、feature opt-in 後に TCP と UDP の layer-4 通信をサポートすると記載する。 | multicast group の送受信成立を資料だけから断定できない。Windows の設定切替と Docker Desktop 再起動が必要。現行 Aspire AddContainer の DCP bridge と host network を併用した際の既存記録は network mode conflict `cannot attach both user-defined and non-user-defined network-modes`。標準管理経路では未確認。 |
| C helper を .NET relay に置き換える | C compiler/WSL に依存する現状を変える選択肢。新規コードを作るなら .NET を優先できる。 | relay 自体は残る。CM4 container 内で動かす場合は .NET runtime または互換性のある self-contained 配布物が必要。UDP socket、owner/generation safety、再起動・cleanup も実装・検証を要する。 |

network namespace 共有案の根拠は [aspire-simulation-test-environment.md の I81-07D/E/G](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/Tracker/Design/Testing/aspire-simulation-test-environment.md) にある。文書は I81-07D の依存起動失敗を保持し、実 Docker 作成エラーは取得しておらず、固定 SDK の既定 network と追加共有指定の組合せを静的な原因候補としている。これを確定原因や現在の実測成功/失敗に読み替えない。

Docker 出典: [Docker Desktop networking how-tos](https://docs.docker.com/desktop/features/networking/networking-how-tos/)、[Docker host network driver](https://docs.docker.com/engine/network/drivers/host/)。

**確認できた Docker Desktop の仕様:** host network feature は version 4.34 以降、Desktop 設定で有効化して再起動すると host と container 間の TCP/UDP 通信を layer 4 で支援する。これは multicast 参加・Aspire/DCP との統合成功を示す根拠ではない。現在の開発機の Docker Desktop version と feature setting はこの調査では確認・変更していない。

## 既存の受入記録と重複確認

- [Issue #56](https://github.com/ibis-ssl/Duck/issues/56) / [PR #124](https://github.com/ibis-ssl/Duck/pull/124) は Windows の現行 DCP bridge 構成を扱う。[Windows acceptance report](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/reports/issue56-windows-docker-desktop-acceptance-20261113.md) は最新 full-stack Fact 1/1 成功を記録する一方、課題全体の判定を PARTIAL とし、元実行の source SHA は unknown と記録する。NET-008 では同一 bridge 内の Duck 受信確認もある。これらは同一 bridge の限定証拠で、host RuntimeHost / host UDP 経路の証拠ではない。
- [Issue #55](https://github.com/ibis-ssl/Duck/issues/55) / [PR #123](https://github.com/ibis-ssl/Duck/pull/123) と [CM4 acceptance alignment report](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/reports/issue55-match001-cm4-acceptance-alignment-20261112.md) は、Linux 上で 715-byte mode 4 → 715-byte mode 3 の CM4 経路を記録する。既存 run `43133171207` は `fullMatchPassed=false` であり、全試合受入の成功や helper 不要を示さない。
- [Issue #80](https://github.com/ibis-ssl/Duck/issues/80)、[#81](https://github.com/ibis-ssl/Duck/issues/81) と [PR #84](https://github.com/ibis-ssl/Duck/pull/84) は Docker Desktop / DCP bridge 移行の過去・現行範囲。これらの正式な完了 Issue を再利用しない。[#81 の G1 report](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/reports/issue81-bridge-contract-investigation-20261022.md) は固定版の静的契約整理として参照し、現行 `CraneLaunchOverlay` の追加後コードや Docker 実測とは区別した。
- [PR #28 task breakdown report](https://github.com/ibis-ssl/Duck/blob/985ce8e7eb9357bd08c2b76ebb33a85fd304f59f/reports/pr28-aspire-task-breakdown-20260929.md) は OS ごとの Docker 実 packet acceptance を分け、`ASPIRE-008` を最終レビュー・進捗同期・PR準備として割り当てている。新しい調査用に Issue #128 と `ASPIRE-009` を追加した。
- Duck の Issue 検索では CM4 loopback helper 撤去または host RuntimeHost topology を扱う専用 open Issue は見つからず、Orion_CM4 の open issue search でも bind-address / `--out-addr` に相当する作業は見つからなかった（2026-11-14）。Orion_CM4 側の変更が選ばれた場合は、実装判断後に同 repo の別課題と PR を作る必要がある。

## 推奨する次の判断

1. **CM4 直接接続を候補として設計評価する。** Orion_CM4 の feedback receive bind を loopback-default の設定にするだけでなく、Duck AppHost が simulator / CM4 の両 peer をどう解決し、再作成時にどう再起動・更新するかを同じ設計で決める。既存 helper はそれまで維持する。
2. **Host RuntimeHost は別 profile の価値を判断する。** 直接 IDE debug が必要なら、RuntimeHost の既存 unicast socket capability を使う host 向け unicast と、multicast を維持する bridge adapter / host-network option を比較する。送信元・下流 consumer、fan-out、source identity と readiness を含む双方向経路を明記し、Windows で実 packet acceptance ができる構成か確認する。
3. どちらの実装を行うかは、この調査 Issue / Draft PR の report review 後に決定する。Duck または Orion_CM4 の製品コード・Docker image をこの作業で変更しない。

## 未実施・未確認

- Docker Desktop での pull、container start、AppHost/DCP 実行、Windows UDP/multicast packet capture。
- 固定 Orion_CM4 image の起動や、helper なしの mode 4→mode 3 / Simulator feedback の実測。
- CM4/Simulator の IP 再作成時の直接 address update、両側の start order、loopback 互換性。
- Docker Desktop host networking feature の現在の版・設定、multicast group membership、Aspire DCP との組合せ。
- Host RuntimeHost の IDE debug/hot reload、ホストと bridge の双方向 UDP 経路、source identity と health/readiness。
- Issue #56 の既存 Windows run に使った source SHA。
- Product build / tests、Docker pull/build/run は未実施（製品コード・image 変更なし）。
- `Tracker/Design/tasks-status.md` の textlint は成功。追加した `ASPIRE-009` 行だけの CSpell は警告0件。ファイル全体の CSpell は PR branch と基準 `main` の双方で同じ15件を報告したため、差分行に語彙警告はない。
- Sudachi whitelist 検査は環境に `SudachiPy` がなく未実行。依存追加はしていない。`reports/**` は Markdown target list の除外対象。`git diff --check origin/main...HEAD` は成功。
