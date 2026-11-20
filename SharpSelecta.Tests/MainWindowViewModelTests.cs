using NSubstitute;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class MainWindowViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private MainWindowViewModel CreateViewModel(out IAudioEngine audioEngine, string? settingsFilePath = null) =>
        TestMainWindowViewModel.Create(settingsFilePath ?? _temp.NewSettingsPath(), out audioEngine);

    [Test]
    public async Task Updates_IsUnsupportedWhenNoUpdateServiceIsRegistered()
    {
        var vm = CreateViewModel(out _);

        await Assert.That(vm.Updates.IsSupported).IsFalse();
    }

    [Test]
    public async Task PlayingTrackFromLibrary_UpdatesSharedPlaybackControlsState()
    {
        var vm = CreateViewModel(out var audioEngine);
        var track = new Track(TestPaths.Music("song.mp3"), "song.mp3");

        await vm.Library.PlayNowCommand.ExecuteAsync(track);

        audioEngine.Received(1).Load(TestPaths.Music("song.mp3"));
        await Assert.That(vm.PlaybackControls.IsPlaying).IsTrue();
        await Assert.That(vm.PlaybackControls.PlayPauseLabel).IsEqualTo("Pause");
    }

    [Test]
    public async Task QueueingFromLibrary_MakesNextTrackCommandConsumeSharedQueue()
    {
        var vm = CreateViewModel(out var audioEngine);
        var track = new Track(TestPaths.Music("song.mp3"), "song.mp3");

        vm.Library.AddToQueueCommand.Execute(track);
        await Assert.That(vm.Queue.Entries.Count).IsEqualTo(1);

        await vm.PlaybackControls.NextTrackCommand.ExecuteAsync(null);

        audioEngine.Received(1).Load(TestPaths.Music("song.mp3"));
        await Assert.That(vm.Queue.Entries.Count).IsEqualTo(1);
        await Assert.That(vm.Queue.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    public async Task ASavedVolumeCurve_ReachesTheSettingsCheckboxAndPlaybackControls()
    {
        var settingsPath = _temp.NewSettingsPath();
        new SettingsStore(settingsPath).Update(s => s with { VolumeCurve = VolumeCurve.Logarithmic });

        var vm = CreateViewModel(out _, settingsPath);

        await Assert.That(vm.PlaybackSettings.UseLogarithmicVolumeScale).IsTrue();
        await Assert.That(vm.PlaybackControls.Volume.Curve).IsEqualTo(VolumeCurve.Logarithmic);
    }

    private static readonly string TaggedTrackFixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3");

    [Test]
    public async Task PersistThenRestoreQueueState_RoundTripsTheQueueAcrossInstances()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(out _, settingsPath);
        await vm.PlaybackControls.PlayNowAsync(new Track(TaggedTrackFixturePath, "tagged-track.mp3"));
        vm.PlaybackControls.PositionSeconds = 12.0;

        vm.PersistQueueStateIfEnabled();

        var restarted = CreateViewModel(out var restartedAudioEngine, settingsPath);
        await restarted.RestoreQueueIfEnabledAsync();

        restartedAudioEngine.Received(1).Load(TaggedTrackFixturePath);
        restartedAudioEngine.Received(1).Seek(12.0);
        await Assert.That(restarted.PlaybackControls.IsPlaying).IsFalse();
        await Assert.That(restarted.Queue.Entries.Count).IsEqualTo(1);
    }

    private async Task<MainWindowViewModel> RestartWithSavedQueueAsync(Action<SettingsStore> configure)
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(out _, settingsPath);
        await vm.PlaybackControls.PlayNowAsync(new Track(TaggedTrackFixturePath, "tagged-track.mp3"));
        vm.PersistQueueStateIfEnabled();
        configure(new SettingsStore(settingsPath));
        return CreateViewModel(out _, settingsPath);
    }

    [Test]
    public async Task ResumePlaybackIfEnabled_WhenOn_PlaysTheRestoredTrack()
    {
        var restarted = await RestartWithSavedQueueAsync(s => s.Update(x => x with { ResumePlaybackOnLaunch = true }));
        await restarted.RestoreQueueIfEnabledAsync();

        restarted.ResumePlaybackIfEnabled();

        await Assert.That(restarted.PlaybackControls.IsPlaying).IsTrue();
    }

    [Test]
    public async Task ResumePlaybackIfEnabled_ByDefault_StaysPaused()
    {
        var restarted = await RestartWithSavedQueueAsync(_ => { });
        await restarted.RestoreQueueIfEnabledAsync();

        restarted.ResumePlaybackIfEnabled();

        await Assert.That(restarted.PlaybackControls.IsPlaying).IsFalse();
    }

    [Test]
    public async Task ResumePlaybackIfEnabled_WithRestoreQueueOff_DoesNothing()
    {
        var restarted = await RestartWithSavedQueueAsync(s => s.Update(x => x with { ResumePlaybackOnLaunch = true, RestoreQueueOnStartup = false }));
        await restarted.RestoreQueueIfEnabledAsync();

        restarted.ResumePlaybackIfEnabled();

        await Assert.That(restarted.PlaybackControls.IsPlaying).IsFalse();
        await Assert.That(restarted.Queue.Entries.Count).IsEqualTo(0);
    }

    [Test]
    public async Task ResumePlaybackIfEnabled_WithNothingSaved_DoesNothing()
    {
        var settingsPath = _temp.NewSettingsPath();
        new SettingsStore(settingsPath).Update(x => x with { ResumePlaybackOnLaunch = true });
        var vm = CreateViewModel(out var audioEngine, settingsPath);
        await vm.RestoreQueueIfEnabledAsync();

        vm.ResumePlaybackIfEnabled();

        audioEngine.DidNotReceive().Load(Arg.Any<string>());
        await Assert.That(vm.PlaybackControls.IsPlaying).IsFalse();
    }

    [Test]
    public async Task RestoreQueueIfEnabledAsync_WhenDisabled_LeavesQueueEmpty()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(out _, settingsPath);
        await vm.PlaybackControls.PlayNowAsync(new Track(TaggedTrackFixturePath, "tagged-track.mp3"));
        vm.PersistQueueStateIfEnabled();

        var restarted = CreateViewModel(out var restartedAudioEngine, settingsPath);
        restarted.PlaybackSettings.RestoreQueueOnStartup = false;

        await restarted.RestoreQueueIfEnabledAsync();

        restartedAudioEngine.DidNotReceive().Load(Arg.Any<string>());
        await Assert.That(restarted.Queue.Entries.Count).IsEqualTo(0);
    }

    [Test]
    public async Task PersistQueueStateIfEnabled_WhenDisabled_DoesNotSaveAnything()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(out _, settingsPath);
        await vm.PlaybackControls.PlayNowAsync(new Track(TaggedTrackFixturePath, "tagged-track.mp3"));
        vm.PlaybackSettings.RestoreQueueOnStartup = false;

        vm.PersistQueueStateIfEnabled();

        await Assert.That(QueueStateStore.Load(settingsPath)).IsNull();
    }

    [Test]
    public async Task PersistQueueStateIfEnabled_WithAnEmptyQueue_ClearsAnyPreviouslySavedState()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(out _, settingsPath);
        await vm.PlaybackControls.PlayNowAsync(new Track(TaggedTrackFixturePath, "tagged-track.mp3"));
        vm.PersistQueueStateIfEnabled();
        await Assert.That(QueueStateStore.Load(settingsPath)).IsNotNull();

        var emptied = CreateViewModel(out _, settingsPath);
        emptied.PersistQueueStateIfEnabled();

        await Assert.That(QueueStateStore.Load(settingsPath)).IsNull();
    }

    private string CopyFixture(string name)
    {
        var path = _temp.FilePath(name);
        File.Copy(TaggedTrackFixturePath, path);
        return path;
    }

    private MainWindowViewModel CreateViewModelOpeningFilesWith(OpenedFilesAction action, out IAudioEngine audioEngine)
    {
        var settingsPath = _temp.NewSettingsPath();
        new SettingsStore(settingsPath).Update(s => s with { OpenedFilesAction = action });
        return CreateViewModel(out audioEngine, settingsPath);
    }

    private static string QueuedPaths(MainWindowViewModel vm) =>
        string.Join("|", vm.PlaybackControls.QueueEntries.Select(e => e.Track.FilePath));

    [Test]
    public async Task OpenFilesAsync_ByDefault_ReplacesTheQueueAndPlaysTheFirstFile()
    {
        var vm = CreateViewModel(out var audioEngine);
        await vm.PlaybackControls.PlayNowAsync(new Track(TaggedTrackFixturePath, "tagged-track.mp3"));
        var a = CopyFixture("a.mp3");
        var b = CopyFixture("b.mp3");

        await vm.OpenFilesAsync([a, b]);

        await Assert.That(QueuedPaths(vm)).IsEqualTo($"{a}|{b}");
        await Assert.That(vm.Queue.CurrentIndex).IsEqualTo(0);
        audioEngine.Received(1).Load(a);
    }

    [Test]
    public async Task OpenFilesAsync_AddToQueue_AppendsWithoutInterruptingPlayback()
    {
        var vm = CreateViewModelOpeningFilesWith(OpenedFilesAction.AddToQueue, out var audioEngine);
        await vm.PlaybackControls.PlayNowAsync(new Track(TaggedTrackFixturePath, "tagged-track.mp3"));
        var a = CopyFixture("a.mp3");

        await vm.OpenFilesAsync([a]);

        await Assert.That(QueuedPaths(vm)).IsEqualTo($"{TaggedTrackFixturePath}|{a}");
        await Assert.That(vm.Queue.CurrentIndex).IsEqualTo(0);
        audioEngine.DidNotReceive().Load(a);
    }

    [Test]
    public async Task OpenFilesAsync_PlayNext_InsertsAfterTheCurrentTrack()
    {
        var vm = CreateViewModelOpeningFilesWith(OpenedFilesAction.PlayNext, out var audioEngine);
        var later = CopyFixture("later.mp3");
        await vm.PlaybackControls.PlayNowAsync([new Track(TaggedTrackFixturePath, "tagged-track.mp3"), new Track(later, "later.mp3")]);
        var a = CopyFixture("a.mp3");

        await vm.OpenFilesAsync([a]);

        await Assert.That(QueuedPaths(vm)).IsEqualTo($"{TaggedTrackFixturePath}|{a}|{later}");
        await Assert.That(vm.Queue.CurrentIndex).IsEqualTo(0);
        audioEngine.DidNotReceive().Load(a);
    }

    [Test]
    public async Task OpenFilesAsync_AddToQueue_WithNothingQueued_PlaysTheFirstFile()
    {
        var vm = CreateViewModelOpeningFilesWith(OpenedFilesAction.AddToQueue, out var audioEngine);
        var a = CopyFixture("a.mp3");

        await vm.OpenFilesAsync([a]);

        await Assert.That(QueuedPaths(vm)).IsEqualTo(a);
        audioEngine.Received(1).Load(a);
    }

    [Test]
    public async Task OpenFilesAsync_AddToQueue_KeepsEntriesQueuedBeforeAnythingPlayed()
    {
        var vm = CreateViewModelOpeningFilesWith(OpenedFilesAction.AddToQueue, out _);
        await vm.PlaybackControls.AddToQueue(new Track(TaggedTrackFixturePath, "tagged-track.mp3"));
        var a = CopyFixture("a.mp3");

        await vm.OpenFilesAsync([a]);

        await Assert.That(QueuedPaths(vm)).IsEqualTo($"{TaggedTrackFixturePath}|{a}");
    }

    [Test]
    public async Task OpenFilesAsync_WithNoAudioFiles_LeavesTheQueueAlone()
    {
        var vm = CreateViewModel(out var audioEngine);
        await vm.PlaybackControls.PlayNowAsync(new Track(TaggedTrackFixturePath, "tagged-track.mp3"));
        var picture = _temp.FilePath("cover.jpg");
        File.WriteAllBytes(picture, []);

        await vm.OpenFilesAsync([picture]);

        await Assert.That(QueuedPaths(vm)).IsEqualTo(TaggedTrackFixturePath);
        audioEngine.Received(1).Load(Arg.Any<string>());
    }

    [Test]
    public async Task Equalizer_IsConstructedWithTenBands()
    {
        var vm = CreateViewModel(out _);

        await Assert.That(vm.Equalizer.Bands.Count).IsEqualTo(10);
    }
}
