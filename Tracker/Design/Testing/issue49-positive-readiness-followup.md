# #49 Positive participant readiness follow-up

This note tracks the remaining #49 acceptance gap reported by the aggregate review. It does not change the approved acceptance requirements.

The existing match-controller contract requires fresh evidence for every match participant before start and when collecting the final result. A resource being `Running` is not, by itself, positive service readiness; missing or unknown health must not satisfy the participant-ready condition.

Repository inspection found existing positive health checks for simulator, Game Controller, Crane, and Duck vision. The existing referee API/packet matcher verifies fresh referee data but does not by itself attest the liveness/readiness of each producer. Match-mode `tigers-blue` (Sumatra), `autoref-tigers` (AutoRef), and `ssl-log-recorder` are registered as plain containers with pinned images and multicast launch arguments, but no Aspire health check or documented readiness endpoint/output contract was found for those resources. Read-only inspection of their pinned linux/amd64 image configs found no Docker `HEALTHCHECK` instruction. Observing shared UDP traffic without source attribution would be live integration evidence and would not establish readiness for each distinct participant.

The accepted design also says not to assume every container has a verification endpoint and to implement the required receive/decode diagnostics in the corresponding container (line 237). That implementation is not present for Sumatra, AutoRef, or recorder in this repository.

The implementation now requires `HealthStatus == Healthy` in addition to `Running`; null, unknown, and unhealthy health status cannot pass participant readiness. A focused regression first failed against the previous behavior (Running plus null was accepted), then passed after this fail-closed change (7/7 focused controller tests).

Positive readiness for all configured participants remains blocked until an accepted readiness interface is established for the three resources above. This PR must not invent probes or claim the full acceptance condition is met.

References:

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`, match lifecycle requirements (current design lines 42 and 355).
- Aggregate review finding IFR-002 on integration commit `440353ee1128ecb816b711532f4fe6d5f8c42312`.

The dedicated Draft PR is formally linked to #49 and Project16. The fail-closed readiness predicate and focused regression are implemented; a positive readiness interface for all participants remains unresolved.
