using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;
using SharpSelecta.Core.Presence;

namespace SharpSelecta.Tests;

public class RichPresenceTests
{
    private sealed class RecordingPresence : IRichPresence, IDisposable
    {
        public List<string> Calls { get; } = [];

        public List<PresenceActivity> Shown { get; } = [];

        public bool Disposed { get; private set; }

        public void Show(PresenceActivity activity)
        {
            Shown.Add(activity);
            Calls.Add("show");
        }

        public void Clear() => Calls.Add("clear");

        public void Dispose() => Disposed = true;
    }

    private static PresenceActivity Activity(string title, DateTimeOffset started) => new(title, "Artist", "Album", started, started + TimeSpan.FromMinutes(3));

    // --- ThrottledRichPresence ---

    [Test]
    public async Task Throttle_SendsTheFirstUpdateAtOnce()
    {
        var time = new ManualTimeProvider();
        var inner = new RecordingPresence();
        var throttle = new ThrottledRichPresence(inner, TimeSpan.FromSeconds(4), time);

        throttle.Show(Activity("One", time.GetUtcNow()));

        await Assert.That(inner.Calls).IsEquivalentTo(["show"]);
    }

    [Test]
    public async Task Throttle_CoalescesUpdatesWithinTheIntervalIntoOneTrailingSendOfTheLatest()
    {
        var time = new ManualTimeProvider();
        var inner = new RecordingPresence();
        var throttle = new ThrottledRichPresence(inner, TimeSpan.FromSeconds(4), time);
        throttle.Show(Activity("One", time.GetUtcNow()));

        time.Advance(TimeSpan.FromSeconds(1));
        throttle.Show(Activity("Two", time.GetUtcNow()));
        time.Advance(TimeSpan.FromSeconds(1));
        throttle.Show(Activity("Three", time.GetUtcNow()));
        await Assert.That(inner.Shown.Count).IsEqualTo(1);

        time.Advance(TimeSpan.FromSeconds(2));

        await Assert.That(inner.Shown.Select(a => a.Title)).IsEquivalentTo(["One", "Three"]);
    }

    [Test]
    public async Task Throttle_SendsImmediatelyAgainOnceTheIntervalHasPassed()
    {
        var time = new ManualTimeProvider();
        var inner = new RecordingPresence();
        var throttle = new ThrottledRichPresence(inner, TimeSpan.FromSeconds(4), time);
        throttle.Show(Activity("One", time.GetUtcNow()));

        time.Advance(TimeSpan.FromSeconds(5));
        throttle.Show(Activity("Two", time.GetUtcNow()));

        await Assert.That(inner.Shown.Select(a => a.Title)).IsEquivalentTo(["One", "Two"]);
        await Assert.That(time.PendingTimers).IsEqualTo(0);
    }

    [Test]
    public async Task Throttle_ALaterClearReplacesAPendingShow()
    {
        var time = new ManualTimeProvider();
        var inner = new RecordingPresence();
        var throttle = new ThrottledRichPresence(inner, TimeSpan.FromSeconds(4), time);
        throttle.Show(Activity("One", time.GetUtcNow()));

        time.Advance(TimeSpan.FromSeconds(1));
        throttle.Show(Activity("Two", time.GetUtcNow()));
        throttle.Clear();
        time.Advance(TimeSpan.FromSeconds(4));

        await Assert.That(inner.Calls).IsEquivalentTo(["show", "clear"]);
    }

    [Test]
    public async Task Throttle_DoesNotResendWhatWasJustSent()
    {
        var time = new ManualTimeProvider();
        var inner = new RecordingPresence();
        var throttle = new ThrottledRichPresence(inner, TimeSpan.FromSeconds(4), time);

        throttle.Clear();
        time.Advance(TimeSpan.FromSeconds(10));
        throttle.Clear();
        var activity = Activity("One", time.GetUtcNow());
        throttle.Show(activity);
        time.Advance(TimeSpan.FromSeconds(10));
        throttle.Show(activity);

        await Assert.That(inner.Calls).IsEquivalentTo(["clear", "show"]);
    }

