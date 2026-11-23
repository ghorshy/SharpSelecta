using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

public sealed partial class TrackListView : UserControl
{
    public static readonly StyledProperty<IEnumerable<LibraryTrackViewModel>?> ItemsSourceProperty =
        AvaloniaProperty.Register<TrackListView, IEnumerable<LibraryTrackViewModel>?>(nameof(ItemsSource));

    public static readonly StyledProperty<bool> AllowReorderAndSortProperty =
        AvaloniaProperty.Register<TrackListView, bool>(nameof(AllowReorderAndSort), true);

    public static readonly StyledProperty<bool> AllowRowReorderProperty =
        AvaloniaProperty.Register<TrackListView, bool>(nameof(AllowRowReorder));

    public IEnumerable<LibraryTrackViewModel>? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    // Column visibility/width and per-row selection/commands are still fully shared and active
    // regardless of this flag - only header-click sort and drag-to-reorder are gated, since a
    // Recently Added or playlist grid's order must not be user-overridable.
    public bool AllowReorderAndSort
    {
        get => GetValue(AllowReorderAndSortProperty);
        set => SetValue(AllowReorderAndSortProperty, value);
    }

    // Independent of AllowReorderAndSort: a playlist has a fixed manual order (no column sort)
    // but its rows can be dragged to a new place.
    public bool AllowRowReorder
    {
        get => GetValue(AllowRowReorderProperty);
        set => SetValue(AllowRowReorderProperty, value);
    }

    private bool _columnWidthsDirty;
    private readonly List<LibraryTrackViewModel> _orderedSelection = [];
    private IReadOnlyList<LibraryTrackViewModel>? _draggedRows;
    private LibraryTrackViewModel? _collapseSelectionTo;

