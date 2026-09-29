namespace Duck.Testing.RefereeDriver;

public interface IRefereePacketReceiver
{
    ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken cancellationToken);
}

public sealed class UdpRefereeCommandSource(IRefereePacketReceiver receiver) : IRefereeCommandSource, IDisposable
{
    public const string DefaultMulticastAddress = "224.5.23.1";
    public const int DefaultPort = 11003;

    public ValueTask<RefereeCommandSnapshot> WaitForChangeAsync(
        uint? previousCommandCounter,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
    }
}

public interface IGameControllerWebSocketTransport
{
    Task SendJsonAsync(Uri endpoint, string json, CancellationToken cancellationToken);
}

public sealed class GameControllerWebSocketControlClient(
    IGameControllerWebSocketTransport transport,
    Uri? endpoint = null) : IGameControllerControlClient
{
    public static Uri DefaultEndpoint { get; } = new("ws://127.0.0.1:8082/api/control");

    public Task SendContinueActionAsync(
        GameControllerContinueAction action,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
