using System.Text.Json;
using System.Text.Json.Serialization;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Core.Library;

public static partial class SettingsStore
{
    public static IReadOnlyList<string>? LoadLibraryFolderPaths(string settingsFilePath) => Load(settingsFilePath)?.LibraryFolderPaths;

    public static void SaveLibraryFolderPaths(string settingsFilePath, IReadOnlyList<string> folderPaths) =>
        Update(settingsFilePath, data => data with { LibraryFolderPaths = folderPaths });

    public static IReadOnlyDictionary<string, bool>? LoadColumnVisibility(string settingsFilePath) => Load(settingsFilePath)?.Columns;

    public static void SaveColumnVisibility(string settingsFilePath, IReadOnlyDictionary<string, bool> columns) =>
        Update(settingsFilePath, data => data with { Columns = columns });

    public static IReadOnlyList<string>? LoadColumnOrder(string settingsFilePath) => Load(settingsFilePath)?.ColumnOrder;

    public static void SaveColumnOrder(string settingsFilePath, IReadOnlyList<string> columnOrder) =>
        Update(settingsFilePath, data => data with { ColumnOrder = columnOrder });

    public static double? LoadRightColumnWidth(string settingsFilePath) => Load(settingsFilePath)?.RightColumnWidth;

    public static void SaveRightColumnWidth(string settingsFilePath, double width) =>
        Update(settingsFilePath, data => data with { RightColumnWidth = width });

    public static IReadOnlyDictionary<string, double>? LoadColumnWidths(string settingsFilePath) => Load(settingsFilePath)?.ColumnWidths;

    public static void SaveColumnWidths(string settingsFilePath, IReadOnlyDictionary<string, double> columnWidths) =>
        Update(settingsFilePath, data => data with { ColumnWidths = columnWidths });

    public static (string PropertyPath, bool Descending)? LoadSort(string settingsFilePath)
    {
        var data = Load(settingsFilePath);
        return data?.SortPropertyPath is { } propertyPath ? (propertyPath, data.SortDescending ?? false) : null;
    }

    public static void SaveSort(string settingsFilePath, string propertyPath, bool descending) =>
        Update(settingsFilePath, data => data with { SortPropertyPath = propertyPath, SortDescending = descending });

    public static double? LoadTileSize(string settingsFilePath) => Load(settingsFilePath)?.TileSize;

    public static void SaveTileSize(string settingsFilePath, double tileSize) =>
        Update(settingsFilePath, data => data with { TileSize = tileSize });

    public static LibraryViewMode? LoadViewMode(string settingsFilePath) => Load(settingsFilePath)?.ViewMode;

    public static void SaveViewMode(string settingsFilePath, LibraryViewMode viewMode) =>
        Update(settingsFilePath, data => data with { ViewMode = viewMode });

    public static LibrarySection? LoadLibrarySection(string settingsFilePath) => Load(settingsFilePath)?.LibrarySection;

    public static void SaveLibrarySection(string settingsFilePath, LibrarySection section) =>
        Update(settingsFilePath, data => data with { LibrarySection = section });

    public static LibraryViewMode? LoadRecentlyAddedViewMode(string settingsFilePath) => Load(settingsFilePath)?.RecentlyAddedViewMode;

    public static void SaveRecentlyAddedViewMode(string settingsFilePath, LibraryViewMode viewMode) =>
        Update(settingsFilePath, data => data with { RecentlyAddedViewMode = viewMode });

    public static string? LoadSelectedPlaylistId(string settingsFilePath) => Load(settingsFilePath)?.SelectedPlaylistId;

    public static void SaveSelectedPlaylistId(string settingsFilePath, string? playlistId) =>
        Update(settingsFilePath, data => data with { SelectedPlaylistId = playlistId });

    public static AlbumSortMode? LoadAlbumSortMode(string settingsFilePath) => Load(settingsFilePath)?.AlbumSortMode;

