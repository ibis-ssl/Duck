# タスク状況

ルール: このファイルは `task-breakdown-planner`、`task-consistency-manager`、`progress-sync-manager` からのみ更新する。

## 現在のタスク

- ID: RUNTIME-HOST-011
- Title: RuntimeHost / DebugHost split の final review / tracking sync / PR ready を完了する
- Phase: review
- Status: in-progress
- Size: large
- Dependencies: RUNTIME-HOST-010.
- Exit Criteria:
  - RUNTIME-HOST-010 の validation evidence と review result を含めて tracking を最終同期する。
  - gpt-5.5 high final review、必要な修正と r2 review、commit、Draft PR #17 ready 化を完了する。
  - 既知 hold `RuntimeHostDependencyBoundaryContractTests.DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop` の扱いを最終判断として report / PR に残す。

## 完了済みタスク

- `RUNTIME-HOST-001`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了した。`Tracker/Design/` へ設計資料と active tracking を統合し、RuntimeHost / DebugHost の命名、責務境界、AutoRef 将来内包、loop isolation、旧ログ互換非要件、BreakingChanges 不要を設計へ固定した。gpt-5.5 high review は初回 blocking 2 件を修正し、r2 で no findings を確認した。Draft PR #17 を作成した。
  - Review Evidence:
    - `reports/runtime-host-001-design-review-20260514155548.md`
    - `reports/runtime-host-001-design-fix-20260514160144.md`
    - `reports/runtime-host-001-design-review-r2-20260514160734.md`
- `RUNTIME-HOST-002`: RuntimeHost / DebugHost project dependency boundary contract を追加した。`Tracker.RuntimeHost` が `Tracker.DebugHost` / `Tracker.Server` / Web UI / diagnostics replay UI project を参照しないこと、RuntimeHost source が diagnostics logging / replay / Blazor UI namespace を直接参照しないこと、DebugHost が read-side host であることを Red contract として固定した。
  - Implementation Evidence:
    - `reports/runtime-host-002-implementation-20260514163841.md`
    - `reports/runtime-host-002-boundary-context-20260514164124.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDependencyBoundaryContractTests -m:1 /nr:false` は 3 failed / 0 passed。現時点では `Tracker.RuntimeHost` project/source root と `Tracker.DebugHost` root が未存在のため、意図した Red contract として assertion failure になっている。
  - Review Evidence:
    - `reports/runtime-host-002-review-20260514164528.md`
    - `reports/runtime-host-002-review-fix-20260514164850.md`
    - `reports/runtime-host-002-review-r2-20260514165133.md`
    - r2 review で blocking findings なし。DebugHost loop ownership marker の将来 false positive 可能性は hold として記録した。
- `RUNTIME-HOST-003`: diagnostics sample boundary と legacy degraded contract を追加した。diagnostics sample tick が tracker committed frame cadence / `WorldFrameCommitted` に依存しないこと、Diagnostics `Vision Input` が diagnostics sample sidecar から復元されること、旧 render snapshot sidecar が unsupported / degraded legacy であることを Red contract として固定し、設計文書の task reference を固定一覧へ同期した。
  - Implementation Evidence:
    - `reports/runtime-host-003-implementation-20260514165750.md`
    - `reports/runtime-host-003-boundary-context-20260514165750.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDiagnosticsSampleBoundaryContractTests -m:1 /nr:false` は 3 failed / 0 passed。`RuntimeHostDiagnosticsSampleBoundaryContractTests` は compile 済みで、diagnostics sample sidecar 未実装と legacy degraded 表示未実装を assertion failure として固定している。
  - Review Evidence:
    - `reports/runtime-host-003-review-20260514170652.md`
    - review で blocking findings なし。diagnostics sample sidecar schema と raw Vision payload DTO の詳細は RUNTIME-HOST-007 の green 実装側で確認する hold として記録した。
