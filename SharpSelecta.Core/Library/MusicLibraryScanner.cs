using System.Collections.Concurrent;
using AtlTrack = ATL.Track;

namespace SharpSelecta.Core.Library;

public static class MusicLibraryScanner
{
    private static readonly string[] SupportedExtensions = [".mp3", ".flac", ".wav", ".m4a"];

    private static readonly ConcurrentDictionary<string, byte[]?> ArtworkCache = new();

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
                // ATL.NET's Year is nullable in theory but reports 0 rather than null when a file
                // has no year tag — normalize that to null so it displays as blank, not "0".
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
            // One unreadable/corrupt file shouldn't stop the whole folder scan — fall back to a
            // filename-only entry instead.
            return new Track(path, fileName) { FileType = fileType };
        }
    }

    // Not read during Scan — decoding embedded pictures for an entire library upfront would be
    // wasteful. Called instead when a track is actually loaded for playback.
    public static byte[]? LoadArtwork(string filePath) =>
        ArtworkCache.GetOrAdd(filePath, static path =>
        {
            try
            {
                return new AtlTrack(path).EmbeddedPictures.FirstOrDefault()?.PictureData;
            }
            catch (Exception)
            {
                return null;
            }
        });
}
