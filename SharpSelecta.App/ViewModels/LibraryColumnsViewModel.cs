using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed partial class LibraryColumnViewModel(string key, string header, bool isVisible) : ViewModelBase
{
    public string Key { get; } = key;

    public string Header { get; } = header;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = isVisible;
}

public sealed class LibraryColumnsViewModel
{
    private readonly SettingsStore _settings;
    private readonly Dictionary<string, LibraryColumnViewModel> _byKey;

    public IReadOnlyList<LibraryColumnViewModel> All { get; }

    public LibraryColumnViewModel this[string key] => _byKey[key];

    // Lets the Library and Recently Added grids pick up each other's column order/width changes live.
    public event EventHandler? LayoutChanged;

    public LibraryColumnsViewModel(SettingsStore settings)
    {
        _settings = settings;
        var saved = settings.Current.Columns is { } columns && columns.Values.Any(visible => visible) ? columns : null;

        // Columns added later are hidden until switched on (right-click the header), so an existing layout doesn't change.
        All =
        [
            Column("TrackNumber", Strings.ColumnTrack, true),
            Column("DiscNumber", Strings.ColumnDisc, false),
            Column("Title", Strings.ColumnTitle, true),
            Column("Artist", Strings.ColumnArtist, true),
            Column("AlbumArtist", Strings.ColumnAlbumArtist, false),
            Column("Album", Strings.ColumnAlbum, true),
            Column("Genre", Strings.ColumnGenre, false),
            Column("Length", Strings.ColumnLength, true),
            Column("SampleRate", Strings.ColumnSampleRate, true),
            Column("BitDepth", Strings.ColumnBitDepth, true),
            Column("Bitrate", Strings.ColumnBitrate, true),
            Column("FileType", Strings.ColumnFileType, true),
            Column("Year", Strings.ColumnYear, true),
        ];
        _byKey = All.ToDictionary(c => c.Key);

        foreach (var column in All)
        {
            column.PropertyChanged += OnColumnPropertyChanged;
        }

        LibraryColumnViewModel Column(string key, string header, bool visibleByDefault) =>
            new(key, header, saved is not null && saved.TryGetValue(key, out var visible) ? visible : visibleByDefault);
    }

    public void NotifyLayoutChanged() => LayoutChanged?.Invoke(this, EventArgs.Empty);

    private void OnColumnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LibraryColumnViewModel.IsVisible) || sender is not LibraryColumnViewModel column)
            return;

        if (!column.IsVisible && !All.Any(c => c.IsVisible))
        {
            column.IsVisible = true;
            return;
        }

        _settings.Update(s => s with { Columns = All.ToDictionary(c => c.Key, c => c.IsVisible) });
    }
}
