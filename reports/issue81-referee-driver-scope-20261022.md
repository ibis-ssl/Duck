# Sub-agent実行レポート

## タスク

I81-03 RefereeDriverの実行入口・内部GC URI・11003観測・資源内診断口について、承認済み設計と現在のDuckから実装可能な独立範囲を確定する。外部診断契約待ちを全Duck作業の一律停止へ広げない。

## sub-agentを使う理由

既存実装担当が依存と現在の実装を直接確認し、親が台帳に採用する実行可能な案を返す。

## 対象範囲

Issue81/PR84、branch task/issue81-aspire-desktop-bridge、HEAD234067d3cc3027a20f3794a7ce8fea75f26220ca。I81-03の依存・公開/内部契約・対象path・TDDの実行順/必要なfunctional Red・検証経路・完了条件を読み取りで確定する。

## 対象外

この準備段階では製品/test編集・実サービス/実機起動・外部repo編集・依存/環境/認証/network設定変更・commit/push・新Issue/PR・merge・Issue closeなし。後続I81-04以降の実装へ範囲を広げない。

親採用: scope draftの03A→03B→03Cをtasks-status/current Issue81 phaseへ反映。03CのHTTP route/status等の詳細は同単位で文書同期する。今回は03Aのtest/scaffold→実functional Redだけを先行し、Red回収後に同担当へ実装を戻す。phases同期は初回未実行拒否の後、上位親が初期制限を明示撤回した新根拠で同操作を一度再審査しexit0となった。旧拒否を現在の停止条件にはしない。

## Dispatch profile

Parent-owned. task_kind=investigation_and_implementation_planning; work_class=judgment_heavy; uncertainty=high; change_radius=cross_module; criticality=high; repetition=single; decomposability=sequential_dependencies; decomposition_policy=forbidden; context_need=bounded_history. selection_source=existing_executor_continuity_and_user_override. executor=/root/multicast_investigation_fresh; original actual requested=gpt-6.1-sol/high; applied=null; application_status=reused_existing_agent_profile; profile_observability=final_profile_hidden; original_fork=none; normal_persistence. Parent owns canonical tasks/phases adoption. User 11:54 permits needed Duck-wide reads for parent/implementer/reviewer, independent test author remains implementation-blind. Past TDD deviation is recorded and is not a new approval gate; new selected implementation must follow governing TDD.

## 実行コマンド

- 既読work-context-manager / implementation-executor / implementation-worker / tdd-executor / report-output-manager / report-writerを同じ担当で継続適用。orchestrator再入場・nested agentなし。requested元Sol/high、applied=nullは親Dispatchどおり保持。
- PowerShell login=false / require_escalated → wsl.exe -d Ubuntuの利用者経路でgit rev-parse / status / ls-files / grepとPythonによる対象本文・行番号を読取。cwd=/home/donabe/Duck-issue81-aspire-desktop-bridge、HEAD=234067d3cc3027a20f3794a7ce8fea75f26220ca、branch=task/issue81-aspire-desktop-bridge。開始時は親tasks変更と本report未追跡だけ。root AGENTS.mdは既確認のとおり不存在。
- 既存調査が固定したGC8050f232の既存制御APIだけを追加確認。既存 `/mnt/c/Program Files/GitHub CLI/gh.exe api --method GET repos/RoboCup-SSL/ssl-game-controller/contents/<path>?ref=8050f232c3130323bbd91d1d3d56e9553506c8e4` をserver.go / ssl_gc_api.proto / ssl_gc_state.protoの下記3pathへ各1回。全exit 0、Git blob SHA1照合。外部改修・image取得ではない。
- 読取source/hash/固定URL/GET終了値/能力の証拠を `/tmp/issue81-referee-driver-scope-20261022/read-manifest.json` へ保存。SHA256 `affef7c596ab12d8807971611e39b8c6afdd3b3dc401911d332e37132ccc065d`。この段階ではbuild/test/restore/lint/host/container/DCP起動を一つも実行せず、CI照会/待機もしない。
- 保存後に本report全文とgit status、自己所有reportのdiff-checkを読み戻す。canonical tasks/phases・他reportは変更しない。

## 対象ファイル

### 確認した現在の境界

