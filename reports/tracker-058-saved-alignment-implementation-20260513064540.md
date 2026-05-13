# Sub-agent実行レポート

## タスク

- 目的: TRACKER-058 の保存時 tracker snapshot alignment を TDD で実装し、ER-Force replay Field 表示の再発防止を追加する。
- タスク種別: implementation

## sub-agentを使う理由

- 理由: ユーザー指示により実装・調査・レビュー・テストは gpt-5.5 high sub-agent を使う。親は manager として scope、review、commit/push を管理する。

## 対象範囲

- 対象: `tracker-snapshot-alignment.jsonl` 保存/読込、metadata 連携、diagnostics replay / Field source での保存済み alignment 優先、時間軸一致 regression test、必要最小限の ER-Force Docker test helper 組み込み。

## 対象外

- 対象外: 既存 capture の完全救済、外部 ER-Force プロセスの強制起動/停止、大規模 socket abstraction、unrelated cleanup、`Tracker/Tracker.Server/appsettings.json` の既存ローカル差分変更。

## 実行コマンド

- 実行コマンド:
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/implementation-executor/SKILL.md`
- `sed -n '1,240p' /home/ibis/AI/CodexSkill/skills/tdd-executor/SKILL.md`
- `sed -n '1,220p' /home/ibis/AI/CodexSkill/skills/sub-agent-task-manager/SKILL.md`
- `sed -n '1,260p' reports/tracker-058-er-force-replay-investigation-20260513062747.md`
- `sed -n '1,280p' reports/tracker-058-saved-alignment-design-20260513063637.md`
- `sed -n '1,240p' reports/tracker-058-saved-alignment-implementation-20260513064540.md`
- `sed -n '1,260p' Tracker/Tracker.Core/Design/tasks-status.md`
- `rg -n "DiagnosticsLog|RenderSnapshot|CapturePacket|TrackerPacketSnapshotLogWriter|VisionPacketCaptureSessionState|WriteRecord\\(" Tracker/Tracker.Server Tracker/Tracker.Tests -g '*.cs'`
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests" -m:1 /nr:false` (Red: 2 failed)
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests" -m:1 /nr:false` (Green: 23 passed)
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerCaptureOnSessionSnapshotContractTests" -m:1 /nr:false` (6 passed)
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~CaptureReplayTests" -m:1 /nr:false` (8 passed)
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --filter "FullyQualifiedName~TrackerDiagnosticsComparisonViewStateTests|FullyQualifiedName~TrackerCaptureOnSessionSnapshotContractTests|FullyQualifiedName~CaptureReplayTests|FullyQualifiedName~VisionPacketCaptureTests|FullyQualifiedName~TrackerCoordinatorDiagnosticsCaptureTests" -m:1 /nr:false` (45 passed)
- `git diff --check` (pass)
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj -m:1 /nr:false` (229 passed / 1 failed: 既存 dirty `Tracker/Tracker.Server/appsettings.json` の `Tracker:Receive:Enabled=true` により `TrackerConfigurationBindingTests.AppsettingsJson_ExposesTigersAlignedTrackerDefaults` が失敗)
- `docker --version && docker compose version`

## 対象ファイル

