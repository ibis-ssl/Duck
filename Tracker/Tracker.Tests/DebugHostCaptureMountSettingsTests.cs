using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class DebugHostCaptureMountSettingsTests
{
    [Fact]
    public void CreateCapture_UsesRunScopedLocalAppDataAndCreatesOnlyNewDirectory()
    {
        var testRoot = CreateTestRoot();
        var localAppData = Path.Combine(testRoot, "LocalAppData");
        Directory.CreateDirectory(localAppData);
        var runId = Guid.NewGuid().ToString("N");

        try
        {
            var settings = DebugHostCaptureMountSettings.CreateCapture(localAppData, runId);

            Assert.Equal(
                Path.GetFullPath(Path.Combine(localAppData, "Duck", "Aspire", runId, "captures")),
                settings.HostDirectory);
            Assert.Equal("/data/captures", settings.ContainerDirectory);
            Assert.False(settings.IsReadOnly);
            Assert.True(Directory.Exists(settings.HostDirectory));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("..")]
    [InlineData("../outside")]
    [InlineData("..\\outside")]
    [InlineData("run/id")]
    [InlineData("run\\id")]
    [InlineData("run:id")]
    public void CreateCapture_RejectsUnsafeRunIdBeforeWritingOutsideLocalAppData(string runId)
    {
        var testRoot = CreateTestRoot();
        var localAppData = Path.Combine(testRoot, "LocalAppData");
        Directory.CreateDirectory(localAppData);

        try
        {
            Assert.Throws<ArgumentException>(() => DebugHostCaptureMountSettings.CreateCapture(localAppData, runId));
            Assert.Empty(Directory.EnumerateFileSystemEntries(localAppData));
            Assert.Equal(localAppData, Assert.Single(Directory.EnumerateFileSystemEntries(testRoot)));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateCapture_RejectsPreexistingRunDirectoryWithoutChangingItsContents()
    {
        var testRoot = CreateTestRoot();
        var localAppData = Path.Combine(testRoot, "LocalAppData");
        var runId = Guid.NewGuid().ToString("N");
        var runDirectory = Path.Combine(localAppData, "Duck", "Aspire", runId);
        Directory.CreateDirectory(runDirectory);
        var sentinelPath = Path.Combine(runDirectory, "owner-data.bin");
        var sentinel = new byte[] { 1, 3, 5, 7, 11 };
        File.WriteAllBytes(sentinelPath, sentinel);

        try
        {
            Assert.Throws<InvalidOperationException>(() => DebugHostCaptureMountSettings.CreateCapture(localAppData, runId));
            Assert.Equal(sentinel, File.ReadAllBytes(sentinelPath));
            Assert.False(Directory.Exists(Path.Combine(runDirectory, "captures")));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [Fact]
    public void CreateReplay_UsesExistingAbsoluteDirectoryReadOnlyAndNeverCreatesOrChangesSource()
    {
        var testRoot = CreateTestRoot();
        var replayDirectory = Path.Combine(testRoot, "saved-capture");
        Directory.CreateDirectory(replayDirectory);
        var sentinelPath = Path.Combine(replayDirectory, "record.bin");
        var sentinel = new byte[] { 2, 4, 6, 8 };
        File.WriteAllBytes(sentinelPath, sentinel);

        try
        {
            var settings = DebugHostCaptureMountSettings.CreateReplay(replayDirectory);

            Assert.Equal(Path.GetFullPath(replayDirectory), settings.HostDirectory);
            Assert.Equal("/data/replay", settings.ContainerDirectory);
            Assert.True(settings.IsReadOnly);
            Assert.Equal(sentinel, File.ReadAllBytes(sentinelPath));
            Assert.Throws<ArgumentException>(() => DebugHostCaptureMountSettings.CreateReplay("relative-capture-path"));
            Assert.Throws<DirectoryNotFoundException>(() =>
                DebugHostCaptureMountSettings.CreateReplay(Path.Combine(testRoot, "missing")));
            Assert.False(Directory.Exists(Path.Combine(testRoot, "missing")));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static string CreateTestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "duck-debug-host-capture-mount-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
