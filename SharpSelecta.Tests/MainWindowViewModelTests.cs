using Avalonia.Controls;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class MainWindowViewModelTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-mainwindow-vm-settings-{Guid.NewGuid():N}.json");

    private static MainWindowViewModel CreateViewModel(out IAudioEngine audioEngine, string? settingsFilePath = null)
    {
        audioEngine = Substitute.For<IAudioEngine>();
        var filePickerService = Substitute.For<IFilePickerService>();
        return new MainWindowViewModel(
            audioEngine,
            filePickerService,
            settingsFilePath ?? CreateTempSettingsPath(),
            NullLogger<PlaybackControlsViewModel>.Instance,
            NullLogger<LibraryViewModel>.Instance,
            NullLogger<QueueViewModel>.Instance);
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
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = CreateViewModel(out _, settingsPath);

            vm.RightColumnWidth = new GridLength(300);
            vm.PersistRightColumnWidth();

            var restarted = CreateViewModel(out _, settingsPath);
            await Assert.That(restarted.RightColumnWidth.Value).IsEqualTo(300);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    private static readonly string TaggedTrackFixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3");

    [Test]
    public async Task PersistThenRestoreQueueState_RoundTripsTheQueueAcrossInstances()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
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
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task RestoreQueueIfEnabledAsync_WhenDisabled_LeavesQueueEmpty()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = CreateViewModel(out _, settingsPath);
            await vm.PlaybackControls.PlayNowAsync(new Track(TaggedTrackFixturePath, "tagged-track.mp3"));
            vm.PersistQueueStateIfEnabled();

            var restarted = CreateViewModel(out var restartedAudioEngine, settingsPath);
            restarted.PlaybackSettings.RestoreQueueOnStartup = false;

            await restarted.RestoreQueueIfEnabledAsync();

            restartedAudioEngine.DidNotReceive().Load(Arg.Any<string>());
            await Assert.That(restarted.Queue.Entries.Count).IsEqualTo(0);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task PersistQueueStateIfEnabled_WhenDisabled_DoesNotSaveAnything()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = CreateViewModel(out _, settingsPath);
            await vm.PlaybackControls.PlayNowAsync(new Track(TaggedTrackFixturePath, "tagged-track.mp3"));
            vm.PlaybackSettings.RestoreQueueOnStartup = false;

            vm.PersistQueueStateIfEnabled();

            await Assert.That(LibrarySettingsStore.LoadQueueState(settingsPath)).IsNull();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task PersistQueueStateIfEnabled_WithAnEmptyQueue_ClearsAnyPreviouslySavedState()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = CreateViewModel(out _, settingsPath);
            await vm.PlaybackControls.PlayNowAsync(new Track(TaggedTrackFixturePath, "tagged-track.mp3"));
            vm.PersistQueueStateIfEnabled();
            await Assert.That(LibrarySettingsStore.LoadQueueState(settingsPath)).IsNotNull();

            var emptied = CreateViewModel(out _, settingsPath);
            emptied.PersistQueueStateIfEnabled();

            await Assert.That(LibrarySettingsStore.LoadQueueState(settingsPath)).IsNull();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }
}
