# Sub-agent実行レポート

## タスク

`TRACKER-041` 全 tracker 保存方針に対する実装・テストの現状確認。

## sub-agentを使う理由

親エージェントは実装・調査を行わず、サブエージェントの報告を読んで判断するため。

## 対象範囲

- `TrackerConnectionLib` 実装が self 除外をしているか確認する
- `TrackerConnectionLibThirdPartyTrackerTests` が self 除外を契約化しているか確認する
- 全 tracker packet 保存・自身ログとの重複保持方針へ変更する場合の修正範囲を特定する

## 対象外

- ファイル修正
- 実装変更
- テスト変更
- PR の ready 化

## 実行コマンド

- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/implementation-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/report-output-manager/SKILL.md`
- `rg -n "TRACKER-041|TrackerConnectionLib|third.*party|tracker packet|self 除外|all trackers" /home/ibis/.codex/memories/MEMORY.md`
- `sed -n '1,240p' reports/tracker-041-all-trackers-implementation-audit-20260512111218.md`
- `git status --short`
- `rg --files TrackerConnectionLib/src Tracker/Tracker.Tests | rg 'TrackerConnectionLib|ThirdParty|CaptureReplay|Diagnostics|Playback|Tracker.Tests.csproj'`
- `sed -n '1,260p' reports/tracker-041-tdd-tests-20260512105825.md`
- `sed -n '1,260p' reports/tracker-041-implementation-20260512110523.md`
- `sed -n '1,260p' Tracker/Tracker.Tests/TrackerConnectionLibThirdPartyTrackerTests.cs`
- `sed -n '1,260p' Tracker/Tracker.Tests/Tracker.Tests.csproj`
- `sed -n '1,260p' TrackerConnectionLib/src/MultiTrackerManager.cs`
- `sed -n '1,220p' TrackerConnectionLib/src/TrackerState.cs`
- `sed -n '1,220p' TrackerConnectionLib/src/TrackerPacketAdapter.cs`
- `sed -n '1,220p' TrackerConnectionLib/src/ITrackerPacket.cs`
- `sed -n '1,260p' TrackerConnectionLib/src/TrackerWrapperPacketDeserializer.cs`
- `sed -n '1,220p' TrackerConnectionLib/src/UdpTrackerReceiver.cs`
- `rg -n "MultiTrackerManager|TrackerState<|TrackerPacketAdapter|TrackerWrapperPacket|thirdparty|3rdparty|CaptureReplay|Snapshot|Diagnostics|Playback|sidecar|comparison|tracker packet|TrackerConnectionLib" Tracker TrackerConnectionLib -g '*.cs' -g '*.razor' -g '*.csproj' -g '*.md'`
- `sed -n '1,260p' Tracker/Tracker.Tests/CaptureReplayTests.cs`
- `sed -n '1,260p' Tracker/Tracker.Tests/TrackerCoordinatorDiagnosticsCaptureTests.cs`
- `sed -n '1,260p' Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
- `sed -n '1,260p' Tracker/Tracker.Tests/TrackerDiagnosticsLogReaderTests.cs`
- `rg -n "class TrackerRenderSnapshot|record TrackerRenderSnapshot|RenderSnapshot|render-snapshots|TrackerDiagnostics|Tracker packet|TrackerPacket|CaptureSession|metadata|DiagnosticsLogPath|CaptureReplay|Replay" Tracker/Tracker.Server Tracker/Tracker.CaptureReplay Tracker/Tracker.Core -g '*.cs' -g '*.razor' -g '*.md'`
- `rg -n "TrackerConnectionLib|MultiTrackerManager|UdpTrackerReceiver|TrackerWrapperPacket|TrackerPacketAdapter|ThirdParty|thirdparty|3rdparty" . -g '*.cs' -g '*.razor' -g '*.csproj' -g '*.md'`
- `nl -ba TrackerConnectionLib/src/MultiTrackerManager.cs | sed -n '1,150p'`
- `nl -ba Tracker/Tracker.Tests/TrackerConnectionLibThirdPartyTrackerTests.cs | sed -n '1,140p'`
- `nl -ba Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md | sed -n '1,125p'`
- `nl -ba Tracker/Tracker.Core/Design/tasks-status.md | sed -n '1,42p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs | sed -n '1,180p'`
- `nl -ba Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs | sed -n '1,230p'`
- `nl -ba Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs | sed -n '1,220p'`
- `nl -ba Tracker/Tracker.CaptureReplay/ReplayOptions.cs | sed -n '1,280p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs | sed -n '1,530p'`
- `nl -ba Tracker/Tracker.Server/Components/Pages/Diagnostics.razor | sed -n '1,240p'`
- テストは実行していない。今回の指示が現状確認であり、必要なら読み取り専用コマンドだけ実行する条件だったため。

