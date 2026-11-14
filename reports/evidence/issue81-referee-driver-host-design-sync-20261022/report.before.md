# Sub-agent実行レポート

## タスク

Issue81 / PR84 の I81-03C。薄い実行入口と注入構成、明示的なEnabled・ContainerMode・内部Game Controller接続先、内部39110のGET /health/refereeを先行試験から実装する。

## sub-agentを使う理由

同じ実装担当が03A・03Bの契約と証拠を継承する。親は台帳・公開・レビューを管理する。

## 対象範囲

repo=/home/donabe/Duck-issue81-aspire-desktop-bridge、branch=task/issue81-aspire-desktop-bridge、Issue81/PR84。開始基準ed0b376bf6fd5836c611269368ccacb1bb34b837。tasks-statusのI81-03Cと既存scope報告が正本。03A・03B通常レビューは条件付き合格・必須製品指摘0。

## 対象外

後続DCP資源配線・Dockerfile/配布物・localhost公開・外部診断・実機受入・外部repo・新package導入・環境/認証/ネットワーク設定・merge/Issue close。自己申告の所有者入力やHTTP応答を実DCP所有証明としない。過去TDD欠落の免除・独立最終レビューの代替は禁止。

## Dispatch profile

Parent-owned. existing_executor=/root/multicast_investigation_fresh; original_actual_requested=gpt-6.1-sol/high; original_fork=none; task_kind=implementation; work_class=implementation; uncertainty=medium; change_radius=cross_module; criticality=high; repetition=iterative; decomposability=sequential; decomposition_policy=forbidden; parallelism_mode=single_agent; role_plan=original default role reused; applied=null; application_status=reused_existing_agent_profile; profile_observability=final_profile_hidden; verification_capability=remote_ci_only; report_persistence_mode=normal_persistence; commit=parent_owned; push=parent_owned; exact_head_ci=parent_owned; report_attestation_allowed=false.


文書同期の継続profile: task_kind=design_document_implementation; work_class=document_editing; uncertainty=low; change_radius=two_design_documents; criticality=medium; decomposability=sequential; decomposition_policy=forbidden_by_user_preference; existing_executor=/root/multicast_investigation_fresh; original_actual_requested=gpt-6.1-sol/high; applied=null; application_status=reused_existing_agent_profile; scope=approved_03C_contract_concretization; breaking_change=false; new_agent=false. parent design decision: existing two design documents need opt-in Host/route/config/payload and evidence-boundary synchronization; no architecture/placement adoption change.

## 実行コマンド

- work-context-manager / implementation-executor / implementation-worker / tdd-executor / report-output-manager / report-writerの実ファイルを読み適用。親が比較設計260行のTDDを適用済み。同一担当・元gpt-6.1-sol/high/fork none、applied:null、新agent/orchestrator再入場なし。
- PowerShell login:false/require_escalated→WSL Ubuntuの既存ユーザー経路でrepo/source/既存host fixture/SDK参照を読取。HEAD ed0b376bf6fd5836c611269368ccacb1bb34b837、branch task/issue81-aspire-desktop-bridge、Issue81/PR84。
- 既存Roslyn構文解析: /usr/lib/dotnet/dotnet /tmp/issue81-independent-spec-tests-prep-20261022/Declarations.dll /tmp/issue81-referee-driver-host-initial-red-preparation-20261022/syntax-input.json /tmp/issue81-referee-driver-host-initial-red-preparation-20261022/syntax-result.json — exit0。新C#2fileに構文errorなし。csproj3/slnx1はXML parse成功。git diff --check exit0。
- 既存source34のstrict byte guard初回assertionはcore csprojの許可済みfriend assembly追加1件だけでexit1。33件byte不変と1件の正確なmetadata挿入を別に照合して保存し、34件全不変とは主張しない。旧製品/test bodyは不変。新build/restore/test/host起動・CI操作なし。正式parser/host検証は既知proto不足によりremote_ci_only、構文解析をfunctional Redに数えない。

### 最初のHTTP単位：7件runtime Red後の最小候補（2026-10-22）

- 採用済みexact0316e326aa881fd03f65f8bb9b625bd042ee9bd1 / PR CI39846734850の745実行・738pass/7fail/error0/timeout0を根拠に最小実装。新Hostの実ビルド成功後のassertion失敗であり、compile失敗をRedとしていない。
- 既存Roslyn構文解析: /usr/lib/dotnet/dotnet /tmp/issue81-independent-spec-tests-prep-20261022/Declarations.dll /tmp/issue81-referee-driver-host-initial-green-candidate-20261022/syntax-input.json /tmp/issue81-referee-driver-host-initial-green-candidate-20261022/syntax-result.json — exit0。csproj3/slnx1 XML parse成功、git diff --check exit0。構文はsemantic compile/runtimeGreenと区別。
- Red source39のうち今回変更するHost helper以外38fileのbyte不変、固定Host11test SHA a0e22ede28ce708d4764544d1587f8284725b2c5730016f7ce252124eccf9e13を確認。新build/test/restore/host/サービス起動・CI操作なし。