- `RUNTIME-HOST-004`: `Tracker.Server` を `Tracker.DebugHost` project / namespace / 起動経路へ rename した。active project、namespace、launch path、README、solution / project reference、CaptureReplay / tests の参照を `Tracker.DebugHost` へ揃え、existing debug normal path を維持した。
  - Implementation Evidence:
    - `reports/runtime-host-004-implementation-20260514171550.md`
    - `reports/runtime-host-004-rename-impact-20260514171550.md`
    - `reports/runtime-host-004-verification-20260514172634.md`
    - Red: `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter FullyQualifiedName~RuntimeHostDebugHostRenameContractTests -m:1 /nr:false` は 3 failed / 0 passed。`Tracker.DebugHost` folder/project 未存在と active reference 未更新を assertion failure として確認した。
    - Green: 同 focused test は 3 passed。`dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false`、`dotnet build Tracker/Tracker.CaptureReplay/Tracker.CaptureReplay.csproj -m:1 /nr:false`、`dotnet build Duck.slnx -m:1 /nr:false` は成功した。
  - Review Evidence:
    - `reports/runtime-host-004-review-20260514172921.md`
    - review で blocking findings なし。full `Tracker.Tests` は RUNTIME-HOST-002 / RUNTIME-HOST-003 の既存 Red contract があるため未実行とした。
- `RUNTIME-HOST-005`: tracker operation loop の共有 runtime boundary を `Tracker.Core/Runtime` へ抽出した。`TrackerCoordinator`、`ITrackerPacketPublisher`、`TrackerPublisherOptions`、`TrackedSnapshot`、`TrackedSnapshotStore`、`UdpTrackerPacketPublisher` を UI 非依存 Core runtime 境界へ寄せ、DebugHost は UDP decode / raw store / capture 後に Core coordinator を呼ぶ adapter とした。旧 diagnostics log / render snapshot sidecar 生成は Core operation loop から外し、performance 優先と RuntimeHost 再利用境界を固定した。
  - Implementation Evidence:
    - `reports/runtime-host-005-implementation-20260514180031.md`
    - `reports/runtime-host-005-verification-20260514180308.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostSharedOperationLoopBoundaryTests|FullyQualifiedName~TrackerCoordinatorFrameFlowTests|FullyQualifiedName~TrackerCoordinatorResetAndProfileTests|FullyQualifiedName~TrackerProfileRequestServiceTests|FullyQualifiedName~TrackerCoordinatorDiagnosticsCaptureTests" -m:1 /nr:false` は 15 passed。
    - `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false` は成功し、`git diff --check` も成功した。
  - Review Evidence:
    - `reports/runtime-host-005-review-20260514180308.md`
    - review で blocking findings なし。DebugHost read-side UI 化、diagnostics sample sidecar、RuntimeHost scaffold は RUNTIME-HOST-006 以降へ残す。
- `RUNTIME-HOST-006`: DebugHost live display を read-side snapshot 境界へ寄せた。`VisionLiveDisplaySnapshotProvider` が 1 render tick で raw / tracked / 3rd party tracker snapshot を固定し、`Home.razor` は raw / tracked store を直接 inject せず同一 composite snapshot から Raw / Tracked / Compare を派生する。`ExternalTrackerSnapshotStore` は `MultiTrackerManager` update event から packet / metadata を clone 済み DTO として保持し、render path が mutable manager state を直接読まない構造にした。
  - Implementation Evidence:
    - `reports/runtime-host-006-boundary-context-20260514181333.md`
    - `reports/runtime-host-006-implementation-20260514182342.md`
    - `reports/runtime-host-006-verification-20260514182549.md`
    - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~RuntimeHostDebugHostReadSideSnapshotBoundaryTests|FullyQualifiedName~VisionLiveComparisonViewStateTests|FullyQualifiedName~TrackedVisionViewStateTests" -m:1 /nr:false` は 18 passed。
    - `dotnet build Tracker/Tracker.DebugHost/Tracker.DebugHost.csproj -m:1 /nr:false` と `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false` は成功し、`git diff --check` も成功した。
  - Review Evidence:
    - `reports/runtime-host-006-review-20260514182549.md`
    - review で blocking findings なし。diagnostics sample sidecar と RuntimeHost scaffold は RUNTIME-HOST-007 以降へ残す。
