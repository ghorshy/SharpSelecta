using AtlTrack = ATL.Track;

namespace SharpSelecta.Core.Library;

public static class MusicLibraryScanner
{
    private static readonly string[] SupportedExtensions = [".mp3", ".flac", ".wav", ".m4a", ".ogg", ".aiff", ".aif"];

    public static IEnumerable<string> FindAudioFiles(string folderPath) =>
        Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

    internal static Track ReadTrack(string path)
    {
        AtlConfiguration.Ensure();
        var fileName = Path.GetFileName(path);
        var fileType = FileTypeOf(path);

        try
        {
            var atlTrack = new AtlTrack(path);
            var title = TagText.NullIfBlank(atlTrack.Title);
            var displayName = title ?? Path.GetFileNameWithoutExtension(path);

            return new Track(path, displayName)
            {
                // ATL.NET reports these as 0, not null, when a file has no such tag.
                TrackNumber = atlTrack.TrackNumber is > 0 ? atlTrack.TrackNumber : null,
                DiscNumber = atlTrack.DiscNumber is > 0 ? atlTrack.DiscNumber : null,
                Title = title,
                Artist = TagText.NullIfBlank(atlTrack.Artist),
                Album = TagText.NullIfBlank(atlTrack.Album),
                AlbumArtist = TagText.NullIfBlank(atlTrack.AlbumArtist),
                Genre = TagText.NullIfBlank(atlTrack.Genre),
                Comment = TagText.NullIfBlank(atlTrack.Comment),
                Year = atlTrack.Year is > 0 ? atlTrack.Year : null,
                Duration = TimeSpan.FromSeconds(atlTrack.Duration),
                SampleRate = (int)atlTrack.SampleRate,
                BitDepth = atlTrack.BitDepth,
                Bitrate = atlTrack.Bitrate,
                FileType = fileType,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new Track(path, fileName) { FileType = fileType };
        }
    }

    private static AudioFileType FileTypeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".mp3" => AudioFileType.Mp3,
        ".flac" => AudioFileType.Flac,
        ".wav" => AudioFileType.Wav,
        ".ogg" => AudioFileType.Ogg,
        ".aiff" or ".aif" => AudioFileType.Aiff,
        ".m4a" => Mp4Codec.ReadAudioCodec(path) switch
        {
            Mp4Codec.Alac => AudioFileType.M4aAlac,
            Mp4Codec.Aac => AudioFileType.M4aAac,
            _ => AudioFileType.M4a,
        },
        _ => AudioFileType.Unknown,
    };

    public static Track? ReadTrackIfExists(string filePath) => File.Exists(filePath) ? ReadTrack(filePath) : null;

    public sealed record ArtworkResult(byte[] Bytes, bool IsSeparateFile);

    public static byte[]? LoadArtwork(string filePath) => LoadArtworkWithSource(filePath)?.Bytes;

    // Embedded art wins; a cover file in the track's folder is the fallback.
    public static ArtworkResult? LoadArtworkWithSource(string filePath)
    {
        AtlConfiguration.Ensure();

        try
        {
            var embedded = new AtlTrack(filePath).EmbeddedPictures.FirstOrDefault()?.PictureData;
            if (embedded is { Length: > 0 })
            {
                return new ArtworkResult(embedded, IsSeparateFile: false);
            }

            return CoverFile.Find(filePath) is { } coverPath
                ? new ArtworkResult(File.ReadAllBytes(coverPath), IsSeparateFile: true)
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static bool HasCoverFile(string trackFilePath) => CoverFile.Find(trackFilePath) is not null;
}
