# Sub-agent実行レポート

## タスク

- 目的: TRACKER-059 のため、現在の diagnostics replay timeline がどの周期に合わせて進むかを調査し、最速 tracker source cadence に合わせる設計・実装方針を出す。
- タスク種別: investigation

## sub-agentを使う理由

- 理由: ユーザー指示により調査・設計・実装・テストは gpt-5.5 high sub-agent を使う。親は manager として tracking、判断、commit/push を管理する。

## 対象範囲

- 対象: `/diagnostics` playback / scrub / timeline index、saved alignment sidecar、Field source frame 解決、Vision/render snapshot と tracker snapshot の cadence 差の扱い。
- 追加対象: 新規 capture 保存時の alignment 生成 cadence。単に UI replay timeline を最速化するだけではなく、Vision/render snapshot と tracker snapshots の union timeline、または fastest source cadence を含む alignment record を保存する方式まで TRACKER-059 の設計範囲に含める。

## 対象外

- 対象外: 実装変更、外部 ER-Force プロセス操作、既存ローカル差分 `Tracker/Tracker.Server/appsettings.json` の変更、PR #9 外の unrelated cleanup。

## 実行コマンド

- 実行コマンド:
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/design-doc-maintainer/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
- `sed -n '1,260p' reports/tracker-059-fastest-timeline-investigation-20260513173834.md`
- `sed -n '1,240p' reports/tracker-058-saved-alignment-design-20260513063637.md`
- `sed -n '1,260p' reports/tracker-058-saved-alignment-implementation-20260513064540.md`
- `git status --short --branch`
- `rg -n --glob '!**/bin/**' --glob '!**/obj/**' "class DiagnosticsPlaybackState|DiagnosticsPlaybackState|GetInterval|ShouldApplyTick|Playback|FastForward|Scrub|Selected|timeline|Timeline|TrackerDiagnosticsComparisonViewStateReader|LoadFieldSourceFrame|FieldSource|saved-session-alignment|TrackerSnapshotAlignment|RenderSnapshot|Comparison" Tracker/Tracker.Server Tracker/Tracker.Tests Tracker/Tracker.CaptureReplay -g '*.cs' -g '*.razor'`
- `rg -n --glob '!**/bin/**' --glob '!**/obj/**' "diagnostics|playback|timeline|saved-session-alignment|tracker-snapshot-alignment|render snapshot|Field source|フィールド|replay" Tracker/Tracker.Core/Design Tracker/Tracker.Server/README.md -g '*.md'`
- `rg --files Tracker/Tracker.Server | rg 'Diagnostics|Tracker.*(Snapshot|Comparison|Alignment|Replay)|VisionPacketCapture|Render'`
- `rg --files Tracker/Tracker.Tests | rg 'Diagnostics|CaptureReplay|CaptureOn|Replay|Comparison'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs | sed -n '1,240p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '520,760p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs | sed -n '1,620p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs | sed -n '1,1240p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs | sed -n '1,280p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs | sed -n '1,420p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs | sed -n '1,140p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs | sed -n '1,240p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs | sed -n '1,320p'`
- `nl -ba Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs | sed -n '120,190p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor | sed -n '1,380p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '400,520p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs | sed -n '1,260p'`
- `nl -ba Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs | sed -n '1,1040p'`
- `nl -ba Tracker/Tracker.Server/README.md | sed -n '80,100p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '174,204p'`
- `nl -ba Tracker/Tracker.Core/Design/tasks-status.md | sed -n '1,220p'`

## 対象ファイル

