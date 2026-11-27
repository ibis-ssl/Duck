# Issue #52 SimulatorResponse 観測実装レポート

## タスク

- 目的: 既存 run では未記録の simulator `SimulatorResponse` を、既存の通常1回の初期 setup datagram に対して取得し、送信・受信・validation response の証拠を分ける。
- 種別: implementation / bounded diagnostic

## 対象範囲

- `SimulatorBallControlClient` の初期 setup 送信と、その同一 UDP socket 上での非ブロッキング response 観測。
- match-result の初期 setup event に、response の受信有無と `SimulatorError` のみを記録する最小の wiring、および focused .NET 回帰確認。
- 出力は response が明示的に得られた時だけ受信済みとする。timeout/response無しは適用失敗と断定しない。

## 対象外

- datagram の追加送信、retry、固定待機、配置座標・初期 setup の内容変更。
- Crane のコード、planner、image、挙動の変更。
- push、PR 本文/コメント更新、Docker acceptance run、merge。これらは親が管理する。

## Dispatch profile (parent-owned)

- selection inputs: task_kind=implementation; work_class=bounded_technical; uncertainty=high (UDP response/SimulatorControl semantics span client and simulator); change_radius=local (RefereeDriver.Host result pipeline); criticality=ordinary; repetition=single; decomposability=sequential_dependencies; context_need=bounded_history.
- selection source: automatic per sub-agent profile skill; user’s latest instruction limits work to the existing single initial setup send.
- observed decomposability: sequential_dependencies; decomposition policy/disposition: one bounded agent task, no parallel split.
- requested: `gpt-6.1-sol`, reasoning `high`, `fork_turns=none`.
- role plan: default role; no explicit agent type. Final applied profile remains unknown until runtime evidence.

## 実装の要点

- 既存の初期 setup に対する送信は `UdpClient.SendAsync` 1 回のまま。payload の生成処理、配置座標、初期 STOP 境界、後続の ball-only placement、Crane は変更していない。
- 初期 setup の同一 IPv4 UDP socket を ephemeral local endpoint に bind し、送信前に `ReceiveAsync` を開始する。送信完了後は response を待たずに既存の処理へ戻る。追加送信、再送、固定待機はない。
- 応答送信元が送信先の address/port と一致する datagram だけを観測対象にする。最初の一致 datagram を `SimulatorResponse.Parser` で解析し、`SimulatorError.code/message` と optional presence を保存する。空の response も受信として扱う。
- 観測の既定上限は 5 秒。応答、解析エラー、受信エラー、取消、期限到達のいずれでも socket を finally で破棄する。match-result 保存時は未完了の受信を cancel して終了を回収し、network deadline まで待たない。成功経路と開始タイムアウト経路は同じ保存処理を通る。
- `initial_setup_sent_unconfirmed` は維持する。別の top-level `initialSetupSimulatorResponse` が観測結果を表す。診断の有無・内容は試合の合否や進行条件に使用しない。

## 対象ファイル

- `Testing/Duck.Testing.RefereeDriver.Host/InitialSetupResponseCapture.cs`: 応答/error の immutable 記録、同一 socket の bounded receive、取消・破棄。
- `Testing/Duck.Testing.RefereeDriver.Host/SimulatorBallPlacementFixture.cs`: 初期 setup の一回送信に受信観測を接続。ball-only 送信と配置生成は維持。重複した初期 setup 呼出しは追加送信前に拒否する。
- `Testing/Duck.Testing.RefereeDriver.Host/MatchControllerWorker.cs`: response 観測の終了と `match-result.json` への追加保存。
- `Tracker/Tracker.Tests/SimulatorInitialSetupResponseTests.cs`: loopback の一回送信・payload 同一性・応答待ちなし、空/エラー応答、解析失敗、期限、開始取消、receive failure、socket 破棄の 7 ケース。
- `Tracker/Tracker.Tests/MatchControllerWorkerTests.cs`: 既存の成功・開始タイムアウト試験に、観測終了呼出しと保存内容の assertion を追加。
- 本レポートと `reports/evidence/issue52-simulator-response-observation-20261108120201/`: 実装・検証証跡。Dispatch profile は親所有の内容をそのまま保持した。

## 出力の意味

| status | 意味 |
| --- | --- |
| `response_received` | 対象 endpoint からの datagram を `SimulatorResponse` として解析できた。`errors` は simulator の validation response。pose 適用の独立した証明ではない。 |
| `invalid_response` | 対象 endpoint から受信した datagram の protobuf 解析に失敗。byte 数と例外メッセージを保持。 |
| `receive_error` | UDP 受信処理で socket/disposal error が発生。例外を保持し、試合進行へ送出しない。 |
| `no_response_observed` | 既定 5 秒以内、または開始取消・結果確定までに response を観測できなかった。setup datagram の非受信や非適用を意味しない。 |
| `null` | 初期 setup の response 観測を開始していない。 |

## 実行コマンド・結果

