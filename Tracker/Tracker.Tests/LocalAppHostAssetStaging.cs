using System.Security.Cryptography;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

/// <summary>既存のローカル受入資料を検査し、一時配置したファイルだけを回収します。</summary>
internal static class LocalAppHostAssetStaging
{
    private const string HelperName = BaseSimulationResourceRegistration.Cm4TransportHelperAssetName;
    private const string ManifestSuffix = ".sha256";

    internal static string ResolveCm4TransportHelperSource(string repositoryRoot) =>
        ResolveCm4TransportHelperSource(repositoryRoot, AppContext.BaseDirectory,
            Environment.GetEnvironmentVariable("DUCK_ASPIRE_CM4_TRANSPORT_HELPER"));

    internal static string ResolveCm4TransportHelperSource(string repositoryRoot, string appContextBaseDirectory, string? configuredPath)
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var fullPath = Path.GetFullPath(configuredPath);
            if (!File.Exists(fullPath)) throw new FileNotFoundException($"Configured CM4 transport helper does not exist: {fullPath}");
            Cm4HelperPreflight.Validate(fullPath);
            return fullPath;
        }
        var outputDirectory = new DirectoryInfo(Path.TrimEndingDirectorySeparator(appContextBaseDirectory));
        var targetFramework = outputDirectory.Name;
        var configuration = outputDirectory.Parent?.Name;
        var appHostProject = Path.Combine(repositoryRoot, "Testing", "Duck.Testing.AppHost");
        var roots = new[]
        {
            appContextBaseDirectory,
            configuration is null ? string.Empty : Path.Combine(appHostProject, "obj", configuration, targetFramework),
            Path.Combine(appHostProject, "obj", "Debug", targetFramework),
            Path.Combine(appHostProject, "obj", "Release", targetFramework),
            configuration is null ? string.Empty : Path.Combine(appHostProject, "bin", configuration, targetFramework),
            Path.Combine(repositoryRoot, "artifacts", "issue81-runtime-bundle", "Duck.Testing.AppHost"),
        };
        foreach (var root in roots.Where(root => !string.IsNullOrWhiteSpace(root)))
        {
            var candidate = Path.GetFullPath(Path.Combine(root, HelperName));
            // An incomplete or inconsistent higher-priority pair is not silently replaced.
            if (!File.Exists(candidate) && !File.Exists(candidate + ManifestSuffix)) continue;
            Cm4HelperPreflight.Validate(candidate);
            return candidate;
        }
        throw new FileNotFoundException($"The existing CM4 transport helper and source manifest were not found. Set DUCK_ASPIRE_CM4_TRANSPORT_HELPER to the locally prepared helper asset. Searched: {string.Join(Path.PathSeparator, roots.Where(root => !string.IsNullOrWhiteSpace(root)).Select(root => Path.Combine(root, HelperName)))}");
    }

    internal static IReadOnlyList<StagedAppHostAsset> StageLocalAppHostAssets(string cm4HelperSource, string craneOverlaySource) =>
        StageLocalAppHostAssets(cm4HelperSource, craneOverlaySource, AppContext.BaseDirectory);

    internal static IReadOnlyList<StagedAppHostAsset> StageLocalAppHostAssets(string cm4HelperSource, string craneOverlaySource, string destinationRoot)
    {
        Cm4HelperPreflight.Validate(cm4HelperSource);
        var destinationHelper = Path.Combine(destinationRoot, HelperName);
        // Do not attach a manifest to an existing unrelated/legacy executable (or vice versa).
        if (File.Exists(destinationHelper) != File.Exists(destinationHelper + ManifestSuffix))
            throw new InvalidOperationException($"Refusing to complete an existing partial CM4 transport helper pair at '{destinationHelper}'.");
        var staged = new List<StagedAppHostAsset>();
        try
        {
            Stage(cm4HelperSource, HelperName);
            Stage(cm4HelperSource + ManifestSuffix, HelperName + ManifestSuffix);
            Cm4HelperPreflight.Validate(destinationHelper);
            Stage(craneOverlaySource, "crane-launch-overlay");
            return staged;
        }
        catch
        {
            CleanupStagedAppHostAssets(staged);
            throw;
        }

        void Stage(string sourcePath, string assetName)
        {
            if (!File.Exists(sourcePath)) throw new FileNotFoundException($"AppHost asset source was not found: {sourcePath}");
            var source = Path.GetFullPath(sourcePath);
            var destination = Path.GetFullPath(Path.Combine(destinationRoot, assetName));
            if (string.Equals(source, destination, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)) return;
            var sourceHash = GetSha256(source);
            if (File.Exists(destination))
            {
                if (GetSha256(destination) != sourceHash)
                    throw new InvalidOperationException($"Refusing to replace a different AppHost asset at '{destination}'.");
                return;
            }
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.Copy(source, temporary, overwrite: false);
                if (GetSha256(temporary) != sourceHash)
                    throw new InvalidOperationException($"Source asset changed during staging: {source}");
                if ((assetName == HelperName || assetName == "crane-launch-overlay") && !OperatingSystem.IsWindows())
                    File.SetUnixFileMode(temporary, File.GetUnixFileMode(source));
                File.Move(temporary, destination, overwrite: false);
                staged.Add(new StagedAppHostAsset(destination, sourceHash));
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }

    /// <summary>終了処理の成否にかかわらず、一時配置と試験用の後処理を回収します。</summary>
    internal static async Task FinishWithStagedAssetCleanupAsync(
        Func<Task> finish, IEnumerable<StagedAppHostAsset> stagedAssets,
        Exception? originalFailure, Action release, Action<string> report)
    {
        Exception? primary = originalFailure;
        var secondary = new List<Exception>();
        void Record(Exception exception)
        {
            if (primary is null) primary = exception;
            else secondary.Add(exception);
        }
        try { await finish(); }
        catch (Exception exception) { Record(exception); }
        finally
        {
            try { CleanupStagedAppHostAssets(stagedAssets); }
            catch (Exception exception) { Record(exception); }
            finally
            {
                try { release(); }
                catch (Exception exception) { Record(exception); }
            }
        }
        foreach (var exception in secondary)
        {
            // A diagnostic failure must not replace the primary test exception.
            try { report($"Secondary NET-003 cleanup failure: {exception}"); }
            catch (Exception) { }
        }
        if (originalFailure is null && primary is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();
    }

    internal static void CleanupStagedAppHostAssets(IEnumerable<StagedAppHostAsset> stagedAssets)
    {
        foreach (var asset in stagedAssets.Reverse())
        {
            if (File.Exists(asset.Path) && GetSha256(asset.Path) == asset.Sha256) File.Delete(asset.Path);
            else if (File.Exists(asset.Path)) Console.Error.WriteLine($"Refusing to remove a changed staged AppHost asset: {asset.Path}");
        }
    }

    internal static string GetSha256(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    internal sealed record StagedAppHostAsset(string Path, string Sha256);
}
