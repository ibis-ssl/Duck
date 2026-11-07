using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using System.Threading.Channels;
using Duck.Testing.Diagnostics;
using Duck.Testing.RefereeDriver;
using Duck.Testing.RefereeDriver.Hosting;
using Google.Protobuf;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tracker.Tests;

/// <summary>実際の受信・API照合とHTTP経路を組み、標準CIの隔離した動的ポートで検証する。</summary>
public sealed class RefereeDriverHostHealthTests
{
    [Theory]
    [InlineData(true, "http://0.0.0.0:39110")]
    [InlineData(false, "http://127.0.0.1:39110")]
    public void EnabledBindingFollowsExplicitContainerMode(bool container, string expected)
    {
        var builder = CreateBuilder("true", container);
        RefereeDriverHost.ConfigureHttp(builder);

        Assert.Equal(expected, builder.Configuration["urls"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public void DisabledBindingPreservesExistingSettings(string? enabled)
    {
        var builder = CreateBuilder(enabled, true);
        builder.Configuration["urls"] = "http://127.0.0.1:42345";
        RefereeDriverHost.ConfigureHttp(builder);

        Assert.Equal("http://127.0.0.1:42345", builder.Configuration["urls"]);
    }

    [Fact]
    public async Task HealthReportsActualEvidenceAndExpiresWithoutRefreshingEitherInput()
    {
        await using var fixture = await HttpFixture.CreateAsync();
        Assert.True((await fixture.Core.Matcher.ReadAsync(fixture.Core.Token)).IsReady);
        using var response = await fixture.Client.GetAsync("/health/referee");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.True(root.GetProperty("isReady").GetBoolean());
        Assert.Equal("Matched", root.GetProperty("reason").GetString());
        var diagnostic = root.GetProperty("diagnostic");
        Assert.Equal("referee", diagnostic.GetProperty("inputId").GetString());
        Assert.Equal("host-run", diagnostic.GetProperty("identity").GetProperty("runId").GetString());
        Assert.Equal("referee-driver", diagnostic.GetProperty("identity").GetProperty("resourceId").GetString());
        Assert.Equal(new string('d', 64), diagnostic.GetProperty("identity").GetProperty("containerId").GetString());
        Assert.Equal(1, diagnostic.GetProperty("identity").GetProperty("generation").GetInt64());
        Assert.Equal(1, diagnostic.GetProperty("receiverEpoch").GetInt64());
        Assert.Equal("192.0.2.70:11003", diagnostic.GetProperty("sourceKey").GetString());
        Assert.Equal(2, diagnostic.GetProperty("successfullyDecodedCount").GetInt64());
        Assert.Equal(0, diagnostic.GetProperty("decodeFailureCount").GetInt64());
        Assert.Equal(fixture.Core.Clock.GetUtcNow(), diagnostic.GetProperty("lastReceivedAt").GetDateTimeOffset());
        Assert.Equal(TimeSpan.Zero, TimeSpan.Parse(diagnostic.GetProperty("decodeAgeUpperBound").GetString()!));
        Assert.Equal(1, root.GetProperty("api").GetProperty("requestSequence").GetInt64());

        fixture.Core.Clock.Advance(TimeSpan.FromSeconds(3));
        using var boundary = await fixture.Client.GetAsync("/health/referee");
        Assert.Equal(HttpStatusCode.OK, boundary.StatusCode);
        fixture.Core.Clock.Advance(TimeSpan.FromTicks(1));
        using var expired = await fixture.Client.GetAsync("/health/referee");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, expired.StatusCode);
        using var expiredJson = JsonDocument.Parse(await expired.Content.ReadAsStringAsync());
        Assert.False(expiredJson.RootElement.GetProperty("isReady").GetBoolean());
        Assert.Equal(2, expiredJson.RootElement.GetProperty("diagnostic")
            .GetProperty("successfullyDecodedCount").GetInt64());
        Assert.Equal(3, fixture.Core.Receiver.ReceiveCalls);
        Assert.Equal(1, fixture.Core.Api.ConnectCalls);
        Assert.Equal(0, fixture.Core.Api.SendCalls);
        Assert.Equal(1, fixture.Core.Api.DisposeCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("short-id")]
    public async Task MissingOrShortOwnerCannotBecomeReady(string? containerId)
    {
        await using var fixture = await HttpFixture.CreateAsync(containerId: containerId);
        Assert.False((await fixture.Core.Matcher.ReadAsync(fixture.Core.Token)).IsReady);
        using var response = await fixture.Client.GetAsync("/health/referee");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("isReady").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("diagnostic").GetProperty("identity").ValueKind);
        Assert.Equal(0, fixture.Core.Api.ConnectCalls);
        Assert.Equal(0, fixture.Core.Api.SendCalls);
    }

    [Fact]
    public async Task DifferentApiCommandCannotPassHttpHealth()
    {
        await using var fixture = await HttpFixture.CreateAsync(apiCommand: "STOP", apiPhase: "STOP");
        var result = await fixture.Core.Matcher.ReadAsync(fixture.Core.Token);
        Assert.False(result.IsReady);
        Assert.Equal("CommandMismatch", result.Reason);
        using var response = await fixture.Client.GetAsync("/health/referee");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("isReady").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("reason").GetString()));
        Assert.Equal(0, fixture.Core.Api.SendCalls);
        Assert.Equal(1, fixture.Core.Api.DisposeCalls);
    }

