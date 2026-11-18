using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

public class LibraryViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private LibraryViewModel CreateViewModel(
        out IAudioEngine audioEngine,
        out IFilePickerService filePickerService,
        out PlaybackControlsViewModel playbackControls,
        string? settingsFilePath = null) =>
        CreateViewModel(out audioEngine, out filePickerService, out playbackControls, out _, settingsFilePath);

    private LibraryViewModel CreateViewModel(
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
        var settings = new SettingsStore(settingsFilePath ?? _temp.NewSettingsPath());
        playbackControls = new PlaybackControlsViewModel(audioEngine, queue, settings, NullLogger<PlaybackControlsViewModel>.Instance);
        return new LibraryViewModel(
            filePickerService,
            playbackControls,
            fileManagerService,
            settings,
            TestThemeLayout.Default,
            NullLogger<LibraryViewModel>.Instance);
    }

    [Test]
    public async Task AddFolderCommand_WhenFolderSelected_PopulatesTracksFromScan()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = _temp.NewSubdirectory();
        File.WriteAllBytes(Path.Combine(root.FullName, "song.mp3"), []);
        filePickerService.PickLibraryFolderAsync().Returns(root.FullName);

        await vm.Folders.AddFolderCommand.ExecuteAsync(null);

        await Assert.That(vm.Tracks.Count).IsEqualTo(1);
        await Assert.That(vm.Tracks[0].Track.FilePath).IsEqualTo(Path.Combine(root.FullName, "song.mp3"));
    }

    [Test]
    public async Task AddFolderCommand_WithMultipleFolders_MergesTracksFromBoth()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var rootA = _temp.NewSubdirectory();
        var rootB = _temp.NewSubdirectory();
        File.WriteAllBytes(Path.Combine(rootA.FullName, "songA.mp3"), []);
        File.WriteAllBytes(Path.Combine(rootB.FullName, "songB.mp3"), []);

        filePickerService.PickLibraryFolderAsync().Returns(rootA.FullName);
        await vm.Folders.AddFolderCommand.ExecuteAsync(null);
        filePickerService.PickLibraryFolderAsync().Returns(rootB.FullName);
        await vm.Folders.AddFolderCommand.ExecuteAsync(null);

        await Assert.That(vm.Tracks.Count).IsEqualTo(2);
        await Assert.That(vm.Tracks.Select(t => t.Track.FilePath)).Contains(Path.Combine(rootA.FullName, "songA.mp3"));
        await Assert.That(vm.Tracks.Select(t => t.Track.FilePath)).Contains(Path.Combine(rootB.FullName, "songB.mp3"));
    }

    [Test]
    public async Task RescanCommand_PicksUpFilesAddedSinceTheLastScan()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = _temp.NewSubdirectory();
        File.WriteAllBytes(Path.Combine(root.FullName, "songA.mp3"), []);
        filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
        await vm.Folders.AddFolderCommand.ExecuteAsync(null);
        await Assert.That(vm.Tracks.Count).IsEqualTo(1);

        File.WriteAllBytes(Path.Combine(root.FullName, "songB.mp3"), []);
        await vm.RescanCommand.ExecuteAsync(null);

        await Assert.That(vm.Tracks.Count).IsEqualTo(2);
    }

    [Test]
    public async Task RescanCommand_AfterAFileWasRenamed_PointsTheQueueAndNowPlayingAtTheNewPath()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out var playbackControls);
        var root = _temp.NewSubdirectory();
        var oldPath = Path.Combine(root.FullName, "old.mp3");
        var newPath = Path.Combine(root.FullName, "new.mp3");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), oldPath);
        filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
        await vm.Folders.AddFolderCommand.ExecuteAsync(null);
        await playbackControls.PlayNowAsync(vm.Tracks[0].Track);

        File.Move(oldPath, newPath);
        await vm.RescanCommand.ExecuteAsync(null);

        await Assert.That(playbackControls.QueueEntries.Single().Track.FilePath).IsEqualTo(newPath);
        await Assert.That(playbackControls.CurrentTrack!.FilePath).IsEqualTo(newPath);
    }

    [Test]
    public async Task ApplyPendingFolderChangesCommand_AfterRemovingAPendingFolder_RescansRemainingFolders()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var rootA = _temp.NewSubdirectory();
        var rootB = _temp.NewSubdirectory();
        File.WriteAllBytes(Path.Combine(rootA.FullName, "songA.mp3"), []);
        File.WriteAllBytes(Path.Combine(rootB.FullName, "songB.mp3"), []);

        filePickerService.PickLibraryFolderAsync().Returns(rootA.FullName);
        await vm.Folders.AddFolderCommand.ExecuteAsync(null);
        filePickerService.PickLibraryFolderAsync().Returns(rootB.FullName);
        await vm.Folders.AddFolderCommand.ExecuteAsync(null);

        vm.Folders.RemovePendingFolderCommand.Execute(rootA.FullName);
        await vm.Folders.ApplyPendingChangesCommand.ExecuteAsync(null);

        await Assert.That(vm.Folders.Paths).IsEquivalentTo([rootB.FullName]);
        await Assert.That(vm.Tracks.Count).IsEqualTo(1);
        await Assert.That(vm.Tracks[0].Track.FilePath).IsEqualTo(Path.Combine(rootB.FullName, "songB.mp3"));
    }

    [Test]
    public async Task ApplyPendingFolderChangesCommand_WithNoPendingChanges_DoesNotRescan()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = _temp.NewSubdirectory();
        File.WriteAllBytes(Path.Combine(root.FullName, "songA.mp3"), []);
        filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
        await vm.Folders.AddFolderCommand.ExecuteAsync(null);
        await Assert.That(vm.Tracks.Count).IsEqualTo(1);

        File.WriteAllBytes(Path.Combine(root.FullName, "songB.mp3"), []);
        await vm.Folders.ApplyPendingChangesCommand.ExecuteAsync(null);

        await Assert.That(vm.Tracks.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ApplyPendingFolderChangesCommand_WhenLastFolderRemoved_ClearsTracks()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = _temp.NewSubdirectory();
        File.WriteAllBytes(Path.Combine(root.FullName, "song.mp3"), []);
        filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
        await vm.Folders.AddFolderCommand.ExecuteAsync(null);

        vm.Folders.RemovePendingFolderCommand.Execute(root.FullName);
        await vm.Folders.ApplyPendingChangesCommand.ExecuteAsync(null);

        await Assert.That(vm.Folders.Paths).IsEmpty();
        await Assert.That(vm.Tracks).IsEmpty();
        await Assert.That(vm.Folders.HasFolders).IsFalse();
    }

    [Test]
    public async Task AddFolderCommand_WhenNoFolderSelected_DoesNotTouchTracks()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        filePickerService.PickLibraryFolderAsync().Returns((string?)null);

        await vm.Folders.AddFolderCommand.ExecuteAsync(null);

        await Assert.That(vm.Tracks).IsEmpty();
    }

    [Test]
    public async Task AddFolderCommand_WhenFolderDoesNotExist_SetsStatusMessage()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        filePickerService.PickLibraryFolderAsync().Returns(TestPaths.Rooted("no", "such", "folder"));

        await vm.Folders.AddFolderCommand.ExecuteAsync(null);

        await Assert.That(vm.StatusMessage).IsNotNull();
    }

    [Test]
    public async Task HasTracksAndNoTracks_ReflectWhetherAnyTracksAreLoaded()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        await Assert.That(vm.NoTracks).IsTrue();
        await Assert.That(vm.HasTracks).IsFalse();

        var root = _temp.NewSubdirectory();
        File.WriteAllBytes(Path.Combine(root.FullName, "song.mp3"), []);
        filePickerService.PickLibraryFolderAsync().Returns(root.FullName);

        await vm.Folders.AddFolderCommand.ExecuteAsync(null);

        await Assert.That(vm.HasTracks).IsTrue();
        await Assert.That(vm.NoTracks).IsFalse();
    }

    [Test]
    public async Task AddFolderCommand_PersistsFolderPathForNextLaunch()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(out _, out var filePickerService, out _, settingsPath);
        var root = _temp.NewSubdirectory();
        filePickerService.PickLibraryFolderAsync().Returns(root.FullName);

        await vm.Folders.AddFolderCommand.ExecuteAsync(null);

        await Assert.That(new SettingsStore(settingsPath).Current.LibraryFolderPaths).IsEquivalentTo([root.FullName]);
    }

    [Test]
    public async Task InitializeAsync_WhenFoldersRemembered_ScansThemAutomatically()
    {
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        File.WriteAllBytes(Path.Combine(root.FullName, "song.mp3"), []);
        new SettingsStore(settingsPath).Update(s => s with { LibraryFolderPaths = [root.FullName] });
        var vm = CreateViewModel(out _, out _, out _, settingsPath);

        await vm.InitializeAsync();

        await Assert.That(vm.Tracks.Count).IsEqualTo(1);
        await Assert.That(vm.Tracks[0].Track.FilePath).IsEqualTo(Path.Combine(root.FullName, "song.mp3"));
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
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), trackPath);
        new SettingsStore(settingsPath).Update(s => s with { LibraryFolderPaths = [root.FullName] });
        LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

        var vm = CreateViewModel(out _, out _, out _, settingsPath);
        await vm.InitializeAsync();

        await Assert.That(vm.Tracks.Count).IsEqualTo(1);
        await Assert.That(vm.Tracks[0].Track.FilePath).IsEqualTo(trackPath);
        await Assert.That(vm.Tracks[0].Track.Title).IsEqualTo("Test Song");
    }

    [Test]
    public async Task InitializeAsync_WhenNothingChangedOnDiskSinceTheIndex_ReplacesTracksOnlyOnce()
    {
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), Path.Combine(root.FullName, "a.mp3"));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), Path.Combine(root.FullName, "b.mp3"));
        new SettingsStore(settingsPath).Update(s => s with { LibraryFolderPaths = [root.FullName] });
        LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
        var vm = CreateViewModel(out _, out _, out _, settingsPath);
        var replacements = 0;
        vm.Tracks.CollectionChanged += (_, _) => replacements++;

        await vm.InitializeAsync();

        await Assert.That(replacements).IsEqualTo(1);
        await Assert.That(vm.Tracks.Count).IsEqualTo(2);
    }

    [Test]
    public async Task InitializeAsync_WhenAFileWasAddedSinceTheIndex_ShowsItAfterReconciling()
    {
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), Path.Combine(root.FullName, "a.mp3"));
        new SettingsStore(settingsPath).Update(s => s with { LibraryFolderPaths = [root.FullName] });
        LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), Path.Combine(root.FullName, "b.mp3"));
        var vm = CreateViewModel(out _, out _, out _, settingsPath);

        await vm.InitializeAsync();

        await Assert.That(vm.Tracks.Count).IsEqualTo(2);
    }

    [Test]
    public async Task InitializeAsync_WhenPlaylistSectionAndSelectionWereSaved_RestoresTheSelectedPlaylistAndItsTracks()
    {
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        var trackPath = Path.Combine(root.FullName, "song.mp3");
        File.WriteAllBytes(trackPath, []);
        new SettingsStore(settingsPath).Update(s => s with { LibraryFolderPaths = [root.FullName] });

        var vm = CreateViewModel(out _, out _, out _, settingsPath);
        await vm.InitializeAsync();
        var playlistId = vm.Playlists.CreatePlaylist("Saved");
        vm.Playlists.ReorderSelectedPlaylist([trackPath]);
        vm.LibrarySection = LibrarySection.Playlist;

        var restarted = CreateViewModel(out _, out _, out _, settingsPath);
        await restarted.InitializeAsync();

        await Assert.That(restarted.LibrarySection).IsEqualTo(LibrarySection.Playlist);
        await Assert.That(restarted.Playlists.SelectedPlaylistId).IsEqualTo(playlistId);
        await Assert.That(restarted.Playlists.Tracks.Select(t => t.Track.FilePath)).IsEquivalentTo([trackPath]);
    }

    [Test]
    public async Task InitializeAsync_WhenSavedSelectedPlaylistNoLongerExists_FallsBackToLibrarySection()
    {
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        LibraryIndexStore.Reconcile(settingsPath, [root.FullName]); // ensures the index db/schema exists
        new SettingsStore(settingsPath).Update(s => s with { LibrarySection = LibrarySection.Playlist, SelectedPlaylistId = "does-not-exist" });

        var vm = CreateViewModel(out _, out _, out _, settingsPath);
        await vm.InitializeAsync();

        await Assert.That(vm.LibrarySection).IsEqualTo(LibrarySection.Library);
        await Assert.That(vm.Playlists.SelectedPlaylistId).IsNull();
        await Assert.That(vm.Playlists.Tracks).IsEmpty();
    }

    [Test]
    public async Task ISettingsCategoryViewModel_ApplyCommand_AppliesPendingFolderChanges()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var category = (ISettingsCategoryViewModel)vm;
        var root = _temp.NewSubdirectory();
        filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
        await vm.Folders.AddFolderCommand.ExecuteAsync(null);
        vm.Folders.RemovePendingFolderCommand.Execute(root.FullName);

        await Assert.That(category.HasPendingChanges).IsTrue();

        await ((IAsyncRelayCommand)category.ApplyCommand).ExecuteAsync(null);

        await Assert.That(vm.Folders.Paths).IsEmpty();
        await Assert.That(category.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task ISettingsCategoryViewModel_CancelCommand_DiscardsPendingFolderChanges()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var category = (ISettingsCategoryViewModel)vm;
        var root = _temp.NewSubdirectory();
        filePickerService.PickLibraryFolderAsync().Returns(root.FullName);
        await vm.Folders.AddFolderCommand.ExecuteAsync(null);
        vm.Folders.RemovePendingFolderCommand.Execute(root.FullName);

        category.CancelCommand.Execute(null);

        await Assert.That(vm.Folders.PendingPaths).IsEquivalentTo([root.FullName]);
        await Assert.That(category.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task ISettingsCategoryViewModel_HasPendingChanges_NotifiesWhenTheFoldersChange()
    {
        var vm = CreateViewModel(out _, out _, out _);
        vm.Folders.Restore([TestPaths.Music()]);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.Folders.RemovePendingFolderCommand.Execute(TestPaths.Music());

        await Assert.That(raised).Contains(nameof(ISettingsCategoryViewModel.HasPendingChanges));
    }

    [Test]
    public async Task PlayNowCommand_LoadsIntoEngineAndStartsPlaybackViaPlaybackControls()
    {
        var vm = CreateViewModel(out var audioEngine, out _, out var playbackControls);
        var track = new Track(TestPaths.Music("song.mp3"), "song.mp3");

        await vm.PlayNowCommand.ExecuteAsync(track);

        audioEngine.Received(1).Load(TestPaths.Music("song.mp3"));
        await Assert.That(playbackControls.LoadedFileName).IsEqualTo("song.mp3");
        await Assert.That(playbackControls.IsPlaying).IsTrue();
    }

    [Test]
    public async Task PlayNextCommand_InsertsTrackAtFrontOfQueue()
    {
        var vm = CreateViewModel(out _, out _, out var playbackControls);
        await playbackControls.AddToQueue(new Track(TestPaths.Music("existing.mp3"), "existing.mp3"));
        var track = new Track(TestPaths.Music("song.mp3"), "song.mp3");

        vm.PlayNextCommand.Execute(track);

        await Assert.That(playbackControls.QueueEntries[0].Track).IsEqualTo(track);
    }

    [Test]
    public async Task AddToQueueCommand_AppendsTrackToQueue()
    {
        var vm = CreateViewModel(out _, out _, out var playbackControls);
        var first = new Track(TestPaths.Music("first.mp3"), "first.mp3");
        var second = new Track(TestPaths.Music("second.mp3"), "second.mp3");

        vm.AddToQueueCommand.Execute(first);
        vm.AddToQueueCommand.Execute(second);

        await Assert.That(playbackControls.QueueEntries[0].Track).IsEqualTo(first);
        await Assert.That(playbackControls.QueueEntries[1].Track).IsEqualTo(second);
    }

    [Test]
    public async Task PlayNextCommand_WhenClickedTrackIsPartOfTheOrderedSelection_ActsOnTheWholeSelectionInSelectionOrder()
    {
        var vm = CreateViewModel(out _, out _, out var playbackControls);
        var first = new Track(TestPaths.Music("first.mp3"), "first.mp3");
        var third = new Track(TestPaths.Music("third.mp3"), "third.mp3");

        // Selected out of visual order: third, then first - the ordered list must preserve that.
        // A track that was never selected (e.g. one sitting between them in the grid) stays out.
        vm.SetSelectedTracksInOrder([third, first]);

        vm.PlayNextCommand.Execute(first);

        await Assert.That(playbackControls.QueueEntries.Count).IsEqualTo(2);
        await Assert.That(playbackControls.QueueEntries[0].Track).IsEqualTo(third);
        await Assert.That(playbackControls.QueueEntries[1].Track).IsEqualTo(first);
    }

    [Test]
    public async Task AddToQueueCommand_WhenClickedTrackIsPartOfTheOrderedSelection_ActsOnTheWholeSelectionInSelectionOrder()
    {
        var vm = CreateViewModel(out _, out _, out var playbackControls);
        var first = new Track(TestPaths.Music("first.mp3"), "first.mp3");
        var second = new Track(TestPaths.Music("second.mp3"), "second.mp3");

        vm.SetSelectedTracksInOrder([second, first]);

        vm.AddToQueueCommand.Execute(second);

        await Assert.That(playbackControls.QueueEntries[0].Track).IsEqualTo(second);
        await Assert.That(playbackControls.QueueEntries[1].Track).IsEqualTo(first);
    }

    [Test]
    public async Task PlayNextCommand_WhenClickedTrackIsOutsideTheOrderedSelection_ActsOnlyOnThatTrack()
    {
        var vm = CreateViewModel(out _, out _, out var playbackControls);
        var selectedTrack = new Track(TestPaths.Music("selected.mp3"), "selected.mp3");
        var clickedTrack = new Track(TestPaths.Music("clicked.mp3"), "clicked.mp3");

        vm.SetSelectedTracksInOrder([selectedTrack]);

        vm.PlayNextCommand.Execute(clickedTrack);

        await Assert.That(playbackControls.QueueEntries.Count).IsEqualTo(1);
        await Assert.That(playbackControls.QueueEntries[0].Track).IsEqualTo(clickedTrack);
    }

    [Test]
    public async Task AddToPlaylistCommand_WhenClickedTrackIsPartOfTheOrderedSelection_AddsAllOfThemInClickOrder()
    {
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        LibraryIndexStore.Reconcile(settingsPath, [root.FullName]); // ensures the index db/schema exists
        var vm = CreateViewModel(out _, out _, out _, settingsPath);
        var trackA = new Track(TestPaths.Music("a.mp3"), "a.mp3");
        var trackB = new Track(TestPaths.Music("b.mp3"), "b.mp3");
        vm.Tracks.Add(new LibraryTrackViewModel(trackA, vm));
        vm.Tracks.Add(new LibraryTrackViewModel(trackB, vm));
        vm.SetSelectedTracksInOrder([trackB, trackA]);
        var playlistId = vm.Playlists.CreatePlaylist("Target");

        vm.AddToPlaylistCommand.Execute((trackA, playlistId));

        var persistedOrder = vm.Playlists.Tracks.Select(t => t.Track.FilePath).ToList();
        await Assert.That(persistedOrder).IsEquivalentTo([TestPaths.Music("b.mp3"), TestPaths.Music("a.mp3")]);
    }

    [Test]
    public async Task AddAlbumToPlaylistCommand_AppendsEveryAlbumTrackInAlbumOrder()
    {
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
        var vm = CreateViewModel(out _, out _, out _, settingsPath);
        var existing = new Track(TestPaths.Music("existing.mp3"), "existing.mp3");
        var albumTracks = new[] { new Track(TestPaths.Music("01.mp3"), "01.mp3"), new Track(TestPaths.Music("02.mp3"), "02.mp3") }
            .Select(t => new LibraryTrackViewModel(t, vm)).ToList();
        var album = new AlbumViewModel("Album", "Artist", null, albumTracks, vm);
        var playlistId = vm.Playlists.CreatePlaylist("Target");
        vm.Playlists.AddTracksToSelectedPlaylist([existing]);

        vm.AddAlbumToPlaylistCommand.Execute((album, playlistId));

        var persistedOrder = vm.Playlists.Tracks.Select(t => t.Track.FilePath).ToList();
        await Assert.That(persistedOrder).IsEquivalentTo([TestPaths.Music("existing.mp3"), TestPaths.Music("01.mp3"), TestPaths.Music("02.mp3")]);
    }

    [Test]
    public async Task PlayNextCommand_InCoverArtView_IgnoresAStaleListSelection()
    {
        var vm = CreateViewModel(out _, out _, out var playbackControls);
        var selectedTrack = new Track(TestPaths.Music("selected.mp3"), "selected.mp3");
        var clickedTrack = new Track(TestPaths.Music("clicked.mp3"), "clicked.mp3");
        vm.Tracks.Add(new LibraryTrackViewModel(selectedTrack, vm));
        vm.Tracks.Add(new LibraryTrackViewModel(clickedTrack, vm));
        vm.SetSelectedTracksInOrder([selectedTrack, clickedTrack]);
        vm.ViewMode = LibraryViewMode.AlbumGrid;

        vm.PlayNextCommand.Execute(clickedTrack);

        await Assert.That(playbackControls.QueueEntries.Count).IsEqualTo(1);
        await Assert.That(playbackControls.QueueEntries[0].Track).IsEqualTo(clickedTrack);
    }

    [Test]
    public async Task SavingTagsInTheProperties_RefreshesIndexLibraryQueueAndNowPlaying()
    {
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        var trackPath = Path.Combine(root.FullName, "tagged-track.mp3");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), trackPath);
        var indexed = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]).Tracks[0];
        var vm = CreateViewModel(out _, out _, out var playbackControls, settingsPath);
        vm.Tracks.Add(new LibraryTrackViewModel(indexed, vm));
        await playbackControls.PlayNowAsync(indexed);

        var properties = vm.CreateTrackProperties(indexed);
        properties.Title = "Edited Title";
        properties.Genre = "Jazz";
        await properties.ApplyCommand.ExecuteAsync(null);

        await Assert.That(vm.Tracks[0].Track.Title).IsEqualTo("Edited Title");
        await Assert.That(vm.DisplayedTracks[0].Track.Title).IsEqualTo("Edited Title");
        await Assert.That(vm.Tracks.Count).IsEqualTo(1);
        await Assert.That(LibraryIndexStore.LoadIndexed(settingsPath, [root.FullName])[0].Genre).IsEqualTo("Jazz");
        await Assert.That(playbackControls.QueueEntries[0].Track.Title).IsEqualTo("Edited Title");
        await Assert.That(playbackControls.CurrentTrack!.Title).IsEqualTo("Edited Title");
    }

    private (LibraryViewModel Vm, PlaybackControlsViewModel Playback, List<Track> Tracks, string SettingsPath, DirectoryInfo Root) CreatePlaylistWithTracks(int count, bool addMissingAtEnd = false, TrackStartAction startAction = TrackStartAction.InsertAndPlay)
    {
        var settingsPath = _temp.NewSettingsPath();
        new SettingsStore(settingsPath).Update(s => s with { TrackStartAction = startAction });
        var root = _temp.NewSubdirectory();
        for (var i = 1; i <= count; i++)
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), Path.Combine(root.FullName, $"track{i}.mp3"));
        }

        var indexed = LibraryIndexStore.Reconcile(settingsPath, [root.FullName]).Tracks.OrderBy(t => t.FilePath).ToList();
        var vm = CreateViewModel(out _, out _, out var playback, settingsPath);
        var playlistTracks = new List<Track>(indexed);
        if (addMissingAtEnd)
        {
            playlistTracks.Add(new Track(Path.Combine(root.FullName, "gone.mp3"), "gone.mp3"));
        }

        vm.Playlists.CreatePlaylist("Mix");
        vm.Playlists.AddTracksToSelectedPlaylist(playlistTracks);
        vm.LibrarySection = LibrarySection.Playlist;
        return (vm, playback, indexed, settingsPath, root);
    }

    private static IReadOnlyList<LibraryTrackViewModel> Shown(LibraryViewModel vm) => vm.Playlists.Tracks;

    private static string QueuedFileNames(PlaybackControlsViewModel playback) =>
        string.Join("|", playback.QueueEntries.Select(e => Path.GetFileName(e.Track.FilePath)));

    private static Track Elsewhere() => new(TestPaths.Music("something-else.mp3"), "else.mp3");

    [Test]
    public async Task StartTrack_InsertAndPlay_PutsTheTrackAfterTheCurrentOneAndPlaysIt_KeepingTheQueue()
    {
        var (vm, playback, tracks, _, _) = CreatePlaylistWithTracks(4);
        await playback.PlayNowAsync(Elsewhere());

        await vm.StartTrackAsync(vm.Playlists.Tracks[1], Shown(vm));

        await Assert.That(QueuedFileNames(playback)).IsEqualTo("something-else.mp3|track2.mp3");
        await Assert.That(playback.CurrentTrack!.FilePath).IsEqualTo(tracks[1].FilePath);
    }

    [Test]
    public async Task StartTrack_PlayFromHere_ReplacesTheQueueWithTheClickedTrackThroughTheEndOfTheShownList()
    {
        var (vm, playback, tracks, _, _) = CreatePlaylistWithTracks(4, startAction: TrackStartAction.PlayFromHere);
        await playback.PlayNowAsync(Elsewhere());

        await vm.StartTrackAsync(vm.Playlists.Tracks[1], Shown(vm));

        await Assert.That(QueuedFileNames(playback)).IsEqualTo("track2.mp3|track3.mp3|track4.mp3");
        await Assert.That(playback.QueueCurrentIndex).IsEqualTo(0);
        await Assert.That(playback.CurrentTrack!.FilePath).IsEqualTo(tracks[1].FilePath);
    }

    [Test]
    public async Task StartTrack_ReplaceQueue_LeavesOnlyTheClickedTrack()
    {
        var (vm, playback, tracks, _, _) = CreatePlaylistWithTracks(4, startAction: TrackStartAction.ReplaceQueue);
        await playback.PlayNowAsync(Elsewhere());

        await vm.StartTrackAsync(vm.Playlists.Tracks[1], Shown(vm));

        await Assert.That(playback.QueueEntries.Select(e => e.Track.FilePath)).IsEquivalentTo([tracks[1].FilePath]);
        await Assert.That(playback.CurrentTrack!.FilePath).IsEqualTo(tracks[1].FilePath);
    }

    [Test]
    public async Task StartTrack_AddToQueue_AppendsWithoutInterruptingWhatIsPlaying()
    {
        var (vm, playback, _, _, _) = CreatePlaylistWithTracks(4, startAction: TrackStartAction.AddToQueue);
        await playback.PlayNowAsync(Elsewhere());

        await vm.StartTrackAsync(vm.Playlists.Tracks[1], Shown(vm));

        await Assert.That(QueuedFileNames(playback)).IsEqualTo("something-else.mp3|track2.mp3");
        await Assert.That(playback.CurrentTrack!.FilePath).IsEqualTo(Elsewhere().FilePath);
    }

    [Test]
    public async Task StartTrack_AddToQueue_BehindWhatIsPlaying_LeavesTheAutoDjSourceAlone()
    {
        var (vm, playback, _, settingsPath, _) = CreatePlaylistWithTracks(4, startAction: TrackStartAction.AddToQueue);
        await playback.PlayNowAsync(Elsewhere());

        await vm.StartTrackAsync(vm.Playlists.Tracks[1], Shown(vm));

        await Assert.That(new SettingsStore(settingsPath).Current.AutoDjSourcePlaylistId).IsNull();
    }

    [Test]
    public async Task StartTrack_AddToQueue_WithNothingLoaded_StartsTheTrack()
    {
        var (vm, playback, tracks, _, _) = CreatePlaylistWithTracks(4, startAction: TrackStartAction.AddToQueue);

        await vm.StartTrackAsync(vm.Playlists.Tracks[1], Shown(vm));

        await Assert.That(playback.CurrentTrack!.FilePath).IsEqualTo(tracks[1].FilePath);
        await Assert.That(playback.IsPlaying).IsTrue();
    }

    [Test]
    public async Task StartTrack_PlayFromHere_WithAutoDjOn_StartsThatTrackAndAddsRandomOnesFromThePlaylist()
    {
        var (vm, playback, tracks, settingsPath, _) = CreatePlaylistWithTracks(10, startAction: TrackStartAction.PlayFromHere);
        playback.IsAutoDjEnabled = true;
        var playlistId = vm.Playlists.SelectedPlaylistId;

        await vm.StartTrackAsync(vm.Playlists.Tracks[3], Shown(vm));

        await Assert.That(playback.QueueEntries[0].Track.FilePath).IsEqualTo(tracks[3].FilePath);
        await Assert.That(playback.QueueEntries.Count).IsEqualTo(1 + AutoDj.Lookahead);
        await Assert.That(playback.QueueEntries.Skip(1).All(e => e.Source == QueueEntrySource.AutoDj)).IsTrue();
        await Assert.That(playback.QueueEntries.Select(e => e.Track.FilePath).Distinct().Count()).IsEqualTo(playback.QueueEntries.Count);
        await Assert.That(playback.QueueEntries.All(e => tracks.Any(t => t.FilePath == e.Track.FilePath))).IsTrue();
        await Assert.That(new SettingsStore(settingsPath).Current.AutoDjSourcePlaylistId).IsEqualTo(playlistId);
    }

    [Test]
    public async Task StartTrack_InAPlaylist_RemembersThePlaylistForAutoDjLater()
    {
        var (vm, playback, tracks, settingsPath, _) = CreatePlaylistWithTracks(4, startAction: TrackStartAction.PlayFromHere);
        await vm.StartTrackAsync(vm.Playlists.Tracks[1], Shown(vm));

        await Assert.That(new SettingsStore(settingsPath).Current.AutoDjSourcePlaylistId).IsEqualTo(vm.Playlists.SelectedPlaylistId);

        playback.ToggleAutoDjCommand.Execute(null); // turning it on later uses that playlist
        await Assert.That(playback.AutoDjPool!().Count).IsEqualTo(4);
    }

    [Test]
    public async Task StartTracks_ForAWholeAlbum_PlayFromHere_QueuesTheWholeAlbum()
    {
        var (vm, playback, tracks, _, _) = CreatePlaylistWithTracks(3, startAction: TrackStartAction.PlayFromHere);
        await playback.PlayNowAsync(Elsewhere());

        await vm.StartTracksAsync(tracks);

        await Assert.That(playback.QueueEntries.Select(e => e.Track.FilePath)).IsEquivalentTo(tracks.Select(t => t.FilePath));
        await Assert.That(playback.CurrentTrack!.FilePath).IsEqualTo(tracks[0].FilePath);
    }

    [Test]
    public async Task PlayTrackItem_TheMenusPlayNow_InsertsAndPlaysWhateverTheSetting()
    {
        var (vm, playback, tracks, _, _) = CreatePlaylistWithTracks(4, startAction: TrackStartAction.ReplaceQueue);
        await playback.PlayNowAsync(Elsewhere());

        await vm.PlayTrackItemCommand.ExecuteAsync(vm.Playlists.Tracks[1]);

        await Assert.That(QueuedFileNames(playback)).IsEqualTo("something-else.mp3|track2.mp3");
    }

    [Test]
    public async Task Rescan_PicksUpFilesAddedOrRemovedOnDisk_IncludingInAnOpenPlaylist()
    {
        var (vm, _, tracks, _, root) = CreatePlaylistWithTracks(3);
        vm.Folders.Paths.Add(root.FullName);
        await vm.RescanCommand.ExecuteAsync(null);
        await Assert.That(vm.Tracks.Count).IsEqualTo(3);
        await Assert.That(vm.Playlists.Tracks.Count(t => t.IsMissing)).IsEqualTo(0);

        File.Delete(tracks[0].FilePath);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), Path.Combine(root.FullName, "brand-new.mp3"));
        // Same size and time as the deleted copy would make it a rename.
        File.SetLastWriteTimeUtc(Path.Combine(root.FullName, "brand-new.mp3"), DateTime.UtcNow.AddMinutes(1));
        await vm.RescanCommand.ExecuteAsync(null);

        await Assert.That(vm.Tracks.Count).IsEqualTo(3);
        await Assert.That(vm.Tracks.Select(t => Path.GetFileName(t.Track.FilePath))).Contains("brand-new.mp3");
        await Assert.That(vm.Playlists.Tracks.Count(t => t.IsMissing)).IsEqualTo(1);
    }

    [Test]
    public async Task PlayingFromTheLibrary_MakesAutoDjDrawFromTheWholeLibrary()
    {
        var (vm, playback, tracks, settingsPath, _) = CreatePlaylistWithTracks(3);
        vm.Tracks.ReplaceAll(tracks.Select(t => new LibraryTrackViewModel(t, vm)));
        await vm.PlayTrackItemCommand.ExecuteAsync(vm.Playlists.Tracks[0]); // from the playlist first
        vm.LibrarySection = LibrarySection.Library;

        await vm.PlayTrackItemCommand.ExecuteAsync(vm.Tracks[1]);

        await Assert.That(new SettingsStore(settingsPath).Current.AutoDjSourcePlaylistId).IsNull();
        await Assert.That(playback.AutoDjPool!().Select(t => t.FilePath)).IsEquivalentTo(tracks.Select(t => t.FilePath));
    }

    [Test]
    public async Task AutoDjSource_IsRestoredFromSettings_AndADeletedPlaylistFallsBackToTheLibrary()
    {
        var (vm, _, tracks, settingsPath, _) = CreatePlaylistWithTracks(3);
        vm.Tracks.ReplaceAll(tracks.Select(t => new LibraryTrackViewModel(t, vm)));
        var playlistId = vm.Playlists.SelectedPlaylistId!;
        new SettingsStore(settingsPath).Update(s => s with { AutoDjSourcePlaylistId = playlistId });

        var restored = CreateViewModel(out _, out _, out var restoredPlayback, settingsPath);
        restored.Tracks.ReplaceAll(tracks.Select(t => new LibraryTrackViewModel(t, restored)));
        await Assert.That(restoredPlayback.AutoDjPool!().Count).IsEqualTo(3);

        restored.Playlists.DeletePlaylist(playlistId);
        await Assert.That(restoredPlayback.AutoDjPool!().Count).IsEqualTo(3); // library, not an empty pool
    }

    [Test]
    public async Task StartTrack_PlayFromHere_SkipsMissingFilesWhenBuildingTheQueue()
    {
        var (vm, playback, tracks, _, _) = CreatePlaylistWithTracks(2, addMissingAtEnd: true, startAction: TrackStartAction.PlayFromHere);
        await vm.StartTrackAsync(vm.Playlists.Tracks[0], Shown(vm));

        await Assert.That(playback.QueueEntries.Select(e => e.Track.FilePath))
            .IsEquivalentTo(tracks.Select(t => t.FilePath));
    }

    [Test]
    public async Task PlayTrackItem_OutsideAPlaylist_IsAPlainPlayNow()
    {
        var (vm, playback, tracks, _, _) = CreatePlaylistWithTracks(3);
        vm.LibrarySection = LibrarySection.Library;
        var existing = new Track(TestPaths.Music("existing.mp3"), "existing.mp3");
        await playback.PlayNowAsync(existing);

        await vm.PlayTrackItemCommand.ExecuteAsync(new LibraryTrackViewModel(tracks[2], vm));

        await Assert.That(playback.QueueEntries.Select(e => e.Track.FilePath))
            .IsEquivalentTo([existing.FilePath, tracks[2].FilePath]);
    }

    [Test]
    public async Task RemoveFromPlaylistCommand_RemovesOnlyTheClickedTrackFromTheCurrentlySelectedPlaylist()
    {
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
        var vm = CreateViewModel(out _, out _, out _, settingsPath);
        var trackA = new Track(TestPaths.Music("a.mp3"), "a.mp3");
        vm.Playlists.CreatePlaylist("Target");
        vm.Playlists.AddTracksToSelectedPlaylist([trackA]);

        vm.RemoveFromPlaylistCommand.Execute(trackA);

        await Assert.That(vm.Playlists.Tracks).IsEmpty();
    }

    private static void AddTrack(LibraryViewModel vm, string filePath, string? album, string? artist, int? trackNumber = null, string? albumArtist = null, string? title = null) =>
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(filePath, filePath) { Album = album, Artist = artist, AlbumArtist = albumArtist, TrackNumber = trackNumber, Title = title }, vm));

    [Test]
    public async Task Albums_GroupsTracksByAlbumTitle_IgnoringArtist()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Compilation", "Artist One");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Compilation", "Artist Two");

        await Assert.That(vm.Grid.Albums.Count).IsEqualTo(1);
        await Assert.That(vm.Grid.Albums[0].Tracks.Count).IsEqualTo(2);
        await Assert.That(vm.Grid.Albums[0].Artist).IsEqualTo(Strings.VariousArtists);
    }

    [Test]
    public async Task Albums_WithSameTitleButDifferentAlbumArtist_StaysAsSeparateAlbums()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Bassline", "Capital", albumArtist: "Capital");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Bassline", "K Dot", albumArtist: "K Dot & DJ Q");

        await Assert.That(vm.Grid.Albums.Count).IsEqualTo(2);
        await Assert.That(vm.Grid.Albums[0].Title).IsEqualTo("Bassline");
        await Assert.That(vm.Grid.Albums[1].Title).IsEqualTo("Bassline");
        await Assert.That(vm.Grid.Albums.Select(a => a.Tracks.Count)).IsEquivalentTo([1, 1]);
    }

    [Test]
    public async Task Albums_GroupingIgnoresCaseAndSurroundingWhitespace()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Discovery", "Daft Punk");
        AddTrack(vm, TestPaths.Music("b.mp3"), "  discovery ", "Daft Punk");

        await Assert.That(vm.Grid.Albums.Count).IsEqualTo(1);
        await Assert.That(vm.Grid.Albums[0].Tracks.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Albums_WithNoAlbumTag_FallBackToUnknownAlbumBucket()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), album: null, artist: "Some Artist");
        AddTrack(vm, TestPaths.Music("b.mp3"), album: null, artist: "Some Artist");

        await Assert.That(vm.Grid.Albums.Count).IsEqualTo(1);
        await Assert.That(vm.Grid.Albums[0].Title).IsEqualTo(Strings.UnknownAlbum);
    }

    [Test]
    public async Task Albums_OrdersTracksByTrackNumber()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("b.mp3"), "Album", "Artist", trackNumber: 2);
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album", "Artist", trackNumber: 1);

        await Assert.That(vm.Grid.Albums[0].Tracks[0].Track.FilePath).IsEqualTo(TestPaths.Music("a.mp3"));
        await Assert.That(vm.Grid.Albums[0].Tracks[1].Track.FilePath).IsEqualTo(TestPaths.Music("b.mp3"));
    }

    [Test]
    public async Task Albums_WithNoTrackNumber_FallsBackToOrderingByTitle()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Album", "Artist", title: "Zebra");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album", "Artist", title: "Apple");

        await Assert.That(vm.Grid.Albums[0].Tracks[0].Track.Title).IsEqualTo("Apple");
        await Assert.That(vm.Grid.Albums[0].Tracks[1].Track.Title).IsEqualTo("Zebra");
    }

    [Test]
    public async Task TrackRows_WithMultipleArtists_ShowsEachTracksArtistAsSuffix()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Compilation", "Artist One");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Compilation", "Artist Two");

        var rows = vm.Grid.Albums[0].TrackRows;
        await Assert.That(rows[0].ArtistSuffix).IsEqualTo("(Artist One)");
        await Assert.That(rows[1].ArtistSuffix).IsEqualTo("(Artist Two)");
    }

    [Test]
    public async Task TrackRows_WithASingleArtist_HasNoArtistSuffix()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Album", "Only Artist");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album", "Only Artist");

        await Assert.That(vm.Grid.Albums[0].TrackRows.Select(r => r.ArtistSuffix)).IsEquivalentTo(new string?[] { null, null });
    }

    [Test]
    public async Task Albums_WhereSomeTracksHaveGuests_AreByTheArtistsEveryTrackShares()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Actually", "Pet Shop Boys", trackNumber: 1);
        AddTrack(vm, TestPaths.Music("b.mp3"), "Actually", "Pet Shop Boys;Dusty Springfield", trackNumber: 2);

        await Assert.That(vm.Grid.Albums[0].Artist).IsEqualTo("Pet Shop Boys");
        // The shared artist is in the label already, so rows only name the guests.
        await Assert.That(vm.Grid.Albums[0].TrackRows.Select(r => r.ArtistSuffix))
            .IsEquivalentTo(new string?[] { null, "(Dusty Springfield)" });
    }

    [Test]
    public async Task Albums_WithSeveralSharedArtists_ListsThemAllInTheDisplayFormat()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Collab", "A;B;C", trackNumber: 1);
        AddTrack(vm, TestPaths.Music("b.mp3"), "Collab", "A;B", trackNumber: 2);

        await Assert.That(vm.Grid.Albums[0].Artist).IsEqualTo("A, B");
        await Assert.That(vm.Grid.Albums[0].TrackRows.Select(r => r.ArtistSuffix)).IsEquivalentTo(new string?[] { "(C)", null });
    }

    [Test]
    public async Task Albums_WhereEveryTrackHasTheSameSeveralArtists_ShowAllOfThemAndNoSuffix()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Duo", "TRC;Trilla");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Duo", "TRC;Trilla");

        await Assert.That(vm.Grid.Albums[0].Artist).IsEqualTo("TRC, Trilla");
        await Assert.That(vm.Grid.Albums[0].TrackRows.Select(r => r.ArtistSuffix)).IsEquivalentTo(new string?[] { null, null });
    }

    [Test]
    public async Task Albums_WhoseTracksShareNoArtist_AreStillVariousArtists_WithEachTracksArtistsAsSuffix()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Mixed", "A;B", trackNumber: 1);
        AddTrack(vm, TestPaths.Music("b.mp3"), "Mixed", "C", trackNumber: 2);

        await Assert.That(vm.Grid.Albums[0].Artist).IsEqualTo(Strings.VariousArtists);
        await Assert.That(vm.Grid.Albums[0].TrackRows.Select(r => r.ArtistSuffix)).IsEquivalentTo(new string?[] { "(A, B)", "(C)" });
    }

    [Test]
    public async Task Albums_WithASingleArtist_ShowsThatArtistNotVariousArtists()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Album", "Only Artist");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album", "Only Artist");

        await Assert.That(vm.Grid.Albums[0].Artist).IsEqualTo("Only Artist");
    }

    [Test]
    public async Task Albums_WithNoArtistTag_ShowsBlankArtist()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Album", artist: null);

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
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album A", "Artist");
        await Assert.That(vm.HasTracks).IsTrue();

        vm.IsLoadingLibrary = true;

        await Assert.That(vm.IsTrackListViewVisible).IsFalse();
        await Assert.That(vm.IsAlbumGridViewVisible).IsFalse();
    }

    [Test]
    public async Task LibrarySection_DefaultsToLibrary()
    {
        var vm = CreateViewModel(out _, out _, out _);

        await Assert.That(vm.LibrarySection).IsEqualTo(LibrarySection.Library);
    }

    [Test]
    public async Task SwitchingToRecentlyAdded_HidesLibraryViewsAndShowsRecentlyAddedViews()
    {
        var vm = CreateViewModel(out _, out _, out _);
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album", "Artist");

        vm.LibrarySection = LibrarySection.RecentlyAdded;

        await Assert.That(vm.IsTrackListViewVisible).IsFalse();
        await Assert.That(vm.IsAlbumGridViewVisible).IsFalse();
        await Assert.That(vm.IsRecentlyAddedListVisible).IsTrue();
        await Assert.That(vm.IsRecentlyAddedCoverArtVisible).IsFalse();
    }

    [Test]
    public async Task IsPlaylistViewVisible_TrueOnlyWhenPlaylistSectionIsActiveWithTracksLoaded()
    {
        var vm = CreateViewModel(out _, out _, out _);
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album", "Artist");

        await Assert.That(vm.IsPlaylistViewVisible).IsFalse();

        vm.LibrarySection = LibrarySection.Playlist;

        await Assert.That(vm.IsPlaylistViewVisible).IsTrue();
    }

    private static LibraryTrackViewModel DatedTrack(LibraryViewModel vm, string path, string album, DateTime added) =>
        new(new Track(path, path) { Album = album, DateAddedUtc = added }, vm);

    [Test]
    public async Task RecentlyAddedTracks_AreOnlyTheNewest_NotTheWholeLibrary()
    {
        var vm = CreateViewModel(out _, out _, out _);
        var now = DateTime.UtcNow;
        vm.Tracks.ReplaceAll(Enumerable.Range(0, LibraryViewModel.RecentlyAddedLimit + 20)
            .Select(i => DatedTrack(vm, TestPaths.Music($"{i:D3}.mp3"), "Album", now.AddMinutes(-i))));

        await Assert.That(vm.RecentlyAddedTracks.Count).IsEqualTo(100);
        await Assert.That(vm.RecentlyAddedTracks[0].Track.FilePath).IsEqualTo(TestPaths.Music("000.mp3"));
        await Assert.That(vm.RecentlyAddedTracks[99].Track.FilePath).IsEqualTo(TestPaths.Music("099.mp3"));
    }

    [Test]
    public async Task RecentlyAddedTracks_WithASearch_LooksOnlyWithinTheNewest()
    {
        var vm = CreateViewModel(out _, out _, out _);
        var now = DateTime.UtcNow;
        var tracks = Enumerable.Range(0, LibraryViewModel.RecentlyAddedLimit)
            .Select(i => DatedTrack(vm, TestPaths.Music($"new{i:D3}.mp3"), "Fresh", now.AddMinutes(-i)))
            .Append(DatedTrack(vm, TestPaths.Music("zxqvkj.mp3"), "Ancient", now.AddDays(-900)))
            .ToList();
        vm.Tracks.ReplaceAll(tracks);

        vm.Search.Query = "zxqvkj";
        await vm.Search.DebounceTask;

        await Assert.That(vm.RecentlyAddedTracks).IsEmpty();
        await Assert.That(vm.DisplayedTracks.Count).IsEqualTo(1); // the library view still finds it
    }

    [Test]
    public async Task RecentlyAddedGrid_ShowsWholeAlbumsThatContainANewTrack_AndNothingElse()
    {
        var vm = CreateViewModel(out _, out _, out _);
        var now = DateTime.UtcNow;
        var tracks = Enumerable.Range(1, LibraryViewModel.RecentlyAddedLimit - 1)
            .Select(i => DatedTrack(vm, TestPaths.Music($"fresh{i:D3}.mp3"), "Fresh", now.AddMinutes(-i)))
            .Append(DatedTrack(vm, TestPaths.Music("straddle-new.mp3"), "Straddle", now))
            .Append(DatedTrack(vm, TestPaths.Music("straddle-old.mp3"), "Straddle", now.AddDays(-1000)))
            .Append(DatedTrack(vm, TestPaths.Music("ancient.mp3"), "Ancient", now.AddDays(-2000)))
            .ToList();
        vm.Tracks.ReplaceAll(tracks);

        await Assert.That(vm.RecentlyAddedGrid.Albums.Select(a => a.Title)).IsEquivalentTo(["Fresh", "Straddle"]);
        await Assert.That(vm.RecentlyAddedGrid.Albums.Single(a => a.Title == "Straddle").Tracks.Count).IsEqualTo(2);
        await Assert.That(vm.Grid.Albums.Count).IsEqualTo(3);
    }

    [Test]
    public async Task RecentlyAddedTracks_OrdersByDateAddedUtcDescending()
    {
        var vm = CreateViewModel(out _, out _, out _);
        var now = DateTime.UtcNow;
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("old.mp3"), "old.mp3") { DateAddedUtc = now.AddDays(-1) }, vm));
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("new.mp3"), "new.mp3") { DateAddedUtc = now }, vm));

        await Assert.That(vm.RecentlyAddedTracks[0].Track.FilePath).IsEqualTo(TestPaths.Music("new.mp3"));
        await Assert.That(vm.RecentlyAddedTracks[1].Track.FilePath).IsEqualTo(TestPaths.Music("old.mp3"));
    }

    [Test]
    public async Task RecentlyAddedTracks_WithASearchQuery_FiltersToMatchingTracksOnly()
    {
        var vm = CreateViewModel(out _, out _, out _);
        var now = DateTime.UtcNow;
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("a.mp3"), "a.mp3") { Title = "Wonderwall", Artist = "Oasis", DateAddedUtc = now.AddDays(-2) }, vm));
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("b.mp3"), "b.mp3") { Title = "No Woman No Cry", Artist = "Bob Marley", DateAddedUtc = now.AddDays(-1) }, vm));
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("c.mp3"), "c.mp3") { Title = "Live Forever", Artist = "Oasis", DateAddedUtc = now }, vm));

        vm.Search.Query = "oasis";
        await vm.Search.DebounceTask;

        await Assert.That(vm.RecentlyAddedTracks.Count).IsEqualTo(2);
        await Assert.That(vm.RecentlyAddedTracks[0].Track.FilePath).IsEqualTo(TestPaths.Music("c.mp3"));
        await Assert.That(vm.RecentlyAddedTracks[1].Track.FilePath).IsEqualTo(TestPaths.Music("a.mp3"));
    }

    [Test]
    public async Task RecentlyAddedGrid_SortsAlbumsByTheirMostRecentTrack_AndCannotBeUserSorted()
    {
        var vm = CreateViewModel(out _, out _, out _);

        await Assert.That(vm.RecentlyAddedGrid.AllowUserSort).IsFalse();
    }

    [Test]
    public async Task ActiveViewMode_ReflectsRecentlyAddedViewModeOnlyWhenThatSectionIsActive()
    {
        var vm = CreateViewModel(out _, out _, out _);
        vm.ViewMode = LibraryViewMode.TrackList;
        vm.RecentlyAddedViewMode = LibraryViewMode.AlbumGrid;

        await Assert.That(vm.ActiveViewMode).IsEqualTo(LibraryViewMode.TrackList);

        vm.LibrarySection = LibrarySection.RecentlyAdded;

        await Assert.That(vm.ActiveViewMode).IsEqualTo(LibraryViewMode.AlbumGrid);

        vm.ActiveViewMode = LibraryViewMode.AlbumGrid;

        await Assert.That(vm.RecentlyAddedViewMode).IsEqualTo(LibraryViewMode.AlbumGrid);
        await Assert.That(vm.ViewMode).IsEqualTo(LibraryViewMode.TrackList); // untouched
    }

    [Test]
    public async Task IsLoadingLibrary_IsFalseAfterFoldersFinishLoading()
    {
        var vm = CreateViewModel(out _, out var filePickerService, out _);
        var root = _temp.NewSubdirectory();
        filePickerService.PickLibraryFolderAsync().Returns(root.FullName);

        await vm.Folders.AddFolderCommand.ExecuteAsync(null);

        await Assert.That(vm.IsLoadingLibrary).IsFalse();
    }

    [Test]
    public async Task ClearArtworkCacheCommand_DeletesCachedThumbnailFiles()
    {
        var settingsPath = _temp.NewSettingsPath();
        var cacheDirectory = Path.Combine(Path.GetDirectoryName(settingsPath)!, "artwork-cache");
        var vm = CreateViewModel(out _, out _, out _, settingsPath);
        Directory.CreateDirectory(cacheDirectory);
        File.WriteAllBytes(Path.Combine(cacheDirectory, "fake.jpg"), [1, 2, 3]);

        vm.Grid.ClearArtworkCacheCommand.Execute(null);

        await Assert.That(Directory.EnumerateFiles(cacheDirectory)).IsEmpty();
    }

    [Test]
    public async Task DisplayedTracks_WithNoSearchQuery_MirrorsTracks()
    {
        var vm = CreateViewModel(out _, out _, out _);

        AddTrack(vm, TestPaths.Music("a.mp3"), "Album A", "Artist A", title: "Song A");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album B", "Artist B", title: "Song B");

        await Assert.That(vm.DisplayedTracks.Select(t => t.Track.FilePath)).IsEquivalentTo(vm.Tracks.Select(t => t.Track.FilePath));
    }

    [Test]
    public async Task DisplayedTracks_WithASearchQuery_FiltersAndRanksByTitleArtistOrAlbum()
    {
        var vm = CreateViewModel(out _, out _, out _);
        AddTrack(vm, TestPaths.Music("a.mp3"), "Morning Glory", "Oasis", title: "Wonderwall");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Legend", "Bob Marley", title: "No Woman No Cry");
        AddTrack(vm, TestPaths.Music("c.mp3"), "Some Album", "Some Artist", title: "Unrelated Song");

        vm.Search.Query = "oasis";
        await vm.Search.DebounceTask;

        await Assert.That(vm.DisplayedTracks.Count).IsEqualTo(1);
        await Assert.That(vm.DisplayedTracks[0].Track.FilePath).IsEqualTo(TestPaths.Music("a.mp3"));
    }

    [Test]
    public async Task DisplayedTracks_UpdatesWhenTracksChangeWhileAQueryIsActive()
    {
        var vm = CreateViewModel(out _, out _, out _);
        AddTrack(vm, TestPaths.Music("a.mp3"), "Morning Glory", "Oasis", title: "Wonderwall");
        vm.Search.Query = "oasis";
        await vm.Search.DebounceTask;
        await Assert.That(vm.DisplayedTracks.Count).IsEqualTo(1);

        AddTrack(vm, TestPaths.Music("b.mp3"), "Definitely Maybe", "Oasis", title: "Live Forever");

        await Assert.That(vm.DisplayedTracks.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ClearingTheSearch_RestoresAllTracks()
    {
        var vm = CreateViewModel(out _, out _, out _);
        AddTrack(vm, TestPaths.Music("a.mp3"), "Morning Glory", "Oasis", title: "Wonderwall");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Legend", "Bob Marley", title: "No Woman No Cry");
        vm.Search.Query = "oasis";

        vm.Search.ClearCommand.Execute(null);

        await Assert.That(vm.Search.Query).IsEqualTo("");
        await Assert.That(vm.DisplayedTracks.Count).IsEqualTo(2);
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
        var track = new Track(TestPaths.Music("song.mp3"), "song.mp3");

        await vm.ShowInFileManagerCommand.ExecuteAsync(track);

        await fileManagerService.Received(1).RevealInFileManagerAsync(TestPaths.Music("song.mp3"));
    }

    [Test]
    public async Task ShowAlbumInFileManagerCommand_RevealsTheFirstTracksFile()
    {
        var vm = CreateViewModel(out _, out _, out _, out var fileManagerService);
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album", "Artist", trackNumber: 2);
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album", "Artist", trackNumber: 1);

        await vm.ShowAlbumInFileManagerCommand.ExecuteAsync(vm.Grid.Albums[0]);

        await fileManagerService.Received(1).RevealInFileManagerAsync(TestPaths.Music("a.mp3"));
    }
    private async Task<(LibraryViewModel Vm, PlaybackControlsViewModel Playback)> CreateLibraryPlaying(int count, int playingIndex)
    {
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        for (var i = 1; i <= count; i++)
        {
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), Path.Combine(root.FullName, $"track{i}.mp3"));
        }

        new SettingsStore(settingsPath).Update(s => s with { LibraryFolderPaths = [root.FullName] });
        var vm = CreateViewModel(out _, out _, out var playback, settingsPath);
        await vm.InitializeAsync();
        await playback.PlayNowAsync(vm.Tracks.Single(t => t.Track.FilePath.EndsWith($"track{playingIndex}.mp3")).Track);
        return (vm, playback);
    }

    private static List<LibraryTrackViewModel> RecordReveals(LibraryViewModel vm)
    {
        var revealed = new List<LibraryTrackViewModel>();
        vm.RevealTrackRequested += (_, item) => revealed.Add(item);
        return revealed;
    }

    [Test]
    public async Task RevealPlayingTrack_InTheLibraryList_AsksTheListToShowThePlayingTrack()
    {
        var (vm, playback) = await CreateLibraryPlaying(3, playingIndex: 2);
        var revealed = RecordReveals(vm);

        vm.RevealPlayingTrackCommand.Execute(null);

        await Assert.That(revealed.Single().Track.FilePath).IsEqualTo(playback.CurrentTrack!.FilePath);
        await Assert.That(vm.DisplayedTracks).Contains(revealed.Single());
    }

    [Test]
    public async Task RevealPlayingTrack_WhenTheSearchHidesIt_ClearsTheSearch()
    {
        var (vm, playback) = await CreateLibraryPlaying(3, playingIndex: 1);
        vm.Search.Query = "zzzz-matches-nothing";
        await vm.Search.DebounceTask;
        var revealed = RecordReveals(vm);

        vm.RevealPlayingTrackCommand.Execute(null);

        await Assert.That(vm.Search.Query).IsEqualTo("");
        await Assert.That(revealed.Single().Track.FilePath).IsEqualTo(playback.CurrentTrack!.FilePath);
    }

    [Test]
    public async Task RevealPlayingTrack_InAPlaylistThatHasIt_StaysInThePlaylist()
    {
        var (vm, playback) = await CreateLibraryPlaying(3, playingIndex: 2);
        vm.Playlists.CreatePlaylist("Mix");
        vm.Playlists.AddTracksToSelectedPlaylist([playback.CurrentTrack!]);
        vm.LibrarySection = LibrarySection.Playlist;
        var revealed = RecordReveals(vm);

        vm.RevealPlayingTrackCommand.Execute(null);

        await Assert.That(vm.LibrarySection).IsEqualTo(LibrarySection.Playlist);
        await Assert.That(vm.Playlists.Tracks).Contains(revealed.Single());
    }

    [Test]
    public async Task RevealPlayingTrack_InAPlaylistWithoutIt_SwitchesToTheLibrary()
    {
        var (vm, playback) = await CreateLibraryPlaying(3, playingIndex: 2);
        vm.Playlists.CreatePlaylist("Mix");
        vm.Playlists.AddTracksToSelectedPlaylist([vm.Tracks.First(t => t.Track != playback.CurrentTrack).Track]);
        vm.LibrarySection = LibrarySection.Playlist;
        var revealed = RecordReveals(vm);

        vm.RevealPlayingTrackCommand.Execute(null);

        await Assert.That(vm.LibrarySection).IsEqualTo(LibrarySection.Library);
        await Assert.That(revealed.Single().Track.FilePath).IsEqualTo(playback.CurrentTrack!.FilePath);
    }

    [Test]
    public async Task RevealPlayingTrack_InTheAlbumGrid_ExpandsItsAlbumAndAsksTheGridToShowTheTrack()
    {
        var (vm, playback) = await CreateLibraryPlaying(3, playingIndex: 3);
        vm.ViewMode = LibraryViewMode.AlbumGrid;
        var listReveals = RecordReveals(vm);
        AlbumTrackReveal? gridReveal = null;
        vm.Grid.RevealRequested += (_, reveal) => gridReveal = reveal;

        vm.RevealPlayingTrackCommand.Execute(null);

        await Assert.That(listReveals).IsEmpty();
        await Assert.That(gridReveal!.Track.Track.Track.FilePath).IsEqualTo(playback.CurrentTrack!.FilePath);
        await Assert.That(vm.Grid.ExpandedAlbum).IsSameReferenceAs(gridReveal.Album);
    }

    [Test]
    public async Task RevealPlayingTrack_WhenThePlayingTrackIsNotInTheLibrary_DoesNothing()
    {
        var (vm, playback) = await CreateLibraryPlaying(2, playingIndex: 1);
        await playback.PlayNowAsync(Elsewhere());
        vm.LibrarySection = LibrarySection.RecentlyAdded;
        var revealed = RecordReveals(vm);

        vm.RevealPlayingTrackCommand.Execute(null);

        await Assert.That(revealed).IsEmpty();
        await Assert.That(vm.LibrarySection).IsEqualTo(LibrarySection.RecentlyAdded);
    }
    private static int _fileWrites;

    // Written next to the target and renamed over it: the album grid may still be reading the old file's artwork.
    private static string AddFile(DirectoryInfo root, string name, string album, string title = "Song")
    {
        var path = Path.Combine(root.FullName, name);
        var staging = Path.Combine(root.FullName, $"staging-{Guid.NewGuid():N}.mp3");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3"), staging);
        TrackTagEditor.Write(staging, new TrackTagEdits(title, "Artist", "Artist", album, null, null, null, null));
        File.SetLastWriteTimeUtc(staging, DateTime.UtcNow.AddMinutes(Interlocked.Increment(ref _fileWrites)));
        File.Move(staging, path, overwrite: true);
        return path;
    }

    private async Task<(LibraryViewModel Vm, DirectoryInfo Root)> CreateScannedLibrary(params (string Name, string Album)[] files)
    {
        var settingsPath = _temp.NewSettingsPath();
        var root = _temp.NewSubdirectory();
        foreach (var (name, album) in files)
        {
            AddFile(root, name, album);
        }

        new SettingsStore(settingsPath).Update(s => s with { LibraryFolderPaths = [root.FullName] });
        var vm = CreateViewModel(out _, out _, out _, settingsPath);
        await vm.InitializeAsync();
        return (vm, root);
    }

    private static List<NotifyCollectionChangedAction> RecordActions<T>(ObservableCollection<T> collection)
    {
        var actions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => actions.Add(e.Action);
        return actions;
    }

    [Test]
    public async Task Rescan_AppliesAddedAndRemovedFilesAsItemChanges_KeepingTheOtherRows()
    {
        var (vm, root) = await CreateScannedLibrary(("a.mp3", "A"), ("b.mp3", "A"), ("c.mp3", "A"));
        var kept = vm.Tracks.Single(t => t.Track.FilePath.EndsWith("a.mp3"));
        var trackActions = RecordActions(vm.Tracks);
        var displayedActions = RecordActions(vm.DisplayedTracks);

        File.Delete(Path.Combine(root.FullName, "b.mp3"));
        AddFile(root, "d.mp3", "A");
        await vm.RescanCommand.ExecuteAsync(null);

        await Assert.That(vm.Tracks.Select(t => Path.GetFileName(t.Track.FilePath))).IsEquivalentTo(["a.mp3", "c.mp3", "d.mp3"]);
        await Assert.That(vm.Tracks[0]).IsSameReferenceAs(kept);
        await Assert.That(trackActions).IsEquivalentTo([NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add]);
        await Assert.That(displayedActions).DoesNotContain(NotifyCollectionChangedAction.Reset);
    }

    [Test]
    public async Task Rescan_UpdatesAFileWithChangedTagsInPlace()
    {
        var (vm, root) = await CreateScannedLibrary(("a.mp3", "A"), ("b.mp3", "A"));
        var row = vm.Tracks.Single(t => t.Track.FilePath.EndsWith("a.mp3"));
        var trackActions = RecordActions(vm.Tracks);
        var changedProperties = new List<string?>();
        row.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        AddFile(root, "a.mp3", "A", title: "Renamed");
        await vm.RescanCommand.ExecuteAsync(null);

        await Assert.That(vm.Tracks.Single(t => t.Track.FilePath.EndsWith("a.mp3"))).IsSameReferenceAs(row);
        await Assert.That(row.Track.Title).IsEqualTo("Renamed");
        await Assert.That(changedProperties).IsNotEmpty();
        await Assert.That(trackActions).IsEmpty();
    }

    [Test]
    public async Task Rescan_WithNothingChanged_RaisesNoCollectionChanges()
    {
        var (vm, _) = await CreateScannedLibrary(("a.mp3", "A"));
        var changed = 0;
        vm.Tracks.Changed += (_, _) => changed++;

        await vm.RescanCommand.ExecuteAsync(null);

        await Assert.That(changed).IsEqualTo(0);
    }

    [Test]
    public async Task Rescan_RebuildsOnlyTheAlbumsThatChanged_AndKeepsTheExpandedOneOpen()
    {
        var (vm, root) = await CreateScannedLibrary(("a1.mp3", "A"), ("b1.mp3", "B"));
        var albumA = vm.Grid.Albums.Single(a => a.Title == "A");
        var albumB = vm.Grid.Albums.Single(a => a.Title == "B");
        albumB.ArtworkBytes = [1, 2, 3];
        vm.Grid.ExpandAlbum(albumB);
        vm.Grid.ExpandAlbum(albumA);

        AddFile(root, "a2.mp3", "A");
        await vm.RescanCommand.ExecuteAsync(null);

        var rebuiltA = vm.Grid.Albums.Single(a => a.Title == "A");
        await Assert.That(rebuiltA).IsNotSameReferenceAs(albumA);
        await Assert.That(rebuiltA.Tracks.Count).IsEqualTo(2);
        await Assert.That(vm.Grid.Albums.Single(a => a.Title == "B")).IsSameReferenceAs(albumB);
        await Assert.That(albumB.ArtworkBytes).IsEquivalentTo(new byte[] { 1, 2, 3 });
        await Assert.That(vm.Grid.ExpandedAlbum).IsSameReferenceAs(rebuiltA);
        await Assert.That(vm.Grid.Rows.Count(r => r.ExpandedAlbum == rebuiltA)).IsEqualTo(1);
    }

    [Test]
    public async Task Rescan_KeepsTheRightClickSelectionPointingAtUpdatedTracks()
    {
        var (vm, root) = await CreateScannedLibrary(("a.mp3", "A"), ("b.mp3", "A"));
        vm.SetSelectedTracksInOrder([.. vm.Tracks.Select(t => t.Track)]);

        AddFile(root, "a.mp3", "A", title: "Renamed");
        await vm.RescanCommand.ExecuteAsync(null);

        var renamed = vm.Tracks.Single(t => t.Track.FilePath.EndsWith("a.mp3")).Track;
        await Assert.That(vm.ResolveSelection(renamed).Count).IsEqualTo(2);
    }
    [Test]
    public async Task Rescan_KeepsTheOpenPlaylistsRowsThatDidNotChange()
    {
        var (vm, _, tracks, _, root) = CreatePlaylistWithTracks(3);
        vm.Folders.Paths.Add(root.FullName);
        await vm.RescanCommand.ExecuteAsync(null);
        var keptRow = vm.Playlists.Tracks[1];
        var actions = RecordActions(vm.Playlists.Tracks);

        File.Delete(tracks[0].FilePath);
        await vm.RescanCommand.ExecuteAsync(null);

        await Assert.That(vm.Playlists.Tracks[1]).IsSameReferenceAs(keptRow);
        await Assert.That(vm.Playlists.Tracks[0].IsMissing).IsTrue();
        await Assert.That(actions).IsEquivalentTo([NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add]);
    }
    [Test]
    public async Task WatchFolders_IsAPendingSetting_SavedOnApply_AndUndoneByCancel()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateViewModel(out _, out _, out _, settingsPath);
        await Assert.That(vm.IsWatchingFolders).IsTrue();

        vm.WatchFolders = false;
        await Assert.That(vm.HasPendingChanges).IsTrue();
        ((ISettingsCategoryViewModel)vm).CancelCommand.Execute(null);
        await Assert.That(vm.WatchFolders).IsTrue();
        await Assert.That(vm.HasPendingChanges).IsFalse();

        vm.WatchFolders = false;
        ((ISettingsCategoryViewModel)vm).ApplyCommand.Execute(null);

        await Assert.That(vm.IsWatchingFolders).IsFalse();
        await Assert.That(vm.HasPendingChanges).IsFalse();
        await Assert.That(new SettingsStore(settingsPath).Current.WatchLibraryFolders).IsFalse();
    }
    private LibraryViewModel CreateWithThreeAlbums(out PlaybackControlsViewModel playback)
    {
        // An index file has to exist for playlists to be saved.
        var settingsPath = _temp.NewSettingsPath();
        LibraryIndexStore.Reconcile(settingsPath, [_temp.NewSubdirectory().FullName]);
        var vm = CreateViewModel(out _, out _, out playback, settingsPath);
        foreach (var album in new[] { "A", "B", "C" })
        {
            AddTrack(vm, TestPaths.Music($"{album}1.mp3"), album, "Artist", trackNumber: 1);
            AddTrack(vm, TestPaths.Music($"{album}2.mp3"), album, "Artist", trackNumber: 2);
        }

        return vm;
    }

    private static AlbumViewModel GridAlbum(LibraryViewModel vm, string title) => vm.Grid.Albums.Single(a => a.Title == title);

    [Test]
    public async Task AlbumMenu_OnASelectedAlbum_ActsOnEverySelectedAlbum_InClickOrder()
    {
        var vm = CreateWithThreeAlbums(out var playback);
        vm.Grid.Select(GridAlbum(vm, "C"));
        vm.Grid.ToggleSelected(GridAlbum(vm, "A"));

        await vm.AddAlbumToQueueCommand.ExecuteAsync(GridAlbum(vm, "A"));

        await Assert.That(QueuedFileNames(playback)).IsEqualTo("C1.mp3|C2.mp3|A1.mp3|A2.mp3");
    }

    [Test]
    public async Task AlbumMenu_OnAnAlbumOutsideTheSelection_ActsOnThatAlbumOnly()
    {
        var vm = CreateWithThreeAlbums(out var playback);
        vm.Grid.Select(GridAlbum(vm, "A"));
        vm.Grid.ToggleSelected(GridAlbum(vm, "C"));

        await vm.PlayAlbumNextCommand.ExecuteAsync(GridAlbum(vm, "B"));

        await Assert.That(QueuedFileNames(playback)).IsEqualTo("B1.mp3|B2.mp3");
    }

    [Test]
    public async Task AlbumMenu_PlayNowAndAddToPlaylist_TakeTheSelectionToo()
    {
        var vm = CreateWithThreeAlbums(out var playback);
        vm.Grid.Select(GridAlbum(vm, "B"));
        vm.Grid.ToggleSelected(GridAlbum(vm, "C"));
        var playlistId = vm.Playlists.CreatePlaylist("Mix");

        await vm.PlayAlbumNowCommand.ExecuteAsync(GridAlbum(vm, "C"));
        vm.AddAlbumToPlaylistCommand.Execute((GridAlbum(vm, "B"), playlistId));

        await Assert.That(QueuedFileNames(playback)).IsEqualTo("B1.mp3|B2.mp3|C1.mp3|C2.mp3");
        await Assert.That(LibraryIndexStore.GetPlaylistTracks(vm.Settings.FilePath, playlistId).Count).IsEqualTo(4);
    }
}
