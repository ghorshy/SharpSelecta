using AtlTrack = ATL.Track;

namespace SharpSelecta.Core.Library;

public static class MusicLibraryScanner
{
    internal static readonly string[] SupportedExtensions = [".mp3", ".flac", ".wav", ".m4a"];

    static MusicLibraryScanner()
    {
        // Otherwise ATL reports the file name as Title for untitled files, which the tag editor
        // would then show (and write back) as if it were a real tag.
        ATL.Settings.UseFileNameWhenNoTitle = false;
    }

    public static IReadOnlyList<Track> Scan(string folderPath)
    {
        return Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .AsParallel()
            .AsOrdered()
            .Select(ReadTrack)
            .ToList();
    }

    internal static Track ReadTrack(string path)
    {
        var fileName = Path.GetFileName(path);
        var fileType = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();

        try
        {
            var atlTrack = new AtlTrack(path);
            var title = NullIfEmpty(atlTrack.Title);
            var displayName = title ?? Path.GetFileNameWithoutExtension(path);

            return new Track(path, displayName)
            {
                // ATL.NET reports these as 0, not null, when a file has no such tag.
                TrackNumber = atlTrack.TrackNumber is > 0 ? atlTrack.TrackNumber : null,
                Title = title,
                Artist = NullIfEmpty(atlTrack.Artist),
                Album = NullIfEmpty(atlTrack.Album),
                AlbumArtist = NullIfEmpty(atlTrack.AlbumArtist),
                Genre = NullIfEmpty(atlTrack.Genre),
                Comment = NullIfEmpty(atlTrack.Comment),
                Year = atlTrack.Year is > 0 ? atlTrack.Year : null,
                Duration = TimeSpan.FromSeconds(atlTrack.Duration),
                SampleRate = (int)atlTrack.SampleRate,
                BitDepth = atlTrack.BitDepth,
                Bitrate = atlTrack.Bitrate,
                FileType = fileType,
            };
        }
        catch (Exception)
        {
            return new Track(path, fileName) { FileType = fileType };
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    public static Track? ReadTrackIfExists(string filePath) => File.Exists(filePath) ? ReadTrack(filePath) : null;

    // Embedded art wins; a cover file in the track's folder is the fallback.
    public static byte[]? LoadArtwork(string filePath)
    {
        try
        {
            var embedded = new AtlTrack(filePath).EmbeddedPictures.FirstOrDefault()?.PictureData;
            if (embedded is { Length: > 0 })
            {
                return embedded;
            }

            return CoverFile.Find(filePath) is { } coverPath ? File.ReadAllBytes(coverPath) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
