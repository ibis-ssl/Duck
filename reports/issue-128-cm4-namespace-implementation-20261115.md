# Issue #128 I128-NS-01 実装可否報告

## 2026-11-15 実装更新

- これは当初の調査のみの記述を更新する実装状況です。I128-NS-01 の Compose 経路は host RuntimeHost profile に登録済みで、I128-UDP-02 と I128-FINAL-03 は未完了です。
- `ComposeSimulationStack` は `cm4-sim` を bridge namespace owner とし、Simulator、Game Controller、Crane、既存 UDP boundary relay を `network_mode: service:cm4-sim` follower として Compose project に登録します。Duck AppHost が同じ Docker context・一意 project name を使って `compose up/down`、ログ、service 状態監視、起動失敗時 cleanup を所有します。
- CM4 は固定 image/tag と既存 command を維持し、host profile では C helper を mount・起動しません。Simulator の feedback destination は loopback `127.0.0.1` です。Crane は既存 image と launch command を維持し、launch overlay mount / entrypoint を使わず、既存 ROS coordinator/team readiness probe を `docker compose exec` で呼びます。
- Game Controller の control port は owner に動的 publish し、既存 `/api/control` WebSocket readiness probe の URI を Compose allocation から生成します。Crane の readiness は同じ既存診断スクリプトを使います。
- helper-free 完了ではありません。Windows RuntimeHost と Docker 間の既存 UDP boundary relay は暫定で Compose follower として残り、読み取り専用 mount されています。最終目標から relay を外す根拠・実 packet 成功はまだありません。
- `start-duck-simulation.cs` は host profile で C helper を準備せず、AppHost build に `-p:DuckHostRuntimeProfile=true` を渡します。この property では C helper output copy と Crane overlay publish target を skip します。Legacy profile の準備・publish は維持します。Compose host profile の `--persistent` は未対応のため、起動前に明示拒否します。Legacy profile の Persistent / StopPersistent は変更していません。
- 画像、タグ、digest、Dockerfile、Duck source mount は変更していません。Base profile の model test は現行 image tag、CM4/Crane mount 不在、Simulator loopback feedback、Game Controller endpoint、Compose follower group を固定します。
- 検証: AppHost build は通常 build と `-p:DuckHostRuntimeProfile=true` の両方で成功。host property buildの直前にAppHost outputのhelper/overlay生成物を削除し、再build後どちらも生成されないことを確認。focused .NET testsは6/6 passed。Linux packet-flow / Docker / Windows RuntimeHost双方向UDPは未確認で、FA780はこの実行環境からoffline。file-based launcherの実行確認も実行環境のread-only temp/homeで失敗し、Windowsでのcompile/runを証明していません。
- Issue #128 は open のままです。base branch `design/issue-128-cm4-shared-namespace`、head branch `task/issue-128-cm4-helper-removal`。I128-UDP-02 / I128-FINAL-03 は未完了。

## 実行範囲

- 作業担当: `/root/issue128_ns_implementation`
- 対象: Issue #128 の I128-NS-01（Windows-host RuntimeHost profile における CM4 / Simulator / Crane の namespace 経路）
- 作業ブランチ: `task/issue-128-cm4-helper-removal`
- 指定 base / 開始 HEAD: `8193cb6500228c7dbb8e604994f13dc5e3d0ad8c`（設計 PR #132 の `design/issue-128-cm4-shared-namespace`）
- 開始時の状態: clean。branch と HEAD は依頼の指定値に一致。
- モデル: 起動要求は `gpt-6-luna` / `high`（親の `spawn_agent` 指定）。実際に適用されたモデル情報はこの実行環境から観測できないため `null`。
- 認可境界: 既存 image・タグ・digest、Dockerfile、container 内のソースを変更しない。Docker / Windows 実行、既存 I81-07D の再実行、UDP boundary relay の変更、PR / Issue 操作、merge は対象外。

## 判定

当初の調査時点では、固定 Aspire.Hosting 13.5.4 の公開 `ContainerResource` モデルに1つのcontainerをowner、別の標準containerをfollowerとするrouteが見つからず、製品コードを変更しなかった。後続の明示指示により、そのDCP routeとは別のCompose経路をAppHost内で実装した。これはDockerでnamespace共有が不可能という結論ではなく、標準DCP routeが確認できないことと、Compose経路の実機成立は別問題である。

raw `--network container:<owner>` を `WithContainerRuntimeArgs` で追加する案は、公開モデルで共有モードを表すものではない。固定版 DCP は各標準 container に `DefaultAspireContainerNetwork` を設定し、別途収集した runtime args を `Spec.RunArgs` に設定する。既存ネットワーク指定を共有モードへ置き換える標準モデル上の経路は、参照した実装では確認できない。この構成を実装済みや runtime 成功と扱わない。

## 根拠

