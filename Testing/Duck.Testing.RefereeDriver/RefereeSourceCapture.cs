using System.Net;
using System.Text.Json;
using Duck.Testing.Diagnostics;

namespace Duck.Testing.RefereeDriver;

internal sealed record RefereeSourceTuple(string Address, int Port, long DatagramCount);
internal sealed record RefereeCaptureDestination(string Address, int Port);
internal sealed record RefereeSourceCaptureSnapshot(
    int SchemaVersion, string StackId, string? RunId, string ReceiverResourceId,
    string? ReceiverContainerId, long? ReceiverGeneration, long ReceiverEpoch,
    bool CaptureComplete, DateTimeOffset CaptureStartedAtUtc, DateTimeOffset? CaptureEndedAtUtc,
    RefereeCaptureDestination Destination, long TotalDatagramCount,
    IReadOnlyList<RefereeSourceTuple> Sources, bool CaptureTruncated);

/// <summary>Count raw received source tuples for one identity/epoch and a closed observation window.</summary>
internal sealed class RefereeSourceCapture(
    string path, string stackId, string receiverResourceId,
    RefereeCaptureDestination destination, TimeProvider clock)
{
    private const int MaximumSources = 1024;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
    };
    private readonly object gate = new();
    private readonly Dictionary<(string Address, int Port), long> sources = new();
    private ResourceDiagnosticIdentity? identity;
    private ServiceReceiveDiagnostics.ReceiveEpoch? epoch;
    private DateTimeOffset started;
    private DateTimeOffset? ended;
    private long total;
    private bool complete;
    private bool truncated;
    private bool active;

    internal void Begin(ResourceDiagnosticIdentity? nextIdentity, ServiceReceiveDiagnostics.ReceiveEpoch nextEpoch)
    {
        lock (gate)
        {
            identity = nextIdentity;
            epoch = nextEpoch;
            started = clock.GetUtcNow();
            ended = null;
            total = 0;
            complete = false;
            truncated = false;
            active = true;
            sources.Clear();
            WriteSnapshot(); // Once per epoch; never once per datagram.
        }
    }

    internal void Record(ServiceReceiveDiagnostics.ReceiveEpoch receivedEpoch, IPEndPoint endpoint)
    {
        lock (gate)
        {
            if (!active || ended is not null || !ReferenceEquals(epoch, receivedEpoch)) return;
            total = checked(total + 1);
            var key = (endpoint.Address.ToString(), endpoint.Port);
            if (sources.TryGetValue(key, out var count))
                sources[key] = checked(count + 1);
            else if (sources.Count < MaximumSources)
                sources.Add(key, 1);
            else
                truncated = true;
        }
    }

    internal RefereeSourceCaptureSnapshot Complete(
        ResourceDiagnosticIdentity? currentIdentity, ServiceReceiveDiagnostics.ReceiveEpoch? currentEpoch)
    {
        lock (gate)
        {
            if (ended is null)
            {
                ended = clock.GetUtcNow();
                complete = active && identity is not null && identity == currentIdentity &&
                    ReferenceEquals(epoch, currentEpoch) && total > 0 && !truncated && ended >= started;
            }
            WriteSnapshot();
            return SnapshotCore();
        }
    }

    internal void Abort(ServiceReceiveDiagnostics.ReceiveEpoch endedEpoch)
    {
        lock (gate)
        {
            if (!ReferenceEquals(epoch, endedEpoch)) return;
            active = false;
            if (ended is not null) return; // Retain an already closed capture window.
            ended = clock.GetUtcNow();
            complete = false;
            WriteSnapshot();
        }
    }

    internal RefereeSourceCaptureSnapshot GetSnapshot()
    {
        lock (gate) return SnapshotCore();
    }

    private RefereeSourceCaptureSnapshot SnapshotCore() => new(
        1, stackId, identity?.RunId, receiverResourceId, identity?.ContainerId,
        identity?.Generation, epoch?.Number ?? 0, complete, started, ended,
        destination, total, sources.OrderBy(pair => pair.Key.Address, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Port)
            .Select(pair => new RefereeSourceTuple(pair.Key.Address, pair.Key.Port, pair.Value)).ToArray(), truncated);

    private void WriteSnapshot()
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(SnapshotCore(), JsonOptions));
            if (OperatingSystem.IsLinux())
                File.SetUnixFileMode(temporary, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
