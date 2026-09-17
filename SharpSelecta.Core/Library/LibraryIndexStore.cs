using Microsoft.Data.Sqlite;

namespace SharpSelecta.Core.Library;

public static class LibraryIndexStore
{
    public sealed record ReconcileResult(IReadOnlyList<Track> Tracks, IReadOnlyList<string> FailedFolders);

    public sealed record PlaylistSummary(string Id, string Name, DateTime CreatedUtc);

    public sealed record PlaylistTrackEntry(int Position, string FilePath, Track? Track);

    private const string ColumnList =
        "FilePath, FolderPath, DisplayName, TrackNumber, Title, Artist, Album, AlbumArtist, Year, " +
        "DurationSeconds, SampleRate, BitDepth, Bitrate, FileType, LastWriteTimeUtcTicks, FileSizeBytes, DateAddedUtc";

    public static IReadOnlyList<Track> LoadIndexed(string settingsFilePath, IReadOnlyList<string> folderPaths)
    {
        var indexFilePath = IndexFilePath(settingsFilePath);
        if (!File.Exists(indexFilePath))
        {
            return [];
        }

        using var connection = OpenConnection(indexFilePath);
        EnsureSchema(connection);

        var tracks = new List<Track>();
        foreach (var folderPath in folderPaths)
        {
            tracks.AddRange(LoadFolderIndex(connection, folderPath).Values.Select(v => v.Track));
        }

        return tracks;
    }

