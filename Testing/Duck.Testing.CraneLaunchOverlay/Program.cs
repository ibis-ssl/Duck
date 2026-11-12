using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Xml.Linq;

namespace Duck.Testing.CraneLaunchOverlay;

internal static class CraneLaunchOverlayProgram
{
    private static readonly HashSet<string> ResourceNames = ["simulator", "cm4-sim"];
    private const string ExpectedLoopback = "127.0.0.1";
    private static readonly TimeSpan DnsTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ChildGracePeriod = TimeSpan.FromMilliseconds(2350);

    public static async Task<int> Main(string[] args)
    {
        var target = Environment.GetEnvironmentVariable("DUCK_CRANE_TARGET_ADDRESS");
        var targetPort = Environment.GetEnvironmentVariable("CRANE_TARGET_PORT");
        var expectedResource = Environment.GetEnvironmentVariable("DUCK_CRANE_EXPECTED_RESOURCE");
        var planner = Environment.GetEnvironmentVariable("PLANNER");
        var useIbisPacketAdapter = string.Equals(
            Environment.GetEnvironmentVariable("DUCK_CRANE_IBIS_PACKET_ADAPTER"), "true", StringComparison.OrdinalIgnoreCase);
        var plannerResource = ExpectedTargetResource(planner, useIbisPacketAdapter);
        if (string.IsNullOrWhiteSpace(target) || expectedResource != plannerResource ||
            !ResourceNames.Contains(expectedResource) ||
            useIbisPacketAdapter && expectedResource != "cm4-sim")
        {
            Console.Error.WriteLine("Crane target configuration is missing or does not match the selected planner.");
            return 2;
        }

        try
        {
            if (!int.TryParse(targetPort, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedTargetPort) ||
                parsedTargetPort is < 1 or > 65535)
                throw new InvalidOperationException("Crane target port must be a valid UDP port from 1 through 65535.");

            var address = await ResolveTargetAddressAsync(target, expectedResource, CancellationToken.None);
            var packageShare = FindPackageShareDirectory("crane_bringup");
            var adapterCommandPort = useIbisPacketAdapter
                ? CraneIbisPacketAdapter.DefaultCraneCommandPort
                : parsedTargetPort;
            await using var ibisAdapter = useIbisPacketAdapter
                ? new CraneIbisPacketAdapter(IPAddress.Parse(address))
                : null;
            ibisAdapter?.Start();
            var overlay = CreateOverlay(
                packageShare,
                ibisAdapter is not null ? ExpectedLoopback : address,
                ibisAdapter is not null ? adapterCommandPort : parsedTargetPort);
            var prefixes = Environment.GetEnvironmentVariable("AMENT_PREFIX_PATH") ?? string.Empty;
            try
            {
                var process = StartRosLaunch(args, overlay, prefixes);
                return ibisAdapter is null
                    ? await WaitForOwnedProcessAsync(process)
                    : await WaitForOwnedProcessAsync(process, ibisAdapter.Completion);
            }
            catch (OwnedChildNotReapedException)
            {
                overlay.Preserve();
                throw;
            }
            finally
            {
                overlay.Dispose();
            }
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Crane launch preparation failed: {error.GetType().Name}: {error.Message}");
            return 2;
        }
    }

    internal static string ExpectedTargetResource(string? planner, bool useMode4PacketAdapter) =>
        useMode4PacketAdapter || planner == "visibility_graph" ? "cm4-sim" : "simulator";

