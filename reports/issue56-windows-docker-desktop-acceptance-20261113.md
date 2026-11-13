# Windows Docker Desktop acceptance report - Duck Issue #56

## Scope and result

This is the Windows Docker Desktop acceptance attempt for `ASPIRE-007B`, limited to `ASPIRE-NET-001` through `ASPIRE-NET-009`. `ASPIRE-MATCH-001` through `ASPIRE-MATCH-005` were out of scope and were not run.

**Overall: BLOCKED before acceptance execution.** The acceptance command `docker version` did not resolve from this process's PATH. Issue #55 records a per-user Docker Desktop CLI path, but this task process runs as `fa780\codexsandboxoffline`; a read-only access check against the known `donabe` path returned Access denied. Docker Desktop processes are visible on the host, but the engine connection and required host-networking configuration could not be verified from this process. No acceptance case ran. This is an access/runtime setup blocker, not a test failure or pass, and it does not establish that Docker Desktop or its CLI are absent from FA780.

## Scope verification

- Checkout: `task/issue56-aspire-007b`, based on `109f4d93ce9770eb9fbcbfd3343af7a87571a9ca`.
- The current acceptance design is `Tracker/Design/Testing/aspire-simulation-test-environment.md`, section `Network acceptance`, lines 434-442. `Tracker/Design/tasks-status.md` lists prerequisites `ASPIRE-006E` and `ASPIRE-006G` and requires Windows Docker Desktop with host networking; this run does not claim those prerequisites complete.
- The verifier's `gh issue view 56 --repo ibis-ssl/Duck` attempt could not reach GitHub because of socket access policy. The parent read the live Issue before this run, and the separate normal reviewer later inspected the live Issue successfully; no discrepancy with the local completion criterion was reported.
- The verifier could not resolve `work-context-manager`, `report-output-manager`, and `report-writer` with its skill loader. The parent read and applied these skills for the task.
- The prior #55 report `reports/issue55-docker-path-runtime-fix-20261112.md` records the per-user CLI location and PATH lookup failure. The prior #55 conversation records a successful absolute-path Engine connection on FA780. That history does not prove the current task token can execute that CLI or connect to the Engine.

## Environment and Docker access

