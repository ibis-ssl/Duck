using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Duck.Testing.AppHost;

/// <summary>ランチャーがログを回収するための標準出力と標準エラーを、AppHost の終了時に閉じられるようにします。</summary>
internal static class LauncherLogHandles
{
    private const int StdOutputHandle = -11;
    private const int StdErrorHandle = -12;
    private const uint HandleFlagInherit = 1;

    /// <summary>Windows のランチャー経由で起動した場合、ログ用のハンドルを AppHost 自身に限定します。</summary>
    /// <remarks>常駐コンテナを扱う Docker CLI が AppHost より長く動いても、ランチャーは AppHost のログを末尾まで回収できます。</remarks>
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
