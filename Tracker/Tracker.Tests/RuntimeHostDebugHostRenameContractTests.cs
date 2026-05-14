using System.Xml.Linq;

namespace Tracker.Tests;

/// <summary>
/// RUNTIME-HOST-004 の DebugHost rename contract を固定する。
/// </summary>
public class RuntimeHostDebugHostRenameContractTests
{
    /// <summary>
    /// 何を確認しているか: active debug UI host が Tracker.Server ではなく Tracker.DebugHost project として存在することを確認する。
    /// </summary>
    [Fact]
    public void DebugHostProject_ReplacesServerProjectAndKeepsWebSdk()
    {
        var debugHostRoot = RepositoryPath("Tracker", "Tracker.DebugHost");
        var debugHostProject = Path.Combine(debugHostRoot, "Tracker.DebugHost.csproj");
        var serverRoot = RepositoryPath("Tracker", "Tracker.Server");
        var serverProject = Path.Combine(serverRoot, "Tracker.Server.csproj");

        Assert.True(
            Directory.Exists(debugHostRoot),
            "RUNTIME-HOST-004 requires the active debug UI host folder to be Tracker/Tracker.DebugHost.");
        Assert.True(
            File.Exists(debugHostProject),
            "RUNTIME-HOST-004 requires the active debug UI host project to be Tracker.DebugHost.csproj.");
        Assert.False(
            Directory.Exists(serverRoot),
            "Tracker/Tracker.Server must not remain as the active debug UI host folder after the DebugHost rename.");
        Assert.False(
            File.Exists(serverProject),
            "Tracker.Server.csproj must not remain as the active debug UI host project after the DebugHost rename.");

        var project = XDocument.Load(debugHostProject);
        Assert.Equal("Microsoft.NET.Sdk.Web", project.Root?.Attribute("Sdk")?.Value);
    }

    /// <summary>
    /// 何を確認しているか: solution と dependent projects が active DebugHost project を参照することを確認する。
    /// </summary>
    [Fact]
    public void ActiveProjectReferences_PointToDebugHostInsteadOfServer()
    {
        var files = new[]
        {
            RepositoryPath("Duck.slnx"),
            RepositoryPath("Tracker", "Tracker.CaptureReplay", "Tracker.CaptureReplay.csproj"),
            RepositoryPath("Tracker", "Tracker.Tests", "Tracker.Tests.csproj"),
        };

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);

            Assert.Contains("Tracker.DebugHost", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Tracker.Server", text, StringComparison.Ordinal);
            Assert.DoesNotContain("Tracker/Tracker.Server", text, StringComparison.Ordinal);
            Assert.DoesNotContain(@"Tracker\Tracker.Server", text, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 何を確認しているか: active DebugHost source と起動手順が Tracker.DebugHost namespace / path に揃っていることを確認する。
    /// </summary>
    [Fact]
    public void DebugHostSourceReadmeAndSettings_DoNotUseActiveServerNames()
    {
        var debugHostRoot = RepositoryPath("Tracker", "Tracker.DebugHost");

        Assert.True(
            Directory.Exists(debugHostRoot),
            "RUNTIME-HOST-004 requires Tracker.DebugHost source before namespace and launch-path contracts can be checked.");

        var sourceText = ReadFiles(debugHostRoot, IsSourceOrSettingsFile);
        Assert.Contains("Tracker.DebugHost", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("Tracker.Server", sourceText, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace Tracker.Server", sourceText, StringComparison.Ordinal);

        var rootReadme = File.ReadAllText(RepositoryPath("README.md"));
        var debugHostReadme = File.ReadAllText(Path.Combine(debugHostRoot, "README.md"));
        var navMarkup = File.ReadAllText(Path.Combine(debugHostRoot, "Components", "Layout", "NavMenu.razor"));
        var readmeText = string.Concat(rootReadme, Environment.NewLine, debugHostReadme);

        Assert.Contains("Tracker.DebugHost", readmeText, StringComparison.Ordinal);
        Assert.Contains("dotnet run --project Tracker/Tracker.DebugHost", readmeText, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet run --project Tracker/Tracker.Server", readmeText, StringComparison.Ordinal);
        Assert.Contains("sidebar-brand-text\">Tracker.DebugHost</span>", navMarkup, StringComparison.Ordinal);
        Assert.Contains("sidebar-brand-mark\" aria-hidden=\"true\">DH</span>", navMarkup, StringComparison.Ordinal);
        Assert.DoesNotContain("sidebar-brand-mark\" aria-hidden=\"true\">TS</span>", navMarkup, StringComparison.Ordinal);
    }

    private static string ReadFiles(string root, Func<string, bool> predicate)
    {
        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => !NormalizePath(path).Contains("/bin/", StringComparison.Ordinal))
            .Where(path => !NormalizePath(path).Contains("/obj/", StringComparison.Ordinal))
            .Where(predicate)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
    }

    private static bool IsSourceOrSettingsFile(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".cs", StringComparison.Ordinal)
            || string.Equals(extension, ".razor", StringComparison.Ordinal)
            || string.Equals(extension, ".cshtml", StringComparison.Ordinal)
            || string.Equals(extension, ".json", StringComparison.Ordinal);
    }

    private static string RepositoryPath(params string[] segments)
    {
        return Path.Combine([FindRepositoryRoot(), .. segments]);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Tracker", "Tracker.Tests", "Tracker.Tests.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root containing Tracker/Tracker.Tests/Tracker.Tests.csproj was not found.");
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }
}
