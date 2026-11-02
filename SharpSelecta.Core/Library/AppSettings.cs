using SharpSelecta.Core.Audio;

namespace SharpSelecta.Core.Library;

public sealed record AppSettings
{
    public IReadOnlyList<string>? LibraryFolderPaths { get; init; }
    public IReadOnlyDictionary<string, bool>? Columns { get; init; }
    public IReadOnlyList<string>? ColumnOrder { get; init; }
    public double? RightColumnWidth { get; init; }
    public IReadOnlyDictionary<string, double>? ColumnWidths { get; init; }
    public string? SortPropertyPath { get; init; }
    public bool SortDescending { get; init; }
    public double? TileSize { get; init; }
    public LibraryViewMode ViewMode { get; init; } = LibraryViewMode.TrackList;
    public LibrarySection LibrarySection { get; init; } = LibrarySection.Library;
    public LibraryViewMode RecentlyAddedViewMode { get; init; } = LibraryViewMode.TrackList;
    public string? SelectedPlaylistId { get; init; }
    public AlbumSortMode AlbumSortMode { get; init; } = AlbumSortMode.Title;
    public bool AlbumSortDescending { get; init; }
    public bool RestoreQueueOnStartup { get; init; } = true;
    public string? OutputDeviceName { get; init; }
    public double? Volume { get; init; }
    public VolumeCurve VolumeCurve { get; init; } = VolumeCurve.Linear;
    public int SeekStepSeconds { get; init; } = 5;
    public IReadOnlyDictionary<string, string>? ShortcutOverrides { get; init; }
    public AppTheme Theme { get; init; } = AppTheme.Dark;
    public string? CustomThemeFileName { get; init; }
    public bool EqualizerEnabled { get; init; }
    public string? EqualizerPresetName { get; init; }
    public IReadOnlyList<float>? EqualizerBandGainsDb { get; init; }
    public bool UseWaveformSlider { get; init; }
    public bool AutoDjEnabled { get; init; }

    // The playlist Auto DJ draws from; null means the whole library.
    public string? AutoDjSourcePlaylistId { get; init; }

    public bool DiscordPresenceEnabled { get; init; }
    public bool CheckForUpdatesAutomatically { get; init; } = true;

    public string? FanartApiKey
    {
        get;
        init => field = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