    [Test]
    public async Task Throttle_DisposeClearsAtOnce_DropsPendingUpdates_AndDisposesTheInnerSink()
    {
        var time = new ManualTimeProvider();
        var inner = new RecordingPresence();
        var throttle = new ThrottledRichPresence(inner, TimeSpan.FromSeconds(4), time);
        throttle.Show(Activity("One", time.GetUtcNow()));
        throttle.Show(Activity("Two", time.GetUtcNow()));

        throttle.Dispose();
        time.Advance(TimeSpan.FromSeconds(10));
        throttle.Show(Activity("Three", time.GetUtcNow()));

        await Assert.That(inner.Calls).IsEquivalentTo(["show", "clear"]);
        await Assert.That(inner.Disposed).IsTrue();
    }

    // --- RichPresenceCoordinator ---

    private sealed class Fixture
    {
        public IAudioEngine Engine { get; } = Substitute.For<IAudioEngine>();

        public PlaybackControlsViewModel Playback { get; }

        public RecordingPresence Presence { get; } = new();

        public ManualTimeProvider Time { get; } = new();

        public IntegrationsSettingsViewModel Settings { get; }

        public RichPresenceCoordinator Coordinator { get; }

        public Fixture(bool enabled = true)
        {
            var settingsPath = Path.Combine(Path.GetTempPath(), $"sharpselecta-presence-tests-{Guid.NewGuid():N}.json");
            Playback = new PlaybackControlsViewModel(Engine, new PlaybackQueue(), settingsPath, NullLogger<PlaybackControlsViewModel>.Instance);
            Settings = new IntegrationsSettingsViewModel(settingsPath);
            Coordinator = new RichPresenceCoordinator(Playback, Presence, Settings, Time);
            if (enabled)
            {
                Enable();
            }
        }

        public void Enable(bool on = true)
        {
            Settings.DiscordPresenceEnabled = on;
            ((SharpSelecta.App.ViewModels.ISettingsCategoryViewModel)Settings).ApplyCommand.Execute(null);
        }

        public async Task SetPositionAsync(double position, double duration = 200)
        {
            Engine.Position.Returns(position);
            Engine.Duration.Returns(duration);
            await Playback.RefreshPositionAsync();
        }

        public static Track Song(string title = "Song", string? artist = "A;B") =>
            new($"/music/{title}.mp3", title) { Title = title, Artist = artist, Album = "The Album", Duration = TimeSpan.FromSeconds(200) };
    }

    [Test]
    public async Task Coordinator_WhilePlaying_ShowsTheTrackWithTheDisplaySeparatorAndTimestamps()
    {
        var f = new Fixture();
        await f.Playback.PlayNowAsync(Fixture.Song());
        await f.SetPositionAsync(30, 200);

        // The position jumping from 0 to 30 with no time passing is a seek, so it's re-shown with the right start.
        var shown = f.Presence.Shown.Last();
        await Assert.That(shown.Title).IsEqualTo("Song");
        await Assert.That(shown.Artists).IsEqualTo("A, B");
        await Assert.That(shown.Album).IsEqualTo("The Album");
        await Assert.That(shown.StartedAtUtc).IsEqualTo(f.Time.GetUtcNow() - TimeSpan.FromSeconds(30));
        await Assert.That(shown.EndsAtUtc).IsEqualTo(shown.StartedAtUtc + TimeSpan.FromSeconds(200));
    }

    [Test]
    public async Task Coordinator_WhenPaused_ClearsTheStatus()
    {
        var f = new Fixture();
        await f.Playback.PlayNowAsync(Fixture.Song());
        f.Playback.PlayPauseCommand.Execute(null); // pause

        await Assert.That(f.Playback.IsPlaying).IsFalse();
        await Assert.That(f.Presence.Calls.Last()).IsEqualTo("clear");
    }

    [Test]
    public async Task Coordinator_NormalProgressDoesNotResend_ButASeekDoes()
    {
        var f = new Fixture();
        await f.Playback.PlayNowAsync(Fixture.Song());
        await f.SetPositionAsync(0);
        var before = f.Presence.Shown.Count;

        f.Time.Advance(TimeSpan.FromSeconds(10));
        await f.SetPositionAsync(10); // as expected after 10 seconds
        await Assert.That(f.Presence.Shown.Count).IsEqualTo(before);

        f.Time.Advance(TimeSpan.FromSeconds(1));
        await f.SetPositionAsync(120); // jumped ahead
        await Assert.That(f.Presence.Shown.Count).IsEqualTo(before + 1);
        await Assert.That(f.Presence.Shown.Last().StartedAtUtc).IsEqualTo(f.Time.GetUtcNow() - TimeSpan.FromSeconds(120));
    }