- `RUNTIME-HOST-007`: DebugHost diagnostics sample sidecar fast path を実装した。UI 非依存 `DiagnosticsSampleHostedService` が設定値 `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` に従って latest raw / tracker snapshot を `diagnostics-samples.jsonl` へ保存し、Diagnostics replay / Field は sample sidecar の bounded lookup と semantic summary を主経路にする。旧 render snapshot sidecar だけの session は unsupported / degraded legacy として扱い、高コストな互換 path は復活させない。
  - Implementation Evidence:
    - `reports/runtime-host-007-implementation-20260514184219.md`
    - `reports/runtime-host-007-review-fix-20260514185807.md`
    - `reports/runtime-host-007-configurable-sample-interval-20260514191628.md`
    - `reports/runtime-host-007-verification-20260514184527.md`
    - focused / affected tests、`Tracker.DebugHost` build、`Tracker.Tests` build、`git diff --check` は sub-agent report で成功を確認した。
  - Review Evidence:
    - `reports/runtime-host-007-review-20260514184501.md`
    - `reports/runtime-host-007-review-r2-20260514190459.md`
    - `reports/runtime-host-007-review-r3-20260514191820.md`
    - `reports/runtime-host-007-review-r4-20260514192425.md`
    - 初回 review の blocking 2 件を修正し、r2 / r3 / r4 で no findings を確認した。
- `RUNTIME-HOST-008`: `Tracker.RuntimeHost` headless project scaffold と configuration を追加した。`Tracker.RuntimeHost` project、Program、options / DI bootstrap、solution entry を追加し、Web UI / diagnostics replay / capture viewer を持たない headless host として起動できる scaffold を作った。`RuntimeHost:OperationLoopIntervalMilliseconds` を設定として公開し、0 以下は host start validation error になる contract を追加した。
  - Implementation Evidence:
    - `reports/runtime-host-008-implementation-20260514192917.md`
    - adjusted R008 focused は 7 passed。broad focused は 23 passed / 1 failed で、失敗は R008 範囲外の既存 DebugHost loop ownership assertion として review で確認した。
    - `dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false`、`dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`、`git diff --check` は sub-agent report で成功を確認した。
  - Review Evidence:
    - `reports/runtime-host-008-review-20260514193633.md`
    - `reports/runtime-host-008-review-fix-20260514194021.md`
    - `reports/runtime-host-008-review-r2-20260514194042.md`
    - 初回 review の XML summary blocker を修正し、r2 で no findings を確認した。
- `RUNTIME-HOST-009`: RuntimeHost tracker operation loop と official packet publish normal path を実装した。RuntimeHost は headless SSL-Vision receiver、latest packet buffer、`RuntimeHost:OperationLoopIntervalMilliseconds` に従う operation loop、Core `TrackerCoordinator` / `TrackedSnapshotStore` / `UdpTrackerPacketPublisher` を DI で組み立て、fake SSL-Vision input が coordinator / publisher / latest snapshot store へ届く normal path を固定した。missing active profile は DebugHost と同じく明示失敗に揃えた。
  - Implementation Evidence:
    - `reports/runtime-host-009-implementation-20260514194405.md`
    - `reports/runtime-host-009-review-fix-20260514200105.md`
    - R009 focused は 3 passed、review-fix 後 `RuntimeHostOperationLoopTests` は 5 passed。broad focused は 26 passed / 1 failed で、失敗は R009 範囲外の既存 DebugHost ownership assertion として review で確認した。adjusted focused は 26 passed。
    - `dotnet build Tracker/Tracker.RuntimeHost/Tracker.RuntimeHost.csproj -m:1 /nr:false`、`dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false`、`git diff --check` は sub-agent report で成功を確認した。
  - Review Evidence:
    - `reports/runtime-host-009-review-20260514195653.md`
    - `reports/runtime-host-009-review-r2-20260514200945.md`
    - 初回 review の missing active profile fallback blocker を修正し、r2 で no findings を確認した。latest packet buffer は latest 優先のまま R010 manual evidence 後判断の hold とした。