1. Aspire 13.5.4 の [`ContainerCreator.PrepareContainerNetworks`](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/Dcp/ContainerCreator.cs#L2343-L2478) は、コンテナ資源の `Spec.Networks` に `DefaultAspireContainerNetwork` を設定する。また、独自 container network annotation は `Custom container networks are not supported yet.` として拒否する。
2. 同じ版の [`BuildRunArgsAsync` と呼出側](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/Dcp/ContainerCreator.cs#L2641-L2667) は runtime argument annotation を `Spec.RunArgs` に設定する。ネットワーク生成コードが追加の runtime args により `Spec.Networks` を置き換える処理は、ここで参照した経路にない。
3. [`WithContainerRuntimeArgs`](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/ContainerResourceBuilderExtensions.cs#L3008-L3059) の API 説明は、引数を underlying container runtime の run command に「追加」する用途としている。共有 namespace を表す高水準モデル API とは説明されていない。
4. 既存 [`issue81-referee-driver-scope-20261022.md`](issue81-referee-driver-scope-20261022.md) は、13.5.4 DCP の default network と追加 `--network container:...` 指定が同じ生成物に残る静的懸念を記録している。DCP binary の引数解釈順や実 Docker 作成エラーは未確認で、モデル上の矛盾と実際の失敗原因を区別している。
5. [I81-07D 記録](../Tracker/Design/Testing/aspire-simulation-test-environment.md)では、標準 `ContainerResource` を使った既存の限定組成で follower の開始に失敗したが、Docker create error は保存されていない。3回目に owner 1世代と終了時の所有資源残存0が確認された。I81-07D は再実行しておらず、原因は未確定のまま保持する。旧 `--network host` conflict は別構成であり、本件の診断に用いていない。
6. 設計 PR #132 の [`issue-128-cm4-namespace-design-20261115.md`](issue-128-cm4-namespace-design-20261115.md) が示す CM4 / Simulator / Crane の loopback endpoint は候補設計の根拠であって、namespace 共有・UDP 通信・再生成挙動の実測ではない。

## 候補と実装の区別

- 実装: host RuntimeHost profile のCM4、Simulator、Game Controller、Crane、既存UDP boundary relayをCompose resourceへ登録。CM4がowner、残りが同じnamespaceのfollower。Program/Base registrationとAppHost lifecycle managerを接続した。
- CM4 C helper: host profileではmount・起動・asset準備を要求しない。Simulator feedbackは`127.0.0.1`。Crane launch overlayのpublish target / mount / entrypointもhost profileでは使わず、既存Crane commandを使う。Legacy profileのhelper・overlay準備は残す。
- Game Controller: control port `8082/TCP` はowner上でCompose動的割当し、Compose port結果を既存EndpointReferenceへ投影する。既存の`/api/control` WebSocket readinessを利用。
- Crane:既存ROS coordinator/team readiness probeをCompose execで利用。固定image/tagとcommandはmodel testで確認。
- 暫定要素: `Duck.Testing.UdpBoundaryRelay`は既存CM4 image上のfollowerとして残し、その既存Linux binaryをRO mountする。Windows RuntimeHostとのUDP境界relay撤去は未達。
- 固定 image/tag/digest、Dockerfile、Duck source mountは変更していない。Compose ymlにsource mountはない。
- `--persistent` host RuntimeHost profileは誤った永続資源を残さないよう明示拒否。既存Legacy profileのPersistent/StopPersistentは変更していない。

## 検証

- 実施: local-cache restore、AppHost build、host property付きAppHost build、focused model tests。Gitlink SHA、image tag、source mountsを確認した。
- AppHost build: `dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore` — exit 0。ASPIRE010と既存Program型のwarningあり。
- Host profile build: AppHost outputのhelper/overlay生成物を先に削除し、`dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore -p:DuckHostRuntimeProfile=true` — exit 0。その後`cm4-transport-control`と`crane-launch-overlay`が生成されないことを確認。
- Focused tests: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter 'FullyQualifiedName~WindowsHostRuntimeProfileUsesRequiredUdpEndpointsAndDoesNotRegisterDuckImage|FullyQualifiedName~ComposeSimulationStackTests|FullyQualifiedName~ComposeAllocatedGameControllerPortFlowsThroughExistingEndpointReference|FullyQualifiedName~StandardBaseRegistersAnOwnedCm4TransportServerSessionLifecycle'` — exit 0、5 passed / 0 failed / 0 skipped。Compose fake runner start/cleanup/dynamic port/Crane ROS probe、profile resource model、legacy CM4 sessionを実行。
- Launcher実行: `dotnet run --file scripts/start-duck-simulation.cs --no-restore -- --help` はこのexecutorのread-only temp/homeで失敗。Windows上でfile-based appのcompile/runは未確認。sourceではhost modeが`-p:DuckHostRuntimeProfile=true`を渡し、helper準備と要求ファイル確認をskipする。
- 未実施: 実Docker / Compose / DCP、Windows / FA780、UDP packet test。FA780はoffline。GC WebSocket probeとCrane ROS probeは既存code pathをCompose向けにモデル化したが、実機probe成功ではない。
- SDK: `/workspace/.duck-env/env.sh` 読込後 SDK `10.0.401`。依存version/gitlink変更なし。
- `git diff --check`: exit 0。
- Markdown focused textlint: `npm run lint:md:text -- --files Tracker/Design/tasks-status.md Tracker/Design/phases-status.md` — exit 0。
- Markdown focused cspell（wrapper findings 追記後）: `npm run lint:md:spell -- --files Tracker/Design/tasks-status.md Tracker/Design/phases-status.md` — exit 1、2ファイルで196件。Markdown whitelist: `npm run lint:md:whitelist -- --files Tracker/Design/tasks-status.md Tracker/Design/phases-status.md` — exit 1。語彙許可設定は変更していない。
- 必須全体検査 `npm run lint:md`（wrapper findings 追記後）— exit 1。textlint段階は通過し、cspell段階で28文書中10文書に743件の未登録語が報告されたため、後段のwhitelist全体検査へ進まなかった。未登録語の許可一覧を無断で編集していない。
- `node_modules` は検査時に `/workspace/Duck/node_modules` への一時symlinkとして設置し、検査終了後に削除した。

## 既存 Docker CLI wrapper 経路の追加判定

標準 DCP 以外の既存経路として、`DockerWrapperResourceExtensions`、`DockerContainerWrapper`、`DockerContainerSpec` と既存 wrapper tests を確認した。この wrapper は通信データを中継する container 内 helper ではなく、Aspire の `ExecutableResource` としてホスト側で `Duck.Testing.AppHost` を起動し、shell を介さない Docker CLI 引数で**単一の固定 image container**を作成・監視・停止する既存 lifecycle process である。この経路自体は固定 image と container 内 Duck source 非配置の優先条件に合う。

ただし、現状を使って安全に shared namespace を構成する API / lifecycle はないため、この代替経路でも製品コードを変更しなかった。

1. [`DockerContainerSpec.CreateRunArguments`](../Testing/Duck.Testing.AppHost/DockerContainerSpec.cs) は `--network host` を固定しており、`--network` は container arguments で上書きできない reserved option である。
2. [`DockerContainerSpec.IsExactOwner`](../Testing/Duck.Testing.AppHost/DockerContainerSpec.cs) は inspect 結果の `HostConfig.NetworkMode == "host"` を ownership 契約に含める。従って follower の `container:<owner>` network mode は現行 wrapper の exact-owner 検査で拒否される。
3. [`DockerWrapperResourceExtensions.AddDockerWrapper`](../Testing/Duck.Testing.AppHost/DockerWrapperResourceExtensions.cs) は resource ごとに `ExecutableResource` を一つ作り、起動引数へ一資源分の `DockerWrapperLaunchOptions` を渡す。[`DockerContainerWrapper.RunAsync`](../Testing/Duck.Testing.AppHost/DockerContainerWrapper.cs) はその一資源の単一 ID と owner/stack/resource/run label を照合し、停止・削除もその完全 ID に限定する。owner と follower を一群として順序制御する coordinator はここでは生成されない。
4. `BaseSimulationResourceRegistration` の既存 `WaitFor` / `WaitForStart` は依存資源に対する起動条件を登録しているが、読み取った wrapper 実装・model tests に follower の先行停止、owner の再生成後に全 follower を同じ新 namespace へ再参加させる group lifecycle 契約はない。`DockerContainerWrapperLifecycleTests` は fake Docker CLI による単一 container の停止・cleanup を確認する。実 Docker や shared network namespace の証拠ではない。
5. 既存 `DockerContainerSpecTests` は `--network host` の引数と host-mode owner identity を固定契約として検査する。テストを通すためにこの契約を単純に `container:<owner>` へ拡張するだけでは、resource group の所有・停止・再生成・失敗時cleanupを保証しない。

既存単一Docker CLI wrapper経路は今回採用していない。Compose経路では別途host profile model / fake-CLI testsを追加した。Compose群の実機再生成・hard shutdown後の回収は未確認である。

## 既存 PR の積み上げ状態（読み取り専用）

- [PR #129](https://github.com/ibis-ssl/Duck/pull/129) は open、base `main`、head branch `task/issue-128-host-runtimehost-cm4-topology`、head `26706ebc57912a4138ae611c700dea7cdd06aad9`。
- [PR #130](https://github.com/ibis-ssl/Duck/pull/130) は open、base `main`、head branch `task/issue-128-launcher-dotnet`、head `5e1eabd17686980c37a34c3745f7dbc169e48086`。
- 両方が `README.md`、`Testing/README.md`、`scripts/start-duck-simulation.cs`、`scripts/start-duck-simulation.ps1` を変更している。PR #129 はより広い RuntimeHost/relay 差分を含むが、launcher と利用手順は重複する。
- 現状は parallel 2 PR で、stacked base ではない。両方の base/head を維持し merge / force push / retarget しない条件の下では、既存 PR 自体を stacked に直せない。重複差分を残したまま片方を独立に merge して問題ないとも判定できない。新しいPRを作らず、現在は重複差分のある並列状態と記録する。

## PR #133 の現状

- 初回読み戻し時: [Draft PR #133](https://github.com/ibis-ssl/Duck/pull/133) は open。base `design/issue-128-cm4-shared-namespace`、head branch `task/issue-128-cm4-helper-removal`、head `61bac03adabbad5a3a7dc4a0462816c2fc9752ad`。
- 初回 PR 本文は標準 Aspire/DCP blockerと製品差分なしを説明するが、wrapper / Compose 調査を含まず、SDKも利用不能と誤記していた。Compose調査は commit `e4e98c758c956b2ade734986c58daed5fb7c1d96`、PR公開状態の訂正は commit `8d9f04b4af3a1fce6486955482fe2466f20a33ae` として通常 push 済み。PR #133 のtitle/bodyは新たな証拠に合わせて更新し、GitHubから読み戻して確認した。
- 先の CI run `43682444156` は旧 HEAD `61bac03adabbad5a3a7dc4a0462816c2fc9752ad` の結果: .NET test success（1,448 passed、4 skipped）、ASPIRE full-stack Linux acceptance skipped、ASPIRE-NET-002 Linux packet flow skipped。これは最新 report commit の CI ではなく、wrapper/Compose runtime、Windows / Docker実 packetを証明しない。

## Docker Compose 経路の追加評価

Composeは `network_mode: service:cm4-sim` followerをモデル化できる。今回の差分で標準Aspire DCP resourceではなく、AppHost内で起動するCompose lifecycle managerとして実装し、host RuntimeHost profileへ接続した。過去のDCP blockerは依然として標準ContainerResource APIに関するもの。Compose runtimeと固定imageの実通信が成功した証拠ではない。

1. Duckの `Duck.Testing.AppHost.csproj` は Aspire AppHost SDK / Aspire.Hosting 13.5.4 と既存のDuck project referencesを持つが、Compose hosting integration packageやCompose resource登録はない。手元の13.5.4 NuGet package cacheにも `Aspire.Hosting.Docker` integration packageは見つからなかった。Aspireの公式[Docker Compose deployment guide](https://aspire.dev/deployment/docker-compose/) は `AddDockerComposeEnvironment` をdeployment targetとして説明し、`aspire deploy` が `docker compose up -d --remove-orphans` を行い、後の `aspire destroy` は別操作で確認promptを出す。これはAppHostの通常run内でComposeを親resourceとして保持し、DCPのresource readiness / console logs / session stop/restartへ統合する仕様とは異なる。公式の[AspireとComposeの比較](https://aspire.dev/app-host/migrate-from-docker-compose/)も、Composeのcontainer orchestrationとAspireのhost process / resource dashboardを区別し、startup依存の機構が異なると説明している。
2. Docker Compose公式[service reference](https://docs.docker.com/reference/compose-file/services/#network_mode) は `service:{name}` / `container:{name}` を明示的にサポートし、`network_mode` とservice-level `networks`を同時に使うことを禁止する。したがってSimulator/Crane follower自身を別Compose networkへ接続することはできず、namespace ownerとなるCM4 serviceが接続したnetwork stackを共有する。Compose仕様上の機能存在は、固定 Docker Desktop 上でCM4/Simulator/Crane のloopback endpointが正常に送受信することの実証ではない。
3. Compose followersは`networks`を持てないので、Game Controllerも含めて同じnamespaceに登録し、CM4 owner側に必要なhost portsをpublishするmodelを追加した。Compose managerは起動状態、ログ、異常停止、startup/stop cleanupを管理し、GCの既存WebSocket readinessとCraneの既存ROS readinessを呼ぶ。ただし全てfake CLI/model levelで、固定imageとDocker Desktop上でmulticast受信・fan-outが成立するか未実測。他のprofile consumer (例: TIGERs/Sumatra) はこのbase host profileでは登録していない。
4. AppHost managerは一意Compose project名と解決済みDocker contextを使い、AppHost shutdown時にそのprojectだけを停止し、失敗時もproject-scoped cleanupを試みる。Unexpected service exitでresourceをunhealthyにしAppHost停止を要求する。Compose syntax/Windows path/Docker CLI process cancellation/DCPとの実行順序は実Docker未検証。Crash/force termination後の起動資源cleanlinessとPersistent lifetimeは未保証のためhost profileのPersistentを明示拒否している。
5. Windows上のRuntimeHostとDocker Compose containersのUDP境界はCM4/Simulator/Crane間のloopback問題とは別であり、Compose namespace共有はhost-to-DockerのVision ingress / Tracker multicast egressを解決しない。PR #129のWindows/Docker boundary relayをCompose共有だけで撤去したり、Windows/Docker DesktopでUDPやmulticastが通ると主張しない。

### Compose経路の結論

- **実現可能（仕様面）**: Composeは `network_mode: service:cm4-sim` を表現できる。固定image・起動commandをcompose serviceに記し、Duck sourceをcontainerに含めないこと自体は可能な構成案である。
- **モデル実装済み**: host RuntimeHost profileでCM4 owner、Simulator / Game Controller / Crane / existing UDP relay followersをCompose manager経由で登録。CM4 helper mount/start、Crane overlay mount/entrypointはprofileから外した。
- **検証済み範囲**: model tests、fake CLIでstart・port projection・Crane probe・AppHost shutdown / failed start cleanup、fixed image tag・mount model。host property buildでC helper / overlay outputが作られないこと。
- **未実証 / 未達**: fixed Docker imageでCompose起動、CM4↔Simulator/Crane loopback packet、GC multicast、Windows/Docker UDP、TIGERs等他profile consumer、Docker Desktop再生成/再起動。existing UDP boundary relayは残り、helper-free goal未達。I128-NS-01はモデル実装済みだが実機受入中。

## 最新 HEAD / 公開状態

- PR #133 の最新 base は `design/issue-128-cm4-shared-namespace`、head branch は `task/issue-128-cm4-helper-removal`。先行したCompose lifecycle foundation commit `f68adf341a3feb344404488531d56a61546f0b04` は通常push済み。後続のprofile integration commitをpushした後、この節とPR本文のHEAD / CIを読み戻して確定する。
- PR本文にはprofileへの接続範囲、helper/overlay除外、残存relay、Persistent制限、実機未検証を記載する。先行head `f68adf3` のCI run `43687193748` は.NET tests success、Linux packet-flow/full-stack skippedで、後続headのCI結果ではない。
- PR #129/#130 のremote base/headは読み取りのみ。現状のparallel PRとlauncher重複差分を維持し、いずれも変更していない。

## 未解決・次の境界

- 標準 Aspire/DCP `ContainerResource` routeではなく、ComposeをAppHost内で所有するprofileを実装した。以前のI81-07D DCP follower失敗の原因は未確定のまま扱い、失敗構成は再実行しない。
- Compose managerの実Windows/Docker Desktop起動、固定imageのloopback、Game Controller/他consumerのmulticast ingress/fan-out、owner再生成時のfollower挙動は未実証。AppHostプロセスの強制終了後の残存資源も実測していない。
- Host RuntimeHost profileのPersistent lifetimeは明示拒否する。Session shutdown / failed startupはfake CLIでproject-scoped cleanupを検査した。Legacy profile Persistent/StopPersistentは既存挙動を維持する。
- CM4 C helperとCrane overlayはhost profileから除去したが、既存UDP boundary relayは残存し、helper-free goal未達。Simulator Vision ingress / RuntimeHost Tracker egress の双方向packet pathも未確認。
- 次に必要: 正規のWindows/Docker Desktop環境でPR最新HEADを実行し、既存image identity、AppHost/Compose実行profile、owner ports、GC/Crane readiness、Simulator/CM4 loopback、relay UDP経路を個別に観測。FA780は現在このexecutorからofflineのため実施していない。
- Issue #128 は open のまま。I128-UDP-02 と I128-FINAL-03 は未完了。

## 2026-11-15 追加調査: UDP boundary relay の撤去可否

今回の読み取り調査では Docker を実行せず、既存固定版のソース、host profile の構成、既存の source identity 調査を照合した。現行 `duck-udp-boundary-relay` を除去してよい直接経路は、コードだけでは成立を確認できなかったため、サービス・mount・画像設定を変更していない。

### Simulator Vision → Windows RuntimeHost

- 固定 framework `a52b6bdecd97a277544dd6f275afbf4483781e12` の [`simulator.cpp`](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/simulator/simulator.cpp#L1130-L1179) で、Vision 出力先は `--localhost` の有無により `127.0.0.1` または `224.5.23.2` に決まる。任意の Windows host IP を指定する Vision destination option は当該 CLI にない。`--ibis-feedback-addr` はIBIS robot feedback送信先であり、SSL-Vision送信先ではない。
- 現 Compose profile は Simulator、CM4、Crane の loopback を共有し、RuntimeHost は Windows 上で `127.0.0.1:10020` を受信する。Simulator の `--localhost` を使っても宛先はSimulator自身の共有 namespace の loopbackになり、Windows loopbackへは出ない。通常 multicast の Windows/Docker Desktop bridge 越境受信を裏付ける実 packet evidence もない。
- 結論: 既存 fixed image / source を変えず、Windows RuntimeHost へVisionを直接 unicast する設定は現行 CLIから指定できない。relayを外すには Simulator 側の新しい出力 destination、host networking等の別ネットワーク契約、または追加転送機能が必要になるが、いずれも今回の非変更・非追加条件を満たす根拠がない。

### Windows RuntimeHost Tracker → Docker consumers

- `UdpTrackerPacketPublisher` は設定された数値IP:portの `IPEndPoint` へUDP packet bytesを送信する。RuntimeHost profileは現状 `127.0.0.1:<Compose relayの動的公開port>` を設定し、relayがDocker内で `224.5.23.2:11010` に再multicastする。Game Controller は固定 command `-trackerAddress 224.5.23.2:11010` を使い、既存 consumers はこのmulticast groupを受信する。
- owner portをhostへpublishしてRuntimeHostからunicastすれば単一container方向の試行候補にはなるが、宛先はmulticast groupではなくownerのcontainer IP/portになる。複数 consumerへの既存 multicast fan-out が保たれること、GC の multicast receiverがそのunicast datagramを受けること、別 consumerにも確実に複製されることを既存コードから保証できない。Docker Desktop hostから同bridge内 multicast groupへ送信できる証拠もない。port publishだけではmulticast bridgeを示さない。
- payload の `TrackerWrapperPacket.Uuid` / `SourceName` は同じpublisher packet内にあり、direct sendでpayloadを保持すること自体は可能。一方、現在のrelayはbytesをそのまま再送するが、受信側で見えるsource IP:portをrelayへ変える。direct時はRuntimeHost socketのsource endpointになるため、`MultiTrackerManager` のendpoint-inclusive source keyにより、旧・新endpointの重なりや再起動時に同じpayload identityが別source stateとして見える可能性がある。切替の実測はない。
- 結論: 一 consumer向けunicastは設定候補に過ぎず、既存group consumer全体とsource endpoint契約を保つrelay-free経路ではない。host/network namespaceやDocker Desktopの設定を変えずにmulticast境界を越えられると主張しない。

### CM4 feedback / Crane ROS path（Windows boundaryとは別）

- 固定 Orion_CM4 `d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a` の [`cm4_sim.cpp`](https://github.com/ibis-ssl/Orion_CM4/blob/d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a/cm4/bridge/cm4_sim.cpp) はCraneからのcommandを`:12345`で受け、Simulator向け制御を `127.0.0.1:12346` へ送信し、Simulatorからのrobot feedbackを `127.0.0.1:50100+id` で受信後、製品上必要なrobot-ID別multicastへ再配信する。
- Compose shared namespaceでは Simulator `--ibis-feedback-addr 127.0.0.1`、CM4のloopback制御先、Crane既定 `127.0.0.1:12345` が同一namespace内で合うモデルになり、Duck C helperをこのCM4/Simulator境界から外せる構成根拠となる。Compose/fake CLI/model testsで登録とloopback設定は確認したが、固定imageの実packet、CM4再生成後の到達は未検証。CM4製品プロセス内のposition-controlとfeedback multicastは別の製品機能であり、外部Duck C helperやWindows UDP boundary relayとは区別する。
- Crane ROS readinessは既存のteam/coordinator probeをCompose execで呼ぶモデルを確認済み。ROS DDSの実通信・namespace共有の影響は未検証。

### 結論・最小次段

現段階で `duck-udp-boundary-relay` を削除する差分は作らない。relayを本当に撤去する場合の最小成立条件は、(a) fixed SimulatorCLIに既存の任意Vision destinationがある証拠、またはそのimage/sourceを変更できる別承認、(b) host RuntimeHostから全consumerへ行くmulticast経路または全consumerのunicast受信契約、(c) payload UUID/SourceNameとsource endpointを分けて評価する実 packet / fan-out / 再起動受入である。現在 (a) は固定CLIに存在せず、(b)(c) も未確認。画像変更や新relayを使って達成扱いしない。

PR積み上げについて、読み取り時点の [PR #129](https://github.com/ibis-ssl/Duck/pull/129) はbase `main`、head `task/issue-128-host-runtimehost-cm4-topology`、head `16 commits ahead / 2 behind`。 [PR #130](https://github.com/ibis-ssl/Duck/pull/130) もbase `main`、head `task/issue-128-launcher-dotnet`。双方が `README.md`、`Testing/README.md`、`scripts/start-duck-simulation.cs`、`scripts/start-duck-simulation.ps1` の4 fileを重複変更し、共通の分岐点は `985ce8e`。現在のbase/headを変更せず、merge / force pushもしない条件では並列2 PRのまま。将来整理するなら #130 をlauncher基盤にして、#129のtopic-only差分をそのhead上へ再積層した新しいhead/PRを作るか、#130を先にmergeした後で#129をrebaseする必要がある。どちらも既存PRの書換え/mergeを伴うので今回実行しない。

この追加調査で製品コード、Issue、PR base/head、Docker設定、画像、tag/digestは変更していない。実機、Docker、multicast、UDP packetは未実施。FA780 offlineの状態は回避していない。

## 2026-11-15 FA780 no-build launch failure の切り分け

- 共有された Run1 は Debug build 前の `dotnet run --no-build ... --Testing:RuntimeHostOnWindows=true` で、`Program.cs` の `CraneTargetAddressSettings.FromConfiguration(... useStandardBase: true)` が `cm4-sim` を既定選択し、存在しない `crane-launch-overlay` を要求したところで終了した。Compose stack 登録・起動には到達していない。
- `Duck.Testing.AppHost.csproj` は `DuckHostRuntimeProfile=true` で Crane overlay の publish target と output copy を省略する。Compose host profile の `BaseSimulationResourceRegistration` は `ComposeStack` がある場合、この設定値を消費しない。従って Run1 の事前条件は host profile に不要であり、profile-aware な設定解決で skip する変更を行った。通常 / legacy profile の設定解決と overlay 準備は保持する。
- 追加した `ComposeHostProfileDoesNotRequireTheLegacyCraneOverlay` と、既存の standard Crane overlay / host RuntimeHost model tests は、`dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter 'FullyQualifiedName~ComposeHostProfileDoesNotRequireTheLegacyCraneOverlay|FullyQualifiedName~StandardVisibilityGraphUsesTheExistingCm4CraneTargetOverlayByDefault|FullyQualifiedName~WindowsHostRuntimeProfileUsesRequiredUdpEndpointsAndDoesNotRegisterDuckImage'` で 3/3 passed。これは設定解決・モデル試験であり、FA780 再実行ではない。
- Run2 は通常 Debug build 後に Dashboard が localhost:59702 で listen したとの記録だが、`docker compose up` の呼出・結果は stdout/stderr に記録されず、run 中・終了後の対象 container は観測されず、終了は手動 Ctrl+C の exit 1。Compose manager の生ログ・直接呼出時刻は現在の作業木にないため、manager の `StartAsync` 到達有無や停止段階は確定できない。`StartAsync` が resource の Running を publish する前に `compose up --wait --wait-timeout 120`、port projection、Game Controller readiness、Crane readiness（最大360秒）を逐次待つため、50秒の無進展だけで hang / crash を意味しない。
- この修正が扱うのは Run1 の不要な Crane overlay precondition のみ。Run2 の進行、Compose 起動、UDP/packet、固定 image での namespace/loopback、Windows↔Docker通信は依然未検証。FA780 は offline であり、Docker の再起動・再実行は行っていない。

## 2026-11-15 FA780 Compose relay failure evidence

- FA780 executed the then-current original PR #133 HEAD `38d82d22747596e6275c2bb0261e04259cc98873` in a dedicated checkout with `base`, `visibility_graph`, and `RuntimeHostOnWindows=true`. The four Compose simulation services (CM4 simulator, SimulatorCLI, Crane, Game Controller) became healthy. `duck-udp-boundary-relay` exited with code 139; `docker compose up --detach --wait --wait-timeout 120` returned exit 1 and AppHost startup failed before Windows RuntimeHost startup or packet verification.
- The run was cleaned up: no remaining project containers, network, process, or listener. Locked image digests stayed unchanged. No Duck source was mounted into a container; the relay ELF was the only read-only bind mount, and the C helper / Crane overlay mounts were absent for this profile.
- The AppHost copy and publish artifact were the same 73,674,609-byte Linux x86-64 ELF, SHA-256 `45CD4E5B6CE720DE05D4932A3411668BD1B783882844A3C65F45BB257D059B53`. No relay stdout/stderr, core dump, stack trace, explicit signal, or Docker-host crash detail was available. Exit code 139 is the observed result; its root cause is unknown and is not attributed to a specific signal or architecture incompatibility.
- This is a failed real Compose startup, not a packet test. The relay remains in this interim profile and the helper/relay-free target is still unmet. No rerun or Docker/network setting change was performed during this restack.

## 2026-11-15 restacked Draft PRs

- Draft PR #134 (`task/issue-128-host-runtimehost-on-launcher`) is based on PR #130's launcher branch and contains the 17 unique #129 paths. Head `c93c9c1ef2bae7bb2cceaf65cdf94fcd667d1229`; CI run `43711799687` succeeded for `.NET tests`; Linux packet-flow and full-stack acceptance jobs were skipped.
- Draft PR #135 (`design/issue-128-cm4-shared-namespace-restacked`) is based on #134 and contains only the four design/documentation paths from #132. Head `04a6f69b3bd1c0ff665bb6bacb07f9af3127bc73`; CI run `43712243703` succeeded for `.NET tests`; Linux packet-flow and full-stack acceptance jobs were skipped.
- Draft PR #136 is prepared as a child of #135 by applying the #133 Compose/profile changes and the net launcher delta from #130 to #133. The launcher delta was manually restacked onto the #130 file-based app; the four duplicate launcher/README paths from #129 and the two #131 topology figures are not reintroduced. Its relay remains an interim mounted executable and the helper/relay-free goal is not complete.
- Existing PRs #129 through #133 retain their original base/head. No old PR was edited, closed, merged, or force-pushed. Issue #128 remains open.

## 2026-11-15 comparison `simulator-control-relay` removal follow-up

- Parent-assigned scope: remove the comparison-only `simulator-control-relay` without adding a replacement relay, preserve the existing Simulator control datagram contract and response observation, keep Docker images unchanged and Duck source out of containers, validate relevant builds/unit tests, and push normally to PR #136 only after required checks pass.
- Parent-reviewed source path: HTTP `:39111` currently fronts Simulator control datagrams to `simulator:10300/UDP`; the full-stack acceptance path uses that endpoint for initial own-half setup and response observation. Game Controller control remains a separate `ws://game-controller:8082/api/control` route. This helper is distinct from the Windows/Docker UDP boundary relay tracked by I128-UDP-02.
- Source review: `SimulatorBallControlClient` in `Testing/Duck.Testing.RefereeDriver.Host/SimulatorBallPlacementFixture.cs` already sends the existing protobuf `SimulatorCommand` to an injected `IPEndPoint` on UDP 10300 and starts `InitialSetupResponseCapture` on the same socket before sending. The response observer accepts only `SimulatorResponse` datagrams from the exact configured endpoint, records the same request's sent payload metadata, and bounds observation to five seconds. The full-stack acceptance harness already obtains Docker's dynamically published endpoints through `docker inspect`; the change adds the Simulator's `10300/udp` dynamic loopback endpoint only for comparison + enabled container RefereeDriver mode and injects that inspected endpoint into the existing client. This preserves the request payload and response-observation path in source/model, without adding a sidecar. The Game Controller `ws://game-controller:8082/api/control` path remains separate and unchanged.
- Code changes: `Testing/Duck.Testing.AppHost/Program.cs` registers the comparison-only dynamic `10300/udp` endpoint and removes `simulator-control-relay`; `Testing/Duck.Testing.RefereeDriver.Host/Program.cs` removes its `--simulator-control-relay` dispatch; `SimulatorControlRelayApplication.cs`, `Tracker.Tests/SimulatorControlRelayClient.cs`, and its client/server test files are deleted. `AppHostApplicationModelTests.cs` asserts the relay is absent and the simulator endpoint is dynamic, loopback-targeted UDP 10300. `AspireFullStackAcceptanceTests.cs` resolves the simulator UDP mapping and uses the existing direct client for both own-half setup and ball placements. No image, image tag/digest, Dockerfile, Duck source mount, or Game Controller control route changed.
- Focused validation passed: `source /workspace/.duck-env/env.sh && dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 /nr:false --filter 'FullyQualifiedName~ComparisonModeAddsPinnedExternalTrackersAndObserverWithoutAnotherRefereeProducer|FullyQualifiedName~SimulatorInitialSetupResponseTests'` — exit 0, 10 passed, 0 failed, 0 skipped.
- Full-stack acceptance source compile passed: `source /workspace/.duck-env/env.sh && dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 /nr:false -p:RunAspireFullStackAcceptance=true` — exit 0, 0 errors (31 warnings, including existing Aspire/analyzer warnings). This compiles the gated acceptance path; it does not run Docker or packet acceptance.
- Required builds passed: `source /workspace/.duck-env/env.sh && dotnet build Duck.slnx --no-restore -m:1 -p:BuildInParallel=false /nr:false` — exit 0, 0 errors, one ASPIRE010 warning; and `source /workspace/.duck-env/env.sh && dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 /nr:false` — exit 0, 0 errors, two warnings. The test project build reports MSB9008 for an obsolete `obj` asset reference to the previously deleted `Duck.Testing.UdpBoundaryRelay.csproj`; restore was not run and the current csproj/solution do not reference that project.
- The required full test command `source /workspace/.duck-env/env.sh && dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-build --no-restore -m:1 /nr:false` completed with exit 1: 1,438 passed, 4 skipped, and 2 unrelated existing Crane process-group failures: `CraneLaunchOverlayTests.StopAndReapForceKillsGroupThatIgnoresTerminate` and `CraneLaunchOverlayTests.StopAndReapTerminatesOnlyTheOwnedProcessGroup`, both throwing `OwnedChildNotReapedException`. Parent review confirmed these separate known failures do not invalidate the scoped related-test/build gate; normal commit/push to the existing PR branch was then authorized. At report-edit time HEAD is `fbcf45df39051eb6ef1d10ac923216df6de47190`; commit/push and remote readback are pending.
- Markdown lint command `npm run lint:md:text -- --files Tracker/Design/tasks-status.md Tracker/Design/phases-status.md` was attempted and exited 1 because `node_modules/textlint/bin/textlint.js` is missing. No dependencies were installed. `git diff --check` passed.
- Runtime limits: no Docker/FA780/container execution, image pull/build/tag action, or packet test was performed. The exact UDP response source after Docker Desktop's host-port NAT and Windows-to-container delivery are unverified; the unchanged client requires the reply source to equal the mapped endpoint, so the real full-stack acceptance remains necessary. The comparison-only `simulator-control-relay` code/resource is removed, but the separate Windows/Docker UDP boundary relay tracked by I128-UDP-02 remains and the overall all-relay-free goal is not complete.
- Execution limits: no FA780/Docker launch, no image pull/build/tag mutation, no network or environment changes, no new agent/branch/PR/Issue, no merge or force push.