- 作業ディレクトリ: `C:\Users\donabe\Documents\Codex\2026-11-07\task\Duck-issue52-aspire-006g2`。
- 実行環境: Windows、PowerShell、.NET SDK `10.0.403`。正式な `require_escalated` shell の `whoami` は `fa780\donabe`。初回の既定 sandbox は `fa780\codexsandboxoffline` だったため以後使用せず、その読み取り結果は検証証跡に使用していない。編集・テスト・証跡保存はすべて正式な shell で実行した。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter 'FullyQualifiedName~SimulatorInitialSetupResponseTests|FullyQualifiedName~SimulatorBallPlacementFixtureTests|FullyQualifiedName~MatchControllerWorkerTests' --logger 'trx;LogFileName=focused.trx' --results-directory <worktree>/reports/evidence/issue52-simulator-response-observation-20261108120201`
- 結果: 終了コード 0、合計 57 / 成功 57 / 失敗 0 / skip 0、テスト実行時間 401 ms。既存配置試験は既存ケースをそのまま実行した。
- 証跡: `identity.log`、`focused.log`、`focused.exit-code`、`focused.trx`、`source-fingerprint.json`。TRX に個別の実行結果を保存した。
- `git diff --check`: 終了コード 0。CRLF 正規化に関する Git warning のみ。既存依存由来の NU1902/NU1900、Aspire・compiler・xUnit warning を focused.log に保持した。新規診断コードの compile error はない。
- この検証は focused inner loop であり、repository 全体の local equivalence gate や Docker acceptance の証拠ではない。Python、Docker run、commit、push、PR 編集、merge は実行していない。

## 検証順序・ポリシー

- 事実として今回は実装と focused テストを作成した後にテストを実行した。当初の先行 Red/Green や先行失敗の証拠はない。下記の事後再現とは区別する。
- 読み取り中に `Tracker/Design/Testing/tracker-comparison-debug-design.md:284` の「実装は TDD で行う」を確認し、親へ通知した。供給された作業 context には testing order の明記がなかった。事後テストを TDD と扱わず、親でこの診断追加の policy disposition を解決する必要がある。

## 確認済みの外部証跡（親から提供）

- branch baseline: `task/issue52-aspire-006g2`、HEAD `a9a45bdc3cf1fbe929350e33e40a0f646545b19e`、PR #119 は base `task/issue50-aspire-006g` の Draft。
- GHCR latest index: `sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79`。linux/amd64: `sha256:9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554`。AppHost pin と run `42436181286` に一致。acceptance 直前の再確認は親が実施する。
- baseline exact-head CI `42440022580` は成功。PR event のため packet/full-stack jobs は skip。今回の未 commit 差分に一致する CI はまだない。

## 引き渡し・残る確認

- 技術 HEAD は `a9a45bdc3cf1fbe929350e33e40a0f646545b19e` のまま。変更は未 commit。独立レビュー判定は実施していない。
- ローカル試験は .NET loopback の動作証拠であり、実 simulator image が response を返すことや setup を適用することは未確認。
- 受信と終了の競合は、受信 task を必ず回収し socket を finally で破棄することで管理する。観測開始前の DNS/send 失敗は従来どおり試合開始エラーになり得る。
- 親の次作業: policy disposition、通常レビュー、最終候補の必要な検証、commit/CI、最新 image digest の再照合後に許可済みの診断 acceptance を管理する。

## 追加の事実記録: 事後再現と中止指示

- 親からの再現指示に従い、全ソース・テスト・レポート・既存証跡を `C:\Users\donabe\AppData\Local\Temp\issue52-response-replay-20261108031137` に退避し SHA256 manifest を保存した。
- 追加済みテストを維持したまま、本番の変更済み 2 ファイルを committed baseline blob と byte 一致する内容へ一時差替えし、新規本番 capture ファイルを一時的に除いた。同じ focused コマンドは `SimulatorResponseObservation` 未定義の CS0246 により終了コード 1。これは実際の compile failure であり、runtime assertion の失敗ではない。`replay-red.log` と exit-code に保持した。
- 同じ PowerShell 操作の finally で本番 3 ファイルを退避内容へ戻し、各 SHA256 一致を確認した。テストも不変。復元後の focused Green は 57/57 成功、失敗/skip 0、310 ms、終了コード 0。`replay-green.log` / TRX / exit-code に保存した。
- この Red/restore/Green は、その後の中止指示が agent に到着する前に完了していた。中止指示後に再現を継続したものではない。`replay-restoration.log`、`replay-preservation-verification.json` に復元証拠を保持する。
- この事後再現によって、最初の実装順序が TDD へ変わることはない。当初は test-after であり、先行失敗の証拠はない。再現作業を行った事実も保持し、「提供に遅延がなかった」とは主張しない。以後 TDD 再現のための作業は行わない。

## 最新 image・既存環境の局所確認

- 毎操作の正式な elevated shell 内で `whoami=fa780\donabe` を確認した。Python は一度も実行していない。本番ソース・テストは事後再現の復元後に変更していない。
- Registry API で latest を再確認: index `sha256:f505fcb144ca5fcadcbfa828c814df0262e3112f6038fdbccf4da10bae580f79`、linux/amd64 `sha256:9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554`。既存 match image pin と一致する。
- 既存 Docker CLI: `C:\Users\donabe\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe`。context は `desktop-linux`、Client/Server は 29.9.1、engine ID は `5bc5ab8a-7340-47ba-8b7c-9f645375a54c`、linux/x86_64。`docker ps` は空だった。
- 許可された非破壊操作として `docker pull --platform linux/amd64 ghcr.io/ibis-ssl/framework-simulatorcli@sha256:9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554` と image inspect を実施した。初回 pull は PATH 外の credential helper を解決できず終了 1。当該 PowerShell プロセスの PATH に既存 Docker bin を追加した 1 回の再試行は成功、終了 0。設定や認証は変更していない。image はローカルに保持した。
- inspect: Entrypoint は `tini --`、Cmd は `./bin/simulator-cli`、WorkingDir は `/home/default`、User は `default`、Env は PATH のみ、Volumes はない。ExposedPorts metadata は 10300/10301/10302 の tcp。これは UDP protocol の対応有無を証明する情報ではない。初回 inspect template は存在しない optional Volumes field で失敗したため、raw JSON による読み取りで正常取得した。
- 証跡: `simulator-image-identity.log`、pull の各 log/exit-code、`simulator-image-inspect-template-error.log`、`simulator-image-inspect-raw.json`、`simulator-image-inspect.json`。コンテナ作成・起動、追加 setup 送信、network/volume の作成、prune/remove はしていない。

## 実行可能性と範囲の自己点検

- 既存 sender の実行箇所は `MatchControllerWorker` の初回 STOP callback。`RefereeDriverApplication` は `MatchController:Enabled=true` でこれを登録する。simulator に対して初期 setup だけを一度送る単体 CLI/config は既存 repository に見つからなかった。
- `Testing:VisionSimulationOnly` は simulator と Duck の構成で今回の sender を呼ばない。`Testing:MatchReadinessOnly=true` は match-controller を作成しないため今回の response 観測には使えない。
- 既存の `workflow_dispatch mode=match` は Ubuntu 24.04 の Docker full-stack を起動し、Python scripts も実行する。今回の「Python を新規使用しない」制約に従って起動していない。既存 packet job の simulator-only 呼出しも今回の sender を使用しない。
- 既存 match 経路は DCP の bridge network、通常の全参加者、match-results と recorder の bind mounts を使用する。結果は `Path.GetTempPath()/duck-aspire-match-results/<stackId>/match-result.json` に保存される。既存 cleanup は owner/stack/run labels と今回作成した container IDs に限定し、image を削除しない。
- 新しいコンテナ設定・専用 sender harness・起動 mode を独自に作成することはしていない。現在は「既存 simulator-only 構成のまま今回の sender を呼ぶ」実行経路がないため、起動前に親へこの境界を報告した。
- 技術差分の自己点検: 新規送信ループなし、初期 payload/座標変更なし、通常送信は一回、応答受信を試合進行の条件に使用しない。受信元 endpoint を照合し、socket は受信 task の finally で破棄し、保存経路で task を回収する。これは実装者の確認であり独立レビュー判定ではない。

## 許可済み局所診断の実行結果

- 追加の明示指示に従い、repo 外 `C:\Users\donabe\AppData\Local\Temp\issue52-local-simulator-response-20261108032102` に最小 Windows .NET harness を作成した。本番の変更済み Host DLL を reflection で読み込み、既存 `CreateInitialOwnHalfSetupCommand` / `SendInitialOwnHalfSetupAsync` / bounded response capture をそのまま呼び出した。本番コード・テストは変更していない。
- harness は初期 setup メソッドの呼出しを一か所だけ持つ。送信前に `FileMode.CreateNew` で一意の send-attempt marker を作るため、同じ診断の再実行は追加送信前に拒否する。追加送信、再送、network probe datagram はない。
- 実行 shell の `whoami` は `fa780\donabe`。起動前に `127.0.0.1:10300` の exclusive UDP bind が成功し、空きポートであることを確認した（確認用 socket は送信せず破棄）。
- 実際の container ID: `fa275dcf9ff412277bbf3d02f4cf7742c1c20ca74ed17f681d39d4de1aac4d29`。image は許可された exact digest `ghcr.io/ibis-ssl/framework-simulatorcli@sha256:9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554`。
- 起動は `docker run --detach --rm --name <一意名> --publish 127.0.0.1:10300:10300/udp --entrypoint tini <exact-image> -- ./bin/simulator-cli -g 2020B --realism None --ibis-port 12346 --ibis-team-color yellow`。既存 workflow/AppHost と同じ simulator 引数、default bridge、loopback UDP control 公開のみ、volume なし。Crane、full-stack、Python、追加 network は使用していない。
- 送信前は `/proc/net/udp` と `/proc/net/udp6` を受動的に読み、10300（hex 283C）の bind を確認した。固定の startup sleep 後に無条件送信する手順ではない。
- 送信先 `127.0.0.1:10300`、`blueTeamOnPositiveHalf=true`、payload 915 bytes、SHA256 `8CC5F464C3D2815A13DA8CD63997A8BD12650698C136994FD8F2F72FCA4DBDDC`。本番 sender の一回の SendAsync は例外なく完了し、`2026-11-08T03:22:20.3532192+00:00` を send-completed 記録に保持した。
- 5 秒の観測結果は `no_response_observed`、remoteEndpoint/datagramBytes は null、errors は空。理由は `No response observed within 5s. This does not establish non-receipt or non-application of the setup datagram.`。追加送信や再試行は行わなかった。
- この結果は「送信完了・当該 socket での response 未観測」を示す。Docker Desktop の loopback UDP mapping を通ったこと、simulator の受信・pose 適用、SimulatorResponse の常時返送を証明しない。`errors=[]` も simulator がエラーなしと返した意味ではない。
- finally で logs/inspect を保存後、この一意 container ID だけを stop した。`--rm` による自動削除と、名前一致 container の残存なしを確認。後続の `docker ps` も空。本番 image と既存 network は保持し、prune/remove は行っていない。
- 証跡: `local-diagnostic-port-check.log`、`local-diagnostic-invocation.json`、`local-diagnostic-send-metadata.json`、`local-diagnostic-send-once.lock`、`local-diagnostic-send-completed.log`、`local-simulator-response.json`、harness build/run log と exit-code、`local-simulator-inspect.json`、`local-simulator-proc-net-udp.log`、`local-simulator.stdout-stderr.log`、`local-simulator-final-inspect.json`、`local-simulator-stop.log`、`local-simulator-cleanup.log`。harness 自体の写しも証跡として保持した。
- harness build は warning/error とも 0、run は終了コード 0。終了後に本番 3 ファイルとテスト 2 ファイルの SHA256 が事前退避と一致することを確認した。技術 HEAD は `a9a45bdc3cf1fbe929350e33e40a0f646545b19e` のまま。commit/push/PR 編集/merge なし。

## Linux 受入 run 42436181286 との対比と次の最小観測

### 環境と証拠の境界

Linux 受入 run `42436181286` は Ubuntu runner 上の DCP bridge network における通常 match startup である。上記の Windows Docker Desktop 局所診断は loopback UDP port mapping を通る別環境であり、その `no_response_observed` を Linux の不達へ一般化しない。Linux の `initialStopSnapshot.blueTeamOnPositiveHalf` は `false` で、Windows 局所診断の指定は `true` である。

Linux の確認対象は、既存の `C:\Users\donabe\AppData\Local\Temp\issue52-run-42436181286\aspire-full-stack-42436181286-1` 配下のログ・JSON と同じ run の Recorder archive である。今回、追加送信・container 起動・受入 run・Crane 変更は行っていない。

### 構成上の送信先、送信完了、simulator 受領を分ける

- 構成上の宛先は `simulator:10300`（IPv4 / UDP）。当該 run の sender は DNS 解決で選択した IPv4 に送るが、setup datagram が実際に使用した宛先 IP、送信元 IP/port、送信時刻は保存された setup event にない。`startup-diagnostics.txt` の CM4 target `172.18.0.7` は別の通信経路の記録であり、その値を setup の実宛先 IP と断定しない。
- `match/match-result.json` の `ballPlacementFixtureEvents[0].status` は `initial_setup_sent_unconfirmed`。`reason` は中心の停止ボールと 22 台の own-half robot teleport を一度送信し、`blueTeamOnPositiveHalf=False` とした旨を記録している。この event は sender の `SendAsync` が例外なく完了した後に追加される。simulator の受信・拒否・配置適用を示す event ではない。
- `initialStopSnapshot` は `command=1`、`commandCounter=0`、`packetTimestamp=1794103173709682`、`blueTeamOnPositiveHalf=false`、`initialStopCommand=STOP`。この `packetTimestamp` は Referee packet の時刻であり、setup の実送信時刻ではない。
- 同 JSON の simulator identity は `currentResourceId=duck-gha-42436181286-1-simulator`、`containerId=3ebfaa63bc7e84ba50a25f4c349bd8f317ba5565cae62c65c2ea27070ce0b430`、`runId=gha-42436181286-1`。`simulator.inspect.txt` の exact image digest は `sha256:9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554`。
- `initialReadiness.participantEvidence.resources` の simulator は `state=Running`、`healthStatus=Healthy`、`isReady=true` だが `serviceEvidenceReady=false`。process/container readiness は UDP 10300 での setup 受領証拠ではない。
- この run の result JSON に `initialSetupSimulatorResponse` は存在しない。UDP 10300 の当該 setup datagram と特定できる受信・拒否・`SimulatorResponse` の記録は確認できない。
- `simulator.startup.log` には後続の Yellow robot 0–10 の `command dropped` がある。例として robot #3 は `vision_global_pos (1.200, 0.000)` が simulator の robot 位置から 5.384 m 離れ、0.50 m の閾値を超えると記録している。これは後続 robot command の位置推定不一致であり、初回 setup の受信失敗を直接示さない。

### Vision の受信、記録、配置適用を分ける

- `duck.startup.log` は `RuntimeHost receiving SSL-Vision packets from 224.5.23.2:10020 via 172.18.0.8, 127.0.0.1` と記録する。`VisionPacketsReceivedTotal` は起動時 0 から 182、430 と増え、保存ログ末尾では 14629。ここで multicast endpoint は受信先、列挙された interface は受信側であり、Vision UDP datagram の実際の送信元 IP/port の記録ではない。
- `match-result.json` の `initialReadiness.visionReady=true`。同 initial readiness の recorder `serviceEvidenceReason` は `referee=4930, vision-detection=7183, tracker=18713; decompressed-eof=False`。後の `match/participant-status.json` は recorder `referee=9791, vision-detection=14991, tracker=38753; decompressed-eof=False` を記録する。観測時点が異なるため、それぞれの値を最終 archive の件数と混同しない。
- 最終 `match/motion-acceptance.json` は `RecordStreamComplete=true`、`RecordCount=74373`、`RefereeRecords=11403`、`VisionRecords=17560`、`ActiveWindows=[]`、`Passed=false`。Vision 受信と記録の存在は確認できるが、これだけで setup の配置適用を示さない。
- 保存済みの STOP 時 archive 座標の解析結果では、ball はほぼ `(0, 0)` mm、Blue #3 は `(3400.64, -2799.32)` mm、Yellow #3 は `(-3399.36, -2799.32)` mm。Linux の intended setup は Blue #3 `(-1200, 0)` mm、Yellow #3 `(+1200, 0)` mm であり、robot 座標は一致していない。この保存済み解析値は今回新たに時系列再抽出できた値ではない。実測方向角も今回確定していない。
- ball が中心にあることだけでは初期状態と teleport の効果を区別できず、robot 座標の不一致だけでも未適用と setup 後の移動を区別できない。受信・適用・移動の原因は断定しない。

### 既存 .NET CLI の到達範囲と次の最小観測

既存 build の `Duck.Testing.AppHost.dll --verify-motion-archive <保存済みarchive>` を report 出力先指定なしで実行し、完全終端と上記 74373 / 11403 / 17560 件を確認した。終了コード 1 は `No active referee command window contained position changes for both teams.` の意味で、archive の読取り不能ではない。既存 `issue52-ref-inspect/bin/Release/net10.0/inspect.dll` も終了コード 0 で読み取り、`STOP(counter=0)` から `PREPARE_KICKOFF_YELLOW(counter=1)` への遷移と `NORMAL_START` の欠如を確認した。

既存 motion CLI は active command window の集計専用で、STOP 中の個別 Vision 座標と時刻を出力しない。内部 `SslLogRecordReader` は frame を読めるが、現在の CLI で時系列座標を出す機能はない。setup の実送信時刻も未記録のため、現在の証拠と既存 CLI だけで「配置適用後に移動した」とは確定できない。新しい parser/source/test の作成や、生成型ロード失敗への再挑戦はしていない。

次の最小観測案は、**次の単一の通常 setup 送信時に実宛先 IP/UDP port・送信時刻と受信 response を記録し、Vision の最初の座標と時刻を相関する**こと。送信完了、response の受信と SimulatorError、Vision の観測姿勢は別の証拠として扱う。通常 setup は一回のままで、retry・追加 setup datagram・Crane 変更は加えない。この段落は次の観測案であり、新しい実行の完了や許可を示すものではない。

## 次の通常 Linux setup 一回に向けた送信 metadata の最小拡張

ユーザーの追加指示により、現行 response capture の結果に送信 metadata だけを追加した。変更は `InitialSetupResponseCapture.cs` と `SimulatorBallPlacementFixture.cs`、対応する既存 focused test 2 ファイル。配置 payload/座標、送信回数、startup の進行条件、Crane、Vision archive の reader/CLI/format は変更していない。

既存 `initialSetupSimulatorResponse` の追加 field は以下のとおり。

| field | 取得時点・意味 |
| --- | --- |
| `destinationIp` | DNS 解決後に選択し、実際の SendAsync に渡す IPEndPoint の address。hostname からの推測ではない。 |
| `destinationPort` | 同じ IPEndPoint の UDP port。本番既定 10300。 |
| `sendStartedAtUtc` | 既存の一回の SendAsync を呼ぶ直前の UTC wall-clock。 |
| `sendCompletedAtUtc` | その SendAsync の await が例外なく完了した直後。失敗した場合は null のまま。remote receipt/application の時刻ではない。 |
| `responseReceivedAtUtc` | 送信先と一致する datagram の ReceiveAsync が完了した直後。解析成功/解析失敗で保持し、未観測では null。 |

metadata は receive task の終了を回収して既存 result へ保存する時に付加する。応答待ちを startup の条件へ加えていない。送信 payload は従来と同じ生成関数で一度作成し、SendAsync 呼出しも一回のまま。受信が send await の完了より先に観測される可能性はあり、両時刻の順序を remote application の順序と解釈しない。

### 限定検証と証跡

- 正式 elevated shell の各操作で `whoami=fa780\donabe` を確認。.NET SDK 10.0.403。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter 'FullyQualifiedName~SimulatorInitialSetupResponseTests|FullyQualifiedName~MatchControllerWorkerTests' --logger 'trx;LogFileName=metadata-focused.trx' --results-directory <既存evidence directory>`
- 結果: 終了コード 0、19/19 成功、失敗/skip 0、362 ms。新 field の実 endpoint と送信時間範囲、受信時刻、未応答時の null、JSON 保存を確認した。Full test の再実行はしていない。
- `git diff --check` は終了コード 0。`metadata-focused-identity.log`、`metadata-focused.log`、TRX、exit-code、`metadata-diff-check.log`、`metadata-source-fingerprint.json` に保持する。依存由来の既存 warning も log に残す。
- テキスト編集の最初の guarded anchor が改行差で一致せず、その操作はテスト前に停止した。capture record の変更だけが先に保存されていたため、残りの送信側と test を続けて完成させた。テスト失敗や Red の証拠とは扱っていない。

