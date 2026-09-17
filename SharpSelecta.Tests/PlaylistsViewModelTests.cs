using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class PlaylistsViewModelTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-playlists-vm-tests-{Guid.NewGuid():N}.json");

    private static readonly string TaggedTrackFixturePath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "tagged-track.mp3");

    private static PlaylistsViewModel CreateViewModel(string settingsFilePath, out LibraryViewModel library)
    {
        var playbackControls = new PlaybackControlsViewModel(
            Substitute.For<IAudioEngine>(), new PlaybackQueue(), settingsFilePath, NullLogger<PlaybackControlsViewModel>.Instance);
        library = new LibraryViewModel(
            Substitute.For<IFilePickerService>(), playbackControls, Substitute.For<IFileManagerService>(),
            settingsFilePath, NullLogger<LibraryViewModel>.Instance);
        return library.Playlists;
    }

    [Test]
    public async Task CreatePlaylist_AddsItToPlaylistsAndSelectsIt()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-playlists-vm-tests-");
        try
        {
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]); // ensures the index db/schema exists
            var vm = CreateViewModel(settingsPath, out _);

            var id = vm.CreatePlaylist("Chill");

            await Assert.That(vm.Playlists.Select(p => p.Name)).IsEquivalentTo(["Chill"]);
            await Assert.That(vm.SelectedPlaylistId).IsEqualTo(id);
        }
        finally
        {
            File.Delete(settingsPath);
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task SelectPlaylist_PopulatesTracksInOrder()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-playlists-vm-tests-");
        try
        {
            File.Copy(TaggedTrackFixturePath, Path.Combine(root.FullName, "a.mp3"));
            File.Copy(TaggedTrackFixturePath, Path.Combine(root.FullName, "b.mp3"));
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var aPath = Path.Combine(root.FullName, "a.mp3");
            var bPath = Path.Combine(root.FullName, "b.mp3");
            var vm = CreateViewModel(settingsPath, out _);
            var id = vm.CreatePlaylist("Order");
            vm.ReorderSelectedPlaylist([bPath, aPath]);

            vm.SelectPlaylist(null);
            vm.SelectPlaylist(id);

            await Assert.That(vm.Tracks.Select(t => t.Track.FilePath)).IsEquivalentTo([bPath, aPath]);
        }
        finally
        {
            File.Delete(settingsPath);
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task AddTracksToSelectedPlaylist_AppendsAndPersists()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-playlists-vm-tests-");
        try
        {
            File.Copy(TaggedTrackFixturePath, Path.Combine(root.FullName, "a.mp3"));
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var aPath = Path.Combine(root.FullName, "a.mp3");
            var vm = CreateViewModel(settingsPath, out _);
            var id = vm.CreatePlaylist("Adds");

            vm.AddTracksToSelectedPlaylist([new Track(aPath, "a.mp3")]);

            await Assert.That(vm.Tracks.Select(t => t.Track.FilePath)).IsEquivalentTo([aPath]);
            var persisted = LibraryIndexStore.GetPlaylistTracks(settingsPath, id);
            await Assert.That(persisted.Select(e => e.FilePath)).IsEquivalentTo([aPath]);
        }
        finally
        {
            File.Delete(settingsPath);
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task RemoveFromSelectedPlaylist_RemovesOnlyOneMatchingEntry()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-playlists-vm-tests-");
        try
        {
            File.Copy(TaggedTrackFixturePath, Path.Combine(root.FullName, "a.mp3"));
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var aPath = Path.Combine(root.FullName, "a.mp3");
            var vm = CreateViewModel(settingsPath, out _);
            vm.CreatePlaylist("Dupes");
            var track = new Track(aPath, "a.mp3");
            vm.AddTracksToSelectedPlaylist([track, track]);

            vm.RemoveFromSelectedPlaylist(track);

            await Assert.That(vm.Tracks.Count).IsEqualTo(1);
        }
        finally
        {
            File.Delete(settingsPath);
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task DeletePlaylist_WhenItWasSelected_ClearsSelectionAndTracks()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-playlists-vm-tests-");
        try
        {
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var vm = CreateViewModel(settingsPath, out _);
            var id = vm.CreatePlaylist("Goner");

            vm.DeletePlaylist(id);

            await Assert.That(vm.Playlists).IsEmpty();
            await Assert.That(vm.SelectedPlaylistId).IsNull();
            await Assert.That(vm.Tracks).IsEmpty();
        }
        finally
        {
            File.Delete(settingsPath);
            root.Delete(recursive: true);
        }
    }

    [Test]
    public async Task SelectPlaylist_WhenAnEntrysFileIsMissing_MarksThatRowAsMissing()
    {
        var settingsPath = CreateTempSettingsPath();
        var root = Directory.CreateTempSubdirectory("sharpselecta-playlists-vm-tests-");
        try
        {
            File.Copy(TaggedTrackFixturePath, Path.Combine(root.FullName, "a.mp3"));
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);
            var aPath = Path.Combine(root.FullName, "a.mp3");
            var vm = CreateViewModel(settingsPath, out _);
            var id = vm.CreatePlaylist("Will Break");
            vm.ReorderSelectedPlaylist([aPath]);
            File.Delete(aPath);
            LibraryIndexStore.Reconcile(settingsPath, [root.FullName]);

            vm.SelectPlaylist(null);
            vm.SelectPlaylist(id);

            await Assert.That(vm.Tracks.Count).IsEqualTo(1);
            await Assert.That(vm.Tracks[0].IsMissing).IsTrue();
            await Assert.That(vm.Tracks[0].Track.FilePath).IsEqualTo(aPath);
        }
        finally
        {
            File.Delete(settingsPath);
            root.Delete(recursive: true);
        }
    }
}
