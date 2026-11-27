# Duck #128: helper-free base composition

## 目的と対象

標準 `base` の `visibility_graph` 構成から CM4 C helper を使わずに、固定済みの Simulator / Game Controller / Crane イメージと Duck RuntimeHost を同じ Docker network 上へ置く opt-in 経路を追加した。通常の DCP resource path はこの変更だけでは切り替えていない。既存動作への影響を避けるため、現在は `--helper-free-compose` でのみ選択する。

## 実装した構成

- `scripts/start-duck-simulation.cs --helper-free-compose` が session-lifetime の base Compose 構成を選び、CM4 helper の mount / 起動を省く。host RuntimeHost、persistent stack、comparison / match、Planner の追加 resource は対象外。
- `cm4-sim` を Compose network namespace の owner とし、Simulator、Game Controller、Crane を `network_mode: service:cm4-sim` の follower とする。固定イメージはそのまま使用する。
- Duck RuntimeHost は既存の AppHost/DCP build、identity、health/readiness、cleanup を保ったまま、Compose の `${project}_duck-simulation` bridge にも接続する。Duck は Simulator の multicast Vision を同じ bridge から直接受信する。
- Tracker publisher に任意の `Publish.InterfaceName` を追加した。helper-free base profile は `eth0` を選択し、IPv4 multicast を Compose bridge から送信する。未指定 profile は既存の system routing table 選択を維持する。
- Compose owner/follower の起動・readiness と owner 交替時の followers 再作成を model-level lifecycle で検証する。第三者イメージへの source mount や image build は加えていない。

## 検証

### 自動テスト

対象テストを実行し 9 passed / 0 failed:

- `TrackerPublisherInterfaceTests`
- RuntimeHost tracker publish option binding
- helper-free Compose profile / unsupported mode checks
- Compose owner replacement recreates namespace followers without pulling images

### Docker 実機

通常ユーザー `fa780\\donabe` の Docker Desktop CLI で、Compose project `duck-5c8106d58e08b9b5861c`、DCP run `local-20261118T072320Z-0d51b95d99b145a89168213a2465f4fd` を使用。Docker Compose は既存イメージを `--pull never` で利用した。実機で観測した時点の結果:

- RuntimeHost `/health/vision`: 5,750 Vision packets received / decoded、decode failures 0、Detection から Tracker frames 2,898、Tracker publish failures 0。
- `visionDatagramsForwarded=0`。Duck RuntimeHost の application-level Vision resend は経路に含まれない。
- Crane `/robot_feedback`: CM4 feedback sample に `valid_packet_count=12818`, `invalid_packet_count=0`。CM4 owner / Crane の共有 namespace 内 loopback feedback が届いている。
- Crane の起動直後は `224.5.23.2:11010` Tracker receiver の一時的な未受信警告を出した。後続ログでその警告が止まったことは観測したが、この固定イメージは packet acceptance counter を外部に公開せず、Crane 内の Tracker receive を独立した counter で採取していない。したがってこの実測だけで Crane Tracker acceptance を断定しない。
- 試験終了後に該当 Compose project / AppHost run-id で container と network を照会し、残存がないことを確認した。Docker image、Firewall、security settings は変更していない。

owner recreation は、外部からの `docker compose up --force-recreate --no-deps cm4-sim` により試みた。owner は再作成されたが、同時に Compose followers / AppHost の session 状態が失われ、再起動後の Vision / Tracker / feedback を採取できなかった。この操作は AppHost 管理下の lifecycle 再作成として実行できた証拠にならない。owner recreation 後の readiness・endpoint 再解決・feedback 復帰は未検証とする。

## 未解決

- `--helper-free-compose` は opt-in base 専用経路であり、既定の標準 DCP `visibility_graph` profile から helper mount / target controller を除く global migration は未完了。
- Windows native RuntimeHost と Docker Linux consumers 間の relay-free multicast ingress / egress は未検証。今回確認したのは Duck、Simulator、GC、Crane が同一 Docker bridge / owner namespace 上にある直接通信であり、Windows-Docker boundary の証明ではない。
- Crane Tracker receive の直接 counter と owner recreation 後の functional recovery を再取得する必要がある。
- 作業中の既存差分は commit/push せず保持している。Firewall やその他 security settings は変更していない。

## 2026-11-18 follow-up: independent Crane receive, owner recovery, default cutover

### Crane's own Tracker processing

After the owner recreation in run `local-20261118T073254Z-4e927cb8ca30481d944880e7ec6f3821` / Compose project `duck-aa3e9344c38551a7cf70`, RuntimeHost health reported 8,443 Vision packets decoded, zero decode failures, Tracker frame 4,249, zero publish failures, and ready=true. Crane's own ROS `/world_model` sample contained `tracker_detected: true` and `available_tracker: true`; the Crane process was the publisher. A separate `/robot_feedback` sample reported robot 0, `valid_packet_count=9235`, `invalid_packet_count=0`.

This is independent downstream evidence that Crane processed Tracker data, not just a successful local UDP send count. The exact simulator and receiver packet capture is still not present.

### Owner replacement

The approved `docker compose up --pull never --force-recreate --no-deps --detach cm4-sim` recreated the owner. AppHost remained alive; after recovery all four Compose services (owner, simulator, Game Controller, Crane) were `Up`. Duck's health endpoint remained ready and resumed receiving Vision. Crane `/world_model` again showed `tracker_detected: true`, and the feedback sample remained valid. This owner replacement succeeded with fixed images and no C helper or Vision relay.

