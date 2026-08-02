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

        Deactivated += (_, _) => Close();
    }
}
