# Issue #128: relay-free direct UDP 経路の検証

## 結論

Docker image と Duck source mount を変更せず、host-networking profile から UDP relay service を除いた direct-unicast 構成を試作した。Windows RuntimeHost までは Simulator Vision が到達し、RuntimeHost は受信済み datagram を GC と Crane の専用ポートへ送信した。しかし、Crane の world model は Vision geometry / Tracker を受信せず、Game Controller の geometry 更新も観測できなかった。したがって relay 全廃は未達であり、この試作を稼働する最終構成として扱わない。

## 変更した候補構成

- Simulator は `--localhost` を使い、Vision を Windows RuntimeHost の UDP `127.0.0.1:10020` へ送る。
- RuntimeHost は decoded Vision を GC `127.0.0.1:10021` と Crane `127.0.0.1:10022` へ複製する。
- Tracker packet は GC `127.0.0.1:11011` と Crane `127.0.0.1:11012` へ unicast fan-out する。
- GC / Crane の受信ソケットは Linux host namespace 内で `0.0.0.0` に bind する。Crane の pinned launch XML は既存の読み取り専用 `crane-launch-overlay` executable mount で一時 overlay に適用する。Docker image は変更せず、Duck source も container に置かない。
- host-runtime Compose profile から `duck-udp-boundary-relay` と `duck-tracker-udp-ingress` を取り除いた。CM4、Simulator、Game Controller、Crane の4サービスだけで起動する。

## 実測

検証用 Compose project は `duck-556a51121fd1b195d1b3`。使った固定 image は CM4 `d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a`、Simulator `a52b6bd`、Game Controller `3.20.3`、Crane `scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d`。

- RuntimeHost `/health/vision`: 1,359 packet decoded、2,718 datagram forwarding success、0 send failure。2,718 は Vision packet ごとに二 consumer への send を試みた数。
- Crane startup log: launch overlay が Vision `0.0.0.0:10022` と Tracker `0.0.0.0:11012` を適用したことを確認。
- Crane の継続ログ: Vision geometry を未受信、Tracker `0.0.0.0:11012` を未受信と報告。`/world_model` の Vision / Tracker 入力成立を確認できなかった。
- Game Controller は `0.0.0.0:10021` / `0.0.0.0:11011` で listen したが、geometry 更新を示す記録は得られなかった。
- 補助的な正例として、Windows PowerShell から Docker host-networking namespace の一時 listener UDP `12001` に送信した marker が listener に届いた。これは汎用 host-networking UDP 経路の正例であり、製品 consumer の受信証拠ではない。
- Windows から観測済み Linux VM eth0 IPv4 `192.168.65.3:12003` を listener にした別の一時試験では marker を確認できなかった。
- 直前の CM4 owner 再作成記録にある feedback valid packet と namespace follower 再生成の実測は、relays を用いた旧構成の結果である。今回の relay-free 候補で feedback を再試験していない。

## 試験資源

AppHost を停止後、今回の一時 Compose project に属する container が0件であることと生成 Compose manifest が削除済みであることを確認した。`duck-i128-route-probe` 一時 listener も後片付け済み。既存の別 project、Docker image、Firewall / security 設定は変更していない。

## 未解決

RuntimeHost の send API は成功し、同じ Docker host-networking 経路の一時 listener には UDP が届いた一方、実 consumer では受信を確認できなかった。失敗範囲は RuntimeHost の送信後から consumer の UDP socket / packet processing までで、Docker backend 内の具体的な欠落位置は未特定。したがって direct endpoint の再解決、Crane / GC の実 packet acceptance、Tracker return path、owner 再作成後の relay-free feedback は未確認。

追加診断中に Crane container を停止して同じポートの一時 listener に置き換える操作を試みたところ、AppHost 側も終了して health endpoint が閉じた。listener の受信記録は残らず、この試行は証拠に採用しない。作成された一時 resource はすべて削除済み。

最終図はまだ更新しない。relay-free の実受信が確認できず、動作する完成経路が確定していない。実在しない受信・中継要素を図に加えないためである。TIGERs を含める図を作る場合は、TIGERs を含む比較 profile と今回の base host-runtime profile の選択状態を区別する。

