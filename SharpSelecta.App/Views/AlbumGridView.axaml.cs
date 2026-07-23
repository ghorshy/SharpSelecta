using Avalonia.Controls;
using Avalonia.Input;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class AlbumGridView : UserControl
{
    public AlbumGridView()
    {
        InitializeComponent();
    }

    private void OnScrollerSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (DataContext is AlbumGridViewModel vm)
        {
            vm.SetViewportWidth(e.NewSize.Width);
        }
    }

    // Ctrl+scroll zooms the tiles; a plain scroll passes through to the ScrollViewer as normal.
    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not AlbumGridViewModel vm || !e.KeyModifiers.HasFlag(KeyModifiers.Control))
            return;

        vm.AdjustTileSize(e.Delta.Y * 10);
        e.Handled = true;
    }

    private void OnExpandedTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: LibraryTrackViewModel item })
        {
            item.Library.PlayNowCommand.Execute(item.Track);
        }
    }
}
