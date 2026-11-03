using SharpSelecta.App.Services;

namespace SharpSelecta.Tests;

public class AlbumArtworkCacheTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    [Test]
    public async Task Prune_DeletesThumbnailsOfAlbumsNoLongerInTheLibrary_AndKeepsLiveOnes()
    {
        var cacheDirectory = _temp.NewSubdirectory().FullName;
        File.WriteAllBytes(AlbumArtworkCache.GetCachePath(cacheDirectory, "Live AlbumArtist"), [1]);
        File.WriteAllBytes(AlbumArtworkCache.GetCachePath(cacheDirectory, "Renamed AlbumArtist"), [2]);
        var unrelatedFile = Path.Combine(cacheDirectory, "notes.txt");
        File.WriteAllText(unrelatedFile, "");
        var inFlightWrite = AlbumArtworkCache.GetCachePath(cacheDirectory, "Renamed AlbumArtist") + ".tmp-0123";
        File.WriteAllBytes(inFlightWrite, [3]);

        var deleted = AlbumArtworkCache.Prune(cacheDirectory, [" live albumartist "]);

        await Assert.That(deleted).IsEqualTo(1);
        await Assert.That(AlbumArtworkCache.GetOrCreate(cacheDirectory, "Live AlbumArtist", () => null)).IsEquivalentTo(new byte[] { 1 });
        await Assert.That(AlbumArtworkCache.GetOrCreate(cacheDirectory, "Renamed AlbumArtist", () => null)).IsNull();
        await Assert.That(File.Exists(unrelatedFile)).IsTrue();
        await Assert.That(File.Exists(inFlightWrite)).IsTrue();
    }

    [Test]
    public async Task Prune_WithoutACacheDirectory_DeletesNothing()
    {
        var deleted = AlbumArtworkCache.Prune(_temp.FilePath("missing"), []);

        await Assert.That(deleted).IsEqualTo(0);
    }
}
