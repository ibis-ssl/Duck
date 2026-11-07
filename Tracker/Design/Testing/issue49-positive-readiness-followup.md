# #49 Positive participant readiness follow-up

This note tracks the remaining #49 acceptance gap reported by the aggregate review. It does not change the approved acceptance requirements.

The existing match-controller contract requires fresh evidence for every match participant before start and when collecting the final result. A resource being `Running` is not, by itself, positive service readiness; missing or unknown health must not satisfy the participant-ready condition.

Repository inspection found existing positive health checks for simulator, Game Controller, Crane, and Duck vision. The existing referee API/packet matcher verifies fresh referee data but does not by itself attest the liveness/readiness of each producer. Match-mode `tigers-blue` (Sumatra), `autoref-tigers` (AutoRef), and `ssl-log-recorder` are registered as plain containers with pinned images and multicast launch arguments, but no Aspire health check or documented readiness endpoint/output contract was found for those resources. Read-only inspection of their pinned linux/amd64 image configs found no Docker `HEALTHCHECK` instruction. Observing shared UDP traffic without source attribution would be live integration evidence and would not establish readiness for each distinct participant.

The accepted design also says not to assume every container has a verification endpoint and to implement the required receive/decode diagnostics in the corresponding container (line 237). That implementation is not present for Sumatra, AutoRef, or recorder in this repository.

PR #115 merged the accepted resource-specific evidence contract into the design branch at merge SHA `845eee0a554f652c418dc19536faf8e0243d772e`. The AppHost projection publishes current resource/container/run identity, service evidence, and freshness. The match-controller deserializes that participant-status JSON and accepts unknown Docker health only when the resource-specific positive evidence is present; unknown health without positive service evidence remains false. Other participant resources still require `Healthy`.

A focused integration test writes the AppHost-shaped JSON to the configured participant-status path and exercises `WaitForInitialReadinessAsync`. The live readiness-only run confirmed separate advancing TIGERs and AutoRef tracker-output evidence and Recorder record decoding/finalization; each service became not-ready after its own stop. This proves pre-start readiness preparation only, not the full post-start match lifecycle. The latter E2E acceptance belongs to #50 and is not an additional #49/#114 gate.

References:

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`, match lifecycle requirements (current design lines 42 and 355).
- Aggregate review finding IFR-002 on integration commit `440353ee1128ecb816b711532f4fe6d5f8c42312`.

The integration test and its focused fail-closed case verify the #114-to-match-controller connection. Exact-head CI and normal/independent review are still required for this PR branch before it can be merged. The existing #114 issue state is tracked separately; this note does not claim that it was closed automatically by merging into the design branch.
