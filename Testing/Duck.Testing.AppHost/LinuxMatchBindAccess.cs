using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace Duck.Testing.AppHost;

/// <summary>Run bind-writing Linux match containers as the host owner of their run-scoped bind paths.</summary>
internal static class LinuxMatchBindAccess
{
    internal static void PrepareFile(string path, bool writable)
    {
        if (!OperatingSystem.IsLinux()) return;
        PrepareGroup(path);
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite |
            UnixFileMode.GroupRead | (writable ? UnixFileMode.GroupWrite : (UnixFileMode)0));
    }

    internal static void PrepareDirectory(string path)
    {
        if (!OperatingSystem.IsLinux()) return;
        PrepareGroup(path);
        // New files from either host or container inherit the same group.
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.SetGroup);
    }

    internal static void ApplyTo(IResourceBuilder<ContainerResource> resource)
    {
        if (!OperatingSystem.IsLinux()) return;

        var userId = GetEffectiveUserId();
        if (userId == 0)
            throw new InvalidOperationException("Linux match containers must not run as root to access host-owned bind paths.");

        var groupId = GetEffectiveGroupId();
        resource.WithContainerRuntimeArgs("--user",
            $"{userId.ToString(CultureInfo.InvariantCulture)}:{groupId.ToString(CultureInfo.InvariantCulture)}");
    }

    private static void PrepareGroup(string path)
    {
        // Reject redirected paths instead of changing permissions through links.
        for (var candidate = Path.GetFullPath(path); !string.IsNullOrEmpty(candidate);
             candidate = Path.GetDirectoryName(candidate))
            if ((File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Match bind path contains a symbolic link: {candidate}");
        // -1 preserves the owner UID. The caller must own this exact path and
        // belong to its effective group; no elevated helper or recursive chown.
        if (ChangeGroup(path, uint.MaxValue, GetEffectiveGroupId()) != 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), $"Could not set match bind group: {path}");
    }

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint GetEffectiveUserId();

    [DllImport("libc", EntryPoint = "getegid")]
    private static extern uint GetEffectiveGroupId();

    [DllImport("libc", EntryPoint = "chown", SetLastError = true)]
    private static extern int ChangeGroup(string path, uint unchangedOwner, uint group);
}
