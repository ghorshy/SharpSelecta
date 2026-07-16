using NSubstitute;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Tests;

public class PlaybackControlsViewModelTests
{
    private static PlaybackControlsViewModel CreateViewModel(out IAudioEngine audioEngine)
    {
        audioEngine = Substitute.For<IAudioEngine>();
        return new PlaybackControlsViewModel(audioEngine);
    }

    [Test]
    public async Task PlayPauseCommand_WhenNotPlaying_PlaysAndUpdatesState()
    {
        var vm = CreateViewModel(out var audioEngine);

        vm.PlayPauseCommand.Execute(null);

        audioEngine.Received(1).Play();
        await Assert.That(vm.IsPlaying).IsTrue();
        await Assert.That(vm.PlayPauseLabel).IsEqualTo("Pause");
    }

    [Test]
    public async Task PlayPauseCommand_WhenPlaying_PausesAndUpdatesState()
    {
        var vm = CreateViewModel(out var audioEngine);
        vm.PlayPauseCommand.Execute(null);

        vm.PlayPauseCommand.Execute(null);

        audioEngine.Received(1).Pause();
        await Assert.That(vm.IsPlaying).IsFalse();
        await Assert.That(vm.PlayPauseLabel).IsEqualTo("Play");
    }

    [Test]
    public async Task SettingVolume_ForwardsToEngine()
    {
        var vm = CreateViewModel(out var audioEngine);

        vm.Volume = 0.4;

        audioEngine.Received(1).Volume = 0.4f;
        await Assert.That(vm.Volume).IsEqualTo(0.4);
    }

    [Test]
    public async Task SettingPositionSeconds_SeeksTheEngine()
    {
        var vm = CreateViewModel(out var audioEngine);

        vm.PositionSeconds = 42.0;

        audioEngine.Received(1).Seek(42.0);
    }

    [Test]
    public async Task RefreshPosition_UpdatesFromEngineWithoutSeeking()
    {
        var vm = CreateViewModel(out var audioEngine);
        audioEngine.Position.Returns(30.0);
        audioEngine.Duration.Returns(180.0);

        vm.RefreshPosition();

        await Assert.That(vm.PositionSeconds).IsEqualTo(30.0);
        await Assert.That(vm.DurationSeconds).IsEqualTo(180.0);
        audioEngine.DidNotReceive().Seek(Arg.Any<double>());
    }

    [Test]
    public async Task PreviousAndNextTrackCommands_AreDisabled()
    {
        var vm = CreateViewModel(out _);

        await Assert.That(vm.PreviousTrackCommand.CanExecute(null)).IsFalse();
        await Assert.That(vm.NextTrackCommand.CanExecute(null)).IsFalse();
    }
}
