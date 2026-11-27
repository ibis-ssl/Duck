namespace Duck.Testing.RefereeDriver;

/// <summary>正規化した公開状態とUDP指令の対応を判定する境界。鮮度・世代の照合は別途行う。</summary>
internal static class GameControllerStateMapping
{
    /// <summary>対応表を検証する先行試験用の未実装入口。現時点では一致成功を返さない。</summary>
    internal static bool MatchesUdpCommand(
        GameControllerMatchSnapshot? api,
        Referee.Types.Command udpCommand) => false;
}
