namespace SharpSelecta.Core.Presence;

// What the player is doing, as a "now listening" status for an external service (Discord). A null
// field isn't shown; no StartedAtUtc means no progress, EndsAtUtc is also null when the length isn't known.
public sealed record PresenceActivity(string Title, string? Artists, string? Album, DateTimeOffset? StartedAtUtc, DateTimeOffset? EndsAtUtc);

public interface IRichPresence
{
    void Show(PresenceActivity activity);

    void Clear();
}
