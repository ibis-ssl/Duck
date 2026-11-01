# Duck #82 ASPIRE-NET-003 acceptance record

## Scope and implementation

This records the opt-in base AppHost path that adds one DebugHost observer to the existing five-resource stack. The DebugHost listens for SSL-Vision multicast at `224.5.23.2:10020`, has its own raw packet capture, disables tracker transmit/receive, shares the AppHost stack/run ownership labels, and has a dynamic loopback-only HTTP endpoint. Its model test asserts that no UDP endpoint is published. Existing comparison mode and the default base mode remain unchanged.

The source branch was `task/aspire-docker-desktop-496`, starting HEAD `9881a1d7e53cb6b71cce374a8b63dea83342c8dd`. The final local test-only change moved Docker process-PATH restoration into an outer `finally`; it does not change AppHost construction or packet measurement. The live acceptance execution below used the same AppHost and measurement logic before that cleanup-scope-only hardening.

## Validation

- `dotnet test Tracker\Tracker.Tests\Tracker.Tests.csproj -c Release --no-restore -p:RunAspireFullStackAcceptance=true --filter FullyQualifiedName~AppHostApplicationModelTests.BaseModeCanAddDebugHostVisionObserverWithoutPublishedUdp` passed: 1/1.
- After the cleanup-scope-only hardening, the same focused model test passed again: 1/1. Build output included `Duck.Testing.AppHost`, `Tracker.DebugHost`, `Tracker.RuntimeHost`, and `Tracker.Tests`.
- After that hardening, the focused model test and existing `RestoresPathAfterDockerPathScopeExitsWithAnError` test passed together: 2/2.
- `git diff --check` passed. Existing warnings remain: NU1902 for the repository's MessagePack version, ASPIRE010 for its configured Aspire CLI bundle, and pre-existing compiler/analyzer warnings.
- Repository Markdown text/spell lint was not run because local `node_modules`/textlint are absent. No dependency installation was performed.

## Windows Docker Desktop measurement

Preflight: `desktop-linux`, Engine ID `5bc5ab8a-7340-47ba-8b7c-9f645375a54c`; all six required simulator/game-controller/CM4/Crane/runtime-base/ASP.NET-base images were already present locally, and `docker ps -a` was empty. No Docker pull was run. Docker image events for the run contained only local `create`/`tag` events, not `pull` events.

The single passing acceptance command was:

```powershell
dotnet test Tracker\Tracker.Tests\Tracker.Tests.csproj -c Release --no-build --no-restore -p:RunAspireFullStackAcceptance=true --filter FullyQualifiedName~AspireFullStackAcceptanceTests.BaseAppHostAndDebugHostReceiveVisionMulticastDuringTheSameWindow --logger 'trx;LogFileName=issue82-net003-final-window.trx' --results-directory $env:TEMP\duck82-net003-trx
```

The test-runner exit code was 0. The test's measured duration was 26.743 seconds, from approximately `2026-11-01T03:06:52.565Z` through `2026-11-01T03:07:19.308Z`, within the 300-second cap. It started six owned resources on the existing DCP bridge. Duck's loopback HTTP was `127.0.0.1` and returned 200. The only host endpoint checked for DebugHost was dynamic TCP bound to `127.0.0.1`; there was no published UDP endpoint in the AppHost model.

The same 5-second measurement window was `2026-11-01T03:07:07.3670254Z` to `2026-11-01T03:07:12.3738596Z`:

| Observation | Before | After / window evidence |
| --- | ---: | ---: |
| Duck decoded Vision packets | 290 | 604 (increase 314); HTTP 200 → 200 |
| DebugHost decoded Vision packets | 99 | 412 (increase 313) |
| DebugHost raw capture | — | 313 records in the same window, all 313 decoded as detection packets; 313 balls and 6,886 robots |
| Capture timestamps | — | first `03:07:07.3747799Z`, last `03:07:12.3662530Z` |

DebugHost `/health/vision` returned 503 and `IsReady=false` throughout because its identity diagnostic was unattributed. The response still reported the increasing successfully-decoded count, and the raw capture independently showed the same-window datagrams. This readiness state is recorded as an observation; it was not silently treated as ready, and HTTP-200 readiness is not part of ASPIRE-NET-003's stated count/time/content acceptance condition.

The run's six container full IDs were:

| Resource | Full ID |
| --- | --- |
| Duck | `f76925f89bcafb3620026d455d781451d4e3fee4b57d0e9cc2d0c89d5ea1e09d` |
| DebugHost | `f7b008edcf2a8d4d60adc491ae7bee2b5a5409a65dffe53bb6cb07c3b6f05324` |
| Simulator | `cbcd14d1bd1ec6e875e2887586346206043e0f5a733ca634dae1554a2f779ae1` |
| Game Controller | `39e057103f3930b23e487335ddc564ea920b63773cbb0ae509b51b0effad0e12` |
| CM4 simulator | `ad67e3fa1540559f069840bc4b8f5300d4f9bfebf29ad9a45b6e40553a927980` |
| Crane | `c0384c2bb26ea4287b061643a4080cf927c7a712347ec15687d113cde6dd14de` |

AppHost stop/dispose destroyed all six IDs. The pre-rescue owned-ID list and final `docker ps -a` were empty; the pre-run inventory was empty too. No existing other-owner container was present or changed. Two local build images were retained and not deleted: Duck `sha256:af2606bf04790d9dec906353c933af0af04e84c67b6631ccb872e30161e4cafa` (`duck:298ef1e4b4d3e5170790989f1e0b703b9d94c6c7`) and DebugHost `sha256:b25b22faecf8e1e6aa1aa3613ab1f1747d4a1bc976b0048e6165c47cacbd95ed` (`debug-host:4ed20b33f49303487bedd24e2834e560a6b88d96`).

The TRX is at `%TEMP%\duck82-net003-trx\issue82-net003-final-window.trx`; local capture output is under `%LOCALAPPDATA%\Temp\issue82-net003-7bf10de0fbab4110b03d9e7164e0c78f`. Earlier unsuccessful attempts did not reach a same-window measurement and are not counted as acceptance evidence; their owned containers were cleaned. This passing measurement is Windows Docker Desktop evidence only.

## Remaining scope

This completes the measured ASPIRE-NET-003 path on Windows only. It does not complete Issue #82: the other original startup/stop, failure/cancellation cleanup, UDP routes, interface variants, and Linux-specific acceptance remain separate and unverified as applicable. The observed DebugHost identity/readiness 503 is also kept visible for follow-up; this result does not claim DebugHost readiness or Linux execution.