## 引継ぎ時の環境確認

再確認時の `whoami` は `fa780\codexsandboxoffline` だった。既知の Docker Desktop CLI (`C:\Users\donabe\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe`) の `Test-Path` は Access denied となり、この実行環境から Docker project の残存状態を再確認できなかった。これは直前の通常ユーザー実行で記録した project cleanup 確認とは区別する。権限変更やユーザー切替はしていない。

## 2026-11-18 consumer 受信の再切り分け

ローカルの Game Controller source (`SslProto/src/external/ssl-game-controller/internal/app/vision/visionReceiver.go` と `internal/app/tracker/trackerReceiver.go`) では Vision / Tracker の両 receiver が `sslnet.NewMulticastServer(address)` を使用していた。直前の試作は `0.0.0.0` の direct-unicast listener と別ポートへの unicast 送信を前提にしており、固定 consumer の受信方式と合わない可能性がある。そこで host-runtime profile を consumer の既定 group に戻す候補へ変更した。RuntimeHost は Simulator の localhost Vision を受信後 `224.5.23.2:10020` に multicast し、Tracker も `224.5.23.2:11010` に multicast する。Game Controller と Crane は既定 group / port のまま、共有する CM4 namespace 内で受信する。

この multicast fan-out は構成試験ではまだ実測していない。次の Docker run は既存の固定 image のみを使い、新規 image build / source mount / relay は追加しない。主な確認点は Game Controller geometry 更新、Crane Vision geometry と Tracker world-model flags、および CM4 feedback の実受信である。CM4 C helper は標準 DCP profile に残っているため、全 helper-free 要件には未達。

## 2026-11-18 relay-free multicast 実機確認

`duck128mc20261118a` 専用起動で、Compose project `duck-34519e4da33029ff876a` と固定 image の CM4 `d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a`、Simulator `a52b6bd`、Game Controller `3.20.3`、Crane `scenario-4063cd31cd5b11b1cc919003907f5f4c527b252d` を一度だけ起動した。CM4 owner / service namespace followers の構成に relay はない。Simulator の実引数には `--localhost` と `--ibis-feedback-addr 127.0.0.1` が含まれていた。

RuntimeHost `/health/vision` は `VisionPacketsReceived=0`、`DecodeFailureCount=0`、`visionDatagramsForwarded=0`、`visionForwardFailures=0` を返した。つまり RuntimeHost ingress 自体で Vision packet を観測できなかった。Game Controller の起動ログは Vision `224.5.23.2:10020` と Tracker `224.5.23.2:11010` を listen すると記録したが、`Geometry changed from` の不完全な一行以外に packet acceptance を確定できる記録は得られなかった。Tracker source 切替も確認できなかった。Crane は `224.5.23.2:11010` から Tracker を受信していないと繰り返し報告し、world model を publish しなかった。RuntimeHost→multicast 送信や consumer 受信の成功は確認できていない。

追加の一回限り Linux→Windows marker を container の `bash` から `host.docker.internal:10020` に送ったが、直後の health も全て0のままだった。shell command は marker の送信要求で終了したが、受信証拠はない。これは製品 Vision datagram の試験ではない。CM4 feedback の受信・再作成後回復も今回未確認。

AppHost 停止後も4 container が `Up` だったため、Compose project / service labels を確認し、今回起動した `cm4-sim`、`simulator`、`game-controller`、`crane` のみを停止・削除した。project container と network は0、生成 manifest と作業ログも temp から削除した。他 project、image、Firewall / security 設定は変更していない。

ソースで確定できる到達境界は、Simulator shared namespace から Windows RuntimeHost listener までが未達であること。下流 consumer が何の packet も受け取らなかった原因は、この実行ではさらに切り分けられない。relay-free と CM4 C helper 全撤去は未達のまま。

## 現在のコード検証

multicast candidate の model / lifecycle / forwarding 対象テストを Debug で再構築して 6 件実行し、6 pass / 0 fail だった。`git diff --check` も成功。NuGet の vulnerability metadata 取得は sandbox socket restriction により NU1900 warning、Aspire010 と既存 analyzer warning が出たが、build/test の失敗ではない。専用 Docker run は上記の ingress 0 結果を受けて停止・削除済み。現 diff は未コミットのまま保全している。

