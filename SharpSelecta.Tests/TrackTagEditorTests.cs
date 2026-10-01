using SharpSelecta.Core.Library;
using AtlTrack = ATL.Track;

namespace SharpSelecta.Tests;

public class TrackTagEditorTests
{
    private static readonly TrackTagEdits SampleEdits = new(
        Title: "New Title", Artist: "New Artist", AlbumArtist: "New Album Artist", Album: "New Album",
        Genre: "Jazz", Comment: "A comment", Year: 1999, TrackNumber: 7);

    private static string CopyFixture(string fixtureName, out DirectoryInfo dir)
    {
        dir = Directory.CreateTempSubdirectory("sharpselecta-tag-editor-tests-");
        var path = Path.Combine(dir.FullName, fixtureName);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureName), path);
        return path;
    }

    [Test]
    [Arguments("untagged-track.mp3")]
    [Arguments("untagged-track.flac")]
    [Arguments("untagged-track.m4a")]
    [Arguments("untagged-track.wav")]
    public async Task Write_RoundTripsEveryField(string fixtureName)
    {
        var path = CopyFixture(fixtureName, out var dir);
        try
        {
            TrackTagEditor.Write(path, SampleEdits);

            var track = MusicLibraryScanner.ReadTrackIfExists(path)!;
            await Assert.That(track.Title).IsEqualTo("New Title");
            await Assert.That(track.Artist).IsEqualTo("New Artist");
            await Assert.That(track.AlbumArtist).IsEqualTo("New Album Artist");
            await Assert.That(track.Album).IsEqualTo("New Album");
            await Assert.That(track.Genre).IsEqualTo("Jazz");
            await Assert.That(track.Comment).IsEqualTo("A comment");
            await Assert.That(track.Year).IsEqualTo(1999);
            await Assert.That(track.TrackNumber).IsEqualTo(7);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Write_WithEmptyFields_ClearsExistingTags()
    {
        var path = CopyFixture("tagged-track.mp3", out var dir);
        try
        {
            TrackTagEditor.Write(path, new TrackTagEdits(null, null, null, null, null, null, null, null));

            var track = MusicLibraryScanner.ReadTrackIfExists(path)!;
            await Assert.That(track.Title).IsNull();
            await Assert.That(track.Artist).IsNull();
            await Assert.That(track.Album).IsNull();
            await Assert.That(track.Year).IsNull();
            await Assert.That(track.TrackNumber).IsNull();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Write_LeavesNoTempFileBehind()
    {
        var path = CopyFixture("untagged-track.mp3", out var dir);
        try
        {
            TrackTagEditor.Write(path, SampleEdits);

            await Assert.That(Directory.GetFiles(dir.FullName).Length).IsEqualTo(1);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Write_WhenOriginalIsMissing_ThrowsAndLeavesNoTempFile()
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-tag-editor-tests-");
        try
        {
            var path = Path.Combine(dir.FullName, "missing.mp3");

            await Assert.That(() => TrackTagEditor.Write(path, SampleEdits)).Throws<IOException>();
            await Assert.That(Directory.GetFiles(dir.FullName).Length).IsEqualTo(0);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    [Arguments("untagged-track.mp3")]
    [Arguments("untagged-track.flac")]
    [Arguments("untagged-track.m4a")]
    [Arguments("untagged-track.wav")]
    public async Task Write_RoundTripsTheDiscNumber_AndClearsItWhenEmpty(string fixtureName)
    {
        var path = CopyFixture(fixtureName, out var dir);
        try
        {
            TrackTagEditor.Write(path, SampleEdits with { DiscNumber = 2 });
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(path)!.DiscNumber).IsEqualTo(2);

            TrackTagEditor.Write(path, SampleEdits with { DiscNumber = null });
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(path)!.DiscNumber).IsNull();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
