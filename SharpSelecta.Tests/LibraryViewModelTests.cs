using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Resources;
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

    private static string IndexFilePath(string settingsPath) =>
        Path.Combine(Path.GetDirectoryName(settingsPath)!, $"{Path.GetFileNameWithoutExtension(settingsPath)}.library-index.db");

    private static LibraryViewModel CreateViewModel(
        out IAudioEngine audioEngine,
        out IFilePickerService filePickerService,
        out PlaybackControlsViewModel playbackControls,
        string? settingsFilePath = null) =>
        CreateViewModel(out audioEngine, out filePickerService, out playbackControls, out _, settingsFilePath);

    private static LibraryViewModel CreateViewModel(
        out IAudioEngine audioEngine,
        out IFilePickerService filePickerService,
        out PlaybackControlsViewModel playbackControls,
        out IFileManagerService fileManagerService,
        string? settingsFilePath = null)
    {
        audioEngine = Substitute.For<IAudioEngine>();
        filePickerService = Substitute.For<IFilePickerService>();
        fileManagerService = Substitute.For<IFileManagerService>();
        var queue = new PlaybackQueue();
        playbackControls = new PlaybackControlsViewModel(audioEngine, queue, NullLogger<PlaybackControlsViewModel>.Instance);
        return new LibraryViewModel(
            filePickerService,
            playbackControls,
            fileManagerService,
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
    public async Task RescanCommand_PicksUpFilesAddedSinceTheLastScan()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            File.WriteAllBytes(Path.Combine(root.FullName, "songA.mp3"), []);
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
            await vm.AddFolderCommand.ExecuteAsync(null);
            await Assert.That(vm.Tracks.Count).IsEqualTo(1);

            File.WriteAllBytes(Path.Combine(root.FullName, "songB.mp3"), []);
            await vm.RescanCommand.ExecuteAsync(null);

            await Assert.That(vm.Tracks.Count).IsEqualTo(2);
        }
        finally
        {
            root.Delete(recursive: true);
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
    public async Task ApplyPendingFolderChangesCommand_WithNoPendingChanges_DoesNotRescan()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            File.WriteAllBytes(Path.Combine(root.FullName, "songA.mp3"), []);
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
            await vm.AddFolderCommand.ExecuteAsync(null);
            await Assert.That(vm.Tracks.Count).IsEqualTo(1);

            File.WriteAllBytes(Path.Combine(root.FullName, "songB.mp3"), []);
            await vm.ApplyPendingFolderChangesCommand.ExecuteAsync(null);

            await Assert.That(vm.Tracks.Count).IsEqualTo(1);
        }
        finally
        {
            root.Delete(recursive: true);
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

                await Assert.That(SettingsStore.LoadLibraryFolderPaths(settingsPath)).IsEquivalentTo([root.FullName]);
            }
            finally
            {
                root.Delete(recursive: true);
            }
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
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
            SettingsStore.SaveLibraryFolderPaths(settingsPath, [root.FullName]);
            var vm = CreateViewModel(out _, out _, out _, settingsPath);

            await vm.InitializeAsync();

            await Assert.That(vm.Tracks.Count).IsEqualTo(1);
            await Assert.That(vm.Tracks[0].Track.FilePath).IsEqualTo(Path.Combine(root.FullName, "song.mp3"));
        }
        finally
        {
            root.Delete(recursive: true);
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
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
    public async Task InitializeAsync_WhenIndexAlreadyHasMatchingTracks_HydratesFromIndexImmediately()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), trackPath);
            SettingsStore.SaveLibraryFolderPaths(settingsPath, [root.FullName]);
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            var vm = CreateViewModel(out _, out _, out _, settingsPath);
            await vm.InitializeAsync();

            await Assert.That(vm.Tracks.Count).IsEqualTo(1);
            await Assert.That(vm.Tracks[0].Track.FilePath).IsEqualTo(trackPath);
            await Assert.That(vm.Tracks[0].Track.Title).IsEqualTo("Test Song");
        }
        finally
        {
            root.Delete(recursive: true);
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
        }
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
            File.Delete(IndexFilePath(settingsPath));
        }
    }

    private static void AddTrack(LibraryViewModel vm, string filePath, string? album, string? artist, int? trackNumber = null, string? albumArtist = null, string? title = null) =>
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(filePath, filePath) { Album = album, Artist = artist, AlbumArtist = albumArtist, TrackNumber = trackNumber, Title = title }, vm));

    [Test]
    public async Task Albums_GroupsTracksByAlbumTitle_IgnoringArtist()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, "/music/a.mp3", "Compilation", "Artist One");
        AddTrack(vm, "/music/b.mp3", "Compilation", "Artist Two");

        await Assert.That(vm.Grid.Albums.Count).IsEqualTo(1);
        await Assert.That(vm.Grid.Albums[0].Tracks.Count).IsEqualTo(2);
        await Assert.That(vm.Grid.Albums[0].Artist).IsEqualTo(Strings.VariousArtists);
    }

    [Test]
    public async Task Albums_WithSameTitleButDifferentAlbumArtist_StaysAsSeparateAlbums()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, "/music/a.mp3", "Bassline", "Capital", albumArtist: "Capital");
        AddTrack(vm, "/music/b.mp3", "Bassline", "K Dot", albumArtist: "K Dot & DJ Q");

        await Assert.That(vm.Grid.Albums.Count).IsEqualTo(2);
        await Assert.That(vm.Grid.Albums[0].Title).IsEqualTo("Bassline");
        await Assert.That(vm.Grid.Albums[1].Title).IsEqualTo("Bassline");
        await Assert.That(vm.Grid.Albums.Select(a => a.Tracks.Count)).IsEquivalentTo([1, 1]);
    }

    [Test]
    public async Task Albums_GroupingIgnoresCaseAndSurroundingWhitespace()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, "/music/a.mp3", "Discovery", "Daft Punk");
        AddTrack(vm, "/music/b.mp3", "  discovery ", "Daft Punk");

        await Assert.That(vm.Grid.Albums.Count).IsEqualTo(1);
        await Assert.That(vm.Grid.Albums[0].Tracks.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Albums_WithNoAlbumTag_FallBackToUnknownAlbumBucket()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, "/music/a.mp3", album: null, artist: "Some Artist");
        AddTrack(vm, "/music/b.mp3", album: null, artist: "Some Artist");

        await Assert.That(vm.Grid.Albums.Count).IsEqualTo(1);
        await Assert.That(vm.Grid.Albums[0].Title).IsEqualTo(Strings.UnknownAlbum);
    }

    [Test]
    public async Task Albums_OrdersTracksByTrackNumber()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, "/music/b.mp3", "Album", "Artist", trackNumber: 2);
        AddTrack(vm, "/music/a.mp3", "Album", "Artist", trackNumber: 1);

        await Assert.That(vm.Grid.Albums[0].Tracks[0].Track.FilePath).IsEqualTo("/music/a.mp3");
        await Assert.That(vm.Grid.Albums[0].Tracks[1].Track.FilePath).IsEqualTo("/music/b.mp3");
    }

    [Test]
    public async Task Albums_WithNoTrackNumber_FallsBackToOrderingByTitle()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, "/music/a.mp3", "Album", "Artist", title: "Zebra");
        AddTrack(vm, "/music/b.mp3", "Album", "Artist", title: "Apple");

        await Assert.That(vm.Grid.Albums[0].Tracks[0].Track.Title).IsEqualTo("Apple");
        await Assert.That(vm.Grid.Albums[0].Tracks[1].Track.Title).IsEqualTo("Zebra");
    }

    [Test]
    public async Task TrackRows_WithMultipleArtists_ShowsEachTracksArtistAsSuffix()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, "/music/a.mp3", "Compilation", "Artist One");
        AddTrack(vm, "/music/b.mp3", "Compilation", "Artist Two");

        var rows = vm.Grid.Albums[0].TrackRows;
        await Assert.That(rows[0].ArtistSuffix).IsEqualTo("(Artist One)");
        await Assert.That(rows[1].ArtistSuffix).IsEqualTo("(Artist Two)");
    }

    [Test]
    public async Task TrackRows_WithASingleArtist_HasNoArtistSuffix()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, "/music/a.mp3", "Album", "Only Artist");
        AddTrack(vm, "/music/b.mp3", "Album", "Only Artist");

        await Assert.That(vm.Grid.Albums[0].TrackRows.Select(r => r.ArtistSuffix)).IsEquivalentTo(new string?[] { null, null });
    }

    [Test]
    public async Task Albums_WithASingleArtist_ShowsThatArtistNotVariousArtists()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, "/music/a.mp3", "Album", "Only Artist");
        AddTrack(vm, "/music/b.mp3", "Album", "Only Artist");

        await Assert.That(vm.Grid.Albums[0].Artist).IsEqualTo("Only Artist");
    }

    [Test]
    public async Task Albums_WithNoArtistTag_ShowsBlankArtist()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, "/music/a.mp3", "Album", artist: null);

        await Assert.That(vm.Grid.Albums[0].Artist).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ShowEmptyState_WhileLibraryIsLoading_IsFalseEvenWithNoTracksYet()
    {
        var vm = CreateViewModel(out _, out _, out _);
        await Assert.That(vm.NoTracks).IsTrue();

        vm.IsLoadingLibrary = true;

        await Assert.That(vm.ShowEmptyState).IsFalse();
    }

    [Test]
    public async Task ViewVisibility_WhileLibraryIsLoading_IsFalseEvenWithTracksAlreadyShown()
    {
        var vm = CreateViewModel(out _, out _, out _);
        AddTrack(vm, "/music/a.mp3", "Album A", "Artist");
        await Assert.That(vm.HasTracks).IsTrue();

        vm.IsLoadingLibrary = true;

        await Assert.That(vm.IsTrackListViewVisible).IsFalse();
        await Assert.That(vm.IsAlbumGridViewVisible).IsFalse();
    }

    [Test]
    public async Task IsLoadingLibrary_IsFalseAfterFoldersFinishLoading()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = Directory.CreateTempSubdirectory("sharpselecta-library-vm-tests-");
        try
        {
            filePickerService.PickLibraryFolderAsync().Returns(root.FullName);

            await vm.AddFolderCommand.ExecuteAsync(null);

            await Assert.That(vm.IsLoadingLibrary).IsFalse();
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ClearArtworkCacheCommand_DeletesCachedThumbnailFiles()
    {
        var settingsPath = CreateTempSettingsPath();
        var cacheDirectory = Path.Combine(Path.GetDirectoryName(settingsPath)!, "artwork-cache");
        try
        {
            var vm = CreateViewModel(out _, out _, out _, settingsPath);
            Directory.CreateDirectory(cacheDirectory);
            File.WriteAllBytes(Path.Combine(cacheDirectory, "fake.jpg"), [1, 2, 3]);

            vm.Grid.ClearArtworkCacheCommand.Execute(null);

            await Assert.That(Directory.EnumerateFiles(cacheDirectory)).IsEmpty();
        }
        finally
        {
            File.Delete(settingsPath);
            File.Delete(IndexFilePath(settingsPath));
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    [Test]
    public async Task DisplayedTracks_WithNoSearchQuery_MirrorsTracks()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, "/music/a.mp3", "Album A", "Artist A", title: "Song A");
        AddTrack(vm, "/music/b.mp3", "Album B", "Artist B", title: "Song B");

        await Assert.That(vm.DisplayedTracks.Select(t => t.Track.FilePath)).IsEquivalentTo(vm.Tracks.Select(t => t.Track.FilePath));
    }

    [Test]
    public async Task DisplayedTracks_WithASearchQuery_FiltersAndRanksByTitleArtistOrAlbum()
    {
        var vm = CreateViewModel(out _, out _, out _);
        AddTrack(vm, "/music/a.mp3", "Morning Glory", "Oasis", title: "Wonderwall");
        AddTrack(vm, "/music/b.mp3", "Legend", "Bob Marley", title: "No Woman No Cry");
        AddTrack(vm, "/music/c.mp3", "Some Album", "Some Artist", title: "Unrelated Song");

        vm.SearchQuery = "oasis";

        await Assert.That(vm.DisplayedTracks.Count).IsEqualTo(1);
        await Assert.That(vm.DisplayedTracks[0].Track.FilePath).IsEqualTo("/music/a.mp3");
    }

    [Test]
    public async Task DisplayedTracks_UpdatesWhenTracksChangeWhileAQueryIsActive()
    {
        var vm = CreateViewModel(out _, out _, out _);
        AddTrack(vm, "/music/a.mp3", "Morning Glory", "Oasis", title: "Wonderwall");
        vm.SearchQuery = "oasis";
        await Assert.That(vm.DisplayedTracks.Count).IsEqualTo(1);

        AddTrack(vm, "/music/b.mp3", "Definitely Maybe", "Oasis", title: "Live Forever");

        await Assert.That(vm.DisplayedTracks.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ClearSearchCommand_ResetsSearchQueryAndRestoresAllTracks()
    {
        var vm = CreateViewModel(out _, out _, out _);
        AddTrack(vm, "/music/a.mp3", "Morning Glory", "Oasis", title: "Wonderwall");
        AddTrack(vm, "/music/b.mp3", "Legend", "Bob Marley", title: "No Woman No Cry");
        vm.SearchQuery = "oasis";

        vm.ClearSearchCommand.Execute(null);

        await Assert.That(vm.SearchQuery).IsEqualTo("");
        await Assert.That(vm.DisplayedTracks.Count).IsEqualTo(2);
    }

    [Test]
    public async Task FocusSearchCommand_RaisesSearchFocusRequested()
    {
        var vm = CreateViewModel(out _, out _, out _);
        var raised = false;
        vm.SearchFocusRequested += (_, _) => raised = true;

        vm.FocusSearchCommand.Execute(null);

        await Assert.That(raised).IsTrue();
    }

    [Test]
    public async Task ShowInFileManagerLabel_ReflectsTheServicesActionLabel()
    {
        var vm = CreateViewModel(out _, out _, out _, out var fileManagerService);
        fileManagerService.ActionLabel.Returns("Show in Testolinux");

        await Assert.That(vm.ShowInFileManagerLabel).IsEqualTo("Show in Testolinux");
    }

    [Test]
    public async Task ShowInFileManagerCommand_RevealsTheTracksFile()
    {
        var vm = CreateViewModel(out _, out _, out _, out var fileManagerService);
        var track = new Track("/music/song.mp3", "song.mp3");

        vm.ShowInFileManagerCommand.Execute(track);

        fileManagerService.Received(1).RevealInFileManager("/music/song.mp3");
    }

    [Test]
    public async Task ShowAlbumInFileManagerCommand_RevealsTheFirstTracksFile()
    {
        var vm = CreateViewModel(out _, out _, out _, out var fileManagerService);
        AddTrack(vm, "/music/b.mp3", "Album", "Artist", trackNumber: 2);
        AddTrack(vm, "/music/a.mp3", "Album", "Artist", trackNumber: 1);

        vm.ShowAlbumInFileManagerCommand.Execute(vm.Grid.Albums[0]);

        fileManagerService.Received(1).RevealInFileManager("/music/a.mp3");
    }
}
