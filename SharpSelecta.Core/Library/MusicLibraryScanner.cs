using AtlTrack = ATL.Track;

namespace SharpSelecta.Core.Library;

public static class MusicLibraryScanner
{
    // internal, not private: LibraryIndexStore's Reconcile reuses this same enumeration/filter
    // logic rather than duplicating it.
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

    // internal, not private: LibraryIndexStore's Reconcile reuses this same ATL tag-read for
    // files it determines are new or changed, rather than duplicating the read/fallback logic.
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

    // Reconstructs a single Track directly from disk, bypassing a full folder Scan — used to
    // restore a saved queue on startup, where the file may not even live under a currently
    // configured library folder. Returns null instead of the private ReadTrack's filename-only
    // fallback when the file is simply gone, so a stale saved path is dropped rather than
    // resurrected as a bogus queue entry.
    public static Track? ReadTrackIfExists(string filePath) => File.Exists(filePath) ? ReadTrack(filePath) : null;

    // Not read during Scan — decoding embedded pictures for an entire library upfront would be
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