### 標準構成・起動停止の組成：先行試験準備（2026-10-22）

- 既読のimplementation-executor / implementation-worker / work-context-manager / tdd-executor / report系を同一担当で継続適用。HEAD503dcab1389f4c0e477c8abc8b1b7c040bf4510a、比較設計260行TDD、tasks03C/scope91/93行に従い先行試験と最小scaffoldだけを保存。
- 既存Roslyn構文解析: /usr/lib/dotnet/dotnet /tmp/issue81-independent-spec-tests-prep-20261022/Declarations.dll /tmp/issue81-referee-driver-host-composition-red-preparation-20261022/syntax-input.json /tmp/issue81-referee-driver-host-composition-red-preparation-20261022/syntax-result.json — exit0。新C#3file、XML4件parse成功、git diff --check exit0。
- 旧source39は38byte不変と新Host csprojのOutputType Exe1行だけに分類。Host11/旧745/独立67/raceの既存test body不変。新build/restore/test/開発機Host/実サービス/CI操作なし。構文はsemantic compile・runtimeRedの代用としない。

### 標準構成・起動停止の組成：13件runtime Red後の最小候補（2026-10-22）

- 固定exact3bbb2a5f0b5a503d173a3da7f2eb4306e4f7d558 / PR CI39850142081はHost実ビルド後761件=748pass/13fail/skip0、error/timeout0。TRXで新16件13fail/3pass、旧745・独立67・race・Host11の全個別Passedとsource42のarchive/HEAD/disk一致を確認し、親が13件を有効Redとして採用した。
- 既存Roslyn構文解析: /usr/lib/dotnet/dotnet /tmp/issue81-independent-spec-tests-prep-20261022/Declarations.dll /tmp/issue81-referee-driver-host-composition-green-candidate-20261022/syntax-input.json /tmp/issue81-referee-driver-host-composition-green-candidate-20261022/syntax-result.json — exit0（C#2file）。csproj3/slnx1のXML parse成功、git diff --check exit0。他41sourceのbyte不変と固定16test SHA64a42c911f7268a29064000856cc7655eba1055506c38fabdabc830fa5c884c6を確認。
- work-context/implementation/TDD/report系の実ファイルを再読して継続適用。最初のshell読取loopはPowerShell変数展開で空pathとなりexit1、その後明示pathのPython読取exit0で解消。新build/test/restore/開発機Host/外部サービス起動・CI操作なし。構文はsemantic compileやruntimeGreenと区別する。

### 明示AutoDriveと既定観測：先行試験準備（2026-10-22）

- 既読のwork-context/implementation/TDD/report各Skillを同一担当で継続適用。HEAD1c8c8270f85a63f8f7d7f76ff2996854ff53600c。scope89-94行、比較設計260行、既存Driver/制御adapter/fixture/旧AppHostの明示team設定経路を読取し、先行試験の期待値を固定。
- 構文: /usr/lib/dotnet/dotnet /tmp/issue81-independent-spec-tests-prep-20261022/Declarations.dll /tmp/issue81-referee-driver-host-autodrive-red-preparation-20261022/syntax-input.json /tmp/issue81-referee-driver-host-autodrive-red-preparation-20261022/syntax-result.json — exit0（新C#2file）。XML4件parse成功、git diff --check exit0。既存source42はbyte不変。新build/test/restore/開発機Host/実GC/CI操作なし。

### 明示AutoDrive：8件runtime Red後の最小候補（2026-10-22）

- exact6f39d4c0b324fcdce361c1934c4f0d89b03e5370 / CI39853200976はHost実ビルド後776件=768pass/8fail/skip0、error/timeout0。新15は8失敗/7回帰成功、旧761・独立67・race・Host11/composition16の全個別PassedをTRXで確認し、親が8件を有効functionalRedとして採用した。
- 既存Roslyn構文: /usr/lib/dotnet/dotnet /tmp/issue81-independent-spec-tests-prep-20261022/Declarations.dll /tmp/issue81-referee-driver-host-autodrive-green-candidate-20261022/syntax-input.json /tmp/issue81-referee-driver-host-autodrive-green-candidate-20261022/syntax-result.json — exit0（製品C#2file）。XML4件parse成功、git diff --check exit0。他42source/固定15test不変。新build/test/restore/開発機Host/実GC/CI操作なし。

## 対象ファイル

