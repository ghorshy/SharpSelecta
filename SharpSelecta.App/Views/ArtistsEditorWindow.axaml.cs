using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using SharpSelecta.App.ViewModels;

namespace SharpSelecta.App.Views;

public partial class ArtistsEditorWindow : Window
{
    public ArtistsEditorWindow()
    {
        InitializeComponent();
    }

    // The edited list, or null if the user cancelled.
    public static async Task<IReadOnlyList<string>?> ShowAsync(Window owner, ArtistsEditorViewModel viewModel)
    {
        var window = new ArtistsEditorWindow { DataContext = viewModel };
        viewModel.CloseRequested += (_, result) => window.Close(result);
        return await window.ShowDialog<IReadOnlyList<string>?>(owner);
    }
}
