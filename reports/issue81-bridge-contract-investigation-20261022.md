# Sub-agent実行レポート

## タスク

I81-G: Issue81/PR84の方式B実装に必要な外部通信・自サービス診断・Aspire13.5.4 localhost bind契約を一次ソースで確定する。

## sub-agentを使う理由

利用者指定の既存実行担当へ調査を委任し、親は採用判断と統合を所有する。

## 対象範囲

固定した外部版とDuck設定、Aspire13.5.4/DCPの正確な静的契約を対応付け、Duck内で完結する変更と未承認の外部変更を分ける。

## 対象外

製品・設計・canonical tracking編集、外部リポジトリ編集、環境/依存/認証/network変更、イメージ取得・ビルド・起動、実機試験、commit/push、PR/Issue操作、nested agentは禁止。読取調査と本報告の子所有欄だけ。

## Dispatch profile

Parent-owned. task_kind=investigation; work_class=judgment_heavy; uncertainty=high; change_radius=cross_system; criticality=high; decomposability=sequential_dependencies; decomposition_policy=forbidden. requested=gpt-6.1-sol/high from original actual spawn; continuity=/root/multicast_investigation_fresh; role/default impact hidden; applied=null; application_status=reused_existing_agent_profile; profile_observability=final_profile_hidden. Same-agent continuation, no new spawn. Parent profile requested gpt-6.1-sol/low.

## 実行コマンド

- 作業先は /home/donabe/Duck-issue81-aspire-desktop-bridge、branch=task/issue81-aspire-desktop-bridge、base=task/aspire-docker-desktop-496、HEAD=6902b736d7934a65147624b2581c3a82e86a2e8a。開始時のgit statusは本報告だけ未追跡。PowerShell login=false／require_escalated → wsl.exe -d Ubuntu の既存経路で本報告・Duckソースを読んだ。
- 指定の work-context-manager、implementation-executor、implementation-worker、report-output-manager、report-writer を /home/donabe/CodexSkill/skills/<name>/SKILL.md から実読。context解決、実装前の依存読取、通常報告に適用した。名前にinvestigation／researchを含む関連Skillは同skills直下に見つからず、存在しない手順を適用済みとは扱わない。orchestrator再入場、nested Codex／agentなし。
- WSLのghは利用不可。既存Windowsの C:\\Program Files\\GitHub CLI\\gh.exe を用い、gh api のGETだけで外部一次ソースを読んだ。auth変更・token表示・connector使用なし。
- 主な正確な取得形: gh api 'repos/<owner>/<repo>/git/trees/<固定ref>?recursive=1'、gh api 'repos/<owner>/<repo>/contents/<path>?ref=<固定ref>' -H 'Accept: application/vnd.github.raw+json'、gh api 'repos/<owner>/<repo>/git/refs/tags/<tag>'。対象repo/ref/pathと行は後掲の各URLで固定した。GitHub code検索はrepo／fileの発見だけに用い、そのHEAD本文を固定版の証拠にせず、該当固定refから再取得した。
- 参照発見のGETは orgs/ibis-ssl/repos?per_page=100（およびpage=2）、search/code?q=cm4_sim+org%3Aibis-ssl、search/code?q=getHostAddressAndPortForContainerPort+repo%3Amicrosoft%2Fdcp。DuckのIssue／PRへの再操作はしていない。親の双方向リンク証拠を継承した。
- API取得は終了コードを回収済み。最初に推測した ibis-ssl/orion-cm4-sim の固定treeは404／exit1だったため、検索で実在する ibis-ssl/Orion_CM4 を特定して固定SHAを取得した。crane_world_model_publisher.cpp、ssl-go-tools/multicastServer.go、DCP/networking/constants.go の推測pathも404／exit1。必要な前二者はtreeから実pathを特定して取得、DCP定数の推測pathは証拠にせずcli_orchestratorの既定127.0.0.1の明示処理とDocker公式で確認した。
- tree絞込のjqで文字列内のバックスラッシュを誤った2試行はparse error／exit1、正しいフィルターで再取得した。Aspire全treeの初回出力は上限に達したため絞込版を再取得。WSL /tmp探索の一部systemd-privateはPermission denied／exit1で、迂回しなかった。これらは検査成功やソース不存在の根拠にしない。
- webは公式Dockerのport-publishing、Aspire13.5.4 Versions.props、DCP0.25.13 cli_orchestratorを読取。ソースGETと本報告保存以外の実行はなし。image manifest／layer取得、image pull、build、container、DCP、Windows実機、製品test／lintは実行していない。

## 対象ファイル

- 書込みは本 reports/issue81-bridge-contract-investigation-20261022.md の子所有欄だけ。先頭の既記入／Dispatch profileと末尾の親所有事前証拠をそのまま保持する。
- Duck側の根拠は HEAD6902b736 の Testing/Duck.Testing.AppHost/Program.cs、AppHost csproj、Directory.Packages.props、現行の aspire-simulation-test-environment.md と tracker-comparison-debug-design.md、historyおよび前担当の計画報告。canonical tasks／phasesは編集しない。
- 外部は下表の固定refにある通信・受信・復号・診断・公開workflowと直接依存を読取のみ。外部checkout作成・編集、依存導入なし。ソースを取得して読み解いたことは、そのimageを取得・起動したことを意味しない。

## 指摘事項