The earlier run that ended in Session mode had no persisted monitor exception in its startup log. `ComposeSimulationStackOwner.MonitorAsync` calls `StopApplication()` if owner/follower recovery throws; that path can look like a clean session end because the launcher prints the same message after any zero-exit session. A Console error diagnostic now records the exception before stopping. Repeating the same owner recreation did not reproduce the exit or emit that diagnostic. The original specific exception therefore remains unknown; the recovery path itself is now physically verified.

### Standard entry point and relay removal

The no-argument session entry point `dotnet run --file scripts/start-duck-simulation.cs` now selects the helper-free standard base Compose profile. A no-argument physical run (`local-20261118T074100Z-99b00fbf69a241fe955ff18a096ebc61`, project `duck-2b1b67a0275c683a6542`) reported 1,163 decoded Vision packets, 616 Tracker frames, zero Tracker publish failures, and ready=true; Crane `/world_model` had `tracker_detected: true`; `/robot_feedback` reported robot 0 with 3,818 valid and zero invalid packets.

The RuntimeHost `VisionReceiver:ForwardAddress/ForwardPort` feature, send socket, counters, health fields, and forwarding test were removed. `Testing:Compose:HostNetworkingEnabled=true` is now rejected because its Vision route depended on that relay. No RuntimeHost forwarding is active in the standard path (`visionDatagramsForwarded` was zero in the measured run before the field was removed).

The Docker build context for the Duck RuntimeHost image is `Tracker/Tracker.RuntimeHost/bin/Release/net10.0/publish`, and the directory contains no `.cs`, `.csproj`, or solution files. There is no source bind mount. `Tracker.RuntimeHost.dll` and dependencies are copied into its own runtime image; if the policy forbids any compiled Duck executable code inside containers, this design does not satisfy that stricter interpretation.

### Verification and remaining scope

The current targeted tests passed 12 / 12, including publisher interface selection, AppHost host-runtime model without forwarding, rejection of the old relay-only profile, standard helper-free Compose, and owner follower recreation.

The standard session path is switched. The explicit `--persistent` path still uses the older DCP/helper lifecycle; persistent Compose cleanup/restart was not migrated. `--host-runtime` remains a separate Windows native path with no Vision forward. Direct Windows-Docker multicast ingress/egress remains unverified. The first attempt's exact AppHost exit cause cannot be stated from available logs, even though the same recreation succeeds now.

The two test Compose projects and AppHost run IDs were stopped and checked; no containers or Compose networks for them remain. No images, firewall rules, or security settings were changed. All source changes remain uncommitted and unpushed.

### Windows native RuntimeHost boundary check (no relay)

Run `local-20261118T075145Z-ededcc32e3664474977b52ea3872beb3` started the RuntimeHost as Windows process `dotnet Tracker.RuntimeHost.dll`; Docker contained only the fixed CM4/Simulator/Game Controller/Crane peers, with no Duck RuntimeHost container. The Windows health endpoint returned HTTP 503 after observation: `VisionPacketsReceived=0`, decode status `NoObservation`, `DetectionRobotObservedCount=0`, Tracker publish count 0. Therefore this direct Windows-to-Docker multicast path did not deliver Vision to Windows RuntimeHost during the sample.

Read-only Windows route inspection showed IPv4 multicast routes (`224.0.0.0/4`) on WSL vEthernet (ifIndex 41), Ethernet (ifIndex 8), Wi-Fi (ifIndex 20), and other adapters; interface metrics included Tailscale 5 and WSL vEthernet 15. This is route configuration evidence only, not proof of the selected egress or packet delivery. No firewall or route setting was changed. The exact Compose project/network was stopped and verified absent.

## 2026-11-18 追記：persistent helper-free lifecycle

`--persistent` 起動も helper-free standard base Compose を選ぶよう launcher / AppHost を更新した。Compose owner は AppHost 停止時に project と manifest を保持し、Compose service に stack/run/resource ownership label を付ける。`--stop-persistent` は旧 DCP stack 用の cleanup を維持しつつ、対応 manifest がある場合は Duck RuntimeHost を先に停止し、決定論的 project 名の Compose project を停止して manifest を削除する。

保持と profile selection の新規テストは 2/2 pass。`dotnet run --file scripts/start-duck-simulation.cs -- --help` で launcher のコンパイル・引数表示を確認した。Docker 上での persistent 停止後再起動と `--stop-persistent` cleanup は未検証。したがって標準 session の実機証拠を persistent 実機確認へ外挿しない。

## 2026-11-18 追記：persistent 実機再起動・cleanup

`--persistent` も標準 helper-free Compose を使うよう変更し、Compose owner は AppHost shutdown 時に project / manifest を保持する。`--stop-persistent` は Duck RuntimeHost の後に Compose project を停止する。停止後に同じ stack ID で再起動すると、peripheral 4 containers は同じ Compose project として再利用された。RuntimeHost health は ready=true、Vision 684 packets received。

通常ユーザー `fa780\\donabe`、Docker context `desktop-linux`、CLI 29.9.1。stack `duck128persist20261118a`、project `duck-7833515c1303a641422f`。cleanup 完了後の検査で stack/run label の containers 0、Compose project network 0、manifest なし。Compose/AppHost persistent model・lifecycle tests は 10/10 pass、launcher `--help` compile check 成功。

Aspire は proxyless health endpoint の host port が起動ごとに変わる警告を出し、Duck RuntimeHost container は再作成された。peripheral Compose project / network は保持・再利用された。Windows native RuntimeHost の直接 multicast ingress は依然未解決。image、Firewall、route、security settings は変更していない。
