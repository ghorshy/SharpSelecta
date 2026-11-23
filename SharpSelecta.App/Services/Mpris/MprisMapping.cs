using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;
using Tmds.DBus.Protocol;

namespace SharpSelecta.App.Services.Mpris;

public static class MprisMapping
{
    public static string PlaybackStatus(TransportState transportState, bool isPlaying) => transportState switch
    {
        TransportState.NoTrack => "Stopped",
        TransportState.Finished => "Stopped",
        _ => isPlaying ? "Playing" : "Paused",
    };

    public static Dictionary<string, VariantValue> BuildMetadata(Track? track, long? heartbeatTick = null)
    {
        var metadata = new Dictionary<string, VariantValue>();

        if (track is not null)
        {
            metadata["mpris:trackid"] = VariantValue.ObjectPath(TrackId(track));
            metadata["mpris:length"] = VariantValue.Int64((long)track.Duration.TotalMicroseconds);
            metadata["xesam:title"] = VariantValue.String(track.DisplayName);

            // xesam:artist is a list, so every artist of the track goes in separately.
            var artists = ArtistList.Split(track.Artist);
            if (artists.Count > 0)
            {
                metadata["xesam:artist"] = VariantValue.Array(artists.ToArray());
            }

            if (!string.IsNullOrWhiteSpace(track.Album))
            {
                metadata["xesam:album"] = VariantValue.String(track.Album);
            }
        }

        if (heartbeatTick is { } tick)
        {
            metadata["x-sharpselecta:heartbeat"] = VariantValue.Int64(tick);
        }

        return metadata;
    }

    public static ObjectPath TrackId(Track track) => new($"/org/sharpselecta/Track/{(uint)track.FilePath.GetHashCode()}");
}
