# ASPIRE-005 wrapper design review r8

## Review identity

- Mode: limited ordinary re-review of parent findings KERO-ASPIRE-WRAP-001 / 002 and adjacent scope contracts.
- Repository branch: `task/pr28-aspire-005`.
- Exact design targets:
  - `Tracker/Design/Testing/aspire-simulation-test-environment.md`: GitHub content blob `df4513e5cf606652c0e3bd1f7140829e4bc0e5b8`, SHA-256 `64b1772bb900b37c4076f10b12e19c4c3356b537be83f0577f3019c688d94546`.
  - `Tracker/Design/Testing/tracker-comparison-debug-design.md`: GitHub content blob `015499ea11e8ae2059c46d4169107254000eb377`, SHA-256 `e88b8bf51e690b38249df36e0ba11d74a509b682d0744ed6e657f5c1dc7b5b2d`.
  - `Tracker/Design/tasks-status.md`: GitHub content blob `d14217a260997ff985b3fb7f498e81f61526b38b`, SHA-256 `db6f1049acfe3e0136d7ca497915d7494bbfcd4cafed157dcd899d5a9cda76c6`.
- Reviewer also checked the corrected task rows against the final task-document SHA above.

## Review disposition

**Pass; no findings.** KERO-ASPIRE-WRAP-001/002 are closed at design level.

- Comparison trackers are individual wrapper executable resources with complete owner inspection and service-specific process / diagnostic / identity-matched packet readiness. DebugHost remains a .NET project resource, waits for Duck project-start and both tracker wrappers, and has its own three-source comparison health.
- Comparison mode is no longer described as a future generic container addition. Its design contract is explicit, with implementation/model/wrapper tests assigned to ASPIRE-006A/B, comparison UI/replay to ASPIRE-006C-E, and real packet OS acceptance to ASPIRE-007A. ASPIRE-005 is explicitly base mode only.
- Game Controller startup checks the configured initial HALT / STOP once per resource generation. Continued health checks API/process/container liveness and fresh known referee traffic without requiring the initial command to persist. Restart clears the generation latch and requires initial-state validation again.
- Host-side Docker CLI shell use is prohibited; the existing Crane `bash -c` example is explicitly scoped to the command inside the container image.
- ASPIRE-004B / ASPIRE-006F5 task rows now specify readiness-conditioned `WaitFor` for services and distinguish Duck's project-start notification. No stale `WaitForStart` dependency remains in those rows.

## Validation and limits

- Targeted Markdown text lint on the three files: passed.
- `git diff --check`: passed.
- Targeted CSpell check reports 300 unknown vocabulary terms across these three existing technical design/status files; no dictionary or lint rule bypass was added. This is the same aggregate count recorded in the earlier check before the current Japanese wording revisions.
- This is a design-only review. Wrapper implementation, focused runtime tests, and hosted packet acceptance remain unverified. No implementation approval is inferred.