    [Fact]
    public async Task NoReceivedPacketCannotPassHttpHealth()
    {
        await using var fixture = await HttpFixture.CreateAsync(initialPackets: 0);
        using var response = await fixture.Client.GetAsync("/health/referee");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(json.RootElement.GetProperty("isReady").GetBoolean());
        Assert.Equal(0, json.RootElement.GetProperty("diagnostic").GetProperty("successfullyDecodedCount").GetInt64());
        Assert.Equal(0, fixture.Core.Api.ConnectCalls);
        Assert.Equal(1, fixture.Core.Receiver.ReceiveCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public async Task DisabledHealthDoesNotExposeAReadyEndpoint(string? enabled)
    {
        await using var fixture = await HttpFixture.CreateAsync(enabled: enabled);
        using var response = await fixture.Client.GetAsync("/health/referee");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, fixture.Core.Api.ConnectCalls);
        Assert.Equal(0, fixture.Core.Api.SendCalls);
    }

    private static WebApplicationBuilder CreateBuilder(string? enabled, bool container)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Production,
            Args = [],
        });
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["RefereeDriver:Enabled"] = enabled,
            ["Testing:ContainerMode"] = container.ToString(),
            ["RefereeDriver:GameControllerUri"] = CoreFixture.ApiEndpoint.ToString(),
            ["Testing:Diagnostics:RunId"] = "host-run",
            ["Testing:Diagnostics:ResourceId"] = "referee-driver",
            ["Testing:Diagnostics:ContainerId"] = new string('d', 64),
            ["Testing:Diagnostics:Generation"] = "1",
        });
        return builder;
    }

    /// <summary>HTTPだけを動的loopbackへ置き、実UDPやGame Controllerは起動せず終了時にすべて回収する。</summary>
    private sealed class HttpFixture(WebApplication app, HttpClient client, CoreFixture core) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public CoreFixture Core { get; } = core;

        public static async Task<HttpFixture> CreateAsync(
            string? enabled = "true", string? containerId = "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd",
            string apiCommand = "HALT", string apiPhase = "HALT", int initialPackets = 2)
        {
            var builder = CreateBuilder(enabled, true);
            builder.Configuration["Testing:Diagnostics:ContainerId"] = containerId;
            var core = await CoreFixture.CreateAsync(builder.Configuration, apiCommand, apiPhase, initialPackets);
            WebApplication? app = null;
            try
            {
                builder.Services.AddSingleton<TimeProvider>(core.Clock);
                builder.Services.AddSingleton(core.Diagnostics);
                builder.Services.AddSingleton(core.Service);
                builder.Services.AddSingleton(core.Observer);
                builder.Services.AddSingleton(core.Matcher);
                RefereeDriverHost.ConfigureHttp(builder);
                builder.WebHost.UseKestrel().UseUrls("http://127.0.0.1:0");
                app = builder.Build();
                RefereeDriverHost.MapHealth(app);
                await app.StartAsync();
                var address = app.Services.GetRequiredService<IServer>().Features
                    .Get<IServerAddressesFeature>()!.Addresses.Single();
                var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                {
                    BaseAddress = new Uri(address),
                    Timeout = TimeSpan.FromSeconds(5),
                };
                return new(app, client, core);
            }
            catch
            {
                if (app is not null) await app.DisposeAsync();
                await core.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            try { await app.StopAsync(); }
            finally
            {
                try { await app.DisposeAsync(); }
                finally { await Core.DisposeAsync(); }
            }
        }
    }

    /// <summary>既存の受信機・parser・診断・API client・照合器を同じ時計で構成する。</summary>
    private sealed class CoreFixture : IAsyncDisposable
    {
        internal static readonly Uri ApiEndpoint = new("ws://game-controller:8082/api/control");
        internal FixtureClock Clock { get; } = new();
        internal FixtureReceiver Receiver { get; } = new();
        internal ServiceReceiveDiagnostics Diagnostics { get; }
        internal RefereeReceiveService Service { get; }
        internal GameControllerStateObserver Observer { get; }
        internal RefereeApiMatcher Matcher { get; }
        internal ApiSocket Api { get; }
        internal CancellationToken Token => cancellation.Token;
        private readonly CancellationTokenSource cancellation = new();
        private readonly Task run;

        private CoreFixture(IConfiguration config, string apiCommand, string apiPhase)
        {
            Diagnostics = new(ServiceReceiveDiagnostics.ParseIdentity(
                config["Testing:Diagnostics:RunId"], config["Testing:Diagnostics:ResourceId"],
                config["Testing:Diagnostics:ContainerId"], config["Testing:Diagnostics:Generation"]), Clock);
            Service = new(Receiver, Diagnostics);
            Api = new(apiCommand, apiPhase);
            Observer = new(ApiEndpoint, (uri, token) =>
                new GameControllerStateClient(uri, Api.ConnectAsync, timeProvider: Clock).ReadMatchStateAsync(token),
                TimeSpan.FromSeconds(3), Clock);
            Matcher = new(Service, Observer);
            run = Service.RunAsync(cancellation.Token);
        }

        internal static async Task<CoreFixture> CreateAsync(
            IConfiguration config, string apiCommand, string apiPhase, int initialPackets)
        {
            var fixture = new CoreFixture(config, apiCommand, apiPhase);
            try
            {
                for (var i = 0; i < initialPackets; i++) fixture.Receiver.Supply(Packet().ToByteArray());
                await fixture.Receiver.AtReceiveCall(initialPackets + 1).WaitAsync(TimeSpan.FromSeconds(5));
                Assert.False(fixture.run.IsCompleted);
                return fixture;
            }
            catch
            {
                await fixture.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await cancellation.CancelAsync();
            _ = await Record.ExceptionAsync(async () => await run.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(run.IsCompleted, "受信fixtureのTaskを回収する。");
            await Service.DisposeAsync();
            Api.Dispose();
            cancellation.Dispose();
        }
    }

    private sealed class FixtureReceiver : IRefereeDatagramReceiver
    {
        private readonly Channel<ReadOnlyMemory<byte>> packets = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        private readonly ConcurrentDictionary<int, TaskCompletionSource> calls = new();
        private int receiveCalls;
        internal int ReceiveCalls => Volatile.Read(ref receiveCalls);
        internal Task AtReceiveCall(int number) => calls.GetOrAdd(number,
            _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        internal void Supply(ReadOnlyMemory<byte> packet) => Assert.True(packets.Writer.TryWrite(packet));

        public async ValueTask<RefereeReceivedDatagram> ReceiveAsync(
            Func<ServiceReceiveDiagnostics.ReceiveAttempt> captureReceive, CancellationToken token)
        {
            var number = Interlocked.Increment(ref receiveCalls);
            calls.GetOrAdd(number, _ => new(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
            var packet = await packets.Reader.ReadAsync(token);
            return new(packet, new IPEndPoint(IPAddress.Parse("192.0.2.70"), 11003), captureReceive());
        }
    }

    private sealed class ApiSocket(string command, string phase) : WebSocket
    {
        private readonly byte[] payload = JsonSerializer.SerializeToUtf8Bytes(new
        {
            matchState = new { command = new { type = command, forTeam = "UNKNOWN" }, gameState = new { type = phase } },
        });
        private bool disposed;
        internal int ConnectCalls { get; private set; }
        internal int SendCalls { get; private set; }
        internal int DisposeCalls { get; private set; }
        internal Task<WebSocket> ConnectAsync(Uri endpoint, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            Assert.Equal(CoreFixture.ApiEndpoint, endpoint);
            ConnectCalls++;
            return Task.FromResult<WebSocket>(this);
        }
        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => disposed ? WebSocketState.Closed : WebSocketState.Open;
        public override string? SubProtocol => null;
        public override Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            payload.AsSpan().CopyTo(buffer.AsSpan());
            return Task.FromResult(new WebSocketReceiveResult(payload.Length, WebSocketMessageType.Text, true));
        }
        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type,
            bool endOfMessage, CancellationToken token)
        {
            SendCalls++;
            throw new InvalidOperationException("観測専用で送信しない。");
        }
        public override void Abort() { }
        public override Task CloseAsync(WebSocketCloseStatus status, string? description, CancellationToken token) =>
            Task.CompletedTask;
        public override Task CloseOutputAsync(WebSocketCloseStatus status, string? description, CancellationToken token) =>
            Task.CompletedTask;
        public override void Dispose()
        {
            if (disposed) return;
            disposed = true;
            DisposeCalls++;
        }
    }

    private static global::Referee Packet() => new()
    {
        PacketTimestamp = 1,
        Stage = global::Referee.Types.Stage.NormalFirstHalf,
        Command = global::Referee.Types.Command.Halt,
        CommandCounter = 7,
        CommandTimestamp = 1,
        Yellow = Team("yellow"),
        Blue = Team("blue"),
    };

    private static global::Referee.Types.TeamInfo Team(string name) => new()
    {
        Name = name, Score = 0, RedCards = 0, YellowCards = 0,
        Timeouts = 0, TimeoutTime = 0, Goalkeeper = 0,
    };

    private sealed class FixtureClock : TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Interlocked.Read(ref timestamp);
        public override DateTimeOffset GetUtcNow() =>
            new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero) + TimeSpan.FromTicks(GetTimestamp());
        internal void Advance(TimeSpan elapsed) => Interlocked.Add(ref timestamp, elapsed.Ticks);
    }
}