## 2026-11-18 Simulator Vision port reassignment experiment

### Rationale and implementation

The earlier controlled comparison recorded 498 Simulator datagrams at a single Windows UDP listener and 502 valid decoded packets at RuntimeHost, both with `--localhost`. In a second controlled comparison, adding Game Controller to the shared Linux host network namespace stopped Windows reception; stopping it restored reception. `/proc/net/udp` showed wildcard binds on UDP 10020. This supports the shared-port conflict as a likely boundary cause; it does not establish which socket receives each individual datagram.

To test that explanation without changing the pinned images, the host-networking profile now asks the Simulator's existing Simulation Control API (`SimulatorCommand.config.vision_port`) to move Vision output to UDP 10021. Windows RuntimeHost binds 10021, decodes the stream, and uses its existing forwarding feature to publish valid Vision datagrams to multicast 224.5.23.2:10020. Game Controller and Crane retain their existing multicast endpoints. The control request is retried a bounded number of times and waits for a `SimulatorResponse`; this is RuntimeHost application code and adds no container-side helper or separate relay service.

The helper-free host Compose profile itself includes no CM4 helper mount or reconfiguration command. The ordinary DCP `visibility_graph` profile still uses the C helper to route Simulator feedback and retarget after owner recreation. Removing that helper globally without replacing its feedback and recreation contract would break that existing profile; global removal remains incomplete.

### Measurement

- A first AppHost invocation was rejected by its own preflight because `RuntimeHostOnWindows` does not support persistent container lifetime. It exited before creating resources. The corrected invocation used the default session lifetime.
- Run ID: `duck128-port21-20261118-a`; Compose project: `duck-232a3e03c3b8d30720b2`.
- User was confirmed as `fa780\donabe`; Docker operations used the known absolute Docker Desktop CLI path. Pinned Simulator, CM4, Game Controller, and Crane images were used without rebuilding, and no Duck source was mounted.
- RuntimeHost health on UDP 10021 reported `VisionPacketsReceived=2148`, `DecodeFailureCount=0`, `visionDatagramsForwarded=2148`, and `visionForwardFailures=0`. The reported most recent source was `127.0.0.1:58357`. `IsReady=false / Unattributed` means this count alone does not prove Detection attribution or Tracker output.
- Game Controller logged `Geometry changed from`, supporting receipt of forwarded Vision on 224.5.23.2:10020. This proves the geometry update path at that observation point, not full Vision consumer acceptance.
- Crane continued to report no recent Tracker reception on 224.5.23.2:11010 and did not publish a world model. Detection-to-Tracker production and Crane acceptance remain unverified.
- The Simulator's Vision port configuration response was not independently captured in the saved log. The measured RuntimeHost receive count and Game Controller geometry update are consistent with port reassignment and forwarding working, but explicit Simulator confirmation remains pending.
- A targeted .NET test run passed 6/6: `RuntimeSimulatorVisionPortConfigurationTests` plus `WindowsHostRuntimeProfileUsesRequiredUdpEndpointsAndDoesNotRegisterDuckImage` for null/false/true host-network choices. `git diff --check` passed. NU1900 warnings indicate NuGet vulnerability metadata could not be reached; Aspire010 and existing analyzer warnings also appeared.

### Cleanup and remaining work

AppHost shutdown left four containers running. Only the four containers created by this project were stopped and removed by their exact IDs. A subsequent project-label query found no remaining containers or networks. No other project, image, firewall rule, or security setting was changed.

This is a promising direct ingress plus RuntimeHost distribution candidate, not a completed all-consumer solution. Capture the Simulator response, then use valid Detection input and verify Tracker generation, Crane receipt, and feedback after CM4 and Simulator recreation. Do not claim a global C-helper-free result until a replacement for the standard DCP feedback/owner-recreation behavior is implemented and tested.

## 2026-11-18 追記: Simulator Vision port を分離した再測定

### 変更と根拠

