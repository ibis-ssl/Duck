using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;

internal static class SharingCauseProbe
{
    private static readonly List<object> Results = new();
    private static int Unexpected;

    private static void Check(string reader, string phase, Func<int> action, bool expectSharingViolation)
    {
        try
        {
            int count = action();
            bool expected = !expectSharingViolation && count == 0;
            if (!expected) Unexpected++;
            Results.Add(new { reader, phase, outcome = "success", count, expected });
        }
        catch (Exception error)
        {
            Exception actual = error.GetBaseException();
            bool expected = expectSharingViolation && actual is IOException && actual.HResult == unchecked((int)0x80070020);
            if (!expected) Unexpected++;
            Results.Add(new { reader, phase, outcome = "exception", type = actual.GetType().FullName,
                hresult = $"0x{actual.HResult:X8}", message = actual.Message, expected });
        }
    }

    private static int ReadCompatible(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        int count = 0;
        string? line;
        while ((line = reader.ReadLine()) is not null)
            if (!string.IsNullOrWhiteSpace(line)) count++;
        return count;
    }

    private static int Main(string[] args)
    {
        string bin = Path.GetFullPath(args[0]);
        string output = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(output);
        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            string path = Path.Combine(bin, name.Name + ".dll");
            return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
        };
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(bin, "Tracker.DebugHost.dll"));
        foreach (string name in new[] { "DiagnosticsSampleLogReader", "TrackerPacketSnapshotLogReader", "TrackerSnapshotAlignmentLogReader" })
        {
            var type = assembly.GetType("Tracker.DebugHost.Tracking." + name, throwOnError: true)!;
            var method = type.GetMethod("ReadRecords", BindingFlags.Public | BindingFlags.Static)!;
            string path = Path.Combine(output, name + ".jsonl");
            int ReadActual()
            {
                var result = (IEnumerable)method.Invoke(null, new object[] { path })!;
                int count = 0;
                foreach (var item in result) count++;
                return count;
            }

            // 製品と同じ書込条件。空行で開く操作だけを検証し、JSONの意味には依存しない。
            using (var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)))
            {
                writer.WriteLine();
                writer.Flush();
                Check(name, "writer-open_actual-product-reader", ReadActual, true);
                Check(name, "writer-open_explicit-readwrite-reader", () => ReadCompatible(path), false);
            }
            Check(name, "writer-closed_actual-product-reader", ReadActual, false);
        }
        Console.WriteLine(JsonSerializer.Serialize(new {
            processId = Environment.ProcessId,
            framework = RuntimeInformation.FrameworkDescription,
            os = RuntimeInformation.OSDescription,
            productAssembly = assembly.Location,
            productMvid = assembly.ManifestModule.ModuleVersionId,
            cases = Results,
            unexpected = Unexpected
        }, new JsonSerializerOptions { WriteIndented = true }));
        return Unexpected == 0 ? 0 : 1;
    }
}
