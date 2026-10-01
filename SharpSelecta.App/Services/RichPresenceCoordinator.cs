using System;
using System.ComponentModel;
using SharpSelecta.App.Formatting;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Presence;

namespace SharpSelecta.App.Services;

// Keeps the "now listening" status in step with playback: shown while a track is playing (with a progress
// bar from its start/end times), cleared when paused, stopped, or the integration is switched off.
public sealed class RichPresenceCoordinator : IDisposable
{
    // The position ticks along on its own; a jump bigger than this from where it should be is a seek,
    // which needs the status' start/end times recomputed.
    private const double SeekToleranceSeconds = 2;

    private readonly PlaybackControlsViewModel _playback;
    private readonly IRichPresence _presence;
    private readonly IntegrationsSettingsViewModel _settings;
    private readonly TimeProvider _time;

    private double _anchorPositionSeconds;
    private DateTimeOffset _anchorAt;

    public RichPresenceCoordinator(
        PlaybackControlsViewModel playback, IRichPresence presence, IntegrationsSettingsViewModel settings, TimeProvider? time = null)
    {
        _playback = playback;
        _presence = presence;
        _settings = settings;
        _time = time ?? TimeProvider.System;

        _playback.PropertyChanged += OnPlaybackPropertyChanged;
        _settings.DiscordPresenceEnabledChanged += OnEnabledChanged;
        Update();
    }

    private void OnEnabledChanged(object? sender, EventArgs e) => Update();

    private void OnPlaybackPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(PlaybackControlsViewModel.CurrentTrack) or nameof(PlaybackControlsViewModel.IsPlaying):
                Update();
                break;
            case nameof(PlaybackControlsViewModel.PositionSeconds) when _playback.IsPlaying && _settings.IsDiscordPresenceEnabled:
                if (HasJumped())
                {
                    Update();
                }

                break;
        }
    }

    private bool HasJumped()
    {
        var expected = _anchorPositionSeconds + (_time.GetUtcNow() - _anchorAt).TotalSeconds;
        return Math.Abs(_playback.PositionSeconds - expected) > SeekToleranceSeconds;
    }

    private void Update()
    {
        if (!_settings.IsDiscordPresenceEnabled || _playback.CurrentTrack is not { } track || !_playback.IsPlaying)
        {
            _presence.Clear();
            return;
        }

        var now = _time.GetUtcNow();
        _anchorPositionSeconds = _playback.PositionSeconds;
        _anchorAt = now;

        var started = now - TimeSpan.FromSeconds(_playback.PositionSeconds);
        var length = _playback.DurationSeconds > 0 ? TimeSpan.FromSeconds(_playback.DurationSeconds) : track.Duration;
        _presence.Show(new PresenceActivity(
            track.Title ?? track.DisplayName,
            TrackFormatting.FormatArtists(track.Artist),
            track.Album,
            started,
            length > TimeSpan.Zero ? started + length : null));
    }

    public void Dispose()
    {
        _playback.PropertyChanged -= OnPlaybackPropertyChanged;
        _settings.DiscordPresenceEnabledChanged -= OnEnabledChanged;
        _presence.Clear();
    }
}