- `RUNTIME-HOST-010`: RuntimeHost / DebugHost split の focused validation と manual evidence を揃えた。RuntimeHost / DebugHost の focused tests と build、diagnostics sample evidence、legacy degraded evidence、DebugHost UI normal path、RuntimeHost headless normal path を sub-agent report に残し、`.gitignore` に runtime / diagnostics capture artifacts を追加してローカル生成物が通常差分へ混入しないことを確認した。
  - Validation Evidence:
    - `reports/runtime-host-010-validation-20260514201701.md`
    - RuntimeHost focused tests は 10 passed、adjusted boundary focused は 10 passed、diagnostics focused は 15 passed。
    - `Tracker.RuntimeHost` / `Tracker.DebugHost` / `Tracker.Tests` build、RuntimeHost 短時間 headless 起動、DebugHost HTTP 200 起動確認、`git diff --check`、`.gitignore` artifact ignore 確認は sub-agent report で成功を確認した。
    - broad focused は既知の `RuntimeHostDependencyBoundaryContractTests.DebugHost_ReadsLatestImmutableSnapshotOrPublishedOutputInsteadOfOwningTrackerOperationLoop` 1 件のみ failure。現設計では DebugHost の Core loop adapter 残存を許容しているため、R010 blocker ではなく R011 final review で扱う hold とした。
  - Review Evidence:
    - `reports/runtime-host-010-review-20260514202428.md`
    - review で blocking findings なし。既知 DebugHost ownership assertion failure は hold 継続が妥当と確認した。

## 固定残タスク

