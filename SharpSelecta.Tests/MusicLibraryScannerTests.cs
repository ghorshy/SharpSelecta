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

            await Assert.That(tracks.Count).IsEqualTo(2);
            await Assert.That(tracks.Select(t => t.DisplayName)).Contains("top-level.mp3");
            await Assert.That(tracks.Select(t => t.DisplayName)).Contains("01 - track.flac");
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
}
