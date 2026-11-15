# Issue #128 I128-NS-01 実装可否報告

## 実行範囲

- 作業担当: `/root/issue128_ns_implementation`
- 対象: Issue #128 の I128-NS-01（Windows-host RuntimeHost profile における CM4 / Simulator / Crane の namespace 経路）
- 作業ブランチ: `task/issue-128-cm4-helper-removal`
- 指定 base / 開始 HEAD: `8193cb6500228c7dbb8e604994f13dc5e3d0ad8c`（設計 PR #132 の `design/issue-128-cm4-shared-namespace`）
- 開始時の状態: clean。branch と HEAD は依頼の指定値に一致。
- モデル: 起動要求は `gpt-6-luna` / `high`（親の `spawn_agent` 指定）。実際に適用されたモデル情報はこの実行環境から観測できないため `null`。
- 認可境界: 既存 image・タグ・digest、Dockerfile、container 内のソースを変更しない。Docker / Windows 実行、既存 I81-07D の再実行、UDP boundary relay の変更、PR / Issue 操作、merge は対象外。

## 判定

この作業環境で確認できた固定 Aspire.Hosting 13.5.4 の公開 `ContainerResource` モデルには、1つの container を network namespace owner とし、別の標準 container をその namespace follower として宣言する対応済み API route が見つからなかった。したがって製品コードは変更していない。これは「Docker で namespace 共有が不可能」とする結論ではなく、現在の固定 Aspire/DCP と標準 `ContainerResource` を使う対応済み route を確認できなかったという阻害である。

raw `--network container:<owner>` を `WithContainerRuntimeArgs` で追加する案は、公開モデルで共有モードを表すものではない。固定版 DCP は各標準 container に `DefaultAspireContainerNetwork` を設定し、別途収集した runtime args を `Spec.RunArgs` に設定する。既存ネットワーク指定を共有モードへ置き換える標準モデル上の経路は、参照した実装では確認できない。この構成を実装済みや runtime 成功と扱わない。

## 根拠