### 次の起動・対応付けの計画

この追加差分による新たな simulator 起動・setup 送信・workflow dispatch は行っていない。親へ変更概要と focused 結果を報告した後、レビューと必要な既存手順を満たしてから、次の通常 Linux 受入の初期 setup 一回で metadata と response を保存する計画である。最新 image digest の起動直前照合は親が管理する。

Vision は同じ run の既存 recorder archive を保持し、必要になった時点で既存 `SslLogRecordReader` を利用する一時 C# 解析から、送信区間前後の Vision frame 座標を照合する案にとどめる。常設 CLI、独自 parser、新しい archive format は作らない。現行 reader の callback は recorder timestamp を公開していないため、一時解析で既存 frame の capture timestamp と send UTC の対応を安全に取れない場合は、送信 metadata・response・既存 Vision count/pose archive で分かる範囲を報告し、適用後移動を断定しない。

TDD 先行失敗証拠がない事実、完了済み事後 replay と当初実装順序の区別は既存節のまま保持する。再現のやり直し、Python、追加送信、Crane 変更、push/PR 更新はしていない。

## Issue #52 と #54 の条件分離、旧 HEAD の実移動証拠

GitHub Issue 原文、parent API、PR #119、既存 run artifact を読み取りで照合した。#52 と #54 はともに親 #50 の兄弟 Issue。#54 が #52 の子ではなく、PR #119 の `closingIssuesReferences` は #52 だけである。

