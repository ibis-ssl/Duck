# Duck Local Launch Lifecycle Report

## Metadata and target identity

- Report type: implementation report
- Date: 2026-11-14 UTC
- Repository: `Duck-issue56`
- Branch: `task/issue56-aspire-007b`
- Local implementation commit: `1bda0f960a7481c4e63322ce3fcff880f19dc84f`.
- Remote push status: pending at report update.
- Requested runtime profile: GPT-6 luna/high. Actual profile metadata was not observable, so application is **unverified**.
- Persistence mode: repository file at `reports/issue56-local-launch-lifecycle-20261114.md`.

## Purpose and scope

Provide a simple local PowerShell entry point for the normal Duck base simulation. Session remains the default. Persistent startup uses stable Stack ID and Run ID values and has a scoped cleanup command. Game Controller match setup and UI actions remain manual. Match mode, control-port conflict checks, issue #56 completion, PR #124 merge/closure, Docker settings, dependencies, and image changes are outside scope.

## Requirements and implementation

- `scripts/start-duck-simulation.ps1` prepares the existing runtime and starts the built AppHost. Its default is Session; `-Persistent -PersistentStackId <id>` uses that value for both stable Stack ID and Run ID; `-StopPersistent -PersistentStackId <id>` validates the ID, enumerates stack/run candidates, verifies every candidate's owner/stack/run labels before deleting, and reads back the exact scope.
- `-StopPersistent` now acquires the same exclusive `%TEMP%\duck-aspire-stack.lock` lease as AppHost and holds it across candidate enumeration, label checks, deletion, and readback. If an AppHost or another cleanup holds the lease, cleanup fails before listing or changing containers.
- `PersistentStackId` validation now accepts the same character set required by capture Run IDs (ASCII letters/digits, hyphen, underscore; no dots), and runs before any startup preparation or cleanup.
- AppHost normalizes the lifetime mode once (trim and lowercase), writes the canonical value back to configuration, and uses the same mode for standard and published containers.
- `Testing/Duck.Testing.AppHost/Program.cs` accepts only `session` or `persistent`, requires stable Stack ID and Run ID in Persistent mode, applies Persistent lifetime to published app containers, and adds the owner label to the base debug-host container.
- `Testing/Duck.Testing.AppHost/BaseSimulationResourceRegistration.cs` applies `ContainerLifetime.Persistent` to standard base containers only when configured. Session remains Aspire's default lifetime.
- `Testing/Duck.Testing.AppHost/DebugHostCaptureMountSettings.cs` retains fail-on-existing behavior by default. Persistent base startup can reuse its existing Run ID capture directory only after validating the directory structure and reparse-point constraints. No capture contents were cleared or overwritten during this work; byte-for-byte content integrity was not separately hashed.
- `README.md` now documents default Session startup, Persistent startup/restart with the same ID, exact-stack cleanup, and the manual Game Controller UI boundary.
- README also points to `Tracker/Tracker.Tests/Tracker.Tests.csproj` for current Tracker tests and states that Auto-referee tests are future work, not implemented or verified.

Per-launch fixture manifest, diagnostics, handoff trace, startup-mode, and AppHost log paths avoid collisions and preserve the existing no-overwrite fixture-manifest behavior.

## Validation commands and results

### Parser and build

- PowerShell parser invocation: `[System.Management.Automation.Language.Parser]::ParseFile(...)`; result: 0 parser errors.
- Launcher preparation published `Tracker/Tracker.RuntimeHost`, `Tracker/Tracker.DebugHost`, and `Testing/Duck.Testing.RefereeDriver.Host` in Release, then built `Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj` in Debug. Final build succeeded with 9 existing warnings, including Aspire CLI bundle and package advisory warnings. No dependency versions were changed.
- An initial build failed because the experimental `WithPersistentLifetime()` API emitted `ASPIREPERSISTENCE001`. The implementation now uses the supported `WithLifetime(ContainerLifetime.Persistent)` API; no analyzer suppression or project-level warning configuration was added.
- No automated test suite was run. Only the requested parser/build and bounded runtime checks were performed.
- Review follow-up validation: PowerShell parser reported `0` errors. `dotnet build Testing/Duck.Testing.AppHost/Duck.Testing.AppHost.csproj --configuration Debug --no-restore -m:1 /nr:false` succeeded with `5 warnings, 0 errors` (ASPIRE010, NU1904, two NU1903, NU1902). No Docker/runtime/CI tests were run for this follow-up.
- Preflight used the existing `desktop-linux` engine (`5bc5ab8a-7340-47ba-8b7c-9f645375a54c`, Docker 29.9.1). Required confirmed project images were already cached; images were left untouched.