- 新規 Testing/Duck.Testing.RefereeDriver.Host/Duck.Testing.RefereeDriver.Host.csproj: net10.0/通常SDK、Microsoft.AspNetCore.App FrameworkReferenceと既存core参照だけ。追加NuGetなし。現時点はLibraryでProgramなし。
- 新規 Testing/Duck.Testing.RefereeDriver.Host/RefereeDriverHost.cs: ConfigureHttp/MapHealthの空body宣言だけ。待受変更・経路登録・DI標準登録・worker・受信/制御behaviorを未実装に保持。SHA256 428ca9e0a1ef21ec5ab925688563f40d0ee944197a17540ba2737bf7f9501e2f。
- 新規 Tracker/Tracker.Tests/RefereeDriverHostHealthTests.cs: 11case。SHA256 a0e22ede28ce708d4764544d1587f8284725b2c5730016f7ce252124eccf9e13。実ReceiveService/parser/Diagnostics/Client/Observer/MatcherをDIへ組み、新Hostの同じ設定/登録境界へ渡す。CI隔離Kestrelの動的loopbackのみを起動するfixtureで、今は未実行。
- 既存core csprojはInternalsVisibleTo Duck.Testing.RefereeDriver.Hostの1行だけ追加し、FrameworkReference/OutputType/公開APIは変更しない。既存は.NETCoreだけのLibraryでruntimeconfigなし。新ASP.NET依存を別Hostへ隔離し旧利用条件を保持。Tracker.Tests.csprojに新Host参照、Duck.slnxに新projectを追加。旧AppHost projectと全旧test sourceは不変。
- 本報告の子5欄のみ記入。親prefix/Dispatch、親tracking差分を保護。正確な6source path/hash/diffはsource-manifest.json/source-snapshot/tracked.diff、分離根拠はruntime-isolation-proof.jsonへ保存。

### 最初のHTTP単位：7件runtime Red後の最小候補（2026-10-22）

- 製品変更はTesting/Duck.Testing.RefereeDriver.Host/RefereeDriverHost.csだけ。SHA256 680e1f964febf1c0353f263b40e1c7d16542d164ae5ca8919cc5753a80580e34。ConfigureHttp/MapHealthへ最小behaviorを追加し、関数説明/XMLコメントは日本語。
- 既存core・全旧test/固定67/race・新Host11test・csproj/solutionは不変。本報告子5欄へ履歴追記し、親prefix/Dispatch・親の未追跡RedCI証拠を保持。
- 証拠: /tmp/issue81-referee-driver-host-initial-green-candidate-20261022/。before/after、production.diff、source-manifest.json、unchanged38-source-guards.json、completeness-matrix.json、構文/XML/diffのcommand/exitを保存。

### 標準構成・起動停止の組成：先行試験準備（2026-10-22）

- 新規Testing/Duck.Testing.RefereeDriver.Host/Program.cs: RefereeDriverApplication.RunAsync(args)へ委譲する薄い入口。RunAsyncはTask.CompletedTaskのscaffoldで実Hostを起動しない。
- 新規Testing/Duck.Testing.RefereeDriver.Host/RefereeDriverApplication.cs: Buildはframeworkのbuilder.Buildだけ。標準DI/validation/health登録/workerをまだ実装しない。受信口＋明示owner、受信factory(options)/WebSocket接続(uri,token)の最小内部宣言を追加。
- 新Host csprojにOutputType Exeだけを追加。旧core/CLIのFrameworkReference・公開API/runtime条件を変更せず、HTTP依存は既存新Host内だけ。新package/install/version変更なし。
- 新規Tracker/Tracker.Tests/RefereeDriverHostCompositionTests.cs: 16case、最終SHA256 64a42c911f7268a29064000856cc7655eba1055506c38fabdabc830fa5c884c6。実coreを手登録せず本番Build/DI/startstop/HTTPを通し、通信生成/接続と時計だけを置換する。
- 本報告子5欄追記。親prefix/Dispatch・親tracking差分を保持。正確な4source path/hashはsource-manifest.json/source-snapshotへ保存。

### 標準構成・起動停止の組成：13件runtime Red後の最小候補（2026-10-22）

- 製品変更はTesting/Duck.Testing.RefereeDriver.Host/RefereeDriverApplication.csの1file。Buildの標準DI/設定検査、RunAsyncの既存Programからの委譲先、同fileのBackgroundServiceを実装。source SHA256 b3d19851c24a71c9018846d3f1f70a86bb1ffae912fcfb97863d0b2d6bbb73f4。Program.csは既存の薄い委譲を維持し、旧core csproj/runtime条件・全旧test/固定67/race・今回16test・新Host csprojを変更していない。
- 本報告の子5欄だけへ履歴追記。親prefix/Dispatch・親tracking・親の未追跡RedCI evidenceを保護。証拠: /tmp/issue81-referee-driver-host-composition-green-candidate-20261022/ のbefore/after、source-manifest.json、other41-source-guards.json、production-path-composition-matrix.json、command/exit。

