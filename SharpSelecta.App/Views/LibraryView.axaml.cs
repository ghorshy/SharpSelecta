using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

public partial class LibraryView : UserControl
{
    public LibraryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        foreach (var column in TracksGrid.Columns)
        {
            column.PropertyChanged += OnColumnPropertyChanged;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not LibraryViewModel vm)
            return;

        var order = LibrarySettingsStore.LoadColumnOrder(vm.SettingsFilePath);
        if (order is not null)
        {
            for (var i = 0; i < order.Count; i++)
            {
                var column = TracksGrid.Columns.FirstOrDefault(c => c.Tag as string == order[i]);
                if (column is not null)
                {
                    column.DisplayIndex = i;
                }
            }
        }

        var widths = LibrarySettingsStore.LoadColumnWidths(vm.SettingsFilePath);
        if (widths is not null)
        {
            foreach (var column in TracksGrid.Columns)
            {
                if (column.Tag is string key && widths.TryGetValue(key, out var width))
                {
                    column.Width = new DataGridLength(width);
                }
            }
        }
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
        if (e.Property != DataGridColumn.WidthProperty || DataContext is not LibraryViewModel vm)
            return;

        // Width.Value (set synchronously as part of the property itself) rather than ActualWidth
        // (a layout-computed value that may not have caught up yet at the exact moment this fires).
        var widths = TracksGrid.Columns
            .Where(c => c.Tag is string)
            .ToDictionary(c => (string)c.Tag!, c => c.Width.Value);

        LibrarySettingsStore.SaveColumnWidths(vm.SettingsFilePath, widths);
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
