using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

public partial class CreditsEditorWindow : Window
{
    private static readonly DataFormat<CreditEntryViewModel> DragEntryFormat =
        DataFormat.CreateInProcessFormat<CreditEntryViewModel>("SharpSelecta.CreditEntry");

    public CreditsEditorWindow()
    {
        InitializeComponent();
    }

    private async void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: CreditEntryViewModel entry } handle
            || !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)
            return;

        // The handle sits in the row's Grid, whose parent is the row Border.
        var row = handle.FindAncestorOfType<Border>();
        row?.Classes.Add("dragging");
        try
        {
            var data = new DataTransfer();
            data.Add(DataTransferItem.Create(DragEntryFormat, entry));
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }
        finally
        {
            row?.Classes.Remove("dragging");
        }
    }

    private void OnRowDragOver(object? sender, DragEventArgs e)
    {
        if (!e.DataTransfer.Contains(DragEntryFormat))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        (sender as Border)?.Classes.Add("drag-over");
    }

    private void OnRowDragLeave(object? sender, DragEventArgs e) => (sender as Border)?.Classes.Remove("drag-over");

    private void OnRowDrop(object? sender, DragEventArgs e)
    {
        (sender as Border)?.Classes.Remove("drag-over");

        if (sender is Control { DataContext: CreditEntryViewModel target }
            && e.DataTransfer.TryGetValue(DragEntryFormat) is { } dragged
            && DataContext is CreditsEditorViewModel viewModel)
        {
            viewModel.Move(dragged, target);
            e.DragEffects = DragDropEffects.Move;
        }
    }

    // The edited credits, or null if the user cancelled.
    public static async Task<IReadOnlyList<CreditEntry>?> ShowAsync(Window owner, CreditsEditorViewModel viewModel)
    {
        var window = new CreditsEditorWindow { DataContext = viewModel };
        viewModel.CloseRequested += (_, result) => window.Close(result);
        return await window.ShowDialog<IReadOnlyList<CreditEntry>?>(owner);
    }
}
