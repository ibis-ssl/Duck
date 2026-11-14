# Sub-agent実行レポート

## タスク

Issue #81 の承認済み方式Bを実装する。現在の委任は計画案だけ。正式PRリンク確認前は製品編集を開始しない。

## sub-agentを使う理由

利用者の指定に従い、既存の調査・設計担当へ実行を継続委任する。親は計画採用・追跡・Git・レビュー統合を所有する。

## 対象範囲

承認済み設計と現行コードから、依存順・対象ファイル・検証・完了条件を持つIssue81内の実装単位を提案する。

## 対象外

今は計画のみ。製品編集、canonical tracking編集、commit/push、PR/Issue操作、環境・依存・認証・network設定変更、実機起動、外部リポジトリ編集、nested agent起動は行わない。

## Dispatch profile

Parent-owned. selection_source=explicit user assignment; task_kind=implementation planning; work_class=judgment_heavy; uncertainty=high; change_radius=cross_system; criticality=high; decomposability=sequential_dependencies; decomposition_policy=forbidden; context_need=bounded_history. requested=gpt-6.1-sol/high from original actual spawn arguments; continuity=/root/multicast_investigation_fresh; role/default-role impact hidden; applied=null; application_status=reused_existing_agent_profile; profile_observability=final_profile_hidden. No new spawn. User explicitly requested same executor continuity and allows unobserved applied profile.

## 実行コマンド

- 実行経路: Windows PowerShell、login=false、require_escalated の exec → wsl.exe -d Ubuntu → Python／bash。作業ディレクトリは /home/donabe/Duck-issue81-aspire-desktop-bridge。WSLはソース読取と本報告の保存だけに使用した。
- git branch --show-current、git rev-parse HEAD、git status --short: branch=task/issue81-aspire-desktop-bridge、HEAD=88cb20f2b5cea46eb90bff80ba8d535639625de0。開始時は親が作成した本報告だけが未追跡で、製品差分なし。
- 最初に本報告を読み、/home/donabe/CodexSkill/skills/{work-context-manager,implementation-executor,implementation-worker,task-breakdown-planner,design-executor,report-output-manager,report-writer}/SKILL.md を実ファイルから読んだ。work-context-managerで境界を解決し、implementation-executor／implementation-workerの事前読取、task-breakdown-plannerの親採用用の限定計画案、report-output-manager／report-writerの通常報告を適用した。design-executorは設計変更の手順と境界を確認しただけで、設計編集を実施していない。development-orchestrator再入場、別担当・nested codexの起動なし。
- rootおよび配下のAGENTS.mdを探索したが存在しなかった。read_text、find、grep、sed、catで対象設計・直接依存・試験・CIを読んだ。rgは既存環境で利用不可のため導入していない。
- 前タスクの調査報告2件は /home/donabe/Duck-aspire-docker-desktop-496/reports/aspire-007d-issue80-multicast-investigation-63b1a91-20261022.md と同 aspire-007d-issue80-addcontainer-root-cause-20261022.md を既存の履歴証拠として読取のみ。新worktreeの製品ソースを別checkoutへ置き換えていない。
- 既存キャッシュ /home/donabe/.nuget/packages/aspire.hosting/13.5.4/lib/net8.0/Aspire.Hosting.xml を読取。依存取得、ライブラリ実行、DCP実行なし。
- 読取の初回bashループはPowerShellが変数を展開したためSkillパスを列挙できず、Pythonの実パス読取へ訂正した。一部のまとめ読取は存在しない任意設定ファイルにより終了値1、または出力上限になったため、必要箇所を分割して再読した。これらを検査成功とは扱わない。
- 以下の検証欄はすべて今後の実行案であり、現時点ではbuild／test／lint／container／実機検査を実行していない。CIの稼働結果も照会していない。

## 対象ファイル

### 今回保存するファイル

- reports/issue81-desktop-bridge-implementation-20261022.md の子所有欄だけ。親作成済みの導入・Dispatch profileを保持する。

### 読取対象と実装候補

