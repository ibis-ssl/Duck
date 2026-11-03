# #49 Positive participant readiness follow-up

This note tracks the remaining #49 acceptance gap reported by the aggregate review. It does not change the approved acceptance requirements.

The existing match-controller contract requires fresh evidence for every match participant before start and when collecting the final result. A resource being `Running` is not, by itself, positive service readiness; missing or unknown health must not satisfy the participant-ready condition.

Repository inspection found existing positive health checks for simulator, Game Controller, Crane, Duck vision, and the match-controller referee endpoint. Match-mode `tigers-blue` (Sumatra), `autoref-tigers` (AutoRef), and `ssl-log-recorder` are registered as plain containers with pinned images and launch arguments, but no health check or documented readiness endpoint/output contract was found for those three resources. Observing shared UDP traffic would be live integration evidence and is not an accepted substitute for an existing service-readiness signal.

Therefore positive readiness for all configured participants remains blocked until an accepted readiness interface is established for those three resources. This PR must not treat `Running` plus null/unknown health as ready, and must not invent probes or claim the full acceptance condition is met.

References:

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`, match lifecycle requirements (current design lines 42 and 355).
- Aggregate review finding IFR-002 on integration commit `440353ee1128ecb816b711532f4fe6d5f8c42312`.

The dedicated Draft PR is formally linked to #49 and Project16. Product implementation and verification remain pending because the required participant readiness interfaces are not present in the repository.
