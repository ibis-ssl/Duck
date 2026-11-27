# Issue #128 / I128-UDP-02 実機確認（2026-11-16）

## 対象・制約

- Repository: `ibis-ssl/Duck`、HEAD `2ff447063ad35f423577bfdefdf600c3010e042f`。作業場所は `C:\Users\donabe\Documents\Codex\2026-11-15\task-9\Duck-PR136-FA780`。
- 前回成功記録 `reports/issue-52-simulator-response-observation-20261108120201.md:95` に記載の Docker Desktop CLI `C:\Users\donabe\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe` と context `desktop-linux` を使用した。
- 実行主体は `fa780\donabe`。CLI の存在確認は成功し、`docker info` は `29.9.1 linux/x86_64` を返した。
- AppHost は既存の Debug 出力を使用し、`--Testing:RuntimeHostOnWindows=true --Testing:Mode=base` で起動。新規イメージ build/pull/tag 操作は実行せず、image ID の比較もしていない。
- Game Controller の Match type は操作せず、試合を開始していない。Duck ソース／ファイルをコンテナへコピーしていない。設定変更は行っていない。

## 実行観測

- AppHost 起動後、今回の一意な Compose project `duck-73ceebecc36756054c80` に属する `cm4-sim`、`simulator`、`game-controller`、`crane` の4サービスが稼働した。全 container に同じ `com.docker.compose.project` が付いていることを `docker inspect` で確認した。
- Simulator ログは `command receiver on UDP port 12346` と `feedback sender to 127.0.0.1 base port 50100 synchronized to simulator loop (125 Hz)` を出力した。CM4 ログには `simulator output: 127.0.0.1:12346 (mode 3, 715B)` があった。これらは command/feedback 経路の起動根拠で、Vision UDP 送信成功の証拠ではない。
- Game Controller のログは `224.5.23.2:10020 [eth0]` と `224.5.23.2:11010 [eth0]` の listen を示した。これはコンテナ側 listener の起動であり、Vision packet 受信自体は立証しない。
- Windows 上では `Get-NetUDPEndpoint -LocalPort 10020` が `0.0.0.0:10020`、OwningProcess `10832` を示した。host の IPv4 候補として WSL vEthernet `172.24.96.1`、Wi-Fi `192.168.1.103`、Ethernet `192.168.1.148`、Tailscale `100.127.51.85` を観測した。実際に multicast membership を得た NIC は直接確認できていない。
- `http://127.0.0.1:39108/health/vision` は HTTP 503。応答は `VisionPacketsReceived=0`、`DecodeFailureCount=0`、`Identity=null`、`IsReady=false`、`Reason=NoObservation`。追加の8秒後も同じ値だった。Identity 未設定は readiness/503 の別要因だが、wrapper receive 0 件の説明にはならない。raw UDP datagram count は RuntimeHost の当該診断応答に存在しない。
- `Get-NetIPMulticastAddress` はこの実行環境でコマンドとして認識されず、NIC membership の追加確認はしていない。権限迂回や代替設定は行っていない。

## 根拠・結論

- pinned framework source と Duck 起動引数の静的調査（`reports/issue128-udp02-simulator-vision-start-condition-20261115.md`）から、Simulator は Game Controller の match start を待たず Vision を生成し、送信先は `224.5.23.2:10020`。試合未開始だけを受信 0 件の原因とする根拠はない。
- 実機では Windows listener bind と Docker 上の Simulator/CM4/GC 起動は確認したが、Vision の実 send と Windows への packet 到達は確認できていない。受信 0 件の原因（Simulator の送信実行、Docker Desktop の multicast 境界、host NIC membership 等）は未確定。packet capture は行っていない。
- I128-UDP-02 の解消、full-stack 成功、コード修正の根拠は得られていない。製品コード、PR、Issue は変更していない。

## 後片付け

- AppHost 終了後も試験 container が残っていたため、4 container の project/service labels を確認してから、この一意な Compose project だけに `docker compose --project-name duck-73ceebecc36756054c80 down --timeout 10` を実行した。
- 後確認では同 project の container、network、AppHost/RuntimeHost process は残っていない。イメージと tag には操作していない。

## 追加確認：DCP の RuntimeHost stdout

前節では RuntimeHost が実際に multicast group に join した NIC を直接確認できていないと記載したが、試験後に残っていた Aspire DCP の resource stdout を読み戻し、これを補足する。