過去の比較では、単独の Windows UDP listener が `--localhost` Simulator から 498 件受信し、RuntimeHost も 502 件を decode した一方、共有 Linux namespace に Game Controller を加えると Windows 側の受信が止まり、外すと再開した。`/proc/net/udp` では UDP 10020 の wildcard bind が複数確認されている。これは同一ポート競合を有力な原因とする根拠だが、個々の packet がどの socket に配送されたかまでは示さない。

固定 image を変えず、既存 Simulation Control API の `SimulatorCommand.config.vision_port` で Simulator の Vision 出力を UDP 10021 に変更する設定を RuntimeHost に追加した。host-networking profile では RuntimeHost が 10021 を受信し、有効な Vision datagram を既存転送機能で multicast `224.5.23.2:10020` に送り直す。GC と Crane の既存 multicast endpoint は維持する。設定要求は有限回再送し、`SimulatorResponse` を待つ。コンテナ内 C helper や別 relay service は増やしていない。

標準 DCP `visibility_graph` profile には引き続き C helper がある。helper は Simulator feedback の経路と、Simulator/CM4 の所有コンテナ再作成後の宛先更新を担う。これらを置き換える仕組みなしに helper を全削除すると標準経路が壊れるため、今回その削除は行っていない。host Compose profile は helper mount・起動・reconfigure command を使わないが、全 DCP profile からの撤去とは区別する。

### 実測

- persistent lifetime を渡した最初の AppHost は preflight で終了し、Docker resource を作成していない。Windows RuntimeHost が persistent lifetime を拒否するため、次は既定の session lifetime で起動した。
- Run ID `duck128-port21-20261118-a`、Compose project `duck-232a3e03c3b8d30720b2`。`whoami` は `fa780\donabe`、Docker は確認済み絶対パスで操作した。固定 CM4 / Simulator / GC / Crane image を使い、image build と Duck source mount は行っていない。
- RuntimeHost health は入力 10021 で Vision 2,148 件 decode、decode failure 0、転送成功 2,148、転送 failure 0。直近の送信元は `127.0.0.1:58357`。health の `IsReady=false / Unattributed` のため、この数だけでは Detection attribution や Tracker 生成を証明しない。
- Game Controller log に `Geometry changed from` があり、RuntimeHost 転送先 multicast 10020 で geometry 更新を受けた地点まで確認した。GC の全 Vision acceptance ではない。
- Crane は Tracker `224.5.23.2:11010` の受信がない警告を継続し、world model を発行しなかった。Detection → Tracker 生成と Crane acceptance は未確認。
- Simulator の設定応答ログは保存されていない。RuntimeHost の受信数と GC の geometry 更新は port 分離・転送が動いた測定と整合するが、Simulator からの明示的な設定 ACK は未採取。
- `RuntimeSimulatorVisionPortConfigurationTests` と host-networking model test を実行し、最終実行は 7 passed / 0 failed。`git diff --check` も成功。NuGet vulnerability metadata 取得の NU1900、既存 Aspire010 / analyzer 警告あり。

### 後片付けと未完了

AppHost shutdown 後もこの project の4 container が稼働していたため、自分が作成した正確な container ID だけを stop/rm した。project label による後確認では container と network は0件。別 project、image、Firewall / security 設定は変更していない。

次は Simulator の ACK を観測可能にし、Detection を含む入力から Tracker 生成、Crane 受信を確認すること。CM4 feedback と owner recreation 後の宛先再解決も helper-free では未検証。Issue #128 の全 consumer 完了や C helper 全撤去は未達である。

## 2026-11-18 続報: 成功応答の有無と Tracker 出力境界

### Simulator の応答

Pinned `framework-simulatorcli:a52b6bd` に対応する Simulator ソースでは、Simulation Control の `SimulatorResponse` は parse error 等のエラー時のみ送信し、成功した `vision_port` 更新は `setPort()` を呼ぶだけで成功応答を設定しない。TeleportRobot も成功時に応答を送らない。根拠: https://github.com/ibis-ssl/framework/blob/a52b6bd/src/simulator/simulator.cpp (SimulatorCommandAdaptor::handleDatagrams / handleSimulatorError)。したがって成功 ACK の取得は実装・環境の欠陥ではなく、この pinned 実装が成功応答を出さないことによる制約。前回までの応答待ち・再送は撤去し、設定・初期配置 command を一度だけ送る形に直した。health は「command を送った」状態と実際の Detection 観測を分ける。

