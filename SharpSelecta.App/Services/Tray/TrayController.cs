using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

        _playPause = new NativeMenuItem(playback.PlayPauseLabel) { Command = playback.PlayPauseCommand };
        var menu = new NativeMenu();
        menu.Add(_playPause);
        menu.Add(new NativeMenuItem(Strings.Previous) { Command = playback.PreviousTrackCommand });
        menu.Add(new NativeMenuItem(Strings.Next) { Command = playback.NextTrackCommand });
        menu.Add(new NativeMenuItemSeparator());
        var show = new NativeMenuItem(Strings.TrayShowWindow);
        show.Click += (_, _) => WindowActivation.BringToFront(_window);
        menu.Add(show);
        var quit = new NativeMenuItem(Strings.TrayQuit);
        quit.Click += (_, _) => _desktop.Shutdown();
        menu.Add(quit);

        _icon = new TrayIcon { Icon = window.Icon, Menu = menu, ToolTipText = ToolTip(), IsVisible = TrayPolicy.ShowsIcon(settings.Current) };
        _icon.Clicked += (_, _) => WindowActivation.BringToFront(_window);
        if (Application.Current is { } app)
            TrayIcon.SetIcons(app, [_icon]);

        _window.Closing += OnWindowClosing;
        _window.PropertyChanged += OnWindowPropertyChanged;
        _playback.PropertyChanged += OnPlaybackPropertyChanged;
        _interfaceSettings.PropertyChanged += OnInterfaceSettingsPropertyChanged;
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
            _playPause.Header = _playback.PlayPauseLabel;
        else if (e.PropertyName == nameof(PlaybackControlsViewModel.CurrentTrack))
            _icon.ToolTipText = ToolTip();
    }

    private void OnInterfaceSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(InterfaceSettingsViewModel.MinimizeToTray) or nameof(InterfaceSettingsViewModel.CloseToTray))
            _icon.IsVisible = TrayPolicy.ShowsIcon(_settings.Current);
    }

    private string ToolTip() =>
        _playback.CurrentTrack is { } track ? TrackFormatting.ArtistTitleLabel(track.Artist, track.Title ?? track.DisplayName) : Strings.AppName;

    public void Dispose()
    {
        _window.Closing -= OnWindowClosing;
        _window.PropertyChanged -= OnWindowPropertyChanged;
        _playback.PropertyChanged -= OnPlaybackPropertyChanged;
        _interfaceSettings.PropertyChanged -= OnInterfaceSettingsPropertyChanged;
        _icon.Dispose();
    }
}
