using Avalonia.Controls;
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
        var track = new Track("/music/song.mp3", "song.mp3");

        await vm.Library.PlayNowCommand.ExecuteAsync(track);

        audioEngine.Received(1).Load("/music/song.mp3");
        await Assert.That(vm.PlaybackControls.IsPlaying).IsTrue();
        await Assert.That(vm.PlaybackControls.PlayPauseLabel).IsEqualTo("Pause");
    }

    [Test]
    public async Task QueueingFromLibrary_MakesNextTrackCommandConsumeSharedQueue()
    {
        var vm = CreateViewModel(out var audioEngine);
        var track = new Track("/music/song.mp3", "song.mp3");

        vm.Library.AddToQueueCommand.Execute(track);
        await Assert.That(vm.Queue.Entries.Count).IsEqualTo(1);

        await vm.PlaybackControls.NextTrackCommand.ExecuteAsync(null);

        audioEngine.Received(1).Load("/music/song.mp3");
        await Assert.That(vm.Queue.Entries.Count).IsEqualTo(1);
        await Assert.That(vm.Queue.CurrentIndex).IsEqualTo(0);
    }

    [Test]
    public async Task RightColumnWidth_DefaultsTo220WhenNothingIsSaved()
    {
        var vm = CreateViewModel(out _);

        await Assert.That(vm.RightColumnWidth.Value).IsEqualTo(220);
    }

    [Test]
    public async Task RightColumnWidth_PersistsAcrossInstancesForTheSameSettingsFile()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(out _, settingsPath);

        vm.RightColumnWidth = new GridLength(300);
        vm.PersistRightColumnWidth();

        var restarted = CreateViewModel(out _, settingsPath);
        await Assert.That(restarted.RightColumnWidth.Value).IsEqualTo(300);
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

    [Test]
    public async Task Equalizer_IsConstructedWithTenBands()
    {
        var vm = CreateViewModel(out _);

        await Assert.That(vm.Equalizer.Bands.Count).IsEqualTo(10);
    }
}
