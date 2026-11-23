using System.Diagnostics;
using Tracker.Core;
using Tracker.RuntimeHost;

namespace Tracker.Performance;

/// <summary>
/// 記録受信時刻から仮想周期を進め、製品のバッファと周期処理を呼び出す。
/// </summary>
internal static class ReplayRunner
{
    /// <summary>
    /// 新しい追跡状態で区間を再生し、最後の入力を消費する通常周期で終了する。
    /// </summary>
    internal static ReplayPass Run(VisionWindow window, ReplayConfiguration configuration, bool hashOutput)
    {
        var inputs = window.Inputs;
        var intervalNs = configuration.IntervalMilliseconds * 1_000_000L;
        var firstNs = inputs[0].ReceiverTimestampNs;
        var lastOffsetNs = inputs[^1].ReceiverTimestampNs - firstNs;
        var cycles = checked((int)((lastOffsetNs + intervalNs - 1) / intervalNs) + 1);
        var cycleSamples = new List<double>(cycles);
        var receiveSamples = new List<double>(inputs.Count);

        using var publisher = new ReplayPublisher(hashOutput);
        var engine = new ReplayEngine(window.CameraDetectionCounts.Count + 1);
        var resolved = configuration.Tracker;
        var snapshot = new TrackedSnapshotStore(resolved.EngineSettings.ProfileName);
        var generator = new TrackerPacketGenerator(resolved.PublisherOptions.SourceName, resolved.PublisherOptions.Uuid);
        var coordinator = new TrackerCoordinator(engine, generator, resolved, snapshot, publisher, []);
        var buffer = new RuntimeVisionPacketBuffer();
        var loop = new RuntimeHostOperationLoop(buffer, coordinator, resolved);
        var counters = new ReplayCounters();
        var next = 0;
        long cycleAllocated = 0;
        long receiveAllocated = 0;
        using var process = Process.GetCurrentProcess();
        int[] gcBefore = [GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2)];
        var cpuStart = process.TotalProcessorTime;
        var start = Stopwatch.GetTimestamp();

        for (var cycle = 0; cycle < cycles; cycle++)
        {
            var offsetNs = cycle * intervalNs;
            var bufferOrderTick = -1L;
            while (next < inputs.Count && inputs[next].ReceiverTimestampNs - firstNs <= offsetNs)
            {
                var input = inputs[next++];
                // 100 ns 単位に変換して同じ時刻になる入力も、バッファから記録順で取り出す。
                bufferOrderTick = Math.Max(input.ReceiverTimestampNs / 100, bufferOrderTick + 1);
                var allocationStart = GC.GetAllocatedBytesForCurrentThread();
                var receiveStart = Stopwatch.GetTimestamp();
                var packet = SSL_WrapperPacket.Parser.ParseFrom(input.Payload);
                buffer.StorePacket(packet, DateTimeOffset.UnixEpoch.AddTicks(bufferOrderTick));
                var elapsed = Stopwatch.GetElapsedTime(receiveStart).TotalMicroseconds;
                receiveAllocated += GC.GetAllocatedBytesForCurrentThread() - allocationStart;
                receiveSamples.Add(elapsed);
                counters.RecordInput(packet.Detection, input.ReceiverTimestampNs);
            }

            var cycleAllocationStart = GC.GetAllocatedBytesForCurrentThread();
            var cycleStart = Stopwatch.GetTimestamp();
            loop.ProcessLatestPacket();
            var cycleElapsed = Stopwatch.GetElapsedTime(cycleStart).TotalMicroseconds;
            cycleAllocated += GC.GetAllocatedBytesForCurrentThread() - cycleAllocationStart;
            cycleSamples.Add(cycleElapsed);
            // 最終受信時刻が long の上限近くでも、仮想遅延の差分は計算できる。
            counters.CompleteCycle(engine, offsetNs, firstNs);
        }

        var wallMilliseconds = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var cpuMilliseconds = (process.TotalProcessorTime - cpuStart).TotalMilliseconds;
        int[] gcCounts = [
            GC.CollectionCount(0) - gcBefore[0],
            GC.CollectionCount(1) - gcBefore[1],
            GC.CollectionCount(2) - gcBefore[2],
        ];
        return new ReplayPass(
            wallMilliseconds,
            cpuMilliseconds,
            Quantiles.Calculate(cycleSamples),
            Quantiles.Calculate(receiveSamples),
            Quantiles.Calculate(counters.CommitLagMilliseconds),
            cycleAllocated,
            receiveAllocated,
            gcCounts,
            counters.GetCounts(cycles, publisher, snapshot.GetSnapshot()),
            publisher.FinishHash());
    }
}