- 承認済み現行設計: Tracker/Design/Testing/aspire-simulation-test-environment.md、tracker-comparison-debug-design.md。historyは経緯の読取だけ。
- AppHost: Testing/Duck.Testing.AppHost/Program.cs、Duck.Testing.AppHost.csproj、DockerContainerSpec.cs、DockerContainerWrapper.cs、DockerWrapperResourceExtensions.cs、DockerWrapperLaunchOptions.cs、DockerServiceReadiness.cs、VisionServiceReadiness.cs、TrackerPacketReadinessMonitor.cs、GameControllerReadinessMonitor.cs、RefereeReadinessState.cs、CraneProbeDiagnostics.cs、StackOwnershipLease.cs、ExclusiveControlPortPreflight.cs、CraneFixtureManifest.cs、CraneFixtureSettings.cs、Fixtures/simulation_protocol_comparison.xml。
- 実行側: Tracker/Tracker.RuntimeHost/{Program.cs,RuntimeVisionDiagnosticsHttpService.cs,RuntimeVisionReceiverService.cs,Tracker.RuntimeHost.csproj}、Tracker/Tracker.DebugHost/{Program.cs,Tracker.DebugHost.csproj,Tracking/DebugHostHealthEndpoints.cs,Tracking/TrackerComparisonReadiness.cs,Vision/VisionPacketCaptureSession.cs,Vision/VisionPacketCaptureWriter.cs,Tracking/TrackerPacketSnapshotLogWriter.cs}、Testing/Duck.Testing.RefereeDriver/{Duck.Testing.RefereeDriver.csproj,RefereeDriver.cs,RefereeDriverNetworkAdapters.cs}、TrackerConnectionLib/src/UdpTrackerReceiver.cs。
- 試験: Tracker/Tracker.Tests/{Tracker.Tests.csproj,AppHostApplicationModelTests.cs,AspireFullStackAcceptanceTests.cs,RuntimeVisionReceiverDiagnosticsTddTests.cs}と、既存のDockerContainerSpecTests、DockerContainerWrapperLifecycleTests、ExclusiveControlPortPreflightTests、RefereeDriverFixtureTests、RefereeDriverNetworkAdapterTests、RefereeReadinessStateTests、TrackerComparisonReadinessTests、DebugHostHealthEndpointTests、VisionPacketCaptureTests、比較・再生の対象試験。
- 直接依存／運用: Directory.Packages.props、.gitmodules、README.md、.github/workflows/dotnet-test.yml、tools/lint/markdown-targets.json、package.json、scripts/capture_aspire_dcp_logs.py、scripts/sanitize_aspire_artifacts.py、scripts/tests。canonical tasks／phasesは読取だけ。後掲の新規ファイル名は計画候補で、まだ存在・作成済みとは扱わない。

## 指摘事項

### 現コードに基づく境界

| 証拠 | 計画への影響 |
| --- | --- |
| AppHost Program.cs:4–6、77–100、202以降、および DockerWrapperResourceExtensions.cs:11–59 | 外部資源はExecutableResourceの旧起動管理、Duck／DebugHostはProjectResource。標準ContainerResource移行を実装する必要がある。 |
| DockerContainerSpec.cs:89–99、143–151 | --network hostを生成し、所有者照合もhostを要求する。引数だけ消して従来の所有権・後始末を残せない。DCPへ所有を移管し、二重のdocker run／stop／rmを撤去する。 |
| AppHost Program.cs:115–123、history:72 | cm4-simのout-addr／multicast-ifが127.0.0.1。履歴はCraneのfrom_string、cm4のinet_addrと固定応答、simulatorの数値QHostAddressを報告する。同じブリッジに置くだけでは別コンテナへの送信・応答を満たさない。 |
| DockerContainerWrapper.cs:84–97、VisionServiceReadiness.cs:40–45、TrackerPacketReadinessMonitor.cs:18–33、GameControllerReadinessMonitor.cs:約180 | 旧起動管理が10020／11010／11003をホストで受信し、GC APIにもlocalhostで接続する。資源モデルだけの移行ではWindows境界依存が残る。 |
| RuntimeHost Program.cs:8–11、RuntimeVisionDiagnosticsHttpService.cs:15–21 | 診断は比較モードだけでHTTP127.0.0.1:39108。コンテナ外から限定公開するには内部待受とホスト公開を分離し、baseでも自サービス受信を観測可能にする。 |
| RuntimeVisionReceiverService.cs:319–330、既存RuntimeVisionReceiverDiagnosticsTddTests | 自サービスの正常復号カウンターを再利用できる。新しい検出10件の条件ではgeometry-onlyを件数へ混入しない試験が必要。 |
| DebugHost Program.cs:約112–130、末尾healthマッピング | UIとUDP受信は同じサーバー。HTTPS転送の既定をコンテナ向けHTTP8080と整合させ、ブラウザーの双方向接続、health、Captureを同じ公開経路で扱う。 |
| RefereeDriver csproj:1–13、NetworkAdapters.cs:114–130 | 現状はライブラリでProgramなし。APIのURI注入境界は既存。実行用入口と診断を加え、既定localhostに頼らずws://game-controller:8082/api/controlを明示する。 |
| AppHostApplicationModelTests.cs:26–161、252–338 | 旧Project／Executable／host network／固定ホストhealth portを正解とする試験がある。まず新契約を失敗試験で固定し、旧期待値の単なる削除で済ませない。 |
| AspireFullStackAcceptanceTests.cs:49–66、641以降、workflowのASPIRE-NET-002とaspire-full-stack | 既存受入の観測器はホストUDPとLinux NICを利用する。旧経路の成功をWindows方式Bへ転用できない。#81で受入用の入口・証跡を整え、Windows実測は#82へ渡す。 |

