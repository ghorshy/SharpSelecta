namespace SharpSelecta.App;

// The app's reverse-DNS id: the .desktop file's name, the window's Wayland app_id / X11 WM_CLASS (so a
// panel matches the window to its menu entry and icon), and the root of its D-Bus names. It follows
// where the code lives (github.com/ghorshy/SharpSelecta), as Flathub requires.
public static class AppIdentity
{
    public const string Id = "io.github.ghorshy.SharpSelecta";

    public const string SingleInstanceInterface = Id + ".SingleInstance";

    public const string SingleInstanceObjectPath = "/io/github/ghorshy/SharpSelecta/SingleInstance";

    public const string RepositoryUrl = "https://github.com/ghorshy/SharpSelecta";
}
