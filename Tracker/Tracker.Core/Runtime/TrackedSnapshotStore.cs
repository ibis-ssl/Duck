namespace Tracker.Core;

/// <summary>
/// tracker coordinator が更新し、read-side が読み取る最新 snapshot と publish 統計を thread-safe に保持する。
/// </summary>
public sealed class TrackedSnapshotStore
{
    private readonly object gate = new();
    private TrackerFrame? latestFrame;
    private DateTimeOffset? receivedAt;
    private string activeProfileName;
    private long publishSuccessCount;
    private long publishFailureCount;

    /// <summary>
    /// 初期 active profile 名を指定して snapshot store を作成する。
    /// </summary>
    public TrackedSnapshotStore(string initialActiveProfileName = "default")
    {
        activeProfileName = string.IsNullOrWhiteSpace(initialActiveProfileName)
            ? "default"
            : initialActiveProfileName;
    }

    /// <summary>
    /// 現在保持している latest frame、active profile、publish 統計の一貫した snapshot を返す。
    /// </summary>
    public TrackedSnapshot GetSnapshot()
    {
        lock (gate)
        {
            return new TrackedSnapshot(
                latestFrame,
                receivedAt,
                activeProfileName,
                publishSuccessCount,
                publishFailureCount);
        }
    }

    /// <summary>
    /// 最新 committed frame と受信時刻を保存し、frame metadata の profile 名を active profile として反映する。
    /// </summary>
    public void UpdateLatestFrame(TrackerFrame frame, DateTimeOffset frameReceivedAt)
    {
        lock (gate)
        {
            latestFrame = frame;
            receivedAt = frameReceivedAt;
            activeProfileName = frame.Metadata.ProfileName ?? activeProfileName;
        }
    }

    /// <summary>
    /// profile 名と publish 統計を維持したまま、最新 committed frame だけを消去する。
    /// </summary>
    public void ClearLatestFrame()
    {
        lock (gate)
        {
            latestFrame = null;
            receivedAt = null;
        }
    }

    /// <summary>
    /// active profile を切り替え、旧 profile の latest frame を read-side へ残さないように消去する。
    /// </summary>
    public void SwitchActiveProfile(string profileName)
    {
        lock (gate)
        {
            activeProfileName = profileName;
            latestFrame = null;
            receivedAt = null;
        }
    }

    /// <summary>
    /// latest frame を保持したまま active profile 名だけを更新する。
    /// </summary>
    public void SetActiveProfileName(string profileName)
    {
        lock (gate)
        {
            activeProfileName = profileName;
        }
    }

    /// <summary>
    /// tracker packet publish 成功回数を 1 増やす。
    /// </summary>
    public void RecordPublishSuccess()
    {
        lock (gate)
        {
            publishSuccessCount++;
        }
    }

    /// <summary>
    /// tracker packet publish 失敗回数を 1 増やす。
    /// </summary>
    public void RecordPublishFailure()
    {
        lock (gate)
        {
            publishFailureCount++;
        }
    }
}