1. Aspire 13.5.4 の [`ContainerCreator.PrepareContainerNetworks`](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/Dcp/ContainerCreator.cs#L2343-L2478) は、コンテナ資源の `Spec.Networks` に `DefaultAspireContainerNetwork` を設定する。また、独自 container network annotation は `Custom container networks are not supported yet.` として拒否する。
2. 同じ版の [`BuildRunArgsAsync` と呼出側](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/Dcp/ContainerCreator.cs#L2641-L2667) は runtime argument annotation を `Spec.RunArgs` に設定する。ネットワーク生成コードが追加の runtime args により `Spec.Networks` を置き換える処理は、ここで参照した経路にない。
3. [`WithContainerRuntimeArgs`](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/ContainerResourceBuilderExtensions.cs#L3008-L3059) の API 説明は、引数を underlying container runtime の run command に「追加」する用途としている。共有 namespace を表す高水準モデル API とは説明されていない。
4. 既存 [`issue81-referee-driver-scope-20261022.md`](issue81-referee-driver-scope-20261022.md) は、13.5.4 DCP の default network と追加 `--network container:...` 指定が同じ生成物に残る静的懸念を記録している。DCP binary の引数解釈順や実 Docker 作成エラーは未確認で、モデル上の矛盾と実際の失敗原因を区別している。
5. [I81-07D 記録](../Tracker/Design/Testing/aspire-simulation-test-environment.md)では、標準 `ContainerResource` を使った既存の限定組成で follower の開始に失敗したが、Docker create error は保存されていない。3回目に owner 1世代と終了時の所有資源残存0が確認された。I81-07D は再実行しておらず、原因は未確定のまま保持する。旧 `--network host` conflict は別構成であり、本件の診断に用いていない。
6. 設計 PR #132 の [`issue-128-cm4-namespace-design-20261115.md`](issue-128-cm4-namespace-design-20261115.md) が示す CM4 / Simulator / Crane の loopback endpoint は候補設計の根拠であって、namespace 共有・UDP 通信・再生成挙動の実測ではない。

## 候補と実装の区別

- 候補: CM4 を namespace owner、Simulator と Crane を follower とし、固定 image 内の既存 loopback endpoint を保つ。条件が成立すれば、この profile から CM4 C helper の読み取り専用 mount と Crane launch overlay を撤去する。
- 実装: なし。CM4 registration、Simulator/Crane registration、helper mount / asset / startup、overlay publish / mount / destination は変更していない。
- 固定 image: pull、build、tag、digest、Dockerfile の変更なし。
- container source: Duck source / executable の追加配置なし。
- 他スライス: `Duck.Testing.UdpBoundaryRelay` は未変更。Windows RuntimeHost と Docker consumers の UDP boundary は別タスクのまま。

## 検証

- 実施: Git branch/status/HEAD 確認、既存設計・I81-07D・I81-07H 報告の読み取り、Aspire 13.5.4 の公開 API と DCP ソース確認。
- 未実施: product build、model test、実 Docker / DCP、Windows / FA780、UDP packet test。コード変更がなく、依頼で禁止された runtime topology 再試行もしないため実行していない。
- SDK 状態: 初期 `command -v dotnet` は `PATH` 上で見つからなかった。リポジトリ手順の `/workspace/.duck-env/env.sh` を読み込んだ後は `/workspace/.duck-env/dotnet/dotnet`、SDK `10.0.401` が利用可能だった。今回製品差分を作っていないため build/test は実行していない。
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

今後この経路を再検討する場合、最低限必要な pure model coverage は host RuntimeHost profile に限定された owner/follower 登録、follower が owner の準備完了後にだけ開始すること、fixed image と args が変わらないこと、helper/overlay が登録されないこと。実 wrapper の fake-CLI lifecycle coverage には複数 container の起動・AppHost 停止・owner 異常停止・owner 再生成時に、follower cleanup と再参加が owner 世代に結び付くことを含める必要がある。現行 wrapper にその群制御がないため、今回これらのテストや speculative network-mode extension は追加していない。モデル test は追加・実行なし。

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

Docker Compose は Docker の正式な orchestration としてサービス群を記述・起動・停止でき、`network_mode: service:cm4-sim` はCM4 serviceと同じnetwork namespaceを使う follower をモデル化できる。このComposeモデルだけを見る限り、CM4 / Simulator / Craneのloopback通信に使えそうで、外部製品のソース変更・固定image変更・containerへのDuck source配置は仕様上必須ではない。しかし、Duckの今の Aspire AppHost run からCompose一式を安全に所有・監視する既存経路が確認できず、完成実装に採用していない。

1. Duckの `Duck.Testing.AppHost.csproj` は Aspire AppHost SDK / Aspire.Hosting 13.5.4 と既存のDuck project referencesを持つが、Compose hosting integration packageやCompose resource登録はない。手元の13.5.4 NuGet package cacheにも `Aspire.Hosting.Docker` integration packageは見つからなかった。Aspireの公式[Docker Compose deployment guide](https://aspire.dev/deployment/docker-compose/) は `AddDockerComposeEnvironment` をdeployment targetとして説明し、`aspire deploy` が `docker compose up -d --remove-orphans` を行い、後の `aspire destroy` は別操作で確認promptを出す。これはAppHostの通常run内でComposeを親resourceとして保持し、DCPのresource readiness / console logs / session stop/restartへ統合する仕様とは異なる。公式の[AspireとComposeの比較](https://aspire.dev/app-host/migrate-from-docker-compose/)も、Composeのcontainer orchestrationとAspireのhost process / resource dashboardを区別し、startup依存の機構が異なると説明している。
2. Docker Compose公式[service reference](https://docs.docker.com/reference/compose-file/services/#network_mode) は `service:{name}` / `container:{name}` を明示的にサポートし、`network_mode` とservice-level `networks`を同時に使うことを禁止する。したがってSimulator/Crane follower自身を別Compose networkへ接続することはできず、namespace ownerとなるCM4 serviceが接続したnetwork stackを共有する。Compose仕様上の機能存在は、固定 Docker Desktop 上でCM4/Simulator/Crane のloopback endpointが正常に送受信することの実証ではない。
3. Game Controllerや他のVision multicast consumerは別serviceのままならCM4 ownerのbridge networkに加わる必要がある。しかしnetwork_mode followerは`networks`を持てず、Docker公式[bridge driver documentation](https://docs.docker.com/engine/network/drivers/bridge/)は同じbridge上のcontainer間通信・service到達を説明するものの、今回必要なmulticast group ingress / fan-outの保証を記載していない。Game Controllerを含む各consumerも同namespaceへ移すと、別の資源のlisten port・host publish/UI到達を再配置する必要があり、この作業の小範囲では安全性を確認できない。multicast受信や複数consumer fan-outは未保証・未実測とする。
4. Composeはcontainer起動、停止、logs、healthcheck、restart policyなどcontainer側運用を構成できる。ただし今のAppHostには `compose up/down`、親resource readiness/log bridge、owner/follower group cleanup / generation handlingを結び付けるruntime integrationがない。別ExecutableResourceやfile-based launcherでCompose CLIを呼ぶなら、そのコードが新しい外部 orchestration helper / group lifecycle managerとなるため「現行のAppHost resource modelが安全に管理する」という条件を満たさない。未確認のAPIをつないだだけのモデルtestを作っても、起動・停止・再生成契約の証明にならないため製品コード・model testを追加しない。
5. Windows上のRuntimeHostとDocker Compose containersのUDP境界はCM4/Simulator/Crane間のloopback問題とは別であり、Compose namespace共有はhost-to-DockerのVision ingress / Tracker multicast egressを解決しない。PR #129のWindows/Docker boundary relayをCompose共有だけで撤去したり、Windows/Docker DesktopでUDPやmulticastが通ると主張しない。

### Compose経路の結論

- **実現可能（仕様面）**: Composeは `network_mode: service:cm4-sim` を表現できる。固定image・起動commandをcompose serviceに記し、Duck sourceをcontainerに含めないこと自体は可能な構成案である。
- **現行AppHostで未成立**: AspireのCompose integrationはdeployment output/operationであり、このrepositoryの通常AppHost runtimeに親resourceとして実装されていない。host RuntimeHostとの同一実行・readiness・logs・stop/restart・owner再生成時のfollower管理は未確認。
- **packet path未確認**: CM4↔Simulator/Crane loopback、Game Controllerその他のmulticast receive/fan-out、Docker Desktop、Windows RuntimeHost境界はすべて実測なし。
- **作業判断**: 新しいCompose parent/helperまたは未実証multicast経路を作って完成扱いにはせず、製品コード・testsを変更しない。I128-NS-01をblockerのままにし、C helper / Crane overlayは残す。

## 最新 HEAD / 公開状態

- Compose調査 commit `e4e98c758c956b2ade734986c58daed5fb7c1d96` と公開状態の訂正 commit `8d9f04b4af3a1fce6486955482fe2466f20a33ae` は、`61bac03adabbad5a3a7dc4a0462816c2fc9752ad` の子として同じ既存branchへ通常pushした。PR #133 は Draft / OPEN、base `design/issue-128-cm4-shared-namespace`、head `8d9f04b4af3a1fce6486955482fe2466f20a33ae`。title は `[Issue #128] CM4 namespace feasibility blocker`。
- PR #133 の本文には製品コード変更なし・wrapper/Compose blocker・検証範囲を明記した。前回の本文にあった「このexecutorに.NET SDKがない」は不正確で、`/workspace/.duck-env/env.sh` 読み込み後にSDK 10.0.401が利用可能だった。ただし今回build/testは実行していない。現HEADの CI run `43684313248` は読み戻し時点で .NET tests in progress、Linux packet-flow skipped。
- PR #129/#130 のremote base/headは読み取りのみ。現状のparallel PRとlauncher重複差分を維持し、いずれも変更していない。

## 未解決・次の境界

- DCP が標準 `ContainerResource` の既定ネットワークを除去・置換できる公開 route、または既存 Aspire/DCP 管理下で共有 namespace を表せる対応済み API の追加根拠が必要。
- I81-07D の既存失敗は再実行せず、根本原因未確定を維持する。新しい topology の Docker 実行・失敗再現も今回行っていない。
- 対応 route がない場合の次の設計判断は、標準 `ContainerResource` の範囲外となる DCP 統合または orchestration 経路を別途明示的に設計する必要がある。今回、内部 DCP モデルの書換えや非標準 custom resource を推測で採用していない。
- 実装可能な経路が確定するまで helper と overlay を除去しない。CM4 loopback、feedback、multicast fan-out、CM4 / Simulator / Crane の再生成追従、Windows-host RuntimeHost の双方向 packet path は未検証。
- Issue #128 は open のまま。I128-UDP-02 と I128-FINAL-03 の状態は変更していない。
