using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace SharpSelecta.App.Views;

// Opens at the cover's own size (smaller if the screen can't fit it) and never grows past it.
public partial class AlbumCoverWindow : Window
{
    // Leaves room for the title bar and the panels around the screen's working area.
    private const double ScreenShare = 0.9;

    public AlbumCoverWindow()
    {
        InitializeComponent();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed)
                Close();
        };
        Cover.PropertyChanged += (_, e) =>
        {
            if (e.Property == Image.SourceProperty)
                FitToCover();
        };
        // Again once shown: only then is the owner known, and with it the screen to fit on.
        Opened += (_, _) => FitToCover();
    }

    private void FitToCover()
    {
        if (Cover.Source is not IImage cover)
            return;

        var screen = Owner is Window owner ? owner.Screens.ScreenFromWindow(owner) : Screens.Primary;
        var available = screen is null
            ? cover.Size
            : screen.WorkingArea.Size.ToSize(screen.Scaling) * ScreenShare;
        var size = Fit(cover.Size, available);

        MaxWidth = cover.Size.Width;
        MaxHeight = cover.Size.Height;
        Width = size.Width;
        Height = size.Height;
    }

    // Scales the cover down, keeping its proportions, until it fits; never scales it up.
    public static Size Fit(Size cover, Size available)
    {
        var scale = Math.Min(1, Math.Min(available.Width / cover.Width, available.Height / cover.Height));
        return new Size(Math.Round(cover.Width * scale), Math.Round(cover.Height * scale));
    }
}
