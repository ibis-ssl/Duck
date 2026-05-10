# TRACKER-019 Evidence

## 目的

- Tracker v1 の integration 観点 verification として late packet、geometry reset、profile switch、observer/event、viewer 切替の確認結果を固定する

## 実行コマンド

- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --disable-build-servers --filter 'FullyQualifiedName~Update_DropsLatePacketsAndDoesNotLetThemContaminateLaterFlushes|FullyQualifiedName~Update_DropsLatePacketsThatFallInsideAnAlreadyCommittedMergeWindow|FullyQualifiedName~Update_EmitsGeometryResetAndDropsPendingFramesFromOldGeometryGeneration|FullyQualifiedName~Update_EmitsGeometryResetWhenGoalGeometryChanges|FullyQualifiedName~Update_WithControlOnlyProfileSwitch_EmitsOnlyProfileSwitched|FullyQualifiedName~Update_OrdersProfileSwitchBeforeWorldFrameCommitted_WhenSwitchAndFrameShareAResult|FullyQualifiedName~ProcessPacket_WhenDerivedEventsExist_NotifiesObserverInEmittedOrder|FullyQualifiedName~RequestProfileSwitch_WithoutPacket_DrainsControlOnlyUpdateAndClearsSnapshotBeforeObserverNotification|FullyQualifiedName~ProcessPacket_WithPendingProfileSwitch_PublishesCommittedFrameAfterApplyingNewProfileContext|FullyQualifiedName~ProcessPacket_WhenGeometryResetOccurs_ClearsTrackedSnapshotBeforeNotifyingObserver'`
- `nl -ba Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs | sed -n '120,610p'`
- `nl -ba Tracker/Tracker.Tests/TrackerCoordinatorTests.cs | sed -n '50,240p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Home.razor | sed -n '30,220p'`

## 結果概要

- focused test は成功した。`Passed: 10 / Failed: 0 / Skipped: 0`
- late packet は engine contract test で 2 観点確認できた。既に commit 済み merge window に落ちる packet も drop され、後続 flush を汚染しない
- geometry reset は engine / coordinator の両方で確認できた。geometry change 時に reset event が先行し、旧 generation の pending frame は破棄される
- profile switch は control-only switch と frame 同梱 switch の両方で確認できた。observer には `profile:fast` が通知され、後続 committed frame は新 profile context を使う
- observer/event は coordinator test で emitted order を確認できた。`world-frame` の後に `kick` と `contact` が通知される
- viewer 切替は automated component test ではなく `Home.razor` の静的確認で担保した。`Raw` / `Tracked` の toggle button と `viewerMode` 分岐が存在し、tracked mode では `TrackedDetailsPanel` が描画される

## 詳細

### late packet

- `TrackerEngineTemporalContractTests.Update_DropsLatePacketsAndDoesNotLetThemContaminateLaterFlushes`
  - 根拠: `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs:127`
  - 確認内容: late packet 受信時に `CommittedFrames` は空で `LatePacketDropCount == 1` になり、その後の flush は `2_000_000_000L` の frame だけを commit する
- `TrackerEngineTemporalContractTests.Update_DropsLatePacketsThatFallInsideAnAlreadyCommittedMergeWindow`
  - 根拠: `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs:236`
  - 確認内容: 既に commit 済み merge window 内へ遅れて到着した packet も drop され、後続 flush は旧 window を復活させない

### geometry reset

- `TrackerEngineTemporalContractTests.Update_EmitsGeometryResetAndDropsPendingFramesFromOldGeometryGeneration`
  - 根拠: `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs:392`
  - 確認内容: geometry reset 後の結果に `GeometryReset` が含まれ、最初の emitted event は `GeometryReset`、commit 対象は新 geometry 世代の frame のみになる
- `TrackerEngineTemporalContractTests.Update_EmitsGeometryResetWhenGoalGeometryChanges`
  - 根拠: `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs:434`
  - 確認内容: field size だけでなく goal geometry 変更でも `GeometryReset` が発火する
