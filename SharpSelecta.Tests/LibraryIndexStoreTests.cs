using Microsoft.Data.Sqlite;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class LibraryIndexStoreTests
{
    private static readonly string TaggedTrackFixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3");

    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-library-index-tests-{Guid.NewGuid():N}.json");

    private static string IndexFilePath(string settingsPath) =>
        Path.Combine(Path.GetDirectoryName(settingsPath)!, $"{Path.GetFileNameWithoutExtension(settingsPath)}.library-index.db");

    private static void CopyFixtureInto(string folder, string fileName) =>
        File.Copy(TaggedTrackFixturePath, Path.Combine(folder, fileName));

    [Test]
    public async Task LoadIndexed_WhenNoIndexFileExists_ReturnsEmpty()
    {
        var settingsPath = CreateTempSettingsPath();

        var loaded = LibraryIndexStore.LoadIndexed(settingsPath, ["/music/library"]);

        await Assert.That(loaded).IsEmpty();
    }

    [Test]
    public async Task Reconcile_OnFirstRun_ScansDiskAndPersistsToIndex()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            CopyFixtureInto(root.FullName, "tagged-track.mp3");

            var result = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            await Assert.That(result.Tracks.Count).IsEqualTo(1);
            await Assert.That(result.Tracks[0].Title).IsEqualTo("Test Song");
            await Assert.That(result.FailedFolders).IsEmpty();

            root.Delete(recursive: true);

            var hydrated = LibraryIndexStore.LoadIndexed(settingsPath, [root.FullName]);
            await Assert.That(hydrated.Count).IsEqualTo(1);
            await Assert.That(hydrated[0].Title).IsEqualTo("Test Song");
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            if (root.Exists) root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Reconcile_WhenMtimeAndSizeUnchanged_ReusesIndexedTagsWithoutReReadingTheFile()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            CopyFixtureInto(root.FullName, "tagged-track.mp3");

            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            await using (var connection = new SqliteConnection($"Data Source={IndexFilePath(settingsPath)}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = "UPDATE Tracks SET Title = 'mutated' WHERE FilePath = @path";
                command.Parameters.AddWithValue("@path", trackPath);
                await command.ExecuteNonQueryAsync();
            }

            var unchanged = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            await Assert.That(unchanged.Tracks[0].Title).IsEqualTo("mutated");

            await File.WriteAllBytesAsync(trackPath, await File.ReadAllBytesAsync(TaggedTrackFixturePath));
            File.SetLastWriteTimeUtc(trackPath, DateTime.UtcNow);

            var changed = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            await Assert.That(changed.Tracks[0].Title).IsEqualTo("Test Song");
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Reconcile_WhenFileDeletedFromDisk_RemovesItFromIndex()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            CopyFixtureInto(root.FullName, "a.mp3");
            CopyFixtureInto(root.FullName, "b.mp3");
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            File.Delete(Path.Combine(root.FullName, "a.mp3"));
            var result = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            await Assert.That(result.Tracks.Count).IsEqualTo(1);
            await Assert.That(result.Tracks[0].FilePath).IsEqualTo(Path.Combine(root.FullName, "b.mp3"));

            await using var connection = new SqliteConnection($"Data Source={IndexFilePath(settingsPath)}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Tracks";
            var count = (long)(await command.ExecuteScalarAsync())!;
            await Assert.That(count).IsEqualTo(1L);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Reconcile_WhenFolderRemovedFromConfiguredList_PrunesItsTracksFromTheIndex()
    {
        var settingsPath = CreateTempSettingsPath();
        var folderA = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-a-");
        var folderB = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-b-");
        try
        {
            CopyFixtureInto(folderA.FullName, "a.mp3");
            CopyFixtureInto(folderB.FullName, "b.mp3");
            LibraryIndexStore.Reconcile(settingsPath, [folderA.FullName, folderB.FullName]);

            LibraryIndexStore.Reconcile(settingsPath, [folderB.FullName]);

            await using var connection = new SqliteConnection($"Data Source={IndexFilePath(settingsPath)}");
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM Tracks WHERE FolderPath = @folder";
            command.Parameters.AddWithValue("@folder", folderA.FullName);
            var count = (long)(await command.ExecuteScalarAsync())!;
            await Assert.That(count).IsEqualTo(0L);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            folderA.Delete(recursive: true);
            folderB.Delete(recursive: true);
        }
    }

    [Test]
    public async Task TryGetWaveformPeaks_WhenIndexFileDoesNotExist_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();

        var peaks = LibraryIndexStore.TryGetWaveformPeaks(settingsPath, "/music/a.mp3");

        await Assert.That(peaks).IsNull();
    }

    [Test]
    public async Task TryGetWaveformPeaks_WhenNoPeaksHaveBeenSavedForTheTrack_ReturnsNull()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            var peaks = LibraryIndexStore.TryGetWaveformPeaks(settingsPath, Path.Combine(root.FullName, "tagged-track.mp3"));

            await Assert.That(peaks).IsNull();
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task SaveWaveformPeaks_ThenTryGetWaveformPeaks_RoundTripsTheValues()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            LibraryIndexStore.SaveWaveformPeaks(settingsPath, trackPath, [0.1f, 0.5f, 1f, 0f]);
            var peaks = LibraryIndexStore.TryGetWaveformPeaks(settingsPath, trackPath);

            await Assert.That(peaks).IsEquivalentTo([0.1f, 0.5f, 1f, 0f]);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Reconcile_WhenAFileChanges_ClearsAnyPreviouslyCachedWaveformPeaks()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            LibraryIndexStore.SaveWaveformPeaks(settingsPath, trackPath, [0.1f, 0.5f]);

            await File.WriteAllBytesAsync(trackPath, await File.ReadAllBytesAsync(TaggedTrackFixturePath));
            File.SetLastWriteTimeUtc(trackPath, DateTime.UtcNow);
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            var peaks = LibraryIndexStore.TryGetWaveformPeaks(settingsPath, trackPath);
            await Assert.That(peaks).IsNull();
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Reconcile_OnAnIndexFileFromBeforeWaveformCachingExisted_MigratesTheSchemaWithoutLosingData()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            CopyFixtureInto(root.FullName, "tagged-track.mp3");

            var indexFilePath = IndexFilePath(settingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(indexFilePath)!);
            await using (var connection = new SqliteConnection($"Data Source={indexFilePath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE Tracks (
                        FilePath              TEXT    NOT NULL PRIMARY KEY,
                        FolderPath            TEXT    NOT NULL,
                        DisplayName           TEXT    NOT NULL,
                        TrackNumber           INTEGER NULL,
                        Title                 TEXT    NULL,
                        Artist                TEXT    NULL,
                        Album                 TEXT    NULL,
                        AlbumArtist           TEXT    NULL,
                        Year                  INTEGER NULL,
                        DurationSeconds       REAL    NOT NULL,
                        SampleRate            INTEGER NOT NULL,
                        BitDepth              INTEGER NOT NULL,
                        Bitrate               INTEGER NOT NULL,
                        FileType              TEXT    NULL,
                        LastWriteTimeUtcTicks INTEGER NOT NULL,
                        FileSizeBytes         INTEGER NOT NULL
                    );
                    """;
                await command.ExecuteNonQueryAsync();
            }

            var result = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            await Assert.That(result.Tracks.Count).IsEqualTo(1);
            LibraryIndexStore.SaveWaveformPeaks(settingsPath, trackPath, [0.3f]);
            var peaks = LibraryIndexStore.TryGetWaveformPeaks(settingsPath, trackPath);
            await Assert.That(peaks).IsEquivalentTo([0.3f]);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Reconcile_WhenAFolderIsMissing_ReportsItFailedButKeepsServingItsLastIndexedTracks()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            var firstResult = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            await Assert.That(firstResult.Tracks.Count).IsEqualTo(1);

            root.Delete(recursive: true);

            var result = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            await Assert.That(result.FailedFolders).IsEquivalentTo([root.FullName]);
            await Assert.That(result.Tracks.Count).IsEqualTo(1);
            await Assert.That(result.Tracks[0].Title).IsEqualTo("Test Song");
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            if (root.Exists) root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Reconcile_OnANewTrack_SetsDateAddedUtcToApproximatelyNow()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            var before = DateTime.UtcNow;

            var result = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            var after = DateTime.UtcNow;
            await Assert.That(result.Tracks[0].DateAddedUtc).IsGreaterThanOrEqualTo(before);
            await Assert.That(result.Tracks[0].DateAddedUtc).IsLessThanOrEqualTo(after);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Reconcile_WhenATrackIsUnchanged_PreservesItsOriginalDateAddedUtc()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            var first = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var originalDateAdded = first.Tracks[0].DateAddedUtc;

            var second = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            await Assert.That(second.Tracks[0].DateAddedUtc).IsEqualTo(originalDateAdded);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Reconcile_WhenATracksContentChanges_StillPreservesItsOriginalDateAddedUtc()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            var first = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var originalDateAdded = first.Tracks[0].DateAddedUtc;

            await File.WriteAllBytesAsync(trackPath, await File.ReadAllBytesAsync(TaggedTrackFixturePath));
            File.SetLastWriteTimeUtc(trackPath, DateTime.UtcNow);
            var second = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            await Assert.That(second.Tracks[0].DateAddedUtc).IsEqualTo(originalDateAdded);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Reconcile_OnAnIndexFileFromBeforeDateAddedUtcExisted_BackfillsFromLastWriteTime()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(trackPath);

            var indexFilePath = IndexFilePath(settingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(indexFilePath)!);
            await using (var connection = new SqliteConnection($"Data Source={indexFilePath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE Tracks (
                        FilePath              TEXT    NOT NULL PRIMARY KEY,
                        FolderPath            TEXT    NOT NULL,
                        DisplayName           TEXT    NOT NULL,
                        TrackNumber           INTEGER NULL,
                        Title                 TEXT    NULL,
                        Artist                TEXT    NULL,
                        Album                 TEXT    NULL,
                        AlbumArtist           TEXT    NULL,
                        Year                  INTEGER NULL,
                        DurationSeconds       REAL    NOT NULL,
                        SampleRate            INTEGER NOT NULL,
                        BitDepth              INTEGER NOT NULL,
                        Bitrate               INTEGER NOT NULL,
                        FileType              TEXT    NULL,
                        LastWriteTimeUtcTicks INTEGER NOT NULL,
                        FileSizeBytes         INTEGER NOT NULL,
                        WaveformPeaks         BLOB    NULL
                    );
                    INSERT INTO Tracks (FilePath, FolderPath, DisplayName, DurationSeconds, SampleRate, BitDepth, Bitrate, LastWriteTimeUtcTicks, FileSizeBytes)
                    VALUES (@FilePath, @FolderPath, @DisplayName, 0, 0, 0, 0, @Ticks, 0);
                    """;
                command.Parameters.AddWithValue("@FilePath", trackPath);
                command.Parameters.AddWithValue("@FolderPath", root.FullName);
                command.Parameters.AddWithValue("@DisplayName", "tagged-track.mp3");
                command.Parameters.AddWithValue("@Ticks", lastWriteTimeUtc.Ticks);
                await command.ExecuteNonQueryAsync();
            }

            var result = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            await Assert.That(result.Tracks[0].DateAddedUtc).IsEqualTo(lastWriteTimeUtc);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task LoadIndexed_OnAnIndexFileFromBeforeDateAddedUtcExisted_MigratesSchemaAndBackfillsFromLastWriteTime()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            var lastWriteTimeUtc = File.GetLastWriteTimeUtc(trackPath);

            var indexFilePath = IndexFilePath(settingsPath);
            Directory.CreateDirectory(Path.GetDirectoryName(indexFilePath)!);
            await using (var connection = new SqliteConnection($"Data Source={indexFilePath}"))
            {
                await connection.OpenAsync();
                var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE Tracks (
                        FilePath              TEXT    NOT NULL PRIMARY KEY,
                        FolderPath            TEXT    NOT NULL,
                        DisplayName           TEXT    NOT NULL,
                        TrackNumber           INTEGER NULL,
                        Title                 TEXT    NULL,
                        Artist                TEXT    NULL,
                        Album                 TEXT    NULL,
                        AlbumArtist           TEXT    NULL,
                        Year                  INTEGER NULL,
                        DurationSeconds       REAL    NOT NULL,
                        SampleRate            INTEGER NOT NULL,
                        BitDepth              INTEGER NOT NULL,
                        Bitrate               INTEGER NOT NULL,
                        FileType              TEXT    NULL,
                        LastWriteTimeUtcTicks INTEGER NOT NULL,
                        FileSizeBytes         INTEGER NOT NULL,
                        WaveformPeaks         BLOB    NULL
                    );
                    INSERT INTO Tracks (FilePath, FolderPath, DisplayName, DurationSeconds, SampleRate, BitDepth, Bitrate, LastWriteTimeUtcTicks, FileSizeBytes)
                    VALUES (@FilePath, @FolderPath, @DisplayName, 0, 0, 0, 0, @Ticks, 0);
                    """;
                command.Parameters.AddWithValue("@FilePath", trackPath);
                command.Parameters.AddWithValue("@FolderPath", root.FullName);
                command.Parameters.AddWithValue("@DisplayName", "tagged-track.mp3");
                command.Parameters.AddWithValue("@Ticks", lastWriteTimeUtc.Ticks);
                await command.ExecuteNonQueryAsync();
            }

            var result = LibraryIndexStore.LoadIndexed(settingsPath, [root.FullName]);

            await Assert.That(result.Count).IsEqualTo(1);
            await Assert.That(result[0].DateAddedUtc).IsEqualTo(lastWriteTimeUtc);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task CreatePlaylist_ThenListPlaylists_ReturnsItWithAGeneratedId()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            var playlistId = LibraryIndexStore.CreatePlaylist(settingsPath, "Chill");

            await Assert.That(playlistId).IsNotNull();
            var playlists = LibraryIndexStore.ListPlaylists(settingsPath);
            await Assert.That(playlists.Count).IsEqualTo(1);
            await Assert.That(playlists[0].Id).IsEqualTo(playlistId);
            await Assert.That(playlists[0].Name).IsEqualTo("Chill");
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ListPlaylists_OrdersByCreationTime()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            LibraryIndexStore.CreatePlaylist(settingsPath, "First");
            LibraryIndexStore.CreatePlaylist(settingsPath, "Second");

            var playlists = LibraryIndexStore.ListPlaylists(settingsPath);

            await Assert.That(playlists.Select(p => p.Name)).IsEquivalentTo(["First", "Second"]);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task RenamePlaylist_ChangesTheNameWithoutChangingTheId()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var playlistId = LibraryIndexStore.CreatePlaylist(settingsPath, "Old Name");

            LibraryIndexStore.RenamePlaylist(settingsPath, playlistId, "New Name");

            var playlists = LibraryIndexStore.ListPlaylists(settingsPath);
            await Assert.That(playlists[0].Id).IsEqualTo(playlistId);
            await Assert.That(playlists[0].Name).IsEqualTo("New Name");
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task DeletePlaylist_RemovesItAndItsTracks()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            var playlistId = LibraryIndexStore.CreatePlaylist(settingsPath, "Temp");
            LibraryIndexStore.ReplacePlaylistTracks(settingsPath, playlistId, [trackPath]);

            LibraryIndexStore.DeletePlaylist(settingsPath, playlistId);

            await Assert.That(LibraryIndexStore.ListPlaylists(settingsPath)).IsEmpty();
            await Assert.That(LibraryIndexStore.GetPlaylistTracks(settingsPath, playlistId)).IsEmpty();
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ReplacePlaylistTracks_ThenGetPlaylistTracks_RoundTripsOrderAndResolvesRealTracks()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            CopyFixtureInto(root.FullName, "a.mp3");
            CopyFixtureInto(root.FullName, "b.mp3");
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var aPath = Path.Combine(root.FullName, "a.mp3");
            var bPath = Path.Combine(root.FullName, "b.mp3");
            var playlistId = LibraryIndexStore.CreatePlaylist(settingsPath, "Order Test");

            LibraryIndexStore.ReplacePlaylistTracks(settingsPath, playlistId, [bPath, aPath]);

            var entries = LibraryIndexStore.GetPlaylistTracks(settingsPath, playlistId);
            await Assert.That(entries.Count).IsEqualTo(2);
            await Assert.That(entries[0].Position).IsEqualTo(0);
            await Assert.That(entries[0].FilePath).IsEqualTo(bPath);
            await Assert.That(entries[0].Track).IsNotNull();
            await Assert.That(entries[1].Position).IsEqualTo(1);
            await Assert.That(entries[1].FilePath).IsEqualTo(aPath);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ReplacePlaylistTracks_CalledAgain_FullyReplacesThePreviousOrder()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            CopyFixtureInto(root.FullName, "a.mp3");
            CopyFixtureInto(root.FullName, "b.mp3");
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var aPath = Path.Combine(root.FullName, "a.mp3");
            var bPath = Path.Combine(root.FullName, "b.mp3");
            var playlistId = LibraryIndexStore.CreatePlaylist(settingsPath, "Order Test");
            LibraryIndexStore.ReplacePlaylistTracks(settingsPath, playlistId, [aPath, bPath]);

            LibraryIndexStore.ReplacePlaylistTracks(settingsPath, playlistId, [bPath]);

            var entries = LibraryIndexStore.GetPlaylistTracks(settingsPath, playlistId);
            await Assert.That(entries.Count).IsEqualTo(1);
            await Assert.That(entries[0].FilePath).IsEqualTo(bPath);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ReplacePlaylistTracks_AllowsTheSameTrackTwice()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            var playlistId = LibraryIndexStore.CreatePlaylist(settingsPath, "Repeats");

            LibraryIndexStore.ReplacePlaylistTracks(settingsPath, playlistId, [trackPath, trackPath]);

            var entries = LibraryIndexStore.GetPlaylistTracks(settingsPath, playlistId);
            await Assert.That(entries.Count).IsEqualTo(2);
            await Assert.That(entries[0].FilePath).IsEqualTo(trackPath);
            await Assert.That(entries[1].FilePath).IsEqualTo(trackPath);
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task GetPlaylistTracks_WhenAFileNoLongerHasAMatchingTracksRow_ReturnsNullTrackForThatEntry()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-index-tests-");
        try
        {
            CopyFixtureInto(root.FullName, "tagged-track.mp3");
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            var playlistId = LibraryIndexStore.CreatePlaylist(settingsPath, "Will Go Missing");
            LibraryIndexStore.ReplacePlaylistTracks(settingsPath, playlistId, [trackPath]);

            File.Delete(trackPath);
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            var entries = LibraryIndexStore.GetPlaylistTracks(settingsPath, playlistId);
            await Assert.That(entries.Count).IsEqualTo(1);
            await Assert.That(entries[0].FilePath).IsEqualTo(trackPath);
            await Assert.That(entries[0].Track).IsNull();
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            root.Delete(recursive: true);
        }
    }
}
