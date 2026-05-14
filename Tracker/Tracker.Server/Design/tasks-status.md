# Tasks Status

Rule: This file may be updated only through `task-breakdown-planner`, `task-consistency-manager`, or `progress-sync-manager`.

## Current Task

- ID: RAW-VISION-016
- Title: Issue #10 の最終検証 / docs / review / PR ready を完了する
- Phase: review
- Status: in-progress
- Size: small
- Dependencies: RAW-VISION-015 complete.
- Exit Criteria:
  - RAW-VISION-015 の実装結果について、対象テスト、サーバービルド、必要な追加検証を `reports/issue-10-raw-vision-016-validation-20260514095659.md` に記録した。
  - Compare overlay と diagnostics latest-before metadata 表示の Codex 側確認証跡を `reports/issue-10-raw-vision-016-validation-20260514095659.md` に残した。Playwright は利用不可、metadata 付き diagnostics log は未取得のため、HTML 応答と契約テストによる代替証跡として記録済み。UI を見ながらの動作確認はユーザー側で実施する。
  - README / docs の更新要否を確認し、設計用語説明は `raw-vision-viewer-plan.md` の脚注へ統合した。audit / review は `reports/issue-10-design-terminology-audit-r3-20260514102338.md` と `reports/issue-10-design-terminology-review-r3-20260514103027.md` に記録済みで指摘なし。
  - gpt-5.5 high の最終レビューを `reports/issue-10-raw-vision-016-final-review-20260514103501.md` に残し、blocking / user-confirmation-required / non-blocking held concern がないことを確認した。
  - tracking と PR #15 本文を同期し、ユーザー側 UI 確認後に draft 解除判断できる材料を揃える。

## Issue #10 固定残タスク

- 固定一覧は `RAW-VISION-013`、`RAW-VISION-014`、`RAW-VISION-015`、`RAW-VISION-016` とする。Issue #10 の tracking では補助番号を使わない。
- `RAW-VISION-013`: Vision split / overlay の source 候補、同時取得方針、diagnostics replay の時間同期監査、geometry 基準、diagnostics 寄せ UI、固有名詞説明を設計へ固定した。調査 report、設計 report / r2、gpt-5.5 high review は完了済みで blocking findings なし。
- `RAW-VISION-014`: Vision split / overlay の view-state、layout、source selection、overlay layer contract、geometry contract と diagnostics latest-before fallback contract を TDD Red として固定した。gpt-5.5 high review r4 は blocking findings なし。
- `RAW-VISION-015`: Vision split / overlay UI、live source snapshot 接続、diagnostics selected tick missing 時の latest-before 表示/比較を実装した。gpt-5.5 high r2 review は指摘なし。
- `RAW-VISION-016`: README / manual evidence、最終検証、review、PR #15 ready 化を完了する。検証 report、設計用語脚注化 review、final review は完了。PR #15 同期を実行中。UI を見ながらの動作確認はユーザー側で実施する。

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

- ID: RAW-VISION-015
- Title: Vision split / overlay UI と live source snapshot 接続を実装する
- Phase: implementation
- Status: complete
- Size: medium
- Dependencies: RAW-VISION-014 complete.
- Exit Criteria:
  - Vision 画面で左右分割と overlay mode を切り替えられ、Layer A/B source と visibility を diagnostics に近い UI で操作できる。
  - Raw / Tracked / 3rd party tracker の同一 UI render tick immutable snapshot を field に描画し、既存 raw/tracked 単体表示を壊さない。
  - 3rd party tracker は `MultiTrackerManager<TrackerPacketAdapter>` から immutable snapshot store / composer を通して接続する。
  - geometry は raw geometry 優先、raw geometry が無い場合のみ tracked fallback、3rd party tracker packet から復元しない。
  - diagnostics は selected replay timeline tick / selected time を固定したまま、対象 source alignment がない場合に同じ source の selected tick 以前の `latest-before snapshot` を直前 sample hold として表示/比較し、future/later snapshot へ fallback しない。
  - RAW-VISION-014 で追加した対象契約テスト 37 件は成功し、`Tracker/Tracker.Server/Tracker.Server.csproj` のビルドも成功した。
  - 実装レポートは `reports/issue-10-raw-vision-015-implementation-20260514092635.md`、修正レポートは `reports/issue-10-raw-vision-015-fix-20260514094259.md`、レビューは `reports/issue-10-raw-vision-015-review-20260514093808.md` と `reports/issue-10-raw-vision-015-review-r2-20260514095053.md` に記録済みで、r2 は指摘なし。