| 範囲 | 原文・設計上の条件 |
| --- | --- |
| #52 / ASPIRE-006G2 | `ASPIRE-MATCH-003` を実行し、同一 active referee 窓で Crane と TIGERs の双方の位置変化を実 packet で確認する。完了時は独立レビューと、その時点の HEAD に一致する検証結果を記録する。 |
| #54 / ASPIRE-006G4 | `ASPIRE-MATCH-005` を実行し、試合終了、結果、SSL log、Crane の記録データ、全 resource の標準出力・標準エラーを失敗時も保存する。本文に POST_GAME の文字列はないが、参照設計は POST_GAME と時間上限終了を区別する。 |

#54 側の POST_GAME / 試合終了条件を、#52 の同一 active window 移動条件に追加しない。全体 workflow の成功、個別 motion 条件、試合終了を別々に記録する。

### run 42434455957: #52 condition passed at older head

- GitHub run API の `head_sha` と artifact `environment-before.txt` の `sha` はともに `8753c5dbb94e2b990b08be706ec03da333f5e337`。workflow は `.NET tests` / `.github/workflows/dotnet-test.yml`、event は `workflow_dispatch`、attempt は 1、branch は `task/issue52-aspire-006g2`。
- 同 run の `simulator.inspect.txt` は image `ghcr.io/ibis-ssl/framework-simulatorcli@sha256:9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554`、run identity `gha-42434455957-1` を記録する。
- `match/motion-acceptance.json` は `Passed=true`、`RecordStreamComplete=true`、`RecordCount=71199`、`RefereeRecords=10894`、`VisionRecords=16794`。active windows は 10 窓で、全窓が `CraneMovingRobotCount=11`、`TigersMovingRobotCount=11`。例として `CommandCounter=2` / `Command=2` / `VisionPackets=252` の一つの窓で両チーム11台の移動を記録している。
- これは #52 の中心条件を満たした歴史的な実測である。一方、run 全体は `failure`、`match-result.json` は `stageReached=maximum_time`、`terminationKind=MaximumTimeReached`、`failure=MATCH_MAXIMUM_TIME_REACHED: execution stopped without POST_GAME.`。motion pass と試合終了未達は両立する。
- 既存受入レポート先頭の「記録時点 HEAD 129a752」は、そのレポートの記録時点を指し、run の実行 HEAD ではない。実行 HEAD は API と artifact が一致する `8753c5d` として扱う。
- 当該 run の artifact 基点は `C:\Users\donabe\AppData\Local\Temp\issue52-run-42434455957\aspire-full-stack-42434455957-1`。

