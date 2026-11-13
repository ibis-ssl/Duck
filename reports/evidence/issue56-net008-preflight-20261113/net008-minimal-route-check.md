# Issue #56 NET-008 execution route and result (2026-11-13)

## Result

The initial read-only preflight did not start a runtime. After identifying the exact missing AppHost pass-through and using the address recorded by the same existing NET-003 test shape, I added a narrow AppHost setting and ran one focused NET-008 acceptance. Docker Desktop host-network settings were not changed or inspected through the UI.

The existing AppHost route does not currently provide a valid, known IPv4 address to the VisionReceiver container before it joins multicast. Supplying an address inferred from an older DCP run would be a guess: DCP allocates each run's container address dynamically, and the old `acceptance-result.json` records other source endpoints but not the receiver container's address. The existing base DebugHost registration sets `VisionReceiver__MulticastAddress` and `VisionReceiver__Port`, but does not forward `VisionReceiver__InterfaceAddress`. The only current AppHost `InterfaceAddress` passthrough is `Testing:RefereeDriver:InterfaceAddress` to the optional RefereeDriver, which is a different receiver and multicast endpoint (224.5.23.1:11003).

Before this follow-up, the current NET-003 live test started Duck and DebugHost and verified both counters advance, without explicit VisionReceiver configuration or a receiver stop operation.

## Follow-up implementation and live result

- Added the AppHost input `Testing:Duck:VisionInterfaceAddress`, forwarded to Duck as `VisionReceiver__InterfaceAddress`. The AppHost model test verifies this pass-through and confirms DebugHost remains unconfigured by default.
- Reused the existing `BaseAppHostAndDebugHostReceiveVisionMulticastDuringTheSameWindow` live acceptance path. Prior evidence from this exact test shape recorded Duck's interface as `172.18.0.5`; the run supplied that value and required `docker inspect` to confirm the active Duck container actually owned it before asserting reception.
- The live test passed 1/1 in 38.054 seconds. Explicit Duck receiver diagnostics showed only `172.18.0.5`; Duck decoded `332 -> 645` and default-interface DebugHost decoded `144 -> 457` in the shared five-second window. Aspire's `ResourceCommandService` stopped DebugHost, which reached `Exited`; Duck then decoded `1026 -> 1339` over a further five seconds.
- DebugHost health was HTTP 503 / not ready in this stack even while its receive count increased. The receiver-independence result is grounded in Duck's count advancing after DebugHost stopped.
- Preflight recorded Tailscale up (`100.127.51.85`), Ethernet 2 up (`192.168.1.148`), WSL virtual Ethernet up (`172.24.96.1`), and Wi-Fi disconnected. The container chose the explicit `172.18.0.5` interface. Docker Desktop host-network state remains unknown.
- Docker event capture started before launch; its saved run-filtered event query has 75 events. The live run cleaned its six owned containers. Readback found no run-labelled containers, no global active containers, no AppHost/dotnet/DCP processes, and both shared and run-specific locks exclusively openable.
- One initial invocation failed before test execution due to a compile error (`List<string>` passed instead of `string[]`); it started no containers. The code was corrected with `.ToArray()` and one live run then passed. No runtime retry was made.
- Live evidence is under `reports/evidence/issue56-net008-runtime-20261113/issue56-net008-20261113-mwf2oh2v-p5ngj5/`; the model test TRX is `reports/evidence/issue56-net008-preflight-20261113/test-results/net008-apphost-model.trx`.

### Sequential receiver start follow-up

- Independent review identified that the first run proved stop isolation but did not separately measure starting a receiver while the peer was already active. The same test path now uses Aspire's `StartCommand` after stopping DebugHost.
- Run `issue56-net008-start-20261113-144500` passed 1/1: Duck explicit-interface reception was 339 -> 653 in the shared window; omitted-interface DebugHost was 145 -> 459. Duck continued 1037 -> 1350 while DebugHost was stopped. Its next baseline, 1351, was sampled immediately before `StartCommand`; the 1873 follow-up was sampled after DebugHost reached `Running` and the test waited an additional five seconds. That measured interval includes the command and resource startup, followed by the five-second wait.
- Exact-run Docker event query returned 90 records, showing DebugHost's stopped generation destroyed and a second generation created/started. Post-run readback found no run containers, no global active containers, no task processes, no busy target ports, and the shared lock exclusively openable. The initial background event-stream invocation failed due to argument construction; the exact-run bounded query succeeded and is preserved.
- The latest run's preflight file does not contain an adapter snapshot. The previous run's pre-run adapter snapshot shows Tailscale, Ethernet 2, and WSL virtual Ethernet Up, with Wi-Fi disconnected. The latest post-run snapshot shows Tailscale/Ethernet Up and Wi-Fi disconnected; its IP table records WSL virtual Ethernet `172.24.96.1` as preferred but not its adapter status. It is timestamped 14:48:55Z and is not presented as a measurement taken during the test. The actual Duck interface `172.18.0.5` is recorded in `test-output.txt` and verified against `docker inspect` in the test.
- Evidence: `reports/evidence/issue56-net008-runtime-20261113/issue56-net008-start-20261113-144500/` (`test-results/net008-start-live.trx`, `test-output.txt`, `docker-events.jsonl`, `docker-container-lifecycle.csv`, `cleanup-readback.txt`, `host-network-postrun-snapshot.txt`).

## Source evidence

- `Testing/Duck.Testing.AppHost/Program.cs`: base-mode DebugHost still gets the VisionReceiver group and port without an InterfaceAddress setting; Duck now accepts the focused `Testing:Duck:VisionInterfaceAddress` pass-through. `Program.cs:788-800` remains the separate RefereeDriver address path.
- `Tracker/Tracker.RuntimeHost/RuntimeVisionReceiverOptions.cs:25-27` and `RuntimeVisionReceiverService.cs:232-305`: an explicit VisionReceiver address must parse as IPv4 and is used directly for multicast membership; absent configuration triggers local-address discovery.
- `Tracker/Tracker.Tests/AspireFullStackAcceptanceTests.cs:2774` onward: the live test now optionally sets an explicit Duck interface, verifies it against `docker inspect`, then stops and starts DebugHost through Aspire resource commands while confirming Duck's counter continues through both transitions.
- `reports/issue56-windows-docker-desktop-acceptance-20261113.md` row NET-008 reflects the focused start/stop pass; Issue #56 overall, NET-007, NET-009, and the original host-network prerequisite remain incomplete/unknown.

## Just-in-time preflight

- `docker ps`: empty.
- AppHost / .NET processes: none.
- `%TEMP%\duck-aspire-stack.lock`: exclusive open succeeded.
- TCP listeners and UDP endpoints on 8082, 10020, 11003, 11010, 12345, 12346, 10300, 39110, and 39111: none observed.
- Existing image cache includes the e969 acceptance images; no image was pulled.
- The previously assigned `issue56_acceptance_resume` remains running and has not replied to a safe-boundary query. It was not interrupted. No same-stack operation was visible in this preflight; its future operation state remains unknown.

## NET-008 completion boundary

The two focused live runs provide the omitted and explicit interface cases, and the latest run includes both DebugHost stop and start transitions while Duck continues to receive. Host VPN/Tailscale/multiple-NIC conditions are recorded separately from the selected container address. The host-network setting was not changed and remains unknown; NET-009 and Issue #56 remain incomplete.

The live runs did not alter Docker Desktop host-network settings, host interfaces, network configuration, permissions, authentication, or dependencies. NET-008's focused receiver interface and start/stop independence criteria passed; NET-009 and Issue #56 remain incomplete.
