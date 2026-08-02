using AtlTrack = ATL.Track;

namespace SharpSelecta.Core.Library;

public static class MusicLibraryScanner
{
    internal static readonly string[] SupportedExtensions = [".mp3", ".flac", ".wav", ".m4a"];

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
            var displayName = !string.IsNullOrWhiteSpace(atlTrack.Title) ? atlTrack.Title : fileName;

            return new Track(path, displayName)
            {
                TrackNumber = atlTrack.TrackNumber,
                Title = atlTrack.Title,
                Artist = atlTrack.Artist,
                Album = atlTrack.Album,
                AlbumArtist = atlTrack.AlbumArtist,
                // ATL.NET reports Year as 0, not null, when a file has no year tag.
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

    public static Track? ReadTrackIfExists(string filePath) => File.Exists(filePath) ? ReadTrack(filePath) : null;

    public static byte[]? LoadArtwork(string filePath)
    {
        try
        {
            return new AtlTrack(filePath).EmbeddedPictures.FirstOrDefault()?.PictureData;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