### 着手前ゲート

- G0（親所有、pending）: 正式PRリンクを親が読み戻し、この計画を採用するまで製品編集を開始しない。今回Issue本文の新規外部照会はせず、親の委任と承認済み2設計を根拠とする。PR／base refは親の確定待ち。現在のtasks-statusには#80進行中等の旧状態があるが、ユーザー承認済みとの最新委任を優先し、親が整合させる。
- G1（設計、blocked）: Crane／cm4-sim／simulatorの固定版の送信先・応答先・待受・インターフェースと再作成追従を確定する。既存情報は数値IP／loopback依存を示すため、「DNS名を設定するだけで可能」と確定できない。Duckの3サブモジュールに対象外部実装はなく、現在の固定イメージ内部ソースを今回再検証していない。変更が不要とも、3外部repoすべての改修が必須とも断定しない。ただし現資料通りなら、少なくとも外部側で名前解決・再接続と応答先設定の改修が必要で、Duckだけの設定変更では完結しない。所有repo／固定commit／file・symbol／新CLI・設定／更新image digest／再作成条件を親が確定し、必要な外部変更を別途承認・提供するまで該当実装を止める。起動時に一度解決した数値IP、固定IP、中継、単一コンテナ化へ置換しない。
- G2（設計、blocked）: Aspire13.5.4のendpoint→DCP→Docker公開までの127.0.0.1限定・動的ポート契約を確定する。既存Aspire.Hosting.xml:3640–3680ではTargetPortはコンテナ内ポート、TargetHostは資源の待受先、IsProxiedはDCP管理の指定と読める。しかしTargetHost=localhostだけでDocker HostIp=127.0.0.1を保証する証拠にはならない。EndpointReferenceの同xml:4078–4089もコンテナ内参照にはコンテナのnetwork contextが必要と明記する。正確な13.5.4ソースの設定→DCP仕様と、Windows側の実行DCP版・Docker bindingを対応させる証跡が未提供。今回外部調査・起動をせず、API名を推測して実装しない。標準AddContainerで限定できない場合は#80へ判断を戻し、全IF公開や別起動管理へ逃げない。静的契約の確定と後日のWindows実測は別々に残す。
- G3（G1と一緒に確定、blocked）: 外部サービス自身の入力受信／復号／時刻の取得方法と資源内HTTP診断口を確定する。別ソケットの観測器を同じコンテナへ移すだけではTIGERs／ER-Force／GC自身の入力を証明できない。既存wrapperは独立受信、process／socket／ROS検査を使い、この要求を満たす外部アプリ内診断は読取範囲で確認できない。固定外部版の既存診断を利用できるか、外部側計測追加が必要かを記録し、後者なら外部変更の承認と提供が先。診断hostを重ねた派生イメージで実装できる部分と外部サービスの改修が必要な部分を分ける。
- G1～G3は実機試験不足だけではなく、コード化する契約・依存物の不足である。現資料からゲート通過を認定しない。正式PRだけできても未確定契約へ進まない。外部改修・依存取得・環境変更の権限を本計画から派生させない。