- 変更または確認したファイル:
- 変更: `reports/tracker-059-fastest-timeline-investigation-20260513173834.md`
- 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsPlaybackState.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonUiState.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Dispatch.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs`
- 確認: `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
- 確認: `Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldViewFactory.cs`
- 確認: `Tracker/Tracker.Server/Components/Pages/DiagnosticsFieldOverlayRenderModelFactory.cs`
- 確認: `Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
- 確認: `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
- 確認: `Tracker/Tracker.Server/README.md`
- 確認: `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- 確認: `Tracker/Tracker.Core/Design/tasks-status.md`
- 確認のみ: `Tracker/Tracker.Server/appsettings.json` は既存 dirty diff があることだけ確認し、変更していない。

## 指摘事項

- 指摘要約または「指摘なし」:
- 現在の `/diagnostics` Play / Fast Forward / scrub は tracker snapshot sidecar の cadence ではなく、`TrackerDiagnosticsLogReader.ReadFile(...)` が返す diagnostics log entry 配列 `entries` を timeline としている。`Diagnostics.razor` の左 timeline も `entries` を列挙し、scrubber も `entries.Count` と selected index を基準にする。
- `DiagnosticsPlaybackState.GetNextIndex(...)` は `entryCount` に対して Play は 1 entry、Fast Forward は倍率由来 step で index を進めるだけで、tracker source sample の時刻列を見ない。`GetInterval(...)` も current/next diagnostics entry の timestamp delta を使う。
- `Diagnostics.razor.cs` の playback loop は `SelectedEntryIndex` から次の diagnostics entry index を計算し、`SelectEntryByIndex(...)` で `selectedEntry`、render snapshot、comparison state を同期する。従って ER-FORCE が diagnostics entry 間に複数 snapshot を持っていても、それらは playback tick として選択されない。
- render snapshot は `selectedEntry.TrackedFrame` で `renderSnapshotsByFrame` から引かれる。render snapshot sidecar により多くの frame が存在しても、現行 UI は diagnostics entry と対応しない render frame を scrub / playback の tick として選ばない。
- comparison / Field source は `TrackerDiagnosticsComparisonUiState.Load(...)` から `TrackerDiagnosticsComparisonViewStateReader.Load(...)` / `LoadFieldSourceFrame(...)` を呼ぶが、入力は常に selected diagnostics entry である。保存済み alignment も `DiagnosticsLineNumber` で引くため、line に対応しない tracker snapshot は中間 tick にならない。
- `TrackerSnapshotAlignmentLogWriter.CaptureDiagnosticsEntry(...)` は diagnostics entry が記録された時点で source ごとの最新 tracker snapshot を alignment record として保存する。現在の alignment sidecar は「diagnostics entry -> tracker snapshot」の対応表であり、「tracker snapshot -> replay tick」の独立 timeline ではない。
- `TrackerPacketSnapshotLogWriter` は source ごとの最新 snapshot と全 snapshot sidecar record を持っているため、高速 ER-FORCE sample 自体は `tracker-packet-snapshots.jsonl` に保存され得る。ただし現行 `/diagnostics` はその全 record cadence を timeline selection に使っていない。
- `TrackerSnapshotReplayReader.ReadSession(...)` は CLI 用 `SnapshotInputs` を tracker snapshot timestamp 順に並べるが、`/diagnostics` page の Play / scrub はこの reader を使っていない。CLI の `trackerSnapshot` 出力は snapshot を列挙できる一方、UI playback cadence の根拠にはなっていない。
- 現在の挙動分類は「Vision cadence」寄り。ただし厳密には Vision raw cadence そのものではなく、throttle 済み diagnostics log entry cadence / selected render snapshot cadence である。tracker cadence でも、Vision と tracker の mixed fastest cadence でもない。

## 結果

- 結果:
- 判定: 現行実装では ER-FORCE tracker source cadence が Vision / render snapshot cadence より速くても、ER-FORCE の中間 sample は `/diagnostics` playback tick として再生されない。Play / Fast Forward / scrub は diagnostics entry index に従い、各 selected entry で保存済み alignment または legacy nearest により tracker source frame を解決する。
- ユーザー期待「速い方に合わせる。Vision は nearest / last frame を保持してカクカクになる」を満たすには、selected diagnostics entry list とは別に unified replay timeline を作る必要がある。
- 推奨最小設計: `tracker-packet-snapshots.jsonl` の tracker snapshot `ReceivedAt` と render snapshot `ReceivedAt` / diagnostics entry timestamp を同じ capture-time 軸へ載せ、source ごとの sample を union した `TrackerDiagnosticsReplayTimeline` を log 選択時に index 化する。tick の timestamp は `TrackedFrame.timestamp` ではなく capture-time `ReceivedAt` を基準にする。ER-FORCE の `TrackedFrame.timestamp` は ibis own と時刻系が違うため、fastest timeline の時刻軸へ使わない。
- 追加設計判断: 保存時 alignment sidecar も diagnostics entry 単位だけでは不十分。現在の `TrackerSnapshotAlignmentLogWriter.CaptureDiagnosticsEntry(...)` は diagnostics line ごとに source 最新 snapshot を保存するため、ER-FORCE が Vision より高速な場合に中間 sample の比較点が保存されない。正確な比較には、新規 capture 保存時点で fast tracker samples 分の alignment record を作る必要がある。
- 保存時 alignment 生成の推奨最小設計: `TrackerPacketSnapshotLogWriter.Append(...)` で tracker snapshot が保存された時点、または render snapshot / diagnostics entry が保存された時点の両方を alignment writer に通知し、capture-time `ReceivedAt` の union timeline を構成する。各 alignment record は `TimelineTimestamp` 相当の capture-time、`TimelineKind`、参照する render snapshot frame number / render receivedAt、参照する diagnostics line、参照する tracker snapshot record index、source key、matching rule、delta、status を持つ。
- 高速 tracker tick の保存規則: ER-FORCE snapshot が 20ms 間隔、Vision/render が 100ms 間隔なら、20/40/60/80ms の tracker tick でも alignment record を保存し、同じ last-known または nearest Vision/render snapshot frame を参照する。これにより replay 時に UI が後から union timeline を推定するだけでなく、保存済み比較点として同じ Vision frame が複数 fast tracker alignment records から参照される。
- 低速 Vision tick の保存規則: Vision/render snapshot が進んだ tick では、その時点に対応する tracker source の latest-before または nearest/current snapshot を参照する alignment record を保存する。source ごとに高速/低速が混在しても、比較点は capture-time 軸で deterministic に再現できるようにする。
- alignment sidecar schema は既存 record を破壊せず拡張するのが最小。既存 `DiagnosticsLineNumber` / `TrackerSnapshotRecordIndex` は維持し、tracker-only tick では `DiagnosticsLineNumber` を last-known diagnostics line として持つか、nullable 化が必要なら schema version を上げる。実装時の安全側は schema version 2 の `ReplayTimelineRecord` 追加、または別 sidecar `tracker-replay-alignment.jsonl` の導入。PR scope を抑えるなら既存 file に optional fields を追加し、reader は v1/v2 両対応にする。
- timeline entry は少なくとも `TimelineIndex`、`Timestamp`、`Kind`、`DiagnosticsLineNumber?`、`RenderFrameNumber?`、`TrackerSnapshotRecordIndex?`、`SourceRole`、`SourceLabel`、`RemoteEndpoint` を持つ。表示上の selected entry は timeline entry から解決した last-known diagnostics entry / render snapshot と、exact tracker snapshot または source ごとの latest-before tracker snapshot の組にする。
- Vision / render snapshot 解決は、fast tracker tick の時刻に対して `ReceivedAt <= tick.Timestamp` の latest-before render snapshot を優先する。先頭だけ prior frame がない場合は nearest-after を fallback とする。これにより高速 tracker tick が複数続いても同じ Vision frame を保持し、期待どおり Vision 側がカクカクになる。
- tracker source 解決は、tick 自体が tracker snapshot の場合はその record index を exact に使う。別 source の Field / overlay は同じ tick timestamp に対する latest-before または saved alignment の代表を使う。既存 selected diagnostics entry 用の `saved-session-alignment` lookup は残し、diagnostics entry tick の互換 path として使う。
- existing UI への最小接続は、`entries` だけを scrubber count にするのではなく unified timeline entry count を使うこと。`selectedEntry` は timeline entry から last-known diagnostics entry として派生させ、既存 detail panel / Settings / comparison panel の大半を維持する。`selectedRenderSnapshot` は selected diagnostics entry の tracked frame 直引きではなく timeline tick timestamp から latest-before render snapshot を引く。
- design doc 影響: user-visible playback contract が変わるため、実装前に `tracker-server-cli-ui-detail-design.md` と `Tracker.Server/README.md` へ「Play / Fast Forward / scrub は fastest available source cadence の unified timeline」「Vision/render は last-known hold」「tracker timestamp ではなく capture-time receivedAt 基準」を追記する必要がある。architecture doc は capture-time alignment の既存記述があるため、必要なら replay timeline index の一文追加で足りる。
- TDD方針: Red test では Vision/render snapshots を 0ms / 100ms、ER-FORCE tracker snapshots を 0ms / 20ms / 40ms / 60ms / 80ms / 100ms のように作る。unified replay timeline が 6 個以上の fast tracker tick を含むこと、tick 20/40/60/80ms で同じ Vision/render frame 0ms が保持されること、100ms tick で次の Vision/render frame へ進むことを assert する。
- TDD方針: 保存時 alignment writer の Red test を先に追加する。fixture は Vision/render snapshots 0ms / 100ms、ER-FORCE tracker snapshots 0/20/40/60/80/100ms とし、保存される alignment sidecar が ER-FORCE fast samples 分の records を含むこと、20/40/60/80ms の records が同じ Vision/render frame 0ms を参照すること、100ms record が次の Vision/render frame 100ms を参照することを assert する。
- TDD方針: diagnostics entry 単位だけの alignment writer に戻る regression を検出するため、diagnostics log line が 0ms / 100ms の 2 行しかない fixture でも、alignment sidecar record count が 2 ではなく fast tracker sample 数以上になることを固定する。
- TDD方針: ER-FORCE `TrackedFrame.timestamp` は ibis own と非重複の大きな値にして、`ReceivedAt` 以外で timeline を作る regression を検出する。`TrackerDiagnosticsComparisonViewStateTests` または新規 `TrackerDiagnosticsReplayTimelineTests` で pure index contract を先に固定し、その後 `DiagnosticsPlaybackStateTests` / UI state test で playback next index と selected render snapshot hold を固定する。
- TDD方針: Field source / overlay について、fast tracker tick で `External` または `ER-FORCE` を選ぶとその tick の tracker snapshot semantic summary が表示され、`Vision Input` / ibis tracker は held render snapshot の raw / tracked data を維持することを assert する。
- 実装候補ファイル: 新規 `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsReplayTimelineReader.cs` または `TrackerDiagnosticsComparisonViewStateReader` 内の小さな timeline index、`Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`、`Diagnostics.razor`、`DiagnosticsPlaybackState.cs`、`TrackerRenderSnapshotLogReader.cs`、`TrackerDiagnosticsComparisonUiState.cs`、`TrackerDiagnosticsComparisonViewStateReader.cs`、関連 tests、README / design doc。
- 追加実装候補ファイル: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs`、`Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs`、`Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`、`Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs`、`Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs`、`Tracker/Tracker.Tests/TrackerCaptureOnSessionSnapshotContractTests.cs`、`Tracker/Tracker.Tests/TrackerComparisonSourceTddTests.cs`。保存時 alignment cadence の変更は UI より前に Red/Green で固定する。
- 実装順序の推奨: 1) 保存時 alignment sidecar が fastest source cadence の比較点を持つことを TDD で実装、2) saved alignment を読む pure replay timeline index を実装、3) `/diagnostics` Play / Fast Forward / scrub を unified timeline へ接続。保存が不十分なまま UI だけ速くすると、後から正確な比較点を復元できないため順序を逆にしない。
- 分割提案: 最小実装は 1 task で可能。ただし UI 変更が膨らむ場合は、先に pure timeline index + TDD を実装し、次に `Diagnostics.razor.cs` の selected timeline 接続と Field source 表示を接続する 2 段階に分けるのが安全。

