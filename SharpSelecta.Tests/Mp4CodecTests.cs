using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class Mp4CodecTests
{
    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    [Test]
    public async Task FileType_OfAnAlacM4a_NamesAlac()
    {
        var track = MusicLibraryScanner.ReadTrackIfExists(FixturePath("untagged-track-alac.m4a"))!;

        await Assert.That(track.FileType).IsEqualTo(AudioFileType.M4aAlac);
    }

    [Test]
    public async Task FileType_OfAnAacM4a_NamesAac()
    {
        var track = MusicLibraryScanner.ReadTrackIfExists(FixturePath("untagged-track.m4a"))!;

        await Assert.That(track.FileType).IsEqualTo(AudioFileType.M4aAac);
    }

    [Test]
    public async Task FileType_OfOtherFormats_IsTheFormat()
    {
        await Assert.That(MusicLibraryScanner.ReadTrackIfExists(FixturePath("untagged-track.flac"))!.FileType).IsEqualTo(AudioFileType.Flac);
    }

    [Test]
    public async Task FileType_OfAnM4aThatIsNotAnMp4_FallsBackToM4a()
    {
        using var temp = new TempDirectory();
        var path = temp.FilePath("broken.m4a");
        File.WriteAllBytes(path, [1, 2, 3]);
        await Assert.That(MusicLibraryScanner.ReadTrackIfExists(path)!.FileType).IsEqualTo(AudioFileType.M4a);
    }

    [Test]
    public async Task Reconcile_ReReadsM4aRowsIndexedBeforeTheCodecWasKnown()
    {
        using var library = new M4aLibrary();
        LibraryIndexStore.Reconcile(library.SettingsPath, [library.Root]);

        await library.ExecuteAsync("UPDATE Tracks SET FileType = 'M4A'; PRAGMA user_version = 0;");

        var reread = LibraryIndexStore.Reconcile(library.SettingsPath, [library.Root]).Tracks[0];

        await Assert.That(reread.FileType).IsEqualTo(AudioFileType.M4aAlac);
    }

    [Test]
    public async Task Reconcile_DoesNotReReadM4aRowsOnceTheIndexWasMigrated()
    {
        using var library = new M4aLibrary();
        LibraryIndexStore.Reconcile(library.SettingsPath, [library.Root]);

        await library.ExecuteAsync("UPDATE Tracks SET FileType = 'M4a'");

        var track = LibraryIndexStore.Reconcile(library.SettingsPath, [library.Root]).Tracks[0];

        await Assert.That(track.FileType).IsEqualTo(AudioFileType.M4a);
    }

    [Test]
    public async Task LoadIndexed_ReturnsTheFileTypeTheIndexWasReconciledWith()
    {
        using var library = new M4aLibrary();
        LibraryIndexStore.Reconcile(library.SettingsPath, [library.Root]);

        var track = LibraryIndexStore.LoadIndexed(library.SettingsPath, [library.Root])[0];

        await Assert.That(track.FileType).IsEqualTo(AudioFileType.M4aAlac);
    }

    [Test]
    public async Task LoadIndexed_MigratesFileTypesStoredAsDisplayLabels()
    {
        using var library = new M4aLibrary();
        LibraryIndexStore.Reconcile(library.SettingsPath, [library.Root]);

        await library.ExecuteAsync("UPDATE Tracks SET FileType = 'M4A (ALAC)'; PRAGMA user_version = 1;");

        var track = LibraryIndexStore.LoadIndexed(library.SettingsPath, [library.Root])[0];

        await Assert.That(track.FileType).IsEqualTo(AudioFileType.M4aAlac);
    }

    private sealed class M4aLibrary : IDisposable
    {
        private readonly TempDirectory _temp = new();

        public M4aLibrary()
        {
            SettingsPath = _temp.NewSettingsPath();
            Root = _temp.NewSubdirectory().FullName;
            File.Copy(FixturePath("untagged-track-alac.m4a"), Path.Combine(Root, "a.m4a"));
        }

        public string SettingsPath { get; }

        public string Root { get; }

        private string IndexPath => LibraryIndexStore.IndexFilePath(SettingsPath);

        public async Task ExecuteAsync(string sql)
        {
            await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={IndexPath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        public void Dispose() => _temp.Dispose();
    }
}