- ID: RAW-VISION-014
- Title: Vision split / overlay と diagnostics time sync の TDD contract を追加する
- Phase: verification
- Status: complete
- Size: small
- Dependencies: RAW-VISION-013 complete.
- Exit Criteria:
  - split / overlay mode、source selection、3rd party tracker source、同一 UI render tick snapshot、raw geometry 優先 fallback、Layer A/B visibility、same-source 1 layer 化、missing layer でも ready layer を残す挙動、diagnostics 寄せ legend/details の期待挙動を単体テストで固定した。
  - selected replay timeline tick に対象 3rd party source の alignment record が無い場合、selected replay timeline tick / selected time 自体は source ごとに動かさず、同じ source の selected tick 以前の `latest-before snapshot` を直前 sample の hold として表示/比較に使う regression test を追加した。
  - latest-before regression は matching rule、source `receivedAt`、selected tick との差分 delta、stale/latest-before 状態を確認する。
  - selected tick 以前に同じ source の snapshot が一切無い場合だけ `CandidateMissing` / `NoCandidateSnapshot` 相当になり、future/later snapshot へ fallback しないことを固定した。
  - focused test は TDD Red として 37 件中 26 pass / 11 fail を確認した。fail は Vision live comparison API 未実装 9 件、diagnostics latest-before / future fallback 未実装 2 件。
  - TDD / fix / review reports は `reports/issue-10-raw-vision-014-tdd-contract-20260514084547.md`、`reports/issue-10-raw-vision-014-tdd-review-20260514085339.md`、`reports/issue-10-raw-vision-014-tdd-fix-20260514085712.md`、`reports/issue-10-raw-vision-014-tdd-review-r2-20260514090315.md`、`reports/issue-10-raw-vision-014-tdd-fix-r2-20260514090645.md`、`reports/issue-10-raw-vision-014-tdd-review-r3-20260514091311.md`、`reports/issue-10-raw-vision-014-tdd-fix-r3-20260514091616.md`、`reports/issue-10-raw-vision-014-tdd-review-r4-20260514092124.md` に記録済みで、r4 review は blocking findings なし。