### 現在 HEAD と直近の実受入を混同しない

- 現在 PR #119 の base は `task/issue50-aspire-006g`、head branch は `task/issue52-aspire-006g2`、head SHA は `a9a45bdc3cf1fbe929350e33e40a0f646545b19e`。8753c5d はこの祖先だが同一ではなく、今回の未 commit response/destination/time 差分も旧 run に含まれない。旧 run を現在 HEAD の成功証拠へ付け替えない。
- 直近の実受入 run `42436181286` の実行 HEAD は `749709a69171e410e1263157e8356daf46688749` であり、これも現在 HEAD とは同一ではない。この run の `motion-acceptance.json` は `Passed=false`、`RecordStreamComplete=true`、`RecordCount=74373`、`RefereeRecords=11403`、`VisionRecords=17560`、`ActiveWindows=[]`。match は開始 timeout であり、当該 run では #52 の移動条件を観測していない。
- したがって「#52 の過去の条件成立はある」「現在の未 commit 差分を含む最終候補のレビューと対応する検証証跡はまだ整理が必要」「#54 の試合終了条件は別」を並立して保持する。現在の未完了を理由に #52 の条件を #54 の条件へ引き上げない。

## 未 commit instrumentation の実装者自己点検と引き渡し

対象ソースは次の 5 ファイル。

