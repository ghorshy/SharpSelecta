using AtlTrack = ATL.Track;

namespace SharpSelecta.Core.Library;

// The iTunes "part of a compilation" flag. ATL has no property for it, only each format's own field:
// cpil (MP4), TCMP (ID3v2: MP3/WAV/AIFF), COMPILATION (Vorbis comments: FLAC/OGG).
public static class CompilationFlag
{
    private const string Mp4Key = "cpil";
    private const string Id3Key = "TCMP";
    private const string VorbisKey = "COMPILATION";

    private static readonly string[] AllKeys = [Mp4Key, Id3Key, VorbisKey];

    public static bool IsSet(string filePath)
    {
        AtlConfiguration.Ensure();

        try
        {
            return IsSet(new AtlTrack(filePath));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    internal static bool IsSet(AtlTrack atlTrack) =>
        atlTrack.AdditionalFields.Any(f => IsFlagKey(f.Key) && IsTrue(f.Value));

    internal static void Apply(AtlTrack atlTrack, string filePath, bool isCompilation)
    {
        foreach (var existing in atlTrack.AdditionalFields.Keys.Where(IsFlagKey).ToList())
        {
            atlTrack.AdditionalFields.Remove(existing);
        }

        if (isCompilation)
        {
            atlTrack.AdditionalFields[KeyFor(filePath)] = "1";
        }
    }

    private static string KeyFor(string filePath) => Path.GetExtension(filePath).ToLowerInvariant() switch
    {
        ".m4a" => Mp4Key,
        ".flac" or ".ogg" => VorbisKey,
        _ => Id3Key,
    };

    private static bool IsFlagKey(string key) => AllKeys.Contains(key, StringComparer.OrdinalIgnoreCase);

    private static bool IsTrue(string? value) =>
        TagText.NullIfBlank(value) is { } text && text != "0" && !text.Equals("false", StringComparison.OrdinalIgnoreCase);
}