| 現在path / API | 現在できること / 不足 |
| --- | --- |
| Testing/Duck.Testing.RefereeDriver/Duck.Testing.RefereeDriver.csproj:1–11 | net10.0のlibrary。SslProto参照だけでOutputType/Program/HTTP/常駐サービスはない。 |
| RefereeDriver.cs:15–26,42–85 | IRefereeCommandSourceとIGameControllerControlClientを使い、HALT→NEXT_COMMAND→必要なFORCE_START/NORMAL_STARTを駆動。自動処理の呼出者・期限は別途必要。 |
| RefereeDriverNetworkAdapters.cs:13–57 | UdpRefereeCommandSourceはWaitForChange呼出し中だけ受信し、counter不変packetを捨てる。常駐診断とは異なる。既存注入receiverの署名を維持する。 |
| 同:59–105 | IPv4/reuse/Any bindと任意IFのgroup join、11003受信のみ。現在byte列だけを返しremote endpoint/受信時計/epochを供給しない。 |
| 同:114–179 | 既存control clientはendpoint注入済み。既定127.0.0.1と既存JSON形式を保ち、container構成が内部URIを明示供給できる。 |
| 同:182–225 | WebSocket初期Textを一つ読むだけでOutputの内容を検査しない。送信完了やOpenをAPI状態一致の根拠にできない。 |
| AppHost/GameControllerReadinessMonitor.cs:44–81,130–208,241–308 | 旧Windows側UDP観測、loopback API固定、team名設定と接続確認。API matchState比較を実装していない。DriverからAppHost参照すると既存逆参照と循環するため取り込まない。 |
| AppHost/RefereeReadinessState.cs:62–103 | HALT初期確認、known command/team名、UTC3秒の旧判定。既存通常起動契約をそのまま保ち、I81-03で移管/削除しない。 |
| Diagnostics/ServiceReceiveDiagnostics.cs:29–56,60–119,156–165 | 実受信後・parse前の単調時計、同epochの成功/失敗、ID未供給not-ready、旧callback拒否を再利用可能。受信ownerのIDとGC producerの識別は別。 |
| Tracker.Tests/RefereeDriverFixtureTests.cs / RefereeDriverNetworkAdapterTests.cs / RefereeReadinessStateTests.cs | HALT制御、JSON送信、counter不変除外、旧health/retry等の回帰を保持。新常駐・API状態照合・39110組成の証拠にはまだならない。 |

### 最小変更候補（未編集）

- 現projectのcsproj: OutputType=Exe、既存共通DiagnosticsへのProjectReference、HTTP/hosting用Microsoft.AspNetCore.App FrameworkReference。既存SDK/shared framework内で構成し新NuGet packageを増やさない。
- 新規候補: Program.cs（薄い入口）、RefereeDriverApplication.cs（options/DI/health組成）、RefereeReceiveService.cs（単一常駐受信とIRefereeCommandSourceへの分配）、GameControllerStateClient.cs（既存APIの状態読取・正規化）。細かなrecord/純粋状態はこれらへ同居させ、不要なprojectや共通libraryの改造を増やさない。
- 既存RefereeDriverNetworkAdapters.csの受信transportを最小再利用。raw receive直後の時計/endpointを運ぶ新内部seamを加える候補。既存public receiver/driver/control型とdefault/constructor互換を維持。
- 新test候補: RefereeReceiveDiagnosticsTests.cs、GameControllerStateClientTests.cs、RefereeDriverCompositionTests.cs。既存上記3test群を必ず再実行。Duck.slnx追加は同じ既存projectなので通常不要。
- AppHost Program/旧monitor移管、Dockerfile、AddContainer/DCP所有取得・endpoint再割当配線はI81-04以降へ保持し、本unitで編集しない。

## 指摘事項

### 承認設計と既存API/schema

