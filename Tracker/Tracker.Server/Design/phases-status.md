# Phases Status

Rule: This file may be updated only through `task-breakdown-planner`, `task-consistency-manager`, or `progress-sync-manager`.

## Overall State

- Active Phase: implementation
- Active Task: RAW-VISION-015
- Remaining Phases: implementation, verification, review

## Phases

| Phase | Status | Exit Criteria |
| --- | --- | --- |
| preparation | complete | Design plan and tracking files exist before implementation. |
| implementation | in-progress | Receiver/store keep per-camera latest frames and the UI supports a compact field-first layout with moved source selector, axis/cursor overlays, collapsible sidebar behavior, diagnostics render snapshot field/detail resizing, diagnostics frame timeline width resizing, unified Tracker.Server navigation styling, diagnostics timeline playback controls, and Issue #10 Vision split / overlay with Raw / Tracked / 3rd party tracker sources. RAW-VISION-014 TDD Red contract and r4 review are complete; RAW-VISION-015 implementation is active. |
| verification | pending | Issue #10 split / overlay contract tests and diagnostics latest-before fallback / missing-only regression tests pass, focused validation passes, and `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` passes after RAW-VISION-015 implementation. |
| review | pending | Task-scoped gpt-5.5 high review is recorded for Issue #10, no actionable findings remain, tracking and PR #15 are synchronized. |
| design | complete | `raw-vision-viewer-plan.md` records Issue #10 source options, same-tick acquisition strategy, diagnostics replay time-sync audit result, latest-before fallback policy, geometry policy, diagnostics-aligned split / overlay UI behavior, rejected alternatives, and glossary entries for named concepts. gpt-5.5 high review is recorded in `reports/issue-10-raw-vision-013-design-review-20260514083515.md` with no blocking findings. |
