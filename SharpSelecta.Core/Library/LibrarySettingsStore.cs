using System.Text.Json;
using System.Text.Json.Serialization;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Core.Library;

public static partial class LibrarySettingsStore
{
    public static IReadOnlyList<string>? LoadLibraryFolderPaths(string settingsFilePath) => Load(settingsFilePath)?.LibraryFolderPaths;

    public static void SaveLibraryFolderPaths(string settingsFilePath, IReadOnlyList<string> folderPaths) =>
        Save(settingsFilePath, Default(settingsFilePath) with { LibraryFolderPaths = folderPaths });

    public static IReadOnlyDictionary<string, bool>? LoadColumnVisibility(string settingsFilePath) => Load(settingsFilePath)?.Columns;

    public static void SaveColumnVisibility(string settingsFilePath, IReadOnlyDictionary<string, bool> columns) =>
        Save(settingsFilePath, Default(settingsFilePath) with { Columns = columns });

    public static IReadOnlyList<string>? LoadColumnOrder(string settingsFilePath) => Load(settingsFilePath)?.ColumnOrder;

    public static void SaveColumnOrder(string settingsFilePath, IReadOnlyList<string> columnOrder) =>
        Save(settingsFilePath, Default(settingsFilePath) with { ColumnOrder = columnOrder });

    public static double? LoadRightColumnWidth(string settingsFilePath) => Load(settingsFilePath)?.RightColumnWidth;

    public static void SaveRightColumnWidth(string settingsFilePath, double width) =>
        Save(settingsFilePath, Default(settingsFilePath) with { RightColumnWidth = width });

    public static IReadOnlyDictionary<string, double>? LoadColumnWidths(string settingsFilePath) => Load(settingsFilePath)?.ColumnWidths;

    public static void SaveColumnWidths(string settingsFilePath, IReadOnlyDictionary<string, double> columnWidths) =>
        Save(settingsFilePath, Default(settingsFilePath) with { ColumnWidths = columnWidths });

    public static (string PropertyPath, bool Descending)? LoadSort(string settingsFilePath)
    {
        var data = Load(settingsFilePath);
        return data?.SortPropertyPath is { } propertyPath ? (propertyPath, data.SortDescending ?? false) : null;
    }

    public static void SaveSort(string settingsFilePath, string propertyPath, bool descending) =>
        Save(settingsFilePath, Default(settingsFilePath) with { SortPropertyPath = propertyPath, SortDescending = descending });

    public static double? LoadTileSize(string settingsFilePath) => Load(settingsFilePath)?.TileSize;

    public static void SaveTileSize(string settingsFilePath, double tileSize) =>
        Save(settingsFilePath, Default(settingsFilePath) with { TileSize = tileSize });

    public static LibraryViewMode? LoadViewMode(string settingsFilePath) => Load(settingsFilePath)?.ViewMode;

    public static void SaveViewMode(string settingsFilePath, LibraryViewMode viewMode) =>
        Save(settingsFilePath, Default(settingsFilePath) with { ViewMode = viewMode });

    public static AlbumSortMode? LoadAlbumSortMode(string settingsFilePath) => Load(settingsFilePath)?.AlbumSortMode;

    public static void SaveAlbumSortMode(string settingsFilePath, AlbumSortMode sortMode) =>
        Save(settingsFilePath, Default(settingsFilePath) with { AlbumSortMode = sortMode });

    public static bool? LoadAlbumSortDescending(string settingsFilePath) => Load(settingsFilePath)?.AlbumSortDescending;

    public static void SaveAlbumSortDescending(string settingsFilePath, bool descending) =>
        Save(settingsFilePath, Default(settingsFilePath) with { AlbumSortDescending = descending });

    // Defaults to true (opt-out, not opt-in) so a first-ever run behaves the same as every run
    // after it — no separate "first launch" special case needed.
    public static bool LoadRestoreQueueOnStartup(string settingsFilePath) => Load(settingsFilePath)?.RestoreQueueOnStartup ?? true;

    public static void SaveRestoreQueueOnStartup(string settingsFilePath, bool enabled) =>
        Save(settingsFilePath, Default(settingsFilePath) with { RestoreQueueOnStartup = enabled });

    // Null when nothing was ever saved AND when the last saved queue was empty — both cases mean
    // "nothing to restore," so callers don't need to distinguish them.
    public static QueueState? LoadQueueState(string settingsFilePath)
    {
        var data = Load(settingsFilePath);
        return data?.QueueEntries is { Count: > 0 } entries
            ? new QueueState(entries, data.QueueCurrentIndex ?? -1, data.QueuePositionSeconds ?? 0)
            : null;
    }

    public static void SaveQueueState(string settingsFilePath, IReadOnlyList<QueueEntryData> entries, int currentIndex, double positionSeconds) =>
        Save(settingsFilePath, Default(settingsFilePath) with
        {
            QueueEntries = entries,
            QueueCurrentIndex = currentIndex,
            QueuePositionSeconds = positionSeconds,
        });

    public sealed record QueueEntryData(string FilePath, QueueEntrySource Source);

    public sealed record QueueState(IReadOnlyList<QueueEntryData> Entries, int CurrentIndex, double PositionSeconds);

    private static LibrarySettingsData Default(string settingsFilePath) =>
        Load(settingsFilePath) ?? new LibrarySettingsData(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);

    private static LibrarySettingsData? Load(string settingsFilePath)
    {
        if (!File.Exists(settingsFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(settingsFilePath);
            return JsonSerializer.Deserialize(json, LibrarySettingsJsonContext.Default.LibrarySettingsData);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    private static void Save(string settingsFilePath, LibrarySettingsData data)
    {
        var directory = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(settingsFilePath, JsonSerializer.Serialize(data, LibrarySettingsJsonContext.Default.LibrarySettingsData));
    }

    private sealed record LibrarySettingsData(
        IReadOnlyList<string>? LibraryFolderPaths,
        IReadOnlyDictionary<string, bool>? Columns,
        IReadOnlyList<string>? ColumnOrder,
        double? RightColumnWidth,
        IReadOnlyDictionary<string, double>? ColumnWidths,
        string? SortPropertyPath,
        bool? SortDescending,
        double? TileSize,
        LibraryViewMode? ViewMode,
        AlbumSortMode? AlbumSortMode,
        bool? AlbumSortDescending,
        bool? RestoreQueueOnStartup,
        IReadOnlyList<QueueEntryData>? QueueEntries,
        int? QueueCurrentIndex,
        double? QueuePositionSeconds);

    [JsonSerializable(typeof(LibrarySettingsData))]
    private partial class LibrarySettingsJsonContext : JsonSerializerContext
    {
    }
}
