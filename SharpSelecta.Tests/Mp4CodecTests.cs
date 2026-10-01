using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class Mp4CodecTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Test]
    public async Task FileType_OfAnAlacM4a_NamesAlac()
    {
        var track = MusicLibraryScanner.ReadTrackIfExists(FixturePath("untagged-track-alac.m4a"))!;

        await Assert.That(track.FileType).IsEqualTo("M4A (ALAC)");
    }

    [Test]
    public async Task FileType_OfAnAacM4a_NamesAac()
    {
        var track = MusicLibraryScanner.ReadTrackIfExists(FixturePath("untagged-track.m4a"))!;

        await Assert.That(track.FileType).IsEqualTo("M4A (AAC)");
    }

    [Test]
    public async Task FileType_OfOtherFormats_IsTheExtension()
    {
        await Assert.That(MusicLibraryScanner.ReadTrackIfExists(FixturePath("untagged-track.flac"))!.FileType).IsEqualTo("FLAC");
    }

    [Test]
    public async Task FileType_OfAnM4aThatIsNotAnMp4_FallsBackToM4a()
    {
        var path = Path.Combine(Path.GetTempPath(), $"broken-{Guid.NewGuid():N}.m4a");
        File.WriteAllBytes(path, [1, 2, 3]);
        try
        {
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(path)!.FileType).IsEqualTo("M4A");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Reconcile_ReReadsM4aRowsIndexedBeforeTheCodecWasKnown()
    {
        var settingsPath = Path.Combine(Path.GetTempPath(), $"sharpselecta-m4a-{Guid.NewGuid():N}.json");
        var root = Directory.CreateTempSubdirectory("sharpselecta-m4a-");
        try
        {
            File.Copy(FixturePath("untagged-track-alac.m4a"), Path.Combine(root.FullName, "a.m4a"));
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            // Pretend the row predates the feature: indexed as plain "M4A" with an up-to-date write time.
            var indexPath = Path.Combine(Path.GetDirectoryName(settingsPath)!, $"{Path.GetFileNameWithoutExtension(settingsPath)}.library-index.db");
            await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={indexPath}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "UPDATE Tracks SET FileType = 'M4A'";
                await command.ExecuteNonQueryAsync();
            }

            var reread = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]).Tracks[0];

            await Assert.That(reread.FileType).IsEqualTo("M4A (ALAC)");
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(settingsPath);
            File.Delete(Path.Combine(Path.GetDirectoryName(settingsPath)!, $"{Path.GetFileNameWithoutExtension(settingsPath)}.library-index.db"));
            root.Delete(recursive: true);
        }
    }
}