    [Test]
    public async Task Coordinator_ANewTrack_ReplacesTheStatus()
    {
        var f = new Fixture();
        await f.Playback.PlayNowAsync(Fixture.Song("First"));
        await f.Playback.PlayNowAsync(Fixture.Song("Second", "Other"));

        await Assert.That(f.Presence.Shown.Last().Title).IsEqualTo("Second");
        await Assert.That(f.Presence.Shown.Last().Artists).IsEqualTo("Other");
    }

    [Test]
    public async Task Coordinator_WhenDisabled_NeverShowsAnything()
    {
        var f = new Fixture(enabled: false);

        await f.Playback.PlayNowAsync(Fixture.Song());

        await Assert.That(f.Presence.Shown).IsEmpty();
    }

    [Test]
    public async Task Coordinator_SwitchingItOnWhilePlaying_ShowsAtOnce_AndOffClearsIt()
    {
        var f = new Fixture(enabled: false);
        await f.Playback.PlayNowAsync(Fixture.Song());

        f.Enable();
        await Assert.That(f.Presence.Shown.Count).IsEqualTo(1);

        f.Enable(false);
        await Assert.That(f.Presence.Calls.Last()).IsEqualTo("clear");
    }

    [Test]
    public async Task Coordinator_Dispose_ClearsTheStatusAndStopsListening()
    {
        var f = new Fixture();
        await f.Playback.PlayNowAsync(Fixture.Song());

        f.Coordinator.Dispose();
        var calls = f.Presence.Calls.Count;
        await f.Playback.PlayNowAsync(Fixture.Song("Later"));

        await Assert.That(f.Presence.Calls[calls - 1]).IsEqualTo("clear");
        await Assert.That(f.Presence.Calls.Count).IsEqualTo(calls);
    }

    // --- IntegrationsSettingsViewModel ---

    private static string TempSettings() => Path.Combine(Path.GetTempPath(), $"sharpselecta-integrations-tests-{Guid.NewGuid():N}.json");

    [Test]
    public async Task Integrations_DiscordPresenceIsOffByDefault()
    {
        var vm = new IntegrationsSettingsViewModel(TempSettings());

        await Assert.That(vm.DiscordPresenceEnabled).IsFalse();
        await Assert.That(vm.IsDiscordPresenceEnabled).IsFalse();
        await Assert.That(vm.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task Integrations_AnEditOnlyTakesEffectOnApply_WhichPersistsItAndRaisesTheEvent()
    {
        var path = TempSettings();
        var vm = new IntegrationsSettingsViewModel(path);
        var raised = 0;
        vm.DiscordPresenceEnabledChanged += (_, _) => raised++;

        vm.DiscordPresenceEnabled = true;
        await Assert.That(vm.HasPendingChanges).IsTrue();
        await Assert.That(vm.IsDiscordPresenceEnabled).IsFalse();
        await Assert.That(raised).IsEqualTo(0);

        ((SharpSelecta.App.ViewModels.ISettingsCategoryViewModel)vm).ApplyCommand.Execute(null);

        await Assert.That(vm.IsDiscordPresenceEnabled).IsTrue();
        await Assert.That(vm.HasPendingChanges).IsFalse();
        await Assert.That(raised).IsEqualTo(1);
        await Assert.That(new IntegrationsSettingsViewModel(path).DiscordPresenceEnabled).IsTrue();
    }

    [Test]
    public async Task Integrations_CancelRevertsAnUnappliedEdit()
    {
        var vm = new IntegrationsSettingsViewModel(TempSettings());
        vm.DiscordPresenceEnabled = true;

        ((SharpSelecta.App.ViewModels.ISettingsCategoryViewModel)vm).CancelCommand.Execute(null);

        await Assert.That(vm.DiscordPresenceEnabled).IsFalse();
        await Assert.That(vm.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task Integrations_ApplyingWithoutAChange_DoesNotRaiseTheEvent()
    {
        var vm = new IntegrationsSettingsViewModel(TempSettings());
        var raised = 0;
        vm.DiscordPresenceEnabledChanged += (_, _) => raised++;

        ((SharpSelecta.App.ViewModels.ISettingsCategoryViewModel)vm).ApplyCommand.Execute(null);

        await Assert.That(raised).IsEqualTo(0);
    }
}
