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
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not LibraryViewModel vm)
            return;

        var order = LibrarySettingsStore.LoadColumnOrder(vm.SettingsFilePath);
        if (order is null)
            return;

        for (var i = 0; i < order.Count; i++)
        {
            var column = TracksGrid.Columns.FirstOrDefault(c => c.Tag as string == order[i]);
            if (column is not null)
            {
                column.DisplayIndex = i;
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
