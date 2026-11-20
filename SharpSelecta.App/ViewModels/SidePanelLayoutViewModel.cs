using System.ComponentModel;
using Avalonia.Controls;
using SharpSelecta.App.Styles;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

// The main window's side panel (queue and Now Playing) as Interface settings has it, edits included, so a
// change previews live and Cancel puts it back. The main grid has a column on each side of the library;
// the panel takes the one on its side, and the other shrinks to nothing.
public sealed class SidePanelLayoutViewModel : ViewModelBase
{
    private static readonly string[] SettingsProperties =
    [
        nameof(InterfaceSettingsViewModel.SelectedSidePanelPositionOption),
        nameof(InterfaceSettingsViewModel.SelectedQueuePositionOption),
        nameof(InterfaceSettingsViewModel.ShowNowPlayingArtwork),
        nameof(InterfaceSettingsViewModel.ShowNowPlayingDetails),
    ];

    private static readonly string[] LayoutProperties =
    [
        nameof(IsVisible), nameof(IsOnLeft), nameof(IsOnRight), nameof(Column), nameof(LeftWidth), nameof(RightWidth),
        nameof(IsQueueAtBottom), nameof(NowPlayingDock), nameof(ShowsArtwork), nameof(ShowsDetails), nameof(ShowsNowPlaying),
    ];

    private readonly SettingsStore _settings;
    private readonly InterfaceSettingsViewModel _interface;
    private GridLength _width;

    public SidePanelLayoutViewModel(SettingsStore settings, ThemeLayout layout, InterfaceSettingsViewModel interfaceSettings)
    {
        _settings = settings;
        _interface = interfaceSettings;
        _width = new GridLength(settings.Current.RightColumnWidth ?? layout.RightColumnWidth);
        _interface.PropertyChanged += OnInterfaceSettingsChanged;
    }

    private SidePanelPosition Position => _interface.SelectedSidePanelPositionOption.Position;

    public bool IsVisible => Position != SidePanelPosition.Hidden;

    public bool IsOnLeft => Position == SidePanelPosition.Left;

    public bool IsOnRight => Position == SidePanelPosition.Right;

    public int Column => IsOnLeft ? 0 : 4;

    public GridLength LeftWidth
    {
        get => IsOnLeft ? _width : GridLength.Auto;
        set => SetWidth(IsOnLeft, value);
    }

    public GridLength RightWidth
    {
        get => IsOnRight ? _width : GridLength.Auto;
        set => SetWidth(IsOnRight, value);
    }

    public bool IsQueueAtBottom => _interface.SelectedQueuePositionOption.Position == QueuePosition.Bottom;

    public Dock NowPlayingDock => IsQueueAtBottom ? Dock.Top : Dock.Bottom;

    public bool ShowsArtwork => _interface.ShowNowPlayingArtwork;

    public bool ShowsDetails => _interface.ShowNowPlayingDetails;

    public bool ShowsNowPlaying => ShowsArtwork || ShowsDetails;

    // The splitter only drags the column the panel is in; the empty one on the other side stays put.
    private void SetWidth(bool isPanelColumn, GridLength value)
    {
        if (isPanelColumn && value.IsAbsolute)
            _width = value;
    }

    public void PersistWidth() => _settings.Update(s => s with { RightColumnWidth = _width.Value });

    private void OnInterfaceSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!SettingsProperties.Contains(e.PropertyName))
            return;

        foreach (var name in LayoutProperties)
        {
            OnPropertyChanged(name);
        }
    }
}