## リスク

- 未解決のリスクまたは後続対応:
- 現行 alignment sidecar は diagnostics-line-driven なので、fast tracker tick 全件を表す source にはならない。追加指示どおり、TRACKER-059 では保存時 alignment 生成 cadence そのものを修正対象にする必要がある。UI replay 側で tracker snapshot sidecar record の `ReceivedAt` から後付け推定するだけでは、保存済み比較点としての正確性が不足する。
- alignment schema を v1 optional field 追加で済ませるか、schema version 2 / 別 sidecar に分けるかは parent 判断が必要。互換性を優先するなら v1 reader を維持し、v2 record がある場合だけ unified replay timeline に使う。
- 保存時に tracker packet arrival と render snapshot creation のどちらを event source として alignment writer へ通知するかで責務境界が変わる。`TrackerPacketSnapshotLogWriter` 側から通知すると fast tracker cadence は取りやすいが render latest state を参照する必要がある。`TrackerCoordinator` 側で union event を集約すると正確だが変更範囲が広がる。
- `TrackerPacketSnapshotLogWriter` と `TrackerRenderSnapshotCaptureWriter` の `ReceivedAt` はどちらも capture-time だが、別 event stream で記録されるため、latest-before window / tolerance を明示しないと端点で frame が期待より進む可能性がある。
- UI が selected entry 前提で組まれているため、tracker-only tick では detail panel に表示する diagnostics entry が last-known になる。表示上「timeline tick」と「held diagnostics/render frame」を区別する小さな model / label がないと、ユーザーが同じ Vision frameを複数 tick で見ていることを判断しにくい。
- Long capture では unified timeline が tracker snapshot sidecar record 数に比例して大きくなる。TRACKER-055 の cache 方針を守り、log 選択時に index 化し、tick / scrub ごとに sidecar 全体を再読込しないことが必須。
- Fast Forward の step は現行 `speedMultiplier / 4` entry skip である。unified timeline 導入後は tick 数が増えるため、同じ倍率の体感速度が変わる可能性がある。通常 Play は capture-time delta、Fast Forward は delta / multiplier を維持し、必要なら step skip を別途見直す。
- `Tracker/Tracker.Server/appsettings.json` の既存 dirty diff (`Tracker:Receive:Enabled=true`) はユーザー実行用ローカル設定の可能性があるため変更していない。
