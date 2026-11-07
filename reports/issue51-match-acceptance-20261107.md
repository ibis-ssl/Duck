# Issue #51: Aspire match acceptance evidence

Date: 2026-11-07  
Issue: [#51](https://github.com/ibis-ssl/Duck/issues/51)  
PR: [#117](https://github.com/ibis-ssl/Duck/pull/117)  
Tested repository HEAD: `11536c3c` (initial PR branch commit; tree unchanged from base)  
Base HEAD: `4d92023791aa36766c6d3064604545173d135ca8`

## Environment and run

- AppHost host: Windows, .NET 10, Aspire 13.5.4.
- Container engine: Docker Desktop 4.96.0, Docker Engine 29.9.1, Linux `x86_64`.
- Mode: `Testing:Mode=match`, `Testing:MatchReadinessOnly=false`.
- Docker context: `desktop-linux`; AppHost received this context and the current engine ID for its resource ownership/readiness checks.
- AppHost run ID: `bd375745c8b9464796a34a9067671fef`.
- Stack ID: `issue51-aspire-006g1-run2`.

The project history describes Linux execution as the primary acceptance host, while the current development setup uses Windows AppHost with Docker Desktop. This run is Windows-host evidence only; it does not satisfy native Linux-host acceptance.

## MATCH-001: resources and readiness

The participant status snapshot reported all seven match resources `Running`, `Healthy`, and ready: simulator, game-controller, crane, duck, tigers-blue, autoref-tigers, and ssl-log-recorder. The match-controller also started. Evidence snapshots are in `%LOCALAPPDATA%\Temp\duck-aspire-match-results\issue51-aspire-006g1-run2\participant-status.json` and `match-result.json`.

The AppHost wiring uses the configured `WaitFor` dependencies: Crane waits for Duck and Game Controller; match-controller waits for simulator, Game Controller, Duck, and Crane readiness, and waits for startup of Tigers, AutoRef, and recorder. The recorded current-run participant list contains the seven expected resources. Excluded-resource absence is not backed by a separately retained full container inventory and is therefore unverified in this report.

## MATCH-002: teams, STOP, referee publishing

- Crane probe returned `coordinator_match`, `DUCK_CRANE_TEAM_NAME_OK:ibis`, and `Ready=true`; its launch arguments set team `ibis` to YELLOW.
- Simulator is configured with `--ibis-team-color yellow`; Sumatra ran with `--aiBlue`.
- Game Controller logs record `Team TIGERs Mannheim connected` and `Team Client BLUE-TIGERs Mannheim connected`.
- Game Controller logs include referee commands with type `STOP` (for example, process changes 7, 10, and 15). This confirms STOP was observed during this run; the evidence does not separately identify the very first referee command as STOP.
- The current run had one configured Game Controller resource container with `--publishAddress 224.5.23.1:11003`, and its logs emitted a referee-publisher identity. This supports the configured publisher/resource mapping. Recorder packet counts do not independently prove every received referee packet's on-wire source identity, so unique on-wire sender attribution remains unverified.

The recorder decoded current-run input before shutdown: 8,682 referee, 13,818 vision-detection, and 35,012 tracker packets. The finalized archive was `2026-11-07_07-58-30.log.gz` (16,646,630 bytes). Gzip decompression reached EOF with CRC verification; decompressed size was 90,268,663 bytes. SHA-256: `4f5612bf94a288774c85bc8854da2e1c3e13dc72a1672aecdfb5d5074467f`.

## Overall result and remaining checks

Initial/final readiness snapshots were complete. The broader match-controller run ended `failed` at `maximum_time` with `MATCH_MAXIMUM_TIME_REACHED: execution stopped without POST_GAME`; it does not establish a full-match pass. The project history states Linux match acceptance covers MATCH-001 through MATCH-005. Therefore this is partial Windows evidence, not completion of the Linux acceptance requested by #51.

Evidence locations for the run:
- Status and match result JSON: `%LOCALAPPDATA%\Temp\duck-aspire-match-results\issue51-aspire-006g1-run2\`.
- Recorder archive: `%LOCALAPPDATA%\Temp\duck-aspire-match-results\issue51-aspire-006g1-run2\recorder\2026-11-07_07-58-30.log.gz`.
- Container logs were read from the stopped run's Docker containers; no standalone log export or full container inventory was retained in the repository.

### Verified

- Match resource readiness, including Crane after supplying Docker identity/context to the readiness bridge.
- Crane/team setup, simulator team color, Sumatra blue configuration, and connected TIGERs team.
- STOP commands observed in Game Controller logs.
- Configured single Game Controller resource publisher for `224.5.23.1:11003`.
- Recorder packet decoding, archive finalization, gzip CRC/EOF, decompressed byte count, and archive hash.

### Not verified / not included

- Native Linux AppHost acceptance; the host was Windows.
- Full-match `POST_GAME` completion; the run ended at maximum time.
- That the initial referee command was STOP.
- Unique on-wire source attribution for every recorded referee packet.
- Excluded-resource absence from a retained complete container inventory.
- MATCH-003 through MATCH-005 full acceptance.

### Decision requested

Decide whether to reconcile #51's Linux-host requirement against the current Windows AppHost/Docker Desktop design, or run the acceptance on the required native Linux host. This report makes no claim that #51's full acceptance is complete.
