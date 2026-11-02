using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class MusicLibraryScannerTests
{
    [Test]
    public async Task FindAudioFiles_FindsSupportedFilesRecursivelyAndIgnoresOthers()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            var albumDir = Directory.CreateDirectory(Path.Combine(root.FullName, "Artist", "Album"));
            File.WriteAllBytes(Path.Combine(root.FullName, "top-level.mp3"), []);
            File.WriteAllBytes(Path.Combine(albumDir.FullName, "01 - track.flac"), []);
            File.WriteAllBytes(Path.Combine(albumDir.FullName, "cover.jpg"), []);

            var files = MusicLibraryScanner.FindAudioFiles(root.FullName).ToList();

            await Assert.That(files.Count).IsEqualTo(2);
            await Assert.That(files).Contains(Path.Combine(root.FullName, "top-level.mp3"));
            await Assert.That(files).Contains(Path.Combine(albumDir.FullName, "01 - track.flac"));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task FindAudioFiles_ReturnsFilesOrderedByPath()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            string[] names = ["charlie.mp3", "alpha.flac", "echo.wav", "bravo.m4a", "delta.mp3"];
            foreach (var name in names)
            {
                File.WriteAllBytes(Path.Combine(root.FullName, name), []);
            }

            var files = MusicLibraryScanner.FindAudioFiles(root.FullName);

            var expectedOrder = names
                .Select(name => Path.Combine(root.FullName, name))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
            await Assert.That(files).IsEquivalentTo(expectedOrder);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task FindAudioFiles_OnEmptyFolder_ReturnsNothing()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            await Assert.That(MusicLibraryScanner.FindAudioFiles(root.FullName)).IsEmpty();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ReadTrack_OnAnUnreadableFile_FallsBackToFilenameOnlyInsteadOfThrowing()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            var path = Path.Combine(root.FullName, "not-really-audio.wav");
            File.WriteAllBytes(path, [1, 2, 3]);

            var track = MusicLibraryScanner.ReadTrackIfExists(path)!;

            await Assert.That(track.FileType).IsEqualTo("WAV");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ReadTrack_OnAFileWithoutReadPermission_FallsBackToFilenameOnly()
    {
        Skip.When(!OperatingSystem.IsLinux() || Environment.UserName == "root", "Unix permissions, non-root");
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            var path = Path.Combine(root.FullName, "locked.mp3");
            File.WriteAllBytes(path, [1, 2, 3]);
            File.SetUnixFileMode(path, UnixFileMode.None);

            var track = MusicLibraryScanner.ReadTrackIfExists(path)!;

            await Assert.That(track.DisplayName).IsEqualTo("locked.mp3");
            await Assert.That(track.FileType).IsEqualTo("MP3");
            await Assert.That(MusicLibraryScanner.LoadArtworkWithSource(path)).IsNull();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ReadTrack_TrimsWhitespaceAroundTagValues()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            var trackPath = Path.Combine(root.FullName, "padded.mp3");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), trackPath);
            TrackTagEditor.Write(trackPath, new TrackTagEdits("  Padded Title ", " Padded Artist", null, "Album  ", null, null, null, null));

            var track = MusicLibraryScanner.ReadTrackIfExists(trackPath)!;

            await Assert.That(track.Title).IsEqualTo("Padded Title");
            await Assert.That(track.DisplayName).IsEqualTo("Padded Title");
            await Assert.That(track.Artist).IsEqualTo("Padded Artist");
            await Assert.That(track.Album).IsEqualTo("Album");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task AtlConfiguration_DisablesFileNameAsTitleAndConsoleStackTraces()
    {
        AtlConfiguration.Ensure();

        await Assert.That(ATL.Settings.UseFileNameWhenNoTitle).IsFalse();
        await Assert.That(ATL.Settings.OutputStacktracesToConsole).IsFalse();
    }

    [Test]
    public async Task ReadTrack_ReadsTagsAndAudioPropertiesFromARealFile()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3");
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            File.Copy(fixturePath, trackPath);

            var track = MusicLibraryScanner.ReadTrackIfExists(trackPath)!;

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
    public async Task ReadTrack_WithNoYearTag_LeavesYearNullInsteadOfZero()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track-no-year.mp3");
            var trackPath = Path.Combine(root.FullName, "tagged-track-no-year.mp3");
            File.Copy(fixturePath, trackPath);

            var track = MusicLibraryScanner.ReadTrackIfExists(trackPath)!;

            await Assert.That(track.Title).IsEqualTo("No Year Song");
            await Assert.That(track.Year).IsNull();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ReadTrack_WithNoTrackNumberTag_LeavesTrackNumberNullInsteadOfZero()
    {
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-tests-");
        try
        {
            var fixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track-no-tracknumber.mp3");
            var trackPath = Path.Combine(root.FullName, "tagged-track-no-tracknumber.mp3");
            File.Copy(fixturePath, trackPath);

            var track = MusicLibraryScanner.ReadTrackIfExists(trackPath)!;

            await Assert.That(track.Title).IsEqualTo("No Track Number Song");
            await Assert.That(track.TrackNumber).IsNull();
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

}
