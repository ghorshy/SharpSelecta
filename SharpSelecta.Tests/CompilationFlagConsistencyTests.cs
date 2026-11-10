using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class CompilationFlagConsistencyTests
{
    private static Track Track(string fileName, string? album, string? albumArtist) =>
        new(TestPaths.Music(fileName), fileName) { Album = album, AlbumArtist = albumArtist };

    private static IReadOnlyList<InconsistentCompilationAlbum> Find(IEnumerable<Track> tracks, params string[] flaggedFileNames) =>
        CompilationFlagConsistency.FindInconsistent(tracks, path => flaggedFileNames.Contains(Path.GetFileName(path)));

    [Test]
    public async Task AlbumByOneArtist_WithOnlySomeTracksFlagged_IsReported()
    {
        var t5 = Track("05.m4a", "Biohzrd Dub Pack 2024", "Biohzrd");
        var t7 = Track("07.m4a", "Biohzrd Dub Pack 2024", "Biohzrd");
        var t8 = Track("08.m4a", "Biohzrd Dub Pack 2024", "Biohzrd");

        var result = Find([t5, t7, t8], "07.m4a", "08.m4a");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Album).IsEqualTo("Biohzrd Dub Pack 2024");
        await Assert.That(result[0].AlbumArtist).IsEqualTo("Biohzrd");
        await Assert.That(result[0].Flagged).IsEquivalentTo(new[] { t7, t8 });
        await Assert.That(result[0].Unflagged).IsEquivalentTo(new[] { t5 });
    }

    [Test]
    [Arguments(null)]
    [Arguments("  ")]
    public async Task AlbumWithoutAnAlbumArtist_IsLeftAlone(string? albumArtist)
    {
        var tracks = new[]
        {
            Track("01.mp3", "I Love Bassline", albumArtist),
            Track("02.mp3", "I Love Bassline", albumArtist),
        };

        await Assert.That(Find(tracks, "01.mp3")).IsEmpty();
    }

    [Test]
    public async Task TracksAreGroupedLikeTheAlbumGrid_TrimmedAndIgnoringCase()
    {
        var tracks = new[]
        {
            Track("01.m4a", "Actually", "Pet Shop Boys"),
            Track("02.m4a", " actually ", "PET SHOP BOYS"),
        };

        var result = Find(tracks, "02.m4a");

        await Assert.That(result.Count).IsEqualTo(1);
        await Assert.That(result[0].Flagged.Single().FilePath).IsEqualTo(TestPaths.Music("02.m4a"));
        await Assert.That(result[0].Unflagged.Single().FilePath).IsEqualTo(TestPaths.Music("01.m4a"));
    }

    [Test]
    public async Task FullyFlaggedAndUnflaggedAlbums_AreConsistent()
    {
        var tracks = new[]
        {
            Track("01.mp3", "Winter Weaponry Vol. 2", "Various Artists"),
            Track("02.mp3", "Winter Weaponry Vol. 2", "Various Artists"),
            Track("03.mp3", "Coool", "Anri"),
            Track("04.mp3", "Coool", "Anri"),
        };

        await Assert.That(Find(tracks, "01.mp3", "02.mp3")).IsEmpty();
    }
}