    // The grid sorts by property path (Track.Title, ...) through reflection, which NativeAOT would trim away.
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(LibraryTrackViewModel))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Track))]
    public TrackListView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        foreach (var column in TracksGrid.Columns)
        {
            column.PropertyChanged += OnColumnPropertyChanged;
        }

        // The theme's widths can only be looked up once the view is in a window.
        AttachedToLogicalTree += (_, _) => ApplyColumnLayout(DataContext as LibraryViewModel);

        TracksGrid.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, handledEventsToo: true);
        TracksGrid.AddHandler(InputElement.PointerPressedEvent, OnPressedKeepSelection, RoutingStrategies.Tunnel);
        TracksGrid.AddHandler(InputElement.PointerPressedEvent, OnColumnBorderDoubleClick, RoutingStrategies.Tunnel, handledEventsToo: true);
        TracksGrid.AddHandler(InputElement.PointerReleasedEvent, OnReleasedCollapseSelection, handledEventsToo: true);
        var drag = new TrackDragSource(TracksGrid, DraggedTracks)
        {
            OnError = ex => (DataContext as LibraryViewModel)?.ReportDragFailure(ex),
        };
        drag.Starting += (_, source) =>
        {
            _collapseSelectionTo = null;
            _draggedRows = AllowRowReorder ? DraggedRows(source) : null;
        };
        drag.Finished += (_, _) =>
        {
            _draggedRows = null;
            RowDropIndicator.IsVisible = false;
        };
        DragDrop.SetAllowDrop(TracksGrid, true);
        TracksGrid.AddHandler(DragDrop.DragOverEvent, OnRowDragOver);
        TracksGrid.AddHandler(DragDrop.DragLeaveEvent, OnRowDragLeave);
        TracksGrid.AddHandler(DragDrop.DropEvent, OnRowDrop);
        TracksGrid.Sorting += (_, _) => Dispatcher.UIThread.Post(SaveCurrentSort, DispatcherPriority.Background);
        TracksGrid.SelectionChanged += OnTracksGridSelectionChanged;
    }

    private static LibraryTrackViewModel? RowItem(object? source) =>
        (source as Visual)?.FindAncestorOfType<DataGridRow>(includeSelf: true)?.DataContext as LibraryTrackViewModel;

    // A press normally collapses a multi-selection to the pressed row, which would leave only that
    // row to drag; on a selected row it waits for the release instead, and a drag keeps them all.
    private void OnPressedKeepSelection(object? sender, PointerPressedEventArgs e)
    {
        _collapseSelectionTo = null;
        if (!e.GetCurrentPoint(TracksGrid).Properties.IsLeftButtonPressed || e.KeyModifiers != KeyModifiers.None ||
            e.ClickCount > 1 || TracksGrid.SelectedItems.Count < 2)
            return;

        if (RowItem(e.Source) is { } item && TracksGrid.SelectedItems.Contains(item))
        {
            _collapseSelectionTo = item;
            e.Handled = true;
        }
    }

    private void OnReleasedCollapseSelection(object? sender, PointerReleasedEventArgs e)
    {
        if (_collapseSelectionTo is { } item)
        {
            _collapseSelectionTo = null;
            TracksGrid.SelectedItem = item;
        }
    }

    // The selection in click order when the pressed row is part of it, else just that row.
    private IReadOnlyList<Track>? DraggedTracks(Visual source)
    {
        if (source.FindAncestorOfType<DataGridColumnHeader>(includeSelf: true) is not null || RowItem(source) is not { IsMissing: false } item)
            return null;

        return TracksGrid.SelectedItems.Contains(item) && _orderedSelection.Count > 1
            ? [.. _orderedSelection.Where(t => !t.IsMissing).Select(t => t.Track)]
            : [item.Track];
    }

    private void OnLoadingRow(object? sender, DataGridRowEventArgs e) =>
        e.Row.Classes.Set("missing", e.Row.DataContext is LibraryTrackViewModel { IsMissing: true });

    // DataGrid.SelectedItems is ordered by the underlying list, not by click order - track
    // click order ourselves so Play Next/Add to Queue can act on it in the order selected.
    private void OnTracksGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach (var removed in e.RemovedItems)
        {
            if (removed is LibraryTrackViewModel removedItem)
            {
                _orderedSelection.Remove(removedItem);
            }
        }

        foreach (var added in e.AddedItems)
        {
            if (added is LibraryTrackViewModel addedItem)
            {
                _orderedSelection.Add(addedItem);
            }
        }

        if (DataContext is LibraryViewModel vm)
        {
            vm.SetSelectedTracksInOrder([.. _orderedSelection.Select(item => item.Track)]);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not LibraryViewModel vm)
            return;

        ApplyColumnLayout(vm);
        vm.Tracks.CollectionChanged += (_, e) =>
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
                ApplySavedSort(vm);
        };
        vm.Columns.LayoutChanged += (_, _) => ApplyColumnLayout(vm);
        vm.RevealTrackRequested += (_, item) => Dispatcher.UIThread.Post(() => Reveal(item), DispatcherPriority.Loaded);
    }

    // Every list shares the LibraryViewModel; only the one on screen that lists the track reacts.
    private void Reveal(LibraryTrackViewModel item)
    {
        if (!IsEffectivelyVisible || ItemsSource?.Contains(item) != true)
            return;

        TracksGrid.SelectedItem = item;
        TracksGrid.ScrollIntoView(item, null);
        TracksGrid.Focus();
    }

    // Each column gets the width the user dragged it to, else the theme's (Size.Column.<Tag>). A saved
    // width too small to show anything is ignored, so a bad settings file can't collapse the list.
    private void ApplyColumnLayout(LibraryViewModel? vm)
    {
        var saved = vm?.Settings.Current;
        if (saved?.ColumnOrder is { } order)
        {
            var displayIndexByKey = order.Select((key, index) => (key, index)).ToDictionary(x => x.key, x => x.index);
            ApplyToTaggedColumns(displayIndexByKey, (column, index) => column.DisplayIndex = index);
        }

        foreach (var column in TracksGrid.Columns)
        {
            if (column.Tag is not string key)
                continue;

            if (saved?.ColumnWidths is { } widths && widths.TryGetValue(key, out var width) && IsUsableWidth(width, TracksGrid.MinColumnWidth))
                column.Width = new DataGridLength(width);
            else if (this.TryFindResource($"Size.Column.{key}", out var themeWidth) && themeWidth is double pixels)
                column.Width = new DataGridLength(pixels);
        }

        _columnWidthsDirty = false;
    }

    public static bool IsUsableWidth(double width, double minimum) => double.IsFinite(width) && width >= minimum;

    // How far either side of a header border the DataGrid's own resize grip reaches.
    private const double ColumnBorderGrip = 5;

    // As in Excel: a double-click on a column's right border fits it to its widest text. The left border
    // belongs to the column before it. The width is saved on release, like a drag.
    private void OnColumnBorderDoubleClick(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount != 2 || (e.Source as Visual)?.FindAncestorOfType<DataGridColumnHeader>(includeSelf: true) is not { } header ||
            DataGridColumn.GetColumnContainingElement(header) is not { } column)
            return;

        var x = e.GetPosition(header).X;
        var target = x >= header.Bounds.Width - ColumnBorderGrip ? column
            : x <= ColumnBorderGrip ? TracksGrid.Columns.Where(c => c.IsVisible && c.DisplayIndex < column.DisplayIndex).MaxBy(c => c.DisplayIndex)
            : null;
        if (target is null || DataGridColumnFitter.FitWidth(TracksGrid, target) is not { } width)
            return;

        target.Width = new DataGridLength(Math.Max(width, TracksGrid.MinColumnWidth));
        e.Handled = true;
    }

    private void ApplyToTaggedColumns<T>(IReadOnlyDictionary<string, T> valuesByKey, Action<DataGridColumn, T> apply)
    {
        foreach (var column in TracksGrid.Columns)
        {
            if (column.Tag is string key && valuesByKey.TryGetValue(key, out var value))
            {
                apply(column, value);
            }
        }
    }

    private void ApplySavedSort(LibraryViewModel vm)
    {
        if (!AllowReorderAndSort)
            return;

        var saved = vm.Settings.Current;
        if (saved.SortPropertyPath is not { } propertyPath || TracksGrid.CollectionView is not { } collectionView)
            return;

        var direction = saved.SortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        collectionView.SortDescriptions.Clear();
        collectionView.SortDescriptions.Add(DataGridSortDescription.FromPath(propertyPath, direction));
    }

    private void OnColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (!AllowReorderAndSort || DataContext is not LibraryViewModel vm)
            return;

        var orderedKeys = TracksGrid.Columns
            .OrderBy(c => c.DisplayIndex)
            .Select(c => c.Tag as string)
            .OfType<string>()
            .ToList();

        vm.Settings.Update(s => s with { ColumnOrder = orderedKeys });
        vm.Columns.NotifyLayoutChanged();
    }

    private void OnColumnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != DataGridColumn.WidthProperty)
            return;

        _columnWidthsDirty = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (DataContext is not LibraryViewModel vm)
            return;

        if (_columnWidthsDirty)
        {
            _columnWidthsDirty = false;

            // An Auto column's Width.Value is 1, not its width; what's drawn is ActualWidth.
            var widths = new Dictionary<string, double>(vm.Settings.Current.ColumnWidths ?? new Dictionary<string, double>());
            foreach (var column in TracksGrid.Columns)
            {
                var width = column.Width.IsAbsolute ? column.Width.Value : column.ActualWidth;
                if (column.Tag is string key && IsUsableWidth(width, TracksGrid.MinColumnWidth))
                    widths[key] = width;
            }

            vm.Settings.Update(s => s with { ColumnWidths = widths });
            vm.Columns.NotifyLayoutChanged();
        }
    }

    private void SaveCurrentSort()
    {
        if (!AllowReorderAndSort || DataContext is not LibraryViewModel vm || TracksGrid.CollectionView?.SortDescriptions.FirstOrDefault() is not { } sortDescription)
            return;

        vm.Settings.Update(s => s with
        {
            SortPropertyPath = sortDescription.PropertyPath,
            SortDescending = sortDescription.Direction == ListSortDirection.Descending,
        });
    }

    private async void OnTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<DataGridColumnHeader>() is not null)
        {
            return;
        }

        if (sender is DataGrid { SelectedItem: LibraryTrackViewModel item } grid && !item.IsMissing)
        {
            // The CollectionView is the list as shown - sorted and filtered - which Play from here follows.
            var shown = (grid.CollectionView ?? grid.ItemsSource)?.OfType<LibraryTrackViewModel>().ToList() ?? [item];
            await item.Library.StartTrackAsync(item, shown);
        }
    }

    private IReadOnlyList<LibraryTrackViewModel>? DraggedRows(Visual source) =>
        RowItem(source) is not { } item ? null
        : TracksGrid.SelectedItems.Contains(item) && _orderedSelection.Count > 1 ? [.. _orderedSelection]
        : [item];

    // Only a playlist's own rows dropped back onto it: the line goes above or below the row under the
    // pointer, or after the last row below them, and the list scrolls when the pointer nears an edge.
    private void OnRowDragOver(object? sender, DragEventArgs e)
    {
        if (_draggedRows is null || DataContext is not LibraryViewModel vm)
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
        if (DropTarget(e, vm.Playlists.Tracks) is not var (row, after, _))
        {
            RowDropIndicator.IsVisible = false;
            return;
        }

        ShowRowDropIndicator(row, after);
        ScrollNearEdge(row, vm.Playlists.Tracks);
    }

    private void OnRowDragLeave(object? sender, DragEventArgs e) => RowDropIndicator.IsVisible = false;

    private void OnRowDrop(object? sender, DragEventArgs e)
    {
        RowDropIndicator.IsVisible = false;
        if (_draggedRows is not { } rows || DataContext is not LibraryViewModel vm
            || DropTarget(e, vm.Playlists.Tracks) is not var (_, _, index))
            return;

        e.DragEffects = DragDropEffects.Move;
        e.Handled = true;
        if (!vm.Playlists.MoveTracks(rows, index))
            return;

        TracksGrid.SelectedItems.Clear();
        foreach (var row in rows)
        {
            TracksGrid.SelectedItems.Add(row);
        }
    }

    // The row the line is drawn at, whether it goes below it, and the index the rows are put before.
    private (DataGridRow Row, bool After, int Index)? DropTarget(DragEventArgs e, IList<LibraryTrackViewModel> tracks)
    {
        if ((e.Source as Visual)?.FindAncestorOfType<DataGridRow>(includeSelf: true) is { DataContext: LibraryTrackViewModel item } row)
        {
            var after = e.GetPosition(row).Y > row.Bounds.Height / 2;
            return (row, after, tracks.IndexOf(item) + (after ? 1 : 0));
        }

        // Below the rows: after the last one, if it's on screen.
        if (tracks.Count > 0 && RealizedRows().FirstOrDefault(r => ReferenceEquals(r.DataContext, tracks[^1])) is { } last
            && e.GetPosition(last).Y > 0)
            return (last, true, tracks.Count);

        return null;
    }

    private IEnumerable<DataGridRow> RealizedRows() => TracksGrid.GetVisualDescendants().OfType<DataGridRow>().Where(r => r.IsVisible);

    private void ShowRowDropIndicator(DataGridRow row, bool after)
    {
        if (row.TranslatePoint(new Point(0, after ? row.Bounds.Height : 0), GridPanel) is not { } point)
            return;

        Canvas.SetLeft(RowDropIndicator, 0);
        Canvas.SetTop(RowDropIndicator, point.Y - RowDropIndicator.Height / 2);
        RowDropIndicator.Width = GridPanel.Bounds.Width;
        RowDropIndicator.IsVisible = true;
    }

    // A row's height from the top (under the headers) or the bottom: bring the next row into view.
    private void ScrollNearEdge(DataGridRow row, IList<LibraryTrackViewModel> tracks)
    {
        if (row.DataContext is not LibraryTrackViewModel item || row.TranslatePoint(default, TracksGrid) is not { } top)
            return;

        var index = tracks.IndexOf(item);
        if (top.Y < row.Bounds.Height * 2 && index > 0)
            TracksGrid.ScrollIntoView(tracks[index - 1], null);
        else if (top.Y + row.Bounds.Height * 2 > TracksGrid.Bounds.Height && index < tracks.Count - 1)
            TracksGrid.ScrollIntoView(tracks[index + 1], null);
    }

    private void OnAddToPlaylistSubmenuOpened(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: LibraryTrackViewModel trackItem } submenu && DataContext is LibraryViewModel vm)
        {
            AddToPlaylistMenu.Rebuild(submenu, vm.Playlists,
                playlistId => vm.AddToPlaylistCommand.Execute((trackItem.Track, playlistId)));
        }
    }

    private void OnRowMenuOpening(object? sender, CancelEventArgs e)
    {
        if (sender is ContextMenu { DataContext: LibraryTrackViewModel item } menu)
        {
            CompilationFlagMenu.Refresh(menu, item.Library, item.Library.ResolveSelection(item.Track));
        }
    }

    private async void OnCompilationClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: LibraryTrackViewModel item } menuItem)
        {
            await CompilationFlagMenu.ToggleAsync(menuItem, item.Library, item.Library.ResolveSelection(item.Track));
        }
    }

    private async void OnPropertiesClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { DataContext: LibraryTrackViewModel item })
        {
            await TrackPropertiesWindow.ShowAsync(this, item.Library, item.Library.ResolveSelection(item.Track));
        }
    }
}
