using Tracker.Core;
using Tracker.Server.Components;
using Tracker.Server.Tracking;
using Tracker.Server.Vision;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<VisionReceiverOptions>(builder.Configuration.GetSection("VisionReceiver"));
builder.Services.Configure<TrackerOptions>(builder.Configuration.GetSection("Tracker"));
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
builder.Services.AddSingleton<TrackerProfileRequestService>();
builder.Services.AddSingleton<VisionPacketStore>();
builder.Services.AddSingleton<VisionPacketCaptureSession>();
builder.Services.AddSingleton<VisionPacketCaptureWriter>();
builder.Services.AddSingleton<TrackerRenderSnapshotCaptureWriter>();
builder.Services.AddHostedService<VisionReceiverService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

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

app.Run();