### 今回の測定 (run `duck128-port21-trackerdiag-20261118-e`)

- 実行ユーザー `fa780\donabe`。既定 session lifetime、固定 CM4 / Simulator / GC / Crane image を使用。Simulator `vision_port=10021` と yellow robot 0 の初期配置 command を一度ずつ送信した。
- RuntimeHost health: Vision 336 件 decode、decode failure 0、Vision forward 336、forward failure 0。`DetectionRobotCommandSent=true`、yellow robot 0 を含む実 Detection を 336 件観測。これは実 Simulator の Detection packet であり、手動 marker ではない。
- Tracker pipeline health: active profile `sim`、latest frame 163、ロボット22・ボール1。UDP publisher の local `PublishSuccessCount=163`、`PublishFailureCount=0`。この成功数は `UdpClient.Send` が例外なく完了した計数で、consumer 受信証明とは区別する。
- GC は前 run 同様 `Geometry changed from` を記録し、RuntimeHost → multicast Vision 10020 → GC geometry の経路を確認。
- Crane は `Tracker受信が直近1秒ありません (224.5.23.2:11010)` を繰返し、world model を発行しなかった。固定 Crane log は `Listening on 224.5.23.2:11010 [services1 eth0 docker0]` を示す。共有 network namespace の `/proc/net/igmp` は 224.5.23.2 membership を示し、`/proc/net/udp` の UDP 11010 socket は bind 済みだが receive queue は0だった。
- ここまでの証拠で Vision ingress、Simulator Detection、Tracker frame 生成、publisher の local send 呼出しは通る。一方 Linux namespace の Crane socket は datagram を受け取っていない。欠落境界は Tracker publisher send 後から Crane UDP socket delivery 前。local send counter だけで network egress を成功と扱わない。

### CM4 helper の状態

Helper-free Compose profile は CM4・Simulator・GC・Crane が CM4 owner と同じ Compose network namespace を使い、CM4/Simulator の既定 loopback と固定 Crane target overlay を保つため、C helper mount / server / reconfigure command がない。標準 DCP `visibility_graph` profile には C helper が残る。これはその profile の CM4 feedback UDP 経路と owner 再作成後の target invalidation/apply を担う。置換なしに撤去すると既存経路が失われるため、標準 DCP の global helper-free migration は未完了。今回 Crane に Tracker が届かなかったため、CM4 feedback もこの run では検証できていない。Compose owner を再作成した後の follower 再作成・loopback 回復も未検証。

### 後片付け

Run `duck128-port21-trackerdiag-20261118-e` の4 container は AppHost 終了後に残存を確認し、当該 project label の container ID のみ stop/rm した。後確認で当該 project の container/network は0件。別 project・固定 image・Firewall/security 設定は変更していない。

次の必要作業は Windows RuntimeHost から multicast Tracker 11010 が Docker Linux namespace に届かない理由を一段掘り下げ、Crane が受信するところまで修正すること。その後に限り CM4 feedback と owner Compose 再作成を確認できる。標準 DCP 全 helper 撤去には別の置換設計が必要である。

### 追記: 継続確認とテスト (2026-11-18)

- 通常ユーザー `fa780\donabe` と既知の Docker Desktop CLI 絶対パスを使用し、`duck-6ae9546c12cb91eb2e91` project label の container / network を読み取り照会した。該当する残存資源は 0 件。
- `RuntimeSimulatorVisionPortConfigurationTests`、`RuntimeVisionReceiverForwardingTests`、`HostDiagnosticsCompositionTests`、host-networking profile model test の対象 .NET テストは 33 passed / 0 failed。
- より広い AppHost model test は `BaseModeDoesNotAddComparisonOnlyResources` で失敗。失敗は AppHost の `DockerIdentitySettingsResolver` が `docker context show` を PATH 上の `docker` で起動できないためで、モデル assertion まで到達していない。実機 Compose 起動はせず、試験プロセスを中断した。
- `git diff --check` は成功。既存の未コミット差分は保持している。

