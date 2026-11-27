using System.Buffers.Binary;
using System.Security.Cryptography;
using Duck.Testing.AppHost;
using static Tracker.Tests.LocalAppHostAssetStaging;

namespace Tracker.Tests;

/// <summary>ネットワークや実行資源を使わず受入資料の組と一時配置を検証します。</summary>
public sealed class LocalAppHostAssetStagingTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("duck-local-assets-").FullName;
    private string Output => Path.Combine(root, "test-output", "Debug", "net10.0");
    private string Bundle => Path.Combine(root, "artifacts", "issue81-runtime-bundle", "Duck.Testing.AppHost");
    private string Helper => Path.Combine(Bundle, "cm4-transport-control");
    private string Destination => Path.Combine(Output, "cm4-transport-control");

    [Fact]
    public void ResolvesCompleteBundlePairAndRejectsInvalidOverride()
    {
        CreatePair(Helper);
        Assert.Equal(Helper, ResolveCm4TransportHelperSource(root, Output, null));
        Assert.Equal(Helper, ResolveCm4TransportHelperSource(root, Output, Helper));
        var ex = Assert.Throws<FileNotFoundException>(() => ResolveCm4TransportHelperSource(root, Output, Path.Combine(root, "missing")));
        Assert.Contains("Configured CM4 transport helper does not exist", ex.Message);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PartialHigherPriorityPairNeverFallsBackOrMerges(bool helperOnly)
    {
        CreatePair(Helper);
        Directory.CreateDirectory(Output);
        File.Copy(helperOnly ? Helper : Helper + ".sha256", helperOnly ? Destination : Destination + ".sha256");
        Assert.Throws<InvalidOperationException>(() => ResolveCm4TransportHelperSource(root, Output, null));
        Assert.Throws<InvalidOperationException>(() => StageLocalAppHostAssets(Helper, Overlay(), Output));
        Assert.Equal(helperOnly, File.Exists(Destination));
        Assert.Equal(!helperOnly, File.Exists(Destination + ".sha256"));
        Assert.False(File.Exists(Path.Combine(Output, "crane-launch-overlay")));
    }

    [Fact]
    public void StagesWholePairThenCleansOnlyOwnedFiles()
    {
        CreatePair(Helper);
        Directory.CreateDirectory(Output);
        var overlay = Overlay();
        var staged = StageLocalAppHostAssets(Helper, overlay, Output);
        Assert.Equal(3, staged.Count);
        Cm4HelperPreflight.Validate(Destination);
        Assert.Equal(GetSha256(Helper + ".sha256"), GetSha256(Destination + ".sha256"));
        CleanupStagedAppHostAssets(staged);
        Assert.Empty(Directory.EnumerateFiles(Output));
        Assert.True(File.Exists(Helper));
        Assert.True(File.Exists(Helper + ".sha256"));
    }

    [Fact]
    public void ExistingMatchingPairIsPreservedAndConflictingManifestIsRejected()
    {
        CreatePair(Helper);
        Directory.CreateDirectory(Output);
        File.Copy(Helper, Destination);
        File.Copy(Helper + ".sha256", Destination + ".sha256");
        var overlay = Overlay();
        var staged = StageLocalAppHostAssets(Helper, overlay, Output);
        Assert.Single(staged);
        CleanupStagedAppHostAssets(staged);
        Assert.Equal(GetSha256(Helper), GetSha256(Destination));
        Assert.Equal(GetSha256(Helper + ".sha256"), GetSha256(Destination + ".sha256"));
        File.AppendAllText(Destination + ".sha256", "existing changed manifest");
        var hash = GetSha256(Destination + ".sha256");
        Assert.Throws<InvalidOperationException>(() => StageLocalAppHostAssets(Helper, overlay, Output));
        Assert.Equal(hash, GetSha256(Destination + ".sha256"));
        Assert.Equal(GetSha256(Helper), GetSha256(Destination));
    }

    [Fact]
    public void MissingOverlayRollsBackPairAndChangedStagedFileIsNotRemoved()
    {
        CreatePair(Helper);
        Directory.CreateDirectory(Output);
        Assert.Throws<FileNotFoundException>(() => StageLocalAppHostAssets(Helper, Path.Combine(root,"missing-overlay"), Output));
        Assert.Empty(Directory.EnumerateFiles(Output));
        var staged = StageLocalAppHostAssets(Helper, Overlay(), Output);
        File.AppendAllText(Destination, "changed by another actor");
        CleanupStagedAppHostAssets(staged);
        Assert.True(File.Exists(Destination));
        Assert.False(File.Exists(Destination + ".sha256"));
    }

    [Fact]
    public void InvalidSourcePairIsRejectedBeforeAnyPlacement()
    {
        CreatePair(Helper);
        File.AppendAllText(Helper, "modified");
        Directory.CreateDirectory(Output);
        Assert.Throws<InvalidOperationException>(() => ResolveCm4TransportHelperSource(root, Output, null));
        Assert.Throws<InvalidOperationException>(() => StageLocalAppHostAssets(Helper, Overlay(), Output));
        Assert.Empty(Directory.EnumerateFiles(Output));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailedFinalVerificationStillCleansOwnedPairAndPreservesPrimaryException(bool hasBodyFailure)
    {
        CreatePair(Helper);
        Directory.CreateDirectory(Output);
        var overlay = Overlay();
        var existingOverlay = Path.Combine(Output, "crane-launch-overlay");
        File.Copy(overlay, existingOverlay);
        var staged = StageLocalAppHostAssets(Helper, overlay, Output);
        Assert.Equal(2, staged.Count);
        var bodyFailure = new InvalidOperationException("original test failure");
        var shutdownFailure = new IOException("shutdown verification failed");
        var secondary = new List<string>();
        var released = false;
        async Task Exercise()
        {
            Exception? original = null;
            try { if (hasBodyFailure) throw bodyFailure; }
            catch (Exception exception) { original = exception; throw; }
            finally
            {
                await FinishWithStagedAssetCleanupAsync(
                    () => Task.FromException(shutdownFailure), staged, original,
                    () => released = true, secondary.Add);
            }
        }
        var observed = await Record.ExceptionAsync(Exercise);
        Assert.Same(hasBodyFailure ? (Exception)bodyFailure : shutdownFailure, observed);
        Assert.False(File.Exists(Destination));
        Assert.False(File.Exists(Destination + ".sha256"));
        Assert.True(released);
        Assert.True(File.Exists(existingOverlay));
        Assert.Equal(GetSha256(overlay), GetSha256(existingOverlay));
        Assert.Equal(hasBodyFailure, secondary.Any(message => message.Contains(shutdownFailure.Message)));
    }

    private string Overlay()
    {
        var path = Path.Combine(root, "overlay");
        File.WriteAllText(path, "fixture only");
        return path;
    }

    private static void CreatePair(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // Synthetic header for the format check; not an executable test.
        var elf = new byte[120];
        new byte[] { 0x7f, 0x45, 0x4c, 0x46, 2, 1, 1 }.CopyTo(elf, 0);
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(16), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(18), 62);
        BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(32), 64);
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(54), 56);
        BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(56), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(64), 1);
        File.WriteAllBytes(path, elf);
        const string prefix = "Cm4HelperSource.";
        var assembly = typeof(Cm4HelperPreflight).Assembly;
        var hashes = assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(name => { using var stream = assembly.GetManifestResourceStream(name)!; return Convert.ToHexString(SHA256.HashData(stream)) + "  " + name[prefix.Length..]; }).ToList();
        hashes.Add(GetSha256(path) + "  cm4-transport-control");
        File.WriteAllLines(path + ".sha256", hashes);
    }

    public void Dispose() => Directory.Delete(root, recursive: true);
}
