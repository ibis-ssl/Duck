using System.Xml.Linq;

namespace Tracker.Tests;

/// <summary>
/// 何を確認しているか: RuntimeHost / DebugHost 分離前に project dependency と read-side 境界を Red contract として固定する。
/// </summary>
public class RuntimeHostDependencyBoundaryContractTests
{
    /// <summary>
    /// 何を確認しているか: Tracker.RuntimeHost が DebugHost / Server / diagnostics replay UI 側 project を参照しないことを固定する。
    /// </summary>
    [Fact]
    public void RuntimeHostProject_DoesNotReferenceDebugHostServerBlazorOrDiagnosticsReplayProjects()
    {
        var runtimeHostProjectPath = RepositoryPath("Tracker", "Tracker.RuntimeHost", "Tracker.RuntimeHost.csproj");

        Assert.True(
            File.Exists(runtimeHostProjectPath),
            "RUNTIME-HOST-002 contract requires Tracker.RuntimeHost project to exist before dependency boundaries can be checked.");

        var project = XDocument.Load(runtimeHostProjectPath);
        var references = project
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value ?? string.Empty)
            .Where(reference => !string.IsNullOrWhiteSpace(reference))
            .Select(reference => ResolveProjectReferencePath(runtimeHostProjectPath, reference))
            .ToArray();
        var forbiddenReferences = new[]
        {
            RepositoryPath("Tracker", "Tracker.Server", "Tracker.Server.csproj"),
            RepositoryPath("Tracker", "Tracker.DebugHost", "Tracker.DebugHost.csproj"),
            RepositoryPath("Tracker", "Tracker.CaptureReplay", "Tracker.CaptureReplay.csproj"),
        }.Select(NormalizeFullPath).ToArray();

        foreach (var forbiddenReference in forbiddenReferences)
        {
            Assert.DoesNotContain(
                references,
                reference => string.Equals(reference, forbiddenReference, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// 何を確認しているか: RuntimeHost source が diagnostics logging / replay / Blazor UI namespace を直接参照しないことを固定する。
    /// </summary>
    [Fact]
    public void RuntimeHostSource_DoesNotDirectlyReferenceDiagnosticsReplayOrBlazorUiNamespaces()
    {
        var runtimeHostRoot = RepositoryPath("Tracker", "Tracker.RuntimeHost");

        Assert.True(
            Directory.Exists(runtimeHostRoot),
            "RUNTIME-HOST-002 contract requires Tracker.RuntimeHost source root to exist before source boundary references can be checked.");

        var sourceText = ReadSourceText(runtimeHostRoot);
        var forbiddenTokens = new[]
        {
            "Tracker.Server.",
            "Tracker.DebugHost.",
            "namespace Tracker.Server",
            "namespace Tracker.DebugHost",
            "TrackerDiagnostics",
            "DiagnosticsPlayback",
            "TrackerRenderSnapshot",
            "TrackerPacketSnapshotLog",
            "Tracker.CaptureReplay",
            "Microsoft.AspNetCore.Components",
            "AddRazorComponents",
            "MapRazorComponents",
        };
        var hits = forbiddenTokens
            .Where(token => sourceText.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            hits.Length == 0,
            $"Tracker.RuntimeHost source must not directly reference diagnostics replay, logging, or Blazor UI boundaries. Found: {string.Join(", ", hits)}");
    }

    /// <summary>
    /// 何を確認しているか: DebugHost の UI / diagnostics replay / render source が tracker operation loop を直接駆動しないことを固定する。
    /// </summary>
    [Fact]
    public void DebugHostUiDiagnosticsAndRenderSources_DoNotDriveTrackerOperationLoop()
    {
        var homePath = RepositoryPath("Tracker", "Tracker.DebugHost", "Components", "Pages", "Home.razor");
        var uiAndDiagnosticsSourcePaths = new[]
        {
            homePath,
            RepositoryPath("Tracker", "Tracker.DebugHost", "Components", "Pages", "Diagnostics.razor"),
            RepositoryPath("Tracker", "Tracker.DebugHost", "Components", "Pages", "Diagnostics.razor.cs"),
            RepositoryPath("Tracker", "Tracker.DebugHost", "Tracking", "DiagnosticsSampleHostedService.cs"),
            RepositoryPath("Tracker", "Tracker.DebugHost", "Tracking", "TrackerDiagnosticsComparisonViewStateReader.cs"),
            RepositoryPath("Tracker", "Tracker.DebugHost", "Vision", "VisionLiveComparisonViewState.cs"),
            RepositoryPath("Tracker", "Tracker.DebugHost", "Vision", "VisionLiveDisplaySnapshotProvider.cs"),
        };

        foreach (var sourcePath in uiAndDiagnosticsSourcePaths)
        {
            Assert.True(
                File.Exists(sourcePath),
                $"RUNTIME-HOST-011 contract requires DebugHost read-side source to exist before boundary checks can run: {sourcePath}");
        }

        var homeSource = File.ReadAllText(homePath);
        Assert.Contains("@inject VisionLiveDisplaySnapshotProvider", homeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("@inject VisionPacketStore", homeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("@inject TrackedSnapshotStore", homeSource, StringComparison.Ordinal);

        var sourceText = string.Join(Environment.NewLine, uiAndDiagnosticsSourcePaths.Select(File.ReadAllText));
        var forbiddenTokens = new[]
        {
            "TrackerCoordinator",
            "ITrackerEngine",
            "TrackerPacketGenerator",
            "ITrackerPacketPublisher",
            "VisionReceiverService",
            "ProcessPacket(",
        };
        var hits = forbiddenTokens
            .Where(token => sourceText.Contains(token, StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            hits.Length == 0,
            $"DebugHost UI, diagnostics replay, and render snapshot sources must read snapshots instead of driving the tracker operation loop. Found operation loop markers: {string.Join(", ", hits)}");
    }

    private static string ReadSourceText(string root)
    {
        var sourceFiles = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(path => IsSourceFile(path))
            .Where(path => !NormalizePath(path).Contains("/bin/", StringComparison.Ordinal))
            .Where(path => !NormalizePath(path).Contains("/obj/", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        return string.Join(Environment.NewLine, sourceFiles.Select(File.ReadAllText));
    }

    private static bool IsSourceFile(string path)
    {
        var extension = Path.GetExtension(path);
        return string.Equals(extension, ".cs", StringComparison.Ordinal)
            || string.Equals(extension, ".razor", StringComparison.Ordinal)
            || string.Equals(extension, ".cshtml", StringComparison.Ordinal);
    }

    private static string RepositoryPath(params string[] segments)
    {
        return Path.Combine([FindRepositoryRoot(), .. segments]);
    }

    private static string ResolveProjectReferencePath(string projectPath, string referenceInclude)
    {
        var projectDirectory = Path.GetDirectoryName(projectPath)
            ?? throw new InvalidOperationException($"Project path has no directory: {projectPath}");
        var pathLikeInclude = referenceInclude
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);

        return NormalizeFullPath(Path.GetFullPath(pathLikeInclude, projectDirectory));
    }

    private static string NormalizeFullPath(string path)
    {
        return NormalizePath(Path.GetFullPath(path));
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