- 主設計27/81/86/158/183/210–212/235行: RefereeDriver内に11003受信・内部GC API・39110診断を置き、WindowsからUDPを採らず、APIとUDPの同じ遷移を確認する。比較設計31/40/42/251/256/260/275行: 全UDP参加者は将来同じDCP bridge、GC唯一producer、所有照合と自身診断を分離、TDD必須。
- [固定GC server.go:73–115](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/internal/app/api/server.go#L73-L115): 初期にgcState/config/matchState/protocolを別Outputで送る。最初のTextがmatchStateとは限らず、既存clientの捨て読みでは状態を確認できない。publishStateは同じ値を再送しない。
- [固定API proto:11–19](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/proto/api/ssl_gc_api.proto#L11-L19): Output.match_stateは既存公開API。新たなGC診断口の追加を待たずDuck側readerを作れる。serverはprotojsonを使う。readerはmatchStateを待ち、gcState/config/protocolを状態成功に数えない。
- [固定State proto:28–61,105–124](https://github.com/RoboCup-SSL/ssl-game-controller/blob/8050f232c3130323bbd91d1d3d56e9553506c8e4/proto/state/ssl_gc_state.proto#L28-L61): State.commandのtype/for_teamとgame_stateを持つ。Command.HALTはAPI側1で、UDP Referee.Commandの整数へcastできない。まずHALT/STOPとRUNNING（UDP NORMAL_START/FORCE_START）の明示対応を固定し、未対応/不明値はnot-ready。StateにはUDP command_counterがなく、counterの一致を推測しない。
- APIは期限付きの新規接続から初期CurrentStateを読み、接続/取得の開始と完了を単調時計で記録する案。pollで古いAPI snapshotやUDP受信時刻を延命しない。API読取開始時のUDP sequence/epochを保持し、後から到着した旧応答が新しい入力/再接続世代を上書きしない。HALT→RUNNINGの双方を有限窓内の新しい観測として照合し、atomicなcross-protocol counter同期を主張しない。
- 現SslProtoはgitlink9b0765fe由来の全proto生成で、GC imageの固定source8050f232とは同一revisionではない。新API readerは確認した8050契約へ明示対応するSystem.Text.Json正規化案を優先し、ローカル生成型が同じという未確認前提を置かない。既存Referee wire parser/型はSslProtoを再利用。

### 独立実装できる範囲と実際の阻害

| 経路 | 判定 / 理由 / 完了の限界 |
| --- | --- |
| RefereeDriver自身の11003正常decode・count/time・39110 | Duck内で先行可。既存parserと共通診断を使える。これはDriver自身のServiceDecodeでありGCやTIGERsが受けた証拠ではない。 |
| 内部ws://game-controller:8082/api/controlと既存制御JSON/公開matchState読取 | Duck内で先行可。既存endpoint注入と固定公開APIがある。外部3repo改修とは独立。実image相互接続は未実施。 |
| GC自身のvision/tracker count/last receive/decode errors→39102 | G3供給待ち。既存GC tracker一覧やDriverの11003観測で代用不可。G3 report107/109/157行の既存API証拠または外部計測・固定成果物/schemaが必要。I81-03のDriver healthへ捏造して混ぜない。 |
| Crane/cm4/simulatorのunicast/feedback再解決・100mm移動 | G1の外部3repo変更/成果物許可待ち。I81-03のDuck内reader/入口を止めないが、NET-007全体成立は未認定。 |
| 他外部資源の39101–39106、自身復号diagnostic adapter | 当該G3供給待ち。Driverへの同居観測だけで外部ServiceDecodeを名乗らない。追加実装/新Issue作成なし。 |
| DCP完全ID/世代の取得・bridge配置・Windows公開 | I81-05/06等の後続接続。今回は明示供給値を受け、欠落/短ID/不明ageは503/not-ready。自己申告JSONやUDP source endpointを所有証明にしない。 |
| Windows画面→API→内部UDP / 実再作成・IF・複数受信 | #82実測。unit/CIやWSL成功で代用しない。 |

拒否された/保留approvalの操作はこのtaskにない。blockedは上表の具体的供給/後続経路であり、Duck内作業全体の停止条件ではない。

## 結果

### 親が台帳へ採用できる実装scope draft

| 単位 / size / 依存 | 入力→出力 / 具体処理・失敗時 | 完了条件 / 必要なfocused test |
| --- | --- | --- |
| I81-03A / 中 / I81-01・02 | receiver options(group224.5.23.1/port11003/任意IPv4)、raw datagram/endpoint、TimeProvider、明示owner ID→自身のServiceReceiveSnapshotとcommand snapshot。単一常駐loopがparse前stampを採り、正常command/counterのpacketを毎回countする。同counterも新しい受信として記録し、command change待機だけが重複を抑制。診断とDriveで並列Receiveしてpacketを奪い合わない。decode失敗/意味欠落/不明command/旧epoch/cancelはreadyにしない。 | fake raw transport＋実Referee parser＋ManualTimeProviderで不変counter連続count、change待機、parse時間込みage、無受信/破損/未知/ID欠落/旧callback/再起動/取消を固定。所有IDとproducer endpointを分離。これでGC自身のG3準備完了とはしない。 |
| I81-03B / 中 / 03A・上記固定API | 内部GC URI、期限付きWS Output、最新UDP snapshot/sequence/epoch→正規化API状態＋UDPとの新しい一致/不一致理由。公開matchStateを読む。URL差替え/切断で旧応答・旧状態を無効化。新接続はhostname URIから解決し固定数値IPを保持しない。API失敗/timeout/missing/unknown/mismatchはnot-ready。 | HALTとRUNNINGの明示対応、初期gcState/config先行、fragmented JSON、上限超過/close/不正、API enumとUDP enumの相違、遅い旧応答・再設定・同値snapshot期限をfake WS/時計で検証。HALT→NEXT→FORCE/NORMALの既存fixture回帰を維持。自動Drive/チーム名変更は明示選択時だけで、observe-onlyはAPI書込み0。 |
| I81-03C / 小 / 03A・03B | 明示設定＋DI→実行可能Driver、内部HTTP39110、提案route GET /health/referee。health応答はowner ID、receiver epoch、入力ごとの正常/失敗count/last UTC/単調age、API照合結果/理由を返す。Testing:ContainerMode=true時に内部IF待受。未指定/不明ID・未知age・API不一致/古入力は503。200もDCP所有証明ではない。 | 実Application組成fixtureでDI、HTTP結果/mapping、opt-in/observe-only、内部URI、誤loopback使用拒否、取消・worker停止を確認。既存Public API/defaults/旧AppHost healthを保持。内部HTTPと将来Windows127.0.0.1動的公開を別に検証。 |

- 新規構成キー候補はRefereeDriver:Enabled / GameControllerUri / InterfaceAddress / AutoDriveToActive、Testing:ContainerMode、既存Testing:Diagnostics:{RunId,ResourceId,ContainerId,Generation}。名称は本案の候補で未実装。AutoDrive既定false、healthのpollで制御コマンドを送らない。既存standalone clientのloopback既定は維持し、container modeは内部URIを明示する。URIなしをホストloopbackへ暗黙fallbackしない。
- 200条件は当該Driverの帰属付き新しい正常復号と新しいAPI/UDP一致に限り、外部サービス入力/資源全体readyを推測しない。self ownerと期待GC producer/世代を分離し、再設定でepochを更新。完全ID/世代の正しさは将来AppHostがDCP所有状態と照合する。
- 全3group/portは維持しDriverは11003受信のみ。Driverに11003送信APIを新設せず、GC以外のproducerを増やさない。全UDP bridge内という配置はI81-05で検証し、今回Windowsへ受信を残す実装を新設しない。

### 新機能のfunctional Red→Green順序（今回は未実行）

1. 比較設計260行のTDDを適用。親が上記単位をtask-consistency-managerで台帳へ具体化した後、同担当が03Aからtestと最小の呼出可能な契約scaffoldだけを先に作る。型/空登録/no-opまたはNotImplementedのscaffoldを許すが動作完成を先行しない。compile成功を確認してから実行し、compile-errorだけをRedに数えない。
2. 03Aの最初のfunctional Red候補: 完全owner/fake raw受信/手動時計で同counterの正常Referee2packetを供給し、正常件数2・最終受信age・command subscriberの重複抑制をassert。未実装scaffoldのcount0/未供給結果による実行時assertion失敗をTRXに保持する。既存同counter除外test自体は書き換えない。
3. 03Bは有効matchStateと同epochのHALT→RUNNING入力で一致成功をassertするpositive testを先行し、Unknown/no-opの実行結果が失敗するRedを得る。negativeだけが最初から通ることを機能完成の証拠にしない。fake WSへ誤順序/旧応答も供給し、固定sourceのenum/JSON差異を期待値へ具体化する。
4. 03Cは明示container opt-in＋complete identity＋新しい有効入力を与え、実HTTP mapping/応答200をassertする先行fixtureを実行。未mapping/未登録等の実行時の期待不一致をRedとして保存し、同じsourceで実装後にGreenへ進む。実Host/Dockerを開発者マシンで起動しない。
5. 各Redのtest名・TRX・stdout/stderr・exit・実行source/hash/HEADを保存してから、その単位の最小実装を行い同一testを再実行。新旧Red/Greenを別dirに保持。既存67独立testとRefereeDriver/RefereeReadiness/host構成回帰は変更せず標準CIで確認する。過去のRed不足を今回で回復したとは記録しない。

### 検証routeと引渡し

- formal=remote_ci_only。ローカルassetsは存在するがSslProto固定submoduleのproto実体がなく、先行taskのCS0246制約は未解消。SDK/shared ASP.NET refは既存である。今回新buildで能力を誤認定しない。新規依存/設定修復/submodule取得をしない。
- normalized pure状態だけをsource-linkした既存cache/no-restore補助検証は分離して実施可能だが、実Referee decode/HTTP/host組成の標準gateやfunctional Redを代用しない。
- 標準Redは親がtest/scaffoldのreview-targetをcommit/pushしexact-SHA pull_request CIで取得して同担当へ戻す案。既存workflow通常testはrecursive submodules・SDK10.0.xを用い、実Dockerを使うpacket/full-stack jobは通常PRで実行しない。Red回収前に対象behaviorの製品実装へ進まない。
- focused案: dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter 'FullyQualifiedName~RefereeReceiveDiagnosticsTests|FullyQualifiedName~GameControllerStateClientTests|FullyQualifiedName~RefereeDriverCompositionTests|FullyQualifiedName~RefereeDriverFixtureTests|FullyQualifiedName~RefereeDriverNetworkAdapterTests|FullyQualifiedName~RefereeReadinessStateTests' --logger trx --results-directory <stage-dir> --diag <stage-dir>/vstest.log -m:1 /nr:false -bl:<stage-dir>/test.binlog 。標準CIのfull Tracker.Testsも別に保持。親所有のpush/CI実行範囲を本reportで代行しない。
- 完了は各unitの実functional Red→同一source Green、既存回帰、通常reviewの証拠まで。全体Ready、外部G3通過、DCP配置、Windows受入は別の未完了条件。今はscope draftを返し、親の内部台帳採用後に同担当へ実装を渡す。新たな人間承認ゲートを作っていない。

## リスク

- 外部3repoの変更/供給、GCほかの自身診断schema/count/timeは未許可/未供給の経路を保持。既存GC制御APIの利用可能性からG3全体通過を推論しない。
- API StateにUDP command_counterがないため、同じ遷移は有限窓・単調ローカル観測・epoch/sequenceと明示状態対応で照合する。未確認の外部clockや同じ数値enumを用いた完全同期を捏造しない。厳密なatomic対応が必要になれば不足契約として親へ返す。
- Driverの11003観測はDriver自身の正常復号であり、GC producerの所有認証や他trackerの受信証明ではない。UDP endpoint・container環境変数・HTTP自己申告をDCP所有証明にしない。
- 提案URI再接続はDuck内実装で先行できるが、再作成後のDocker DNS/実WebSocket/Windows経路は未検証。旧AppHost monitor除去やbridge配線を先行しない。
- 書込みは本report子5欄と/tmpの読取証拠だけ。製品/tests/設計/tasks/phases・環境/auth/network/依存変更・実サービス起動・CI待機/操作・commit/push・新branch/PR/Issue/merge/closeなし。全読取/保存processは終端、GET終了値全回収。保留approval・未回収結果・実行中processなし。
## 親による外部変更前提の訂正（2026-10-22）

上位親の直接読取調査とユーザーの非破壊方針を受領した記録。ここで新たな外部調査・配置変更は行っていない。外部3リポジトリは未変更・変更未承認であり、外部ソース変更ゼロの選択肢を先に評価する。「宛先指定機能を追加するため外部3repo改変が必須」という説明は不正確で、従前の不足説明をそのような確定結論へ使わない。

- [crane 4063cd31 の ibis_sender_node.cpp](https://github.com/ibis-ssl/crane/blob/4063cd31cd5b11b1cc919003907f5f4c527b252d/crane_sender/src/ibis_sender_node.cpp#L123-L149): 既存 target_address / target_port があり、起動時に数値IPを読む。
- [Orion_CM4 d7a2e07 の cm4_sim.cpp](https://github.com/ibis-ssl/Orion_CM4/blob/d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a/cm4/bridge/cm4_sim.cpp#L541): 既存 --out-addr / --in-port / --feedback-port-base がある一方、feedback受信bindは127.0.0.1固定。
- [framework a52b6bd の simulator.cpp](https://github.com/ibis-ssl/framework/blob/a52b6bdecd97a277544dd6f275afbf4483781e12/src/simulator/simulator.cpp#L1227): 既存 --ibis-feedback-addr（1227/1321行）が数値アドレスの送信先指定に使える。

第一評価候補は、crane / cm4-sim / simulatorを別コンテナのまま同じnetwork namespaceへ置き、loopback前提を保つ方式。[Docker公式 container networks](https://docs.docker.com/engine/network/#container-networks) の --network container:<name|id> が根拠。同じbridgeだけの場合とは異なり、loopbackとポート空間も共有する。共有先は --publish / --hostname / --dns 等が非対応で、公開ポートは共有元へ集約する必要がある。相互通信先をloopbackのままにできれば、各製品への相手IP再解決の追加は不要にできる余地があるが、全体要件適合は未確認。

未検証: ポート競合、multicast送受信IFと他containerへの経路、共有元再作成時の参加container再作成/停止順、DCP自動network指定との競合、所有者/stack限定cleanup、診断帰属。独立再起動の制約変化を明示し、Windows localhost UI到達と既存security境界を保つ。安定共有元containerは候補に過ぎず、未採用・未作成。Aspire v13.5.4 ContainerResourceBuilderExtensions.csのWithContainerRuntimeArgs存在は上位親が直接確認したが、API存在とDCP共存の実証は別である。

外部サービス自身の復号成功数などの診断供給は通信到達性と別契約で、namespace共有だけでG3等を解消とはしない。現在は独立して進められるDuck I81-03Aを継続し、外部ソース変更や配置切替は開始しない。必要な設計比較は既存の設計/タスク範囲で後続判断する。新しい調査agentは起動していない。
### 上位親の追加静的確認（2026-10-22、構成は未変更）

1. ポート: CM4既定in_port=12345/out_addr=127.0.0.1/out_port=12346/feedback_port_base=50100と、frameworkのIBIS_DEFAULT_PORT=12345は、共有namespace内で両方Anyへbindすると競合する。simulator既存 --ibis-port 12346 でCM4送信先と合わせる余地がある。crane既存target_address=127.0.0.1/target_port=12345、position_control.config_portとCM4 --config-portも一致が必要。Duckの実際の起動値と全socketの最終割当は未確認であり、全ポート検証済みとはしない。
2. feedback: CM4の127.0.0.1:50100+idはSO_REUSEADDR=true、既存コメントに同一ホストcrane multicast受信との同居意図がある。crane AsyncUdpReceiverはSO_REUSEADDR/SO_REUSEPORT、multicastはAny:portと全IPv4 IF membershipを使用し、CM4 relay既定IFは127.0.0.1、IP_MULTICAST_LOOP有効。この既存同居構造は候補の根拠だが、reuseだけで全受信者配送を保証しない。vision/referee/ROS DDS/複数instanceは別途確認、複数試合はnamespaceごと分離する。
3. 再作成: [Moby v28.5.1 getNetworkedContainer](https://github.com/moby/moby/blob/v28.5.1/daemon/container_operations.go#L899-L934) は起動時にrunningな共有元を解決し、restart中も拒否する。同名共有元再作成へ既存参加者が自動追従する前提は置かない。anchor存続中のアプリ再作成とanchor再作成を分け、後者は参加者停止→新anchor確認→全参加者再作成を所有者/世代管理で行う案。停止は参加者→anchor、起動は逆、旧固定container IDを再使用しない。未採用で作成許可ではない。
4. Aspire/DCP: [v13.5.4 ContainerCreator.cs L178-195](https://github.com/dotnet/aspire/blob/v13.5.4/src/Aspire.Hosting/Dcp/ContainerCreator.cs#L178-L195) は各Spec.NetworksへDefaultAspireContainerNetwork/aliasesを設定し、311-316行のRunArgs設定はこれを除去しない。329行はendpointからPortsも生成する。Moby同版のErrConflictSharedNetworkとDocker公式のcontainer mode制約より、通常AddContainerへWithContainerRuntimeArgsの --network container:... を足すだけの方式を検証済み解としない。DCP binaryの引数優先順位は未確認だが、Aspireモデル上で矛盾する要求を生成する点を区別する。公開入口はanchor側へ集約が必要で、Aspire作成user-defined bridgeとDocker built-in bridgeを混同しない。

結論: 外部3repoの改変は必須とせず、外部ソース無変更を優先評価する一方、Duck側group lifecycle/network/ports所有の設計が必要。通常DCP AddContainerで保持できなければ既存wrapper管理との整合を検討する。内部DCPモデルの無理な書換えやhost network変更は実行しない。同一アプリcontainer内loopback UDP adapterへDNS再解決を集約する案も未設計候補であり、遅延/送信元の意味/終了管理の別コストがある。共有方式の採用・切替はしていない。外部自己復号診断は別問題のまま。上位親は静的読取だけを実施し、新agent/コンテナ起動/環境変更なし。
