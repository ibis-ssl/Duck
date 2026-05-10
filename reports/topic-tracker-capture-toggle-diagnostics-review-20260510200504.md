# Tracker capture toggle diagnostics review

- Task: Tracker diagnostics output location and packet capture runtime toggle
- Scope:
  - `Tracker.Server` packet capture startup initial value and UI On/Off toggle
  - diagnostics log default output/listing under `VisionReceiver:PacketCapture:DirectoryPath`
  - `Tracker:Diagnostics` settings cleanup to `FilePath`
  - diagnostics page generic timeline rendering without ball-specific highlight
  - related tests and documentation
- Reviewer: Codex review, direct parent-side review-only pass
- Validation before review:
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore -m:1 /nr:false`
  - `DOTNET_CLI_HOME="$PWD/.codex-dotnet-home" NUGET_PACKAGES="$PWD/.codex-nuget-packages" dotnet build Tracker/Tracker.Server/Tracker.Server.csproj --no-restore -m:1 /nr:false`

## Findings

No findings on re-review.

## Disposition

The previous High finding is resolved: `VisionPacketCaptureSession.EnsureStarted()` now gates startup on `VisionPacketCaptureRuntimeControl.Enabled`, so a default `PacketCapture:Enabled=false` startup can still begin capture after the UI turns capture On (`Tracker/Tracker.Server/Vision/VisionPacketCaptureSession.cs:54-58`).

The previous Medium finding is resolved: `TrackerCoordinator` no longer caches `sidecarTrackerDiagnosticsLogPath`; when capture is enabled it resolves diagnostics through the current session state on each write (`Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs:346-353`). The added regression tests cover starting from a configured disabled value and writing diagnostics to a new sidecar after re-enable.

## Outcome

Review completed with no findings. I did not rerun the supplied validation commands; this pass reviewed the current diff and related files directly. No additional blocker was found in the packet capture runtime toggle, immediate Off stop path, diagnostics default output/search under `packet-captures`, `Tracker:Diagnostics` FilePath-focused cleanup, or the generic diagnostics timeline behavior.