    public static void SaveAlbumSortMode(string settingsFilePath, AlbumSortMode sortMode) =>
        Update(settingsFilePath, data => data with { AlbumSortMode = sortMode });

    public static bool? LoadAlbumSortDescending(string settingsFilePath) => Load(settingsFilePath)?.AlbumSortDescending;

    public static void SaveAlbumSortDescending(string settingsFilePath, bool descending) =>
        Update(settingsFilePath, data => data with { AlbumSortDescending = descending });

    public static bool LoadRestoreQueueOnStartup(string settingsFilePath) => Load(settingsFilePath)?.RestoreQueueOnStartup ?? true;

    public static void SaveRestoreQueueOnStartup(string settingsFilePath, bool enabled) =>
        Update(settingsFilePath, data => data with { RestoreQueueOnStartup = enabled });

    public static string? LoadOutputDeviceName(string settingsFilePath) => Load(settingsFilePath)?.OutputDeviceName;

    public static void SaveOutputDeviceName(string settingsFilePath, string? deviceName) =>
        Update(settingsFilePath, data => data with { OutputDeviceName = deviceName });

    public static double? LoadVolume(string settingsFilePath) => Load(settingsFilePath)?.Volume;

    public static void SaveVolume(string settingsFilePath, double volume) =>
        Update(settingsFilePath, data => data with { Volume = volume });

    public static VolumeCurve LoadVolumeCurve(string settingsFilePath) => Load(settingsFilePath)?.VolumeCurve ?? VolumeCurve.Linear;

    public static void SaveVolumeCurve(string settingsFilePath, VolumeCurve curve) =>
        Update(settingsFilePath, data => data with { VolumeCurve = curve });

    public static int LoadSeekStepSeconds(string settingsFilePath) => Load(settingsFilePath)?.SeekStepSeconds ?? 5;

    public static void SaveSeekStepSeconds(string settingsFilePath, int seconds) =>
        Update(settingsFilePath, data => data with { SeekStepSeconds = seconds });

    public static IReadOnlyDictionary<string, string>? LoadShortcutOverrides(string settingsFilePath) => Load(settingsFilePath)?.ShortcutOverrides;

    public static void SaveShortcutOverrides(string settingsFilePath, IReadOnlyDictionary<string, string> overrides) =>
        Update(settingsFilePath, data => data with { ShortcutOverrides = overrides });

    public static AppTheme LoadTheme(string settingsFilePath) => Load(settingsFilePath)?.Theme ?? AppTheme.Dark;

    public static void SaveTheme(string settingsFilePath, AppTheme theme) =>
        Update(settingsFilePath, data => data with { Theme = theme });

    /// <summary>File name (relative to the app's Themes directory) of the selected custom theme.</summary>
    public static bool LoadAutoDjEnabled(string settingsFilePath) => Load(settingsFilePath)?.AutoDjEnabled ?? false;

    public static void SaveAutoDjEnabled(string settingsFilePath, bool enabled) =>
        Update(settingsFilePath, data => data with { AutoDjEnabled = enabled });

    // The playlist Auto DJ draws from; null means the whole library.
    public static string? LoadAutoDjSourcePlaylistId(string settingsFilePath) => Load(settingsFilePath)?.AutoDjSourcePlaylistId;

    public static void SaveAutoDjSourcePlaylistId(string settingsFilePath, string? playlistId) =>
        Update(settingsFilePath, data => data with { AutoDjSourcePlaylistId = playlistId });

    public static bool LoadDiscordPresenceEnabled(string settingsFilePath) => Load(settingsFilePath)?.DiscordPresenceEnabled ?? false;

    public static void SaveDiscordPresenceEnabled(string settingsFilePath, bool enabled) =>
        Update(settingsFilePath, data => data with { DiscordPresenceEnabled = enabled });

    public static bool LoadCheckForUpdatesAutomatically(string settingsFilePath) => Load(settingsFilePath)?.CheckForUpdatesAutomatically ?? true;