### 明示AutoDriveと既定観測：先行試験準備（2026-10-22）

- 新規Tracker/Tracker.Tests/RefereeDriverHostAutoDriveTests.cs：7メソッド/15case。SHA256 fbe57142598ab59c66b78ad21631a022574c155ccb2005b185fcbd3a407a509e。標準Build/DI/workerと実Driver/既存control client/HTTPを使う組成、および注入可能JSON transportの単体境界を先行固定。
- 新規Testing/Duck.Testing.RefereeDriver.Host/RefereeDriverWebSocketTransport.cs：IGameControllerWebSocketTransportの最小内部宣言。constructorのnull検査だけ、SendJsonAsyncはTask.CompletedTask。SHA256 b641e28e6495795d36a5ad2a5e91026ddb182516a3af3ee7f9f43b82ee5ac8ac。接続/初期Output受信/送信/取消/破棄behaviorは未実装、標準Build/workerへの登録も未実装。
- 旧761/独立67/race/Host11/composition16のsourceと期待値・既存HTTP/DI/Program/旧CLI実行条件を変更しない。本報告の子5欄だけへ追記、親prefix/Dispatch/台帳を保護。

### 明示AutoDrive：8件runtime Red後の最小候補（2026-10-22）

- 製品変更はTesting/Duck.Testing.RefereeDriver.Host/RefereeDriverApplication.csとRefereeDriverWebSocketTransport.csだけ。前者SHA256 1a32cd00e4bb91f0efc7a70edf1643db0656b8cd33bf00291030f377efae9494、後者SHA256 6d183c3a1e6491381f56314e31906d1b20d560a3b089123b31eb9c99d9b200e3。関数説明/XMLコメントは日本語。
- 固定15test SHA256 fbe57142598ab59c66b78ad21631a022574c155ccb2005b185fcbd3a407a509eと他42sourceはbyte不変。既存Driver/公開controladapter/旧AppHost/CLI、HTTP/単一受信/診断/Observer/Matcherの実装は不変。本報告子5欄へ履歴追記し、親prefix/Dispatch/未追跡RedCI evidenceを保護。

## 指摘事項

- 最初のbounded unitは「明示Enabled/ContainerModeのHTTP待受選択＋GET /health/refereeの自身診断/API照合射影」。正本はtasks03C、scope91/93行、主設計158行、比較設計260行。HTTP依存は別assemblyへ隔離する親指示を反映した。
- 11caseの独立期待値をexpectations.jsonへ固定。Enabled=trueのcontainer/通常で0.0.0.0:39110/127.0.0.1:39110、Enabled未指定/falseで既存URL保持・route404。実正常2packet/API一致から200、完全owner/epoch1/source/count2/error0/元UTC/age/API要求順をJSONで確認し、API窓3秒境界200/1tick後503・再poll非延命を確認する。欠落/短owner・API不一致・無入力は503。観測write0/接続1/受信3とsocket破棄を照合する。
- 最初のpositive不足は既存matcher正常を先に確認した後のtest54行HTTP status Expected200/現404という実assertionを想定。設定2caseもURL未変更で不足、HTTP503の4caseも現404が不足となる見込み。合計7runtime不足/4既定回帰を想定するが未観測。compileerror/起動error/watchdogtimeoutはRedとしない。
- root isReady/reason、diagnostic（ServiceReceiveSnapshotのcamelCase＋identity）、api（GameControllerApiObservationのcamelCase）を本unitのHTTP形として選択。失敗理由の新しい固定語彙やGC側counterは追加せず、不一致の実matcher理由とHTTPの準備未完了/非空理由を確認する。完全IDの入力構造はDCP所有証明ではない。
- fixtureは手動で実coreをDIへ登録してproduction ConfigureHttp/MapHealthを通す。productionのDI標準登録/Program/worker全体を検証済みとはしない。全Task/HTTP/socketはfinallyまたはawait usingで回収し、5秒watchdogをテストの成功/Red根拠にしない。

### 最初のHTTP単位：7件runtime Red後の最小候補（2026-10-22）

