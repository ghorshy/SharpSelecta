using Microsoft.Data.Sqlite;

namespace SharpSelecta.Core.Library;

public static class LibraryIndexStore
{
    public sealed record ReconcileResult(IReadOnlyList<Track> Tracks, IReadOnlyList<string> FailedFolders);

    public sealed record PlaylistSummary(string Id, string Name, DateTime CreatedUtc);

    public sealed record PlaylistTrackEntry(int Position, string FilePath, Track? Track);

    private static readonly string[] Columns =
    [
        "FilePath", "FolderPath", "DisplayName", "TrackNumber", "Title", "Artist", "Album", "AlbumArtist", "Year",
        "DurationSeconds", "SampleRate", "BitDepth", "Bitrate", "FileType", "LastWriteTimeUtcTicks", "FileSizeBytes", "DateAddedUtc", "Genre", "Comment", "DiscNumber",
    ];

    private static readonly string ColumnList = string.Join(", ", Columns);

    private static readonly string TrackColumnList = string.Join(", ", Columns.Select(column => $"t.{column}"));

    public static IReadOnlyList<Track> LoadIndexed(string settingsFilePath, IReadOnlyList<string> folderPaths)
    {
        using var connection = TryOpen(settingsFilePath);
        if (connection is null)
        {
            return [];
        }

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
                .Select(c => (c.Track, c.LastWriteTimeUtc, c.FileSizeBytes))
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
        using var connection = TryOpen(settingsFilePath);
        if (connection is null)
        {
            return null;
        }

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
        using var connection = TryOpen(settingsFilePath);
        if (connection is null)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Tracks SET WaveformPeaks = @WaveformPeaks WHERE FilePath = @FilePath";
        command.Parameters.AddWithValue("@WaveformPeaks", FloatsToBytes(peaks));
        command.Parameters.AddWithValue("@FilePath", filePath);
        command.ExecuteNonQuery();
    }

