namespace Tracker.Core;

/// <summary>
/// raw vision packet から確定済み tracker frame と tracker event を生成する Core engine 契約。
/// </summary>
public interface ITrackerEngine
{
    /// <summary>
    /// 1 件の SSL_WrapperPacket または control-only 更新を処理し、publish 可能になった frame と event を返す。
    /// packet は profile switch だけを処理する場合に null を許可する。
    /// </summary>
    TrackerUpdateResult Update(
        SSL_WrapperPacket? packet,
        TrackerEngineSettings settings,
        TrackerProfileSwitchRequest? profileSwitchRequest = null);
}