### Session r4

- Run ID: `local-20261114T032351Z-6bd00ad2`.
- Duck `/health/vision`: HTTP 200; 1,456 vision packets; `ready=true`.
- Crane diagnostic: coordinator/team probe succeeded.
- After Ctrl+C, the AppHost process was absent and the exact stack/run `docker ps -aq` readback contained zero containers. A subsequent `-StopPersistent` invocation was a no-op, also confirming zero. The container disappearance was automatic; no manual removal was used for this run.
- An explicit final Aspire shutdown log line was not captured. The available terminal/transcript output ended with a PowerShell pipeline cancellation message; this limitation is retained rather than presented as a successful shutdown-log capture.

### Persistent r1 and same-ID relaunch

- Stable Stack ID and Run ID: `issue56-runtime-20261114`.
- Initial Persistent launch produced 7 containers, each labeled `persistent=true` and carrying owner/stack/run labels. Duck `/health/vision` was HTTP 200 with 1,885 decoded packets and `ready=true`; Crane coordinator probe succeeded. Debug-host received vision packets, while its comparison endpoint returned 503 with `missingRoles` for Duck, TIGERs, and ER-Force, which are absent from the normal base stack.
- After stopping AppHost, all 7 initial container IDs remained Up. The first same-ID relaunch stopped before resource creation because the existing static fixture-manifest path could not be overwritten (`File.Move(... overwrite: false)`). That invariant was retained; launcher paths were made unique per launch.
- The next same-ID relaunch stopped before resource creation because `DebugHostCaptureMountSettings.CreateCapture` rejects an existing Run ID directory. Persistent-only, validated reuse was added without deleting the existing capture directory. That failed attempt's unique fixture manifest and logs were left in the artifact directory.
- After those changes, same-ID Persistent relaunch succeeded. All 7 current containers were Up and `persistent=true`. Duck health returned HTTP 200; packets increased from 2,445 to 2,887 over 5 seconds, with 2,887 decoded and `ready=true`. Crane coordinator probe succeeded.
- Four IDs were reused; Duck, Game Controller, and DebugHost were recreated during rejoin. The Duck, Game Controller, and DebugHost endpoint configurations allocate dynamic host ports. Captured Duck mappings changed from `25177` to `10828`, and DebugHost mappings changed from `25179` to `10830`; this is a plausible but unproven reason for the recreations. The DCP recreation decision and old/new Game Controller port values were not captured. No further relaunch was attempted.

| Resource | Initial Persistent ID | Rejoin ID | Result |
|---|---|---|---|
| simulator | `64775b3a9c791206ff5f3276e77ab6f5eae999a224dc2fbb656b8971d915cf29` | same | reused |
| cm4-sim | `cdb5eed359f05101bd6667a5fcd0bee87cfa79814380e3f075931375683ee551` | same | reused |
| crane | `1f218ffa92638ac0b9f9cf08c6e9eab7c95ff8b3546a5bd8a678f44145627bfe` | same | reused |
| referee-driver | `2f6b1f7c31f5b898fed84fab8c8d48e424ae525320540c47a2ca8bd18f6f4f7f` | same | reused |
| duck | `23b00a4bcfe72d4ce62105d4ff56c6f221625c0880bdf15aedfa6712acb3f05f` | `a1497ab2df8a9b0132a832536854dd68cb877276bf468ed7e855b28f4f189f4f` | recreated |
| game-controller | `11d51257ebdb28d8766cd2880ee1c2130b592231852f97c703664d65d4ea8a8a` | `52d99cfd9a89d961e6239ee574c4998fa8ec5e417483766d6143dfec8ef4bbbf` | recreated |
| debug-host | `90a222475dd84e8e67fbacdd3c3b24c206212f294f617462b1ad47c3b34e5776` | `02fe71b2d887b8d163eca32a2861d7e30d945425860dc02a4afd33fb98eadcc6` | recreated |