- `Testing/Duck.Testing.RefereeDriver.Host/InitialSetupResponseCapture.cs`
- `Testing/Duck.Testing.RefereeDriver.Host/SimulatorBallPlacementFixture.cs`
- `Testing/Duck.Testing.RefereeDriver.Host/MatchControllerWorker.cs`
- `Tracker/Tracker.Tests/SimulatorInitialSetupResponseTests.cs`
- `Tracker/Tracker.Tests/MatchControllerWorkerTests.cs`

実装者点検では、初回の payload/座標生成が維持され、初回 SendAsync 呼出しが一回であること、送信前に同じ socket の受信を開始すること、試合の進行が応答を待たないことを確認した。実 IP/port は実際に SendAsync へ渡す解決済み endpoint から取得し、UTC 時刻は send 呼出し直前・await 完了直後・ReceiveAsync 完了直後で取得する。送信完了は適用を証明せず、受信未観測も不達を証明しない。

受信 task は成功・解析失敗・socket error・期限・取消で終了し、socket は finally で破棄する。既存 result 保存時に未完了の観測を cancel/回収する経路を成功時・開始 timeout 時で共用する。新しい Vision CLI/format、Crane 変更、追加 setup/再送はない。時刻はアプリ側の UTC wall-clock 観測で、kernel packet timestamp や remote application timestamp ではない。

今回の自己点検では、既存 `metadata-focused.trx` の 19 executed / 19 passed / 0 failed・error・timeout・aborted と exit-code 0 を読み直し、5 ソースの SHA256 が `metadata-source-fingerprint.json` と一致することを確認した。テストの再実行ではない。独立レビューの判定はしていない。

当初の TDD 先行失敗証拠がない事実と、完了済み事後 replay の区別は従前どおり保持する。今回の自己点検や19件の成功を TDD と主張しない。親へ、既存の通常レビュー／独立レビュー担当で最終候補差分を確認する必要がある状態として引き渡す。実装担当は reviewer を新規起動せず、commit/push/PR 更新・追加受入 run・送信を行っていない。

## 最終差分照合: 8753c5d から a9a45bd と未 commit observer の証拠境界

`git diff --name-status 8753c5dbb94e2b990b08be706ec03da333f5e337..a9a45bdc3cf1fbe929350e33e40a0f646545b19e` と、match/AppHost/placement/network/motion verifier/image 関連パスの限定 diff を読み取りで照合した。committed 差分は以下の 3 ファイルだけ。

| ファイル | 変更内容と ASPIRE-MATCH-003 への影響 |
| --- | --- |
| `Testing/Duck.Testing.AppHost/Program.cs` | `MatchController__MaximumDuration` の既定値を 2 分から 5 分へ変更。初期配置・開始手順や motion 判定ではなく実行上限の変更。旧 run の最初の 2 分内に成立した移動観測を否定しない。 |
| `scripts/verify_aspire_match_acceptance.py` | `cm4-sim` を誤った excluded resource 集合から除き、`finalReadiness=null` を二次例外にせず未完了扱いに修正。全体 checker の修正であり、packet・配置・motion verifier の変更ではない。 |
| `reports/issue52-current-head-acceptance-20261108.md` | 文書追加。runtime 動作への変更なし。 |

この committed 比較では、Simulator / Crane の起動設定、network、placement payload・座標、初期送信回数、motion verifier、image pin、workflow に変更はない。そのため run `42434455957` の `Passed=true`、10 active referee windows の全窓で両チーム11台が移動した記録は、**旧 HEAD 8753c5d で成立した #52 条件の歴史的な実測証拠として再利用できる**。ただし現在 HEAD a9a45bd の実行成功証拠へ付け替えず、現在の未 commit observer を実行した証拠にも扱わない。

#52 の対象は `ASPIRE-MATCH-003` の同一 active referee 窓における Crane / TIGERs 双方の位置変化。#54 の `ASPIRE-MATCH-005` / POST_GAME・試合終了・結果保存は独立した兄弟 Issue の条件であり、#52 の移動条件をその完了条件まで引き上げない。

直近の既存実受入 `42436181286` は `Passed=false`、`ActiveWindows=[]`、`RecordStreamComplete=true` で、当該 run では双方移動を確認できていない。この run の実行 HEAD は 749709a であり、a9a45bd の exact-head 実受入でもない。旧 run 424344 の motion pass と、新しい run 424361 の motion fail をどちらも保持する。

### 未 commit observer は別の runtime 差分

未 commit の `SimulatorBallPlacementFixture.cs` と新規 `InitialSetupResponseCapture.cs` は、初期送信 socket の明示 bind、送信前 ReceiveAsync、最大5秒の受信寿命と取消・破棄を追加する。`MatchControllerWorker.cs` は結果保存時の受信回収と診断結果保存を追加する。実宛先 IP/port・送信開始/完了・受信時刻の metadata もこの未 commit 差分に含まれる。

送信 payload・配置座標・一回送信・試合進行条件を維持していても、socket/runtime の寿命に触れる差分である。既存19件の focused pass と自己点検はあるが、旧 motion pass をこの observer の Linux 動作証拠へ転用できない。最終 candidate に対する通常レビュー／独立レビューと、通常 Linux setup 一回での実宛先・送信時刻・response 結果の別受入が必要。現在 candidate について #52 の実移動成立を確認する受入範囲は同一 active referee window の双方移動までとし、#54 の POST_GAME は追加必須にしない。