- ID: RAW-VISION-013
- Title: Issue #10 Vision split / overlay の同時取得方針と設計を確定する
- Phase: design
- Status: complete
- Size: small
- Dependencies: RAW-VISION-012 complete, Issue #10 user clarification.
- Exit Criteria:
  - Issue #10 の source 候補を `Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` として設計に固定した。
  - live raw / tracked / 3rd party tracker は、厳密な同一 packet timestamp ではなく、同一 UI render tick の immutable snapshot として比較する方針にした。
  - diagnostics replay / comparison は selected replay timeline tick を動かさず、対象 source record がない場合は selected tick 以前の `latest-before snapshot` を直前 sample hold として表示/比較する方針にした。
  - geometry は raw geometry 優先、tracked fallback、3rd party tracker packet から復元しない方針にした。
  - split / overlay の details、legend、layer visibility、same-source 1 layer 化、ready layer 維持は diagnostics に寄せる。
  - 固有名詞説明を `raw-vision-viewer-plan.md` の脚注へ統合した。
  - 調査 report は `reports/issue-10-vision-overlay-investigation-20260514080106.md`、`reports/issue-10-live-same-tick-investigation-20260514081135.md`、`reports/issue-10-diagnostics-time-sync-audit-20260514081730.md` に記録済み。
  - 設計 report は `reports/issue-10-vision-overlay-design-20260514082233.md`、r2 は `reports/issue-10-vision-overlay-design-r2-20260514082755.md` に記録済み。
  - gpt-5.5 high review は `reports/issue-10-raw-vision-013-design-review-20260514083515.md` に記録済みで blocking findings なし。PR #15 へ push 済み。

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
| RAW-VISION-013 | Issue #10 Vision split / overlay の同時取得方針と設計を確定する | design | complete | RAW-VISION-012 | Source 候補を `Raw Aggregate`、`Raw Camera`、`Tracked`、`3rd party tracker` として固定した。live 比較は同一 UI render tick immutable snapshot、diagnostics は selected tick 固定 + latest-before hold、geometry は raw 優先 / tracked fallback、UI は diagnostics 寄せとして設計済み。調査・設計・r2・gpt-5.5 high review は `reports/issue-10-vision-overlay-investigation-20260514080106.md`、`reports/issue-10-live-same-tick-investigation-20260514081135.md`、`reports/issue-10-diagnostics-time-sync-audit-20260514081730.md`、`reports/issue-10-vision-overlay-design-20260514082233.md`、`reports/issue-10-vision-overlay-design-r2-20260514082755.md`、`reports/issue-10-raw-vision-013-design-review-20260514083515.md` に記録済みで blocking findings なし。設計用語説明は後続で脚注へ統合し、`reports/issue-10-design-terminology-review-r3-20260514103027.md` で指摘なしを確認済み。 |
| RAW-VISION-014 | Vision split / overlay と diagnostics time sync の TDD contract を追加する | verification | complete | RAW-VISION-013 | split / overlay mode、source selection、3rd party tracker source、同一 UI render tick snapshot、raw geometry 優先 fallback、Layer A/B visibility、same-source 1 layer 化、missing layer でも ready layer を残す挙動、diagnostics 寄せ legend/details、latest-before hold、source `receivedAt` / delta / stale metadata、future fallback 禁止を TDD Red として固定した。focused test は 37 件中 26 pass / 11 fail。r4 review は `reports/issue-10-raw-vision-014-tdd-review-r4-20260514092124.md` に記録済みで blocking findings なし。 |
| RAW-VISION-015 | Vision split / overlay UI と live source snapshot 接続を実装する | implementation | complete | RAW-VISION-014 | Vision 画面で左右分割と overlay mode を切り替えられ、Layer A/B source と visibility を diagnostics に近い UI で操作できる。Raw / Tracked / 3rd party tracker の同一 UI render tick snapshot を field に描画し、既存 raw/tracked 単体表示を壊さない。3rd party tracker は `MultiTrackerManager<TrackerPacketAdapter>` から immutable snapshot store / composer を通して接続し、diagnostics は selected replay timeline tick / selected time を固定したまま、対象 source alignment がない場合に同じ source の selected tick 以前の `latest-before snapshot` を直前 sample の hold として表示/比較し、future/later snapshot へ fallback しない。対象契約テスト 37 件とサーバービルドは成功し、r2 review は `reports/issue-10-raw-vision-015-review-r2-20260514095053.md` に記録済みで指摘なし。 |
| RAW-VISION-016 | Issue #10 の final validation / docs / review / PR ready を完了する | review | in-progress | RAW-VISION-015 | 検証 report は `reports/issue-10-raw-vision-016-validation-20260514095659.md` に記録済み。設計用語説明は脚注へ統合し、`reports/issue-10-design-terminology-audit-r3-20260514102338.md` と `reports/issue-10-design-terminology-review-r3-20260514103027.md` に記録済みで指摘なし。gpt-5.5 high final review は `reports/issue-10-raw-vision-016-final-review-20260514103501.md` に記録済みで指摘なし。tracking sync、PR #15 本文更新、ユーザー側 UI 確認後の draft 解除判断材料を揃える。 |
