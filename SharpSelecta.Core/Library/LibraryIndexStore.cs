using Microsoft.Data.Sqlite;

namespace SharpSelecta.Core.Library;

public static class LibraryIndexStore
{
    public sealed record ReconcileResult(IReadOnlyList<Track> Tracks, IReadOnlyList<string> FailedFolders);

    private const string ColumnList =
        "FilePath, FolderPath, DisplayName, TrackNumber, Title, Artist, Album, AlbumArtist, Year, " +
        "DurationSeconds, SampleRate, BitDepth, Bitrate, FileType, LastWriteTimeUtcTicks, FileSizeBytes";

    public static IReadOnlyList<Track> LoadIndexed(string settingsFilePath, IReadOnlyList<string> folderPaths)
    {
        var indexFilePath = IndexFilePath(settingsFilePath);
        if (!File.Exists(indexFilePath))
        {
            return [];
        }

        using var connection = OpenConnection(indexFilePath);
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

                    if (existing.TryGetValue(path, out var indexed)
                        && indexed.LastWriteTimeUtc == lastWriteTimeUtc
                        && indexed.FileSizeBytes == fileSizeBytes)
                    {
                        return (path, indexed.Track, lastWriteTimeUtc, fileSizeBytes, Changed: false);
                    }

                    return (path, MusicLibraryScanner.ReadTrack(path), lastWriteTimeUtc, fileSizeBytes, Changed: true);
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
            UpsertAll(connection, transaction, folderPath, changedEntries);

            var currentPaths = current.Select(c => c.FilePath).ToHashSet();
            var removedPaths = existing.Keys.Where(path => !currentPaths.Contains(path)).ToList();
            DeleteAll(connection, transaction, removedPaths);

            transaction.Commit();
        }

        return (current.Select(c => c.Track).ToList(), false);
    }

    private static void UpsertAll(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string folderPath,
        List<(string FilePath, Track Track, DateTime LastWriteTimeUtc, long FileSizeBytes)> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"""
            INSERT INTO Tracks ({ColumnList})
            VALUES (@FilePath, @FolderPath, @DisplayName, @TrackNumber, @Title, @Artist, @Album, @AlbumArtist, @Year,
                    @DurationSeconds, @SampleRate, @BitDepth, @Bitrate, @FileType, @LastWriteTimeUtcTicks, @FileSizeBytes)
            ON CONFLICT(FilePath) DO UPDATE SET
                FolderPath = excluded.FolderPath, DisplayName = excluded.DisplayName, TrackNumber = excluded.TrackNumber,
                Title = excluded.Title, Artist = excluded.Artist, Album = excluded.Album, AlbumArtist = excluded.AlbumArtist,
                Year = excluded.Year, DurationSeconds = excluded.DurationSeconds, SampleRate = excluded.SampleRate,
                BitDepth = excluded.BitDepth, Bitrate = excluded.Bitrate, FileType = excluded.FileType,
                LastWriteTimeUtcTicks = excluded.LastWriteTimeUtcTicks, FileSizeBytes = excluded.FileSizeBytes
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
                FileSizeBytes         INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_Tracks_FolderPath ON Tracks(FolderPath);
            """;
        command.ExecuteNonQuery();
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
