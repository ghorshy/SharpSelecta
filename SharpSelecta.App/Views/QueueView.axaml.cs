using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class QueueView : UserControl
{
    private static readonly DataFormat<QueueEntryViewModel> DragEntryFormat =
        DataFormat.CreateInProcessFormat<QueueEntryViewModel>("SharpSelecta.QueueEntry");

    private const double DragThreshold = 4;

    private PointerPressedEventArgs? _pressedArgs;
    private ListBoxItem? _pressedContainer;
    private Point _pressedPoint;
    private ListBoxItem? _dragOverContainer;
    private bool _dragOverIsEnd;

    public QueueView()
    {
        InitializeComponent();

        QueueList.AddHandler(InputElement.PointerPressedEvent, OnEntryPointerPressed, handledEventsToo: true);
        QueueList.AddHandler(InputElement.PointerMovedEvent, OnEntryPointerMoved, handledEventsToo: true);
        QueueList.AddHandler(InputElement.PointerReleasedEvent, OnEntryPointerReleased, handledEventsToo: true);
        QueueList.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        QueueList.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        QueueList.AddHandler(DragDrop.DropEvent, OnDrop);
    }

    private void OnEntryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: QueueEntryViewModel item } && DataContext is QueueViewModel vm)
        {
            vm.PlayEntryCommand.Execute(item);
        }
    }

    private void OnEntryPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(QueueList).Properties.IsLeftButtonPressed || e.Source is not Visual source)
            return;

        _pressedContainer = source.FindAncestorOfType<ListBoxItem>(includeSelf: true);
        _pressedArgs = e;
        _pressedPoint = e.GetPosition(QueueList);
    }

    private async void OnEntryPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pressedContainer is not { DataContext: QueueEntryViewModel item } container || _pressedArgs is null)
            return;

        if (!e.GetCurrentPoint(QueueList).Properties.IsLeftButtonPressed)
        {
            ClearPressedState();
            return;
        }

        if (Point.Distance(e.GetPosition(QueueList), _pressedPoint) < DragThreshold)
            return;

        var pressedArgs = _pressedArgs;
        ClearPressedState();

        container.Classes.Add("dragging");
        try
        {
            var dataTransfer = new DataTransfer();
            dataTransfer.Add(DataTransferItem.Create(DragEntryFormat, item));
            await DragDrop.DoDragDropAsync(pressedArgs, dataTransfer, DragDropEffects.Move);
        }
        catch (Exception ex)
        {
            (DataContext as QueueViewModel)?.ReportDragReorderFailure(ex);
        }
        finally
        {
            container.Classes.Remove("dragging");
            ClearDragOverHighlight();
        }
    }

    private void OnEntryPointerReleased(object? sender, PointerReleasedEventArgs e) => ClearPressedState();

    private void ClearPressedState()
    {
        _pressedContainer = null;
        _pressedArgs = null;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DragEntryFormat) && !e.DataTransfer.Contains(TrackDrag.Format))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = e.DataTransfer.Contains(DragEntryFormat) ? DragDropEffects.Move : DragDropEffects.Copy;

        var container = e.Source is Visual source ? source.FindAncestorOfType<ListBoxItem>(includeSelf: true) : null;
        var isEnd = false;
        if (container is null && QueueList.ItemCount > 0)
        {
            container = QueueList.ContainerFromIndex(QueueList.ItemCount - 1) as ListBoxItem;
            isEnd = true;
        }

        if (container == _dragOverContainer && isEnd == _dragOverIsEnd)
            return;

        ClearDragOverHighlight();
        _dragOverContainer = container;
        _dragOverIsEnd = isEnd;
        _dragOverContainer?.Classes.Add(isEnd ? "drag-over-end" : "drag-over");
    }

    private void OnDragLeave(object? sender, DragEventArgs e) => ClearDragOverHighlight();

    private void ClearDragOverHighlight()
    {
        if (_dragOverContainer is not null)
        {
            _dragOverContainer.Classes.Remove("drag-over");
            _dragOverContainer.Classes.Remove("drag-over-end");
        }

        _dragOverContainer = null;
        _dragOverIsEnd = false;
    }

    // Dropped on an entry: before it. Below the last one or into an empty queue: at the end.
    private async void OnDrop(object? sender, DragEventArgs e)
    {
        ClearDragOverHighlight();

        if (DataContext is not QueueViewModel vm)
            return;

        QueueEntryViewModel? targetItem = null;
        if (e.Source is Visual targetSource && targetSource.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { } targetContainer)
        {
            targetItem = targetContainer.DataContext as QueueEntryViewModel;
        }

        if (e.DataTransfer.TryGetValue(DragEntryFormat) is { } draggedItem)
        {
            vm.MoveEntry(draggedItem, targetItem);
        }
        else if (e.DataTransfer.TryGetValue(TrackDrag.Format) is { Count: > 0 } tracks)
        {
            try
            {
                await vm.InsertTracks(tracks, targetItem);
            }
            catch (Exception ex)
            {
                vm.ReportDragReorderFailure(ex);
            }
        }
    }

    // The queue has no library of its own; the window's MainWindowViewModel owns it.
    private async void OnPropertiesClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: QueueEntryViewModel item } &&
            TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel { Library: var library })
        {
            await TrackPropertiesWindow.ShowAsync(this, library, item.Entry.Track);
        }
    }
}
