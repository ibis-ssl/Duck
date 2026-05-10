# Tracker diagnostics scrubber layout evidence

## Scope

- `/diagnostics` timeline scrubber for continuous frame switching.
- Render snapshot index/cache so scrubber movement does not reread gzip on every input event.
- Viewport-contained diagnostics field layout to avoid page scroll conflicting with field zoom / pan.
- `PacketCapture.Enabled=false` with `FlushEachPacket=true` as the default startup/capture behavior.
- Console tracker diagnostics suppression through `Logging:LogLevel` without disabling capture or file diagnostics output.

## Post-review fix

- The review report `reports/topic-tracker-diagnostics-scrubber-layout-review-20260510203435.md` recorded a non-blocking Low finding about stale missing-frame error text.
- The implementation now overwrites `renderSnapshotError` with the currently selected frame number whenever a selected frame has no render snapshot.
- No additional review was run after this fix per user instruction.

## Validation

- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter 'TrackerRenderSnapshotLogReaderTests|VisionReceiverConfigurationResolverTests' -m:1 /nr:false`
  - Passed: 11
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 /nr:false`
  - Passed: 124
- `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --no-restore -m:1 /nr:false`
  - Build succeeded with 0 warnings and 0 errors.
