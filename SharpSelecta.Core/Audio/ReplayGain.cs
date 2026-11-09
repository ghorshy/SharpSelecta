using System.Globalization;
using SharpSelecta.Core.Library;
using AtlTrack = ATL.Track;

namespace SharpSelecta.Core.Audio;

public sealed record ReplayGainInfo(double? TrackGainDb, double? TrackPeak, double? AlbumGainDb, double? AlbumPeak)
{
    public static readonly ReplayGainInfo None = new(null, null, null, null);

    // Never stops a track from playing: a file whose tags can't be read just plays without normalization data.
    public static ReplayGainInfo Read(string filePath)
    {
        if (!File.Exists(filePath))
            return None;

        AtlConfiguration.Ensure();
        try
        {
            return FromTags(new AtlTrack(filePath).AdditionalFields);
        }
        catch (Exception)
        {
            return None;
        }
    }

    // Keys as ATL reports them: upper case for Vorbis comments and ID3 TXXX, lower case for MP4 freeform atoms.
    public static ReplayGainInfo FromTags(IEnumerable<KeyValuePair<string, string>> tags)
    {
        var byKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in tags)
        {
            byKey.TryAdd(key, value);
        }

        return new ReplayGainInfo(
            Number(byKey, "REPLAYGAIN_TRACK_GAIN"), Number(byKey, "REPLAYGAIN_TRACK_PEAK"),
            Number(byKey, "REPLAYGAIN_ALBUM_GAIN"), Number(byKey, "REPLAYGAIN_ALBUM_PEAK"));
    }

    // untaggedGainDb: for a track with no gain tags, which would otherwise jump out next to tagged (turned down) ones.
    public float LinearGain(VolumeNormalization mode, double untaggedGainDb)
    {
        if (mode == VolumeNormalization.Off)
            return 1f;

        var track = (GainDb: TrackGainDb, Peak: TrackPeak);
        var album = (GainDb: AlbumGainDb, Peak: AlbumPeak);
        var (preferred, other) = mode == VolumeNormalization.Album ? (album, track) : (track, album);
        var (gainDb, peak) = preferred.GainDb is null && other.GainDb is not null
            ? (other.GainDb, other.Peak ?? preferred.Peak)
            : (preferred.GainDb, preferred.Peak ?? other.Peak);

        var linear = Math.Pow(10, (gainDb ?? untaggedGainDb) / 20);
        if (peak is > 0 && linear * peak > 1)
        {
            linear = 1 / peak.Value;
        }

        return (float)linear;
    }

    private static double? Number(Dictionary<string, string> tags, string key)
    {
        if (!tags.TryGetValue(key, out var value))
            return null;

        var text = value.Trim();
        if (text.EndsWith("dB", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^2].TrimEnd();
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
            ? number
            : null;
    }
}
