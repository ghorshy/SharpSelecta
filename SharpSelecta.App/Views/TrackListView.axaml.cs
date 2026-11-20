using System.Collections.Specialized;
using System.ComponentModel;
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
    // but still allows the user to drag rows to reorder it.
    public bool AllowRowReorder
    {
        get => GetValue(AllowRowReorderProperty);
        set => SetValue(AllowRowReorderProperty, value);
    }

    private bool _columnWidthsDirty;
    private readonly List<LibraryTrackViewModel> _orderedSelection = [];
    private LibraryTrackViewModel? _rowBeingDragged;
    private LibraryTrackViewModel? _collapseSelectionTo;

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
        TracksGrid.AddHandler(InputElement.PointerPressedEvent, OnRowReorderPointerPressed, handledEventsToo: true);
        TracksGrid.AddHandler(InputElement.PointerReleasedEvent, OnRowReorderPointerReleased, handledEventsToo: true);
        TracksGrid.AddHandler(InputElement.PointerPressedEvent, OnPressedKeepSelection, RoutingStrategies.Tunnel);
        TracksGrid.AddHandler(InputElement.PointerPressedEvent, OnColumnBorderDoubleClick, RoutingStrategies.Tunnel, handledEventsToo: true);
        TracksGrid.AddHandler(InputElement.PointerReleasedEvent, OnReleasedCollapseSelection, handledEventsToo: true);
        var drag = new TrackDragSource(TracksGrid, DraggedTracks)
        {
            StartWhenLeaving = () => AllowRowReorder,
            OnError = ex => (DataContext as LibraryViewModel)?.ReportDragFailure(ex),
        };
        drag.Starting += (_, _) => _collapseSelectionTo = null;
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

    private void OnRowReorderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!AllowRowReorder)
            return;

        if (e.Source is Visual source && source.FindAncestorOfType<DataGridRow>() is { DataContext: LibraryTrackViewModel item })
        {
            _rowBeingDragged = item;
        }
    }

    private void OnRowReorderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!AllowRowReorder)
            return;

        var dragged = _rowBeingDragged;
        _rowBeingDragged = null;
        if (dragged is null)
            return;

        if (e.Source is not Visual source || source.FindAncestorOfType<DataGridRow>() is not { DataContext: LibraryTrackViewModel target } || ReferenceEquals(target, dragged))
            return;

        if (DataContext is not LibraryViewModel vm || ItemsSource?.ToList() is not { } items)
            return;

        var fromIndex = items.IndexOf(dragged);
        var toIndex = items.IndexOf(target);
        if (fromIndex < 0 || toIndex < 0)
            return;

        items.RemoveAt(fromIndex);
        items.Insert(toIndex, dragged);

        vm.Playlists.ReorderSelectedPlaylist(items.Select(t => t.Track.FilePath).ToList());
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
