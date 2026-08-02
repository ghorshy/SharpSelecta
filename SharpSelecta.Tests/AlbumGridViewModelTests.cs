using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class AlbumGridViewModelTests
{
    private static string CreateTempSettingsPath() =>
        Path.Combine(Path.GetTempPath(), $"sharpselecta-album-grid-vm-settings-{Guid.NewGuid():N}.json");

    private static LibraryViewModel CreateLibraryViewModel(string? settingsFilePath = null)
    {
        var audioEngine = Substitute.For<IAudioEngine>();
        var filePickerService = Substitute.For<IFilePickerService>();
        var playbackControls = new PlaybackControlsViewModel(audioEngine, new PlaybackQueue(), NullLogger<PlaybackControlsViewModel>.Instance);
        return new LibraryViewModel(
            filePickerService, playbackControls, settingsFilePath ?? CreateTempSettingsPath(), NullLogger<LibraryViewModel>.Instance);
    }

    private static void AddTrack(LibraryViewModel vm, string filePath, string album) =>
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(filePath, filePath) { Album = album }, vm));

    private static void AddTrack(LibraryViewModel vm, string filePath, string album, string artist, int? year) =>
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(filePath, filePath) { Album = album, Artist = artist, AlbumArtist = artist, Year = year }, vm));

    [Test]
    public async Task SetViewportWidth_PartitionsAlbumsIntoRowsOfTheComputedColumnCount()
    {
        var vm = CreateLibraryViewModel();
        for (var i = 0; i < 5; i++)
        {
            AddTrack(vm, $"/music/{i}.mp3", $"Album {i}");
        }

        vm.Grid.SetViewportWidth(400);

        await Assert.That(vm.Grid.Rows.Count).IsEqualTo(3);
        await Assert.That(vm.Grid.Rows[0].Tiles.Count).IsEqualTo(2);
        await Assert.That(vm.Grid.Rows[1].Tiles.Count).IsEqualTo(2);
        await Assert.That(vm.Grid.Rows[2].Tiles.Count).IsEqualTo(1);
    }

    [Test]
    public async Task SetViewportWidth_WhenNarrowerThanOneTile_StillProducesOneColumn()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, "/music/a.mp3", "Album A");
        AddTrack(vm, "/music/b.mp3", "Album B");

        vm.Grid.SetViewportWidth(10);

        await Assert.That(vm.Grid.Rows.Count).IsEqualTo(2);
        await Assert.That(vm.Grid.Rows[0].Tiles.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ToggleExpand_SetsExpandedAlbumOnItsOwnRowOnly()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, "/music/a.mp3", "Album A");
        AddTrack(vm, "/music/b.mp3", "Album B");
        vm.Grid.SetViewportWidth(400);

        var albumB = vm.Albums[1];
        vm.Grid.ToggleExpandCommand.Execute(albumB);

        await Assert.That(vm.Grid.ExpandedAlbum).IsEqualTo(albumB);
        await Assert.That(vm.Grid.Rows[0].ExpandedAlbum).IsEqualTo(albumB);
    }

    [Test]
    public async Task ToggleExpand_CalledAgainOnTheSameAlbum_Collapses()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, "/music/a.mp3", "Album A");
        vm.Grid.SetViewportWidth(400);
        var album = vm.Albums[0];

        vm.Grid.ToggleExpandCommand.Execute(album);
        vm.Grid.ToggleExpandCommand.Execute(album);

        await Assert.That(vm.Grid.ExpandedAlbum).IsNull();
        await Assert.That(vm.Grid.Rows[0].ExpandedAlbum).IsNull();
    }

    [Test]
    public async Task ToggleExpand_ThenExpandingADifferentAlbum_MovesExpansionWithoutRequiringCollapseFirst()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, "/music/a.mp3", "Album A");
        AddTrack(vm, "/music/b.mp3", "Album B");
        vm.Grid.SetViewportWidth(400);

        vm.Grid.ToggleExpandCommand.Execute(vm.Albums[0]);
        vm.Grid.ToggleExpandCommand.Execute(vm.Albums[1]);

        await Assert.That(vm.Grid.ExpandedAlbum).IsEqualTo(vm.Albums[1]);
        await Assert.That(vm.Grid.Rows[0].ExpandedAlbum).IsEqualTo(vm.Albums[1]);
    }

    [Test]
    public async Task ChangingTileSize_WhenItChangesTheColumnCount_CollapsesAnyExpandedAlbum()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, "/music/a.mp3", "Album A");
        AddTrack(vm, "/music/b.mp3", "Album B");
        vm.Grid.SetViewportWidth(400);
        vm.Grid.ToggleExpandCommand.Execute(vm.Albums[0]);

        vm.Grid.AdjustTileSize(160);

        await Assert.That(vm.Grid.ExpandedAlbum).IsNull();
    }

    [Test]
    public async Task ChangingTileSize_WithoutChangingTheColumnCount_DoesNotCollapseAnyExpandedAlbum()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, "/music/a.mp3", "Album A");
        vm.Grid.SetViewportWidth(2000);
        vm.Grid.ToggleExpandCommand.Execute(vm.Albums[0]);

        vm.Grid.AdjustTileSize(2);

        await Assert.That(vm.Grid.ExpandedAlbum).IsEqualTo(vm.Albums[0]);
    }

    [Test]
    public async Task TileSize_RoundTripsThroughSettings()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = CreateLibraryViewModel(settingsPath);
            vm.Grid.AdjustTileSize(50);

            var restarted = CreateLibraryViewModel(settingsPath);

            await Assert.That(restarted.Grid.TileSize).IsEqualTo(vm.Grid.TileSize);
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task SortMode_DefaultsToTitle()
    {
        var vm = CreateLibraryViewModel();

        await Assert.That(vm.Grid.SortMode).IsEqualTo(AlbumSortMode.Title);
    }

    [Test]
    public async Task SetSortMode_ToArtist_OrdersRowsByArtist()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, "/music/a.mp3", "Zebra Album", "Beta Artist", null);
        AddTrack(vm, "/music/b.mp3", "Apple Album", "Alpha Artist", null);
        vm.Grid.SetViewportWidth(2000);

        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Artist);

        await Assert.That(vm.Grid.Rows[0].Tiles[0].Artist).IsEqualTo("Alpha Artist");
        await Assert.That(vm.Grid.Rows[0].Tiles[1].Artist).IsEqualTo("Beta Artist");
    }

    [Test]
    public async Task SetSortMode_ToYear_OrdersRowsByYearWithUntaggedAlbumsLast()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, "/music/a.mp3", "Album A", "Artist", 2010);
        AddTrack(vm, "/music/b.mp3", "Album B", "Artist", null);
        AddTrack(vm, "/music/c.mp3", "Album C", "Artist", 1999);
        vm.Grid.SetViewportWidth(2000);

        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Year);

        await Assert.That(vm.Grid.Rows[0].Tiles[0].Title).IsEqualTo("Album C");
        await Assert.That(vm.Grid.Rows[0].Tiles[1].Title).IsEqualTo("Album A");
        await Assert.That(vm.Grid.Rows[0].Tiles[2].Title).IsEqualTo("Album B");
    }

    [Test]
    public async Task ToggleSortDirection_ReversesTheOrder()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, "/music/a.mp3", "Album A", "Artist", 2010);
        AddTrack(vm, "/music/b.mp3", "Album B", "Artist", 1999);
        vm.Grid.SetViewportWidth(2000);
        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Year);

        vm.Grid.ToggleSortDirectionCommand.Execute(null);

        await Assert.That(vm.Grid.Rows[0].Tiles[0].Title).IsEqualTo("Album A");
        await Assert.That(vm.Grid.Rows[0].Tiles[1].Title).IsEqualTo("Album B");
    }

    [Test]
    public async Task ToggleSortDirection_WithYearSort_StillSortsUntaggedAlbumsLast()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, "/music/a.mp3", "Album A", "Artist", 2010);
        AddTrack(vm, "/music/b.mp3", "Album B", "Artist", null);
        vm.Grid.SetViewportWidth(2000);
        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Year);

        vm.Grid.ToggleSortDirectionCommand.Execute(null);

        await Assert.That(vm.Grid.Rows[0].Tiles[0].Title).IsEqualTo("Album A");
        await Assert.That(vm.Grid.Rows[0].Tiles[1].Title).IsEqualTo("Album B");
    }

    [Test]
    public async Task SortModeAndDirection_RoundTripThroughSettings()
    {
        var settingsPath = CreateTempSettingsPath();
        try
        {
            var vm = CreateLibraryViewModel(settingsPath);
            vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Year);
            vm.Grid.ToggleSortDirectionCommand.Execute(null);

            var restarted = CreateLibraryViewModel(settingsPath);

            await Assert.That(restarted.Grid.SortMode).IsEqualTo(AlbumSortMode.Year);
            await Assert.That(restarted.Grid.SortDescending).IsTrue();
        }
        finally
        {
            File.Delete(settingsPath);
        }
    }

    [Test]
    public async Task AdjustTileSize_ClampsToTheAllowedRange()
    {
        var vm = CreateLibraryViewModel();

        vm.Grid.AdjustTileSize(-1000);
        var min = vm.Grid.TileSize;
        vm.Grid.AdjustTileSize(1000);
        var max = vm.Grid.TileSize;

        await Assert.That(min).IsGreaterThanOrEqualTo(144);
        await Assert.That(max).IsLessThanOrEqualTo(320);
    }
}
