using DiscordRPC;
using SharpSelecta.Core.Presence;

namespace SharpSelecta.Integrations.Discord;

// Talks to the running Discord client over its local IPC socket/pipe; if Discord isn't running (yet)
// the client keeps trying in the background, so starting it later still picks the status up.
public sealed class DiscordPresenceService(string applicationId) : IRichPresence, IDisposable
{
    // Discord rejects a text field that is shorter than 2 or longer than 128 characters.
    private const int MaxTextLength = 128;

    // The key of the logo uploaded under "Rich Presence > Art Assets" for this application.
    private const string LogoAssetKey = "logo";

    private DiscordRpcClient? _client;

    public void Show(PresenceActivity activity)
    {
        EnsureClient().SetPresence(new RichPresence
        {
            Type = ActivityType.Listening,
            Details = Text(activity.Title),
            State = Text(activity.Artists),
            Assets = new Assets { LargeImageKey = LogoAssetKey, LargeImageText = Text(activity.Album) },
            Timestamps = new Timestamps { Start = activity.StartedAtUtc.UtcDateTime, End = activity.EndsAtUtc?.UtcDateTime },
        });
    }

    public void Clear() => _client?.ClearPresence();

    private DiscordRpcClient EnsureClient()
    {
        if (_client is not null)
            return _client;

        _client = new DiscordRpcClient(applicationId) { SkipIdenticalPresence = true };
        _client.Initialize();
        return _client;
    }

    private static string? Text(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.Length > MaxTextLength)
        {
            trimmed = trimmed[..(MaxTextLength - 1)] + "…";
        }

        return trimmed.Length < 2 ? trimmed + " " : trimmed;
    }

    public void Dispose()
    {
        _client?.ClearPresence();
        _client?.Dispose();
        _client = null;
    }
}
