using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class LibraryViewModelTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-library-vm-settings-{Guid.NewGuid():N}.json");

    private static LibraryViewModel CreateViewModel(
        out IAudioEngine audioEngine,
        out IFilePickerService filePickerService,
        out PlaybackControlsViewModel playbackControls,
        string? settingsFilePath = null)
    {
        audioEngine = Substitute.For<IAudioEngine>();
        filePickerService = Substitute.For<IFilePickerService>();
        var queue = new PlaybackQueue();
        playbackControls = new PlaybackControlsViewModel(audioEngine, queue, NullLogger<PlaybackControlsViewModel>.Instance);
        return new LibraryViewModel(
            filePickerService,
            playbackControls,
            settingsFilePath ?? CreateTempSettingsPath(),
            NullLogger<LibraryViewModel>.Instance);
    }

    [Test]
    public async Task AddFolderCommand_WhenFolderSelected_PopulatesTracksFromScan()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            File.WriteAllBytes(Path.Combine(root.FullName, "song.mp3"), []);
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);

            await vm.AddFolderCommand.ExecuteAsync(null);

            await Assert.That(vm.Tracks.Count).IsEqualTo(1);
            await Assert.That(vm.Tracks[0].Track.FilePath).IsEqualTo(Path.Combine(root.FullName, "song.mp3"));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task HasLibraryFolders_WhenNothingAdded_IsFalse()
    {
        var vm = CreateViewModel(out _, out _, out _);

        await Assert.That(vm.LibraryFolderPaths).IsEmpty();
        await Assert.That(vm.HasLibraryFolders).IsFalse();
    }

    [Test]
    public async Task AddFolderCommand_WhenFolderSelected_AddsToLibraryFolderPaths()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);

            await vm.AddFolderCommand.ExecuteAsync(null);

            await Assert.That(vm.LibraryFolderPaths).IsEquivalentTo([root.FullName]);
            await Assert.That(vm.HasLibraryFolders).IsTrue();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task AddFolderCommand_WhenFolderAlreadyAdded_DoesNotAddDuplicate()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);

            await vm.AddFolderCommand.ExecuteAsync(null);
            await vm.AddFolderCommand.ExecuteAsync(null);

            await Assert.That(vm.LibraryFolderPaths).IsEquivalentTo([root.FullName]);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task AddFolderCommand_WithMultipleFolders_MergesTracksFromBoth()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var rootA = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-a-");
        var rootB = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-b-");
        try
        {
            File.WriteAllBytes(Path.Combine(rootA.FullName, "songA.mp3"), []);
            File.WriteAllBytes(Path.Combine(rootB.FullName, "songB.mp3"), []);

            filePickerService.PickLibraryFolderAsync().Returns(rootA.FullName);
            await vm.AddFolderCommand.ExecuteAsync(null);
            filePickerService.PickLibraryFolderAsync().Returns(rootB.FullName);
            await vm.AddFolderCommand.ExecuteAsync(null);

            await Assert.That(vm.Tracks.Count).IsEqualTo(2);
            await Assert.That(vm.Tracks.Select(t => t.Track.FilePath)).Contains(Path.Combine(rootA.FullName, "songA.mp3"));
            await Assert.That(vm.Tracks.Select(t => t.Track.FilePath)).Contains(Path.Combine(rootB.FullName, "songB.mp3"));
        }
        finally
        {
            rootA.Delete(recursive: true);
            rootB.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ApplyPendingFolderChangesCommand_AfterRemovingAPendingFolder_RescansRemainingFolders()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var rootA = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-a-");
        var rootB = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-b-");
        try
        {
            File.WriteAllBytes(Path.Combine(rootA.FullName, "songA.mp3"), []);
            File.WriteAllBytes(Path.Combine(rootB.FullName, "songB.mp3"), []);

            filePickerService.PickLibraryFolderAsync().Returns(rootA.FullName);
            await vm.AddFolderCommand.ExecuteAsync(null);
            filePickerService.PickLibraryFolderAsync().Returns(rootB.FullName);
            await vm.AddFolderCommand.ExecuteAsync(null);

            vm.RemovePendingFolderCommand.Execute(rootA.FullName);
            await vm.ApplyPendingFolderChangesCommand.ExecuteAsync(null);

            await Assert.That(vm.LibraryFolderPaths).IsEquivalentTo([rootB.FullName]);
            await Assert.That(vm.Tracks.Count).IsEqualTo(1);
            await Assert.That(vm.Tracks[0].Track.FilePath).IsEqualTo(Path.Combine(rootB.FullName, "songB.mp3"));
        }
        finally
        {
            rootA.Delete(recursive: true);
            rootB.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ApplyPendingFolderChangesCommand_WhenLastFolderRemoved_ClearsTracks()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            File.WriteAllBytes(Path.Combine(root.FullName, "song.mp3"), []);
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
            await vm.AddFolderCommand.ExecuteAsync(null);

            vm.RemovePendingFolderCommand.Execute(root.FullName);
            await vm.ApplyPendingFolderChangesCommand.ExecuteAsync(null);

            await Assert.That(vm.LibraryFolderPaths).IsEmpty();
            await Assert.That(vm.Tracks).IsEmpty();
            await Assert.That(vm.HasLibraryFolders).IsFalse();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task RemovePendingFolderCommand_BeforeApply_DoesNotChangeLibraryFolderPaths()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
            await vm.AddFolderCommand.ExecuteAsync(null);

            vm.RemovePendingFolderCommand.Execute(root.FullName);

            await Assert.That(vm.LibraryFolderPaths).IsEquivalentTo([root.FullName]);
            await Assert.That(vm.PendingLibraryFolderPaths).IsEmpty();
            await Assert.That(vm.HasPendingChanges).IsTrue();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task CancelPendingFolderChangesCommand_DiscardsPendingRemoval()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
            await vm.AddFolderCommand.ExecuteAsync(null);

            vm.RemovePendingFolderCommand.Execute(root.FullName);
            vm.CancelPendingFolderChangesCommand.Execute(null);

            await Assert.That(vm.PendingLibraryFolderPaths).IsEquivalentTo([root.FullName]);
            await Assert.That(vm.HasPendingChanges).IsFalse();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task AddFolderCommand_WhenNoFolderSelected_DoesNotTouchTracks()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        filePickerService.PickLibraryFolderAsync().Returns((string?)null);

        await vm.AddFolderCommand.ExecuteAsync(null);

        await Assert.That(vm.Tracks).IsEmpty();
    }

    [Test]
    public async Task AddFolderCommand_WhenFolderDoesNotExist_SetsStatusMessage()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        filePickerService.PickLibraryFolderAsync().Returns("/no/such/folder");

        await vm.AddFolderCommand.ExecuteAsync(null);

        await Assert.That(vm.StatusMessage).IsNotNull();
    }

    [Test]
    public async Task HasTracksAndNoTracks_ReflectWhetherAnyTracksAreLoaded()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        await Assert.That(vm.NoTracks).IsTrue();
        await Assert.That(vm.HasTracks).IsFalse();

        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            File.WriteAllBytes(Path.Combine(root.FullName, "song.mp3"), []);
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);

            await vm.AddFolderCommand.ExecuteAsync(null);

            await Assert.That(vm.HasTracks).IsTrue();
            await Assert.That(vm.NoTracks).IsFalse();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task AddFolderCommand_PersistsFolderPathForNextLaunch()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = CreateViewModel(out _, out var filePickerService, out _, settingsPath);
            var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
            try
            {
                filePickerService.PickLibraryFolderAsync().Returns(root.FullName);

                await vm.AddFolderCommand.ExecuteAsync(null);

                await Assert.That(LibrarySettingsStore.LoadLibraryFolderPaths(settingsPath)).IsEquivalentTo([root.FullName]);
            }
            finally
            {
                root.Delete(recursive: true);
            }
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task InitializeAsync_WhenFoldersRemembered_ScansThemAutomatically()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            File.WriteAllBytes(Path.Combine(root.FullName, "song.mp3"), []);
            LibrarySettingsStore.SaveLibraryFolderPaths(settingsPath, [root.FullName]);
            var vm = CreateViewModel(out _, out _, out _, settingsPath);

            await vm.InitializeAsync();

            await Assert.That(vm.Tracks.Count).IsEqualTo(1);
            await Assert.That(vm.Tracks[0].Track.FilePath).IsEqualTo(Path.Combine(root.FullName, "song.mp3"));
        }
        finally
        {
            root.Delete(recursive: true);
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task InitializeAsync_WhenNothingRemembered_LeavesTracksEmpty()
    {
        var vm = CreateViewModel(out _, out _, out _);

        await vm.InitializeAsync();

        await Assert.That(vm.Tracks).IsEmpty();
    }

    [Test]
    public async Task ISettingsCategoryViewModel_ApplyCommand_AppliesPendingFolderChanges()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var category = (ISettingsCategoryViewModel)vm;
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
            await vm.AddFolderCommand.ExecuteAsync(null);
            vm.RemovePendingFolderCommand.Execute(root.FullName);

            await Assert.That(category.HasPendingChanges).IsTrue();

            await ((IAsyncRelayCommand)category.ApplyCommand).ExecuteAsync(null);

            await Assert.That(vm.LibraryFolderPaths).IsEmpty();
            await Assert.That(category.HasPendingChanges).IsFalse();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ISettingsCategoryViewModel_CancelCommand_DiscardsPendingFolderChanges()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var category = (ISettingsCategoryViewModel)vm;
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
            await vm.AddFolderCommand.ExecuteAsync(null);
            vm.RemovePendingFolderCommand.Execute(root.FullName);

            category.CancelCommand.Execute(null);

            await Assert.That(vm.PendingLibraryFolderPaths).IsEquivalentTo([root.FullName]);
            await Assert.That(category.HasPendingChanges).IsFalse();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task PlayNowCommand_LoadsIntoEngineAndStartsPlaybackViaPlaybackControls()
    {
        var vm = CreateViewModel(out var audioEngine, out _, out var playbackControls);
        var track = new Track("/music/song.mp3", "song.mp3");

        await vm.PlayNowCommand.ExecuteAsync(track);

        audioEngine.Received(1).Load("/music/song.mp3");
        await Assert.That(playbackControls.LoadedFileName).IsEqualTo("song.mp3");
        await Assert.That(playbackControls.IsPlaying).IsTrue();
    }

    [Test]
    public async Task PlayNextCommand_InsertsTrackAtFrontOfQueue()
    {
        var vm = CreateViewModel(out _, out _, out var playbackControls);
        await playbackControls.AddToQueue(new Track("/music/existing.mp3", "existing.mp3"));
        var track = new Track("/music/song.mp3", "song.mp3");

        vm.PlayNextCommand.Execute(track);

        await Assert.That(playbackControls.QueueEntries[0].Track).IsEqualTo(track);
    }

    [Test]
    public async Task AddToQueueCommand_AppendsTrackToQueue()
    {
        var vm = CreateViewModel(out _, out _, out var playbackControls);
        var first = new Track("/music/first.mp3", "first.mp3");
        var second = new Track("/music/second.mp3", "second.mp3");

        vm.AddToQueueCommand.Execute(first);
        vm.AddToQueueCommand.Execute(second);

        await Assert.That(playbackControls.QueueEntries[0].Track).IsEqualTo(first);
        await Assert.That(playbackControls.QueueEntries[1].Track).IsEqualTo(second);
    }

    [Test]
    public async Task HidingEveryColumn_LeavesTheLastOneVisible()
    {
        var vm = CreateViewModel(out _, out _, out _);

        vm.IsTrackNumberColumnVisible = false;
        vm.IsTitleColumnVisible = false;
        vm.IsArtistColumnVisible = false;
        vm.IsAlbumColumnVisible = false;
        vm.IsLengthColumnVisible = false;
        vm.IsSampleRateColumnVisible = false;
        vm.IsBitDepthColumnVisible = false;
        vm.IsBitrateColumnVisible = false;
        vm.IsFileTypeColumnVisible = false;
        vm.IsYearColumnVisible = false;

        await Assert.That(vm.IsYearColumnVisible).IsTrue();
    }

    [Test]
    public async Task HidingAColumn_WhileAnotherIsStillVisible_Succeeds()
    {
        var vm = CreateViewModel(out _, out _, out _);

        vm.IsTrackNumberColumnVisible = false;

        await Assert.That(vm.IsTrackNumberColumnVisible).IsFalse();
        await Assert.That(vm.IsTitleColumnVisible).IsTrue();
    }

    [Test]
    public async Task ColumnVisibility_PersistsAcrossInstancesForTheSameSettingsFile()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = CreateViewModel(out _, out _, out _, settingsPath);
            vm.IsArtistColumnVisible = false;
            vm.IsYearColumnVisible = false;

            var restarted = CreateViewModel(out _, out _, out _, settingsPath);
            await restarted.InitializeAsync();

            await Assert.That(restarted.IsArtistColumnVisible).IsFalse();
            await Assert.That(restarted.IsYearColumnVisible).IsFalse();
            await Assert.That(restarted.IsTitleColumnVisible).IsTrue();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }
}
