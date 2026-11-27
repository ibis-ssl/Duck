using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Duck.Testing.AppHost;

/// <summary>起動前に補助実行ファイルと生成時の入力を照合します。</summary>
internal static class Cm4HelperPreflight
{
    private const string Prefix = "Cm4HelperSource.";

    internal static void Validate(string path)
    {
        var guidance = "Prepare the Linux x64 helper using scripts/prepare-cm4-transport.sh or the matching CI runtime bundle; copy the helper and .sha256 into the AppHost intermediate output and rebuild.";
        InvalidOperationException Failure(string reason) => new($"CM4 transport helper {reason}: '{path}'. {guidance}");
        if (!File.Exists(path)) throw Failure("is missing");
        if (!File.Exists(path + ".sha256")) throw Failure("source manifest is missing");
        try
        {
            var hashes = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var line in File.ReadLines(path + ".sha256"))
            {
                if (line.Length <= 66 || line[64..66] != "  " || !line[..64].All(Uri.IsHexDigit) ||
                    !hashes.TryAdd(line[66..], line[..64])) throw Failure("source manifest is invalid");
            }
            var assembly = typeof(Cm4HelperPreflight).Assembly;
            var names = assembly.GetManifestResourceNames().Where(name => name.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();
            if (names.Length != 9 || hashes.Count != names.Length + 1) throw Failure("source manifest inputs differ");
            foreach (var name in names)
            {
                using var source = assembly.GetManifestResourceStream(name)!;
                if (!hashes.TryGetValue(name[Prefix.Length..], out var hash) ||
                    !string.Equals(hash, Convert.ToHexString(SHA256.HashData(source)), StringComparison.OrdinalIgnoreCase))
                    throw Failure($"source mismatch ({name[Prefix.Length..]})");
            }
            if (new FileInfo(path).Length > 16 * 1024 * 1024) throw Failure("has an unexpected size");
            var bytes = File.ReadAllBytes(path);
            if (!hashes.TryGetValue("cm4-transport-control", out var binaryHash) ||
                !string.Equals(binaryHash, Convert.ToHexString(SHA256.HashData(bytes)), StringComparison.OrdinalIgnoreCase))
                throw Failure("binary digest mismatch");
            if (!IsStaticLinuxX64(bytes)) throw Failure("is not a static Linux x64 ELF executable");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Failure($"could not be read ({exception.Message})");
        }
    }

    internal static bool IsStaticLinuxX64(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 64 || !bytes[..4].SequenceEqual(new byte[] { 0x7f, 0x45, 0x4c, 0x46 }) ||
            bytes[4] != 2 || bytes[5] != 1 || bytes[6] != 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[16..]) != 2 ||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[18..]) != 62) return false;
        var offset = BinaryPrimitives.ReadUInt64LittleEndian(bytes[32..]);
        var size = BinaryPrimitives.ReadUInt16LittleEndian(bytes[54..]);
        var count = BinaryPrimitives.ReadUInt16LittleEndian(bytes[56..]);
        if (size != 56 || count == 0 || offset > (ulong)bytes.Length ||
            (ulong)(size * (long)count) > (ulong)bytes.Length - offset) return false;
        var loads = false;
        for (var i = 0; i < count; i++)
        {
            var type = BinaryPrimitives.ReadUInt32LittleEndian(bytes[(int)(offset + (ulong)(i * size))..]);
            if (type is 2 or 3) return false;
            loads |= type == 1;
        }
        return loads;
    }
}
