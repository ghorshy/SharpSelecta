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

public partial class LibraryView : UserControl
{
    private bool _columnWidthsDirty;

    public LibraryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        foreach (var column in TracksGrid.Columns)
        {
            column.PropertyChanged += OnColumnPropertyChanged;
        }

        TracksGrid.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, handledEventsToo: true);
        TracksGrid.Sorting += (_, _) => Dispatcher.UIThread.Post(SaveCurrentSort, DispatcherPriority.Background);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not LibraryViewModel vm)
            return;

        var order = LibrarySettingsStore.LoadColumnOrder(vm.SettingsFilePath);
        if (order is not null)
        {
            var displayIndexByKey = order.Select((key, index) => (key, index)).ToDictionary(x => x.key, x => x.index);
            ApplyToTaggedColumns(displayIndexByKey, (column, index) => column.DisplayIndex = index);
        }

        var widths = LibrarySettingsStore.LoadColumnWidths(vm.SettingsFilePath);
        if (widths is not null)
        {
            ApplyToTaggedColumns(widths, (column, width) => column.Width = new DataGridLength(width));
        }

        // CollectionView is still null right here — TracksGrid's ItemsSource binding to Tracks
        // hasn't caught up with this DataContext yet — so the saved sort is (re)applied once
        // Tracks actually gets populated instead, when CollectionView is guaranteed to exist.
        vm.Tracks.CollectionChanged += (_, _) => ApplySavedSort(vm);
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
        var sort = LibrarySettingsStore.LoadSort(vm.SettingsFilePath);
        if (sort is not { } savedSort || TracksGrid.CollectionView is not { } collectionView)
            return;

        var direction = savedSort.Descending ? ListSortDirection.Descending : ListSortDirection.Ascending;
        collectionView.SortDescriptions.Clear();
        collectionView.SortDescriptions.Add(DataGridSortDescription.FromPath(savedSort.PropertyPath, direction));
    }

    private void OnColumnReordered(object? sender, DataGridColumnEventArgs e)
    {
        if (DataContext is not LibraryViewModel vm)
            return;

        var orderedKeys = TracksGrid.Columns
            .OrderBy(c => c.DisplayIndex)
            .Select(c => c.Tag as string)
            .OfType<string>()
            .ToList();

        LibrarySettingsStore.SaveColumnOrder(vm.SettingsFilePath, orderedKeys);
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

            // Width.Value (set synchronously as part of the property itself) rather than ActualWidth
            // (a layout-computed value that may not have caught up yet at the exact moment this fires).
            var widths = TracksGrid.Columns
                .Where(c => c.Tag is string)
                .ToDictionary(c => (string)c.Tag!, c => c.Width.Value);

            LibrarySettingsStore.SaveColumnWidths(vm.SettingsFilePath, widths);
        }
    }

    private void SaveCurrentSort()
    {
        if (DataContext is not LibraryViewModel vm || TracksGrid.CollectionView?.SortDescriptions.FirstOrDefault() is not { } sortDescription)
            return;

        LibrarySettingsStore.SaveSort(
            vm.SettingsFilePath, sortDescription.PropertyPath, sortDescription.Direction == ListSortDirection.Descending);
    }

    private void OnTrackDoubleTapped(object? sender, TappedEventArgs e)
    {
        // DoubleTapped bubbles up from anywhere in the DataGrid, including a column header —
        // double-clicking a header to flip the sort direction shouldn't play whatever row
        // happens to be selected.
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
