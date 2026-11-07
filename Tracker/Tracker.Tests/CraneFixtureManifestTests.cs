using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class CraneFixtureManifestTests
{
    [Fact]
    public void ReadsOnlyTheExpectedStackAndNeverReplacesAnExistingManifest()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"duck-crane-manifest-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "run.json");
        var settings = new CraneFixtureSettings("nondefault_crane", CraneFixtureSettings.YellowTeamColor);
        try
        {
            CraneFixtureManifest.WriteAtomically(path, "stack-one", settings);
            Assert.Equal(settings, CraneFixtureManifest.ReadForStack(path, "stack-one"));
            Assert.Throws<IOException>(() => CraneFixtureManifest.WriteAtomically(
                path, "stack-two", new CraneFixtureSettings("other", CraneFixtureSettings.YellowTeamColor)));
            Assert.Equal(settings, CraneFixtureManifest.ReadForStack(path, "stack-one"));
            Assert.Throws<InvalidDataException>(() => CraneFixtureManifest.ReadForStack(path, "stack-two"));

            var json = File.ReadAllText(path);
            Assert.Contains("stack-one", json, StringComparison.Ordinal);
            Assert.Contains("nondefault_crane", json, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MissingMalformedOrInvalidManifestFailsClosed()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"duck-crane-manifest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var malformedPath = Path.Combine(directory, "malformed.json");
        var invalidPath = Path.Combine(directory, "invalid.json");
        try
        {
            Assert.Throws<InvalidDataException>(() =>
                CraneFixtureManifest.ReadForStack(Path.Combine(directory, "missing.json"), "stack"));
            File.WriteAllText(malformedPath, "{");
            Assert.Throws<InvalidDataException>(() =>
                CraneFixtureManifest.ReadForStack(malformedPath, "stack"));
            File.WriteAllText(invalidPath, """{"StackId":"stack","TeamName":"invalid name","TeamColor":"YELLOW"}""");
            Assert.Throws<InvalidDataException>(() =>
                CraneFixtureManifest.ReadForStack(invalidPath, "stack"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
