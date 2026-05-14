namespace Tracker.DebugHost.Components.Pages;

/// <summary>
/// profile settings modal に表示する profile 名、設定 JSON、解決済み設定 JSON。
/// </summary>
internal sealed record DiagnosticsProfileMetadataView(
    string ProfileName,
    string ConfiguredProfileJson,
    string ResolvedSettingsJson);
