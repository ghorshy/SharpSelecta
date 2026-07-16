using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Tests;

public class MainWindowViewModelTests
{
    private static MainWindowViewModel CreateViewModel(
        out IAudioEngine audioEngine,
        out IFilePickerService filePickerService)
    {
        audioEngine = Substitute.For<IAudioEngine>();
        filePickerService = Substitute.For<IFilePickerService>();
        return new MainWindowViewModel(audioEngine, filePickerService, NullLogger<MainWindowViewModel>.Instance);
    }

    [Test]
    public async Task OpenFileCommand_WhenFileSelected_LoadsIntoEngine()
    {
        var vm = CreateViewModel(out var audioEngine, out var filePickerService);
        filePickerService.PickAudioFileAsync().Returns("/music/song.mp3");

        await vm.OpenFileCommand.ExecuteAsync(null);

        audioEngine.Received(1).Load("/music/song.mp3");
        await Assert.That(vm.LoadedFileName).IsEqualTo("song.mp3");
    }

    [Test]
    public async Task OpenFileCommand_WhenEngineThrows_SetsStatusMessageInsteadOfCrashing()
    {
        var vm = CreateViewModel(out var audioEngine, out var filePickerService);
        filePickerService.PickAudioFileAsync().Returns("/music/broken.mp4");
        audioEngine.When(e => e.Load("/music/broken.mp4"))
            .Throw(new InvalidOperationException("no audio stream found"));

        await vm.OpenFileCommand.ExecuteAsync(null);

        await Assert.That(vm.StatusMessage).IsNotNull();
        await Assert.That(vm.LoadedFileName).IsNull();
    }

    [Test]
    public async Task OpenFileCommand_WhenLoadSucceedsAfterAPreviousFailure_ClearsStatusMessage()
    {
        var vm = CreateViewModel(out var audioEngine, out var filePickerService);
        filePickerService.PickAudioFileAsync().Returns("/music/broken.mp4");
        audioEngine.When(e => e.Load("/music/broken.mp4"))
            .Throw(new InvalidOperationException("no audio stream found"));
        await vm.OpenFileCommand.ExecuteAsync(null);
        await Assert.That(vm.StatusMessage).IsNotNull();

        filePickerService.PickAudioFileAsync().Returns("/music/song.mp3");
        await vm.OpenFileCommand.ExecuteAsync(null);

        await Assert.That(vm.StatusMessage).IsNull();
        await Assert.That(vm.LoadedFileName).IsEqualTo("song.mp3");
    }

    [Test]
    public async Task DisplayFileName_ReflectsLoadedFileNameOrDefaultText()
    {
        var vm = CreateViewModel(out _, out var filePickerService);
        await Assert.That(vm.DisplayFileName).IsEqualTo("No file loaded");

        filePickerService.PickAudioFileAsync().Returns("/music/song.mp3");
        await vm.OpenFileCommand.ExecuteAsync(null);

        await Assert.That(vm.DisplayFileName).IsEqualTo("song.mp3");
    }

    [Test]
    public async Task OpenFileCommand_WhenNoFileSelected_DoesNotLoadIntoEngine()
    {
        var vm = CreateViewModel(out var audioEngine, out var filePickerService);
        filePickerService.PickAudioFileAsync().Returns((string?)null);

        await vm.OpenFileCommand.ExecuteAsync(null);

        audioEngine.DidNotReceive().Load(Arg.Any<string>());
        await Assert.That(vm.LoadedFileName).IsNull();
    }

    [Test]
    public async Task PlayPauseCommand_WhenNotPlaying_PlaysAndUpdatesState()
    {
        var vm = CreateViewModel(out var audioEngine, out _);

        vm.PlayPauseCommand.Execute(null);

        audioEngine.Received(1).Play();
        await Assert.That(vm.IsPlaying).IsTrue();
        await Assert.That(vm.PlayPauseLabel).IsEqualTo("Pause");
    }

    [Test]
    public async Task PlayPauseCommand_WhenPlaying_PausesAndUpdatesState()
    {
        var vm = CreateViewModel(out var audioEngine, out _);
        vm.PlayPauseCommand.Execute(null);

        vm.PlayPauseCommand.Execute(null);

        audioEngine.Received(1).Pause();
        await Assert.That(vm.IsPlaying).IsFalse();
        await Assert.That(vm.PlayPauseLabel).IsEqualTo("Play");
    }
}