- DCP ディレクトリ `%TEMP%\aspire-dcpi3b3iorl.4ix` の `resource-executable-675866f3-296f-4f59-9fee-f084a99d2873.log` が `duck-host-runtime-pseksgms` / RootPID `10832` を示し、同じ DCP ディレクトリ内の `675866f3-296f-4f59-9fee-f084a99d2873_out` が process stdout として対応する。読み取り時に `RuntimeHost SSL-Vision receiver diagnostics` の行だけを抽出し、dashboard token 等を出力・保存しなかった。
- `RuntimeVisionReceiverService.CreateUdpClient` は `JoinMulticastGroup` 成功後だけアドレスを `JoinedInterfaces` に追加し、receiver diagnostics log に出力する。DCP stdout は `Interfaces=100.127.51.85, 172.24.96.1, 192.168.1.148, 192.168.1.103, 127.0.0.1` を記録している。これら5アドレスで RuntimeHost の socket membership join が成功した証拠である。
- `VisionPacketsReceivedTotal=0` が UTC `2026-11-15T21:47:18.783Z` から `21:48:34.782Z` まで4秒周期で続いている。health wrapper count 0 / decode failure 0 と一致する。
- これは Windows application socket に packet が届いて decode された証拠がないことを示す。NIC の NDIS 層に raw frame/datagram が到達したかは証明しない。Simulator の runtime send 成功も未観測であり、二境界は未分離。
- Windows `netsh interface ipv4 show joins` は停止中に読み取り成功したが、実行中には採取していない。Windows `netstat -g` は未対応。PktMon packet capture は起動していない。
- `Stop-Process` で当該 AppHost を終了した後もその project の4 container が稼働していた。所有ラベルを照合後、その一意な Compose project のみ `down` し、container/network/process が残っていないことを再確認した。DCP stdout に終了時 `context canceled` cleanup error があるため、AppHost 単体終了は cleanup 完了の証拠に使わない。

## Second bounded observation：既存 image の診断ツールと併受（2026-11-16）

- 起動前に対象の既存 local tags を read-only `docker image inspect` で確認した：Simulator `ghcr.io/ibis-ssl/framework-simulatorcli:a52b6bd`、CM4 `ghcr.io/ibis-ssl/orion-cm4-sim:d7a2e07c47cf09c6d359e391f1cf2828f4fe7f5a`。両方とも local に存在。image ID は取得・比較せず、pull/install/build/tag 操作も行っていない。
- Compose project `duck-84fd419e9da2f313f580` の Simulator と CM4 コンテナに通常の `docker exec ... /bin/sh` を実行し、既存 command を読み取り確認した。両方とも `tcpdump`、`socat`、`busybox`、`nc` が無く、BusyBox applet `tcpdump`/`nc` も見つからなかった。capture は実行していない。
- RuntimeHost 稼働中、source と同じ socket option (`ExclusiveAddressUse=false`, `ReuseAddress=true`) で一時 .NET `UdpClient` を `0.0.0.0:10020` に bind し、RuntimeHost の log に出た5つの local interface addresses (`100.127.51.85`, `172.24.96.1`, `192.168.1.148`, `192.168.1.103`, `127.0.0.1`) で `224.5.23.2` に join できた。実行中の `netsh interface ipv4 show joins` にも `224.5.23.2` が出た。
- 8秒間の一時 listener は raw datagram を0件受信。直後の RuntimeHost health は HTTP 503 / wrapper receive 0 / decode failure 0。DCP host stdout は `Interfaces=100.127.51.85, 172.24.96.1, 192.168.1.148, 192.168.1.103, 127.0.0.1; VisionPacketsReceivedTotal=0` を UTC `2026-11-15T22:12:28.941Z` まで記録した。
- 現在、受信 socket bind/join が失敗したわけではないが、Docker image 内に送信 packet を捕捉する既存 tool がなく、送信側の実 datagram は未観測。Windows の一時 listener も0件のため、Simulator 未送信と Docker Desktop→Windows 経路欠落のどちらかは確定しない。health 503 の identity=null は受信 0 とは別に扱う。
- 同一 project の `cm4-sim`、`simulator`、`game-controller`、`crane` 4 service を labels で確認後、その project のみ Compose `down` した。container/network/process は残っていない。temporary AppHost stdout/stderr と Aspire DCP logs は、この追記の保存後に削除した。

## 実装判断