- `TrackerCoordinatorTests.ProcessPacket_WhenGeometryResetOccurs_ClearsTrackedSnapshotBeforeNotifyingObserver`
  - 根拠: `Tracker/Tracker.Tests/TrackerCoordinatorTests.cs:57`
  - 確認内容: observer 通知前に tracked snapshot の latest frame が clear され、通知順は `geometry-reset` -> `world-frame:1` になる

### profile switch

- `TrackerEngineTemporalContractTests.Update_WithControlOnlyProfileSwitch_EmitsOnlyProfileSwitched`
  - 根拠: `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs:481`
  - 確認内容: control-only switch は frame を commit せず `ProfileSwitched` のみ emit する
- `TrackerEngineTemporalContractTests.Update_OrdersProfileSwitchBeforeWorldFrameCommitted_WhenSwitchAndFrameShareAResult`
  - 根拠: `Tracker/Tracker.Tests/Contracts/TrackerEngineTemporalContractTests.cs:501`
  - 確認内容: 同一結果に switch と frame が同居する場合は `ProfileSwitched` が `WorldFrameCommitted` より先に出る
- `TrackerCoordinatorTests.RequestProfileSwitch_WithoutPacket_DrainsControlOnlyUpdateAndClearsSnapshotBeforeObserverNotification`
  - 根拠: `Tracker/Tracker.Tests/TrackerCoordinatorTests.cs:144`
  - 確認内容: `RequestProfileSwitch` だけで snapshot は `fast` profile に切り替わり、latest frame と received timestamp は clear され、observer には `profile:fast` が通知される
- `TrackerCoordinatorTests.ProcessPacket_WithPendingProfileSwitch_PublishesCommittedFrameAfterApplyingNewProfileContext`
  - 根拠: `Tracker/Tracker.Tests/TrackerCoordinatorTests.cs:185`
  - 確認内容: pending switch 後の committed frame は `fast` profile metadata を持ち、publisher port も新 profile 側へ切り替わる

### observer/event

- `TrackerCoordinatorTests.ProcessPacket_WhenDerivedEventsExist_NotifiesObserverInEmittedOrder`
  - 根拠: `Tracker/Tracker.Tests/TrackerCoordinatorTests.cs:111`
  - 確認内容: observer 通知順は `world-frame:2` -> `kick:2` -> `contact:2` で、emitted order が保持される

### viewer 切替

- `Home.razor`
  - 根拠: `Tracker/Tracker.Server/Components/Pages/Home.razor:36`
  - 確認内容: `Viewer mode selector` 配下に `Raw` / `Tracked` button が存在する
- `Home.razor`
  - 根拠: `Tracker/Tracker.Server/Components/Pages/Home.razor:62`
  - 確認内容: `viewerMode == ViewerMode.Raw` なら raw viewer、`else` 側では tracked canvas と `TrackedDetailsPanel` を描画する
- `Home.razor`
  - 根拠: `Tracker/Tracker.Server/Components/Pages/Home.razor:103`
  - 確認内容: 初期 mode は `ViewerMode.Raw`
- `Home.razor`
  - 根拠: `Tracker/Tracker.Server/Components/Pages/Home.razor:191`
  - 確認内容: `SelectMode` が `viewerMode` を更新し、button click から切替経路が閉じている
- `Home.razor`
  - 根拠: `Tracker/Tracker.Server/Components/Pages/Home.razor:197`
  - 確認内容: tracked mode からの profile switch 要求は `TrackerProfileRequestService.RequestProfileSwitch(profileName)` を呼び、直後に tracked snapshot を再取得する

## リスク

- viewer 切替は今回 automated UI test を追加しておらず、静的コード確認に依存している
- ただし raw/tracked の描画分岐、初期 mode、click handler、tracked panel 配線はコード上で閉じており、正常系 release 判断に必要な最低限の integration 根拠は揃っている