TDD 先行失敗証拠がない事実と、完了済み事後 replay が当初の実装順序を TDD に変えないという既存記録は維持する。今回も事後 replay を TDD と扱っていない。この追記の後は停止し、commit/push/CI/run・追加送信は行わない。

## 独立レビュー P2: 送信中 socket の所有権修正

独立レビューで、送信前に開始した observer の finally が UdpClient を直接破棄しており、通常の一回の SendAsync が長く保留された場合、5秒の観測期限や早い受信完了が送信中の socket を破棄できるという P2 指摘を受けた。診断が通常送信を中断し得るため、元の実装のこの点は修正が必要だった。

修正は以下の3ソースに限定した。

- `SimulatorBallPlacementFixture.cs`: sender は SendAsync の正常/異常終了を TaskCompletionSource で通知する。内側 finally で通知してから、失敗時の observer cleanup を待つため、所有権待ちの deadlock を防ぐ。通常の一回の SendAsync、payload、宛先、時刻 metadata は維持する。
- `InitialSetupResponseCapture.cs`: observer は従来どおり送信前に受信を開始するが、finally で sender の終了通知を待ってから socket を破棄する。観測期限・受信成功・取消が SendAsync の保留中に完了しても、socket を閉じない。5秒は受信の観測期限であり、まだ保留中の通常送信をその期限で打ち切るものではない。
- `SimulatorInitialSetupResponseTests.cs`: `CanceledObserverCannotDisposeSocketWhileSendOwnershipIsPending` を1件追加。先に取消済みの受信を用いて observer が決定的に cleanup に入る状態を作り、pending send 相当の所有権通知が未完了なら socket が有効で観測の破棄も完了しないこと、通知後は結果回収・socket 破棄が完了することを確認する。回帰試験自体は setup datagram を追加送信しない。

### 修正後の限定検証と再レビュー対象