- 採用Redの実失敗は待受2件がtest32行の期待URLに対してnull、positiveが54行ExpectedOK/ActualNotFound、HTTP503の4件が98/115/129行ExpectedServiceUnavailable/ActualNotFound。新11は7失敗/4既定回帰成功、旧734/独立67/raceはTRXで全件個別Passed。より深いJSON/期限assertionはRed時点では未到達で、候補CIでの観測が必要。
- Enabledをboolとして明示trueと読めた時だけ待受/routeを追加。ContainerMode=trueならhttp://0.0.0.0:39110、それ以外の明示有効Hostはhttp://127.0.0.1:39110。未指定/falseでは既存設定を保持しroute未登録404。内部39110を将来Windowsの動的localhost公開と同一視しない。
- handlerは既存matcher.GetCurrentと自身診断のreadonly snapshotをJSONへ射影する。isReady/reason/diagnostic/apiを返し、match.IsReadyだけで200/503を選ぶ。owner欠落・短ID・不一致・期限・無入力を既存matcherのfailclosed判断から成功へ変換しない。正常一致では同packetの診断を使い、準備未完了で診断がなければledgerの現在値を表示する。
- HTTP取得はAPI ReadAsyncやUDP Receiveを呼ばず、時計/UTC/countを更新しない。handlerに新lock/await/transport所有なし。reasonは現在のmatcher判断であり、失敗で保持値が解除された後はNoObservationとなり得る。最後の失敗要求履歴を新たに保持したとはしない。
- completeness-matrix.jsonへ要求・production path・実組成fixture・正確なRed・候補Green待ちを対応付けた。HTTP依存は新Hostだけに隔離したまま、旧coreのCLI/runtime条件は変えていない。

### 標準構成・起動停止の組成：先行試験準備（2026-10-22）

- 自然単位を「標準構成/起動停止」と「明示AutoDrive」に分けた。今回16caseは正常設定2、無効default3、不正/自己URI7、非container明示loopback1、owner欠落/短ID2、取消停止/所有破棄1。expectations.jsonへ正本・独立期待値・行・想定不足を固定。
- 正常は224.5.23.1:11003/既定または明示IPv4IF、単一service/pump、DIの同ledger、実parser/Client/Observer/Matcher→HTTP200、完全ID/世代2/count2/UTC、2回目以降のAPI要求、観測write0/制御0。無効defaultはURI/owner未指定でもUDP/GC/制御を始めずroute404。不正URIは構成エラーとして拒否し、固定loopback/自己39110をcontainer内GCと誤認しない。未指定/短ownerは復号しても503で自己申告所有証明にしない。
- 最初のpositive不足はtest32行StartAsync後の受信factory生成Expected1/Actual0。非container/owner/停止を含む6caseは同じ生成不足、不正URI7caseは106行Assert.NotNull(error)に対するnullを想定。計13runtime不足/3既定回帰は未観測の見込みで、実CI結果を優先する。
- 鮮度/UTCは同じ固定時計、待機timerはBCL実時間を使う。周期取得と停止の深い検証には5秒の回収用watchdogを置くが、そのtimeoutはfunctionalRedではない。停止はhost側取消を確認してから、finallyの試験側安全取消で残る通信Taskを回収する。無関係なallocation0条件/例外型1種類/複数購読仕様は追加しない。

### 標準構成・起動停止の組成：13件runtime Red後の最小候補（2026-10-22）

- 採用Redの実assertionはfactory生成6件のExpected1/Actual0（test32/132/149/167行）、不正URI7件の106行Assert.NotNullに対するnull。watchdogやcompile失敗をRedにしていない。候補の深いHTTP/停止assertionは親の次exact-SHA CIで観測する。
- Enabledを明示trueと読めた場合だけ構成を登録し、未指定/false/不正boolではUDP/GC/制御を開始せずhealth routeも追加しない。内部ws/wssの/api/control URIを明示必須とし、container時のloopback・既知自身名/resource名・内部診断port39110を起動前に拒否する。非containerの明示loopbackは保持する。
- 標準factoryは既存IPv4 multicast受信設定224.5.23.1:11003/任意明示IFへ委譲。ServiceReceiveDiagnosticsは完全IDの明示入力だけを使い、TimeProviderを受信/Client/Observer/Matcherへ共有する。IRefereeCommandSourceと実受信serviceは同一instance。owner欠落/短IDでも正常decode件数を保持するがAPI照合は開始せず503とする。
- BackgroundServiceは単一Runと順次matcher.ReadAsyncを所有し、内部再観測間隔は100ms（新設定キーなし）。HTTP pollは既存readonly handlerのままAPI取得やage延命をしない。API接続は読取ごとに新ClientWebSocketをhostname URIから接続し、失敗時もsocketを破棄する。観測write0、制御登録/AutoDrive behaviorは次単位へ残す。
- 受信または観測の終端で共通取消を通知し、両Taskの結果をWhenAllで回収して実受信serviceの明示ownerを破棄する。取消は正常停止へ限定して扱い、非取消faultはHostへ伝播する。await中のlockや別UDP Receiveなし。既存serviceの冪等Dispose/epoch終了を再利用し、再Stop時に所有資源を重複破棄しない。

### 明示AutoDriveと既定観測：先行試験準備（2026-10-22）

