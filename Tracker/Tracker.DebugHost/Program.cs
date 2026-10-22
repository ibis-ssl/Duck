using Tracker.Core;
using Duck.Testing.Diagnostics;
using Tracker.DebugHost.Components;
using Tracker.DebugHost.Tracking;
using Tracker.DebugHost.Vision;
using Microsoft.Extensions.Options;
using TrackerConnectionLib;

var builder = WebApplication.CreateBuilder(args);
DebugHostHealthEndpoints.ConfigureInternalHttp(builder);
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton(serviceProvider => new ServiceReceiveDiagnostics(
    ServiceReceiveDiagnostics.ParseIdentity(
        builder.Configuration["Testing:Diagnostics:RunId"], builder.Configuration["Testing:Diagnostics:ResourceId"],
        builder.Configuration["Testing:Diagnostics:ContainerId"], builder.Configuration["Testing:Diagnostics:Generation"]),
    serviceProvider.GetRequiredService<TimeProvider>()));

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<VisionReceiverOptions>(builder.Configuration.GetSection("VisionReceiver"));
builder.Services.Configure<TrackerOptions>(builder.Configuration.GetSection("Tracker"));
var startupTrackerOptions = builder.Configuration.GetSection("Tracker").Get<TrackerOptions>() ?? new TrackerOptions();
builder.Services.AddSingleton(serviceProvider =>
{
    var trackerOptions = serviceProvider.GetRequiredService<IOptions<TrackerOptions>>().Value;
    var visionReceiverOptions = serviceProvider.GetRequiredService<IOptions<VisionReceiverOptions>>().Value;
    return new VisionReceiverRuntimeOptionsStore(
        VisionReceiverConfigurationResolver.Resolve(
            visionReceiverOptions,
            trackerOptions.ActiveProfileName));
});
builder.Services.AddSingleton(serviceProvider =>
    new VisionPacketCaptureRuntimeControl(
        serviceProvider.GetRequiredService<IOptions<VisionReceiverOptions>>().Value.PacketCapture.Enabled));
builder.Services.AddSingleton(serviceProvider =>
    TrackerConfigurationResolver.Resolve(serviceProvider.GetRequiredService<IOptions<TrackerOptions>>().Value));
builder.Services.AddSingleton<TrackerRuntimeResolvedOptions>(serviceProvider =>
    serviceProvider.GetRequiredService<TrackerResolvedOptions>());
builder.Services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<TrackerResolvedOptions>().EngineSettings);
builder.Services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<TrackerResolvedOptions>().PublisherOptions);
builder.Services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<TrackerResolvedOptions>().Diagnostics);
builder.Services.AddSingleton<ITrackerEngine, TrackerEngine>();
builder.Services.AddSingleton(serviceProvider =>
    new TrackedSnapshotStore(
        serviceProvider.GetRequiredService<IOptions<TrackerOptions>>().Value.ActiveProfileName));
builder.Services.AddSingleton<ITrackerPacketPublisher, UdpTrackerPacketPublisher>();
builder.Services.AddSingleton<ITrackerObserver, VisionReceiverProfileSwitchObserver>();
builder.Services.AddSingleton<TrackerPacketGenerator>(serviceProvider =>
{
    var resolved = serviceProvider.GetRequiredService<TrackerResolvedOptions>();
    return new TrackerPacketGenerator(resolved.PublisherOptions.SourceName, resolved.PublisherOptions.Uuid);
});
builder.Services.AddSingleton<TrackerCoordinator>();
builder.Services.AddSingleton<TrackerDiagnosticsLogReader>();
builder.Services.AddSingleton<TrackerRenderSnapshotLogReader>();
builder.Services.AddSingleton<TrackerDiagnosticsComparisonViewStateReader>();
builder.Services.AddSingleton<TrackerProfileRequestService>();
builder.Services.AddSingleton<VisionPacketStore>();
builder.Services.AddSingleton<VisionLiveComparisonSnapshotComposer>();
builder.Services.AddSingleton<ExternalTrackerSnapshotStore>();
builder.Services.AddSingleton<VisionLiveDisplaySnapshotProvider>();
builder.Services.AddSingleton<VisionPacketCaptureSession>();
builder.Services.AddSingleton<VisionPacketCaptureWriter>();
builder.Services.AddSingleton<TrackerRenderSnapshotCaptureWriter>();
builder.Services.AddSingleton<TrackerPacketSnapshotLogWriter>();
builder.Services.AddSingleton<TrackerSnapshotAlignmentLogWriter>();
builder.Services.AddSingleton<DiagnosticsSampleLogWriter>();
builder.Services.AddSingleton<DiagnosticsSampleCaptureLoop>();
builder.Services.AddHostedService<DiagnosticsSampleHostedService>();
builder.Services.AddSingleton(serviceProvider =>
{
    var publisherOptions = serviceProvider.GetRequiredService<TrackerPublisherOptions>();
    return new MultiTrackerManager<TrackerPacketAdapter>(
        publisherOptions.Uuid,
        publisherOptions.SourceName);
});
if (startupTrackerOptions.Receive.Enabled)
{
    builder.Services.AddSingleton<DiagnosticTrackerDeserializer>();
    builder.Services.AddSingleton(serviceProvider =>
    {
        var publisherOptions = serviceProvider.GetRequiredService<TrackerPublisherOptions>();
        var trackerOptions = serviceProvider.GetRequiredService<IOptions<TrackerOptions>>().Value;
        var receiveEndpoint = TrackerReceiveEndpointResolver.Resolve(
            trackerOptions.Receive,
            publisherOptions);
        return new UdpTrackerReceiver<TrackerPacketAdapter>(
            receiveEndpoint.Port,
            receiveEndpoint.MulticastAddress,
            serviceProvider.GetRequiredService<DiagnosticTrackerDeserializer>(),
            receiveEndpoint.InterfaceAddress);
    });
    builder.Services.AddSingleton<TrackerConnectionLibSnapshotRecorder>();
    builder.Services.AddHostedService<TrackerConnectionLibReceiverHostedService>();
}

builder.Services.AddHostedService<VisionReceiverService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // コンテナ用の HTTP 待受は、以下で明示的な構成に応じて選択する。
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
DebugHostHealthEndpoints.UseTransportMiddleware(app);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapPost(
    "/api/tracker/profile-switch/{profileName}",
    (string profileName, TrackerProfileRequestService profileRequestService) =>
    {
        profileRequestService.RequestProfileSwitch(profileName);
        return Results.Accepted($"/api/tracker/profile-switch/{profileName}");
    });
app.MapGet(
    "/health/comparison",
    (ExternalTrackerSnapshotStore trackerSnapshots,
        VisionPacketStore visionPackets,
        IConfiguration configuration,
        ServiceReceiveDiagnostics diagnostics) =>
        DebugHostHealthEndpoints.GetComparisonHealthResult(trackerSnapshots, visionPackets, configuration, diagnostics));
app.MapGet("/health/vision", (ServiceReceiveDiagnostics diagnostics, IConfiguration configuration, VisionPacketStore packets) =>
    DebugHostHealthEndpoints.GetVisionHealthResult(diagnostics, configuration, packets));

app.Run();
