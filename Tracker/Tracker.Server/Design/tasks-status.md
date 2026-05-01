# Tasks Status

Rule: This file may be updated only through `task-breakdown-planner`, `task-consistency-manager`, or `progress-sync-manager`.

## Current Task

- ID: RAW-VISION-008
- Title: Compact field-first raw vision layout and field overlays
- Phase: review
- Status: complete
- Size: medium
- Dependencies: RAW-VISION-007 complete.
- Exit Criteria:
  - Header text `Raw Vision Viewer` と `SSL_WrapperPacket / SSL-Vision multicast` は省略され、field 表示面積が拡大される。
  - Aggregate / camera selector は field 上部から移動し、field の縦方向面積を圧迫しない。
  - Field canvas に +X / +Y 方向が分かる axis overlay が追加される。
  - Cursor 座標は field 上で確認でき、cursor 位置に応じて上側または下側に表示される。
  - Desktop の左 sidebar は折りたたみ可能になる。
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
| RAW-VISION-006 | Harden multicast receiver initialization | verification | complete | RAW-VISION-001 | Receiver attempts multicast join across viable IPv4 interfaces, configured interface validation is explicit, tests/build pass, and review is recorded in `reports/raw-vision-multicast-join-evidence-20260430174124.md`. |
| RAW-VISION-007 | Add aggregate and per-camera raw vision views | implementation | complete | RAW-VISION-001, RAW-VISION-003 | Store keeps latest frame per camera, aggregate and camera-specific views are available in the UI, field canvas follows `ssl-vision-client` source-selector/canvas behavior, tests/build pass, and review/evidence are recorded in `reports/raw-vision-source-selector-evidence-20260430181252.md`. |
| RAW-VISION-008 | Compact field-first raw vision layout and field overlays | review | complete | RAW-VISION-007 | Viewer header is compact, source selector moves away from the field top, axis/cursor overlays are added, sidebar can collapse on desktop, `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` passes, and `reports/raw-vision-008-review-20260501101437.md` records a no-findings sub-agent review. |
