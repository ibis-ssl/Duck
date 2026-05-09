using Tracker.Core;
using Tracker.Server.Components;
using Tracker.Server.Tracking;
using Tracker.Server.Vision;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<VisionReceiverOptions>(builder.Configuration.GetSection("VisionReceiver"));
builder.Services.AddSingleton(
    new TrackerEngineSettings
    {
        ProfileName = "default",
        ReorderWindowNs = 100_000_000,
        MergeWindowNs = 20_000_000,
        GeometryResetFieldLengthThresholdMm = 500,
        GeometryResetFieldWidthThresholdMm = 500,
    });
builder.Services.AddSingleton(new TrackerPublisherOptions());
builder.Services.AddSingleton<ITrackerEngine, TrackerEngine>();
builder.Services.AddSingleton<TrackedSnapshotStore>();
builder.Services.AddSingleton<ITrackerPacketPublisher, UdpTrackerPacketPublisher>();
builder.Services.AddSingleton<TrackerPacketGenerator>(serviceProvider =>
{
    var options = serviceProvider.GetRequiredService<TrackerPublisherOptions>();
    return new TrackerPacketGenerator(options.SourceName, options.Uuid);
});
builder.Services.AddSingleton<TrackerCoordinator>();
builder.Services.AddSingleton<VisionPacketStore>();
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

app.Run();
