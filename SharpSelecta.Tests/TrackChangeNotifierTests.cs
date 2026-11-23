using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services.Notifications;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class TrackChangeNotifierTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private sealed class RecordingNotifications : ITrackNotifications
    {
        public List<TrackNotification> Shown { get; } = [];

        public event EventHandler? SkipRequested { add { } remove { } }

        public Task ShowAsync(TrackNotification notification)
        {
            Shown.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class Fixture
    {
        public PlaybackControlsViewModel Playback { get; }

        public RecordingNotifications Notifications { get; } = new();

        public SettingsStore Settings { get; }

        public bool WindowActive { get; set; }

        public Fixture(string settingsPath, bool enabled = true)
        {
            Settings = new SettingsStore(settingsPath);
            Settings.Update(s => s with { TrackChangeNotifications = enabled });
            Playback = new PlaybackControlsViewModel(Substitute.For<IAudioEngine>(), new PlaybackQueue(), Settings, NullLogger<PlaybackControlsViewModel>.Instance);
            _ = new TrackChangeNotifier(Playback, Notifications, Settings, () => WindowActive, NullLogger.Instance,
                _ => Task.FromResult<byte[]?>([1, 2, 3]));
        }

        public static Track Song(string title = "Song") =>
            new(TestPaths.Music($"{title}.mp3"), title) { Title = title, Artist = "A;B", Album = "The Album" };
    }

    [Test]
    public async Task ATrackStartingWhileTheWindowIsInTheBackground_IsAnnouncedWithItsArtistsAlbumAndCover()
    {
        var f = new Fixture(_temp.NewSettingsPath());

        await f.Playback.PlayNowAsync(Fixture.Song());

        var shown = f.Notifications.Shown.Single();
        await Assert.That(shown.Title).IsEqualTo("Song");
        await Assert.That(shown.Body).IsEqualTo("A, B\nThe Album");
        await Assert.That(shown.Artwork).IsEquivalentTo(new byte[] { 1, 2, 3 });
    }

    [Test]
    public async Task EachNewTrack_IsAnnouncedOnce_ButResumingTheSameTrackIsNot()
    {
        var f = new Fixture(_temp.NewSettingsPath());

        await f.Playback.PlayNowAsync(Fixture.Song("First"));
        f.Playback.PlayPauseCommand.Execute(null);
        f.Playback.PlayPauseCommand.Execute(null);
        await f.Playback.PlayNowAsync(Fixture.Song("Second"));

        await Assert.That(f.Notifications.Shown.Select(n => n.Title)).IsEquivalentTo(["First", "Second"]);
    }

    [Test]
    public async Task WhileTheWindowIsFocused_NothingIsAnnounced_NorLaterForTheSameTrack()
    {
        var f = new Fixture(_temp.NewSettingsPath()) { WindowActive = true };

        await f.Playback.PlayNowAsync(Fixture.Song());
        f.WindowActive = false;
        f.Playback.PlayPauseCommand.Execute(null);
        f.Playback.PlayPauseCommand.Execute(null);

        await Assert.That(f.Notifications.Shown).IsEmpty();
    }

    [Test]
    public async Task WhenTheSettingIsOff_NothingIsAnnounced()
    {
        var f = new Fixture(_temp.NewSettingsPath(), enabled: false);

        await f.Playback.PlayNowAsync(Fixture.Song());

        await Assert.That(f.Notifications.Shown).IsEmpty();
    }
}
