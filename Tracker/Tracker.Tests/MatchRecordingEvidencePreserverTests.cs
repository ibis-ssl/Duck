using System.IO.Compression;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

public sealed class MatchRecordingEvidencePreserverTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"match-recording-preserver-{Guid.NewGuid():N}");

    [Fact]
    public void CopySslArchivesPreservesEveryAvailableFileWithoutValidatingIt()
    {
        var source = Path.Combine(_root, "recorder");
        var destination = Path.Combine(_root, "artifacts", "ssl-raw");
        Directory.CreateDirectory(source);
        var first = new byte[] { 0x1f, 0x8b, 0x08, 0x00, 0x01, 0x02 };
        var second = new byte[] { 0x00, 0xff, 0x10, 0x80, 0x41 };
        File.WriteAllBytes(Path.Combine(source, "one.log.gz"), first);
        File.WriteAllBytes(Path.Combine(source, "two.log.gz"), second);

        var copied = MatchRecordingEvidencePreserver.CopySslArchives(source, destination);

        Assert.Equal(2, copied);
        Assert.Equal(first, File.ReadAllBytes(Path.Combine(destination, "one.log.gz")));
        Assert.Equal(second, File.ReadAllBytes(Path.Combine(destination, "two.log.gz")));
    }

    [Fact]
    public void WriteCraneRecoveryArchivePreservesNestedBinaryRecordingBytes()
    {
        var source = Path.Combine(_root, "recording");
        var nested = Path.Combine(source, "data", "chunk.db3");
        var metadata = Path.Combine(source, "metadata.yaml");
        var destination = Path.Combine(_root, "artifacts", "crane-recovery.zip.gz");
        Directory.CreateDirectory(Path.GetDirectoryName(nested)!);
        var databaseBytes = Enumerable.Range(0, 512).Select(value => (byte)(value * 37)).ToArray();
        var metadataBytes = System.Text.Encoding.UTF8.GetBytes("rosbag2_bagfile_information:\n  version: 5\n");
        File.WriteAllBytes(nested, databaseBytes);
        File.WriteAllBytes(metadata, metadataBytes);

        MatchRecordingEvidencePreserver.WriteCraneRecoveryArchive(source, destination);

        using var compressed = File.OpenRead(destination);
        using var gzip = new GZipStream(compressed, CompressionMode.Decompress);
        using var expandedZip = new MemoryStream();
        gzip.CopyTo(expandedZip);
        expandedZip.Position = 0;
        using var archive = new ZipArchive(expandedZip, ZipArchiveMode.Read);
        using var recoveredDatabase = new MemoryStream();
        archive.GetEntry("data/chunk.db3")!.Open().CopyTo(recoveredDatabase);
        using var recoveredMetadata = new MemoryStream();
        archive.GetEntry("metadata.yaml")!.Open().CopyTo(recoveredMetadata);

        Assert.Equal(databaseBytes, recoveredDatabase.ToArray());
        Assert.Equal(metadataBytes, recoveredMetadata.ToArray());
        Assert.True(Directory.Exists(source));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
