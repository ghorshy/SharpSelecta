using Avalonia.Controls;
using Avalonia.Input;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class NowPlayingView : UserControl
{
    private AlbumCoverWindow? _albumCoverWindow;

    public NowPlayingView()
    {
        InitializeComponent();
    }

    private void OnArtworkDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not PlaybackControlsViewModel { CurrentTrackArtworkBytes: not null } vm)
            return;

        // Re-activate the existing preview instead of stacking a duplicate one if it's still open.
        if (_albumCoverWindow is not null)
        {
            _albumCoverWindow.Activate();
            return;
        }

        _albumCoverWindow = new AlbumCoverWindow { DataContext = vm };
        _albumCoverWindow.Closed += (_, _) => _albumCoverWindow = null;

        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            _albumCoverWindow.Show(owner);
        }
        else
        {
            _albumCoverWindow.Show();
        }
    }
}
