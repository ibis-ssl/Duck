using Tracker.Server.Tracking;

namespace Tracker.Server.Components.Vision;

public sealed record TrackerProfileControlViewState(
    string ActiveProfileName,
    IReadOnlyList<TrackerProfileOptionViewState> Profiles)
{
    public bool CanSwitch => Profiles.Count > 1;

    public static TrackerProfileControlViewState FromOptions(TrackerOptions options, TrackedSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(snapshot);

        var activeProfileName = string.IsNullOrWhiteSpace(snapshot.ActiveProfileName)
            ? options.ActiveProfileName
            : snapshot.ActiveProfileName;

        var profiles = options.Profiles.Keys
            .Select(name => new TrackerProfileOptionViewState(
                name,
                string.Equals(name, activeProfileName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (profiles.Count == 0)
        {
            profiles.Add(new TrackerProfileOptionViewState(activeProfileName, true));
        }
        else if (!profiles.Any(profile => profile.IsActive))
        {
            profiles.Add(new TrackerProfileOptionViewState(activeProfileName, true));
        }

        return new TrackerProfileControlViewState(activeProfileName, profiles);
    }
}

public sealed record TrackerProfileOptionViewState(
    string Name,
    bool IsActive);