    // Updates mtime/size so Reconcile doesn't re-read the file; keeps DateAddedUtc and waveform peaks.
    public static void UpdateTrack(string settingsFilePath, Track track)
    {
        if (!File.Exists(track.FilePath))
        {
            return;
        }

        using var connection = TryOpen(settingsFilePath);
        if (connection is null)
        {
            return;
        }

        var fileInfo = new FileInfo(track.FilePath);
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Tracks SET
                DisplayName = @DisplayName, TrackNumber = @TrackNumber, Title = @Title, Artist = @Artist, Album = @Album,
                AlbumArtist = @AlbumArtist, Genre = @Genre, Comment = @Comment, DiscNumber = @DiscNumber, Year = @Year,
                DurationSeconds = @DurationSeconds, SampleRate = @SampleRate, BitDepth = @BitDepth, Bitrate = @Bitrate,
                FileType = @FileType, LastWriteTimeUtcTicks = @LastWriteTimeUtcTicks, FileSizeBytes = @FileSizeBytes
            WHERE FilePath = @FilePath
            """;
        BindTrack(command, track, fileInfo.LastWriteTimeUtc, fileInfo.Length);
        command.ExecuteNonQuery();
    }

    public static string CreatePlaylist(string settingsFilePath, string name)
    {
        using var connection = TryOpen(settingsFilePath);
        if (connection is null)
        {
            return string.Empty;
        }

        var id = Guid.NewGuid().ToString("N");
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
        using var connection = TryOpen(settingsFilePath);
        if (connection is null)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Playlists SET Name = @Name WHERE Id = @Id";
        command.Parameters.AddWithValue("@Name", newName);
        command.Parameters.AddWithValue("@Id", playlistId);
        command.ExecuteNonQuery();
    }

    public static void DeletePlaylist(string settingsFilePath, string playlistId)
    {
        using var connection = TryOpen(settingsFilePath);
        if (connection is null)
        {
            return;
        }

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
        using var connection = TryOpen(settingsFilePath);
        if (connection is null)
        {
            return [];
        }

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
        using var connection = TryOpen(settingsFilePath);
        if (connection is null)
        {
            return [];
        }

        using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT p.Position, p.FilePath, {TrackColumnList}
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
            var track = reader.IsDBNull(2) ? null : ReadIndexedTrack(reader, offset: 2);
            result.Add(new PlaylistTrackEntry(position, filePath, track));
        }

        return result;
    }

    // Points every playlist entry of a file at its replacement (e.g. the converted copy), keeping the position.
    public static void ReplaceFileInPlaylists(string settingsFilePath, string oldFilePath, string newFilePath)
    {
        using var connection = TryOpen(settingsFilePath);
        if (connection is null)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE PlaylistTracks SET FilePath = @New WHERE FilePath = @Old";
        command.Parameters.AddWithValue("@New", newFilePath);
        command.Parameters.AddWithValue("@Old", oldFilePath);
        command.ExecuteNonQuery();
    }

    public static void ReplacePlaylistTracks(string settingsFilePath, string playlistId, IReadOnlyList<string> filePathsInOrder)
    {
        using var connection = TryOpen(settingsFilePath);
        if (connection is null)
        {
            return;
        }

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
        List<(Track Track, DateTime LastWriteTimeUtc, long FileSizeBytes)> entries,
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
                    @DurationSeconds, @SampleRate, @BitDepth, @Bitrate, @FileType, @LastWriteTimeUtcTicks, @FileSizeBytes, @DateAddedUtc,
                    @Genre, @Comment, @DiscNumber)
            ON CONFLICT(FilePath) DO UPDATE SET
                FolderPath = excluded.FolderPath, DisplayName = excluded.DisplayName, TrackNumber = excluded.TrackNumber,
                Title = excluded.Title, Artist = excluded.Artist, Album = excluded.Album, AlbumArtist = excluded.AlbumArtist,
                Genre = excluded.Genre, Comment = excluded.Comment, DiscNumber = excluded.DiscNumber,
                Year = excluded.Year, DurationSeconds = excluded.DurationSeconds, SampleRate = excluded.SampleRate,
                BitDepth = excluded.BitDepth, Bitrate = excluded.Bitrate, FileType = excluded.FileType,
                LastWriteTimeUtcTicks = excluded.LastWriteTimeUtcTicks, FileSizeBytes = excluded.FileSizeBytes,
                WaveformPeaks = NULL
                -- DateAddedUtc is left out so a refresh never bumps a track in Recently Added.
            """;

        foreach (var (track, lastWriteTimeUtc, fileSizeBytes) in entries)
        {
            BindTrack(command, track, lastWriteTimeUtc, fileSizeBytes);
            Bind(command, "@FolderPath", folderPath);
            Bind(command, "@DateAddedUtc", dateAddedUtcTicks);
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
            var track = ReadIndexedTrack(reader, offset: 0);
            var lastWriteTimeUtc = new DateTime(reader.GetInt64(14), DateTimeKind.Utc);
            var fileSizeBytes = reader.GetInt64(15);
            result[track.FilePath] = (lastWriteTimeUtc, fileSizeBytes, track);
        }

        return result;
    }

    private static Track ReadIndexedTrack(SqliteDataReader reader, int offset)
    {
        int? NullableInt(int column) => reader.IsDBNull(offset + column) ? null : reader.GetInt32(offset + column);
        string? NullableString(int column) => reader.IsDBNull(offset + column) ? null : reader.GetString(offset + column);

        return new Track(reader.GetString(offset), reader.GetString(offset + 2))
        {
            TrackNumber = NullableInt(3),
            Title = NullableString(4),
            Artist = NullableString(5),
            Album = NullableString(6),
            AlbumArtist = NullableString(7),
            Year = NullableInt(8),
            Duration = TimeSpan.FromSeconds(reader.GetDouble(offset + 9)),
            SampleRate = reader.GetInt32(offset + 10),
            BitDepth = reader.GetInt32(offset + 11),
            Bitrate = reader.GetInt32(offset + 12),
            FileType = NullableString(13),
            DateAddedUtc = new DateTime(reader.GetInt64(offset + 16), DateTimeKind.Utc),
            Genre = NullableString(17),
            Comment = NullableString(18),
            DiscNumber = NullableInt(19),
        };
    }