- 固定一覧は `RUNTIME-HOST-001` から `RUNTIME-HOST-011` とする。RuntimeHost / DebugHost 分離 scope では `RAW-VISION-*` や `TRACKER-*` を追加しない。
- `RUNTIME-HOST-001`: `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了する。設計資料を `Tracker/Design/` 配下へ移動し、active tracking を統合し、RuntimeHost / DebugHost の責務境界、AutoRef 将来内包、loop isolation、旧ログ互換非要件を設計へ反映する。
- `RUNTIME-HOST-002`: RuntimeHost / DebugHost project dependency boundary contract を追加する。RuntimeHost が DebugHost / Web UI / diagnostics replay UI に依存しないこと、DebugHost が tracker operation loop の主責務を持たず read-side であることを failing tests として固定する。
- `RUNTIME-HOST-003`: diagnostics sample boundary と legacy degraded contract を追加する。diagnostics sample tick が tracker committed frame cadence に依存しないこと、Diagnostics `Vision Input` が diagnostics sample sidecar から復元されること、旧 render snapshot sidecar が unsupported / degraded legacy であることを failing tests として固定する。
- `RUNTIME-HOST-004`: `Tracker.Server` を `Tracker.DebugHost` project / namespace / 起動経路へ rename する。現 `Tracker.Server` の Web UI / diagnostics / replay / capture viewer 責務を DebugHost として明確化し、既存 debug normal path を壊さない。
- `RUNTIME-HOST-005`: tracker operation loop の共有 runtime boundary を抽出する。SSL-Vision input、tracker update、official tracker packet publish、latest tracker snapshot 公開の境界を UI / diagnostics logging から分離し、RuntimeHost から再利用できる形にする。
- `RUNTIME-HOST-006`: DebugHost live display を read-side snapshot 境界へ寄せる。UI render tick ごとに raw / tracked / 3rd party tracker の latest immutable snapshot を固定し、Web rendering tick が tracker operation loop を駆動しない構造にする。
- `RUNTIME-HOST-007`: DebugHost diagnostics sample sidecar fast path を実装する。diagnostics sample tick で latest raw snapshot と latest tracker snapshot を固定して保存し、新規 capture / logging の bounded lookup を主経路にする。
- `RUNTIME-HOST-008`: `Tracker.RuntimeHost` headless project scaffold と configuration を追加する。Web UI / diagnostics replay / capture viewer を持たない headless host として起動できる project / Program / options / DI bootstrap / solution entry を追加し、`RuntimeHost:OperationLoopIntervalMilliseconds` を設定として公開する。
- `RUNTIME-HOST-009`: RuntimeHost tracker operation loop と official packet publish normal path を実装する。SSL-Vision input、tracker state update、official tracker packet publish、DebugHost が読める latest tracker snapshot 公開を headless host の正常系として成立させ、RuntimeHost 実行周期を `RuntimeHost:OperationLoopIntervalMilliseconds` で制御する。
- `RUNTIME-HOST-010`: RuntimeHost / DebugHost split の focused validation と manual evidence を揃える。RuntimeHost / DebugHost build、focused tests、diagnostics sample evidence、legacy degraded evidence、DebugHost UI normal path、RuntimeHost headless normal path の証跡を report に残す。
- `RUNTIME-HOST-011`: RuntimeHost / DebugHost split の final review / tracking sync / PR ready を完了する。gpt-5.5 high review、必要な修正と r2、tracking sync、report references、validation evidence、Draft PR #17 ready 化を完了する。

## 統合済み履歴

- Core / tracker engine 系の旧 tracking は `Tracker/Design/Archive/Core/tasks-status.md` と `Tracker/Design/Archive/Core/phases-status.md` に保存する。
- DebugHost / raw vision / diagnostics 系の旧 tracking は `Tracker/Design/Archive/DebugHost/tasks-status.md` と `Tracker/Design/Archive/DebugHost/phases-status.md` に保存する。
- 旧 `RAW-VISION-013` から `RAW-VISION-016` は PR #15 `Issue #10 Vision画面に分割表示とオーバーレイを追加する` として `2026-05-14T03:29:25Z` に merge 済み。
- `RAW-VISION-017` として開始した loop isolation 設計は、RuntimeHost / DebugHost 分離方針へ scope を拡張したため、以後は `RUNTIME-HOST-001` へ統合する。

## タスク一覧

