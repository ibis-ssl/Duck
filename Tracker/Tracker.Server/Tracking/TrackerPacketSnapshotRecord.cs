namespace Tracker.Server.Tracking;

/// <summary>
/// CaptureOn session folder の tracker packet snapshot sidecar に保存する 1 record。
/// </summary>
/// <param name="SchemaVersion">record schema version。</param>
/// <param name="ReceivedAt">tracker packet を受信した UTC 時刻。</param>
/// <param name="RemoteEndpoint">packet の送信元 endpoint 表示。</param>
/// <param name="SourceUuid">official tracker packet の source UUID。</param>
/// <param name="SourceName">official tracker packet の source name。</param>
/// <param name="SourceRole">own、external、unknown などの保存後分類。</param>
/// <param name="SourceLabel">UI や replay で使う source 表示 label。</param>
/// <param name="TrackedFrameNumber">tracker packet が表す tracked frame number。</param>
/// <param name="TrackedFrameTimestampNs">tracker packet が表す tracked frame timestamp。</param>
/// <param name="Summary">ball / robot 数や復元状態を含む短い summary。</param>
/// <param name="PayloadBase64">replay 用に復元できる raw tracker packet payload。</param>
public sealed record TrackerPacketSnapshotRecord(
    int SchemaVersion,
    DateTimeOffset ReceivedAt,
    string RemoteEndpoint,
    string SourceUuid,
    string SourceName,
    string SourceRole,
    string SourceLabel,
    uint TrackedFrameNumber,
    long TrackedFrameTimestampNs,
    string Summary,
    string PayloadBase64);