- Host: `FA780`. Current process identity: `fa780\codexsandboxoffline`. Its profile and LocalAppData roots are under `C:\Users\donabe`.
- OS: Windows 11 version 23H2, build `22631.6199` (64-bit; version identified by build mapping). Registry values were `ProductName=Windows 10 Pro`, `EditionID=Professional`, `DisplayVersion=23H2`, `CurrentBuild=22631`, and `UBR=6199`; raw values are preserved in `evidence/issue56-windows-docker-desktop-acceptance-20261113/windows-registry-capture.txt`. Microsoft identifies build 22631.6199 as Windows 11 version 23H2 ([KB5068865](https://support.microsoft.com/en-us/servicing/os/windows-11/2025/11/november-11-2025-kb5068865-os-build-22631-6199); [Windows release information](https://learn.microsoft.com/en-us/windows/release-health/windows11-release-information)).
- Current-process `Get-Command docker.exe` found no command; no Docker entry was present in this process's PATH. `DOCKER_HOST` and `DOCKER_CONTEXT` were empty.
- The prior #55 record gives the per-user CLI path as `C:\Users\donabe\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe`. This task's read-only `Test-Path` against that exact path returned **Access denied**. No retry, elevation, identity switch, ACL change, PATH change, Docker configuration change, or alternate engine-endpoint probe was performed. File existence and effective execute permission for the current token therefore remain unverified.
- Docker Desktop-related processes (`Docker Desktop`, `com.docker.backend`, `com.docker.build`, `docker-agent`) were visible on FA780. Process presence does not establish Docker Engine health or current-token connectivity.
- Docker Desktop/Engine version, Engine connection, Linux-container mode, Docker network, host-networking setting, and relevant network configuration: **not verified**. `wsl --status` returned `Wsl/E_ACCESSDENIED`.
- No alternate runtime, unicast fallback, Linux result, or alternate networking mode was used.

## Acceptance cases

No acceptance case was executed. Sent/received packet counts and source identity were not measured and are N/A; the execution count for each case is 0. The design requirements are summarized below; no result is implied.

| ID | Acceptance intent from current design | Result | Executions | Sent/received packet counts | Source identity | Evidence |
|---|---|---|---:|---|---|---|
| ASPIRE-NET-001 | Selected resources, Duck, DebugHost, RefereeDriver, and readiness checks share the DCP bridge; AppHost/DCP own lifecycle; verify start/cancel/failure/duplicate/restart/stop ownership and shutdown within 15 seconds using full IDs. | BLOCKED - Docker access unavailable to this process | 0 | N/A - not measured | N/A - test not executed | Setup/access probe evidence only |
| ASPIRE-NET-002 | Within 5 seconds of stable start, Duck's own `VisionPacketsReceivedTotal` shows at least 10 newly decoded detections; independent observers cannot substitute. | BLOCKED - Docker access unavailable to this process | 0 | N/A - not measured | N/A - test not executed | Same evidence directory |
| ASPIRE-NET-003 | Duck and DebugHost receive the same 10020 input in one window; record counts, input times, and detection content. | BLOCKED - Docker access unavailable to this process | 0 | N/A - not measured | N/A - test not executed | Same evidence directory |
| ASPIRE-NET-004 | TIGERs and ER-Force receive 10020 and Game Controller 11003 and produce independent tracking output; Game Controller alone produces 11003. | BLOCKED - Docker access unavailable to this process | 0 | N/A - not measured | N/A - test not executed | Same evidence directory |
| ASPIRE-NET-005 | DebugHost receives TIGERs and ER-Force 11010 as distinct sources and confirms freshness in one finite window. | BLOCKED - Docker access unavailable to this process | 0 | N/A - not measured | N/A - test not executed | Same evidence directory |
| ASPIRE-NET-006 | DebugHost simultaneously receives Duck 11010, maps all three tracker roles to unique identities, and verifies required delivery to Game Controller and other receivers. | BLOCKED - Docker access unavailable to this process | 0 | N/A - not measured | N/A - test not executed | Same evidence directory |
| ASPIRE-NET-007 | Using observational RefereeDriver and the Windows browser UI, move Game Controller from `HALT` to `running`; confirm via API and internal 11003 receive, required UDP round trips, and at least 100 mm visible movement. | BLOCKED - Docker access unavailable to this process | 0 | N/A - not measured | N/A - test not executed | Same evidence directory |
| ASPIRE-NET-008 | Verify receive with container `InterfaceAddress` omitted and explicit IPv4; starting/stopping one receiver does not stop others; record host VPN/Tailscale/multiple-NIC conditions separately from container selection. | BLOCKED - Docker access unavailable to this process | 0 | N/A - not measured | N/A - test not executed | Same evidence directory |
| ASPIRE-NET-009 | Verify single-AppHost exclusivity and control-port conflicts; recreate an individual resource and show new ID, endpoint re-resolution, response-target update, multicast rejoin, and readiness recovery; stale IP use fails. | BLOCKED - Docker access unavailable to this process | 0 | N/A - not measured | N/A - test not executed | Same evidence directory |

## Commands and preserved evidence

| Command or probe | Outcome | Evidence |
|---|---|---|
| `docker version` from PATH | Exit 1; PowerShell could not resolve `docker`. No Engine request was made. No stdout artifact was captured. | `docker-version.stderr.txt`, `docker-version-capture.txt` |
| `Get-Command docker.exe`, PATH, `DOCKER_HOST` and `DOCKER_CONTEXT` inspection | No command or Docker PATH entry; host/context variables empty. | `runner-docker-access-probe.txt` |
| Read-only `Test-Path` of the #55 per-user CLI path | Access denied to this process identity; the CLI was not invoked through that path. | `runner-docker-access-probe.txt` records the observed result and boundary |
| Docker-related process listing | Docker Desktop/backend/build processes visible; not proof of Engine readiness. | `runner-docker-access-probe.txt` |
| `gh issue view 56 --repo ibis-ssl/Duck` | Exit 1; GitHub API connection denied by socket access policy. The parent and normal reviewer independently read the live Issue. | `gh-issue-view.stdout.txt`, `gh-issue-view.stderr.txt` |
| `wsl --status` | WSL reports `Wsl/E_ACCESSDENIED`. | `wsl-status.stdout.txt`, `wsl-status.stderr.txt` |
| `gh --version` | Exit 0. | `gh-version.stdout.txt`, `gh-version.stderr.txt` |
| Windows registry query for `ProductName`, `EditionID`, `DisplayVersion`, `CurrentBuild`, `UBR`, `BuildLabEx`, and OS version | Raw values are preserved; build 22631.6199 maps to Windows 11 version 23H2. | `windows-registry-capture.txt` |

There are no per-test stdout/stderr files because none of the nine tests was launched. The evidence directory contains setup/access probes only.

## Model profile

Requested verifier profile: **GPT-6 Luna high**. Exact active-model introspection is unavailable in this runtime, so the actual model/profile is **unverified**.

## Blocker and next action

The read-only `Test-Path` query for the known per-user CLI path returned Access denied; file existence, effective execution permission, and Engine connectivity remain unverified for the current token. Docker Desktop-related processes are visible on FA780. Re-run the acceptance only in the already-authorized execution context that can use the installed Docker Desktop CLI and verify the Engine and host-networking configuration. Then record Docker Desktop/Engine versions, network configuration, per-case commands, measured packet counts, source identities, and test stdout/stderr. Do not treat the current access denial as proof that Docker is absent from FA780. No source, dependency, auth, permission, PATH, Docker configuration, Issue, PR, or Slack changes were made by the verifier. The parent set Project #56 to `blocked`; Issue #56 remains open.
