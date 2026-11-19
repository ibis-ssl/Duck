namespace Duck.Testing.AppHost;

/// <summary>ホストの異常停止を終了コードへ伝える状態を管理します。</summary>
internal static class AppHostTerminationState
{
    private static int exitCode;

    /// <summary>現在のホスト終了コードを取得します。</summary>
    public static int ExitCode => Volatile.Read(ref exitCode);

    /// <summary>ホスト開始時に終了コードを成功値へ初期化します。</summary>
    public static void Reset() => Volatile.Write(ref exitCode, 0);

    /// <summary>処理失敗によるホスト停止を非ゼロ終了として記録します。</summary>
    public static void MarkFailed() => Volatile.Write(ref exitCode, 1);
}
