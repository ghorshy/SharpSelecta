using Avalonia.Controls;

namespace SharpSelecta.App.Services;

public static class WindowActivation
{
    // Also brings back a window hidden to the tray.
    public static void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
    }
}
