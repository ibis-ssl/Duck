using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Duck.Testing.AppHost;
using Duck.Testing.Diagnostics;

namespace Tracker.Tests;

public sealed class ContainerIdentityControlExecSessionTests
{
    private const string RunId = "run-session-01";
    private const string ResourceId = "opaque-resource-key";
    private const string ContainerId = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string BootId = "boot-20261023-01";
    private const string RoleName = "duck";

    [Fact]
    public async Task SendsPrepareBeforeReadingChallengeAndCommitsTheSameBootAndIdentity()
    {
        var toRemote = Channel.CreateUnbounded<string>();
        var toLocal = Channel.CreateUnbounded<string>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var identity = new ResourceDiagnosticIdentity(RunId, ResourceId, ContainerId, 1);
        var client = ContainerIdentityControlExecSession.RunAsync(
            new ChannelLineReader(toLocal.Reader),
            new ChannelLineWriter(toRemote.Writer),
            identity,
            RoleName,
            null,
            cancellation.Token,
            TimeSpan.FromSeconds(2));

        var prepare = JsonSerializer.Deserialize<ControlMessage>(
            await toRemote.Reader.ReadAsync(cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2)))!;
        await toLocal.Writer.WriteAsync(
            JsonSerializer.Serialize(new ControlMessage { Type = "challenge", BootId = BootId }) + "\n",
            cancellation.Token);
        var commit = JsonSerializer.Deserialize<ControlMessage>(
            await toRemote.Reader.ReadAsync(cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2)))!;
        await toLocal.Writer.WriteAsync(
            JsonSerializer.Serialize(new ControlMessage { Type = "result", Accepted = true }) + "\n",
            cancellation.Token);
        var accepted = await client.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(accepted.Accepted);
        Assert.Equal(BootId, accepted.BootId);
        Assert.False(accepted.RequiresNewGeneration);
        Assert.Equal("prepare", prepare.Type);
        Assert.Equal(RoleName, prepare.RoleName);
        Assert.Equal(RunId, prepare.RunId);
        Assert.Equal(ResourceId, prepare.ResourceId);
        Assert.Equal(ContainerId, prepare.ContainerId);
        Assert.Equal(1, prepare.Generation);
        Assert.Equal("commit", commit.Type);
        Assert.Equal(RoleName, commit.RoleName);
        Assert.Equal(BootId, commit.ExpectedBootId);
        Assert.Equal(prepare.RunId, commit.RunId);
        Assert.Equal(prepare.ResourceId, commit.ResourceId);
        Assert.Equal(prepare.ContainerId, commit.ContainerId);
        Assert.Equal(prepare.Generation, commit.Generation);
    }

    [Fact]
    public async Task BootChangeReturnsBeforeCommitSoCallerCanRestartWithANewGeneration()
    {
        var toRemote = Channel.CreateUnbounded<string>();
        var toLocal = Channel.CreateUnbounded<string>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var identity = new ResourceDiagnosticIdentity(RunId, ResourceId, ContainerId, 4);
        var client = ContainerIdentityControlExecSession.RunAsync(
            new ChannelLineReader(toLocal.Reader),
            new ChannelLineWriter(toRemote.Writer),
            identity,
            RoleName,
            "boot-old",
            cancellation.Token,
            TimeSpan.FromSeconds(2));

        var prepare = JsonSerializer.Deserialize<ControlMessage>(
            await toRemote.Reader.ReadAsync(cancellation.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(2)))!;
        await toLocal.Writer.WriteAsync(
            JsonSerializer.Serialize(new ControlMessage { Type = "challenge", BootId = BootId }) + "\n",
            cancellation.Token);
        var result = await client.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("prepare", prepare.Type);
        Assert.Equal(4, prepare.Generation);
        Assert.False(result.Accepted);
        Assert.Equal(BootId, result.BootId);
        Assert.True(result.RequiresNewGeneration);
        Assert.False(toRemote.Reader.TryRead(out _));
    }

    public sealed class ControlMessage
    {
        public string? Type { get; init; }
        public string? BootId { get; init; }
        public string? ExpectedBootId { get; init; }
        public string? RunId { get; init; }
        public string? ResourceId { get; init; }
        public string? RoleName { get; init; }
        public string? ContainerId { get; init; }
        public long Generation { get; init; }
        public bool Accepted { get; init; }
    }

    private sealed class ChannelLineReader(ChannelReader<string> channel) : TextReader
    {
        private string? current;
        private int offset;

        public override async ValueTask<int> ReadAsync(Memory<char> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty) return 0;
            while (current is null || offset >= current.Length)
            {
                current = await channel.ReadAsync(cancellationToken);
                offset = 0;
            }
            var count = Math.Min(buffer.Length, current.Length - offset);
            current.AsMemory(offset, count).CopyTo(buffer);
            offset += count;
            return count;
        }
    }

    private sealed class ChannelLineWriter(ChannelWriter<string> channel) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public override Task WriteLineAsync(string? value) => channel.WriteAsync((value ?? string.Empty) + "\n").AsTask();
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
