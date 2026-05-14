# RUNTIME-HOST-006 境界調査レポート

## タスク

DebugHost live display を read-side snapshot 境界へ寄せ、UI render tick が tracker operation loop を駆動しない構造を focused tests で固定する。

## 調査結果

- 現状の `Home.razor` の周期更新は `VisionPacketStore.GetSnapshot()`、`TrackedSnapshotStore.GetSnapshot()`、`VisionLiveComparisonSnapshotComposer.CaptureRenderTickSnapshot()` を呼んでおり、`TrackerCoordinator.ProcessPacket` は呼んでいない。
- tracker operation loop は `VisionReceiverService` の UDP receive loop 側から `Tracker.Core.TrackerCoordinator.ProcessPacket` として呼ばれている。
- ただし `Home.razor` は同じ refresh cycle 内で raw / tracked を先に読み、その後 comparison composer が raw / tracked をもう一度読むため、Raw / Tracked / Compare 表示が同一 render tick の snapshot として固定されていない。
- `VisionLiveComparisonSnapshotComposer` は comparison 用 snapshot 内では raw / tracked を clone 済み DTO に変換しているが、Home 全体の render boundary ではない。
- `MultiTrackerManager` は state object を in-place 更新するため、3rd party tracker の packet と metadata を UI 側で直接読むと同一更新由来である保証が弱い。

## RUNTIME-HOST-006 実装方針

- DebugHost live display 用に `VisionLiveDisplayRenderSnapshot` と provider/composer 境界を追加し、1 render tick で raw / tracked / 3rd party を 1 回ずつ読み取る。
- `Home.razor` は `VisionPacketStore` / `TrackedSnapshotStore` を直接 inject せず、live display snapshot provider から得た composite snapshot だけを使う。
- comparison view は composite snapshot 内の fixed comparison snapshot から作る。
- 3rd party tracker は mutable manager を UI render で直接読まず、packet + metadata を clone した read-side snapshot store を経由する。
- profile switch UI 操作は render tick ではないため、R006 の禁止対象に含めない。

## 対象外

- diagnostics sample sidecar schema / writer / bounded lookup 実装。
- `Diagnostics.razor.cs` replay 入力の新 sidecar 切り替え。
- 旧 render snapshot sidecar / legacy diagnostics log の互換救済。
- `Tracker.RuntimeHost` project scaffold。
- RuntimeHost headless normal path。

## focused test 案

- Home live display が `VisionPacketStore` / `TrackedSnapshotStore` を直接 inject しないこと。
- live display snapshot provider が raw / tracked / external tracker read-side snapshot を 1 tick で固定し、同じ `RenderTickId` の Raw / Tracked / Compare 表示へ渡すこと。
- comparison composer が tracker operation loop 型や `ProcessPacket` を参照しないこと。
- 3rd party tracker read-side store が packet と metadata を clone し、capture 後の manager 更新に影響されないこと。
