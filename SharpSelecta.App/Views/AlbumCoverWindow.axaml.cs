using Avalonia.Controls;
using Avalonia.Input;

namespace SharpSelecta.App.Views;

public partial class AlbumCoverWindow : Window
{
    public AlbumCoverWindow()
    {
        InitializeComponent();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
        };

        // Click-away-to-dismiss, matching the usual "preview" convention for a window with no
        // close button of its own.
        Deactivated += (_, _) => Close();
    }
}
