using System.Collections.Generic;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;
using Tmds.DBus;

namespace SharpSelecta.App.Services.Mpris;

// Pure mapping helpers, kept separate from MprisRoot so they're testable without a live D-Bus
// connection.
public static class MprisMapping
{
    public static string PlaybackStatus(TransportState transportState, bool isPlaying) => transportState switch
    {
        TransportState.NoTrack => "Stopped",
        TransportState.Finished => "Stopped",
        _ => isPlaying ? "Playing" : "Paused",
    };

    public static IDictionary<string, object> BuildMetadata(Track? track)
    {
        if (track is null)
        {
            return new Dictionary<string, object>();
        }

        var metadata = new Dictionary<string, object>
        {
            ["mpris:trackid"] = TrackId(track),
            ["mpris:length"] = (long)track.Duration.TotalMicroseconds,
            ["xesam:title"] = !string.IsNullOrWhiteSpace(track.Title) ? track.Title : track.DisplayName,
        };

        if (!string.IsNullOrWhiteSpace(track.Artist))
        {
            metadata["xesam:artist"] = new[] { track.Artist };
        }

        if (!string.IsNullOrWhiteSpace(track.Album))
        {
            metadata["xesam:album"] = track.Album;
        }

        return metadata;
    }

    // The spec requires mpris:trackid even without a real TrackList interface - a hash of the file
    // path keeps it deterministic and a valid ObjectPath ([A-Za-z0-9_/] only). Only needs to stay
    // consistent within the current process's lifetime (used to detect a stale SetPosition call),
    // not across app restarts, so the default randomized-per-process string hash is fine here.
    public static ObjectPath TrackId(Track track) => new($"/org/sharpselecta/Track/{(uint)track.FilePath.GetHashCode()}");

    // CanGoNext/CanGoPrevious only depend on the queue having entries, independent of whether
    // anything is currently loaded (PlaybackQueue.CanGoNext works off CurrentIndex, which starts at
    // -1 until something's actually been played). CanPlay used to require hasCurrentTrack too,
    // which made it false whenever tracks were queued (e.g. via "Add to Queue") but nothing had been
    // played yet this session - playerctl checks CanPlay/CanPause before sending Play/PlayPause and
    // skips a player reporting false for them (confirmed against a real session bus with two fake
    // players differing only in this property), so it would fall through to a different MPRIS
    // player entirely for play-pause while Next/Previous still correctly landed on this one. True
    // here whenever there's *something* playable right now, matching what MprisRoot's Play/PlayPause
    // handlers actually do in that state (fall through to Next instead of silently no-op'ing).
    public static bool CanPlay(bool canResumeOrPause, bool hasCurrentTrack, bool canGoNext) =>
        canResumeOrPause || (!hasCurrentTrack && canGoNext);
}
