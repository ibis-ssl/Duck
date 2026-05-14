using Tracker.DebugHost.Vision;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// DebugHost の UI 表示有無に依存せず diagnostics sample sidecar を capture する loop 本体。
/// </summary>
public sealed class DiagnosticsSampleCaptureLoop
{
    private readonly VisionPacketCaptureRuntimeControl captureControl;
    private readonly VisionLiveDisplaySnapshotProvider snapshotProvider;
    private readonly DiagnosticsSampleLogWriter writer;

    /// <summary>
    /// capture toggle、read-side snapshot provider、sample writer を接続する。
    /// </summary>
    public DiagnosticsSampleCaptureLoop(
        VisionPacketCaptureRuntimeControl captureControl,
        VisionLiveDisplaySnapshotProvider snapshotProvider,
        DiagnosticsSampleLogWriter writer)
    {
        this.captureControl = captureControl;
        this.snapshotProvider = snapshotProvider;
        this.writer = writer;
    }

    /// <summary>
    /// CaptureOn の場合だけ 1 sample を sidecar に追記し、CaptureOff では writer handle を解放する。
    /// </summary>
    public void CaptureOnce()
    {
        if (!captureControl.Enabled)
        {
            writer.Stop();
            return;
        }

        writer.CaptureSample(snapshotProvider.CaptureRenderTickSnapshot());
    }
}
