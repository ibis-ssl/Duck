# Phases Status

Rule: This file may be updated only through `task-breakdown-planner`, `task-consistency-manager`, or `progress-sync-manager`.

## Overall State

- Active Phase: review
- Active Task: RAW-VISION-016
- Remaining Phases: review

## Phases

| Phase | Status | Exit Criteria |
| --- | --- | --- |
| preparation | complete | Design plan and tracking files exist before implementation. |
| implementation | complete | Receiver/store keep per-camera latest frames and the UI supports a compact field-first layout with moved source selector, axis/cursor overlays, collapsible sidebar behavior, diagnostics render snapshot field/detail resizing, diagnostics frame timeline width resizing, unified Tracker.Server navigation styling, diagnostics timeline playback controls, and Issue #10 Vision split / overlay with Raw / Tracked / 3rd party tracker sources. RAW-VISION-015 implementation and r2 review are complete. |
| verification | complete | Issue #10 split / overlay contract tests and diagnostics latest-before fallback / missing-only regression tests pass, focused validation passes, and `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` passes after RAW-VISION-015 implementation. HTTP 応答と HTML 断片の確認は `reports/issue-10-raw-vision-016-validation-20260514095659.md` に記録済み。Playwright は利用不可、metadata 付き diagnostics log は未取得のため、latest-before metadata の実画面表示は契約テストと HTTP 応答による代替証跡として扱う。UI を見ながらの動作確認はユーザー側で実施する。 |
| review | in-progress | Task-scoped gpt-5.5 high final review is recorded in `reports/issue-10-raw-vision-016-final-review-20260514103501.md` with no findings. PR #15 body synchronization and final tracking close remain. Draft release should wait for user-side UI confirmation. |
| design | complete | `raw-vision-viewer-plan.md` records Issue #10 source options, same-tick acquisition strategy, diagnostics replay time-sync audit result, latest-before fallback policy, geometry policy, diagnostics-aligned split / overlay UI behavior, rejected alternatives, and footnotes for named concepts. gpt-5.5 high review is recorded in `reports/issue-10-raw-vision-013-design-review-20260514083515.md` with no blocking findings. Terminology footnote consolidation is reviewed in `reports/issue-10-design-terminology-review-r3-20260514103027.md` with no findings. |