### 親採用用の順序付き実装単位

小=単一境界の変更、中=複数ファイルだが一つの契約、大=さらに分割が必要。以下はすべて中以下とし、各単位内で失敗試験→最小実装→対象試験→通常レビューの順とする。別Issue作成やcanonical追跡への採用は親が所有する。

| ID / size | 依存 | 対象ファイル候補 | 具体変更 | focused検証案 / exit条件 |
| --- | --- | --- | --- | --- |
| I81-G / 中 | G0、親の設計判断 | 本報告の契約表。設計差分が必要なら親が対象2設計を承認して更新。外部fileはG1で確定するまで未指定 | G1～G3の各行について固定版、API／設定、接続先、再作成、実際の診断根拠を埋め、変更不要／Duck内／外部変更を決定 | ソースと設定の対応表を親が採用し、外部変更があれば供給digestを受領。静的確定後もWindows実測は未実施と明記。ゲートが閉じなければ後続製品実装停止 |
| I81-01 / 中 | I81-G | 新規候補 Testing/Duck.Testing.Diagnostics/{ResourceDiagnosticSnapshot.cs,ResourceReadinessState.cs,Duck.Testing.Diagnostics.csproj}、Tracker/Tracker.Tests/ResourceReadinessStateTests.cs、必要なcsproj参照 | 共通の実行ID／資源ID／コンテナ完全ID／起動世代／対象プロセス／受信件数／最終受信時刻／復号失敗／準備状態の契約を作る。外部サービスの実際の計測と補助観測を識別し、後者だけではreadyにしない。HTTP JSON応答とAppHost側でDCP所有IDを照合する境界を定義。自身の環境変数によるID自己申告だけを所有証明にしない | 新規ResourceReadinessStateTestsを先行。fake time／診断入力で期限切れ、ID変更、欠落、decode失敗、旧世代の応答を拒否。HTTP契約の状態値が同じ判定へ対応し、純粋試験で通る |
| I81-02 / 中 | I81-01 | RuntimeHost/{Program.cs,RuntimeVisionDiagnosticsHttpService.cs,RuntimeVisionReceiverService.cs}、DebugHost/{Program.cs,Tracking/DebugHostHealthEndpoints.cs}、新規診断HTTP試験と既存RuntimeVisionReceiverDiagnosticsTddTests／DebugHostHealthEndpointTests | RuntimeHostをbase/comparison両方で39108から内部IFへ診断公開。実際の受信経路から検出件数・freshnessを更新。DebugHost内部HTTP8080、health2種、HTTPとInteractiveServerに適合するHTTPS転送条件を構成限定で設定。既存単独起動契約を守り、コンテナ用設定を明示 | geometry-only／破損／停止／古い入力は検出10件の根拠にしない。内部HTTPとlocalhostホスト公開は別試験。DebugHostのthree-role health、UIの双方向要求がHTTPSへ誤転送されない。実ネットワーク受入はまだ主張しない |
| I81-03 / 中 | I81-01、G1/G3供給物 | Testing/Duck.Testing.RefereeDriver/{Duck.Testing.RefereeDriver.csproj,Program.cs(新規),RefereeDriverNetworkAdapters.cs,RefereeDriver.cs}、資源内診断実行入口の新規候補、GameControllerReadinessMonitor／RefereeReadinessStateの再利用部分、RefereeDriver*Tests | RefereeDriverを既存公開型を保った実行可能アプリにし、内部GC URIとコンテナIFを注入。11003観測・API状態照合・診断39110を同じ資源で動かす。自動Driveは明示選択時だけとしWindowsの手動UI操作を奪わない。外部資源内の診断はG3で決めた実サービス計測から各内部39101–39106へ出す | fake UDP／WebSocket／時刻でHALT→進行、API断・受信断・再接続・取消を固定。11003送信機能なし。外部processが生存／独立観測だけではreadyにならない |
| I81-04 / 中 | I81-02、I81-03 | 新規候補 Testing/Containers/{runtime-host.Dockerfile,debug-host.Dockerfile,referee-driver.Dockerfile,resource-diagnostics.Dockerfile}、必要なcontainer build manifest、既存csproj、Duck.slnxは必要時のみ | net10.0のLinux配布、依存・appsettings・DebugHost静的資産を含め、固定base digestと成果物digestを記録。外部は固定image由来の派生に診断を同梱し、外部本体をDuckへ複製しない。Debug用は対応PDB／ソース情報／事前取得vsdbgを含む専用target。実行中インストールなし | publish／image構成試験案で入口、権限、診断、static assets、SIGTERM、digestを確認。実image build/smokeは別途許可後。実行環境にないbase／vsdbgは勝手に取得しない。成果物供給契約が確定するまで公開しない |
| I81-05 / 中 | I81-04、G2 | AppHost/{Program.cs,Duck.Testing.AppHost.csproj,BridgeResourceExtensions.cs(新規候補),BridgeImageCatalog.cs(新規候補)}、AppHostApplicationModelTests.cs、Fixtures/simulation_protocol_comparison.xml | 全選択資源を標準AddContainerでDCP既定ブリッジへ登録。DuckもContainerResource。base/comparison／cm4選択を維持し、RefereeDriverは明示選択。確定済みの外部宛先再解決設定を使用。GC8082／Debug8080／診断口だけlocalhost動的TCP、UDP公開なし。GCのvision/tracker入力、唯一11003 producerを維持。WaitForStartとhealthを分け、simulator／受信者の循環なし | AppHostApplicationModelTestsで旧構成を拒否するRedから開始。全UDP資源=ContainerResource、同じDCP default network、no host network/no UDP publication/no docker-wrapper、三group/port、GC唯一producer、TIGERs AIなし、選択依存、内部GC URI、host/container参照context、動的portとG2設定を検査。model試験だけで実HostIpを合格にしない |
| I81-06 / 中 | I81-05 | AppHostのBridgeResourceExtensions、既存3 readiness monitorの移管先、AppHostApplicationModelTests、TrackerComparisonReadinessTests、TrackerComparisonSourceResolverTests、TrackerComparisonSourceTddTests、DebugHostHealthEndpointTests | DCP資源から割当HTTP接続先を解決して診断取得、再作成時の新ID・endpointへ更新。ホストUDP受信を除去。三者の役割／UUID優先／同じUUID集約／衝突／期限を維持。比較追跡器欠落でbase全体を停止せず比較だけnot-ready。再作成で古いendpoint／古い診断を無効化 | fake DCP snapshot／HTTP／時計で新旧ID混在、endpoint再割当、三者の新しさ、同じUUIDの複数endpoint、異役割衝突、比較片側停止を先行試験。WindowsにUDP受信コードが起動されない。依存循環・自動再起動の虚偽保証なし |
| I81-07 / 中 | I81-06 | AppHost/{StackOwnershipLease.cs,ExclusiveControlPortPreflight.cs,BridgeResourceExtensions.cs,OwnedResourceSnapshot.cs(新規候補)}、DockerContainerWrapper.cs／Spec／LaunchOptions／WrapperResourceExtensionsの削除候補、DockerCliProcess.cs・DockerServiceReadiness.csの残存用途、対応旧試験、scripts/capture_aspire_dcp_logs.py／sanitize_aspire_artifacts.pyは変更必要時 | DCPへlifecycle/logを一本化し旧run/stop/rmとwrapper入口を撤去。Windows単一AppHostロック維持、コンテナ内制御portをWindowsにbindしてpreflightしない。所有ID照合と診断は分離。取消／途中失敗／二重起動／個別再作成／停止の証跡をDCP管理から保存。読取inspectやdebug execの必要CLIとコンテナlifecycleを区別。別所有者とcaptureを保護 | fake資源通知・取消で未所有IDを触らない、開始失敗と旧ID競合、後始末時の新旧ID判定を試験。旧wrapper lifecycle試験を新所有契約試験へ置換し単に除外しない。15秒停止は#82実測、単体で保証したとは言わない |
| I81-08 / 中 | I81-07 | AppHostの新規CaptureMountSettings／DebugAttachTargetResolver候補、DebugHostのcapture/replay必要箇所、既存VisionPacketCaptureTests／TrackerReplayIntegrationTddTests／CaptureOn契約試験、README.md、DebugHost/README.md | 実行ID別Windows保存先を作り/data/capturesへbind、CaptureOff後の全sidecar保持と取得、read-only /data/replay設定を追加。DCPの完全ID／image／PID照合からdocker.exe --context desktop-linux exec -i <ID> /vsdbg/vsdbg --interpreter=vscodeの手順・対応sourceFileMapを出す。自動attach／TCP debugger公開なし | path検証・所有する作成先だけ・mount read-only・再作成ID更新・記録保持をfake filesystem／snapshotでTDD。三source metadataとselected tickの再生回帰試験。実vsdbg接続とWindows bind権限／ブラウザーCapture操作は#82未実施と記録 |
| I81-09 / 中 | I81-08 | .github/workflows/dotnet-test.yml、Tracker.Tests.csproj、受入入口の分離が必要ならAspireFullStackAcceptanceTests.csのharness境界、scripts/tests、README.md、両現行設計の必要最小同期、本報告 | 新branchで通常unit/model検査を実行するCIへ整合。旧Linux host-networkジョブは旧証拠として区別し、新方式の受入へ無条件流用しない。Windows側はHTTP診断照会と内部観測資源を利用する受入入口・artifact契約を#82へ渡す。#81内で受入入口変更が必要か親が採用時に確定。Windows runner構築・実機runは別途許可が必要 | unit/model全体、sanitizer Python試験、文書対象検査を計画。JUnit相当のTRX／stdout/stderr／diag／binlog／exit-code／source SHAとimage/DCP/ID manifestを保存。#81完了は移行コードと回帰検査・通常/独立レビューの証拠、#82完了はWindows NET/HOST/DEBUG/CAPTURE受入と分離する |

