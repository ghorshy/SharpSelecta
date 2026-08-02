using System.Collections.Generic;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;
using Tmds.DBus;

namespace SharpSelecta.App.Services.Mpris;

public static class MprisMapping
{
    public static string PlaybackStatus(TransportState transportState, bool isPlaying) => transportState switch
    {
        TransportState.NoTrack => "Stopped",
        TransportState.Finished => "Stopped",
        _ => isPlaying ? "Playing" : "Paused",
    };

    public static IDictionary<string, object> BuildMetadata(Track? track, long? heartbeatTick = null)
    {
        var metadata = new Dictionary<string, object>();

        if (track is not null)
        {
            metadata["mpris:trackid"] = TrackId(track);
            metadata["mpris:length"] = (long)track.Duration.TotalMicroseconds;
            metadata["xesam:title"] = track.DisplayName;

            if (!string.IsNullOrWhiteSpace(track.Artist))
            {
                metadata["xesam:artist"] = new[] { track.Artist };
            }

            if (!string.IsNullOrWhiteSpace(track.Album))
            {
                metadata["xesam:album"] = track.Album;
            }
        }

        if (heartbeatTick is { } tick)
        {
            metadata["x-sharpselecta:heartbeat"] = tick;
        }

        return metadata;
    }

    public static ObjectPath TrackId(Track track) => new($"/org/sharpselecta/Track/{(uint)track.FilePath.GetHashCode()}");

    public static bool CanPlay(bool canResumeOrPause, bool hasCurrentTrack, bool canGoNext) =>
        canResumeOrPause || (!hasCurrentTrack && canGoNext);
}
