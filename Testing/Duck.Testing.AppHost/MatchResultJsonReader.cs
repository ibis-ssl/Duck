using System.Text.Json;

namespace Duck.Testing.AppHost;

internal static class MatchResultJsonReader
{
    internal static int? ReadInt32(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var parsed)
            ? parsed
            : null;
}