I81-05～07は同じProgramを順に変更するが、一単位ずつ契約と検証を閉じる。各中サイズで対象が拡大した場合は親へ再分割を返し、大きい一括変更にしない。外部APIが未確定のまま新規Diagnostics launcherやDockerfileを実装しない。既存比較UI／物体差分モデルの新機能、TIGERs対Craneの対戦、他OSの対応追加はこの計画に含めない。

### 検証コマンド案（未実行）

各unitの新規テスト名は上表の候補で、採用時に確定する。すべてrepo rootから実行する案。失敗時には終了値だけでなく下記ログを保存する。

- 各TDDの先行失敗／最小実装後に dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~<対象testclass> -m:1 /nr:false --logger trx --results-directory artifacts/issue81/<unit> --diag artifacts/issue81/<unit>/vstest.log -bl:artifacts/issue81/<unit>/test.binlog 。stdout/stderrとexit-codeも別ファイルへ保存。SDKと依存が既存・利用可能かは実行許可後に確認し、勝手にrestore/installしない。
- 全体回帰案は dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false --logger trx --results-directory artifacts/issue81/regression --diag artifacts/issue81/regression/vstest.log -bl:artifacts/issue81/regression/test.binlog 。既存RunAspireFullStackAcceptanceは既定falseで実機試験を混入させない。
- AppHost／配布案は dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj -m:1 /nr:false と、各実行projectの dotnet publish <project> -c Debug -r linux-x64 --self-contained false --no-restore 。SDK／RID／image基盤の確定後に実行内容を再確認。container build/smokeコマンドはG1/G3の成果物とbase digestが未確定のため今は捏造しない。
- 診断保護の回帰案は python3 -m unittest discover -s scripts/tests -p 'test_*.py' -v 。
- 文書を実際に変更する段階では node tools/lint/scripts/run-markdown-targets.js text --files <実変更文書>、spell --files <同対象>、Sudachiの同対象を検査し、Yomiyasu strictをbaselineと比較。必要な全体gateは npm run lint:md 。reportsは既存targetsで除外される。本報告のために除外・辞書を変更せず、今回はユーザー指示により検査自体を実行しない。
- 現在のCI通常testはUbuntu、SDK10.0.x、submodule recursive、TRX/stdout/stderr/diag/binlog/失敗時source artifactを保持する。新branchはpull_requestで通常test対象だが、aspire-full-stackは既存branch allowlist／workflow_dispatchで、ASPIRE-NET-002はworkflow_dispatchのみ。現在HEADのrun成功／Windows受入を示す証拠は取得していない。
- #82へ渡す実測条件: Windows AppHost＋利用者Docker Desktop、全所有IDとネットワーク一致、Duck自身5秒10検出、同窓10020、三者11010の独立identity/freshness、GCだけ11003、Windows UI→API→内部11003の遷移、100mm移動、再作成時の名前再解決・応答更新、localhost以外待受なし、正常停止15秒、所有者限定後始末、debug attach、capture取り出し／停止後read-only再生。Linux／WSLの成功を代用しない。

