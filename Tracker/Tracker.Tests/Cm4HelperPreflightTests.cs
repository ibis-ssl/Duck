using System.Buffers.Binary;
using System.Security.Cryptography;
using Duck.Testing.AppHost;

namespace Tracker.Tests;

/// <summary>起動前の補助ファイルの照合条件を検証します。</summary>
public sealed class Cm4HelperPreflightTests
{
    [Fact]
    public void CompleteManifestAcceptsMatchingStaticElfAndRejectsChangedInputs()
    {
        var directory = Path.Combine(Path.GetTempPath(), "duck-cm4-preflight-" + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "cm4-transport-control");
        try
        {
            Assert.Contains("is missing", Assert.Throws<InvalidOperationException>(() => Cm4HelperPreflight.Validate(path)).Message);
            // Header-only fixture checks format validation, not executability.
            var elf = new byte[120];
            new byte[] { 0x7f, 0x45, 0x4c, 0x46, 2, 1, 1 }.CopyTo(elf, 0);
            BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(16), 2);
            BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(18), 62);
            BinaryPrimitives.WriteUInt64LittleEndian(elf.AsSpan(32), 64);
            BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(54), 56);
            BinaryPrimitives.WriteUInt16LittleEndian(elf.AsSpan(56), 1);
            BinaryPrimitives.WriteUInt32LittleEndian(elf.AsSpan(64), 1);
            File.WriteAllBytes(path, elf);
            Assert.Contains("manifest is missing", Assert.Throws<InvalidOperationException>(() => Cm4HelperPreflight.Validate(path)).Message);
            const string prefix = "Cm4HelperSource.";
            var assembly = typeof(Cm4HelperPreflight).Assembly;
            var hashes = assembly.GetManifestResourceNames().Where(name => name.StartsWith(prefix, StringComparison.Ordinal))
                .Select(name => { using var stream = assembly.GetManifestResourceStream(name)!; return Convert.ToHexString(SHA256.HashData(stream)) + "  " + name[prefix.Length..]; }).ToList();
            hashes.Add(Convert.ToHexString(SHA256.HashData(elf)) + "  cm4-transport-control");
            File.WriteAllLines(path + ".sha256", hashes);
            Cm4HelperPreflight.Validate(path);
            var first = hashes[0];
            hashes[0] = new string('0', 64) + first[64..];
            File.WriteAllLines(path + ".sha256", hashes);
            Assert.Contains("source mismatch", Assert.Throws<InvalidOperationException>(() => Cm4HelperPreflight.Validate(path)).Message);
            hashes[0] = first;
            File.WriteAllLines(path + ".sha256", hashes);
            elf[0] = 0;
            File.WriteAllBytes(path, elf);
            Assert.Contains("binary digest mismatch", Assert.Throws<InvalidOperationException>(() => Cm4HelperPreflight.Validate(path)).Message);
            hashes[^1] = Convert.ToHexString(SHA256.HashData(elf)) + "  cm4-transport-control";
            File.WriteAllLines(path + ".sha256", hashes);
            Assert.Contains("not a static Linux x64", Assert.Throws<InvalidOperationException>(() => Cm4HelperPreflight.Validate(path)).Message);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