## 対象ファイル

- `TrackerConnectionLib/src/MultiTrackerManager.cs`
- `TrackerConnectionLib/src/TrackerState.cs`
- `TrackerConnectionLib/src/TrackerPacketAdapter.cs`
- `TrackerConnectionLib/src/ITrackerPacket.cs`
- `TrackerConnectionLib/src/TrackerWrapperPacketDeserializer.cs`
- `TrackerConnectionLib/src/UdpTrackerReceiver.cs`
- `Tracker/Tracker.Tests/TrackerConnectionLibThirdPartyTrackerTests.cs`
- `Tracker/Tracker.Tests/Tracker.Tests.csproj`
- `Tracker/Tracker.Tests/CaptureReplayTests.cs`
- `Tracker/Tracker.Tests/TrackerCoordinatorDiagnosticsCaptureTests.cs`
- `Tracker/Tracker.Tests/DiagnosticsPlaybackStateTests.cs`
- `Tracker/Tracker.Tests/TrackerDiagnosticsLogReaderTests.cs`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs`
- `Tracker/Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs`
- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor`
- `Tracker/Tracker.CaptureReplay/CaptureReplayRunner.cs`
- `Tracker/Tracker.CaptureReplay/ReplayOptions.cs`
- `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`
- `Tracker/Tracker.Core/Design/tasks-status.md`
- `reports/tracker-041-tdd-tests-20260512105825.md`
- `reports/tracker-041-implementation-20260512110523.md`

## 指摘事項

- Blocking normal-path problems:
  - 現 production 実装は全 tracker 保存ではない。`MultiTrackerManager.ProcessPacket` は `IsSelfPacket(packet)` が true の場合に return しており、`uuid` と `sourceName` が self identity と一致する packet を保存しない。参照: `TrackerConnectionLib/src/MultiTrackerManager.cs:35-40`, `TrackerConnectionLib/src/MultiTrackerManager.cs:76-80`
  - 現テストは self 除外を明示的に契約化している。`ProcessPacket_WhenPacketMatchesIbisIdentity_ExcludesSelfPacket` は self packet 処理後に `Assert.Empty(manager.Trackers)` を期待しており、全 tracker 保存方針と矛盾する。参照: `Tracker/Tracker.Tests/TrackerConnectionLibThirdPartyTrackerTests.cs:43-57`
  - 現実装は 3rdparty tracker packet を `TrackerState.LastPacket` としてメモリ上の最新状態に保持するだけで、CaptureOn session folder、metadata、comparison sidecar JSONL、diagnostics、`Tracker.CaptureReplay`、diagnostics playback へ接続していない。`TRACKER-042` 以降の未着手範囲に残っている。参照: `Tracker/Tracker.Core/Design/tasks-status.md:32-35`
  - 現 diagnostics playback は diagnostics log entry と ibis render snapshot の再生であり、3rdparty tracker snapshot の timeline / field 表示 / replay 比較はない。参照: `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs:47-55`, `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor.cs:308-393`, `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor:120-186`
- ユーザー確認が必要な capability gap:
  - 追加方針「存在する tracker packet はすべて保存し、自身の詳細ログとは重複保持を許容する」は、現設計の「self除外」と衝突する。設計・tracking の `self除外` 記述を変更するか、TRACKER-041 のローカル修正に閉じるか親判断が必要。参照: `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md:64-70`, `Tracker/Tracker.Core/Design/tasks-status.md:14-18`
  - 「3rd partyトラッカーもスナップショットを保持し、再生できる」の snapshot 定義を、raw `TrackerWrapperPacket` payload、`TrackedFrame` summary、field rendering 用 DTO、または比較済み record のどれにするか親判断が必要。
- Non-blocking concerns:
  - `TrackerState.LastPacket` は `TrackerPacketAdapter` 経由で raw `TrackerWrapperPacket` を保持しているため、全 tracker 保存へ変える最小修正の土台はある。参照: `TrackerConnectionLib/src/TrackerState.cs`, `TrackerConnectionLib/src/TrackerPacketAdapter.cs`
  - 現 `TrackerConnectionLib` は source identity 単位の最新状態保持であり、時系列 snapshot 履歴ではない。再生要件には別途 append-only sidecar または snapshot store が必要。

## 結果

