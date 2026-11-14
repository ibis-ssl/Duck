using System.Net;
using System.Text.Json;
using Duck.Testing.Diagnostics;
using Google.Protobuf;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Tracker.Core;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using TrackerConnectionLib;

namespace Tracker.Tests;

public sealed class DebugHostIdentityControlTests
{
    [Fact]
    public async Task ExistingPrepareChallengeCommitRelayAttributesNewTrackerReceives()
    {
        const string runId = "debug-host-identity-run";
        const string role = "debug-host";
        const string containerId = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        var diagnostics = new ServiceReceiveDiagnostics();
        var deserializer = new DiagnosticTrackerDeserializer(diagnostics);
        var visionReceiver = new VisionReceiverService(null!, Options.Create(new TrackerOptions()), null!, null!, null!,
            diagnostics, NullLogger<VisionReceiverService>.Instance);
        visionReceiver.BeginDiagnosticEpoch();
        var staleVisionAttempt = visionReceiver.BeginDiagnosticReceive();
        var socketPath = Path.Combine(Path.GetTempPath(), $"duck-debughost-identity-{Guid.NewGuid():N}.sock");
        await using var server = new DebugHostIdentityControlServer(
            diagnostics, deserializer, visionReceiver, runId, expectedResourceId: null, role, socketPath);
        await server.StartAsync(CancellationToken.None);

        var identity = new ResourceDiagnosticIdentity(runId, "debug-host-resource-key", containerId, 1);
        var wrongRole = await ApplyAsync(server, socketPath, identity, "duck");
        Assert.NotEqual(0, wrongRole.ExitCode);
        Assert.False(wrongRole.Accepted);
        Assert.Null(diagnostics.Identity);

        var accepted = await ApplyAsync(server, socketPath, identity, role);
        Assert.Equal(0, accepted.ExitCode);
        Assert.True(accepted.Accepted);
        Assert.Equal(identity, diagnostics.Identity);
        Assert.False(diagnostics.RecordSuccess(staleVisionAttempt, remoteEndpoint: "127.0.0.1:10001"));

        for (var packetNumber = 1; packetNumber <= 2; packetNumber++)
        {
            var visionAttempt = visionReceiver.BeginDiagnosticReceive();
            Assert.True(diagnostics.RecordSuccess(visionAttempt, remoteEndpoint: $"127.0.0.1:{10001 + packetNumber}"));
        }
        var visionSnapshot = Assert.Single(diagnostics.GetSnapshots("vision"));
        Assert.Equal(identity, visionSnapshot.Identity);
        Assert.Equal(2, visionSnapshot.SuccessfullyDecodedCount);
        Assert.True(visionSnapshot.IsReady);

        for (var frameNumber = 1u; frameNumber <= 2; frameNumber++)
        {
            var packet = new TrackerWrapperPacket
            {
                SourceName = "ibis",
                Uuid = "ibis",
                TrackedFrame = new TrackedFrame { FrameNumber = frameNumber, Timestamp = frameNumber },
            };
            Assert.True(deserializer.TryDeserialize(packet.ToByteArray(), out var decoded));
            deserializer.RecordPacket(decoded!, new IPEndPoint(IPAddress.Loopback, 11010));
        }

        var source = Assert.Single(diagnostics.GetSnapshots("tracker"));
        Assert.Equal(identity, source.Identity);
        Assert.Equal(2, source.SuccessfullyDecodedCount);
        Assert.True(source.IsReady);
    }

    [Fact]
    public async Task VisionOnlyIdentityControlDoesNotRequireTrackerReceive()
    {
        const string runId = "debug-host-vision-only-identity-run";
        const string role = "debug-host";
        const string containerId = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd";
        var diagnostics = new ServiceReceiveDiagnostics();
        var deserializer = new DiagnosticTrackerDeserializer(diagnostics);
        var visionReceiver = new VisionReceiverService(null!, Options.Create(new TrackerOptions()), null!, null!, null!,
            diagnostics, NullLogger<VisionReceiverService>.Instance);
        visionReceiver.BeginDiagnosticEpoch();
        var socketPath = Path.Combine(Path.GetTempPath(), $"dh-vision-id-{Guid.NewGuid():N}.sock");
        await using var server = new DebugHostIdentityControlServer(
            diagnostics, deserializer, visionReceiver, runId, expectedResourceId: null, role, socketPath,
            applyTrackerIdentity: false);
        await server.StartAsync(CancellationToken.None);

        var identity = new ResourceDiagnosticIdentity(runId, "debug-host-vision-only-resource", containerId, 1);
        var accepted = await ApplyAsync(server, socketPath, identity, role);

        Assert.Equal(0, accepted.ExitCode);
        Assert.True(accepted.Accepted);
        Assert.Equal(identity, diagnostics.Identity);
        Assert.DoesNotContain(diagnostics.GetSnapshots("tracker"), snapshot => snapshot.SourceKey.Length > 0);

        for (var packetNumber = 1; packetNumber <= 2; packetNumber++)
        {
            var visionAttempt = visionReceiver.BeginDiagnosticReceive();
            Assert.True(diagnostics.RecordSuccess(visionAttempt, remoteEndpoint: $"127.0.0.1:{10100 + packetNumber}"));
        }
        var visionSnapshot = Assert.Single(diagnostics.GetSnapshots("vision"));
        Assert.Equal(identity, visionSnapshot.Identity);
        Assert.Equal(2, visionSnapshot.SuccessfullyDecodedCount);
        Assert.True(visionSnapshot.IsReady);
    }

    private static async Task<(int ExitCode, bool Accepted)> ApplyAsync(
        DebugHostIdentityControlServer server,
        string socketPath,
        ResourceDiagnosticIdentity identity,
        string role)
    {
        var prepare = JsonSerializer.Serialize(new
        {
            type = "prepare",
            roleName = role,
            runId = identity.RunId,
            resourceId = identity.ResourceId,
            containerId = identity.ContainerId,
            generation = identity.Generation,
        });
        var commit = JsonSerializer.Serialize(new
        {
            type = "commit",
            roleName = role,
            expectedBootId = server.BootId,
            runId = identity.RunId,
            resourceId = identity.ResourceId,
            containerId = identity.ContainerId,
            generation = identity.Generation,
        });
        using var input = new StringReader(prepare + "\n" + commit + "\n");
        using var output = new StringWriter();
        var exitCode = await DebugHostIdentityControlProtocol.RelayAsync(
            input, output, socketPath, CancellationToken.None);
        var resultLine = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries)[^1];
        using var result = JsonDocument.Parse(resultLine);
        return (exitCode, result.RootElement.GetProperty("accepted").GetBoolean());
    }
}
