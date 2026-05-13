# Tasks Status

Rule: This file may be updated only through `task-breakdown-planner`, `task-consistency-manager`, or `progress-sync-manager`.

## Current Task

- ID: RAW-VISION-013
- Title: Issue #10 Vision split / overlay の同時取得方針と設計を確定する
- Phase: design
- Status: in-progress
- Size: small
- Dependencies: RAW-VISION-012 complete, Issue #10 user clarification.
- Exit Criteria:
  - Issue #10 の source 候補を `Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` として設計に固定する。
  - live raw / tracked / 3rd party tracker を比較可能にするため、同時取得または同一 render tick snapshot の実現可能性を調査し、採用方針と不採用方針を設計へ記録する。
  - diagnostics replay / comparison が Vision、ibis tracker、3rd party tracker を同じ replay timeline tick で同期できているか監査し、ずれている場合は今回の修正範囲へ入れる。
  - geometry 基準は実装が比較しやすい方針を選び、選択しなかった方針も設計へ記録する。
  - split / overlay の details、legend、layer visibility、UI 挙動は diagnostics に寄せる。
  - 設計書には出てくる固有名詞の説明項目を追加する。
  - 設計調査・設計編集の report を `reports/` に残し、PR #15 へ push する。

## Issue #10 固定残タスク

- 固定一覧は `RAW-VISION-013`、`RAW-VISION-014`、`RAW-VISION-015`、`RAW-VISION-016` とする。Issue #10 の tracking では補助番号を使わない。
- `RAW-VISION-013`: Vision split / overlay の source 候補、同時取得方針、diagnostics replay の時間同期監査、geometry 基準、diagnostics 寄せ UI、固有名詞説明を設計へ固定する。
- `RAW-VISION-014`: Vision split / overlay の view-state、layout、source selection、overlay layer contract と diagnostics latest-before fallback contract を TDD で固定する。
- `RAW-VISION-015`: Vision split / overlay UI、live source snapshot 接続、diagnostics selected tick missing 時の latest-before 表示/比較を実装する。
- `RAW-VISION-016`: README / manual evidence、最終検証、review、PR #15 ready 化を完了する。

## 完了した追加タスク

- ID: RAW-VISION-010
- Title: Diagnostics frame timeline の幅をドラッグで変更可能にする
- Phase: review
- Status: complete
- Size: small
- Dependencies: RAW-VISION-009 complete.
- Exit Criteria:
  - `/diagnostics` 左側の frame timeline と右側 detail の境界をドラッグして timeline 幅を変更できる。
  - frame timeline はユーザー操作で小さくでき、右側 field/detail 表示領域を広げられる。
  - timeline は最小幅でも frame 選択操作が壊れず、長い文字列は省略表示される。
  - 可変幅の境界値を単体テストで確認する。実装・検証は `reports/raw-vision-010-evidence-20260511233242.md` に記録済み。
  - `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` passes。review は `reports/raw-vision-010-review-20260511233242.md`、`reports/raw-vision-010-review-r2-20260511233631.md`、`reports/raw-vision-010-review-r3-20260511234000.md` に記録済み。PR は https://github.com/ibis-ssl/Duck/pull/7。

## 追加タスク

- ID: RAW-VISION-011
- Title: Tracker.Server 共通 navigation の見た目を viewer UI と揃える
- Phase: review
- Status: complete
- Size: small
- Dependencies: RAW-VISION-010 complete.
- Exit Criteria:
  - side navigation / page list が raw vision / diagnostics の濃色 green UI と同じ配色・密度になる。
  - active / hover / collapsed の状態が既存 navigation 操作を維持しつつ視覚的に統一される。
  - mobile navigation toggle が既存操作を維持しつつ、floating template 風の見た目から外れる。
  - `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` passes。実装・検証は `reports/raw-vision-011-evidence-20260512001259.md`、review は `reports/raw-vision-011-review-20260512001259.md` に記録済み。

## 直近完了タスク

