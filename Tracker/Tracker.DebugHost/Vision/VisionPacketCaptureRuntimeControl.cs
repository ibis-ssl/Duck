namespace Tracker.DebugHost.Vision;

public sealed class VisionPacketCaptureRuntimeControl
{
    private readonly object gate = new();
    private readonly bool allowEnable;
    private bool enabled;

    /// <summary>
    /// capture の初期状態と実行中に有効化できるかを指定します。
    /// </summary>
    /// <param name="initialEnabled">起動時に記録を有効にする場合は <see langword="true"/>。</param>
    /// <param name="allowEnable">実行中の Capture On を許可する場合は <see langword="true"/>。</param>
    public VisionPacketCaptureRuntimeControl(bool initialEnabled, bool allowEnable = true)
    {
        this.allowEnable = allowEnable;
        enabled = initialEnabled && allowEnable;
    }

    /// <summary>実行中の Capture On を許可するかを取得します。</summary>
    public bool CanEnable => allowEnable;

    /// <summary>現在の capture 状態を取得します。</summary>
    public bool Enabled
    {
        get
        {
            lock (gate)
            {
                return enabled;
            }
        }
    }

    /// <summary>
    /// capture 状態を変更します。ReplayOnly では有効化要求を拒否します。
    /// </summary>
    /// <param name="value">有効化する場合は <see langword="true"/>。</param>
    /// <returns>要求を適用した場合は <see langword="true"/>。</returns>
    public bool SetEnabled(bool value)
    {
        lock (gate)
        {
            if (value && !allowEnable)
            {
                return false;
            }

            enabled = value;
            return true;
        }
    }
}
