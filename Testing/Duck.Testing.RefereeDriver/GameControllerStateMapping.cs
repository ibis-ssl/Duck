using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Duck.Testing.RefereeDriver;

/// <summary>正規化した公開状態とUDP指令の対応を判定する境界。鮮度・世代の照合は別途行う。</summary>
internal static class GameControllerStateMapping
{
    private static readonly Regex GoDurationPart = new(
        @"(?<value>\d+(?:\.\d+)?)(?<unit>ns|us|µs|ms|s|m|h)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    internal static bool TryParseStage(string? name, out Referee.Types.Stage stage)
    {
        switch (name)
        {
            case "NORMAL_FIRST_HALF_PRE": stage = Referee.Types.Stage.NormalFirstHalfPre; return true;
            case "NORMAL_FIRST_HALF": stage = Referee.Types.Stage.NormalFirstHalf; return true;
            case "NORMAL_HALF_TIME": stage = Referee.Types.Stage.NormalHalfTime; return true;
            case "NORMAL_SECOND_HALF_PRE": stage = Referee.Types.Stage.NormalSecondHalfPre; return true;
            case "NORMAL_SECOND_HALF": stage = Referee.Types.Stage.NormalSecondHalf; return true;
            case "POST_GAME": stage = Referee.Types.Stage.PostGame; return true;
            default: stage = default; return false;
        }
    }

    /// <summary>公開APIの名前を明示変換し、欠落・未知・未対応・型不正なら観測値を作らない。</summary>
    internal static GameControllerMatchSnapshot? Normalize(JsonElement matchState)
    {
        if (matchState.ValueKind != JsonValueKind.Object)
            return null;
        var command = ReadTypeName(matchState, "command") switch
        {
            "HALT" => GameControllerCommandKind.Halt,
            "STOP" => GameControllerCommandKind.Stop,
            "KICKOFF" => GameControllerCommandKind.Kickoff,
            "NORMAL_START" => GameControllerCommandKind.NormalStart,
            "FORCE_START" => GameControllerCommandKind.ForceStart,
            _ => GameControllerCommandKind.Unknown,
        };
        var phase = ReadTypeName(matchState, "gameState") switch
        {
            "HALT" => GameControllerMatchPhase.Halt,
            "STOP" => GameControllerMatchPhase.Stop,
            "KICKOFF" => GameControllerMatchPhase.Kickoff,
            "RUNNING" => GameControllerMatchPhase.Running,
            _ => GameControllerMatchPhase.Unknown,
        };
        return command == GameControllerCommandKind.Unknown || phase == GameControllerMatchPhase.Unknown
            ? null
            : new(command, phase, ReadStageName(matchState), ReadDuration(matchState));
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
            (GameControllerCommandKind.Kickoff, GameControllerMatchPhase.Stop,
                Referee.Types.Command.PrepareKickoffBlue or Referee.Types.Command.PrepareKickoffYellow) => true,
            (GameControllerCommandKind.Kickoff, GameControllerMatchPhase.Kickoff,
                Referee.Types.Command.PrepareKickoffBlue or Referee.Types.Command.PrepareKickoffYellow) => true,
            (GameControllerCommandKind.NormalStart,
                GameControllerMatchPhase.Kickoff or GameControllerMatchPhase.Running,
                Referee.Types.Command.NormalStart) => true,
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

    private static string? ReadStageName(JsonElement matchState)
    {
        if (!matchState.TryGetProperty("stage", out var stage))
            return null;
        return stage.ValueKind == JsonValueKind.String
            ? stage.GetString()
            : stage.ValueKind == JsonValueKind.Object && stage.TryGetProperty("type", out var type) &&
                type.ValueKind == JsonValueKind.String
                    ? type.GetString()
                    : null;
    }

    private static TimeSpan? ReadDuration(JsonElement matchState)
    {
        if (!matchState.TryGetProperty("stageTimeLeft", out var value) ||
            value.ValueKind != JsonValueKind.String)
            return null;
        var text = value.GetString();
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var negative = text[0] == '-';
        var unsigned = negative ? text[1..] : text;
        var matches = GoDurationPart.Matches(unsigned);
        if (matches.Count == 0 || string.Concat(matches.Select(match => match.Value)) != unsigned)
            return null;

        double ticks = 0;
        foreach (Match match in matches)
        {
            if (!double.TryParse(match.Groups["value"].Value, CultureInfo.InvariantCulture, out var amount))
                return null;
            var ticksPerUnit = match.Groups["unit"].Value switch
            {
                "ns" => TimeSpan.TicksPerMillisecond / 1_000_000d,
                "us" or "µs" => TimeSpan.TicksPerMillisecond / 1_000d,
                "ms" => TimeSpan.TicksPerMillisecond,
                "s" => TimeSpan.TicksPerSecond,
                "m" => TimeSpan.TicksPerMinute,
                "h" => TimeSpan.TicksPerHour,
                _ => 0d,
            };
            ticks += amount * ticksPerUnit;
        }
        if (!double.IsFinite(ticks) || ticks > TimeSpan.MaxValue.Ticks)
            return null;
        var duration = TimeSpan.FromTicks((long)Math.Round(ticks));
        return negative ? -duration : duration;
    }
}
