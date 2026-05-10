# Tracker diagnostics scrubber layout review

- Task: Diagnostics viewer scrubber and no-page-scroll field layout
- Scope:
  - `/diagnostics` timeline scrubber with continuous selected-entry updates
  - field rendering layout that avoids page scroll interfering with zoom / pan
  - `VisionReceiver:PacketCapture:FlushEachPacket=true` default while startup capture remains disabled
  - console diagnostics suppression via `Logging:LogLevel`
  - README, design, and configuration binding test updates
- Reviewer: Codex reviewer
- Validation before review:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 /nr:false`
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --no-restore -m:1 /nr:false`

## Findings

### Low

- `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor:345`, `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor:346`: render snapshot index は読めたが選択 frame の snapshot がない場合、`renderSnapshotError ??=` が前回の missing-frame error を保持します。連続して snapshot 欠落 frame を scrub すると、表示中の selected frame ではなく直前に欠落した frame number の error が残る可能性があります。field 描画の正常系や前回 High finding の解消は妨げないため blocker ではありませんが、error message も selected entry と同期させるなら missing-frame branch では毎回現在の `selectedEntry.TrackedFrame` で上書きするのが自然です。

## Disposition

- 前回 High finding は解消済み。`Diagnostics.razor` は log load 時に `ReadIndex(...)` で render snapshot gzip を一度 index 化し、scrubber `oninput` 中は `renderSnapshotsByFrame.TryGetValue(...)` の dictionary lookup だけになっています。
- `TrackerRenderSnapshotLogReader.ReadIndex(...)` は `FilePath` / `LastWriteTimeUtc` / `FileLength` が一致する場合に cached index を返すため、同一 render snapshot file に対する repeated scrub / repeated lookup で gzip の再展開と JSON 再 parse を繰り返しません。
- 追加回帰テスト `TrackerRenderSnapshotLogReaderTests.ReadIndex_ReturnsSnapshotsByFrameForRepeatedScrubbing` は、複数 frame の index 化と同一 reader 内の cached index 再利用を確認しています。
- Low finding は非 blocker。snapshot 欠落時の表示 error が selected frame とずれる可能性に限られ、通常の capture sidecar + render snapshot の field scrubber 正常系は成立します。
- selected entry と range value の同期は、timeline click と range selection のどちらも `entries` 内 object を `selectedEntry` に保持し、`SelectedEntryIndex` が同じ list から index を解決するため、一貫しています。
- CSS は render snapshot 表示時に shell / detail を固定 viewport 内へ閉じ、field canvas 側の wheel / drag とページ全体 scroll の干渉を抑える意図に沿っています。
- `Logging:LogLevel:Tracker.Server.Tracking.TrackerCoordinator=Warning` は `TrackerCoordinator` の `LogInformation` structured log を抑制しますが、file output は `AppendTrackerDiagnosticsFile(...)` が logger level 判定の外で実行されるため継続します。packet capture / render snapshot writer も別 category です。
- `appsettings.json` の `PacketCapture.Enabled=false` / `FlushEachPacket=true` と `VisionReceiverConfigurationResolverTests` の期待は一致しています。

## Outcome

No blocker findings. 前回 High finding は解消済みです。Low finding は error 表示の同期に限定されるため、必要なら follow-up で修正してください。
