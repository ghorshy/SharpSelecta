using System.ComponentModel;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Formatting;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services.Notifications;

// Announces each track once, when it starts playing: not for a queue restored paused, not again after
// a pause, and not while the main window is focused (the track is on screen anyway).
public sealed class TrackChangeNotifier : IDisposable
{
    private readonly PlaybackControlsViewModel _playback;
    private readonly ITrackNotifications _notifications;
    private readonly SettingsStore _settings;
    private readonly Func<bool> _isWindowActive;
    private readonly Func<string, Task<byte[]?>> _loadArtwork;
    private readonly ILogger _logger;
    private Track? _announced;

    public TrackChangeNotifier(
        PlaybackControlsViewModel playback, ITrackNotifications notifications, SettingsStore settings,
        Func<bool> isWindowActive, ILogger logger, Func<string, Task<byte[]?>>? loadArtwork = null)
    {
        _playback = playback;
        _notifications = notifications;
        _settings = settings;
        _isWindowActive = isWindowActive;
        _logger = logger;
        _loadArtwork = loadArtwork ?? (path => Task.Run(() => MusicLibraryScanner.LoadArtwork(path)));
        _playback.PropertyChanged += OnPlaybackPropertyChanged;
    }

    private void OnPlaybackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(PlaybackControlsViewModel.CurrentTrack) or nameof(PlaybackControlsViewModel.IsPlaying)))
            return;

        if (!_playback.IsPlaying || _playback.CurrentTrack is not { } track || track.FilePath == _announced?.FilePath)
            return;

        _announced = track;
        if (_settings.Current.TrackChangeNotifications && !_isWindowActive())
            _ = ShowAsync(track);
    }

    private async Task ShowAsync(Track track)
    {
        try
        {
            var artists = TrackFormatting.FormatArtists(track.Artist);
            var body = string.Join("\n", new[] { artists, track.Album }.Where(line => !string.IsNullOrEmpty(line)));
            var artwork = await _loadArtwork(track.FilePath);
            await _notifications.ShowAsync(new TrackNotification(track.Title ?? track.DisplayName, body, artwork));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to show the notification for {FilePath}", track.FilePath);
        }
    }

    public void Dispose() => _playback.PropertyChanged -= OnPlaybackPropertyChanged;
}