## 結果

- mode=initial_implementation_planning、report_type=implementation_report、phase=normal_persistence。親の採用用の計画案を作成し、製品実装を開始していない。正式Issueは https://github.com/ibis-ssl/Duck/issues/81 。親はIssue81のlinked branch作成とIssue側読戻しを完了したと通知。正式PRは親が計画コミット後に作成する予定で、現時点では未確認、base_refは親確定待ち。
- technical_head=88cb20f2b5cea46eb90bff80ba8d535639625de0、branch=task/issue81-aspire-desktop-bridge、execution_environment=Windows PowerShell→既存WSL Ubuntu、cwd=/home/donabe/Duck-issue81-aspire-desktop-bridge、ownership=current_task、source_state=dirty（親作成の本報告だけ未追跡）。製品差分なし。sourceの検証証拠は作っていない。
- verification_capability=unknown。ファイル読取・Python保存経路とAspire13.5.4のローカル依存文書は利用できたが、新worktreeの.NET実行・全依存・Windows／Docker／DCP能力は本計画では未確認。以前の別worktreeの検査実績を新HEADのGreenへ流用しない。
- commit=not_required（計画段階、未許可）、push=unauthorized、ci_wait=not_required、full_local_equivalence_gate=not_started、matching_ci=null。新しいbuild/test/lintを一つも実行していない。検証コマンドは上表の案だけ。
- 次は親による計画採用・計画コミット・正式PR作成／読戻し。先行単位I81-Gは親採用時の81-01相当で、G1～G3の新規契約調査・確定はその後続作業として扱い、今回へ追加しない。外部変更が必要なら親へ阻害として返し、その承認と成果物供給を待つ。canonical tasks／phasesの編集、レビュー依頼、Issue／PR操作、commit／push／mergeなし。
- 保存先は既存 reports/issue81-desktop-bridge-implementation-20261022.md 。既記入・Dispatch profileを保存前後で照合し、子所有欄だけを更新する。保存後に本文とgit status／差分を読み戻し、実行プロセスが終端した状態で親へ返す。

