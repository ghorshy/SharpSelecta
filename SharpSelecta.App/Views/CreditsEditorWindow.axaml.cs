using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

public partial class CreditsEditorWindow : Window
{
    public CreditsEditorWindow()
    {
        InitializeComponent();
    }

    // The edited credits, or null if the user cancelled.
    public static async Task<IReadOnlyList<CreditEntry>?> ShowAsync(Window owner, CreditsEditorViewModel viewModel)
    {
        var window = new CreditsEditorWindow { DataContext = viewModel };
        viewModel.CloseRequested += (_, result) => window.Close(result);
        return await window.ShowDialog<IReadOnlyList<CreditEntry>?>(owner);
    }
}