- 毎操作の正式 elevated shell で `whoami=fa780\donabe` を確認。
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter 'FullyQualifiedName~SimulatorInitialSetupResponseTests|FullyQualifiedName~MatchControllerWorkerTests' --logger 'trx;LogFileName=ownership-focused.trx' --results-directory <既存evidence directory>`
- 終了コード0、20/20成功、失敗/skip 0、344 ms。対象は capture/result の既存 focused 群と所有権回帰1件のみ。全件テスト、Docker/受入 run、追加 setup、Python は実行していない。
- `git diff --check` は終了コード0。`ownership-focused-identity.log`、`ownership-focused.log`、`ownership-focused.trx`、各 exit-code、`ownership-diff-check.log` と `ownership-source-fingerprint.json` に証拠を保持する。先の19件成功は修正前の別証拠として残す。
- 技術 HEAD は a9a45bd のまま。修正は未 commit で、再レビュー対象はこの HEAD と ownership-source-fingerprint.json の全5ソース hash で特定する。親に既存の通常・独立 reviewer でこの正確な修正を再レビューするよう依頼し、結果待ちとする。実装者は独立判定を行わない。
- TDD 先行証拠はない。完了済み事後 replay を TDD と扱わず、この修正のテストも先行 Red/Green と主張しない。レビュー完了前に commit/push/CI/受入 run を行わない。

## 所有権修正の再レビュー完了と公開前状態

親から通常レビュー PASS、独立レビュー pass_with_held、socket ownership P2 closed の結果を受領した。対象は ownership-source-fingerprint.json の source_content_digest `1A2DD882D4FCD34518E1BA196DEEB9EDEC2A8633B9A9EA0DD6BE5EED2CD94C43`。公開直前に全5ソースの hash 一致を確認した。上記の「再レビュー待ち」は当時の状態であり、ここで更新する。

許可された次段階は、この intended code/report/evidence を子ブランチへ commit/push し、PR #119 の日本語本文を読み戻して現候補の exact-head CI 完了を確認すること。CI 成功と latest linux/amd64 digest・重複 dispatch 不在の再確認後に、通常 setup 一回の既存 match workflow を一度だけ実行する。#52 の判定は同一 active referee window の双方移動、#54 の POST_GAME は別条件として記録する。TDD の先行証拠がない記録は維持する。


## raw 証跡の公開形式

/proc/net/udp の生ヘッダに元から存在する末尾空白を改変しないため、local-simulator-proc-net-udp.log は元の byte のままローカルに保持し、byte 一致を復号 SHA256 で確認した .log.gz コピーを commit 対象にする。raw SHA256 は F64B68F2E92E25FAA380A17089617BA551252CAB7FF404594D2B9A1146C40186。他の証跡を含め staged diff-check を実施する。

## 現候補 a330c4e の Linux 受入 run 42445437138: #52 motion pass

source content digest を固定した通常レビュー PASS / 独立レビュー pass_with_held と socket P2 closed を確認後、実装・診断証跡を commit `a330c4ee7c8a457cdb7487ccca79ef31ccdb0ea9` として同じ子ブランチへ通常 push した。exact-head CI `42445107235` は completed/success。最新 GHCR linux/amd64 digest が現在 match pin `sha256:9573c7cc1551868010218394147a719a9aefb70469df1a807e85dce0f9aa5554` と一致し、active dispatch が0件であることを再確認してから、既存 workflow `mode=match` を一度だけ dispatch した。base 変更、rebase、stack 同期、#53/#54 branch・PR 作成は行っていない。

- 受入 run: https://github.com/ibis-ssl/Duck/actions/runs/42445437138
- source HEAD: `a330c4ee7c8a457cdb7487ccca79ef31ccdb0ea9`
- event: `workflow_dispatch`、attempt: 1、workflow: `.github/workflows/dotnet-test.yml`
- artifact: `aspire-full-stack-42445437138-1`、artifact ID `13603433270`、artifact digest `sha256:86c804b3cde66eac34e2f6252de85557ba8bc586361558f00ffaf4cfca90b49c`
- recorder: `2026-11-08_04-10-32.log.gz`、24,831,747 bytes、SHA256 `2aa14f54012bffd1f91b4c145c060e13a8d4e0f9a8dbd93ec4614e4a536336ff`。取得後の計算値は宣言値と一致。
- simulator: container `a2e12100de144ecfde530b0d2cad8457d50eab71a2777587d38bdd76cd2fd290`、run identity `gha-42445437138-1`、上記 linux/amd64 exact image digest。

### #52 の primary gate と #54 の分離

`match/motion-acceptance.json` 全体を確認した。`Passed=true`、`RecordStreamComplete=true`、`RecordCount=117336`、`RefereeRecords=17744`、`VisionRecords=27757`。8 active referee windows のすべてで `CraneMovingRobotCount=11`、`TigersMovingRobotCount=11`。counter は 2, 6, 10, 14, 18, 24, 28, 32。したがって現実装 HEAD で #52 / ASPIRE-MATCH-003 の同一 active window における双方の実 packet 位置変化を確認した。旧 run の成功を現 HEAD の証拠へ付け替えた判定ではない。

workflow / full-stack job は success。一方 `match-result.json` は `status=failed`、`stageReached=maximum_time`、`terminationKind=MaximumTimeReached`、`failure=MATCH_MAXIMUM_TIME_REACHED: execution stopped without POST_GAME.`、`finalReadiness.isComplete=true`。`match-acceptance.json` は G1 / MATCH-001/002 の scope で passed、`fullMatchPassed=false`。よって workflow success を full-match success と扱わず、#54 の POST_GAME / 試合完了は未達として別記する。これは #52 motion pass の失敗条件に追加しない。

### 初期 setup 一回の実宛先・時刻・response

`initial_setup_sent_unconfirmed` event は一件で、通常の初期 setup は既存の一回のまま。retry・追加 setup 送信・Crane 変更は行っていない。`initialSetupSimulatorResponse` の raw JSON は次を記録する。

- `destinationIp=172.18.0.7`、`destinationPort=10300`。
- `sendStartedAtUtc=2026-11-08T04:12:25.3914007+00:00`。
- `sendCompletedAtUtc=2026-11-08T04:12:25.3915734+00:00`。
- `status=no_response_observed`、5秒の観測で datagram/remoteEndpoint/responseReceivedAtUtc は null、errors は空。

これは SendAsync が例外なく完了したことと応答未観測を分けた記録である。応答未観測を setup 未受信・未適用と断定せず、空の errors を simulator のエラーなし応答とも扱わない。個別の UDP 10300 packet capture や simulator ACK が取れたわけではない。

### 既存 .NET reader による Vision 時系列相関

受入終了後、repo 外の一時 C# 解析だけを用いた。新しい repo CLI/tool/parser/format は追加していない。既存 `SslLogRecordReader` で全117336 records / 27757 Vision を読み、最初の解析では `t_capture=128.758838071` 等の simulation-relative clock であることを確認した。これを送信 UTC と比較しなかった。

既存 `ReadCore` の `observeRecord` と直後の `publishProgress` callback を一時 C# で併用し、既存 summary の `LastVisionDetectionAtUtc` を receiver-time 最大 watermark として各復号 frame に対応付けた。27,757 frame すべてで watermark を取得できた。これは個々の capture/送信時刻や kernel packet timestamp ではなく、時刻逆行があれば最大値が保持される watermark という制約を残す。

| record | recorder Vision watermark UTC | Referee | Blue #3 (mm) | Yellow #3 (mm) |
| --- | --- | --- | --- | --- |
| 27267（送信直前） | 04:12:25.3795594 | STOP / counter 0 | (-3323.8657, -1118.4153) | (-3399.363, -2799.3213) |
| 27271（送信後の最初） | 04:12:25.3953920 | STOP / counter 0 | (-3326.1567, -1119.065) | (-3399.363, -2799.3213) |
| 27284（送信完了約51.9ms後） | 04:12:25.4434485 | PREPARE_KICKOFF_YELLOW / counter 1 | (-1200.2844, 0.011995243) | (1199.992, 0.0000966988) |
| 27336（約0.25秒後） | 04:12:25.6451544 | PREPARE_KICKOFF_YELLOW / counter 1 | (-1201.4094, -15.030473) | (1139.6422, -43.27192) |

本番 payload 生成関数に `blueTeamOnPositiveHalf=false` を与えた intended は Blue #3=(-1200,0) mm、Yellow #3=(+1200,0) mm。record27284 の全22台を intended と照合した最大位置差は 0.284653 mm。その後の移動も観測した。この run では「送信後に意図配置と一致する Vision 状態を観測し、その後移動した」ことを支持する。一方、これを SimulatorResponse の受信や simulator 内部の適用時刻そのものとは扱わない。また、旧 run424361の未記録部分をこの run の結果で補ったとは扱わない。

一時解析結果は `C:\Users\donabe\AppData\Local\Temp\issue52-run-42445437138\vision-setup-recorder-correlation.json`、一時 source は `issue52-vision-correlation-42445437138\Program.cs`（SHA256 `0DD17E50B9EF688AF0B244CAB400D5EE89A628F80F2156F820462761AFD555A0`）。入力 archive は上記 Actions artifact から取得した。追加の focused tests は実行していない。

### 操作・影響と残る範囲

cleanup は `duck.aspire.owner=duck-apphost AND stack=gha-42445437138-1` に限定され、`created_containers_removed_only=true`、`images_removed=false`、`daemon_settings_changed=false`。終了後の対象 container 一覧は header のみ。実測用 image は保持し、ユーザーの host/network 設定は変更していない。

#52 の motion、レビュー、実装 HEAD の CI は確認済み。#54 の POST_GAME、全試合成功、simulator ACK、11003送信元 tuple の process/container 帰属は別の未確認/未達として残す。TDDの当初先行失敗証拠はなく、完了した事後replayをTDDに読み替えない。次の公開操作は、この結果報告のみの保存・PR本文と完了コメントの読み戻し、必要な最終CI確認後、既に許可された #119 の親branchへの merge であり、新しいfull-stack runやstacking操作は行わない。
