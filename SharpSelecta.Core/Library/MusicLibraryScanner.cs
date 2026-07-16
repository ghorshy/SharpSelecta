using AtlTrack = ATL.Track;

namespace SharpSelecta.Core.Library;

public static class MusicLibraryScanner
{
    private static readonly string[] SupportedExtensions = [".mp3", ".flac", ".wav", ".m4a"];

    public static IReadOnlyList<Track> Scan(string folderPath)
    {
        return Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(ReadTrack)
            .ToList();
    }

    private static Track ReadTrack(string path)
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
                Year = atlTrack.Year,
                Duration = TimeSpan.FromSeconds(atlTrack.Duration),
                SampleRate = (int)atlTrack.SampleRate,
                BitDepth = atlTrack.BitDepth,
                Bitrate = atlTrack.Bitrate,
                FileType = fileType,
            };
        }
        catch (Exception)
        {
            // One unreadable/corrupt file shouldn't stop the whole folder scan — fall back to a
            // filename-only entry instead.
            return new Track(path, fileName) { FileType = fileType };
        }
    }
}