- 15caseは明示trueでHALT→NEXT→FORCE/NORMAL/既にactiveの3経路、既定/false/不正bool/Enabled無効の観測専用5、pending制御の停止1、自己URI拒否2、単一/分割初期Output後送信2、明示team JSON1、初期Output待ち取消1。expectations.jsonへ独立期待値と最初の不足assertion/行を固定。
- 想定8runtime不足/7回帰。標準DIの正制御3件は既存clientが未登録のAssert.IsType(null)、停止1件はDriver未登録のAssert.NotNull(null)。transport2/取消1は接続URI列のAssert.Single(empty)、team1は送信列Assert.Single(empty)を想定する。未実行見込みであり、compileerror・watchdogtimeoutをRedに数えない。
- 内部URIはcontrol側にも同じ値を使い、既存GameControllerWebSocketControlClientが作るNEXT_COMMAND/FORCE_START/NORMAL_STARTとUNKNOWNを独立期待値に固定。既存Driverが同じIRefereeCommandSourceを使い、単一受信・同counter診断件数・HTTP一致を保持する。制御JSONへの反応として入力を進めるfakeであり、外部GCや新UDP producerを起動しない。
- team設定の新しい自動キー/自動改名は追加しない。既存public SendYellowTeamNameAsync(crane)のYELLOW/team_name JSONと注入URIを同transportで確認し、既定観測やAutoDrive組成から暗黙のteam変更を要求しない。旧AppHostのteam設定と旧fixture/API既定は不変。
- 全fixtureは正常停止のassertion後にもfinallyで安全取消/Stop/Disposeを回収する。取消送信は完了書込みと区別し、既定観測は接続によるAPI読取が進んでも制御書込み0を固定。5秒watchdogは回収用であり、Redはその前の具体assertionを予定する。

### 明示AutoDrive：8件runtime Red後の最小候補（2026-10-22）

- 実Redは34行の既存controlclient欠落IsType(null)3件、93行Driver欠落NotNull(null)1件、129/147/165行の接続または送信列Single(empty)4件。型解決・compileerror・timeoutではない。固定fixtureの深い送信順/HTTP/停止assertionは候補CIでの実測が必要。
- Buildの既存Enabled判定内でAutoDriveToActiveをbool明示trueと読めた時だけ、既存GameControllerWebSocketControlClient/RefereeDriverFixtureと専用BackgroundServiceを登録する。既定/false/不正bool/Enabled無効は観測のまま。既存の検査済み内部URIをcontrol側へも注入し、loopback既定へfallbackしない。
- RefereeAutoDriveWorkerは既存Driver.DriveToActiveAsyncを一度実行し、観測workerと同じIRefereeCommandSourceを使う。HALT→NEXT→FORCE/NORMAL/既にactiveの判断とJSON生成は既存coreへ委譲する。正常完了後も観測は継続。保留制御にはHostのstoppingTokenを渡し、Task faultはBackgroundServiceの標準監視へ渡す。新UDP Receive/producer、await中lock、未監視Taskは追加しない。
- JSONtransportは注入URIで接続し、初期text OutputのEndOfMessageまで受信した後に一度送信する。close/非textは失敗、呼出取消は伝播し、usingで正常/例外/取消の全socketを破棄する。制御と観測は役割が別で、観測clientからの書込みは追加しない。
- 新しい改名設定や暗黙team変更なし。既存public SendYellowTeamNameAsyncのJSONを同transportで送るだけ。既存controlclientの公開既定endpoint/JSONと旧AppHostのteam設定を変更しない。受信資源の所有停止は既存ObservationWorker/serviceだけに保ち、AutoDriveworkerから再破棄しない。

## 結果

- 先行11caseと空body scaffoldを保存・source/hash/readback確認。構文exit0/XML4成功/diff-check exit0。source34は33byte不変とcore metadata1件の正確な追加を分類して保護、旧734/固定67/raceの期待値/既存source bodyを変更していない。
- 証拠: /tmp/issue81-referee-driver-host-initial-red-preparation-20261022/。expectations.json、work-context.json、runtime-isolation-proof.json、scaffold-boundary.json、source-manifest.json、existing34-guards.json、remaining03C.json、source snapshot、生command/exitを保存。
- runtime Red/Greenは未観測。予定total745は見込みで実測を優先。親が非main commit/pushしexact-SHA PR CIの実assertion不足を採用してから同じ担当へ最小behaviorを戻す。全process終端・exit回収済み。子commit/push/GitHub操作なし。

### 最初のHTTP単位：7件runtime Red後の最小候補（2026-10-22）

- 最小Host1file候補を保存・構文exit0/XML4成功/diff-check exit0と他38file/固定test不変を確認。本報告子5欄へRed→候補を追記・読戻し、親prefix/Dispatch保持。
- runtimeGreenと全745回帰は未確認で、親commit/push後のexact-SHA PR CI待ち。全process終端・exit回収済み。子commit/push/GitHub操作なし。