証拠は Windows の multicast join が成功したことと、同時 listener が受信しなかったことまでであり、Simulator の送信成否を示していない。既存 image に sender capture tool がなく、Windows の raw host receipt を使える既存 capture tool も適用していないため、送信側未送信か Docker Desktop 経路欠落かを分ける根拠がない。原因依存の修正は行わず、外部権限または新しい観測手段が必要な blocker として扱う。

## Health follow-up / publication status

- Local branch `task/issue-128-cm4-compose-restacked` contains commit `1d2f929c` (`fix(runtime-host): report fresh host vision readiness`), based on PR head `2ff447063ad35f423577bfdefdf600c3010e042f`. The commit changes only `RuntimeVisionDiagnosticsHttpService.cs` and `HostDiagnosticsCompositionTests.cs`.
- At this committed HEAD, ran `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~HostDiagnosticsCompositionTests" -m:1 /nr:false`: 21 passed, 0 failed, 0 skipped. Build emitted existing NuGet vulnerability advisories and xUnit analyzer warnings. This is local test evidence, not an on-device network verification.
- Earlier push attempt was `git push origin HEAD:refs/heads/task/issue-128-cm4-compose-restacked`. The automatic approval reviewer rejected it before execution because pushing to the shared PR branch is externally visible and the transcript lacked trusted user-authored authorization for that exact push. No retry or alternate route was used. `user_message_read` did not return a matching explicit push approval in the visible room history.
- Parent-provided context subsequently confirmed the user's custom rule authorizes commit/push to non-main branches and stated “開発したらpushしてください”; the current instruction explicitly says to hold publication. Therefore the commit remains local. At final readback, PR #136 remains open at remote head `2ff447063ad35f423577bfdefdf600c3010e042f`; no push was performed. Approval-review rejection, prior custom-rule authorization, and current publication hold are recorded as separate facts.
- The Vision receive-0 cause remains unresolved. Existing runtime evidence shows successful Windows multicast joins on five interfaces and zero raw datagrams in the temporary listener for 8 seconds; the existing Simulator/CM4 images have no available packet-capture tool. No send-side capture or privileged Windows capture was performed. Simulator-not-sending versus Docker Desktop-to-Windows packet loss remains unproven.

## Additional existing-diagnostics review (2026-11-16)

- Historical success: `reports/issue-52-simulator-response-observation-20261108120201.md:144` records a prior Linux run where a containerized RuntimeHost logged `RuntimeHost receiving SSL-Vision packets from 224.5.23.2:10020 via 172.18.0.8, 127.0.0.1`; `VisionPacketsReceivedTotal` grew to 14,629. `reports/issue54-implementation-20261108.md:247` records an earlier Linux MATCH motion archive with 18,100 Vision records. These show application-level Vision reception in prior Linux/container runs, not the Windows-host path or the current run.
- The Issue #52 SimulatorResponse observer used `/proc/net/udp`/`udp6` to confirm a local UDP bind before observing a SimulatorResponse on port 10300. It does not expose Vision send count, Vision destination, or raw Vision payload.
- The existing `Tracker.DebugHost` raw-Vision viewer/capture receives `SSL_WrapperPacket` through its application UDP receiver and records packets after receipt. It is not NIC-layer capture or a Simulator send counter, and DebugHost is not registered in the Windows-host runtime profile.
- A bounded availability check ran each already-local Simulator and CM4 image with `docker run --pull=never --rm --network none` and queried only `command -v python3`. Neither image contains `python3`. No AppHost stack was started; the disposable containers auto-removed. No image, tag, network, or setting was modified.
- Windows `pktmon` exists. Read-only `pktmon status` said it was not running; `pktmon start help` describes starting packet capture and event collection. Microsoft Learn states Pktmon requires administrator privileges: https://learn.microsoft.com/en-us/windows/win32/pktmon/packetmonitor/nf-packetmonitor-packetmonitorinitialize. No capture was started. A Windows NDIS capture would require an administrator-approved `pktmon start`/`stop` interval and would create packet-capture state/output; this operation is outside the current authorization.
- No normal-user existing method found here directly observes the Simulator's `224.5.23.2:10020` send or the Windows NIC's raw datagram arrival. Sender-side proof would require an available capture/tracing facility in the existing image or Simulator instrumentation; the checked capture utilities are absent. Windows-side raw capture requires the privileged Pktmon operation above. Existing host multicast joins plus the 8-second UdpClient raw-receive count of zero leave the send-vs-Docker-Desktop-boundary question unresolved.
- No send-side capture, elevated operation, AppHost rerun, code change, commit, or push occurred during this additional review. The local code commit remains `1d2f929c`; publication remains on hold.
