using AtlTrack = ATL.Track;

namespace SharpSelecta.Core.Library;

public enum CreditRole
{
    Artist,
    Remixer,
    Composer,
    Conductor,
    Lyricist,
}

public sealed record CreditEntry(CreditRole Role, string Name);

public sealed record TrackCredits(string? Remixer, string? Composer, string? Conductor, string? Lyricist)
{
    public static readonly TrackCredits None = new(null, null, null, null);

    // Remixer has no native tag, so it uses each format's conventional free-form field (see CreditFields).
    public static TrackCredits Read(string filePath)
    {
        AtlConfiguration.Ensure();

        try
        {
            var atlTrack = new AtlTrack(filePath);
            return new TrackCredits(
                CreditFields.ReadRemixer(atlTrack, filePath),
                CreditFields.NullIfBlank(atlTrack.Composer),
                CreditFields.NullIfBlank(atlTrack.Conductor),
                CreditFields.NullIfBlank(atlTrack.Lyricist));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return None;
        }
    }
}

internal static class CreditFields
{
    private const string Id3RemixerFrame = "TPE4";
    private const string FreeFormRemixerField = "REMIXER";

    public static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // MP3/WAV carry ID3v2, whose own "remixed by" frame is TPE4; FLAC (Vorbis comments) and M4A use a
    // REMIXER field. The other name is also checked when reading, for files tagged by other tools.
    private static (string Preferred, string Alternate) RemixerKeys(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() is ".mp3" or ".wav" or ".aiff" or ".aif"
            ? (Id3RemixerFrame, FreeFormRemixerField)
            : (FreeFormRemixerField, Id3RemixerFrame);

    public static string? ReadRemixer(AtlTrack atlTrack, string filePath)
    {
        var (preferred, alternate) = RemixerKeys(filePath);
        foreach (var key in new[] { preferred, alternate })
        {
            var match = atlTrack.AdditionalFields.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase));
            if (NullIfBlank(match.Value) is { } value)
            {
                return value;
            }
        }

        return null;
    }

    public static void Apply(AtlTrack atlTrack, string filePath, TrackCredits credits)
    {
        atlTrack.Composer = credits.Composer ?? "";
        atlTrack.Conductor = credits.Conductor ?? "";
        atlTrack.Lyricist = credits.Lyricist ?? "";

        // Drop whichever spelling is there (any case) so a cleared or changed remixer can't linger
        // under the other name, then write the one this format's convention uses.
        var (preferred, alternate) = RemixerKeys(filePath);
        foreach (var existing in atlTrack.AdditionalFields.Keys
                     .Where(k => string.Equals(k, preferred, StringComparison.OrdinalIgnoreCase)
                              || string.Equals(k, alternate, StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            atlTrack.AdditionalFields.Remove(existing);
        }

        if (NullIfBlank(credits.Remixer) is { } remixer)
        {
            atlTrack.AdditionalFields[preferred] = remixer;
        }
    }
}
