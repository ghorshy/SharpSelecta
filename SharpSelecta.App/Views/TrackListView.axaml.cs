using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Input;
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

    private bool _columnWidthsDirty;
    private readonly List<Track> _orderedSelection = [];

    public TrackListView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        foreach (var column in TracksGrid.Columns)
        {
            column.PropertyChanged += OnColumnPropertyChanged;
        }

        TracksGrid.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, handledEventsToo: true);
        TracksGrid.Sorting += (_, _) => Dispatcher.UIThread.Post(SaveCurrentSort, DispatcherPriority.Background);
        TracksGrid.SelectionChanged += OnTracksGridSelectionChanged;
    }

    // DataGrid.SelectedItems is ordered by the underlying list, not by click order - track
    // click order ourselves so Play Next/Add to Queue can act on it in the order selected.
    private void OnTracksGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        foreach (var removed in e.RemovedItems)
        {
            if (removed is LibraryTrackViewModel removedItem)
            {
                _orderedSelection.Remove(removedItem.Track);
            }
        }

        foreach (var added in e.AddedItems)
        {
            if (added is LibraryTrackViewModel addedItem)
            {
                _orderedSelection.Add(addedItem.Track);
            }
        }

        if (DataContext is LibraryViewModel vm)
        {
            vm.SetSelectedTracksInOrder([.._orderedSelection]);
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not LibraryViewModel vm)
            return;

        ApplyColumnLayout(vm);
        vm.Tracks.CollectionChanged += (_, _) => ApplySavedSort(vm);
        vm.ColumnLayoutChanged += (_, _) => ApplyColumnLayout(vm);
    }

    private void ApplyColumnLayout(LibraryViewModel vm)
    {
        var order = SettingsStore.LoadColumnOrder(vm.SettingsFilePath);
        if (order is not null)
        {
            var displayIndexByKey = order.Select((key, index) => (key, index)).ToDictionary(x => x.key, x => x.index);
            ApplyToTaggedColumns(displayIndexByKey, (column, index) => column.DisplayIndex = index);
        }

        var widths = SettingsStore.LoadColumnWidths(vm.SettingsFilePath);
        if (widths is not null)
        {
            ApplyToTaggedColumns(widths, (column, width) => column.Width = new DataGridLength(width));
        }
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

        var sort = SettingsStore.LoadSort(vm.SettingsFilePath);
        if (sort is not { } savedSort || TracksGrid.CollectionView is not { } collectionView)
            return;

        var direction = savedSort.Descending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        collectionView.SortDescriptions.Clear();
        collectionView.SortDescriptions.Add(DataGridSortDescription.FromPath(savedSort.PropertyPath, direction));
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

        SettingsStore.SaveColumnOrder(vm.SettingsFilePath, orderedKeys);
        vm.NotifyColumnLayoutChanged();
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

            var widths = TracksGrid.Columns
                .Where(c => c.Tag is string)
                .ToDictionary(c => (string)c.Tag!, c => c.Width.Value);

            SettingsStore.SaveColumnWidths(vm.SettingsFilePath, widths);
            vm.NotifyColumnLayoutChanged();
        }
    }

    private void SaveCurrentSort()
    {
        if (!AllowReorderAndSort || DataContext is not LibraryViewModel vm || TracksGrid.CollectionView?.SortDescriptions.FirstOrDefault() is not { } sortDescription)
            return;

        SettingsStore.SaveSort(
            vm.SettingsFilePath, sortDescription.PropertyPath, sortDescription.Direction == ListSortDirection.Descending);
    }

    private void OnTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is Visual source && source.FindAncestorOfType<DataGridColumnHeader>() is not null)
        {
            return;
        }

        if (sender is DataGrid { SelectedItem: LibraryTrackViewModel item })
        {
            item.Library.PlayNowCommand.Execute(item.Track);
        }
    }
}
