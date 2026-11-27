using System.IO.Compression;

namespace Duck.Testing.AppHost;

public static class MatchRecordingEvidencePreserver
{
    public static int CopySslArchives(string recorderDirectory, string destinationDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recorderDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectory);

        if (!Directory.Exists(recorderDirectory))
        {
            return 0;
        }

        Directory.CreateDirectory(destinationDirectory);
        var copied = 0;
        foreach (var sourcePath in Directory.EnumerateFiles(recorderDirectory, "*.log.gz", SearchOption.TopDirectoryOnly)
                     .Where(IsRegularFile)
                     .Order(StringComparer.Ordinal))
        {
            var destinationPath = Path.Combine(destinationDirectory, Path.GetFileName(sourcePath));
            File.Copy(sourcePath, destinationPath, overwrite: true);
            copied++;
        }

        return copied;
    }

    public static void WriteCraneRecoveryArchive(string recordingDirectory, string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var sourceRoot = Path.GetFullPath(recordingDirectory);
        if (!Directory.Exists(sourceRoot))
        {
            throw new DirectoryNotFoundException($"Crane recording directory does not exist: {sourceRoot}");
        }

        var sourceFiles = EnumerateRegularFiles(sourceRoot)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (sourceFiles.Length == 0)
        {
            throw new InvalidDataException("Crane recording directory contains no files to preserve.");
        }

        var output = Path.GetFullPath(destinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var temporaryZip = Path.Combine(Path.GetTempPath(), $"duck-crane-recovery-{Guid.NewGuid():N}.zip");
        try
        {
            using (var zipFile = new FileStream(temporaryZip, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(zipFile, ZipArchiveMode.Create, leaveOpen: false))
            {
                foreach (var sourcePath in sourceFiles)
                {
                    var relativePath = Path.GetRelativePath(sourceRoot, sourcePath).Replace('\\', '/');
                    var entry = archive.CreateEntry(relativePath, CompressionLevel.NoCompression);
                    using var entryStream = entry.Open();
                    using var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    sourceStream.CopyTo(entryStream);
                }
            }

            using var input = new FileStream(temporaryZip, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var outputFile = new FileStream(output, FileMode.Create, FileAccess.Write, FileShare.None);
            using var gzip = new GZipStream(outputFile, CompressionLevel.Optimal, leaveOpen: false);
            input.CopyTo(gzip);
        }
        finally
        {
            if (File.Exists(temporaryZip))
            {
                File.Delete(temporaryZip);
            }
        }
    }

    private static IEnumerable<string> EnumerateRegularFiles(string root)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);

        while (pendingDirectories.TryPop(out var directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if (IsRegularFile(file))
                {
                    yield return file;
                }
            }

            foreach (var childDirectory in Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly))
            {
                if ((File.GetAttributes(childDirectory) & FileAttributes.ReparsePoint) == 0)
                {
                    pendingDirectories.Push(childDirectory);
                }
            }
        }
    }

    private static bool IsRegularFile(string path) =>
        (File.GetAttributes(path) & (FileAttributes.Directory | FileAttributes.ReparsePoint)) == 0;
}
