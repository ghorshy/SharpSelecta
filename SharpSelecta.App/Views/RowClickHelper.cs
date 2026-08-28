using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;

namespace SharpSelecta.App.Views;

// Shared by ItemsControl row click-handlers: the row's own data is on the clicked Button's
// DataContext, while the parent view-model is still the view's DataContext.
internal static class RowClickHelper
{
    public static bool TryGetRowContext<TRow, TViewModel>(
        object? sender, object? dataContext, [NotNullWhen(true)] out TRow? row, [NotNullWhen(true)] out TViewModel? viewModel)
        where TRow : class
        where TViewModel : class
    {
        if (sender is Button { DataContext: TRow r } && dataContext is TViewModel vm)
        {
            row = r;
            viewModel = vm;
            return true;
        }

        row = null;
        viewModel = null;
        return false;
    }
}
