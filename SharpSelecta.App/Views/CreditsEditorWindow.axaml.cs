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
        if (sender is Border row)
        {
            var below = IsOverLowerHalf(row, e);
            row.Classes.Set("drag-over", !below);
            row.Classes.Set("drag-over-below", below);
        }
    }

    private void OnRowDragLeave(object? sender, DragEventArgs e) => ClearDropIndicator(sender as Border);

    private void OnRowDrop(object? sender, DragEventArgs e)
    {
        ClearDropIndicator(sender as Border);

        if (sender is Border { DataContext: CreditEntryViewModel target } row
            && e.DataTransfer.TryGetValue(DragEntryFormat) is { } dragged
            && DataContext is CreditsEditorViewModel viewModel)
        {
            viewModel.Move(dragged, target, insertAfter: IsOverLowerHalf(row, e));
            e.DragEffects = DragDropEffects.Move;
        }
    }

    private static bool IsOverLowerHalf(Border row, DragEventArgs e) => e.GetPosition(row).Y > row.Bounds.Height / 2;

    private static void ClearDropIndicator(Border? row)
    {
        row?.Classes.Remove("drag-over");
        row?.Classes.Remove("drag-over-below");
    }

    // The edited credits, or null if the user cancelled.
    public static async Task<IReadOnlyList<CreditEntry>?> ShowAsync(Window owner, CreditsEditorViewModel viewModel)
    {
        var window = new CreditsEditorWindow { DataContext = viewModel };
        viewModel.CloseRequested += (_, result) => window.Close(result);
        return await window.ShowDialog<IReadOnlyList<CreditEntry>?>(owner);
    }
}