### 標準構成・起動停止の組成：先行試験準備（2026-10-22）

- 先行16case＋未実装scaffoldを保存・構文/XML/diffと旧sourceguardを確認し、親prefix/Dispatchを保持して子5欄を追記・読戻し。runtimeRed/Green未観測、予定total761は実測優先。
- 先行HTTP単位のexact503dcab / CI39847942308はrun全体success、745pass/0fail/試験skip0。成功artifact0/個別TRXなし、packet/full-stack jobskip。固定Host11/source39一致を保存済みで、今回の未実行16caseへ結果を転用しない。
- 証拠: /tmp/issue81-referee-driver-host-composition-red-preparation-20261022/。expectations.json/scaffold-boundary.json/remaining03C.json、source/hash、構文/XML/diff command/exit、生出力と報告readbackを保存。親commit/push/exactCIで実assertion不足を採用してから本実装する。
- 全process終端・exit回収済み。子commit/push/GitHub操作なし。

### 標準構成・起動停止の組成：13件runtime Red後の最小候補（2026-10-22）

- 最小製品1file候補と本報告子欄を保存。構文exit0/XML4成功/diff-check exit0、他41source/固定16test不変。report親prefix/Dispatchを保持して保存後読戻しする。正式検証能力はremote_ci_only、runtimeGreen/全761回帰は親commit/push後のexact-SHA PR CI待ち。自己review合格判定はしない。
- Red証拠は/tmp/issue81-referee-driver-host-composition-red-ci-20261022/ci-evidence.json（SHA256 d87ac51bcc2db11d4d72e585de62dd484c12d835fafdf5d3f2606d2efdf7e8f8）。TRX SHA2561db4812527ac35483bd2526ac8fecfd89d3d4de1d4797a808bcc0d42ccbe26bb。packet/fullstack jobskipを実機成功としない。全発行process終端・exit回収済み。子commit/push/GitHub操作なし。

### 明示AutoDriveと既定観測：先行試験準備（2026-10-22）

- 先行15case＋未実装scaffoldを保存し、構文exit0/XML4成功/diff-check exit0、既存42source不変を確認。runtime Red/Greenは未観測。予定total776と8不足/7回帰は見込みで実測優先。親commit/push/exact-SHA PR CIでruntime不足が採用されるまでAutoDrivebehaviorを実装しない。
- 直前の標準構成exact1c8c8270 / CI39851820995はrun全体success、761pass/0fail/testskip0。成功artifact0/個別TRXなし、packet/fullstack jobskip。source42固定を確認済みだが、新15caseの未実行結果へ転用しない。
- 証拠: /tmp/issue81-referee-driver-host-autodrive-red-preparation-20261022/。expectations/scaffold-boundary/work-context、source snapshot/hash、既存42guard、構文/XML/diff command/exit、report読戻しを保存。全process終端・exit回収済み。子commit/push/GitHub操作なし。

### 明示AutoDrive：8件runtime Red後の最小候補（2026-10-22）

- 最小2製品file候補を保存し、構文exit0/XML4成功/diff-check exit0と他42source/固定15test不変を確認。本報告子5欄のRed→候補を保存・読戻しする。runtimeGreen/全776回帰は親commit/push後のexact-SHA PR CI待ち。自己review合格とはしない。
- 証拠: /tmp/issue81-referee-driver-host-autodrive-green-candidate-20261022/。before/after、source-manifest.json、production-path-composition-matrix.json、other42-source-guards.json、構文/XML/diffのcommand/exitとreport読戻しを保存。Red証拠ci-evidence SHA25688a97843be99599594f9056d88cfc9839e430bc17bd916d62cb7c3f3d901e676、TRX SHA25667647049e65bfbe36d5a33a6519fa43c125950c719306f3d79c8a93bf21deb51。
- 全process終端・exit回収済み。子commit/push/GitHub操作なし。親の未追跡証拠を保持し、後続機能へ広げず候補を返す。

## リスク

HTTP内部待受と将来Windowsの動的localhost公開を区別する。DCP所有の実照合、Docker Desktop/Windows、外部サービス、実流量での照合可用性は未検証。全文文書gate不合格を保持。新規依存が必要な場合は具体的に報告し、無断導入しない。

- 新Hostは参照scaffold段階で実行可能Programなし。既存coreへASP.NET必須runtimeを追加せず、03Cの新opt-in HostだけへHTTP依存を隔離する。インストール/package/version/環境設定変更なし。構文成功はsemantic compileや実HTTP成功ではない。
- 後続はProgram/DI標準登録、Enabledによる実worker/受信の起動抑止、内部URIと誤loopback/自己宛先拒否、実ClientWebSocket接続・hostname再解決、取消/worker停止/所有資源破棄、AutoDrive既定false/明示制御、HTTP契約の文書同期。現first-unitではこれらを先行完成させない。
- AppHost/DCP配線・Dockerfile/配布物・Windows動的localhost公開・外部G3・実機は対象外。HTTP200はDriver自身のローカル証跡で、GCや比較trackerの受信診断・DCP所有証明ではない。過去TDD欠落は保持し今回の先行試験で補填しない。自己review合格判定なし。

