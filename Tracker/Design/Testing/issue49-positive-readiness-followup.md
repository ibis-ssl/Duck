# #49 Positive participant readiness follow-up

This note tracks the remaining #49 acceptance gap reported by the aggregate review. It does not change the approved acceptance requirements.

The existing match-controller contract requires fresh evidence for every match participant before start and when collecting the final result. A resource being `Running` is not, by itself, positive service readiness; missing or unknown health must not satisfy the participant-ready condition. The follow-up will identify the smallest existing health/readiness signal for each configured participant, use it where available, and keep readiness false when no positive signal exists.

References:

- `Tracker/Design/Testing/aspire-simulation-test-environment.md`, match lifecycle requirements (current design lines 42 and 355).
- Aggregate review finding IFR-002 on integration commit `440353ee1128ecb816b711532f4fe6d5f8c42312`.

Implementation and focused verification remain pending until the dedicated Draft PR is formally linked to #49 and Project16.