### Exact cleanup

After the final Persistent run, the parent verified each of the following IDs against `duck.aspire.owner=duck-apphost`, `duck.aspire.stack=issue56-runtime-20261114`, `duck.aspire.run=issue56-runtime-20261114`, and its resource label, then stopped and removed only those resources:

| Resource | Removed container ID |
|---|---|
| debug-host | `02fe71b2d887b8d163eca32a2861d7e30d945425860dc02a4afd33fb98eadcc6` |
| duck | `a1497ab2df8a9b0132a832536854dd68cb877276bf468ed7e855b28f4f189f4f` |
| game-controller | `52d99cfd9a89d961e6239ee574c4998fa8ec5e417483766d6143dfec8ef4bbbf` |
| crane | `1f218ffa92638ac0b9f9cf08c6e9eab7c95ff8b3546a5bd8a678f44145627bfe` |
| referee-driver | `2f6b1f7c31f5b898fed84fab8c8d48e424ae525320540c47a2ca8bd18f6f4f7f` |
| simulator | `64775b3a9c791206ff5f3276e77ab6f5eae999a224dc2fbb656b8971d915cf29` |
| cm4-sim | `cdb5eed359f05101bd6667a5fcd0bee87cfa79814380e3f075931375683ee551` |

Parent-reported post-cleanup readback: target stack/run count 0, global active count 0, and shared lock available. Docker images were not removed.

## Evidence sources

- `artifacts/duck-simulation/local-20261114T032351Z-6bd00ad2/` — Session r4 startup mode, health/startup artifacts, and AppHost transcript.
- `artifacts/duck-simulation/issue56-runtime-20261114/` — Persistent initial/relaunch mode files, fixture manifests, crane diagnostics, identity traces, and AppHost transcripts. The first existing fixture manifest was not overwritten or removed.
- `Testing/Duck.Testing.AppHost/reports/evidence/issue56-lifecycle-cleanup-session-r1.ps1` and `.txt` — an earlier Session cleanup inventory. That cleanup followed a cancelled run and is not evidence for Session r4's automatic cleanup.
- `Testing/Duck.Testing.AppHost/reports/issue56-lifecycle-session-r2-transcript.txt` and `issue56-lifecycle-session-r3-transcript.txt` — earlier bounded Session attempts and their pipeline-stop output.
- Parent's exact cleanup and readback confirmation for the final Persistent run.

## Remaining unknowns and risks

- All 7 Persistent containers did not retain their IDs across rejoin; Duck, Game Controller, and DebugHost were recreated. Dynamic host ports are configured for all three resources, but only Duck and DebugHost old/new mappings were captured. Dynamic mapping differences are a plausible but unproven cause because the DCP recreation decision and Game Controller port delta were not captured. Stable host-port behavior and port-conflict handling were not tested or changed.
- The same-code rejoin is not evidence of code hot refresh. Launcher republishes RuntimeHost, DebugHost, and RefereeDriver on each invocation, but unchanged Persistent containers may be reused. No source-code-change refresh was tested; use Session or explicitly stop the Persistent stack before relaunch when updated container code must be applied.
- The final Aspire shutdown log line was unavailable for Session r4 even though process exit and zero remaining containers were confirmed.
- Debug-host comparison readiness was not expected in base mode because match participants are absent. Game Controller UI and match mode were not exercised.
- The requested GPT-6 luna/high runtime profile could not be verified from runtime metadata.
- The local implementation commit is `1bda0f960a7481c4e63322ce3fcff880f19dc84f`; remote push is pending. No CI run for current HEAD is available. PR #124 remains Draft/Open and Issue #56 remains open per the task boundary.

## Next action and merge boundary

Review the documented persistent-recreation limitation and the source changes independently. Do not interpret this report as authorization to merge or close PR #124 or Issue #56.