### 次の切り分け

Detection を含む Vision と Tracker frame 生成は確認済みだが、Windows RuntimeHost の Tracker multicast は Crane と共有する Linux network namespace の UDP 11010 socket に届いていない。次は送信元 interface/TTL と Windows Docker Desktop backend の namespace ingress を照合し、既存の承認済み構成での再現条件を比較する。Crane 受信が成立する前に CM4 feedback / owner 再作成後の更新を成功扱いしない。

CM4 helper 全廃は未達。現行 helper-free host Compose profile は標準 DCP `visibility_graph` profile の CM4 feedback と owner 再作成後の宛先更新を置き換えていない。固定イメージと helper 全廃を両立させる、標準 profile 用の代替通信・再解決設計と検証が残る。

### 追記: 送信契約・relay 評価 (2026-11-18)

- Tracker publisher は `UdpClient(AddressFamily.InterNetwork)` を生成し、`Send(payload, length, 224.5.23.2:11010)` を実行する。送信 socket に interface index / local address を指定する設定はない。Crane は固定起動時に `224.5.23.2:11010` を listen し、`services1` で group join 済み。address / port / multicast 契約は一致する。Run `duck128-port21-trackerdiag-20261118-e` では Crane socket の receive queue が 0 のため、送信呼び出し後から namespace 内 socket 到達前の境界に欠落がある。
- 現在の Windows ルート表には `224.0.0.0/4` の経路が WSL vEthernet と複数 NIC にあり、接続済み WSL vEthernet の interface metric は 15、Ethernet は 25、Wi-Fi は 35。これは WSL vEthernet が優先され得るという仮説を支えるが、実行時の選択 interface を packet capture した証拠ではない。
- Host-runtime Vision 経路は Simulator の `--localhost` / UDP 10021 送信を RuntimeHost が受け、デコード後に `224.5.23.2:10020` へ再送する。これは明確な application-level relay であり、名前や実装場所を変えても本人の中継全廃条件を満たさない。直近の Game Controller geometry 更新はこの relay を使った途中診断の証拠に限る。
- Relay-free 候補は Simulator が直接 multicast `224.5.23.2:10020` に出し、同じ Linux network namespace 内の Game Controller / Crane が同一 datagram を直接受信する形。ただし現行 Windows native RuntimeHost は Linux VM ingress を受信できなかった過去実測があり、固定 Simulator の単一 Vision 出力を Windows と Linux consumers の双方へ fan-out する設定も確認できていない。従って現在の制約では全 consumers 同時成立は未達で、未検証の route / firewall 変更は行わない。
- 本環境の既定 sandbox user は `fa780\codexsandboxoffline` で、既知 Docker CLI path の読み取りも拒否された。本人指定の通常ユーザー手順に従い `require_escalated` 下で `whoami=fa780\donabe`、absolute Docker CLI、process-only PATH を使用。`docker context show` は `desktop-linux` を返し、以前失敗した AppHost model test を再実行して 1/1 成功した。永続 PATH 変更はない。
- この追記時に新たな Compose run は起動していない。前回 run の project-label cleanup は 0 container / 0 network を確認済み。

標準 DCP の CM4 feedback は CM4 / Simulator を同一 owner network namespace へ配置し、loopback UDP の direct delivery に置き換える Compose profile が既存実装にある。Owner 再作成時は Compose lifecycle が namespace follower を再作成する。ただしこれは標準 DCP path への全面適用・Crane feedback 再受信まで検証した証拠ではなく、今回の host-profile relay-free 欠落を解決した扱いにはしない。

- 追加テスト: process-local PATH を設定した通常ユーザー `fa780\donabe` のみで `AppHostApplicationModelTests.BaseModeDoesNotAddComparisonOnlyResources` を再実行し、1/1 成功 (`docker context show=desktop-linux`)。設定前の `fa780\codexsandboxoffline` では既知 Docker path の読み取りがアクセス拒否され、同じテストが CLI 起動前に失敗していた。
- Compose topology / owner-recreation の既存モデル試験 2 件が成功。これは helper-free namespace 設計と follower 再作成ロジックの単体・モデル証拠であり、実機の CM4 feedback packet または標準 DCP global migration を証明しない。

