using System.Text.Json;
using System.Text.Json.Serialization;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Core.Library;

public static partial class SettingsStore
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

    public static LibrarySection? LoadLibrarySection(string settingsFilePath) => Load(settingsFilePath)?.LibrarySection;

    public static void SaveLibrarySection(string settingsFilePath, LibrarySection section) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { LibrarySection = section });

    public static LibraryViewMode? LoadRecentlyAddedViewMode(string settingsFilePath) => Load(settingsFilePath)?.RecentlyAddedViewMode;

    public static void SaveRecentlyAddedViewMode(string settingsFilePath, LibraryViewMode viewMode) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { RecentlyAddedViewMode = viewMode });

    public static AlbumSortMode? LoadAlbumSortMode(string settingsFilePath) => Load(settingsFilePath)?.AlbumSortMode;

    public static void SaveAlbumSortMode(string settingsFilePath, AlbumSortMode sortMode) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { AlbumSortMode = sortMode });

    public static bool? LoadAlbumSortDescending(string settingsFilePath) => Load(settingsFilePath)?.AlbumSortDescending;

    public static void SaveAlbumSortDescending(string settingsFilePath, bool descending) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { AlbumSortDescending = descending });

    public static bool LoadRestoreQueueOnStartup(string settingsFilePath) => Load(settingsFilePath)?.RestoreQueueOnStartup ?? true;

    public static void SaveRestoreQueueOnStartup(string settingsFilePath, bool enabled) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { RestoreQueueOnStartup = enabled });

    public static string? LoadOutputDeviceName(string settingsFilePath) => Load(settingsFilePath)?.OutputDeviceName;

    public static void SaveOutputDeviceName(string settingsFilePath, string? deviceName) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { OutputDeviceName = deviceName });

    public static double? LoadVolume(string settingsFilePath) => Load(settingsFilePath)?.Volume;

    public static void SaveVolume(string settingsFilePath, double volume) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { Volume = volume });

    public static VolumeCurve LoadVolumeCurve(string settingsFilePath) => Load(settingsFilePath)?.VolumeCurve ?? VolumeCurve.Linear;

    public static void SaveVolumeCurve(string settingsFilePath, VolumeCurve curve) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { VolumeCurve = curve });

    public static int LoadSeekStepSeconds(string settingsFilePath) => Load(settingsFilePath)?.SeekStepSeconds ?? 5;

    public static void SaveSeekStepSeconds(string settingsFilePath, int seconds) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { SeekStepSeconds = seconds });

    public static IReadOnlyDictionary<string, string>? LoadShortcutOverrides(string settingsFilePath) => Load(settingsFilePath)?.ShortcutOverrides;

    public static void SaveShortcutOverrides(string settingsFilePath, IReadOnlyDictionary<string, string> overrides) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { ShortcutOverrides = overrides });

    public static AppTheme LoadTheme(string settingsFilePath) => Load(settingsFilePath)?.Theme ?? AppTheme.Dark;

    public static void SaveTheme(string settingsFilePath, AppTheme theme) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { Theme = theme });

    /// <summary>File name (relative to the app's Themes directory) of the selected custom theme.</summary>
    public static string? LoadCustomThemeFileName(string settingsFilePath) => Load(settingsFilePath)?.CustomThemeFileName;

    public static void SaveCustomThemeFileName(string settingsFilePath, string? fileName) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { CustomThemeFileName = fileName });

    public static bool LoadEqualizerEnabled(string settingsFilePath) => Load(settingsFilePath)?.EqualizerEnabled ?? false;

    public static void SaveEqualizerEnabled(string settingsFilePath, bool enabled) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { EqualizerEnabled = enabled });

    public static string? LoadEqualizerPresetName(string settingsFilePath) => Load(settingsFilePath)?.EqualizerPresetName;

    public static void SaveEqualizerPresetName(string settingsFilePath, string presetName) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { EqualizerPresetName = presetName });

    public static IReadOnlyList<float>? LoadEqualizerBandGainsDb(string settingsFilePath) => Load(settingsFilePath)?.EqualizerBandGainsDb;

    public static void SaveEqualizerBandGainsDb(string settingsFilePath, IReadOnlyList<float> gainsDb) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { EqualizerBandGainsDb = gainsDb });

    public static bool LoadUseWaveformSlider(string settingsFilePath) => Load(settingsFilePath)?.UseWaveformSlider ?? false;

    public static void SaveUseWaveformSlider(string settingsFilePath, bool enabled) =>
        Save(settingsFilePath, CurrentOrEmpty(settingsFilePath) with { UseWaveformSlider = enabled });

    private static SettingsData CurrentOrEmpty(string settingsFilePath) =>
        Load(settingsFilePath) ?? new SettingsData();

    private static SettingsData? Load(string settingsFilePath)
    {
        if (!File.Exists(settingsFilePath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(settingsFilePath);
            return JsonSerializer.Deserialize(json, SettingsJsonContext.Default.SettingsData);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            return null;
        }
    }

    private static void Save(string settingsFilePath, SettingsData data)
    {
        var directory = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(settingsFilePath, JsonSerializer.Serialize(data, SettingsJsonContext.Default.SettingsData));
    }

    private sealed record SettingsData
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
        public LibrarySection? LibrarySection { get; init; }
        public AlbumSortMode? AlbumSortMode { get; init; }
        public bool? AlbumSortDescending { get; init; }
        public bool? RestoreQueueOnStartup { get; init; }
        public string? OutputDeviceName { get; init; }
        public double? Volume { get; init; }
        public VolumeCurve? VolumeCurve { get; init; }
        public int? SeekStepSeconds { get; init; }
        public IReadOnlyDictionary<string, string>? ShortcutOverrides { get; init; }
        public AppTheme? Theme { get; init; }
        public string? CustomThemeFileName { get; init; }
        public bool? EqualizerEnabled { get; init; }
        public string? EqualizerPresetName { get; init; }
        public IReadOnlyList<float>? EqualizerBandGainsDb { get; init; }
        public bool? UseWaveformSlider { get; init; }
        public LibraryViewMode? RecentlyAddedViewMode { get; init; }
    }

    [JsonSerializable(typeof(SettingsData))]
    private partial class SettingsJsonContext : JsonSerializerContext
    {
    }
}
