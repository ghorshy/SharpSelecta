using System.Text.Json;
using System.Text.Json.Serialization;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Core.Library;

public static partial class LibrarySettingsStore
{
    public static IReadOnlyList<string>? LoadLibraryFolderPaths(string settingsFilePath) => Load(settingsFilePath)?.LibraryFolderPaths;

    public static void SaveLibraryFolderPaths(string settingsFilePath, IReadOnlyList<string> folderPaths) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { LibraryFolderPaths = folderPaths });

    public static IReadOnlyDictionary<string, bool>? LoadColumnVisibility(string settingsFilePath) => Load(settingsFilePath)?.Columns;

    public static void SaveColumnVisibility(string settingsFilePath, IReadOnlyDictionary<string, bool> columns) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { Columns = columns });

    public static IReadOnlyList<string>? LoadColumnOrder(string settingsFilePath) => Load(settingsFilePath)?.ColumnOrder;

    public static void SaveColumnOrder(string settingsFilePath, IReadOnlyList<string> columnOrder) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { ColumnOrder = columnOrder });

    public static double? LoadRightColumnWidth(string settingsFilePath) => Load(settingsFilePath)?.RightColumnWidth;

    public static void SaveRightColumnWidth(string settingsFilePath, double width) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { RightColumnWidth = width });

    public static IReadOnlyDictionary<string, double>? LoadColumnWidths(string settingsFilePath) => Load(settingsFilePath)?.ColumnWidths;

    public static void SaveColumnWidths(string settingsFilePath, IReadOnlyDictionary<string, double> columnWidths) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { ColumnWidths = columnWidths });

    public static (string PropertyPath, bool Descending)? LoadSort(string settingsFilePath)
    {
        var data = Load(settingsFilePath);
        return data?.SortPropertyPath is { } propertyPath ? (propertyPath, data.SortDescending ?? false) : null;
    }

    public static void SaveSort(string settingsFilePath, string propertyPath, bool descending) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { SortPropertyPath = propertyPath, SortDescending = descending });

    public static double? LoadTileSize(string settingsFilePath) => Load(settingsFilePath)?.TileSize;

    public static void SaveTileSize(string settingsFilePath, double tileSize) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { TileSize = tileSize });

    public static LibraryViewMode? LoadViewMode(string settingsFilePath) => Load(settingsFilePath)?.ViewMode;

    public static void SaveViewMode(string settingsFilePath, LibraryViewMode viewMode) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { ViewMode = viewMode });

    public static AlbumSortMode? LoadAlbumSortMode(string settingsFilePath) => Load(settingsFilePath)?.AlbumSortMode;

    public static void SaveAlbumSortMode(string settingsFilePath, AlbumSortMode sortMode) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { AlbumSortMode = sortMode });

    public static bool? LoadAlbumSortDescending(string settingsFilePath) => Load(settingsFilePath)?.AlbumSortDescending;

    public static void SaveAlbumSortDescending(string settingsFilePath, bool descending) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { AlbumSortDescending = descending });

    // Defaults to true (opt-out, not opt-in) so a first-ever run behaves the same as every run
    // after it — no separate "first launch" special case needed.
    public static bool LoadRestoreQueueOnStartup(string settingsFilePath) => Load(settingsFilePath)?.RestoreQueueOnStartup ?? true;

    public static void SaveRestoreQueueOnStartup(string settingsFilePath, bool enabled) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { RestoreQueueOnStartup = enabled });

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
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with
        {
            QueueEntries = entries,
            QueueCurrentIndex = currentIndex,
            QueuePositionSeconds = positionSeconds,
        });

    public sealed record QueueEntryData(string FilePath, QueueEntrySource Source);

    public sealed record QueueState(IReadOnlyList<QueueEntryData> Entries, int CurrentIndex, double PositionSeconds);

    // Null means "system default" — the same meaning IAudioEngine.SetOutputDevice(null) uses.
    public static string? LoadOutputDeviceName(string settingsFilePath) => Load(settingsFilePath)?.OutputDeviceName;

    public static void SaveOutputDeviceName(string settingsFilePath, string? deviceName) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { OutputDeviceName = deviceName });

    public static double? LoadVolume(string settingsFilePath) => Load(settingsFilePath)?.Volume;

    public static void SaveVolume(string settingsFilePath, double volume) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { Volume = volume });

    public static VolumeCurve LoadVolumeCurve(string settingsFilePath) => Load(settingsFilePath)?.VolumeCurve ?? VolumeCurve.Linear;

    public static void SaveVolumeCurve(string settingsFilePath, VolumeCurve curve) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { VolumeCurve = curve });

    // Every Save* above is a read-modify-write of the whole settings file - this is the "read"
    // half, falling back to an empty record on first-ever save.
    private static LibrarySettingsData CurrentOrEmpty(string settingsFilePath) =>
        Load(settingsFilePath) ?? new LibrarySettingsData();

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

    private sealed record LibrarySettingsData
    {
        public IReadOnlyList<string>? LibraryFolderPaths { get; init; }
        public IReadOnlyDictionary<string, bool>? Columns { get; init; }
        public IReadOnlyList<string>? ColumnOrder { get; init; }
        public double? RightColumnWidth { get; init; }
        public IReadOnlyDictionary<string, double>? ColumnWidths { get; init; }
        public string? SortPropertyPath { get; init; }
        public bool? SortDescending { get; init; }
        public double? TileSize { get; init; }
        public LibraryViewMode? ViewMode { get; init; }
        public AlbumSortMode? AlbumSortMode { get; init; }
        public bool? AlbumSortDescending { get; init; }
        public bool? RestoreQueueOnStartup { get; init; }
        public IReadOnlyList<QueueEntryData>? QueueEntries { get; init; }
        public int? QueueCurrentIndex { get; init; }
        public double? QueuePositionSeconds { get; init; }
        public string? OutputDeviceName { get; init; }
        public double? Volume { get; init; }
        public VolumeCurve? VolumeCurve { get; init; }
    }

    [JsonSerializable(typeof(LibrarySettingsData))]
    private partial class LibrarySettingsJsonContext : JsonSerializerContext
    {
    }
}
