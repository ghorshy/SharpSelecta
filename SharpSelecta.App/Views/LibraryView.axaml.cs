using System;
using Avalonia.Controls;
using SharpSelecta.App.ViewModels;

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

        vm.SearchFocusRequested += (_, _) =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        };
    }
}