### 追記: standard DCP への適用範囲 (2026-11-18)

既存 Compose host-runtime 実機記録 `reports/issue128-cm4-feedback-recreate-20261118.md` では、CM4 / Simulator / Game Controller / Crane が `network_mode: service:cm4-sim` を共有し、Crane の `/robot_feedback` が owner 交換前に `robot_id=0`, `valid_packet_count=14308`, `invalid_packet_count=0`、owner 再作成後に `valid_packet_count=2101` と受信した。これは Compose 共有 namespace による CM4 feedback と follower 再作成後の復帰を示すが、同 run の RuntimeHost/Crane 境界には別の UDP relay があったため、全体の relay-free 成功ではない。

標準 DCP (`ComposeStack == null`) は個別 Aspire/DCP `ContainerResource` を作り、CM4 helper mount、Simulator への実 CM4 feedback IPv4 注入、helper target controller を使う。helper-free Compose 実装は全 peripheral peers を Compose project の user-defined network と owner namespace に所属させる。この方式を標準 DCP に適用するには、Duck RuntimeHost container もその Compose network に確実に接続し、AppHost の image build/readiness/cleanup と owner 交換ライフサイクルを整合させる必要がある。現在は peripheral stack だけを Compose 化する設定は標準 base に存在せず、Duck container を Compose network に接続する契約・再接続処理もない。単に `ComposeStack` を標準 path に渡すと Duck と multicast consumers が別 network に分かれるため、これを完了条件達成と見なせない。

Compose owner recreation / followers re-create の既存モデル test は今回 2 件再実行し成功した。新規 Docker run は前回と重複するため行わず、CM4 feedback 実機記録は上記既存記録を参照した。標準 DCP 全移行は未実装であり、次は Duck container を同じ Compose bridge に置く lifecycle-safe な構成を追加し、その後 helper mount/target controller を撤去し、Vision / Tracker multicast と Crane feedback を実機で測る必要がある。

## 2026-11-18 Windows native direct multicast follow-up

Run `local-20261118T081119Z-623d22bea8fd4af2a9d95edf34c2fca5` / Compose project `duck-0293c6c9232f8a29c11f` used the pinned Simulator/CM4/Game Controller/Crane images and a Windows-process RuntimeHost, with no Vision relay and no security or network setting changes. RuntimeHost health returned HTTP 503: Vision received/decoded 0, tracker frames 0. Windows `Get-NetUDPEndpoint` showed a listener bound at `0.0.0.0:10020` (PID 9836). `netsh interface ipv4 show joins` showed `224.5.23.2` membership on ifIndex 41 (WSL vEthernet), 8 (Ethernet), 20 (Wi-Fi), 31 (Tailscale), and loopback; this is membership evidence, not proof of packet arrival.

Simulator command line had no `--localhost`; it used `--ibis-port 12346` and no Vision destination override. Simulator shared the Compose owner namespace at `172.18.0.2/16`, gateway `172.18.0.1`; `/proc/net/route` showed its default IPv4 route on `eth0`. The Game Controller in that same namespace logged listening on `224.5.23.2:10020 [eth0]` and a Geometry change at startup, confirming it received the simulator's Vision in Linux. This narrows the failure to the Docker Linux to Windows multicast boundary or later Windows socket delivery; no Windows packet capture was available, so it does not identify the exact drop point.

Windows route inspection listed `224.0.0.0/4` routes across several adapters, but `Find-NetRoute -RemoteIPAddress 224.5.23.2` returned no matching object and ordinary-user `Get-NetIPInterface` was denied. The actual Windows egress choice therefore remains unknown. RuntimeHost already bound the right port and had group membership on the WSL interface, so a config-only interface-address change is not evidenced as a fix. No route/firewall change or relay was attempted. The run's four Compose containers, network and manifest were removed and verified absent.

This remains an experimental boundary result. The separate helper-free Compose profile embeds the compiled `Tracker.RuntimeHost.dll` in Duck's image, so it does not satisfy a strict policy that forbids any Duck executable code in containers. It is not a final product topology or a verified direct Windows receive solution.
