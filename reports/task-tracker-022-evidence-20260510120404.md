# TRACKER-022 Evidence

## 目的

- `VisionReceiver` を profile-aware にし、tracker profile と同名の receiver profile を起動時および runtime switch 後に解決できることを固定する

## 実行コマンド

- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --disable-build-servers --filter 'FullyQualifiedName~VisionReceiverConfigurationResolverTests|FullyQualifiedName~VisionReceiverServiceTests|FullyQualifiedName~TrackerConfigurationBindingTests|FullyQualifiedName~TrackerProfileRequestServiceTests|FullyQualifiedName~TrackerProfileControlViewStateTests'`
- `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --disable-build-servers`
- `git diff -- Tracker/Tracker.Server/Program.cs Tracker/Tracker.Server/Vision/VisionReceiverOptions.cs Tracker/Tracker.Server/Vision/VisionReceiverConfigurationResolver.cs Tracker/Tracker.Server/Vision/VisionReceiverRuntimeOptionsStore.cs Tracker/Tracker.Server/Vision/VisionReceiverProfileSwitchObserver.cs Tracker/Tracker.Server/Vision/VisionReceiverService.cs Tracker/Tracker.Server/README.md Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md Tracker/Tracker.Core/Design/tracker-architecture-plan.md Tracker/Tracker.Core/Design/tasks-status.md Tracker/Tracker.Core/Design/phases-status.md Tracker/Tracker.Tests/VisionReceiverConfigurationResolverTests.cs`

## 結果概要

- `VisionReceiver` は top-level 設定に加えて `Profiles.<name>` を持てるようになった
- 起動時は `Tracker:ActiveProfileName` と同名の receiver profile を優先解決し、未定義なら top-level `VisionReceiver` へ fallback する
- runtime profile switch 完了後は `ITrackerObserver` 経由で receiver 設定を再解決し、`VisionReceiverService` が receive loop を cancel して socket を reopen する
- startup 時の active profile 表示も `Tracker:ActiveProfileName` と一致するようにした
- focused test は `Passed: 16 / Failed: 0 / Skipped: 0`
- full test は `Passed: 97 / Failed: 0 / Skipped: 0`

## 詳細

### 実装

- `Tracker/Tracker.Server/Vision/VisionReceiverOptions.cs`
  - `Profiles` と `VisionReceiverProfileOptions` を追加した
- `Tracker/Tracker.Server/Vision/VisionReceiverConfigurationResolver.cs`
  - active tracker profile 名から有効な receiver 設定を求める resolver を追加した
- `Tracker/Tracker.Server/Vision/VisionReceiverRuntimeOptionsStore.cs`
  - 現在有効な receiver 設定と config change token を保持する runtime store を追加した
- `Tracker/Tracker.Server/Vision/VisionReceiverProfileSwitchObserver.cs`
  - `ProfileSwitched` を受けて receiver profile を切り替える observer を追加した
- `Tracker/Tracker.Server/Vision/VisionReceiverService.cs`
  - runtime store の change token を監視し、receiver 設定変更時に receive loop を抜けて socket を開き直すようにした
- `Tracker/Tracker.Server/Tracking/TrackedSnapshotStore.cs`
  - startup 時の active profile 表示が `Tracker:ActiveProfileName` とずれないよう、初期 active profile を constructor から受け取れるようにした
- `Tracker/Tracker.Server/Program.cs`
  - startup 時に `Tracker:ActiveProfileName` から receiver 初期設定と `TrackedSnapshotStore` の初期 active profile を解決し、observer を DI 登録するようにした

### 設計・文書

- `Tracker/Tracker.Core/Design/tracker-architecture-plan.md`
  - 設定セットに raw vision 受信元を含め、profile switch 後に receiver が追従する規則を追記した
- `Tracker/Tracker.Server/Design/raw-vision-viewer-plan.md`
  - `VisionReceiver.Profiles` と socket reopen 規則を追記した
- `Tracker/Tracker.Server/README.md`
  - `VisionReceiver:Profiles:<name>` の意味と tracker profile 連動を追記した

### テスト

- `VisionReceiverConfigurationResolverTests.Resolve_WithMatchingProfile_UsesProfileSpecificValues`
  - receiver profile が同名 tracker profile を上書き解決できることを確認した
- `VisionReceiverConfigurationResolverTests.Resolve_WithoutMatchingProfile_FallsBackToTopLevelValues`
  - receiver profile が未定義でも top-level 設定へ fallback することを確認した
- `VisionReceiverConfigurationResolverTests.RuntimeOptionsStore_ApplyConfiguration_CancelsPreviousSnapshot`
  - runtime store の設定切替時に旧 receive loop を cancel できることを確認した
- `VisionReceiverConfigurationResolverTests.ProfileSwitchObserver_OnProfileSwitched_AppliesMatchingReceiverProfile`
  - `ProfileSwitched` observer が receiver profile を apply することを確認した
- `VisionReceiverConfigurationResolverTests.StartupRegistrations_ResolveReceiverProfileFromTrackerActiveProfile`
  - startup 時に `Tracker:ActiveProfileName` から receiver 初期設定を解決できることを確認した
- `TrackerProfileControlViewStateTests.TrackedSnapshotStore_UsesConfiguredInitialActiveProfile`
  - startup 時の tracked snapshot が non-default profile 名を保持し、UI 表示の初期 active profile が tracker / receiver の起動状態と一致することを確認した

## リスク

- `Tracker/Tracker.Server/appsettings.json` には user 側の未コミット変更があるため、今回の task では config サンプル自体は変更していない
- そのため新しい `VisionReceiver.Profiles` を実際に使うには、利用側で `appsettings` へ該当 profile を追加する必要がある
