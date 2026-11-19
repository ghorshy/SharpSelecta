using Avalonia;
using Avalonia.Controls;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services;

// Opens the main window at the size it had when it was last closed, and maximized if it was. The theme's
// starting size applies only the first time. The position isn't kept: Wayland doesn't let an app place
// its window, and a saved spot on a monitor that's since gone helps no one.
public sealed class WindowSizeKeeper
{
    private readonly Window _window;
    private Size? _normalSize;

    public WindowSizeKeeper(Window window, AppSettings saved)
    {
        _window = window;
        if (saved.WindowWidth is { } width && saved.WindowHeight is { } height)
        {
            _window.Width = width;
            _window.Height = height;
            _normalSize = new Size(width, height);
        }

        if (saved.WindowMaximized)
            _window.WindowState = WindowState.Maximized;

        _window.Opened += (_, _) => FitToScreen();
        _window.PropertyChanged += (_, e) =>
        {
            if (e.Property == TopLevel.ClientSizeProperty && _window.WindowState == WindowState.Normal)
                _normalSize = _window.ClientSize;
        };
    }

    public void Persist(SettingsStore settings)
    {
        if (_normalSize is not { } size)
            return;

        settings.Update(s => s with
        {
            WindowWidth = size.Width,
            WindowHeight = size.Height,
            WindowMaximized = _window.WindowState == WindowState.Maximized,
        });
    }

    // A size saved on a bigger monitor would open partly off screen.
    private void FitToScreen()
    {
        if (_window.WindowState != WindowState.Normal || _window.Screens.ScreenFromWindow(_window) is not { } screen)
            return;

        var available = screen.WorkingArea.Size.ToSize(screen.Scaling);
        var fitted = Fit(_window.ClientSize, available);
        if (fitted != _window.ClientSize)
        {
            _window.Width = fitted.Width;
            _window.Height = fitted.Height;
        }
    }

    public static Size Fit(Size size, Size available) =>
        new(Math.Min(size.Width, available.Width), Math.Min(size.Height, available.Height));
}
