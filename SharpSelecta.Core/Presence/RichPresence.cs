namespace SharpSelecta.Core.Presence;

// What the player is doing, as a "now listening" status for an external service (Discord).
// EndsAtUtc is null when the track's length isn't known.
public sealed record PresenceActivity(string Title, string Artists, string? Album, DateTimeOffset StartedAtUtc, DateTimeOffset? EndsAtUtc);

public interface IRichPresence
{
    void Show(PresenceActivity activity);

    void Clear();
}
