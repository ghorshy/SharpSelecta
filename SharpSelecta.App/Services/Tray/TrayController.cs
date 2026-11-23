using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SharpSelecta.App.Formatting;
using SharpSelecta.App.Resources;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services.Tray;

// The tray icon and its menu, and hiding the main window to it on minimize/close when the settings ask for that.
public sealed class TrayController : IDisposable
{
    private readonly Window _window;
    private readonly IClassicDesktopStyleApplicationLifetime _desktop;
    private readonly PlaybackControlsViewModel _playback;
    private readonly InterfaceSettingsViewModel _interfaceSettings;
    private readonly SettingsStore _settings;
    private readonly TrayHostWatcher _host;
    private readonly TrayIcon _icon;
    private readonly NativeMenuItem _playPause;
    private readonly Bitmap? _playIcon;
    private readonly Bitmap? _pauseIcon;

    public TrayController(
        Window window, IClassicDesktopStyleApplicationLifetime desktop, PlaybackControlsViewModel playback,
        InterfaceSettingsViewModel interfaceSettings, SettingsStore settings, TrayHostWatcher host)
    {
        _window = window;
        _desktop = desktop;
        _playback = playback;
        _interfaceSettings = interfaceSettings;
        _settings = settings;
        _host = host;

        var iconColor = TrayMenuIcons.IconColor();
        Bitmap? MenuIcon(string key) => iconColor is { } color ? TrayMenuIcons.Render(key, color) : null;
        _playIcon = MenuIcon("Icon.PlayerPlay");
        _pauseIcon = MenuIcon("Icon.PlayerPause");

        _playPause = new NativeMenuItem(playback.PlayPauseLabel) { Command = playback.PlayPauseCommand, Icon = PlayPauseIcon() };
        var menu = new NativeMenu();
        menu.Add(_playPause);
        menu.Add(new NativeMenuItem(Strings.Previous) { Command = playback.PreviousTrackCommand, Icon = MenuIcon("Icon.PlayerTrackPrev") });
        menu.Add(new NativeMenuItem(Strings.Next) { Command = playback.NextTrackCommand, Icon = MenuIcon("Icon.PlayerTrackNext") });
        menu.Add(new NativeMenuItemSeparator());
        var show = new NativeMenuItem(Strings.TrayShowWindow) { Icon = MenuIcon("Icon.AppWindow") };
        show.Click += (_, _) => WindowActivation.BringToFront(_window);
        menu.Add(show);
        var quit = new NativeMenuItem(Strings.TrayQuit) { Icon = MenuIcon("Icon.Power") };
        quit.Click += (_, _) => _desktop.Shutdown();
        menu.Add(quit);

        _icon = new TrayIcon { Icon = TrayIconImage(window), Menu = menu, ToolTipText = ToolTip(), IsVisible = TrayPolicy.ShowsIcon(settings.Current) };
        _icon.Clicked += (_, _) => WindowActivation.BringToFront(_window);
        if (Application.Current is { } app)
            TrayIcon.SetIcons(app, [_icon]);

        _window.Closing += OnWindowClosing;
        _window.PropertyChanged += OnWindowPropertyChanged;
        _playback.PropertyChanged += OnPlaybackPropertyChanged;
        _interfaceSettings.Applied += OnInterfaceSettingsApplied;
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (!TrayPolicy.HidesOnClose(_settings.Current, _host.IsAvailable, e.CloseReason, e.IsProgrammatic))
            return;

        e.Cancel = true;
        _window.Hide();
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty && e.NewValue is WindowState.Minimized
            && TrayPolicy.HidesOnMinimize(_settings.Current, _host.IsAvailable))
            _window.Hide();
    }

    private void OnPlaybackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PlaybackControlsViewModel.PlayPauseLabel))
        {
            _playPause.Header = _playback.PlayPauseLabel;
            _playPause.Icon = PlayPauseIcon();
        }
        else if (e.PropertyName == nameof(PlaybackControlsViewModel.CurrentTrack))
            _icon.ToolTipText = ToolTip();
    }

    private void OnInterfaceSettingsApplied(object? sender, EventArgs e) => _icon.IsVisible = TrayPolicy.ShowsIcon(_settings.Current);

    private Bitmap? PlayPauseIcon() => _playback.IsPlaying ? _pauseIcon : _playIcon;

    // Linux panels draw tray icons as flat white symbols, so the colored logo would vanish on a dark bar;
    // the Windows tray shows full-color app icons.
    private static WindowIcon? TrayIconImage(Window window) =>
        OperatingSystem.IsLinux()
            ? new WindowIcon(AssetLoader.Open(new Uri("avares://SharpSelecta/Assets/sharpselecta-tray.png")))
            : window.Icon;

    private string ToolTip() =>
        _playback.CurrentTrack is { } track ? TrackFormatting.ArtistTitleLabel(track.Artist, track.Title ?? track.DisplayName) : Strings.AppName;

    public void Dispose()
    {
        _window.Closing -= OnWindowClosing;
        _window.PropertyChanged -= OnWindowPropertyChanged;
        _playback.PropertyChanged -= OnPlaybackPropertyChanged;
        _interfaceSettings.Applied -= OnInterfaceSettingsApplied;
        _icon.Dispose();
    }
}
