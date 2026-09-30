using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class CoverArtTests
{
    private static readonly TrackTagEdits NoEdits = new("T", "A", null, "Al", null, null, null, null);

    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static string CopyTrack(string fixtureName, out DirectoryInfo dir)
    {
        dir = Directory.CreateTempSubdirectory("sharpselecta-cover-tests-");
        var path = Path.Combine(dir.FullName, fixtureName);
        File.WriteAllBytes(path, Fixture(fixtureName));
        return path;
    }

    [Test]
    public async Task LoadArtwork_WithoutEmbeddedArt_FallsBackToCoverFileInTheFolder()
    {
        var path = CopyTrack("untagged-track.mp3", out var dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir.FullName, "Cover.JPG"), Fixture("cover-red.jpg"));

            await Assert.That(MusicLibraryScanner.LoadArtwork(path)).IsEquivalentTo(Fixture("cover-red.jpg"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task LoadArtwork_PrefersEmbeddedArtOverACoverFile()
    {
        var path = CopyTrack("tagged-track-with-artwork.mp3", out var dir);
        try
        {
            var embedded = MusicLibraryScanner.LoadArtwork(path);
            File.WriteAllBytes(Path.Combine(dir.FullName, "cover.jpg"), Fixture("cover-red.jpg"));

            await Assert.That(MusicLibraryScanner.LoadArtwork(path)).IsEquivalentTo(embedded!);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task LoadArtwork_WithNoEmbeddedArtAndNoCoverFile_ReturnsNull()
    {
        var path = CopyTrack("untagged-track.mp3", out var dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir.FullName, "notes.jpg"), Fixture("cover-red.jpg"));

            await Assert.That(MusicLibraryScanner.LoadArtwork(path)).IsNull();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Write_ReplaceEmbedded_EmbedsTheImageAndCreatesNoCoverFile()
    {
        var path = CopyTrack("untagged-track.mp3", out var dir);
        try
        {
            TrackTagEditor.Write(path, NoEdits, new CoverArtEdit.Replace(Fixture("cover-red.jpg"), AsSeparateFile: false));

            await Assert.That(MusicLibraryScanner.LoadArtwork(path)).IsEquivalentTo(Fixture("cover-red.jpg"));
            await Assert.That(Directory.GetFiles(dir.FullName).Length).IsEqualTo(1);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    [Arguments("cover-red.jpg", "cover.jpg")]
    [Arguments("cover-blue.png", "cover.png")]
    public async Task Write_ReplaceAsSeparateFile_WritesTheCoverFileAndStripsEmbeddedArt(string imageFixture, string expectedFileName)
    {
        var path = CopyTrack("tagged-track-with-artwork.mp3", out var dir);
        try
        {
            TrackTagEditor.Write(path, NoEdits, new CoverArtEdit.Replace(Fixture(imageFixture), AsSeparateFile: true));

            await Assert.That(File.ReadAllBytes(Path.Combine(dir.FullName, expectedFileName))).IsEquivalentTo(Fixture(imageFixture));
            await Assert.That(new ATL.Track(path).EmbeddedPictures.Count).IsEqualTo(0);
            await Assert.That(MusicLibraryScanner.LoadArtwork(path)).IsEquivalentTo(Fixture(imageFixture));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Write_ReplaceAsSeparateFile_ReplacesAStaleCoverFileOfAnotherExtension()
    {
        var path = CopyTrack("untagged-track.mp3", out var dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir.FullName, "cover.png"), Fixture("cover-blue.png"));

            TrackTagEditor.Write(path, NoEdits, new CoverArtEdit.Replace(Fixture("cover-red.jpg"), AsSeparateFile: true));

            await Assert.That(File.Exists(Path.Combine(dir.FullName, "cover.png"))).IsFalse();
            await Assert.That(File.Exists(Path.Combine(dir.FullName, "cover.jpg"))).IsTrue();
            await Assert.That(Directory.GetFiles(dir.FullName).Length).IsEqualTo(2); // track + cover.jpg
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Write_Remove_ClearsEmbeddedArtAndDeletesCoverFiles()
    {
        var path = CopyTrack("tagged-track-with-artwork.mp3", out var dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir.FullName, "cover.jpg"), Fixture("cover-red.jpg"));

            TrackTagEditor.Write(path, NoEdits, new CoverArtEdit.Remove());

            await Assert.That(MusicLibraryScanner.LoadArtwork(path)).IsNull();
            await Assert.That(File.Exists(Path.Combine(dir.FullName, "cover.jpg"))).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Write_Keep_LeavesEmbeddedArtAlone()
    {
        var path = CopyTrack("tagged-track-with-artwork.mp3", out var dir);
        try
        {
            var before = MusicLibraryScanner.LoadArtwork(path);

            TrackTagEditor.Write(path, NoEdits, new CoverArtEdit.Keep());

            await Assert.That(MusicLibraryScanner.LoadArtwork(path)).IsEquivalentTo(before!);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Write_ReplaceWithANonImage_ThrowsAndLeavesTheTrackUntouched()
    {
        var path = CopyTrack("untagged-track.mp3", out var dir);
        try
        {
            var before = File.ReadAllBytes(path);

            await Assert.That(() => TrackTagEditor.Write(path, NoEdits, new CoverArtEdit.Replace([1, 2, 3], AsSeparateFile: false)))
                .Throws<ArgumentException>();

            await Assert.That(File.ReadAllBytes(path)).IsEquivalentTo(before);
            await Assert.That(Directory.GetFiles(dir.FullName).Length).IsEqualTo(1);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