    internal static async Task<string> ResolveTargetAddressAsync(
        string target,
        string expectedResource,
        CancellationToken cancellationToken,
        Func<string, CancellationToken, Task<IPAddress[]>>? resolver = null)
    {
        if (!ResourceNames.Contains(expectedResource))
            throw new InvalidOperationException("The expected Crane target is not an allowed simulator resource.");
        if (ResourceNames.Contains(target))
        {
            if (target != expectedResource)
                throw new InvalidOperationException("The Crane target does not match the selected planner resource.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(DnsTimeout);
            IPAddress[] addresses;
            try
            {
                addresses = await (resolver is null
                    ? Dns.GetHostAddressesAsync(target, timeout.Token)
                    : resolver(target, timeout.Token)).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("Simulator IPv4 name resolution exceeded its three-second deadline.");
            }

            var unique = addresses.Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => address.ToString()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (unique.Length != 1 || !IsUnicastIpv4(unique[0]))
                throw new InvalidOperationException("The selected simulator must resolve to exactly one safe unicast IPv4 address.");
            return unique[0];
        }
        if (target != ExpectedLoopback)
            throw new InvalidOperationException("A literal Crane target must be the selected resource or 127.0.0.1.");
        return target;
    }

    internal static byte[] PatchSimulatorTarget(byte[] launchBytes, string address, int targetPort)
    {
        if (!IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != AddressFamily.InterNetwork ||
            parsed.ToString() != address || parsed.Equals(IPAddress.Any) || parsed.Equals(IPAddress.Broadcast) ||
            parsed.IsMulticast())
            throw new InvalidOperationException("The Crane target must be a safe IPv4 address.");
        if (targetPort is < 1 or > 65535)
            throw new InvalidOperationException("The Crane target port must be a valid UDP port from 1 through 65535.");

        var document = XDocument.Parse(System.Text.Encoding.UTF8.GetString(launchBytes), LoadOptions.PreserveWhitespace);
        var simLets = document.Descendants()
            .Where(element => element.Name.LocalName == "let" &&
                ((string?)element.Attribute("if") is null or "$(var sim)") &&
                element.Attribute("unless") is null)
            .ToArray();
        var addressCandidates = simLets
            .Where(element => (string?)element.Attribute("name") == "ibis_target_address")
            .ToArray();
        if (addressCandidates.Length != 1 || (string?)addressCandidates[0].Attribute("value") != ExpectedLoopback)
            throw new InvalidOperationException("Crane sim=true must declare exactly one loopback ibis_target_address let.");
        var portCandidates = simLets
            .Where(element => (string?)element.Attribute("name") == "ibis_target_port")
            .ToArray();
        if (portCandidates.Length != 1 ||
            !int.TryParse(
                (string?)portCandidates[0].Attribute("value"),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var existingPort) ||
            existingPort is < 1 or > 65535)
            throw new InvalidOperationException("Crane sim=true must declare exactly one valid ibis_target_port let.");

        addressCandidates[0].SetAttributeValue("value", address);
        portCandidates[0].SetAttributeValue("value", targetPort.ToString(CultureInfo.InvariantCulture));
        return System.Text.Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
    }

    private static bool IsUnicastIpv4(string value) =>
        IPAddress.TryParse(value, out var address) && address.AddressFamily == AddressFamily.InterNetwork &&
        address.ToString() == value && !address.Equals(IPAddress.Any) && !address.Equals(IPAddress.Broadcast) &&
        !address.IsMulticast() && !IPAddress.IsLoopback(address);

    private static string FindPackageShareDirectory(string packageName)
    {
        var prefixes = (Environment.GetEnvironmentVariable("AMENT_PREFIX_PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var prefix in prefixes)
        {
            var marker = Path.Combine(prefix, "share", "ament_index", "resource_index", "packages", packageName);
            var share = Path.Combine(prefix, "share", packageName);
            if (File.Exists(marker) && Directory.Exists(share))
                return Path.GetFullPath(share);
        }
        throw new DirectoryNotFoundException($"Ament package share was not found for '{packageName}'.");
    }

    internal static TemporaryDirectoryOverlay CreateOverlay(string packageSharePath, string address, int targetPort)
    {
        var packageShare = new DirectoryInfo(packageSharePath).ResolveLinkTarget(returnFinalTarget: true) as DirectoryInfo
            ?? new DirectoryInfo(packageSharePath);
        var launchSource = Path.Combine(packageShare.FullName, "launch", "crane.launch.xml");
        var markerSource = Path.Combine(packageShare.Parent!.Parent!.FullName,
            "share", "ament_index", "resource_index", "packages", "crane_bringup");
        if (!File.Exists(launchSource) || !File.Exists(markerSource))
            throw new FileNotFoundException("The Crane package launch file or ament package marker is missing.");

        var patched = PatchSimulatorTarget(File.ReadAllBytes(launchSource), address, targetPort);
        var root = Path.Combine(Path.GetTempPath(), "duck-crane-ament-" + Guid.NewGuid().ToString("N"));
        var packageDestination = Path.Combine(root, "share", "crane_bringup");
        var launchDestination = Path.Combine(packageDestination, "launch");
        var markerDestination = Path.Combine(root, "share", "ament_index", "resource_index", "packages", "crane_bringup");
        try
        {
            Directory.CreateDirectory(launchDestination);
            Directory.CreateDirectory(Path.GetDirectoryName(markerDestination)!);
            File.Copy(markerSource, markerDestination);

            foreach (var child in Directory.EnumerateFileSystemEntries(packageShare.FullName))
            {
                var name = Path.GetFileName(child);
                if (name != "launch")
                {
                    CreatePathLink(child, Path.Combine(packageDestination, name));
                    continue;
                }

                foreach (var launchChild in Directory.EnumerateFileSystemEntries(child))
                {
                    var destination = Path.Combine(launchDestination, Path.GetFileName(launchChild));
                    if (Path.GetFileName(launchChild) == "crane.launch.xml")
                        File.WriteAllBytes(destination, patched);
                    else
                        CreatePathLink(launchChild, destination);
                }
            }
        }
        catch
        {
            try { Directory.Delete(root, recursive: true); }
            catch (DirectoryNotFoundException) { }
            throw;
        }
        return new(root, root);
    }

    private static void CreatePathLink(string source, string destination)
    {
        if (Directory.Exists(source))
            Directory.CreateSymbolicLink(destination, source);
        else
            File.CreateSymbolicLink(destination, source);
    }

    private static Process StartRosLaunch(string[] launchArguments, TemporaryDirectoryOverlay overlay, string existingPrefixes)
    {
        var start = new ProcessStartInfo("setsid") { UseShellExecute = false };
        start.ArgumentList.Add("ros2");
        start.ArgumentList.Add("launch");
        start.ArgumentList.Add("crane_bringup");
        start.ArgumentList.Add("crane.launch.xml");
        foreach (var argument in launchArguments)
            start.ArgumentList.Add(argument);
        start.Environment["AMENT_PREFIX_PATH"] = string.Join(Path.PathSeparator,
            new[] { overlay.ShareDirectory, existingPrefixes }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start ros2 launch.");
        return process;
    }

    private static async Task<int> WaitForOwnedProcessAsync(Process process, Task? adapterCompletion = null)
    {
        var signal = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, context =>
        {
            context.Cancel = true;
            signal.TrySetResult(2);
        });
        using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context =>
        {
            context.Cancel = true;
            signal.TrySetResult(15);
        });
        return await WaitForOwnedProcessCoreAsync(process, adapterCompletion, signal.Task).ConfigureAwait(false);
    }

    internal static async Task<int> WaitForOwnedProcessCoreAsync(
        Process process,
        Task? adapterCompletion,
        Task<int> signal)
    {
        using var processLifetime = process;
        using var stop = new CancellationTokenSource();
        var processExit = process.WaitForExitAsync(stop.Token);
        try
        {
            var waits = adapterCompletion is null
                ? new Task[] { processExit, signal }
                : [processExit, adapterCompletion, signal];
            var completed = await Task.WhenAny(waits).ConfigureAwait(false);
            if (completed == signal)
            {
                var receivedSignal = await signal.ConfigureAwait(false);
                stop.Cancel();
                await StopAndReapOwnedGroupAsync(process).ConfigureAwait(false);
                return 128 + receivedSignal;
            }
            if (adapterCompletion is not null && completed == adapterCompletion)
            {
                try
                {
                    await adapterCompletion.ConfigureAwait(false);
                }
                catch
                {
                    stop.Cancel();
                    await StopAndReapOwnedGroupAsync(process).ConfigureAwait(false);
                    throw;
                }
                stop.Cancel();
                await StopAndReapOwnedGroupAsync(process).ConfigureAwait(false);
                throw new InvalidOperationException("Crane mode-4 forwarding stopped while ROS launch was still running.");
            }
            await processExit.ConfigureAwait(false);
            if (OwnedProcessGroupExists(process.Id))
                await StopAndReapOwnedGroupAsync(process).ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
            await StopAndReapOwnedGroupAsync(process).ConfigureAwait(false);
            throw;
        }
    }

    internal static async Task StopAndReapOwnedGroupAsync(Process process)
    {
        SignalProcessGroup(process.Id, 15);
        var gracefulDeadline = Stopwatch.GetTimestamp() + (long)(ChildGracePeriod.TotalSeconds * Stopwatch.Frequency);
        while (OwnedProcessGroupExists(process.Id) && Stopwatch.GetTimestamp() < gracefulDeadline)
        {
            if (!process.HasExited)
            {
                try
                {
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    // Poll until the grace deadline; then signal the entire owned group.
                }
            }
            await Task.Delay(TimeSpan.FromMilliseconds(20)).ConfigureAwait(false);
        }
        if (!OwnedProcessGroupExists(process.Id))
            return;

        SignalProcessGroup(process.Id, 9);
        var forceDeadline = Stopwatch.GetTimestamp() + (long)(TimeSpan.FromMilliseconds(400).TotalSeconds * Stopwatch.Frequency);
        while (OwnedProcessGroupExists(process.Id) && Stopwatch.GetTimestamp() < forceDeadline)
            await Task.Delay(TimeSpan.FromMilliseconds(20)).ConfigureAwait(false);
        if (OwnedProcessGroupExists(process.Id))
            throw new OwnedChildNotReapedException($"Could not reap owned ROS process group {process.Id}.");
    }

    private static bool OwnedProcessGroupExists(int processId) =>
        OperatingSystem.IsLinux() ? kill(-processId, 0) == 0 : !Process.GetProcessById(processId).HasExited;

    private static void SignalProcessGroup(int processId, int signal)
    {
        if (!OperatingSystem.IsLinux())
        {
            try { Process.GetProcessById(processId).Kill(entireProcessTree: true); }
            catch (ArgumentException) { }
            return;
        }
        _ = kill(-processId, signal);
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int kill(int pid, int signal);

    internal sealed class TemporaryDirectoryOverlay : IDisposable
    {
        private readonly string root;
        public string ShareDirectory { get; }
        internal string RootDirectory { get; }
        private bool preserve;

        internal TemporaryDirectoryOverlay(string root, string shareDirectory)
        {
            this.root = root;
            RootDirectory = root;
            ShareDirectory = shareDirectory;
        }

        public void Preserve() => preserve = true;
        public void Dispose()
        {
            if (preserve)
                return;
            try { Directory.Delete(root, recursive: true); }
            catch (DirectoryNotFoundException) { }
        }
    }

    private sealed class OwnedChildNotReapedException(string message) : InvalidOperationException(message);
}

internal static class IpAddressExtensions
{
    internal static bool IsMulticast(this IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && bytes[0] is >= 224 and <= 239;
    }
}
