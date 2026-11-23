using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class InterfaceSettingsView : UserControl
{
    private static readonly DataFormat<PlaybackBarTile> TileFormat =
        DataFormat.CreateInProcessFormat<PlaybackBarTile>("SharpSelecta.PlaybackBarTile");

    public InterfaceSettingsView()
    {
        InitializeComponent();
    }

    private PlaybackBarEditorViewModel? Editor => (DataContext as InterfaceSettingsViewModel)?.PlaybackBarEditor;

    private async void OnTilePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: PlaybackBarTile tile } border || !e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
            return;

        border.Focus();
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(TileFormat, tile));
        await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
    }

    private bool IsInPool(PlaybackBarTile tile) => Editor?.HiddenTiles.Contains(tile) == true;

    // Tiles in the Hidden pool take a drop to hide; a tile on the bar leaves it to the strip below.
    private void OnTileDragOver(object? sender, DragEventArgs e)
    {
        if (sender is not Border { DataContext: PlaybackBarTile target } || !IsInPool(target)
            || e.DataTransfer.TryGetValue(TileFormat) is not { } dragged)
            return;

        e.Handled = true;
        DropIndicator.IsVisible = false;
        e.DragEffects = dragged.IsLocked ? DragDropEffects.None : DragDropEffects.Move;
    }

    private void OnTileDrop(object? sender, DragEventArgs e)
    {
        if (sender is not Border { DataContext: PlaybackBarTile target } || !IsInPool(target)
            || e.DataTransfer.TryGetValue(TileFormat) is not { } dragged || Editor is not { } editor)
            return;

        e.Handled = true;
        if (editor.Hide(dragged.Component))
            FocusTile(dragged);
    }

    // Over a tile or a gap alike, the drop goes to the gap nearest the pointer.
    private void OnStripDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(TileFormat))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        ShowDropIndicator(DropIndex(e));
    }

    private void OnStripDragLeave(object? sender, DragEventArgs e)
    {
        // Leaving one tile for the next bubbles up here too; only leaving the strip hides the line.
        if (sender is Border strip && !new Rect(strip.Bounds.Size).Contains(e.GetPosition(strip)))
            DropIndicator.IsVisible = false;
    }

    private void OnStripDrop(object? sender, DragEventArgs e)
    {
        DropIndicator.IsVisible = false;
        if (e.DataTransfer.TryGetValue(TileFormat) is { } dragged && Editor is { } editor
            && editor.Place(dragged.Component, DropIndex(e)))
        {
            FocusTile(dragged);
        }
    }

    private void OnPoolDragOver(object? sender, DragEventArgs e)
    {
        var canHide = e.DataTransfer.TryGetValue(TileFormat) is { IsLocked: false };
        DropIndicator.IsVisible = false;
        e.DragEffects = canHide ? DragDropEffects.Move : DragDropEffects.None;
        HiddenPoolArea.Classes.Set("drag-over", canHide);
    }

    private void OnAreaDragLeave(object? sender, DragEventArgs e) => (sender as Border)?.Classes.Remove("drag-over");

    private void OnPoolDrop(object? sender, DragEventArgs e)
    {
        HiddenPoolArea.Classes.Remove("drag-over");
        if (e.DataTransfer.TryGetValue(TileFormat) is { } dragged && Editor?.Hide(dragged.Component) == true)
            FocusTile(dragged);
    }

    private void OnTileKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Border { DataContext: PlaybackBarTile tile } || Editor is not { } editor)
            return;

        var inPool = IsInPool(tile);
        var done = (e.Key, e.KeyModifiers, inPool) switch
        {
            (Key.Left, KeyModifiers.Control, false) => editor.MoveBy(tile.Component, -1),
            (Key.Right, KeyModifiers.Control, false) => editor.MoveBy(tile.Component, 1),
            (Key.Delete, KeyModifiers.None, false) => editor.Hide(tile.Component),
            (Key.Insert, KeyModifiers.None, true) => editor.Show(tile.Component),
            _ => (bool?)null,
        };
        if (done is null)
            return;

        e.Handled = true;
        if (done == true)
            FocusTile(tile);
    }

    // How many tiles on the bar have their middle left of the pointer.
    private int DropIndex(DragEventArgs e)
    {
        var x = e.GetPosition(BarStripPanel).X;
        var index = 0;
        for (var i = 0; i < BarStrip.ItemCount; i++)
        {
            if (BarStrip.ContainerFromIndex(i) is { } tile && tile.TranslatePoint(default, BarStripPanel) is { } topLeft
                && x > topLeft.X + tile.Bounds.Width / 2)
            {
                index = i + 1;
            }
        }

        return index;
    }

    // The line sits in the middle of the gap before the tile at `index` (after the last one at the end),
    // over the strip's background.
    private void ShowDropIndicator(int index)
    {
        var after = index >= BarStrip.ItemCount;
        if (BarStrip.ContainerFromIndex(after ? BarStrip.ItemCount - 1 : index) is not { } tile
            || tile.TranslatePoint(default, BarStripPanel) is not { } topLeft)
            return;

        var gap = (BarStrip.ItemsPanelRoot as StackPanel)?.Spacing ?? 0;
        var x = after ? topLeft.X + tile.Bounds.Width + gap / 2 : topLeft.X - gap / 2;
        Canvas.SetLeft(DropIndicator, x - DropIndicator.Width / 2);
        DropIndicator.Height = BarStripPanel.Bounds.Height;
        DropIndicator.IsVisible = true;
    }

    // A moved tile gets a new container once the list has been laid out again; keep the focus on it.
    private void FocusTile(PlaybackBarTile tile) => Dispatcher.UIThread.Post(() =>
    {
        var list = IsInPool(tile) ? HiddenPool : BarStrip;
        var border = list.ContainerFromItem(tile)?.GetVisualDescendants().OfType<Border>()
            .FirstOrDefault(b => b.Classes.Contains("bar-tile"));
        border?.Focus(NavigationMethod.Tab);
    }, DispatcherPriority.Loaded);
}
