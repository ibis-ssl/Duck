using Microsoft.Extensions.Options;
using Tracker.DebugHost.Vision;

namespace Tracker.DebugHost.Tracking;

/// <summary>
/// Diagnostics sample sidecar を UI render tick から独立して定期保存する hosted service。
/// </summary>
public sealed class DiagnosticsSampleHostedService : BackgroundService
{
    private readonly DiagnosticsSampleCaptureLoop captureLoop;
    private readonly TimeSpan sampleInterval;

    /// <summary>
    /// UI 非依存の diagnostics sample capture loop を hosted service に接続する。
    /// </summary>
    public DiagnosticsSampleHostedService(
        DiagnosticsSampleCaptureLoop captureLoop,
        IOptions<VisionReceiverOptions> visionReceiverOptions)
    {
        this.captureLoop = captureLoop;
        sampleInterval = ResolveSampleInterval(visionReceiverOptions.Value.PacketCapture);
    }

    /// <summary>
    /// diagnostics sample loop の実行周期を設定値から解決する。
    /// </summary>
    internal static TimeSpan ResolveSampleInterval(VisionPacketCaptureOptions options)
    {
        var intervalMilliseconds = options.DiagnosticsSampleIntervalMilliseconds;
        if (intervalMilliseconds <= 0)
        {
            intervalMilliseconds = VisionPacketCaptureOptions.DefaultDiagnosticsSampleIntervalMilliseconds;
        }

        return TimeSpan.FromMilliseconds(intervalMilliseconds);
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(sampleInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                captureLoop.CaptureOnce();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
