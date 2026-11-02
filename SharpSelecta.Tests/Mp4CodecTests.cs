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
        using var library = new M4aLibrary();
        LibraryIndexStore.Reconcile(library.SettingsPath, [library.Root]);

        await library.ExecuteAsync("UPDATE Tracks SET FileType = 'M4A'; PRAGMA user_version = 0;");

        var reread = LibraryIndexStore.Reconcile(library.SettingsPath, [library.Root]).Tracks[0];

        await Assert.That(reread.FileType).IsEqualTo("M4A (ALAC)");
    }

    [Test]
    public async Task Reconcile_DoesNotReReadM4aRowsOnceTheIndexWasMigrated()
    {
        using var library = new M4aLibrary();
        LibraryIndexStore.Reconcile(library.SettingsPath, [library.Root]);

        await library.ExecuteAsync("UPDATE Tracks SET FileType = 'M4A'");

        var track = LibraryIndexStore.Reconcile(library.SettingsPath, [library.Root]).Tracks[0];

        await Assert.That(track.FileType).IsEqualTo("M4A");
    }

    private sealed class M4aLibrary : IDisposable
    {
        public string SettingsPath { get; } = Path.Combine(Path.GetTempPath(), $"sharpselecta-m4a-{Guid.NewGuid():N}.json");

        private readonly DirectoryInfo _root = Directory.CreateTempSubdirectory("sharpselecta-m4a-");

        public M4aLibrary() => File.Copy(FixturePath("untagged-track-alac.m4a"), Path.Combine(Root, "a.m4a"));

        public string Root => _root.FullName;

        private string IndexPath => Path.Combine(Path.GetDirectoryName(SettingsPath)!, $"{Path.GetFileNameWithoutExtension(SettingsPath)}.library-index.db");

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={IndexPath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public void Dispose()
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(SettingsPath);
            File.Delete(IndexPath);
            _root.Delete(recursive: true);
        }
    }
}
