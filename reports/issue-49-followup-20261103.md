# Issue #49 integration follow-up

## Scope

This follow-up addresses the fresh aggregate review findings I43-IFR-001 and I43-IFR-002 against the explicit Issue #49 acceptance and approved Aspire testing design. It remains within match-controller lifecycle ownership; it does not begin Issue #50 live packet acceptance.

## Planned focused changes

- Allow a match seeded at STOP to enter the existing lifecycle directly, while retaining HALT -> RESUME_FROM_HALT behavior for base/comparison use.
- Compose an actual match-controller resource into match mode with its participant/API/referee/vision dependencies and lifecycle worker; connect readiness, start, termination, final evidence, and persisted result without changing unrelated modes.
- Add model/unit coverage for the seeded STOP continuation boundary and match-mode controller composition. No live/full-stack execution is planned.

## Workflow

Draft PR #112 is intended to target `design/issue18-aspire-test-orchestration`, starting from `3041d75cbfa42d8b477e3cf910dba562814a822b`. Before implementation or tests, formally link PR #112 to Issue #49 and Project 16, then read back both. The authorized PR work will not merge PR #28 or #43 and will not start #50.

The original Issue #49 remains closed because a reopen request was denied before execution. No alternate reopen or project-status mutation is attempted.