    public static void SaveCheckForUpdatesAutomatically(string settingsFilePath, bool enabled) =>
        Update(settingsFilePath, data => data with { CheckForUpdatesAutomatically = enabled });

    public static string? LoadFanartApiKey(string settingsFilePath) => Load(settingsFilePath)?.FanartApiKey;

    public static void SaveFanartApiKey(string settingsFilePath, string? apiKey) =>
        Update(settingsFilePath, data => data with { FanartApiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey.Trim() });

    public static string? LoadCustomThemeFileName(string settingsFilePath) => Load(settingsFilePath)?.CustomThemeFileName;

    public static void SaveCustomThemeFileName(string settingsFilePath, string? fileName) =>
        Update(settingsFilePath, data => data with { CustomThemeFileName = fileName });

    public static bool LoadEqualizerEnabled(string settingsFilePath) => Load(settingsFilePath)?.EqualizerEnabled ?? false;

    public static void SaveEqualizerEnabled(string settingsFilePath, bool enabled) =>
        Update(settingsFilePath, data => data with { EqualizerEnabled = enabled });

    public static string? LoadEqualizerPresetName(string settingsFilePath) => Load(settingsFilePath)?.EqualizerPresetName;

    public static void SaveEqualizerPresetName(string settingsFilePath, string presetName) =>
        Update(settingsFilePath, data => data with { EqualizerPresetName = presetName });

    public static IReadOnlyList<float>? LoadEqualizerBandGainsDb(string settingsFilePath) => Load(settingsFilePath)?.EqualizerBandGainsDb;

    public static void SaveEqualizerBandGainsDb(string settingsFilePath, IReadOnlyList<float> gainsDb) =>
        Update(settingsFilePath, data => data with { EqualizerBandGainsDb = gainsDb });

    public static bool LoadUseWaveformSlider(string settingsFilePath) => Load(settingsFilePath)?.UseWaveformSlider ?? false;

    public static void SaveUseWaveformSlider(string settingsFilePath, bool enabled) =>
        Update(settingsFilePath, data => data with { UseWaveformSlider = enabled });

    private static readonly Lock FileLock = new();

    private static SettingsData? Load(string settingsFilePath)
    {
        lock (FileLock)
        {
            if (!File.Exists(settingsFilePath))
            {
                return null;
            }

            try
            {
                return Deserialize(settingsFilePath);
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                return null;
            }
        }
    }

    // An unreadable file throws instead of being overwritten with defaults; a corrupt one is kept aside.
    private static void Update(string settingsFilePath, Func<SettingsData, SettingsData> update)
    {
        lock (FileLock)
        {
            var current = new SettingsData();
            if (File.Exists(settingsFilePath))
            {
                try
                {
                    current = Deserialize(settingsFilePath) ?? current;
                }
                catch (JsonException)
                {
                    File.Move(settingsFilePath, settingsFilePath + ".corrupt", overwrite: true);
                }
            }

            Write(settingsFilePath, update(current));
        }
    }

    private static SettingsData? Deserialize(string settingsFilePath) =>
        JsonSerializer.Deserialize(File.ReadAllText(settingsFilePath), SettingsJsonContext.Default.SettingsData);

    private static void Write(string settingsFilePath, SettingsData data)
    {
        var directory = Path.GetDirectoryName(settingsFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{settingsFilePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(tempPath, JsonSerializer.Serialize(data, SettingsJsonContext.Default.SettingsData));
            File.Move(tempPath, settingsFilePath, overwrite: true);
        }
        finally
        {
            File.Delete(tempPath);
        }
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
        public string? SelectedPlaylistId { get; init; }
        public string? FanartApiKey { get; init; }
        public bool? DiscordPresenceEnabled { get; init; }
        public bool? AutoDjEnabled { get; init; }
        public string? AutoDjSourcePlaylistId { get; init; }
        public bool? CheckForUpdatesAutomatically { get; init; }
    }

    [JsonSerializable(typeof(SettingsData))]
    private partial class SettingsJsonContext : JsonSerializerContext
    {
    }
}