- ID: RAW-VISION-012
- Title: Diagnostics timeline に再生・停止・早送り controls を追加する
- Phase: review
- Status: complete
- Size: small
- Dependencies: RAW-VISION-010 complete.
- Exit Criteria:
  - `/diagnostics` の timeline scrubber 付近に再生、停止、早送り controls が表示される。
  - 再生は選択中 entry から順方向に frame を進め、最後に到達したら停止して先頭 entry に戻る。
  - 通常再生は log entry の timestamp 差分を使い、上限 clamp なしで実際の記録速度に合わせて進む。
  - 再生中は再生ボタンが停止ボタンへ切り替わり、停止は進行中の再生を止めて現在選択 frame を維持する。
  - 早送り中は早送りボタンが停止ボタンへ切り替わり、停止は現在選択 frame を維持する。
  - 早送りは通常再生より大きい step / 短い interval で順方向に進む。
  - log 切替や entry 不在時に再生 state が不整合にならない。
  - 停止や mode 切替の直後に遅れて到着した playback tick は選択状態を変更しない。
  - playback index / interval / stale tick guard を単体テストで確認する。実装・検証は `reports/raw-vision-012-evidence-20260512002100.md` に記録済み。
  - `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` passes。review は `reports/raw-vision-012-review-20260512002100.md`、`reports/raw-vision-012-review-r2-20260512002502.md`、`reports/raw-vision-012-review-r3-20260512002923.md`、`reports/raw-vision-012-review-r4-20260512003653.md`、`reports/raw-vision-012-review-r5-20260512004014.md` に記録済み。

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
| RAW-VISION-009 | Diagnostics render snapshot の field/detail 比率をドラッグで変更可能にする | review | complete | RAW-VISION-008 | `/diagnostics` の render snapshot 表示で field/detail 境界をドラッグでき、4K viewport でも field を大きく表示できる。detail 領域の最低表示・スクロールを維持し、可変高さの境界値テストと `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` が通る。実装・検証は `reports/raw-vision-009-evidence-20260511231841.md`、review は `reports/raw-vision-009-review-20260511231841.md`、PR は https://github.com/ibis-ssl/Duck/pull/7 に記録済み。 |
| RAW-VISION-010 | Diagnostics frame timeline の幅をドラッグで変更可能にする | review | complete | RAW-VISION-009 | `/diagnostics` 左側の frame timeline と右側 detail の境界をドラッグして timeline 幅を変更できる。timeline は小さくでき、右側 field/detail 領域を広げられる。最小幅でも frame 選択操作が壊れず、可変幅の境界値テストと `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` が通る。実装・検証は `reports/raw-vision-010-evidence-20260511233242.md`、review は `reports/raw-vision-010-review-20260511233242.md`、`reports/raw-vision-010-review-r2-20260511233631.md`、`reports/raw-vision-010-review-r3-20260511234000.md`、PR は https://github.com/ibis-ssl/Duck/pull/7 に記録済み。 |
| RAW-VISION-011 | Tracker.Server 共通 navigation の見た目を viewer UI と揃える | review | complete | RAW-VISION-010 | side navigation / page list を raw vision / diagnostics の濃色 green UI と同じ配色・密度に揃え、active / hover / collapsed / mobile toggle の既存操作を維持する。`dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` が通る。実装・検証は `reports/raw-vision-011-evidence-20260512001259.md`、review は `reports/raw-vision-011-review-20260512001259.md` に記録済み。 |
| RAW-VISION-012 | Diagnostics timeline に再生・停止・早送り controls を追加する | review | complete | RAW-VISION-010 | `/diagnostics` の timeline scrubber 付近に再生、停止、早送り controls を追加し、通常再生は log timestamp 差分に合わせて順方向に frame を進める。最後に到達したら停止して先頭 entry に戻る。再生中/早送り中は該当ボタンが停止ボタンへ切り替わり、停止操作は現在選択を維持して止める。log 切替や entry 不在、停止直後の stale tick で state 不整合を起こさず、playback index / interval / stale tick guard テストと `dotnet build Tracker/Tracker.Server/Tracker.Server.csproj` が通る。実装・検証は `reports/raw-vision-012-evidence-20260512002100.md`、review は `reports/raw-vision-012-review-20260512002100.md`、`reports/raw-vision-012-review-r2-20260512002502.md`、`reports/raw-vision-012-review-r3-20260512002923.md`、`reports/raw-vision-012-review-r4-20260512003653.md`、`reports/raw-vision-012-review-r5-20260512004014.md` に記録済み。 |
| RAW-VISION-013 | Issue #10 Vision split / overlay の同時取得方針と設計を確定する | design | in-progress | RAW-VISION-012 | Source 候補を `Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` として固定し、live 比較で同時取得または同一 render tick snapshot をどう扱うか調査・設計する。diagnostics replay / comparison が Vision、ibis tracker、3rd party tracker を同じ replay timeline tick で同期できているか監査し、ずれている場合は今回の修正範囲へ入れる。geometry 基準、diagnostics 寄せ UI、固有名詞説明、採用/不採用方針を `raw-vision-viewer-plan.md` と report に記録し、PR #15 へ push する。 |
| RAW-VISION-014 | Vision split / overlay と diagnostics time sync の TDD contract を追加する | verification | pending | RAW-VISION-013 | split / overlay mode、source selection、3rd party tracker source、同一 UI render tick snapshot、raw geometry 優先 fallback、Layer A/B visibility、same-source 1 layer 化、missing layer でも ready layer を残す挙動、diagnostics 寄せ legend/details の期待挙動を単体テストで先に固定する。selected replay timeline tick に対象 3rd party source の alignment record が無い場合、selected replay timeline tick / selected time 自体は source ごとに動かさず、同じ source の selected tick 以前の `latest-before snapshot` を直前 sample の hold として表示/比較に使い、matching rule、source `receivedAt`、selected tick との差分 delta、stale/latest-before 状態を出す regression test も先に追加する。selected tick 以前に同じ source の snapshot が一切無い場合だけ `CandidateMissing` / `NoCandidateSnapshot` 相当になり、future/later snapshot へ fallback せず ready layer を残すことも固定し、少なくとも 1 件の failing test を確認する。 |
| RAW-VISION-015 | Vision split / overlay UI と live source snapshot 接続を実装する | implementation | pending | RAW-VISION-014 | Vision 画面で左右分割と overlay mode を切り替えられ、Layer A/B source と visibility を diagnostics に近い UI で操作できる。Raw / Tracked / 3rd party tracker の同一 UI render tick snapshot を field に描画し、既存 raw/tracked 単体表示を壊さない。3rd party tracker は `MultiTrackerManager<TrackerPacketAdapter>` から immutable snapshot store / composer を通して接続し、diagnostics は selected replay timeline tick / selected time を固定したまま、対象 source alignment がない場合に同じ source の selected tick 以前の `latest-before snapshot` を直前 sample の hold として表示/比較し、future/later snapshot へ fallback しない。 |
| RAW-VISION-016 | Issue #10 の final validation / docs / review / PR ready を完了する | review | pending | RAW-VISION-015 | README/manual evidence、focused/full validation、gpt-5.5 high review、tracking sync、PR #15 本文更新、draft 解除判断材料を揃える。 |