    public static ReconcileResult Reconcile(string settingsFilePath, IReadOnlyList<string> folderPaths)
    {
        var indexFilePath = IndexFilePath(settingsFilePath);
        var directory = Path.GetDirectoryName(indexFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var connection = OpenConnection(indexFilePath);
        EnsureSchema(connection);

        var tracks = new List<Track>();
        var failedFolders = new List<string>();
        foreach (var folderPath in folderPaths)
        {
            var (folderTracks, failed) = ReconcileFolder(connection, folderPath);
            tracks.AddRange(folderTracks);
            if (failed)
            {
                failedFolders.Add(folderPath);
            }
        }

        PruneFoldersNotIn(connection, folderPaths);

        return new ReconcileResult(tracks, failedFolders);
    }

    private static (IReadOnlyList<Track> Tracks, bool Failed) ReconcileFolder(SqliteConnection connection, string folderPath)
    {
        var existing = LoadFolderIndex(connection, folderPath);
        var dateAddedUtcTicks = DateTime.UtcNow.Ticks;

        List<(string FilePath, Track Track, DateTime LastWriteTimeUtc, long FileSizeBytes, bool Changed)> current;
        try
        {
            current = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.AllDirectories)
                .Where(path => MusicLibraryScanner.SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .AsParallel()
                .AsOrdered()
                .Select(path =>
                {
                    var fileInfo = new FileInfo(path);
                    var lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
                    var fileSizeBytes = fileInfo.Length;

                    var wasIndexed = existing.TryGetValue(path, out var indexed);
                    if (wasIndexed
                        && indexed.LastWriteTimeUtc == lastWriteTimeUtc
                        && indexed.FileSizeBytes == fileSizeBytes)
                    {
                        return (path, indexed.Track, lastWriteTimeUtc, fileSizeBytes, Changed: false);
                    }

                    var track = MusicLibraryScanner.ReadTrack(path);
                    // For new/changed files, set DateAddedUtc to either the original date (if previously indexed)
                    // or the shared "now" timestamp (if new). This matches what UpsertAll will write to the DB.
                    track = track with { DateAddedUtc = wasIndexed ? indexed.Track.DateAddedUtc : new DateTime(dateAddedUtcTicks, DateTimeKind.Utc) };
                    return (path, track, lastWriteTimeUtc, fileSizeBytes, Changed: true);
                })
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (existing.Values.Select(v => v.Track).ToList(), true);
        }

        using (var transaction = connection.BeginTransaction())
        {
            var changedEntries = current
                .Where(c => c.Changed)
                .Select(c => (c.FilePath, c.Track, c.LastWriteTimeUtc, c.FileSizeBytes))
                .ToList();
            UpsertAll(connection, transaction, folderPath, changedEntries, dateAddedUtcTicks);

            var currentPaths = current.Select(c => c.FilePath).ToHashSet();
            var removedPaths = existing.Keys.Where(path => !currentPaths.Contains(path)).ToList();
            DeleteAll(connection, transaction, removedPaths);

            transaction.Commit();
        }

        return (current.Select(c => c.Track).ToList(), false);
    }

    public static IReadOnlyList<float>? TryGetWaveformPeaks(string settingsFilePath, string filePath)
    {
        var indexFilePath = IndexFilePath(settingsFilePath);
        if (!File.Exists(indexFilePath))
        {
            return null;
        }

        using var connection = OpenConnection(indexFilePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT WaveformPeaks FROM Tracks WHERE FilePath = @FilePath";
        command.Parameters.AddWithValue("@FilePath", filePath);

        using var reader = command.ExecuteReader();
        if (!reader.Read() || reader.IsDBNull(0))
        {
            return null;
        }

        return BytesToFloats((byte[])reader.GetValue(0));
    }

    public static void SaveWaveformPeaks(string settingsFilePath, string filePath, IReadOnlyList<float> peaks)
    {
        var indexFilePath = IndexFilePath(settingsFilePath);
        if (!File.Exists(indexFilePath))
        {
            return;
        }

        using var connection = OpenConnection(indexFilePath);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Tracks SET WaveformPeaks = @WaveformPeaks WHERE FilePath = @FilePath";
        command.Parameters.AddWithValue("@WaveformPeaks", FloatsToBytes(peaks));
        command.Parameters.AddWithValue("@FilePath", filePath);
        command.ExecuteNonQuery();
    }

    public static string CreatePlaylist(string settingsFilePath, string name)
    {
        var indexFilePath = IndexFilePath(settingsFilePath);
        if (!File.Exists(indexFilePath))
        {
            return string.Empty;
        }

        var id = Guid.NewGuid().ToString("N");
        using var connection = OpenConnection(indexFilePath);
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Playlists (Id, Name, CreatedUtc) VALUES (@Id, @Name, @CreatedUtc)";
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@CreatedUtc", DateTime.UtcNow.Ticks);
        command.ExecuteNonQuery();
        return id;
    }

    public static void RenamePlaylist(string settingsFilePath, string playlistId, string newName)
    {
        var indexFilePath = IndexFilePath(settingsFilePath);
        if (!File.Exists(indexFilePath))
        {
            return;
        }

        using var connection = OpenConnection(indexFilePath);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Playlists SET Name = @Name WHERE Id = @Id";
        command.Parameters.AddWithValue("@Name", newName);
        command.Parameters.AddWithValue("@Id", playlistId);
        command.ExecuteNonQuery();
    }

    public static void DeletePlaylist(string settingsFilePath, string playlistId)
    {
        var indexFilePath = IndexFilePath(settingsFilePath);
        if (!File.Exists(indexFilePath))
        {
            return;
        }

        using var connection = OpenConnection(indexFilePath);
        using var transaction = connection.BeginTransaction();

        using (var deleteTracks = connection.CreateCommand())
        {
            deleteTracks.Transaction = transaction;
            deleteTracks.CommandText = "DELETE FROM PlaylistTracks WHERE PlaylistId = @Id";
            deleteTracks.Parameters.AddWithValue("@Id", playlistId);
            deleteTracks.ExecuteNonQuery();
        }

        using (var deletePlaylist = connection.CreateCommand())
        {
            deletePlaylist.Transaction = transaction;
            deletePlaylist.CommandText = "DELETE FROM Playlists WHERE Id = @Id";
            deletePlaylist.Parameters.AddWithValue("@Id", playlistId);
            deletePlaylist.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    public static IReadOnlyList<PlaylistSummary> ListPlaylists(string settingsFilePath)
    {
        var indexFilePath = IndexFilePath(settingsFilePath);
        if (!File.Exists(indexFilePath))
        {
            return [];
        }

        using var connection = OpenConnection(indexFilePath);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, CreatedUtc FROM Playlists ORDER BY CreatedUtc ASC";

        var result = new List<PlaylistSummary>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new PlaylistSummary(reader.GetString(0), reader.GetString(1), new DateTime(reader.GetInt64(2), DateTimeKind.Utc)));
        }

        return result;
    }

    public static IReadOnlyList<PlaylistTrackEntry> GetPlaylistTracks(string settingsFilePath, string playlistId)
    {
        var indexFilePath = IndexFilePath(settingsFilePath);
        if (!File.Exists(indexFilePath))
        {
            return [];
        }

        using var connection = OpenConnection(indexFilePath);
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT p.Position, p.FilePath, t.FilePath, t.FolderPath, t.DisplayName, t.TrackNumber, t.Title, t.Artist, t.Album, t.AlbumArtist, t.Year, t.DurationSeconds, t.SampleRate, t.BitDepth, t.Bitrate, t.FileType, t.LastWriteTimeUtcTicks, t.FileSizeBytes, t.DateAddedUtc
            FROM PlaylistTracks p
            LEFT JOIN Tracks t ON t.FilePath = p.FilePath
            WHERE p.PlaylistId = @PlaylistId
            ORDER BY p.Position ASC
            """;
        command.Parameters.AddWithValue("@PlaylistId", playlistId);

        var result = new List<PlaylistTrackEntry>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var position = reader.GetInt32(0);
            var filePath = reader.GetString(1);
            // Column 2 onward is ColumnList's own FilePath - null there means the LEFT JOIN found
            // no matching Tracks row (file removed from the library since it was added here).
            var track = reader.IsDBNull(2) ? null : ReadTrackFromPlaylistJoin(reader);
            result.Add(new PlaylistTrackEntry(position, filePath, track));
        }

        return result;
    }

    // Mirrors LoadFolderIndex's column mapping, offset by the 2 extra leading columns
    // (Position, FilePath) this query selects before ColumnList.
    private static Track ReadTrackFromPlaylistJoin(SqliteDataReader reader) => new(reader.GetString(2), reader.GetString(4))
    {
        TrackNumber = reader.IsDBNull(5) ? null : reader.GetInt32(5),
        Title = reader.IsDBNull(6) ? null : reader.GetString(6),
        Artist = reader.IsDBNull(7) ? null : reader.GetString(7),
        Album = reader.IsDBNull(8) ? null : reader.GetString(8),
        AlbumArtist = reader.IsDBNull(9) ? null : reader.GetString(9),
        Year = reader.IsDBNull(10) ? null : reader.GetInt32(10),
        Duration = TimeSpan.FromSeconds(reader.GetDouble(11)),
        SampleRate = reader.GetInt32(12),
        BitDepth = reader.GetInt32(13),
        Bitrate = reader.GetInt32(14),
        FileType = reader.IsDBNull(15) ? null : reader.GetString(15),
        DateAddedUtc = new DateTime(reader.GetInt64(18), DateTimeKind.Utc),
    };

    public static void ReplacePlaylistTracks(string settingsFilePath, string playlistId, IReadOnlyList<string> filePathsInOrder)
    {
        var indexFilePath = IndexFilePath(settingsFilePath);
        if (!File.Exists(indexFilePath))
        {
            return;
        }

        using var connection = OpenConnection(indexFilePath);
        using var transaction = connection.BeginTransaction();

        using (var delete = connection.CreateCommand())
        {
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM PlaylistTracks WHERE PlaylistId = @PlaylistId";
            delete.Parameters.AddWithValue("@PlaylistId", playlistId);
            delete.ExecuteNonQuery();
        }

        using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO PlaylistTracks (PlaylistId, Position, FilePath) VALUES (@PlaylistId, @Position, @FilePath)";
            var pPlaylistId = insert.Parameters.Add("@PlaylistId", SqliteType.Text);
            var pPosition = insert.Parameters.Add("@Position", SqliteType.Integer);
            var pFilePath = insert.Parameters.Add("@FilePath", SqliteType.Text);

            pPlaylistId.Value = playlistId;
            for (var i = 0; i < filePathsInOrder.Count; i++)
            {
                pPosition.Value = i;
                pFilePath.Value = filePathsInOrder[i];
                insert.ExecuteNonQuery();
            }
        }

        transaction.Commit();
    }

    private static byte[] FloatsToBytes(IReadOnlyList<float> peaks)
    {
        var array = peaks as float[] ?? peaks.ToArray();
        var bytes = new byte[array.Length * sizeof(float)];
        Buffer.BlockCopy(array, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static float[] BytesToFloats(byte[] bytes)
    {
        var floats = new float[bytes.Length / sizeof(float)];
        Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);
        return floats;
    }

    private static void UpsertAll(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string folderPath,
        List<(string FilePath, Track Track, DateTime LastWriteTimeUtc, long FileSizeBytes)> entries,
        long dateAddedUtcTicks)
    {
        if (entries.Count == 0)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        // UpsertAll only runs for new/changed files, so any cached WaveformPeaks on a matched
        // row belongs to the old file content and must be dropped, not carried forward.
        command.CommandText = $"""
            INSERT INTO Tracks ({ColumnList})
            VALUES (@FilePath, @FolderPath, @DisplayName, @TrackNumber, @Title, @Artist, @Album, @AlbumArtist, @Year,
                    @DurationSeconds, @SampleRate, @BitDepth, @Bitrate, @FileType, @LastWriteTimeUtcTicks, @FileSizeBytes, @DateAddedUtc)
            ON CONFLICT(FilePath) DO UPDATE SET
                FolderPath = excluded.FolderPath, DisplayName = excluded.DisplayName, TrackNumber = excluded.TrackNumber,
                Title = excluded.Title, Artist = excluded.Artist, Album = excluded.Album, AlbumArtist = excluded.AlbumArtist,
                Year = excluded.Year, DurationSeconds = excluded.DurationSeconds, SampleRate = excluded.SampleRate,
                BitDepth = excluded.BitDepth, Bitrate = excluded.Bitrate, FileType = excluded.FileType,
                LastWriteTimeUtcTicks = excluded.LastWriteTimeUtcTicks, FileSizeBytes = excluded.FileSizeBytes,
                WaveformPeaks = NULL
                -- DateAddedUtc is deliberately absent here: SQLite's ON CONFLICT DO UPDATE only touches
                -- listed columns, so an existing row keeps its original value. A metadata refresh must
                -- never bump a track back to the top of "Recently Added".
            """;

        var pFilePath = command.Parameters.Add("@FilePath", SqliteType.Text);
        var pFolderPath = command.Parameters.Add("@FolderPath", SqliteType.Text);
        var pDisplayName = command.Parameters.Add("@DisplayName", SqliteType.Text);
        var pTrackNumber = command.Parameters.Add("@TrackNumber", SqliteType.Integer);
        var pTitle = command.Parameters.Add("@Title", SqliteType.Text);
        var pArtist = command.Parameters.Add("@Artist", SqliteType.Text);
        var pAlbum = command.Parameters.Add("@Album", SqliteType.Text);
        var pAlbumArtist = command.Parameters.Add("@AlbumArtist", SqliteType.Text);
        var pYear = command.Parameters.Add("@Year", SqliteType.Integer);
        var pDurationSeconds = command.Parameters.Add("@DurationSeconds", SqliteType.Real);
        var pSampleRate = command.Parameters.Add("@SampleRate", SqliteType.Integer);
        var pBitDepth = command.Parameters.Add("@BitDepth", SqliteType.Integer);
        var pBitrate = command.Parameters.Add("@Bitrate", SqliteType.Integer);
        var pFileType = command.Parameters.Add("@FileType", SqliteType.Text);
        var pLastWriteTimeUtcTicks = command.Parameters.Add("@LastWriteTimeUtcTicks", SqliteType.Integer);
        var pFileSizeBytes = command.Parameters.Add("@FileSizeBytes", SqliteType.Integer);
        var pDateAddedUtc = command.Parameters.Add("@DateAddedUtc", SqliteType.Integer);

        foreach (var (filePath, track, lastWriteTimeUtc, fileSizeBytes) in entries)
        {
            pFilePath.Value = filePath;
            pFolderPath.Value = folderPath;
            pDisplayName.Value = track.DisplayName;
            pTrackNumber.Value = (object?)track.TrackNumber ?? DBNull.Value;
            pTitle.Value = (object?)track.Title ?? DBNull.Value;
            pArtist.Value = (object?)track.Artist ?? DBNull.Value;
            pAlbum.Value = (object?)track.Album ?? DBNull.Value;
            pAlbumArtist.Value = (object?)track.AlbumArtist ?? DBNull.Value;
            pYear.Value = (object?)track.Year ?? DBNull.Value;
            pDurationSeconds.Value = track.Duration.TotalSeconds;
            pSampleRate.Value = track.SampleRate;
            pBitDepth.Value = track.BitDepth;
            pBitrate.Value = track.Bitrate;
            pFileType.Value = (object?)track.FileType ?? DBNull.Value;
            pLastWriteTimeUtcTicks.Value = lastWriteTimeUtc.Ticks;
            pFileSizeBytes.Value = fileSizeBytes;
            pDateAddedUtc.Value = dateAddedUtcTicks;
            command.ExecuteNonQuery();
        }
    }

    private static void DeleteAll(SqliteConnection connection, SqliteTransaction transaction, IReadOnlyList<string> filePaths)
    {
        if (filePaths.Count == 0)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM Tracks WHERE FilePath = @FilePath";
        var parameter = command.Parameters.Add("@FilePath", SqliteType.Text);
        foreach (var filePath in filePaths)
        {
            parameter.Value = filePath;
            command.ExecuteNonQuery();
        }
    }

    private static void PruneFoldersNotIn(SqliteConnection connection, IReadOnlyList<string> folderPaths)
    {
        if (folderPaths.Count == 0)
        {
            return;
        }

        using var command = connection.CreateCommand();
        var placeholders = string.Join(", ", folderPaths.Select((_, i) => $"@f{i}"));
        command.CommandText = $"DELETE FROM Tracks WHERE FolderPath NOT IN ({placeholders})";
        for (var i = 0; i < folderPaths.Count; i++)
        {
            command.Parameters.AddWithValue($"@f{i}", folderPaths[i]);
        }

        command.ExecuteNonQuery();
    }

    private static Dictionary<string, (DateTime LastWriteTimeUtc, long FileSizeBytes, Track Track)> LoadFolderIndex(
        SqliteConnection connection, string folderPath)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {ColumnList} FROM Tracks WHERE FolderPath = @FolderPath";
        command.Parameters.AddWithValue("@FolderPath", folderPath);

        var result = new Dictionary<string, (DateTime, long, Track)>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var filePath = reader.GetString(0);
            var track = new Track(filePath, reader.GetString(2))
            {
                TrackNumber = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                Title = reader.IsDBNull(4) ? null : reader.GetString(4),
                Artist = reader.IsDBNull(5) ? null : reader.GetString(5),
                Album = reader.IsDBNull(6) ? null : reader.GetString(6),
                AlbumArtist = reader.IsDBNull(7) ? null : reader.GetString(7),
                Year = reader.IsDBNull(8) ? null : reader.GetInt32(8),
                Duration = TimeSpan.FromSeconds(reader.GetDouble(9)),
                SampleRate = reader.GetInt32(10),
                BitDepth = reader.GetInt32(11),
                Bitrate = reader.GetInt32(12),
                FileType = reader.IsDBNull(13) ? null : reader.GetString(13),
                DateAddedUtc = new DateTime(reader.GetInt64(16), DateTimeKind.Utc),
            };
            var lastWriteTimeUtc = new DateTime(reader.GetInt64(14), DateTimeKind.Utc);
            var fileSizeBytes = reader.GetInt64(15);
            result[filePath] = (lastWriteTimeUtc, fileSizeBytes, track);
        }

        return result;
    }

    private static void EnsureSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Tracks (
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
                WaveformPeaks         BLOB    NULL,
                DateAddedUtc          INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_Tracks_FolderPath ON Tracks(FolderPath);
            """;
        command.ExecuteNonQuery();

        // CREATE TABLE IF NOT EXISTS above only takes effect for a brand-new index file - a
        // pre-existing one from before this column existed needs an explicit migration.
        if (!HasColumn(connection, "Tracks", "WaveformPeaks"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE Tracks ADD COLUMN WaveformPeaks BLOB NULL";
            alter.ExecuteNonQuery();
        }

        if (!HasColumn(connection, "Tracks", "DateAddedUtc"))
        {
            using var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE Tracks ADD COLUMN DateAddedUtc INTEGER NOT NULL DEFAULT 0";
            alter.ExecuteNonQuery();

            // Pre-existing rows have no real "date added" - approximate it from LastWriteTimeUtcTicks
            // so "Recently Added" has a sane (if imprecise) order instead of every old track tying at 0.
            using var backfill = connection.CreateCommand();
            backfill.CommandText = "UPDATE Tracks SET DateAddedUtc = LastWriteTimeUtcTicks WHERE DateAddedUtc = 0";
            backfill.ExecuteNonQuery();
        }

        using (var playlistCommand = connection.CreateCommand())
        {
            playlistCommand.CommandText = """
                CREATE TABLE IF NOT EXISTS Playlists (
                    Id          TEXT NOT NULL PRIMARY KEY,
                    Name        TEXT NOT NULL,
                    CreatedUtc  INTEGER NOT NULL
                );

                CREATE TABLE IF NOT EXISTS PlaylistTracks (
                    PlaylistId  TEXT    NOT NULL,
                    Position    INTEGER NOT NULL,
                    FilePath    TEXT    NOT NULL,
                    PRIMARY KEY (PlaylistId, Position)
                );

                CREATE INDEX IF NOT EXISTS IX_PlaylistTracks_PlaylistId ON PlaylistTracks(PlaylistId);
                """;
            playlistCommand.ExecuteNonQuery();
        }
    }

    private static bool HasColumn(SqliteConnection connection, string table, string column)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table})";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static SqliteConnection OpenConnection(string indexFilePath)
    {
        var connection = new SqliteConnection($"Data Source={indexFilePath}");
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA busy_timeout=3000;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    private static string IndexFilePath(string settingsFilePath)
    {
        var directory = Path.GetDirectoryName(settingsFilePath);
        var fileName = $"{Path.GetFileNameWithoutExtension(settingsFilePath)}.library-index.db";
        return string.IsNullOrEmpty(directory) ? fileName : Path.Combine(directory, fileName);
    }
}
