using AtlTrack = ATL.Track;

namespace SharpSelecta.Core.Library;

public sealed record TrackTagEdits(
    string? Title,
    string? Artist,
    string? AlbumArtist,
    string? Album,
    string? Genre,
    string? Comment,
    int? Year,
    int? TrackNumber);

public static class TrackTagEditor
{
    // Writes through a temp copy in the same directory and renames it over the original, so a
    // failed save can never leave the user's file half-written.
    public static void Write(string filePath, TrackTagEdits edits)
    {
        var tempPath = Path.Combine(
            Path.GetDirectoryName(filePath) ?? "",
            $".{Path.GetFileNameWithoutExtension(filePath)}.{Guid.NewGuid():N}.tmp{Path.GetExtension(filePath)}");

        try
        {
            File.Copy(filePath, tempPath);

            var atlTrack = new AtlTrack(tempPath)
            {
                Title = edits.Title ?? "",
                Artist = edits.Artist ?? "",
                AlbumArtist = edits.AlbumArtist ?? "",
                Album = edits.Album ?? "",
                Genre = edits.Genre ?? "",
                Comment = edits.Comment ?? "",
                Year = edits.Year ?? 0,
                TrackNumber = edits.TrackNumber ?? 0,
            };

            if (!atlTrack.Save())
            {
                throw new IOException($"Could not write tags to '{filePath}'.");
            }

            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
