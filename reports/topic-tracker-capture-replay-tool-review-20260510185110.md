# Tracker capture replay tool review

## 対象

- PR: https://github.com/ibis-ssl/Duck/pull/5
- Branch: `feat/tracker-capture-replay-tool`
- Base: `main`
- Scope:
  - `Tracker/Tracker.CaptureReplay`
  - packet capture sidecar
  - tracker diagnostics log sidecar
  - `/diagnostics` log viewer
  - related tests and docs

## Reviewer

- reviewer: Codex review agent

## Findings

1. [High] `rawFrame` / `rawBalls` が `trackedFrame` と同じ detection group を指していません。
   - `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs:256`
   - `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs:270`
   - `Tracker/Tracker.CaptureReplay/Program.cs:101`
   - `Tracker/Tracker.CaptureReplay/Program.cs:108`
   - `Tracker/Tracker.CaptureReplay/Program.cs:119`
   - `TrackerEngine.Update()` は reorder window により、現在入力した packet で過去の buffered frame を commit できます。この実装では diagnostics log と CaptureReplay detail filter が「現在入力 packet の raw count」と「flush された committed frame」を同じ行で比較しているため、`raw-balls==1 && balls>=2` のような調査結果が false positive / false negative になります。今回の主目的である ball 分裂調査で、raw と tracked の対応を誤読させる可能性が高いです。

2. [Medium] `--settings Tracker.Server/appsettings.json` replay が `Tracker:RuntimeOverrides` を反映しません。
   - `Tracker/Tracker.CaptureReplay/Program.cs:333`
   - `Tracker/Tracker.CaptureReplay/Program.cs:353`
   - `Tracker/Tracker.CaptureReplay/Program.cs:363`
   - `Tracker/Tracker.CaptureReplay/Program.cs:411`
   - Server 側は profile 設定に runtime override を重ねたものを engine に渡しますが、CaptureReplay の appsettings 読み込みは profile base と built-in default の merge だけです。ユーザーが `RuntimeOverrides` で tuning した環境を `--settings appsettings.json --profile sim` で再生すると、実行時と異なる tracker settings になり、再現結果がずれます。metadata の `ResolvedTrackerOptions.EngineSettings` から読む経路はこの問題を避けられますが、README で appsettings 形式も supported としているため通常利用で踏みます。

3. [Medium] `/diagnostics` が default / configured diagnostics log を見つけられません。
   - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs:21`
   - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs:30`
   - `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs:330`
   - `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs:345`
   - Reader は `VisionReceiver:PacketCapture:DirectoryPath` 配下の `*.tracker-diagnostics.log` だけを列挙します。一方、capture disabled かつ `Tracker:Diagnostics:FilePath=null` の通常設定では coordinator は `AppContext.BaseDirectory/tracker-diagnostics-*.log` に書きますし、`FilePath` 指定時は任意 path にも書きます。そのため `/diagnostics` は diagnostics log が存在しても `No tracker diagnostics logs found.` になり得ます。

## Disposition

- Follow-up required before treating the PR as complete.
- Finding 1 blocks reliable investigation because the tool/page can report misleading raw/tracked mismatches.
- Finding 2 should be fixed or documented as unsupported before relying on appsettings-based replay for tuning checks.
- Finding 3 should be fixed if `/diagnostics` is intended to read tracker diagnostics logs generally, not only capture sidecars.

## Validation Notes

- Reviewed `git diff main...HEAD`.
- Reviewed:
  - `Tracker/Tracker.CaptureReplay/Program.cs`
  - `Tracker/Tracker.Server/Vision/VisionPacketCapture*`
  - `Tracker/Tracker.Server/Tracking/TrackerCoordinator.cs`
  - `Tracker/Tracker.Server/Tracking/TrackerDiagnosticsLogReader.cs`
  - `Tracker/Tracker.Server/Components/Pages/Diagnostics.razor` and `.css`
  - related tests/docs/report updates
- Did not modify production code.
- Did not rerun validation commands in this review pass. Parent-provided validation was:
  - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerDiagnosticsLogReaderTests"`
  - `dotnet test Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore --filter "FullyQualifiedName~TrackerCoordinatorTests.ProcessPacket_WithPacketCaptureSession"`
  - `dotnet build Tracker/Tracker.Tests/Tracker.Tests.csproj --no-restore`
- Additional test gaps:
  - CaptureReplay detail filter should cover delayed committed frames where current packet raw counts differ from the committed frame source group.
  - CaptureReplay appsettings loading should cover `Tracker:RuntimeOverrides`.
  - Diagnostics reader/page should cover default `tracker-diagnostics-*.log` and configured `Tracker:Diagnostics:FilePath` outside packet capture directory.

## Outcome

Findings recorded. The PR needs follow-up before merge if the intended normal path is reliable capture replay and diagnostics timeline investigation.
