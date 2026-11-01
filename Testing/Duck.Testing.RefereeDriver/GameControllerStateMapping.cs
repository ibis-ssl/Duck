using System.Text.Json;

namespace Duck.Testing.RefereeDriver;

/// <summary>正規化した公開状態とUDP指令の対応を判定する境界。鮮度・世代の照合は別途行う。</summary>
internal static class GameControllerStateMapping
{
    /// <summary>公開APIの名前を明示変換し、欠落・未知・未対応・型不正なら観測値を作らない。</summary>
    internal static GameControllerMatchSnapshot? Normalize(JsonElement matchState)
    {
        if (matchState.ValueKind != JsonValueKind.Object)
            return null;
        var command = ReadTypeName(matchState, "command") switch
        {
            "HALT" => GameControllerCommandKind.Halt,
            "STOP" => GameControllerCommandKind.Stop,
            "NORMAL_START" => GameControllerCommandKind.NormalStart,
            "FORCE_START" => GameControllerCommandKind.ForceStart,
            _ => GameControllerCommandKind.Unknown,
        };
        var phase = ReadTypeName(matchState, "gameState") switch
        {
            "HALT" => GameControllerMatchPhase.Halt,
            "STOP" => GameControllerMatchPhase.Stop,
            "RUNNING" => GameControllerMatchPhase.Running,
            _ => GameControllerMatchPhase.Unknown,
        };
        return command == GameControllerCommandKind.Unknown || phase == GameControllerMatchPhase.Unknown
            ? null
            : new(command, phase);
    }

    /// <summary>指令と試合状態がともに対応する組だけを一致とし、列挙値の数値を相互変換しない。</summary>
    internal static bool MatchesUdpCommand(
        GameControllerMatchSnapshot? api,
        Referee.Types.Command udpCommand)
    {
        if (api is null)
            return false;
        return (api.Command, api.Phase, udpCommand) switch
        {
            (GameControllerCommandKind.Halt, GameControllerMatchPhase.Halt, Referee.Types.Command.Halt) => true,
            (GameControllerCommandKind.Stop, GameControllerMatchPhase.Stop, Referee.Types.Command.Stop) => true,
            (GameControllerCommandKind.NormalStart, GameControllerMatchPhase.Running, Referee.Types.Command.NormalStart) => true,
            (GameControllerCommandKind.ForceStart, GameControllerMatchPhase.Running, Referee.Types.Command.ForceStart) => true,
            _ => false,
        };
    }

    /// <summary>対象の状態オブジェクトに文字列のtypeが明示された場合だけ名前を読む。</summary>
    private static string? ReadTypeName(JsonElement matchState, string propertyName) =>
        matchState.TryGetProperty(propertyName, out var state) &&
        state.ValueKind == JsonValueKind.Object &&
        state.TryGetProperty("type", out var type) &&
        type.ValueKind == JsonValueKind.String
            ? type.GetString()
            : null;
}