### 判定の要約

| ゲート | 静的調査の判定 | 実装へ渡せるもの／止めるもの |
| --- | --- | --- |
| G1 外部通信・再解決 | 一部確定、全体blocked | TIGERs／ER-Force／GCのマルチキャスト経路は既存の宛先・port／IF機構を使える。Crane→cm4／simulator、cm4→simulator、simulator→cm4の個別再作成追従は固定版の設定だけで成立しない。少なくともこの3コード系統の最小外部変更と固定成果物が先。 |
| G2 localhost・動的TCP公開 | 静的契約を確定、Windows実測は未実施 | Aspire13.5.4と指定DCP0.25.13で標準AddContainerの限定公開APIからDocker -pまで経路を示せた。API契約欠落という前段の保留は解消できる。実際のWindows DCP版／HostIp／UI到達は#82へ残る。 |
| G3 対象サービス自身の診断 | 部分利用可、全体blocked | Craneのfeedback統計、GCのtracker一覧、ERの実サービスlog、TIGERsの復号後イベント／記録機構は存在する。全必要入力の正常復号件数・最後の受信時刻を資源内HTTPで示す統一契約は確認できず、外部への最小計測追加または既存APIの供給証拠が必要。 |
| 全体I81-G | 調査完了、通過認定なし | 未確定／外部変更依存が残るため製品実装へ進まない。正式リンク成立とは別の設計・供給依存である。 |

### 固定イメージとソース対応

Duck Program.cs:39–55、65、96、114、143、174、194の既定値を基準とする。利用者設定でtagを上書きできるので、後続実行は実際の解決値も記録する。