### 最初のHTTP単位：7件runtime Red後の最小候補（2026-10-22）

- 本unitは薄いHTTP設定/射影まで。Program・標準DI登録・worker・実接続factory・AutoDrive構成は未実装の後続に保持する。未指定Enabledで実受信/workerを作らない最終組成はこのunitの空route/設定保持だけでは実証しない。
- HTTPの完全IDは構造検査済みの帰属入力であり実DCP所有証明ではない。現在判断のreasonと最後の失敗履歴を混同せず、API窓・UDP単調ageをpollで延命しない。実hostname/GC/Desktop/Windows/DCP/外部G3・実機は未検証。
- 既知全文文書gate不合格を保持。新依存/package/install/設定/環境/auth/network/外部変更・新agentなし。元Solhigh継続/applied:null。runtimeGreenや自己review合格を主張しない。

### 標準構成・起動停止の組成：先行試験準備（2026-10-22）

- Exe化は呼出可能な入口だけで、RunAsyncはno-op、標準DI/worker/validation/起動停止behaviorは未実装。fixtureは実行前で、構文成功を本番Host成立としない。
- 明示AutoDrive/controlJSON・TeamName制御は次の自然単位へ分離し、現試験は既定観測の制御0だけを固定。実BCL接続/DNS再解決の証明、Program実行取消、HTTP/構成文書同期、通常レビューも残る。通信境界を置換した隔離試験はDesktop multicast/外部GC/実DCP所有/実機成功ではない。
- AppHost/DCP配線・Dockerfile/配布物・Windows動的localhost公開・外部G3は対象外。全文文書gate既知不合格と過去TDD履歴を保持。新agent/外部/依存install/環境/auth/network変更なし、元Solhigh/fork none/applied:null。自己review判定なし。

### 標準構成・起動停止の組成：13件runtime Red後の最小候補（2026-10-22）

- この履歴は先行scaffold段階の記載を上書きせず、今回の標準構成/起動停止候補を区別する。実Runtime/HTTP組成のGreenは未確認。明示AutoDrive/既存controlJSONの構成、実Program取消の追加実行証拠、構成/HTTP文書同期と通常レビューは後続。
- 新ClientWebSocketへのhostname URI引渡しは静的実装であり、実GC/DNS再解決/実Desktop multicast/DCP所有/Windows公開の成功証拠ではない。完全IDの構造検査と自己診断をDCP実所有証明へ昇格しない。AppHost/DCP配線・Dockerfile/配布物・外部G3・実機・全文文書gate既知不合格を保持する。
- 新依存/install/設定/環境/auth/network/外部変更・新agentなし。元gpt-6.1-sol/high継続/applied:null。後続機能を先回り実装せず、親のexact-SHA CIへ候補を返す。

### 明示AutoDriveと既定観測：先行試験準備（2026-10-22）

- 今回のscaffoldは呼出可能なだけで制御を一切しない。標準DIへの既存Driver/client登録、明示AutoDrive worker、初期Output後の実送信/取消/破棄behaviorはruntimeRed採用後の後続。旧761/固定67/race/Host11/composition16のsource/期待値を保持。
- 隔離fixtureの動的loopback HTTP・fake WS/受信口は実GC/Desktop multicast/DCP所有/Windows公開の成功証拠ではない。実Programの起動取消、文書同期、通常レビュー、外部G3/配布物/実機/全文文書gate不合格を保持。新agent/依存install/環境/auth/network/外部変更なし、元Solhigh/applied:null。

### 明示AutoDrive：8件runtime Red後の最小候補（2026-10-22）

- 候補は静的/構文検査済みだが、固定AutoDrive15/既存761のruntimeGreenは未確認。制御の初期Output/Drive待機は従来どおりcaller/Host取消に従い、新しい自前期限やteam設定機能を追加していない。API観測の既存有限窓は維持する。
- 実Programの起動取消、構成/HTTP文書同期、通常レビューは後続。隔離fixtureや自己診断HTTPを実GC/Desktop multicast/DCP所有/Windows公開の成功へ広げない。配布/DCP配線・外部G3・実機・既知全文文書gate不合格を保持。
- 旧公開API/CLI実行条件、固定test/期待値を変更していない。新依存/install/設定/環境/auth/network/外部変更・新agentなし。元Solhigh/applied:null、runtimeGreenは親CI待ち。