- 変更または確認したファイル:
- 変更: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentRecord.cs`
- 変更: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogReader.cs`
- 変更: `Tracker/Tracker.Server/Tracking/TrackerSnapshotAlignmentLogWriter.cs`
- 変更: `Tracker/Tracker.Server/Vision/VisionPacketCaptureFile.cs`
- 変更: `Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs`
- 変更: `Tracker/Tracker.Server/Tracking/TrackerPacketSnapshotLogWriter.cs`
- 変更: `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsComparisonViewStateReader.cs`
- 変更: `Tracker/Tracker.Server/Tracking/TrackerSnapshotReplayReader.cs`
- 変更: `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
- 変更: `Tracker/Tracker.Server/Tracking/TrackerCoordinator/Diagnostics.cs`
- 変更: `Tracker/Tracker.Server/Program.cs`
- 変更: `Tracker/Tracker.Tests/TrackerDiagnosticsComparisonViewStateTests.cs`
- 変更: `Tracker/Tracker.Tests/TrackerCaptureOnSessionSnapshotContractTests.cs`
- 変更: `Tracker/Tracker.Tests/CaptureReplayTests.cs`
- 変更: `Tracker/Tracker.Server/README.md`
- 変更: `reports/tracker-058-saved-alignment-implementation-20260513064540.md`
- 確認: `Tracker/Tracker.Core/Design/Ref/ibis/docker/README.md`
- 確認: `Tracker/Tracker.Core/Design/Ref/ibis/docker/dev/README.md`
- 確認: `Tracker/Tracker.Core/Design/Ref/ibis/scripts/docker-dev.sh`
- 確認のみ: `Tracker/Tracker.Server/appsettings.json`。既存 dirty diff (`Tracker:Receive:Enabled=true`) は触っていない。

## 指摘事項

- 指摘要約または「指摘なし」:
- 指摘なし。TDD Red は期待どおり、保存済み alignment fixture を置いても現行実装が `nearest-timestamp` を返すことで失敗した。
- Red 失敗1: `TrackerDiagnosticsComparisonViewStateTests.LoadFieldSourceFrame_WithSavedAlignment_UsesExternalSnapshotWhenDataTimestampRangesDoNotOverlap` は expected `saved-session-alignment` / actual `nearest-timestamp`。
- Red 失敗2: `TrackerDiagnosticsComparisonViewStateTests.Load_WithSavedAlignment_AggregatesSameLabelRemoteEndpointSourcesByCaptureTime` は expected `saved-session-alignment` / actual `nearest-timestamp`。
- 時間軸検査は、selected diagnostics entry の capture-time `receivedAt` と chosen external snapshot の `trackerSnapshotReceivedAt` 差分を `TimestampDeltaNs` として assert し、fixture 許容値 10ms / 5ms 以内で固定した。nearest data timestamp に戻ると matching rule と selected frame が外れるため検出できる。

## 結果

- 結果:
- `tracker-snapshot-alignment.jsonl` の最小 model / reader / writer を追加した。snapshot sidecar とは別 file とし、metadata には `TrackerSnapshotAlignmentPath` と `TrackerSnapshotAlignmentLog` を追加した。
- alignment record は diagnostics line / tracked frame / diagnostics receivedAt / session-relative ticks / own timestamp / source role + label + uuid + remote endpoint / tracker snapshot record index / tracker snapshot receivedAt / matching rule / delta / status を持つ。
- 新規 capture では diagnostics log 出力後、publish / snapshot sidecar 反映済みの source snapshot から alignment writer が source key ごとに record を保存する。`TrackerPacketSnapshotLogWriter` は source key ごとの最新 snapshot と 0 始まり record index を保持する。
- `/diagnostics` comparison と Field source は metadata から alignment sidecar を解決し、保存済み alignment がある場合は `saved-session-alignment` を優先する。alignment がない既存 capture は既存の nearest timestamp best-effort を維持し、壊さない。
- ER-FORCE のように label / uuid が同じで remote endpoint が複数ある場合、保存上は role + label + uuid + remote endpoint を保持する。UI の `External` / source label aggregate は receivedAt delta、record index、remote endpoint 順の tie-break で代表 snapshot を選ぶ。
- `Tracker.CaptureReplay` は alignment sidecar がある metadata では `trackerComparison rule=saved-session-alignment ...` を出すようにした。
- Docker helper は `Tracker/Tracker.Core/Design/Ref/ibis/scripts/docker-dev.sh --sim erforce -d` を README に追記した。Docker 本体は起動せず、`docker --version` / `docker compose version` の確認だけ実施した。
- focused validation は 45 passed。`git diff --check` は pass。
- full `Tracker.Tests` は 229 passed / 1 failed。失敗は今回未変更の `Tracker/Tracker.Server/appsettings.json` 既存 dirty diff (`Tracker:Receive:Enabled=true`) が既定値 contract (`Assert.False(trackerOptions.Receive.Enabled)`) と衝突したため。

## リスク

- 未解決のリスクまたは後続対応:
- alignment writer は diagnostics entry 時点で writer が保持している source ごとの最新 snapshot を保存する。外部 tracker packet が diagnostics entry の直後に届くケースでは、次 entry まで対応が遅れる可能性がある。
- 長時間 capture では `TrackerPacketSnapshotLogWriter` が source key ごとの最新 snapshot だけを保持するため memory 増加は source 数に限定されるが、完全な過去 nearest map ではない。今回の正常系は新規 capture の replay / Field source を動かす最小実装として扱う。
- full test の 1 failure は既存ローカル設定差分によるもの。ユーザー指示どおり `Tracker/Tracker.Server/appsettings.json` は revert していない。
- Docker ER-Force 環境はコマンド確認と README 追記のみで、実コンテナ起動はしていない。
