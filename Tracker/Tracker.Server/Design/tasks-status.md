# Tasks Status

Rule: This file may be updated only through `task-breakdown-planner`, `task-consistency-manager`, or `progress-sync-manager`.

## Current Task

- ID: RAW-VISION-001
- Title: Implement SSL_WrapperPacket raw vision viewer
- Phase: review
- Status: complete
- Size: medium
- Dependencies: design setup complete before code changes; `SslProto` generated types available.
- Exit Criteria:
  - `Tracker.Server` references `SslProto` directly.
  - UDP receiver decodes `SSL_WrapperPacket` and updates a singleton store.
  - Root Blazor page renders field, detection tables, geometry calibration, and raw JSON.
  - Tests cover store update/error behavior and field projection behavior.
  - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj` passes.
  - `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` passes.

## Tasks

| ID | Task | Phase | Status | Dependencies | Exit Criteria |
| --- | --- | --- | --- | --- | --- |
| RAW-VISION-000 | Create design and tracking files | preparation | complete | User plan | Design directory, plan, task tracking, and phase tracking exist. |
| RAW-VISION-001 | Implement UDP receiver and packet store | implementation | complete | RAW-VISION-000 | Receiver configuration, hosted service, store snapshot, decode success, and error accounting are implemented. |
| RAW-VISION-002 | Implement field projection | implementation | complete | RAW-VISION-001 | Geometry dimensions and fallback dimensions map to SVG coordinates correctly. |
| RAW-VISION-003 | Implement Blazor raw vision UI | implementation | complete | RAW-VISION-001, RAW-VISION-002 | Root page renders status, field SVG, tables, raw JSON, and focused navigation. |
| RAW-VISION-004 | Add tests | verification | complete | RAW-VISION-001, RAW-VISION-002 | Store and projection tests are present and meaningful. |
| RAW-VISION-005 | Verify and review | verification | complete | RAW-VISION-003, RAW-VISION-004 | Test/build commands pass; review result is recorded in `reports/raw-vision-viewer-evidence-20260430165645.md`. |
