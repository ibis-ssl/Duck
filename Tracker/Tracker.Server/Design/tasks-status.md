# Tasks Status

Rule: This file may be updated only through `task-breakdown-planner`, `task-consistency-manager`, or `progress-sync-manager`.

## Current Task

- ID: RAW-VISION-009
- Title: Diagnostics render snapshot の field/detail 比率をドラッグで変更可能にする
- Phase: review
- Status: complete
- Size: medium
- Dependencies: RAW-VISION-008 complete.
- Exit Criteria:
  - `/diagnostics` の render snapshot 表示で、field 表示領域と下部 detail 領域の境界をドラッグして縦方向の比率を変更できる。
  - 4K など縦に広い viewport では、field 表示領域が固定上限で小さくならず、ユーザー操作で大きくできる。
  - detail 領域は縮小時も最低限の表示・スクロールが維持され、Vision Input / Tracker Output の確認が壊れない。
  - 可変高さの境界値を単体テストで確認する。実装・検証は `reports/raw-vision-009-evidence-20260511231841.md` に記録済み。
  - `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` passes。review は `reports/raw-vision-009-review-20260511231841.md` に記録済み。

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
| RAW-VISION-009 | Diagnostics render snapshot の field/detail 比率をドラッグで変更可能にする | review | complete | RAW-VISION-008 | `/diagnostics` の render snapshot 表示で field/detail 境界をドラッグでき、4K viewport でも field を大きく表示できる。detail 領域の最低表示・スクロールを維持し、可変高さの境界値テストと `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` が通る。実装・検証は `reports/raw-vision-009-evidence-20260511231841.md`、review は `reports/raw-vision-009-review-20260511231841.md` に記録済み。 |
