using Google.Protobuf;

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
/// <param name="SemanticSummary">raw payload から作成した比較用構造化 summary。</param>
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
    string PayloadBase64,
    TrackerPacketSnapshotSemanticSummary? SemanticSummary = null)
{
    /// <summary>
    /// official tracker packet から raw payload と raw 由来 semantic summary を持つ record を作る。
    /// </summary>
    public static TrackerPacketSnapshotRecord FromPacket(
        TrackerWrapperPacket packet,
        DateTimeOffset receivedAt,
        string? remoteEndpoint,
        string sourceRole,
        string? sourceLabel = null)
    {
        var payloadBase64 = Convert.ToBase64String(packet.ToByteArray());
        var sourceName = packet.SourceName ?? string.Empty;
        var normalizedRole = NormalizeSourceRole(sourceRole);
        var normalizedLabel = NormalizeSourceLabel(sourceLabel, sourceName, packet.Uuid, remoteEndpoint, normalizedRole);
        var frame = packet.TrackedFrame;
        var frameNumber = frame?.FrameNumber ?? 0;
        var timestampNs = ToTimestampNs(frame?.Timestamp ?? 0);
        var semanticSummary = TrackerPacketSnapshotSemanticSummary.FromPacket(
            packet,
            normalizedRole,
            normalizedLabel);

        return new TrackerPacketSnapshotRecord(
            SchemaVersion: 1,
            ReceivedAt: receivedAt.ToUniversalTime(),
            RemoteEndpoint: remoteEndpoint ?? string.Empty,
            SourceUuid: packet.Uuid ?? string.Empty,
            SourceName: sourceName,
            SourceRole: normalizedRole,
            SourceLabel: normalizedLabel,
            TrackedFrameNumber: frameNumber,
            TrackedFrameTimestampNs: timestampNs,
            Summary: semanticSummary.ToDisplaySummary(),
            PayloadBase64: payloadBase64,
            SemanticSummary: semanticSummary);
    }

    /// <summary>
    /// 古い record や手書き JSONL で summary がない場合に raw payload から補完する。
    /// </summary>
    public TrackerPacketSnapshotRecord EnsureSemanticSummary()
    {
        if (SemanticSummary is not null)
        {
            return this;
        }

        try
        {
            var payload = Convert.FromBase64String(PayloadBase64);
            var packet = TrackerWrapperPacket.Parser.ParseFrom(payload);
            var semanticSummary = TrackerPacketSnapshotSemanticSummary.FromPacket(
                packet,
                NormalizeSourceRole(SourceRole),
                NormalizeSourceLabel(SourceLabel, SourceName, SourceUuid, RemoteEndpoint, SourceRole));
            return this with
            {
                SemanticSummary = semanticSummary,
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidProtocolBufferException)
        {
            return this with
            {
                SemanticSummary = TrackerPacketSnapshotSemanticSummary.FromRecord(this),
            };
        }
    }

    internal static long ToTimestampNs(double timestampSeconds)
    {
        return (long)Math.Round(timestampSeconds * 1_000_000_000L, MidpointRounding.AwayFromZero);
    }

    internal static string NormalizeSourceRole(string? sourceRole)
    {
        return string.IsNullOrWhiteSpace(sourceRole) ? "unknown" : sourceRole;
    }

    internal static string NormalizeSourceLabel(
        string? sourceLabel,
        string? sourceName,
        string? sourceUuid,
        string? remoteEndpoint,
        string? sourceRole)
    {
        if (!string.IsNullOrWhiteSpace(sourceLabel))
        {
            return sourceLabel;
        }

        if (!string.IsNullOrWhiteSpace(sourceName))
        {
            return sourceName;
        }

        if (!string.IsNullOrWhiteSpace(sourceUuid))
        {
            return sourceUuid;
        }

        if (!string.IsNullOrWhiteSpace(remoteEndpoint))
        {
            return remoteEndpoint;
        }

        return NormalizeSourceRole(sourceRole);
    }
}

/// <summary>
/// tracker packet raw payload から作る比較用 summary。
/// </summary>
public sealed record TrackerPacketSnapshotSemanticSummary(
    int BallCount,
    int RobotCount,
    uint TrackedFrameNumber,
    long TrackedFrameTimestampNs,
    string SourceUuid,
    string SourceName,
    string SourceRole,
    string SourceLabel,
    IReadOnlyList<TrackerPacketSnapshotBallSummary> Balls,
    IReadOnlyList<TrackerPacketSnapshotRobotSummary> Robots)
{
    /// <summary>
    /// official tracker packet から比較用 summary を作る。
    /// </summary>
    public static TrackerPacketSnapshotSemanticSummary FromPacket(
        TrackerWrapperPacket packet,
        string sourceRole,
        string sourceLabel)
    {
        var frame = packet.TrackedFrame;
        var balls = frame?.Balls
            .Select((ball, index) => new TrackerPacketSnapshotBallSummary(
                Index: index,
                XMm: ToMillimeters(ball.Pos?.X ?? 0),
                YMm: ToMillimeters(ball.Pos?.Y ?? 0),
                ZMm: ToMillimeters(ball.Pos?.Z ?? 0),
                Visibility: ball.Visibility))
            .ToArray() ?? [];
        var robots = frame?.Robots
            .Select(robot => new TrackerPacketSnapshotRobotSummary(
                Team: robot.RobotId?.Team.ToString() ?? Team.Unknown.ToString(),
                RobotId: robot.RobotId?.Id ?? 0,
                XMm: ToMillimeters(robot.Pos?.X ?? 0),
                YMm: ToMillimeters(robot.Pos?.Y ?? 0),
                OrientationRad: robot.Orientation,
                Visibility: robot.Visibility))
            .ToArray() ?? [];

        return new TrackerPacketSnapshotSemanticSummary(
            BallCount: balls.Length,
            RobotCount: robots.Length,
            TrackedFrameNumber: frame?.FrameNumber ?? 0,
            TrackedFrameTimestampNs: TrackerPacketSnapshotRecord.ToTimestampNs(frame?.Timestamp ?? 0),
            SourceUuid: packet.Uuid ?? string.Empty,
            SourceName: packet.SourceName ?? string.Empty,
            SourceRole: TrackerPacketSnapshotRecord.NormalizeSourceRole(sourceRole),
            SourceLabel: sourceLabel,
            Balls: balls,
            Robots: robots);
    }

    /// <summary>
    /// payload decode できない record でも source identity と frame metadata を保持した summary を作る。
    /// </summary>
    public static TrackerPacketSnapshotSemanticSummary FromRecord(TrackerPacketSnapshotRecord record)
    {
        return new TrackerPacketSnapshotSemanticSummary(
            BallCount: 0,
            RobotCount: 0,
            TrackedFrameNumber: record.TrackedFrameNumber,
            TrackedFrameTimestampNs: record.TrackedFrameTimestampNs,
            SourceUuid: record.SourceUuid,
            SourceName: record.SourceName,
            SourceRole: TrackerPacketSnapshotRecord.NormalizeSourceRole(record.SourceRole),
            SourceLabel: TrackerPacketSnapshotRecord.NormalizeSourceLabel(
                record.SourceLabel,
                record.SourceName,
                record.SourceUuid,
                record.RemoteEndpoint,
                record.SourceRole),
            Balls: [],
            Robots: []);
    }

    /// <summary>
    /// log 一覧で使う短い summary 文字列へ変換する。
    /// </summary>
    public string ToDisplaySummary()
    {
        return FormattableString.Invariant(
            $"source={SourceLabel} role={SourceRole} frame={TrackedFrameNumber} balls={BallCount} robots={RobotCount}");
    }

    private static double ToMillimeters(double meters)
    {
        return meters * 1000.0;
    }
}

/// <summary>
/// raw tracker packet の ball 代表位置 summary。
/// </summary>
public sealed record TrackerPacketSnapshotBallSummary(
    int Index,
    double XMm,
    double YMm,
    double ZMm,
    float Visibility);

/// <summary>
/// raw tracker packet の robot 代表位置 summary。
/// </summary>
public sealed record TrackerPacketSnapshotRobotSummary(
    string Team,
    uint RobotId,
    double XMm,
    double YMm,
    float OrientationRad,
    float Visibility);
