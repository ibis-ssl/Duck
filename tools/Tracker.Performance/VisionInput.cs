namespace Tracker.Performance;

/// <summary>
/// 記録に含まれる SSL-Vision 入力とその受信時刻。
/// </summary>
/// <param name="ReceiverTimestampNs">ログの受信時刻（Unix 時刻のナノ秒）。</param>
/// <param name="Payload">SSL_WrapperPacket の記録バイト列。</param>
internal sealed record VisionInput(long ReceiverTimestampNs, byte[] Payload);