| ID | タスク | フェーズ | 状態 | 依存関係 | 完了条件 |
| --- | --- | --- | --- | --- | --- |
| RUNTIME-HOST-001 | `Tracker.RuntimeHost` / `Tracker.DebugHost` 分離方針と設計資料統合を完了する | design | complete; draft PR #17 | PR #15 merge complete | `Tracker/Design/` へ設計資料と active tracking を統合し、RuntimeHost / DebugHost の命名、責務境界、AutoRef 将来内包、loop isolation、旧ログ互換非要件、BreakingChanges 不要を設計へ固定し、gpt-5.5 high r2 review で blocking findings なしを確認した。 |
| RUNTIME-HOST-002 | RuntimeHost / DebugHost project dependency boundary contract を追加する | verification | complete; draft PR #17 | RUNTIME-HOST-001 | RuntimeHost が DebugHost / Web UI / diagnostics replay UI に依存しないこと、DebugHost が tracker operation loop の主責務を持たず read-side であることを Red test として固定し、r2 review で blocking findings なしを確認した。 |
| RUNTIME-HOST-003 | diagnostics sample boundary と legacy degraded contract を追加する | verification | complete; draft PR #17 | RUNTIME-HOST-002 | diagnostics sample tick が tracker committed frame cadence / `WorldFrameCommitted` に依存しないこと、Diagnostics `Vision Input` が diagnostics sample sidecar から復元されること、旧 render snapshot sidecar が unsupported / degraded legacy であることを Red contract として固定し、review で blocking findings なしを確認した。 |
| RUNTIME-HOST-004 | `Tracker.Server` を `Tracker.DebugHost` project / namespace / 起動経路へ rename する | implementation | complete; draft PR #17 | RUNTIME-HOST-003 | 現 `Tracker.Server` の Web UI / diagnostics / replay / capture viewer 責務を `Tracker.DebugHost` として明確化し、既存 debug normal path、README、launch settings、solution / project reference を維持し、review で blocking findings なしを確認した。 |
| RUNTIME-HOST-005 | tracker operation loop の共有 runtime boundary を抽出する | implementation | complete; draft PR #17 | RUNTIME-HOST-004 | `Tracker.Core/Runtime` に UI 非依存 shared operation loop、publisher、latest snapshot store を抽出し、DebugHost を Core coordinator 呼び出し adapter に寄せた。focused tests / build / review で blocking findings なしを確認した。 |
| RUNTIME-HOST-006 | DebugHost live display を read-side snapshot 境界へ寄せる | implementation | complete; draft PR #17 | RUNTIME-HOST-005 | `VisionLiveDisplaySnapshotProvider` と `ExternalTrackerSnapshotStore` により DebugHost live display が UI render tick ごとに latest immutable snapshot を固定し、Web rendering tick が tracker operation loop を駆動しないことを focused tests / build / review で確認した。 |
| RUNTIME-HOST-007 | DebugHost diagnostics sample sidecar fast path を実装する | implementation | complete; draft PR #17 | RUNTIME-HOST-003, RUNTIME-HOST-006 | UI 非依存 `DiagnosticsSampleHostedService` が設定値 `VisionReceiver:PacketCapture:DiagnosticsSampleIntervalMilliseconds` に従って latest raw / tracker snapshot を `diagnostics-samples.jsonl` へ保存し、Diagnostics replay / Field は sample sidecar の bounded lookup と semantic summary を主経路にする。focused / affected tests、build、diff-check は sub-agent report で green。初回 review は blocking 2 件、review-fix 後 r2 は Pass、設定化後 r3 は no findings、RuntimeHost 実行周期設定化要件追加後 r4 は no findings。 |
| RUNTIME-HOST-008 | `Tracker.RuntimeHost` headless project scaffold と configuration を追加する | implementation | complete; draft PR #17 | RUNTIME-HOST-005 | Web UI / diagnostics replay / capture viewer を持たない `Tracker.RuntimeHost` project、Program / options / DI bootstrap / solution entry を追加し、tracker only と将来 tracker + AutoRef mode の境界を表現する。`RuntimeHost:OperationLoopIntervalMilliseconds` を設定として公開し、0 以下は起動時 validation error とする contract を focused tests / build / review / commit / Draft PR #17 update 付きで固定した。 |
| RUNTIME-HOST-009 | RuntimeHost tracker operation loop と official packet publish normal path を実装する | implementation | complete; draft PR #17 | RUNTIME-HOST-007, RUNTIME-HOST-008 | RuntimeHost が SSL-Vision input を受け、`RuntimeHost:OperationLoopIntervalMilliseconds` で制御される実行周期に従って tracker state を更新し、official tracker packet を publish し、DebugHost が読める latest tracker snapshot を公開する正常系を focused tests / build / review / commit / Draft PR #17 update 付きで成立させた。 |
| RUNTIME-HOST-010 | RuntimeHost / DebugHost split の focused validation と manual evidence を揃える | review | complete; draft PR #17 | RUNTIME-HOST-009 | RuntimeHost / DebugHost の focused tests と build、diagnostics sample evidence、legacy degraded evidence、DebugHost UI normal path、RuntimeHost headless normal path の証跡を report に残し、task review で no findings を確認した。 |
| RUNTIME-HOST-011 | RuntimeHost / DebugHost split の final review / tracking sync / PR ready を完了する | review | in-progress | RUNTIME-HOST-010 | gpt-5.5 high review、必要な修正と r2、tracking sync、report references、validation evidence、commit 履歴、Draft PR #17 description を最新化し、PR ready 判断を完了する。 |
