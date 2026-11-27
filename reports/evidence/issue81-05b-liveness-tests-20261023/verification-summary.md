# NR007/NR009 liveness boundary verification (2026-10-23)

## Source and fixed test hashes

- `Tracker/Tracker.RuntimeHost/RuntimeIdentityControlProtocol.cs`: `b96d255bc7e52fd017bcc9676df994fc5c2331575b1d14cec281d27de7bf67cf`
- `Tracker/Tracker.RuntimeHost/Program.cs`: `4bc18b1139855eeab090fe3c2e197e8f9093c60878fcc7421e792ae97b31117b` (unchanged)
- `Tracker/Tracker.Tests/RuntimeIdentityControlLivenessTests.cs`: `2d136c3d9ad3320c3fcf61fba38e88da56ede44bd309c637b05643b152731745`
- Existing `RuntimeIdentityControlBoundaryTests.cs`: `58c304d7d96c7812a816bd8407c316f1ceb58f22539a1572718446f714ab8993` (unchanged)
- Existing protocol fixture `RuntimeIdentityControlProtocolTests.cs`: `a9ef455a54f5467748d65fa02e86c47f6dbd5fb85586eefa420868e5a1aa7d06` (unchanged from prior baseline).

The new liveness cases are isolated in their own test file. Earlier 797-test success belongs to the previous source and is not reused as this version's full-suite result.

## Runtime Red and Green

- `fatal-cleanup-red-v1`: exit 1, test setup/build succeeded, accept task faulted with SocketException (125, `Operation canceled`) after the listener was disposed. `DisposeAsync` propagated the accept failure. The pre-fix source awaited the accept task before deleting its owned socket path, so this fault skipped later cleanup. The raw test asserted the propagated SocketException; it did not print the pre-fix path observation independently.
- `server-cleanup-green-v1`: exit 0, 1/1. Cleanup now runs in `finally` even when disposal rethrows the captured accept failure; the test expects the original SocketException and verifies its owned path is removed.
- `hosted-supervision-red-v2`: exit 1, 0/1. With only the server-factory test seam added and no monitoring behavior, the hosted `ExecuteTask` stayed pending for one second after the accept task faulted, until host stop was requested.
- `hosted-supervision-green-v1`: exit 0, 1/1. Hosted service awaits the server completion task, exposes the fault through its own task, and disposes the server/owned path in all exit paths.
- `relay-output-red-v2`: exit 1, 0/2. With cancellation fallback corrected in the fake writer, both a never-completing output write and a never-completing flush exceeded the test's 3-second observation window without caller cancellation.
- `relay-output-green-v1`: exit 0, 2/2. A bounded internal operation deadline now applies to connect, relay writes, output writes, and output flush. The test uses a 150 ms deadline to assert bounded completion and then a subsequent valid relay. The operation deadline bounds the relay's wait; an arbitrary third-party TextWriter operation that ignores cancellation may itself continue independently.
- `boundary-regression-v1`: exit 0, 11/11 existing boundary cases.
- `liveness-suite-green-v1`: exit 0, 4/4 new cases.
- `full-unfiltered-green-v1`: exact unfiltered command `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -p:EnableSourceControlManagerQueries=false`; exit 0, 801/801, skip 0.

Initial harness-only failures were retained separately and are not claimed as feature Red: `fatal-loop-red-v1/v2` used brittle reflection into the async state machine; `hosted-supervision-red-v1` incorrectly required the exact runtime type `Task`; `relay-output-red-v1` used a fake flush writer that ignored cancellation even during emergency cleanup. The valid v2 Red generations above use a stable factory seam/direct server fixture and a cancellation-aware fake.

## Builds

- `runtimehost-build-green-v1`: `dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj --no-restore -p:EnableSourceControlManagerQueries=false`, exit 0, 0 warnings / 0 errors.
- `apphost-build-green-v1`: `dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --no-restore -p:EnableSourceControlManagerQueries=false`, exit 0, existing ASPIRE010 warning 1 / 0 errors.

## Existing Docker CLI read-only result

The already-saved work-context probe `docker-cli-existing-engine-readonly.json` records `docker --context desktop-linux info --format '{{json .ID}}|{{json .OperatingSystem}}|{{json .ServerVersion}}'` exit 0: EngineID `5bc5ab8a-7340-47ba-8b7c-9f645375a54c`, OS `Docker Desktop`, ServerVersion `29.9.1`. Existing context evidence is `docker-cli-existing-context-readonly.json`: `desktop-linux`, `npipe:////./pipe/dockerDesktopLinuxEngine`, no `DOCKER_HOST` or `DOCKER_CONTEXT` override. No container create/pull/start/exec/stop/remove was run; DCP's selected engine has not been compared with this CLI context. Docker Desktop GUI version is not inferred from the daemon result.

## Metadata JSON correction

The old 05B boundary bundle contains 23 metadata `.json` files ending in literal bytes `5c 6e` (`\n`) after the JSON object. Their original bytes and hashes remain unchanged. `metadata-companion-v1.json` records each source path, original raw hash/length, parse error, exact stripped bytes, and parsed record. No unrepaired metadata is silently treated as raw-valid. Original raw JSON is preserved; the companion is supplementary.

All invoked test/build processes ended and their exact exit values were captured in the per-run JSON. No Docker/DCP process was started.
