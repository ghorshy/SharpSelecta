using ATL;
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
    int? TrackNumber,
    int? DiscNumber = null);

public abstract record CoverArtEdit
{
    public sealed record Keep : CoverArtEdit;

    // Clears the embedded picture and deletes any cover file in the track's folder.
    public sealed record Remove : CoverArtEdit;

    // AsSeparateFile writes cover.jpg/png next to the track and strips the embedded picture (an
    // embedded one would otherwise win over the file); otherwise the image is embedded in the track.
    public sealed record Replace(byte[] ImageBytes, bool AsSeparateFile) : CoverArtEdit;
}

public static class TrackTagEditor
{
    public static bool IsSupportedCoverImage(byte[] imageBytes) => CoverFile.TryGetExtension(imageBytes, out _);

    // Writes a temp copy in the same directory and renames it over the original.
    // Null credits leave the file's credits as they are.
    public static void Write(string filePath, TrackTagEdits edits, CoverArtEdit? cover = null, TrackCredits? credits = null)
    {
        byte[]? coverFileBytes = null;
        WriteThroughTempCopy(filePath, atlTrack =>
        {
            atlTrack.Title = edits.Title ?? "";
            atlTrack.Artist = edits.Artist ?? "";
            atlTrack.AlbumArtist = edits.AlbumArtist ?? "";
            atlTrack.Album = edits.Album ?? "";
            atlTrack.Genre = edits.Genre ?? "";
            atlTrack.Comment = edits.Comment ?? "";
            atlTrack.Year = edits.Year ?? 0;
            atlTrack.TrackNumber = edits.TrackNumber ?? 0;
            atlTrack.DiscNumber = edits.DiscNumber ?? 0;

            if (credits is not null)
            {
                CreditFields.Apply(atlTrack, filePath, credits);
            }

            coverFileBytes = ApplyCover(atlTrack, cover ?? new CoverArtEdit.Keep());
        },
        // Cover file goes before the final rename: if it fails, the original track is untouched.
        () => ApplyCoverFile(filePath, cover ?? new CoverArtEdit.Keep(), coverFileBytes));
    }

    public static void WriteCompilationFlag(string filePath, bool isCompilation) =>
        WriteThroughTempCopy(filePath, atlTrack => CompilationFlag.Apply(atlTrack, filePath, isCompilation));

    private static void WriteThroughTempCopy(string filePath, Action<AtlTrack> edit, Action? beforeReplacing = null)
    {
        var tempPath = HiddenTempFile.PathNextTo(filePath, Path.GetExtension(filePath));

        AtlConfiguration.Ensure();

        try
        {
            File.Copy(filePath, tempPath);

            var atlTrack = new AtlTrack(tempPath);
            edit(atlTrack);

            if (!atlTrack.Save())
            {
                throw new IOException($"Could not write tags to '{filePath}'.");
            }

            beforeReplacing?.Invoke();

            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static byte[]? ApplyCover(AtlTrack atlTrack, CoverArtEdit cover)
    {
        switch (cover)
        {
            case CoverArtEdit.Remove:
                atlTrack.EmbeddedPictures.Clear();
                return null;
            case CoverArtEdit.Replace { AsSeparateFile: true } replace:
                CoverFile.ExtensionFor(replace.ImageBytes);
                atlTrack.EmbeddedPictures.Clear();
                return replace.ImageBytes;
            case CoverArtEdit.Replace replace:
                CoverFile.ExtensionFor(replace.ImageBytes);
                atlTrack.EmbeddedPictures.Clear();
                atlTrack.EmbeddedPictures.Add(PictureInfo.fromBinaryData(replace.ImageBytes, PictureInfo.PIC_TYPE.Front));
                return null;
            default:
                return null;
        }
    }

    private static void ApplyCoverFile(string trackFilePath, CoverArtEdit cover, byte[]? coverFileBytes)
    {
        if (cover is CoverArtEdit.Remove)
        {
            foreach (var existing in CoverFile.EnumerateAll(trackFilePath).ToList())
            {
                File.Delete(existing);
            }
        }
        else if (coverFileBytes is not null)
        {
            var directory = Path.GetDirectoryName(trackFilePath) ?? "";
            var coverPath = Path.Combine(directory, "cover" + CoverFile.ExtensionFor(coverFileBytes));
            var tempPath = Path.Combine(directory, $".cover.{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllBytes(tempPath, coverFileBytes);

                // A stale cover.png next to a fresh cover.jpg (or vice versa) would shadow or be
                // shadowed by it - the folder keeps exactly one cover file.
                foreach (var existing in CoverFile.EnumerateAll(trackFilePath).ToList())
                {
                    File.Delete(existing);
                }

                File.Move(tempPath, coverPath, overwrite: true);
            }
            finally
            {
                File.Delete(tempPath);
            }
        }
    }
}