## リスク

- 現時点の最重要阻害はG1～G3。外部版の契約とlocalhost bind方法が未確定のまま標準AddContainerへ置き換えるだけでは、承認済み方式Bを満たさない。
- 外部repo編集、固定版変更、image取得／build／配布、vsdbg取得・使用条件、Docker Desktop／DCP起動とWindows権限は今回の許可に含まれない。後続の供給・確認作業は親が実行許可と責任範囲を明示する必要がある。
- DCP内部ラベル名／コンテナID通知API、診断processの配置、外部サービスから取る実受信値、動的endpointの再割当通知は正確な13.5.4契約の確定が必要。自動restartや古いIP更新を未実装のまま保証しない。
- 共通診断libraryやlauncherの名前は候補。外部アプリの正常復号を別観測で代替できないため、G3の結論によってI81-03/04のfilesは変わり得る。変更範囲が拡大したら親が再採用する。
- Linuxのunit/model試験でDCPモデルの論理は検査できても、Windowsのbind、マルチキャスト複数受信、デバッガー、capture mount権限、15秒停止は別の実測が必要。#81実装完了を#82受入完了へ読み替えない。
- Mermaidの外部送信拒否は前タスクの履歴のまま。本計画で図を編集・送信・再描画していない。描画延期はテキスト計画の提出を妨げない。