- 結論: 現 production 実装は self 除外をしている。`MultiTrackerManager(string selfUuid, string selfSourceName)` で self identity を保持し、`ProcessPacket` 冒頭で一致 packet を破棄する。
- 結論: 現 tests は self 除外を契約化している。クラスコメント、個別 test、`CreateManager()` が self identity constructor を使う構造まで含めて、全 tracker 保存方針と矛盾する。
- 結論: 現 tests は 3rdparty packet の「最新状態保持」は検証しているが、snapshot 履歴、CaptureOn session folder への保存、metadata relative path、diagnostics reader、`Tracker.CaptureReplay`、diagnostics playback での再生は検証していない。
- 結論: 現 production 実装は 3rdparty tracker packet の snapshot replay 実装ではない。`TrackerRenderSnapshotCaptureWriter` / `TrackerRenderSnapshotLogReader` は ibis `TrackerFrame` の render snapshot だけを扱い、`CaptureReplayRunner` は vision capture を再投入して ibis committed frame を出すだけで、3rdparty comparison sidecar を読まない。
- 全 tracker 保存方針へ変える場合の最小修正範囲:
  - `Tracker/Tracker.Tests/TrackerConnectionLibThirdPartyTrackerTests.cs`: self packet を `Assert.Empty` ではなく保存されることへ変更し、3rdparty と self が同時に保持される契約を追加する。クラス・テストコメントも「ibis自身を除外」から「全 tracker 保存」へ修正する。
  - `TrackerConnectionLib/src/MultiTrackerManager.cs`: `IsSelfPacket` による early return を削除するか、比較対象判定ではなく metadata flag 化する。self identity constructor を残す場合は `TrackerState` へ `IsSelf` 等を持たせ、保存は維持する。
  - `TrackerConnectionLib/src/TrackerState.cs`: 必要なら `IsSelf` / `SourceIdentity` / snapshot用途の identity metadata を追加する。現 `LastPacket` は最新のみなので、履歴再生には不十分。
  - `Tracker/Tracker.Core/Design/tracker-server-cli-ui-detail-design.md`, `Tracker/Tracker.Core/Design/tasks-status.md`, `Tracker/Tracker.Core/Design/phases-status.md`: self除外前提を全 tracker 保存・self詳細ログとの重複許容へ同期する。
- 3rdparty tracker snapshot を保持し replay / diagnostics / playback できるようにする場合の最小修正範囲:
  - `TrackerConnectionLib` または `Tracker.Server/Tracking` に append-only snapshot writer / record 型を追加し、各 tracker packet の `receivedAt`、remote endpoint、`uuid`、`sourceName`、tracked frame number/timestamp、payload base64 または field rendering に必要な summary を時系列で保存する。
  - CaptureOn session metadata に comparison / tracker snapshot sidecar relative path を追加し、Capture Off / 再On で session folder と writer が切り替わる契約を追加する。
  - `Tracker.Server/Tracking/TrackerRenderSnapshotCaptureWriter.cs` または別 writer に 3rdparty tracker snapshot sidecar 出力を接続する。既存 writer は ibis `TrackerFrame` 専用なので、既存 schema を破壊せず別 sidecar にするのが最小リスク。
  - `Tracker.Server/Tracking/TrackerRenderSnapshotLogReader.cs` とは別に 3rdparty snapshot reader / index を追加するか、既存 reader に optional sidecar 解決を追加する。
  - `Tracker.Server/Components/Pages/Diagnostics.razor.cs` / `.razor`: diagnostics log entry の playback index に合わせて 3rdparty snapshot を引き、source 切替、frame number/timestamp、ball/robot count、field 表示または比較 summary を出す。
  - `Tracker.CaptureReplay` (`CaptureReplayRunner`, `ReplayOptions`, formatter/tests): metadata relative path から 3rdparty snapshot sidecar を読み、ibis committed frame の timestamp 近傍で replay / detail 出力できるようにする。
  - tests: `TrackerConnectionLibThirdPartyTrackerTests` の全 tracker 保存契約、CaptureOn sidecar 保存 test、metadata relative path test、diagnostics reader test、CaptureReplay出力 test、diagnostics playback/view-state test を追加または更新する。

## リスク

- 現 TRACKER-041 の focused test 成功は旧 self 除外契約の成功であり、追加ユーザー方針の成功証跡としては使えない。
- self packet を保存するように変えると、`ActiveTrackerUuid`、`Trackers` key、UI の source 表示、比較対象選択で self と 3rdparty の区別が必要になる。単に early return を消すだけでは「保存」と「比較対象」の意味が混ざる。
- snapshot replay まで含める場合、TRACKER-041 の範囲を超えて TRACKER-042/043/044/045 の設計・tracking・テストを再同期する必要がある。
- 現確認ではテストを実行していないため、コンパイル・実行状態は既存レポートの focused test 成功記録に依存する。
