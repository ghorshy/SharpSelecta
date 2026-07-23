using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class MusicLibraryScannerTests
{
    [Test]
    public async Task Scan_FindsSupportedFilesRecursivelyAndIgnoresOthers()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            var albumDir = Directory.CreateDirectory(Path.Combine(root.FullName, "Artist", "Album"));
            File.WriteAllBytes(Path.Combine(root.FullName, "top-level.mp3"), []);
            File.WriteAllBytes(Path.Combine(albumDir.FullName, "01 - track.flac"), []);
            File.WriteAllBytes(Path.Combine(albumDir.FullName, "cover.jpg"), []);

            var tracks = MusicLibraryScanner.Scan(root.FullName);

            // Tag content isn't the concern here (these are empty dummy files) — just that
            // recursive scanning finds the right files and ignores non-audio ones.
            await Assert.That(tracks.Count).IsEqualTo(2);
            await Assert.That(tracks.Select(t => t.FilePath)).Contains(Path.Combine(root.FullName, "top-level.mp3"));
            await Assert.That(tracks.Select(t => t.FilePath)).Contains(Path.Combine(albumDir.FullName, "01 - track.flac"));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Scan_ReturnsFilesOrderedByPathDespiteParallelReads()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            string[] names = ["charlie.mp3", "alpha.flac", "echo.wav", "bravo.m4a", "delta.mp3"];
            foreach (var name in names)
            {
                File.WriteAllBytes(Path.Combine(root.FullName, name), []);
            }

            var tracks = MusicLibraryScanner.Scan(root.FullName);

            var expectedOrder = names
                .Select(name => Path.Combine(root.FullName, name))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            await Assert.That(tracks.Select(t => t.FilePath)).IsEquivalentTo(expectedOrder);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Scan_OnEmptyFolder_ReturnsEmptyList()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            var tracks = MusicLibraryScanner.Scan(root.FullName);

            await Assert.That(tracks).IsEmpty();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Scan_OnAnUnreadableFile_FallsBackToFilenameOnlyInsteadOfThrowing()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            File.WriteAllBytes(Path.Combine(root.FullName, "not-really-audio.wav"), [1, 2, 3]);

            var tracks = MusicLibraryScanner.Scan(root.FullName);

            await Assert.That(tracks.Count).IsEqualTo(1);
            await Assert.That(tracks[0].FileType).IsEqualTo("WAV");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Scan_ReadsTagsAndAudioPropertiesFromARealFile()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3");
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            File.Copy(fixturePath, trackPath);

            var tracks = MusicLibraryScanner.Scan(root.FullName);

            await Assert.That(tracks.Count).IsEqualTo(1);
            var track = tracks[0];
            await Assert.That(track.Title).IsEqualTo("Test Song");
            await Assert.That(track.Artist).IsEqualTo("Test Artist");
            await Assert.That(track.Album).IsEqualTo("Test Album");
            await Assert.That(track.Year).IsEqualTo(2024);
            await Assert.That(track.TrackNumber).IsEqualTo(3);
            await Assert.That(track.SampleRate).IsEqualTo(44100);
            await Assert.That(track.FileType).IsEqualTo("MP3");
            await Assert.That(track.DisplayName).IsEqualTo("Test Song");
            await Assert.That(track.Duration.TotalSeconds).IsGreaterThan(0);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Scan_WithNoYearTag_LeavesYearNullInsteadOfZero()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track-no-year.mp3");
            var trackPath = Path.Combine(root.FullName, "tagged-track-no-year.mp3");
            File.Copy(fixturePath, trackPath);

            var tracks = MusicLibraryScanner.Scan(root.FullName);

            await Assert.That(tracks.Count).IsEqualTo(1);
            await Assert.That(tracks[0].Title).IsEqualTo("No Year Song");
            await Assert.That(tracks[0].Year).IsNull();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task LoadArtwork_WhenFileHasAnEmbeddedPicture_ReturnsItsBytes()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track-with-artwork.mp3");

        var artwork = MusicLibraryScanner.LoadArtwork(fixturePath);

        await Assert.That(artwork).IsNotNull();
        await Assert.That(artwork!.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task LoadArtwork_WhenFileHasNoEmbeddedPicture_ReturnsNull()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3");

        var artwork = MusicLibraryScanner.LoadArtwork(fixturePath);

        await Assert.That(artwork).IsNull();
    }

    [Test]
    public async Task LoadArtworkUncached_WhenFileHasAnEmbeddedPicture_ReturnsItsBytes()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track-with-artwork.mp3");

        var artwork = MusicLibraryScanner.LoadArtworkUncached(fixturePath);

        await Assert.That(artwork).IsNotNull();
        await Assert.That(artwork!.Length).IsGreaterThan(0);
    }
}