    private static void BindTrack(SqliteCommand command, Track track, DateTime lastWriteTimeUtc, long fileSizeBytes)
    {
        Bind(command, "@FilePath", track.FilePath);
        Bind(command, "@DisplayName", track.DisplayName);
        Bind(command, "@TrackNumber", track.TrackNumber);
        Bind(command, "@Title", track.Title);
        Bind(command, "@Artist", track.Artist);
        Bind(command, "@Album", track.Album);
        Bind(command, "@AlbumArtist", track.AlbumArtist);
        Bind(command, "@Genre", track.Genre);
        Bind(command, "@Comment", track.Comment);
        Bind(command, "@DiscNumber", track.DiscNumber);
        Bind(command, "@Year", track.Year);
        Bind(command, "@DurationSeconds", track.Duration.TotalSeconds);
        Bind(command, "@SampleRate", track.SampleRate);
        Bind(command, "@BitDepth", track.BitDepth);
        Bind(command, "@Bitrate", track.Bitrate);
        Bind(command, "@FileType", track.FileType);
        Bind(command, "@LastWriteTimeUtcTicks", lastWriteTimeUtc.Ticks);
        Bind(command, "@FileSizeBytes", fileSizeBytes);
    }

    private static void Bind(SqliteCommand command, string name, object? value)
    {
        if (command.Parameters.Contains(name))
        {
            command.Parameters[name].Value = value ?? DBNull.Value;
        }
        else
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
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
                DateAddedUtc          INTEGER NOT NULL DEFAULT 0,
                Genre                 TEXT    NULL,
                Comment               TEXT    NULL,
                DiscNumber            INTEGER NULL
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

        if (!HasColumn(connection, "Tracks", "Genre"))
        {
            using var alter = connection.CreateCommand();
            // Zeroing LastWriteTimeUtcTicks makes the next Reconcile treat every pre-existing row as
            // changed, so Genre/Comment get read from the files instead of staying NULL forever.
            alter.CommandText = """
                ALTER TABLE Tracks ADD COLUMN Genre TEXT NULL;
                ALTER TABLE Tracks ADD COLUMN Comment TEXT NULL;
                UPDATE Tracks SET LastWriteTimeUtcTicks = 0;
                """;
            alter.ExecuteNonQuery();
        }

        if (!HasColumn(connection, "Tracks", "DiscNumber"))
        {
            using var alter = connection.CreateCommand();
            // Same reasoning as Genre/Comment above: existing rows are re-read so the disc number gets filled.
            alter.CommandText = """
                ALTER TABLE Tracks ADD COLUMN DiscNumber INTEGER NULL;
                UPDATE Tracks SET LastWriteTimeUtcTicks = 0;
                """;
            alter.ExecuteNonQuery();
        }

        // Re-read M4A rows indexed before the type told ALAC from AAC, once.
        if (SchemaVersion(connection) < 1)
        {
            using var m4aCommand = connection.CreateCommand();
            m4aCommand.CommandText = """
                BEGIN;
                UPDATE Tracks SET LastWriteTimeUtcTicks = 0 WHERE FileType = 'M4A';
                PRAGMA user_version = 1;
                COMMIT;
                """;
            m4aCommand.ExecuteNonQuery();
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

    private static long SchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version";
        return (long)command.ExecuteScalar()!;
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

    private static SqliteConnection? TryOpen(string settingsFilePath)
    {
        var indexFilePath = IndexFilePath(settingsFilePath);
        return File.Exists(indexFilePath) ? OpenConnection(indexFilePath) : null;
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

    private static string IndexFilePath(string settingsFilePath) =>
        SettingsSiblingFile.PathFor(settingsFilePath, ".library-index.db");
}
