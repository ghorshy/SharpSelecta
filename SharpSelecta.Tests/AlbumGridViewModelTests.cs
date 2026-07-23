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

    [Test]
    public async Task SetViewportWidth_PartitionsAlbumsIntoRowsOfTheComputedColumnCount()
    {
        var vm = CreateLibraryViewModel();
        for (var i = 0; i < 5; i++)
        {
            AddTrack(vm, $"/music/{i}.mp3", $"Album {i}");
        }

        // TileSize defaults to 160, RowSpacing is 8 internally: (400+8)/(160+8) = 2.4 -> 2 columns.
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
        vm.Grid.ToggleExpand(albumB);

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

        vm.Grid.ToggleExpand(album);
        vm.Grid.ToggleExpand(album);

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

        vm.Grid.ToggleExpand(vm.Albums[0]);
        vm.Grid.ToggleExpand(vm.Albums[1]);

        await Assert.That(vm.Grid.ExpandedAlbum).IsEqualTo(vm.Albums[1]);
        await Assert.That(vm.Grid.Rows[0].ExpandedAlbum).IsEqualTo(vm.Albums[1]);
    }

    [Test]
    public async Task ChangingTileSize_CollapsesAnyExpandedAlbum()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, "/music/a.mp3", "Album A");
        vm.Grid.SetViewportWidth(400);
        vm.Grid.ToggleExpand(vm.Albums[0]);

        vm.Grid.AdjustTileSize(20);

        await Assert.That(vm.Grid.ExpandedAlbum).IsNull();
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
    public async Task AdjustTileSize_ClampsToTheAllowedRange()
    {
        var vm = CreateLibraryViewModel();

        vm.Grid.AdjustTileSize(-1000);
        var min = vm.Grid.TileSize;
        vm.Grid.AdjustTileSize(1000);
        var max = vm.Grid.TileSize;

        await Assert.That(min).IsGreaterThanOrEqualTo(80);
        await Assert.That(max).IsLessThanOrEqualTo(320);
    }
}
