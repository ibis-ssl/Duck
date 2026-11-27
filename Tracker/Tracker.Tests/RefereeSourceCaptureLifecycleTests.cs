using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;

namespace Tracker.Tests;

public sealed class RefereeSourceCaptureLifecycleTests
{
    [Fact]
    public async Task ExistingReceivePumpCapturesRawSendersBeforeDecodeAndFreezesTheWindow()
    {
        var root = Path.Combine(Path.GetTempPath(), "duck-source-capture-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "referee-source-capture.json");
        var identity = new ResourceDiagnosticIdentity("run", "controller-current", new string('a', 64), 7);
        var diagnostics = new ServiceReceiveDiagnostics(identity);
        var capture = new RefereeSourceCapture(path, "stack", "match-controller",
            new("224.5.23.1", 11003), TimeProvider.System);
        using var listener = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var firstSender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        using var secondSender = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        await using var adapter = new UdpRefereeDatagramReceiver(listener);
        await using var service = new RefereeReceiveService(adapter, diagnostics, sourceCapture: capture);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var run = service.RunAsync(lifetime.Token);
        try
        {
            var destination = Assert.IsType<IPEndPoint>(listener.Client.LocalEndPoint);
            // Malformed protobuf must still reveal a second raw source tuple.
            byte[] invalidPacket = [0xff];
            await firstSender.SendAsync(invalidPacket.AsMemory(), destination, lifetime.Token);
            await firstSender.SendAsync(invalidPacket.AsMemory(), destination, lifetime.Token);
            await secondSender.SendAsync(invalidPacket.AsMemory(), destination, lifetime.Token);
            while (capture.GetSnapshot().TotalDatagramCount < 3)
                await Task.Delay(1, lifetime.Token);
            var frozen = Assert.IsType<RefereeSourceCaptureSnapshot>(service.CompleteSourceCapture());
            Assert.True(frozen.CaptureComplete);
            Assert.Equal("run", frozen.RunId);
            Assert.Equal(identity.ContainerId, frozen.ReceiverContainerId);
            Assert.Equal(7, frozen.ReceiverGeneration);
            Assert.Equal(1, frozen.ReceiverEpoch);
            Assert.Equal(3, frozen.TotalDatagramCount);
            Assert.Equal(3, frozen.Sources.Sum(source => source.DatagramCount));
            Assert.Equal(2, frozen.Sources.Count);
            Assert.Contains(frozen.Sources, source => source.Port ==
                Assert.IsType<IPEndPoint>(firstSender.Client.LocalEndPoint).Port && source.DatagramCount == 2);
            Assert.Contains(frozen.Sources, source => source.Port ==
                Assert.IsType<IPEndPoint>(secondSender.Client.LocalEndPoint).Port && source.DatagramCount == 1);
            Assert.True(frozen.CaptureEndedAtUtc >= frozen.CaptureStartedAtUtc);
            using var persisted = JsonDocument.Parse(File.ReadAllText(path));
            Assert.True(persisted.RootElement.GetProperty("captureComplete").GetBoolean());
            Assert.Equal(3, persisted.RootElement.GetProperty("totalDatagramCount").GetInt64());
            await secondSender.SendAsync(invalidPacket.AsMemory(), destination, lifetime.Token);
            while (Assert.Single(diagnostics.GetSnapshots(RefereeReceiveService.InputId)).DecodeFailureCount < 4)
                await Task.Delay(1, lifetime.Token);
            Assert.Equal(3, capture.GetSnapshot().TotalDatagramCount);
            Assert.Equal(Assert.Single(Directory.GetFiles(root)), path);
        }
        finally
        {
            await lifetime.CancelAsync();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void NewIdentityEpochDiscardsPreviousSourcesAndAbortPreservesAnIncompleteArtifact()
    {
        var root = Path.Combine(Path.GetTempPath(), "duck-source-capture-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "referee-source-capture.json");
        var firstIdentity = new ResourceDiagnosticIdentity("run", "controller-current", new string('a', 64), 1);
        var nextIdentity = new ResourceDiagnosticIdentity("run", "controller-current", new string('b', 64), 2);
        var diagnostics = new ServiceReceiveDiagnostics(firstIdentity);
        var firstEpoch = diagnostics.BeginEpoch(RefereeReceiveService.InputId);
        var nextEpoch = diagnostics.ReplaceIdentityAndBeginEpoch(nextIdentity, RefereeReceiveService.InputId);
        var capture = new RefereeSourceCapture(path, "stack", "match-controller",
            new("224.5.23.1", 11003), TimeProvider.System);
        try
        {
            capture.Begin(firstIdentity, firstEpoch);
            capture.Record(firstEpoch, new(IPAddress.Loopback, 41001));
            capture.Begin(nextIdentity, nextEpoch);
            capture.Record(firstEpoch, new(IPAddress.Loopback, 41001));
            capture.Record(nextEpoch, new(IPAddress.Loopback, 41002));
            capture.Abort(nextEpoch);
            var partial = capture.GetSnapshot();
            Assert.False(partial.CaptureComplete);
            Assert.Equal(nextIdentity.ContainerId, partial.ReceiverContainerId);
            Assert.Equal(nextEpoch.Number, partial.ReceiverEpoch);
            Assert.Equal(1, partial.TotalDatagramCount);
            Assert.Equal(41002, Assert.Single(partial.Sources).Port);
            using var persisted = JsonDocument.Parse(File.ReadAllText(path));
            Assert.False(persisted.RootElement.GetProperty("captureComplete").GetBoolean());
            Assert.Equal(1, persisted.RootElement.GetProperty("totalDatagramCount").GetInt64());
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
    [Fact]
    public async Task IdentityReplacementDuringPendingReceiveCannotRelabelTheOldPacket()
    {
        var root = Path.Combine(Path.GetTempPath(), "duck-source-capture-test-" + Guid.NewGuid().ToString("N"));
        var path = Path.Combine(root, "referee-source-capture.json");
        var firstIdentity = new ResourceDiagnosticIdentity("run", "controller-current", new string('a', 64), 1);
        var nextIdentity = new ResourceDiagnosticIdentity("run", "controller-current", new string('b', 64), 2);
        var diagnostics = new ServiceReceiveDiagnostics(firstIdentity);
        var capture = new RefereeSourceCapture(path, "stack", "match-controller",
            new("224.5.23.1", 11003), TimeProvider.System);
        var receiver = new PausedReceiver();
        await using var service = new RefereeReceiveService(receiver, diagnostics, sourceCapture: capture);
        using var lifetime = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var run = service.RunAsync(lifetime.Token);
        try
        {
            await receiver.Waiting[0].Task.WaitAsync(lifetime.Token);
            service.ReplaceIdentity(nextIdentity);
            receiver.Deliver(new(IPAddress.Loopback, 41001));
            // Starting the next receive proves the stale packet was processed.
            await receiver.Waiting[1].Task.WaitAsync(lifetime.Token);
            Assert.Equal(nextIdentity.ContainerId, capture.GetSnapshot().ReceiverContainerId);
            Assert.Equal(2, capture.GetSnapshot().ReceiverEpoch);
            Assert.Equal(0, capture.GetSnapshot().TotalDatagramCount);
            Assert.Empty(capture.GetSnapshot().Sources);

            receiver.Deliver(new(IPAddress.Loopback, 41002));
            await receiver.Waiting[2].Task.WaitAsync(lifetime.Token);
            var frozen = Assert.IsType<RefereeSourceCaptureSnapshot>(service.CompleteSourceCapture());
            Assert.True(frozen.CaptureComplete);
            Assert.Equal(nextIdentity.ContainerId, frozen.ReceiverContainerId);
            Assert.Equal(2, frozen.ReceiverGeneration);
            Assert.Equal(2, frozen.ReceiverEpoch);
            Assert.Equal(1, frozen.TotalDatagramCount);
            Assert.Equal(41002, Assert.Single(frozen.Sources).Port);
        }
        finally
        {
            await lifetime.CancelAsync();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private sealed class PausedReceiver : IRefereeDatagramReceiver
    {
        private readonly Channel<IPEndPoint> packets = Channel.CreateUnbounded<IPEndPoint>();
        private int calls;
        internal TaskCompletionSource[] Waiting { get; } =
        [
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously),
            new(TaskCreationOptions.RunContinuationsAsynchronously),
        ];

        internal void Deliver(IPEndPoint endpoint) => Assert.True(packets.Writer.TryWrite(endpoint));

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive, CancellationToken cancellationToken)
        {
            var index = Interlocked.Increment(ref calls) - 1;
            if (index < Waiting.Length) Waiting[index].TrySetResult();
            var endpoint = await packets.Reader.ReadAsync(cancellationToken);
            // Like the UDP adapter, capture the receive attempt after delivery,
            // using the service's callback that closed over its original epoch.
            return new(new byte[] { 0xff }, endpoint, captureReceive());
        }
    }
}
