using Avalonia.Controls;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services.Tray;

// Without a tray to bring it back from, the window is never hidden: it minimizes and closes as usual.
public static class TrayPolicy
{
    public static bool ShowsIcon(AppSettings settings) => settings.MinimizeToTray || settings.CloseToTray;

    public static bool HidesOnMinimize(AppSettings settings, bool trayAvailable) => settings.MinimizeToTray && trayAvailable;

    // Only the user closing the window; quitting from the tray menu, an update restart or the session ending still close it.
    public static bool HidesOnClose(AppSettings settings, bool trayAvailable, WindowCloseReason reason, bool isProgrammatic) =>
        settings.CloseToTray && trayAvailable && reason == WindowCloseReason.WindowClosing && !isProgrammatic;
}
