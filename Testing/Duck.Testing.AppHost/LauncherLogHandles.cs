using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Duck.Testing.AppHost;

/// <summary>ランチャーがログを回収する標準出力と標準エラーを AppHost の子プロセスに継承させません。</summary>
internal static class LauncherLogHandles
{
    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;
    private const uint HandleFlagInherit = 1;

    /// <summary>ランチャー経由の Windows 起動時に、標準出力・標準エラーの継承を無効にします。</summary>
    /// <remarks>常駐コンテナを扱う Docker CLI が AppHost より長く動いても、ログの回収が完了します。</remarks>
    internal static void PreventChildInheritance()
    {
        if (!OperatingSystem.IsWindows() ||
            Environment.GetEnvironmentVariable("DUCK_LAUNCHER_REDIRECTED_OUTPUT") != "1")
        {
            return;
        }

        PreventInheritance(StdOutputHandle);
        PreventInheritance(StdErrorHandle);
    }

    private static void PreventInheritance(int standardHandle)
    {
        if (!SetHandleInformation(GetStdHandle(standardHandle), HandleFlagInherit, 0))
        {
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Could not make the launcher log handle private to AppHost.");
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetHandleInformation(IntPtr handle, uint mask, uint flags);
}