| 資源 / Duck既定image ref | 調査した固定ソースと公開根拠 | 対応の限界 |
| --- | --- | --- |
| Crane: ghcr.io/ibis-ssl/crane:scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d | ibis-ssl/crane@4063cd31cd5b11b1cc919003907f5f4c527b252d。[公開workflow](https://github.com/ibis-ssl/crane/blob/4063cd31cd5b11b1cc919003907f5f4c527b252d/.github/workflows/docker_build.yaml#L97-L106)がscenario-SHA形式を生成 | 命名とworkflowは対応。現registry digest／実際に公開されたbinaryの一致は未取得 |
| cm4-sim: ghcr.io/ibis-ssl/orion-cm4-sim:d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a | 実source repoは ibis-ssl/Orion_CM4@d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a。[workflow](https://github.com/ibis-ssl/Orion_CM4/blob/d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a/.github/workflows/cm4-sim-docker.yml#L55-L64)がcm4/Dockerfileから同image名・github.sha tagを生成 | repo名とimage名は異なる。digestは未確認 |
| simulator: ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd | ibis-ssl/framework@a52b6bdecd97a277544dd6f275afbf4483781e12。[workflow](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/.github/workflows/docker-publish.yaml#L18-L20)と[短縮SHA tag・attestation](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/.github/workflows/docker-publish.yaml#L48-L82) | a52b6bdをAPIでfull SHAへ解決した。digestとattestationの現物は未確認 |
| TIGERs: tigersmannheim/sumatra:2025 | source tag2025→TIGERs-Mannheim/Sumatra@0bb4c653ef8611f9e71a880165f101209be97b3c。[ref](https://api.github.com/repos/TIGERs-Mannheim/Sumatra/git/refs/tags/2025)、[Docker公開workflow](https://github.com/TIGERs-Mannheim/Sumatra/blob/0bb4c653ef8611f9e71a880165f101209be97b3c/.github/workflows/release.yml#L3-L26) | workflowの起動tagはversion/*で、2025 refだけではimage:2025のbinary一致を証明しない。対応するbuild provenance／digestの供給が必要 |
| ER-Force: roboticserlangen/autoref:2025.1.0 | source tag2025.1.0→robotics-erlangen/autoref@da862ea353d877fbbb66dd73827543b78364dc6a。[ref](https://api.github.com/repos/robotics-erlangen/autoref/git/refs/tags/2025.1.0)、[公開workflow](https://github.com/robotics-erlangen/autoref/blob/da862ea353d877fbbb66dd73827543b78364dc6a/.github/workflows/publish-docker.yml#L33-L50)。src/framework gitlink=9814d05763a426f24e5bb134ad7ae2607c64b71e、[repo指定](https://github.com/robotics-erlangen/autoref/blob/da862ea353d877fbbb66dd73827543b78364dc6a/.gitmodules#L1-L3) | 受信／復号処理は別のrobotics-erlangen/framework固定gitlinkで調査。ibis-ssl/frameworkのsimulator版と混同しない。digest未確認 |
| GC: robocupssl/ssl-game-controller:3.20.3 | source v3.20.3→RoboCup-SSL/ssl-game-controller@8050f232c3130323bbd91d1d3d56e9553506c8e4。[ref](https://api.github.com/repos/RoboCup-SSL/ssl-game-controller/git/refs/tags/v3.20.3)、[公開処理](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/.circleci/config.yml#L109-L114)がvを除いたtagを使用 | tag命名は対応。binary/digest未確認。直接依存ssl-go-tools=v1.11.0は同版go.modで確認 |

全imageは現Duck既定値がtagで、@sha256固定ではない。今回imageを取得せず、これらをimmutableなbinary同定済みとは扱わない。必要供給物は各imageの実digest、source revision／submodule、build provenance、変更したCLIと診断schemaの対応表である。

### G1 通信契約と必要な最小外部変更

| 対象 / 成立する部分 | 固定版のfile・symbolと阻害 | 最小変更・提供条件 |
| --- | --- | --- |
| Crane送信 / multicast受信先は既存224.5.23.2／224.5.23.1を保持できる | [crane.launch.xml:34–45](https://github.com/ibis-ssl/crane/blob/4063cd31cd5b11b1cc919003907f5f4c527b252d/crane_bringup/launch/crane.launch.xml#L34-L45)がsimの送信先127.0.0.1とport12345を固定。[IbisSenderNode constructor:123–149](https://github.com/ibis-ssl/crane/blob/4063cd31cd5b11b1cc919003907f5f4c527b252d/crane_sender/src/ibis_sender_node.cpp#L123-L149)はaddress::from_stringで指令と設定12350のendpointを一度だけ作り、[callback:444–446](https://github.com/ibis-ssl/crane/blob/4063cd31cd5b11b1cc919003907f5f4c527b252d/crane_sender/src/ibis_sender_node.cpp#L444-L446)は保持先へsend_toする | crane.launch.xmlにtarget host／portを明示して渡す入口を追加し、IbisSenderNodeにDNS再解決とendpoint差替えを追加。cm4使用時はcm4-sim:12345／12350、非使用時はsimulatorの確定portへ。DuckのCRANE_TARGET_PORT環境変数だけでは、当該固定launchが読む証拠がなく、port12345固定を変えない。再作成後のIP更新と準備解除／再確認が必要 |
| cm4指令入力0.0.0.0:12345、設定入力12350 / feedback multicastの宛先は既存機能 | [cm4_sim.cpp:523–573](https://github.com/ibis-ssl/Orion_CM4/blob/d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a/cm4/bridge/cm4_sim.cpp#L523-L573)はout-addrをinet_addrで一度変換、feedback入力はbindUdp(127.0.0.1,50100+id)固定。drainFeedbackとsendControlDatagramも保持sockaddrを使う。設定だけで別コンテナからfeedbackを受けられない | 同fileのOptions／parseArgs／mainにfeedback bind address指定を追加し、内部IFまたは0.0.0.0を使用。out-addrにDNS名解決・期限付き再解決を追加。返答の実送信先はsimulator側でも更新。--multicast-ifは既存で数値IF／空文字を受け、空文字はOS選択なのでIF設定のためだけにコード変更が必須とはしない。外部ネットワークへ流さずコンテナIFで再参加・送出を確認 |
| simulatorの制御受信10300／10301／10302とIBIS12346はAny bind、visionは既定224.5.23.2:10020 | [simulator.cpp IbisCommandAdaptor:658–671](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/simulator/simulator.cpp#L658-L671)、[CLI:1225–1238](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/simulator/simulator.cpp#L1225-L1238)、[QHostAddress初期化:1316–1322](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/simulator/simulator.cpp#L1316-L1322)、[IbisFeedbackAdaptor::handleGroundTruth:1111–1114](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/simulator/simulator.cpp#L1111-L1114)。feedback-addrは既定loopback、QHostAddressの数値変換結果をm_addrへ保持しDNS再解決なし | --ibis-feedback-addrへcm4-simのhostnameを指定できる名前解決と再解決・更新を同fileへ追加。旧宛先の待機送信を無効化し、相手再作成後に更新する。既定vision multicastを使うだけなら任意unicast宛先の新設は不要。SSL制御の返答はdatagram.senderAddress／senderPortから返す別経路で、IBIS feedbackの固定宛先と混同しない |
| TIGERsのvision10020、referee11003、tracker11010 | [SSLVisionCam.startModule／run](https://github.com/TIGERs-Mannheim/Sumatra/blob/0bb4c653ef8611f9e71a880165f101209be97b3c/modules/moduli-cam/src/main/java/edu/tigers/sumatra/cam/SSLVisionCam.java#L72-L93)、[NetworkRefereeReceiver.start](https://github.com/TIGERs-Mannheim/Sumatra/blob/0bb4c653ef8611f9e71a880165f101209be97b3c/modules/moduli-referee/src/main/java/edu/tigers/sumatra/referee/source/NetworkRefereeReceiver.java#L60-L78)、[VisionTrackerSender.startModule](https://github.com/TIGERs-Mannheim/Sumatra/blob/0bb4c653ef8611f9e71a880165f101209be97b3c/modules/moduli-wp/src/main/java/edu/tigers/sumatra/wp/exporter/VisionTrackerSender.java#L36-L57)に入力IF選択／全IFと出力interface指定がある | Duckの比較fixtureで3宛先・source=NETWORK・gameController=false・publishRefereeMessages=falseを維持。peerの動的unicast IPを保持する経路ではないため、このmulticast通信のためのDNS改修は不要。コンテナ再作成時はプロセスが新しいIFで受信を開始。実同時受信は未実測 |
| ER-Forceの同じmulticast入力／追跡出力 | [autorefcli.getSettings／buildCommand](https://github.com/robotics-erlangen/autoref/blob/da862ea353d877fbbb66dd73827543b78364dc6a/src/cli/autorefcli.cpp#L57-L113)、[Amun.setupReceiver](https://github.com/robotics-erlangen/autoref/blob/da862ea353d877fbbb66dd73827543b78364dc6a/src/backend/amun.cpp#L132-L153)、[framework Receiver.startListen:67–95](https://github.com/robotics-erlangen/framework/blob/9814d05763a426f24e5bb134ad7ae2607c64b71e/src/amun/receiver.cpp#L67-L95)、[VisionTrackedPublisher](https://github.com/robotics-erlangen/autoref/blob/da862ea353d877fbbb66dd73827543b78364dc6a/src/backend/visiontrackedpublisher.cpp#L27-L55)がAnyIPv4／各IFへのjoinとmulticast出力を持つ | --vision-port10020、--gc-port11003、--tracker-port11010を維持。この経路のために外部DNS対応を追加する必要はない。明示IFだけへ絞るCLIがあるとは今回確認していない。設計の明示IPv4受入条件との対応は未確定として保持 |
| GCのvision／tracker受信とreferee送信 | [GC main flags](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/cmd/ssl-game-controller/main.go#L20-L25)、[gc.NewGc／Start](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/gc/gc.go#L49-L80)、直接依存[ssl-go-tools v1.11.0 MulticastServer](https://github.com/RoboCup-SSL/ssl-go-tools/blob/v1.11.0/pkg/sslnet/multicast_server.go#L81-L118)がreuseport・選択IFのjoinを使う | 現行の3宛先と内部-address :8082を維持。skipInterfacesでコンテナ内IFを扱い、Windows NICを渡さない。このmulticast経路のDNS改修は不要。11003のproducerはGCだけ。公開はG2のTCPのみ |

Craneのrobot feedbackは[RobotFeedbackReceiverとROS出力](https://github.com/ibis-ssl/crane/blob/4063cd31cd5b11b1cc919003907f5f4c527b252d/crane_robot_receiver/src/robot_receiver_node.cpp#L350-L401)で224.5.20.(100+id):50100+idを受信する。cm4の既存feedback再配信は製品のrobot制御経路で、10020／11003／11010のホスト境界を越す新中継ではない。同一ブリッジ内の既存経路として残す。

再解決の実装条件は、起動時一回のIP注入では足りない。有限間隔で名前を取り直し、相手の世代／宛先変更時に旧endpointと旧診断を無効化する。UDPは古い宛先へのsendが成功を返し得るため、送信エラー時だけの再解決にしない。未承認の固定IP、relay、共有namespace、単一コンテナ化へ置換しない。外部変更は本調査では実施しない。

### G2 Aspire13.5.4 → DCP → Docker の確定した静的契約

1. Aspire13.5.4の[eng/Versions.props:36–42](https://github.com/microsoft/aspire/blob/v13.5.4/eng/Versions.props#L36-L42)はWindowsを含む各DCP packageを0.25.13へ固定する。DCP tag v0.25.13のcommitは8efd260c452d76d1d98cbf40ab58d8b4a7142ef0。以前に観測したDCPを今のWindowsで実行済みとは扱わない。
2. 公開APIは[WithHttpEndpoint(targetPort, port, name, isProxied)](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/ResourceBuilderExtensions.cs#L1774-L1778)と、[WithEndpoint(name, Action<EndpointAnnotation>)](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/ResourceBuilderExtensions.cs#L1468-L1484)。EndpointAnnotation.TargetHostはpublic setである。
3. [DcpExecutor:693–715](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/Dcp/DcpExecutor.cs#L693-L715)はTargetHostをService.Spec.Addressへ渡す。[ContainerCreator.BuildContainerPorts:1019–1053](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/Dcp/ContainerCreator.cs#L1019-L1053)はTargetPort→ContainerPort、TargetHostが文字列localhost以外なら→HostIP。指定した非proxy host portはHostPortへ渡す。
4. DCPの[ContainerPort API](https://github.com/microsoft/dcp/blob/v0.25.13/api/v1/container_types.go#L150-L165)はhostIPを保持する。[Docker CLI引数生成:625–647](https://github.com/microsoft/dcp/blob/v0.25.13/internal/docker/cli_orchestrator.go#L625-L647)はHostIPを-pへ含め、空なら明示的に127.0.0.1を選ぶ。HostPort=0なら127.0.0.1::<target>/tcpの形にする。Docker公式の[localhost公開仕様](https://docs.docker.com/engine/network/port-publishing/#publishing-ports)も対応する。
5. 非proxyで公開portを省略すると、[EnsureProxylessEndpointPort:803–844](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/Dcp/DcpExecutor.cs#L803-L844)が空きportを割当てEndpoint.Portへ設定し、コンテナのTargetPortは維持する。[allocator:86–99、129–144](https://github.com/microsoft/aspire/blob/v13.5.4/src/Aspire.Hosting/Dcp/ProxylessEndpointPortAllocator.cs#L86-L144)はendpointごとに選択を保持する。OSへのprobeは一時的で、予約raceを完全排除する機構ではない。起動失敗は失敗として扱う。
6. DCPは[container_common.getHostAddressAndPortForContainerPort:457–518](https://github.com/microsoft/dcp/blob/v0.25.13/controllers/container_common.go#L457-L518)で固定specまたはDocker inspectの実mappingを取得し、Service endpointへ伝える。AppHost側は割当結果を参照し、固定host portを組み立てない。再作成でも必ず再取得し、毎回必ず違うportになるとは約束しない。

API構成案は、各標準AddContainerへ WithHttpEndpoint(port: null, targetPort: 8082, name: "http", isProxied: false) を設定し、同じ名前の WithEndpoint("http", e => e.TargetHost = "127.0.0.1") を適用する形。DebugHostはtargetPort8080、診断は現行設計の内部portへ同様に適用する。これはソースから導いた未実装例で、compile済みサンプルではない。

明示127.0.0.1にすることでhost側公開を固定する一方、サービス自身はコンテナ内部の0.0.0.0等で待受ける。GCの-address :8082とDebugHostの内部HTTP8080／HTTPS転送条件は別の実装責務。TargetHostをコンテナ内待受へ渡してloopbackだけに閉じるとDocker公開から届かない。container-to-containerは資源名／内部portと対応network contextを使い、Windows用127.0.0.1 endpointを渡さない。

以上によりG2の「当該版で設定方法が存在しない」という保留は解消できる。未実施はWindowsの実SDK／DCP版照合、Docker HostIp／割当portのinspect、他IF待受なし、GC画面／WebSocket／DebugHost InteractiveServer到達、再作成後のURL更新であり、API契約欠落とは別の#82確認条件である。Docker／DCP設定変更やhost networkなしで静的構成を表現できる。

### G3 固定版の自サービス診断と最小追加点

これは指定した受信・復号・出力・CLI・APIの有限な範囲の確認である。未読の全コードにAPIが絶対にないとは断定しない。別ソケットで同じgroupを受信する観測器の件数をサービス自身の件数へ置換しない。

| 資源 | 固定版で確認できた既存証拠 | 不足／最小追加または供給条件 |
| --- | --- | --- |
| Crane | [RobotFeedbackReceiver::onReceive／getStats](https://github.com/ibis-ssl/crane/blob/4063cd31cd5b11b1cc919003907f5f4c527b252d/crane_robot_receiver/src/robot_receiver_node.cpp#L44-L65)はvalidate後valid/invalid countと受信時刻を保持し、[ROS RobotFeedback](https://github.com/ibis-ssl/crane/blob/4063cd31cd5b11b1cc919003907f5f4c527b252d/crane_robot_receiver/src/robot_receiver_node.cpp#L373-L401)にcount／ageを公開。[WorldModelDataProvider::update](https://github.com/ibis-ssl/crane/blob/4063cd31cd5b11b1cc919003907f5f4c527b252d/crane_world_model_publisher/src/world_model_data_provider.cpp#L278-L335)はvision／trackerのparse成功後にlast receive値を更新 | feedbackは既存ROS値を資源内HTTPへ変換でき、外部receiver本体を作り直す必要はない。vision／trackerのexact count・last値の外部公開は未確認。同updateの成功分岐に件数・受信／parse時刻を追加してROS diagnostics等へ出すのが最小候補。refereeのサービス自身の受信診断は今回の読取で未確定、ROS graph存在だけでは代用不可 |
| cm4-sim | [drainCraneInput／applyDeliveredCommands](https://github.com/ibis-ssl/Orion_CM4/blob/d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a/cm4/bridge/cm4_sim.cpp#L603-L629)が正しい715Bとrobot slot適用時刻を持ち、[drainFeedback:635–645](https://github.com/ibis-ssl/Orion_CM4/blob/d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a/cm4/bridge/cm4_sim.cpp#L635-L645)はdecodeFeedbackPosition成功後の時刻を保持。終了時loop数／degrader統計はある | 起動／終了logだけで継続的な受信準備を判定できない。上記成功分岐に指令受理／feedback decode count、lastReceive、invalid、target再解決世代を加え、機械可読のperiodic stdoutまたは資源内診断APIへ出す最小変更。G1と同じfile／固定成果物でまとめられる |
| simulator | IbisCommandAdaptor::handleDatagramsはsize検査・IBIS deserialize・counter／対象robot検査を実処理に行い、IbisFeedbackAdaptorは実生成feedbackを送る。通常SSL制御はsender addressへ返信する。既存logには不正size／未対応mode等がある | 正常受理count／最後の正常受信／feedback実送出時刻の周期診断は当該固定CLIで確認できない。simulator.cppのhandleDatagrams／handleVisionData／IbisFeedbackAdaptor::handleGroundTruthとPacketSenderThreadの実送信結果に計測を追加し、39101の診断へ接続する最小候補。別のvision受信だけでsimulator制御受理を保証しない |
| TIGERs | [SSLVisionCam.run／publishData:137–185](https://github.com/TIGERs-Mannheim/Sumatra/blob/0bb4c653ef8611f9e71a880165f101209be97b3c/modules/moduli-cam/src/main/java/edu/tigers/sumatra/cam/SSLVisionCam.java#L137-L185)はparse成功後の通知とGameLog記録、[NetworkRefereeReceiver.run:104–109](https://github.com/TIGERs-Mannheim/Sumatra/blob/0bb4c653ef8611f9e71a880165f101209be97b3c/modules/moduli-referee/src/main/java/edu/tigers/sumatra/referee/source/NetworkRefereeReceiver.java#L104-L109)は正常referee通知。[Referee.onNewRefereeMessage:204–211](https://github.com/TIGERs-Mannheim/Sumatra/blob/0bb4c653ef8611f9e71a880165f101209be97b3c/modules/moduli-referee/src/main/java/edu/tigers/sumatra/referee/Referee.java#L204-L211)はnative時刻付きGameLog、[GameLogRecorder.setRecording](https://github.com/TIGERs-Mannheim/Sumatra/blob/0bb4c653ef8611f9e71a880165f101209be97b3c/modules/moduli-gamelog/src/main/java/edu/tigers/sumatra/gamelog/GameLogRecorder.java#L45-L60)がある | 既存native記録を有効にして安全にtailできるなら実サービスのdecode証拠になり得る。ただし固定headless CLI／比較fixtureだけでsetRecordingを呼び、live count/timeを資源内HTTPに出せる供給契約は今回未確定。最小候補はACam／ARefereeの既存observerへの小さな診断module追加、または上記parse成功直後のcount/time公開。比較AIや内蔵GCを有効にする方法は使わない。既存API証拠か改修moduleと新imageの提供までblocked |
| ER-Force | [CLI --record:57–70](https://github.com/robotics-erlangen/autoref/blob/da862ea353d877fbbb66dd73827543b78364dc6a/src/cli/autorefcli.cpp#L57-L70)と[gotStatus→LogFileWriter:134–146](https://github.com/robotics-erlangen/autoref/blob/da862ea353d877fbbb66dd73827543b78364dc6a/src/cli/autorefcli.cpp#L134-L146)は既存。frameworkの[Processor::handleVisionPacket:552–570](https://github.com/robotics-erlangen/framework/blob/9814d05763a426f24e5bb134ad7ae2607c64b71e/src/amun/processor/processor.cpp#L552-L570)はparse成功したframeを保持し、[world_stateのvision_frames／vision_frame_times](https://github.com/robotics-erlangen/framework/blob/9814d05763a426f24e5bb134ad7ae2607c64b71e/src/amun/processor/processor.cpp#L456-L470)へ出す。refereeは[Referee::handlePacket](https://github.com/robotics-erlangen/framework/blob/9814d05763a426f24e5bb134ad7ae2607c64b71e/src/amun/processor/referee.cpp#L49-L109)でdecode | visionはnative status logから観測できる候補で、vision診断のためだけに直ちにframework改修必須とはしない。logの実行中flush／追記読取／全frame保存条件は未確定。refereeの毎packet正常decode count／last receiveはstate遷移logだけでは得られない。framework Referee::handlePacketの成功分岐へ受信時刻とcountを加えstatusへ出し、autoref CLIの既存status経路から資源内HTTPへ渡す最小候補。生UDP Receiver::gotPacketだけではdecode成功証拠にならない |
| Game Controller | [vision.Receiver.consumeData／Time](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/vision/visionReceiver.go#L47-L69)はparse成功したdetectionのt_captureを保持。[tracker.Receiver.consumeData](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/tracker/trackerReceiver.go#L38-L48)はparse後callback、[Engine.ProcessTrackerFrame／processTrackerSources](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/engine/consume_tracker.go#L9-L59)はsourceごとの更新時刻を保持し1秒で一覧から除外。[/api/controlのpublishGcState](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/api/server.go#L122-L137)がその一覧を出す | GC自身のtracker一覧は既存APIで利用可。vision.Timeはpacket由来capture時刻で、ローカル受信時刻／countではなく、既定system modeのHTTP診断として公開も確認できない。vision／tracker consumeDataの成功分岐へaccepted count／last receive／decode errorsを追加、内部gcから限定診断HTTP39102へ公開する最小候補。tracker一覧だけや画面到達だけで全vision入力契約を満たさない |

G3の共通不足は「外部サービスの正常decodeイベントから、countとローカル受信時刻を取り出す供給口」。外部アプリに計測追加が必要なものと、既存ROS／native log／GC APIをDuck側の資源内HTTPへ適合できるものを区別した。後者のbridge adapterは製品データを中継せず、実アプリの証跡だけを返す。run/resource/full container IDとの所有対応はAppHost側でDCP状態と照合し、診断の自己申告だけを所有証明にしない。

必要な外部変更は上記の具体箇所に限定した提案であり、実施を許可されたものではない。TIGERs／ER-Force／GCの通信方式改修が一律必要とは結論しない一方、全サービスの要求された診断が設定だけで提供されるとも結論しない。

### I81-01の先行可能範囲（親採用済み、実装未開始）

- 追加判断: I81-G全体の未通過は、外部接続と無関係なDuck内の純粋契約まで止める理由にはならない。親はこの限定判断を採用した。G1／G3が阻害するのは外部データ取得・変換・接続と全体成立であり、以下のI81-01は別に先行可能。これまでの「I81-01以降を一律待機」と読める結論は、この追加判断で限定する。実装は今回まだ開始せず、親から次の明示委任を受けてから行う。
- 再適用: 前回実読したwork-context-manager／implementation-executor／implementation-workerに従い、HEAD・git status、本報告、tasks-statusのI81-01、既存のRefereeReadinessState／TrackerComparisonReadinessと対象試験、Duck.slnx、AppHost／Tracker.Testsの参照を再読した。HEADは6902b736d7934a65147624b2581c3a82e86a2e8aのまま。tasks-statusは親所有の未コミット差分があり、子は変更しない。追加外部調査・依存確認・テスト実行なし。
- 採用対象: BCLだけのDuck内部の値型／状態判定。HTTP、JSON、DCP外部型、外部サービス固有のschema、UDP／filesystem／Docker実行、実際の所有証明、外部時刻の変換、サービス全体のReadyへの接続を含めない。

| 純粋契約 | この単位で固定する規則 | 後続の責任として残すもの |
| --- | --- | --- |
| identity／世代 | 期待するrun ID、resource ID、完全container ID、Duck内で扱う世代と観測値を厳密一致で照合。IDの名前／prefix一致で受理しない。世代変更で件数基準と準備状態をリセット | 期待IDのDCPからの取得・所有確認、container/process再作成の検出、世代の発行。データの自己申告を所有証明にしない |
| 正常decodeの出所 | サービス自身の正常decode証跡、補助観測、出所不明を区別し、後二者だけでは準備可にしない。最新の失敗／欠落を成功値へ丸めない | 何が当該サービスの正常decodeイベントかを外部API／native logへ対応付けるadapter。enumや型名はDuck内部の分類で外部JSON字段ではない |
| count増分 | 同じidentity／世代／対象入力について最初の有効値を基準とし、後続の正しい新しい観測で増分を確認する。件数巻戻りは同世代での再起動成功とは扱わず拒否。正の増分を観測した時点を保持 | 各外部カウンターの意味・overflow／reset表現の変換、プロトコル別の5秒10検出等の受入規則。G3の未確定値を架空に作らない |
| freshness | 外部時刻が確証なく解釈される場合はnot-ready。内部入力として「取得時点での最終正常decodeからの経過時間の保守的上限」が既知の場合だけ評価可能にし、Duck側のTimeProviderの単調時刻でその後の経過を加算。負値／未知／期限切れを拒否。新しいpoll時刻だけで古い入力を延命しない | age上限をnative時計・取得遅延から妥当に導く処理。外部にその名の字段があるとは仮定しない。t_captureをローカル受信時刻へ読み替えず、同期保証のないUTC同士を減算しない |
| 古い応答 | Duck側が渡す取得順序を照合して重複・逆順の応答を受理しない。古いID／世代の遅着値は新しい状態を上書きできない。同じcountの再読で進捗時刻を更新しない | 実際のcollectorによる取得順序の採番、HTTP並行要求・取消・DCP通知への配線。取得順序は外部サービスが送るsequence字段ではない |
| 判定範囲 | 結果は共通診断契約に対する準備可否と理由に限る。初期値はnot-ready。正規化入力が不足する場合もnot-ready | 実際の資源health、三者のUUID／役割解決、GC状態遷移、通信成功、全体Ready、Windows受入。既存比較の役割判定を置き換えない |

count増分がない再読では新しい進捗を作らない。直前の正当な進捗を保持する場合もその元の期限を延長せず、期限を越えたらnot-readyとする。最新の同世代decode失敗や根拠不明のageは準備可の新しい根拠にならない。古い応答を拒否したこと自体で、別世代の正当な状態を破壊しない。これらの境界を対象試験で先に固定する。

#### 配置と最小候補paths

推奨は既存AppHost内への配置ではなく、既存計画にある小さな共通library。AppHostは既にRuntimeHost／DebugHostをProjectReferenceしているため、後者が共通型を再利用するためAppHostへ参照を戻すと循環する。DebugHostへの配置も画面依存を持ち込み、RefereeDriverはプロトコル固有の責任がある。Tracker.Coreの追跡アルゴリズムへ試験資源の所有契約を入れない。既存の純粋なfreshness／拒否試験の書き方は再利用できるが、既存の状態型自体はプロトコル／比較役割固有で、完全IDと世代を含む汎用契約の代用にはしない。

| 候補path | この単位での変更案 |
| --- | --- |
| Testing/Duck.Testing.Diagnostics/Duck.Testing.Diagnostics.csproj（新規） | net10.0のBCLだけのclass library。PackageReference／restore設定／依存導入を追加しない |
| Testing/Duck.Testing.Diagnostics/ResourceDiagnosticIdentity.cs（新規） | 期待・観測identityの値型と厳密比較。実所有者探索なし |
| Testing/Duck.Testing.Diagnostics/ResourceDiagnosticSnapshot.cs（新規） | 正規化済み内部の件数、出所、decode結果、age根拠、取得順序のimmutable値。外部wire schema／serializer指定なし |
| Testing/Duck.Testing.Diagnostics/ResourceReadinessState.cs（新規） | 同世代の基準・増分・期限・旧応答拒否・理由の純粋判定。TimeProviderはBCLを使用し、テスト用時計も既存xUnitと手書きの小さい実装で足りる |
| Tracker/Tracker.Tests/ResourceReadinessStateTests.cs（新規） | 下記Red→Greenの対象試験 |
| Tracker/Tracker.Tests/Tracker.Tests.csproj（既存） | 上記libraryへのProjectReferenceを1件追加。新しいNuGet packageなし |
| Duck.slnx（既存） | 既存/Testing/フォルダーへ上記libraryを登録 |

これは最大7pathの候補で、製品ファイルはまだ一つも作成・編集していない。RuntimeHost／DebugHost／AppHost／RefereeDriverの実行構成や参照はこの単位では変更せず、後続の接続単位で追加する。canonical trackingの採用は親が所有する。

#### 最小タスク案とexit条件

- I81-01の限定scopeを一単位として、対象試験を先行させる。期待identityの各要素不一致／短いID、世代変更、初回基準のみ、正しい増分、同countの再読、巻戻り、逆順／重複、未来のローカル観測、age未知／負値／期限境界、補助観測のみ、decode失敗、期限後の回復を固定する。実データ取得や外部接続を試験へ混ぜない。
- focused実行案は dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~ResourceReadinessStateTests -m:1 /nr:false --no-restore 。BCL libraryが新規なので既存assetsの状態は実装委任後に確認し、--no-restoreが成立しなければその不足を報告する。新しい依存取得・restoreを今回の計画判断で許可しない。既存RefereeReadinessStateTests／TrackerComparisonReadinessTestsは回帰対象候補とする。
- exit条件は、純粋契約と対象試験が外部サービス・Docker・ネットワークなしで完結し、上記拒否条件を満たし、依存package追加なし、runtime構成差分なし、通常レビューへ渡せること。テストはまだ実行しておらず、全体I81-G／方式B成立やWindows実機成功の判定にはしない。
- 実装開始条件は親から同じ担当への次の明示委任。G1／G3は外部adapter・資源統合の依存として保持する。今回の成果は限定scopeの採用判断と本報告追記のみ。

## 結果

- I81-Gの読取調査を完了。G2の静的設定方法は一次ソースから確定できた。G1の外部unicast／応答／再解決とG3の未提供診断はblockedとして親へ返し、後続製品実装を開始しない。
- 次に既存許可内でできることは、このreportの読取・親による契約表の採用判断と、既存固定ソースに対する限定した追加確認まで。必要な外部修正、更新image取得／build／供給、Duck製品変更、環境変更、実機試験は本許可から派生させない。今は製品編集へ進まない。
- 親が外部担当へ渡す供給要求: Craneの可変target host/portと再解決、cm4の非loopback feedback bindとout DNS再解決、simulatorのfeedback hostname再解決、それぞれの正常受理／decode診断。加えてTIGERsのnative記録起動・live診断契約、ERのlive status/referee受信診断、GCのvision/count/time公開を確認または最小追加した固定source・image digest・schema・試験証拠を受領する。外部改修が不要な既存APIを提示できた項目は、その証拠で置換可能。
- 更新したrepo内ファイルは本報告だけ。既記入／Dispatch profile／親所有事前証拠を保持。technical_head=6902b736d7934a65147624b2581c3a82e86a2e8a、base=task/aspire-docker-desktop-496、commit=not_required、push=unauthorized、ci_wait=not_required、matching_ci=null、full_local_equivalence_gate=not_started。
- verification_capability=unknown（製品検証executorの新確認は対象外）。成功したのはgh API／webの静的読取とreport保存経路で、Windows／Docker／DCP実行能力や実機受入の証拠ではない。新規build/test/lintは未実施、TDDのRed/Greenや独立レビュー判定は出していない。
- canonical trackingの親の拒否はそのまま保持。子が代行・迂回せず、Issue81↔PR84の既存の正式リンク証拠を現在状態として用いた。PR／Issue操作、commit/push、依存導入、環境変更、コンテナ起動、別担当の起動なし。
- 保存後に本報告全文、HEADとgit status／diffを読み戻して返す。すべての取得／保存コマンドは終了コード回収済み。待機中プロセス・承認待ちはない。

## リスク

- tag命名・公開workflowと実binaryは別の証拠で、特にSumatra2025のimage対応、全image digest／provenanceは今回未同定。外部sourceの新しいmain／latestへ無断置換しない。
- G2はSDK既定DCP0.25.13に対する静的保証。利用者が別DCP／別runtime設定を使う場合にはその組合せの確認が必要で、今のWindows実機で同版が動いたとは記載しない。
- sourceではmulticast参加・内部宛先・公開方法を定義できても、Windows Desktop内の複数受信、ソケットIF指定、再作成時のfreshness復帰、GC UI制御、100mm移動、15秒停止、debug attach/captureは#82で実測が必要。
- G3の不足は有限な読取範囲に基づく。GameLog／status logの実行中flush、固定entrypointからの診断機能起動、外部repo計測の実装・試験、image構成・権限は未確認。未確定項目をReady／passとして扱わない。
- 設計文書・code・tests・tasks/phasesは今回変更していない。外部変更・新digestの採用で設計差分が必要になった場合は親へ返し、現在の権限で更新しない。

## 親所有の事前証拠

- worktree /home/donabe/Duck-issue81-aspire-desktop-bridge、branch task/issue81-aspire-desktop-bridge、technical HEAD6902b736d7934a65147624b2581c3a82e86a2e8a。
- Issue https://github.com/ibis-ssl/Duck/issues/81、Draft PR https://github.com/ibis-ssl/Duck/pull/84、base task/aspire-docker-desktop-496。
- Development欄で正式リンク後、gh api graphqlのIssue81.closedByPullRequestsReferences.nodes=[84]とPR84.closingIssuesReferences.nodes=[81]を読み戻した。リンクは成立済み。既存ブランチリンクは空へ移行した。
- tasks-statusのリンク確認状態を更新する操作は、初期のtasks変更禁止を理由に自動審査で実行前拒否された。台帳は旧記載のままだが、本報告とAPIの証拠を現在状態として使う。拒否された台帳更新を別経路で行わない。
