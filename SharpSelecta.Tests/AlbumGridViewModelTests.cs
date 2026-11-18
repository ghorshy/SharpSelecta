using SharpSelecta.App.Resources;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.Tests;

public class AlbumGridViewModelTests : IDisposable
{
    private readonly TempDirectory _temp = new();

    public void Dispose() => _temp.Dispose();

    private LibraryViewModel CreateLibraryViewModel(string? settingsFilePath = null)
    {
        var audioEngine = Substitute.For<IAudioEngine>();
        var filePickerService = Substitute.For<IFilePickerService>();
        var playbackControls = new PlaybackControlsViewModel(audioEngine, new PlaybackQueue(), new SettingsStore(_temp.NewSettingsPath()), NullLogger<PlaybackControlsViewModel>.Instance);
        return new LibraryViewModel(
            filePickerService, playbackControls, Substitute.For<IFileManagerService>(),
            new SettingsStore(settingsFilePath ?? _temp.NewSettingsPath()), TestThemeLayout.Default, NullLogger<LibraryViewModel>.Instance);
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
            AddTrack(vm, TestPaths.Music($"{i}.mp3"), $"Album {i}");
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
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album A");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album B");

        vm.Grid.SetViewportWidth(10);

        await Assert.That(vm.Grid.Rows.Count).IsEqualTo(2);
        await Assert.That(vm.Grid.Rows[0].Tiles.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ExpandAlbum_SetsExpandedAlbumOnItsOwnRowOnly()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album A");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album B");
        vm.Grid.SetViewportWidth(400);

        var albumB = vm.Grid.Albums[1];
        vm.Grid.ExpandAlbum(albumB);

        await Assert.That(vm.Grid.ExpandedAlbum).IsEqualTo(albumB);
        await Assert.That(vm.Grid.Rows[0].ExpandedAlbum).IsEqualTo(albumB);
    }

    [Test]
    public async Task Close_CollapsesTheAlbumAndDeselectsIt()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album A");
        vm.Grid.SetViewportWidth(400);
        var album = vm.Grid.Albums[0];
        vm.Grid.Select(album);
        vm.Grid.ExpandAlbum(album);

        vm.Grid.CloseCommand.Execute(album);

        await Assert.That(vm.Grid.ExpandedAlbum).IsNull();
        await Assert.That(vm.Grid.Rows[0].ExpandedAlbum).IsNull();
        await Assert.That(album.IsSelected).IsFalse();
        await Assert.That(vm.Grid.SelectedAlbums).IsEmpty();
    }

    [Test]
    public async Task ExpandAlbum_ThenExpandingADifferentAlbum_MovesExpansionWithoutRequiringCollapseFirst()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album A");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album B");
        vm.Grid.SetViewportWidth(400);

        vm.Grid.ExpandAlbum(vm.Grid.Albums[0]);
        vm.Grid.ExpandAlbum(vm.Grid.Albums[1]);

        await Assert.That(vm.Grid.ExpandedAlbum).IsEqualTo(vm.Grid.Albums[1]);
        await Assert.That(vm.Grid.Rows[0].ExpandedAlbum).IsEqualTo(vm.Grid.Albums[1]);
    }

    [Test]
    public async Task ChangingTileSize_WhenItChangesTheColumnCount_CollapsesAnyExpandedAlbum()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album A");
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album B");
        vm.Grid.SetViewportWidth(400);
        vm.Grid.ExpandAlbum(vm.Grid.Albums[0]);

        vm.Grid.AdjustTileSize(160);

        await Assert.That(vm.Grid.ExpandedAlbum).IsNull();
    }

    [Test]
    public async Task ChangingTileSize_WithoutChangingTheColumnCount_DoesNotCollapseAnyExpandedAlbum()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album A");
        vm.Grid.SetViewportWidth(2000);
        vm.Grid.ExpandAlbum(vm.Grid.Albums[0]);

        vm.Grid.AdjustTileSize(2);

        await Assert.That(vm.Grid.ExpandedAlbum).IsEqualTo(vm.Grid.Albums[0]);
    }

    [Test]
    public async Task TileSize_RoundTripsThroughSettings()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateLibraryViewModel(settingsPath);
        vm.Grid.AdjustTileSize(50);

        var restarted = CreateLibraryViewModel(settingsPath);

        await Assert.That(restarted.Grid.TileSize).IsEqualTo(vm.Grid.TileSize);
    }

    [Test]
    public async Task SortMode_DefaultsToTitle()
    {
        var vm = CreateLibraryViewModel();

        await Assert.That(vm.Grid.SortMode).IsEqualTo(AlbumSortMode.Title);
    }

    [Test]
    public async Task Tile_ForATrackWithSeveralArtists_ShowsThemWithTheDisplaySeparator()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, TestPaths.Music("a.mp3"), "Collab Album", "TRC;Trilla Jermaine Trilloski", null);
        vm.Grid.SetViewportWidth(2000);

        await Assert.That(vm.Grid.Rows[0].Tiles[0].Artist).IsEqualTo("TRC, Trilla Jermaine Trilloski");
    }

    [Test]
    public async Task SetSortMode_ToArtist_OrdersRowsByArtist()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, TestPaths.Music("a.mp3"), "Zebra Album", "Beta Artist", null);
        AddTrack(vm, TestPaths.Music("b.mp3"), "Apple Album", "Alpha Artist", null);
        vm.Grid.SetViewportWidth(2000);

        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Artist);

        await Assert.That(vm.Grid.Rows[0].Tiles[0].Artist).IsEqualTo("Alpha Artist");
        await Assert.That(vm.Grid.Rows[0].Tiles[1].Artist).IsEqualTo("Beta Artist");
    }

    [Test]
    public async Task SetSortMode_ToYear_OrdersRowsByYearWithUntaggedAlbumsLast()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album A", "Artist", 2010);
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album B", "Artist", null);
        AddTrack(vm, TestPaths.Music("c.mp3"), "Album C", "Artist", 1999);
        vm.Grid.SetViewportWidth(2000);

        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Year);

        await Assert.That(vm.Grid.Rows[0].Tiles[0].Title).IsEqualTo("Album C");
        await Assert.That(vm.Grid.Rows[0].Tiles[1].Title).IsEqualTo("Album A");
        await Assert.That(vm.Grid.Rows[0].Tiles[2].Title).IsEqualTo("Album B");
    }

    [Test]
    public async Task SetSortMode_ClickedAgainWithTheSameMode_TogglesDirectionInstead()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album A", "Artist", 2010);
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album B", "Artist", 1999);
        vm.Grid.SetViewportWidth(2000);
        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Year);
        await Assert.That(vm.Grid.SortDescending).IsFalse();

        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Year);

        await Assert.That(vm.Grid.SortMode).IsEqualTo(AlbumSortMode.Year);
        await Assert.That(vm.Grid.SortDescending).IsTrue();
        await Assert.That(vm.Grid.Rows[0].Tiles[0].Title).IsEqualTo("Album A");
        await Assert.That(vm.Grid.Rows[0].Tiles[1].Title).IsEqualTo("Album B");

        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Year);

        await Assert.That(vm.Grid.SortDescending).IsFalse();
    }

    [Test]
    public async Task SetSortMode_SwitchedToADifferentMode_ResetsDirectionToAscending()
    {
        var vm = CreateLibraryViewModel();
        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Year);
        vm.Grid.ToggleSortDirectionCommand.Execute(null);
        await Assert.That(vm.Grid.SortDescending).IsTrue();

        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Artist);

        await Assert.That(vm.Grid.SortMode).IsEqualTo(AlbumSortMode.Artist);
        await Assert.That(vm.Grid.SortDescending).IsFalse();
    }

    [Test]
    public async Task ToggleSortDirection_ReversesTheOrder()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album A", "Artist", 2010);
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album B", "Artist", 1999);
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
        AddTrack(vm, TestPaths.Music("a.mp3"), "Album A", "Artist", 2010);
        AddTrack(vm, TestPaths.Music("b.mp3"), "Album B", "Artist", null);
        vm.Grid.SetViewportWidth(2000);
        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Year);

        vm.Grid.ToggleSortDirectionCommand.Execute(null);

        await Assert.That(vm.Grid.Rows[0].Tiles[0].Title).IsEqualTo("Album A");
        await Assert.That(vm.Grid.Rows[0].Tiles[1].Title).IsEqualTo("Album B");
    }

    [Test]
    public async Task SortModeAndDirection_RoundTripThroughSettings()
    {
        var settingsPath = _temp.NewSettingsPath();
        var vm = CreateLibraryViewModel(settingsPath);
        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Year);
        vm.Grid.ToggleSortDirectionCommand.Execute(null);

        var restarted = CreateLibraryViewModel(settingsPath);

        await Assert.That(restarted.Grid.SortMode).IsEqualTo(AlbumSortMode.Year);
        await Assert.That(restarted.Grid.SortDescending).IsTrue();
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

    [Test]
    public async Task IncreaseTileSizeCommand_GrowsTheTileSizeByOneStep()
    {
        var vm = CreateLibraryViewModel();
        var initialSize = vm.Grid.TileSize;

        vm.Grid.IncreaseTileSizeCommand.Execute(null);

        await Assert.That(vm.Grid.TileSize).IsEqualTo(initialSize + vm.Grid.TileSizeStep);
    }

    [Test]
    public async Task DecreaseTileSizeCommand_ShrinksTheTileSizeByOneStep()
    {
        var vm = CreateLibraryViewModel();
        var initialSize = vm.Grid.TileSize;

        vm.Grid.DecreaseTileSizeCommand.Execute(null);

        await Assert.That(vm.Grid.TileSize).IsEqualTo(initialSize - vm.Grid.TileSizeStep);
    }

    [Test]
    public async Task SearchQuery_FiltersRowsToAlbumsMatchingByTitleArtistOrMemberTrack()
    {
        var vm = CreateLibraryViewModel();
        vm.Tracks.Add(new LibraryTrackViewModel(
            new Track(TestPaths.Music("a.mp3"), "a.mp3") { Album = "Morning Glory", Artist = "Oasis", Title = "Wonderwall" }, vm));
        vm.Tracks.Add(new LibraryTrackViewModel(
            new Track(TestPaths.Music("b.mp3"), "b.mp3") { Album = "Legend", Artist = "Bob Marley", Title = "One Love" }, vm));
        vm.Grid.SetViewportWidth(2000);

        vm.Search.Query = "wonderwall";
        await vm.Search.DebounceTask;

        await Assert.That(vm.Grid.Rows.Count).IsEqualTo(1);
        await Assert.That(vm.Grid.Rows[0].Tiles[0].Title).IsEqualTo("Morning Glory");

        vm.Search.Query = "";

        await Assert.That(vm.Grid.Rows.Sum(r => r.Tiles.Count)).IsEqualTo(2);
    }

    [Test]
    public async Task SearchQuery_RanksAlbumsByRelevanceInsteadOfTheCurrentSortMode()
    {
        // Sorted by artist, "Aardvark" would come first; the title match must win once a search is active.
        var vm = CreateLibraryViewModel();
        vm.Tracks.Add(new LibraryTrackViewModel(
            new Track(TestPaths.Music("a.mp3"), "a.mp3") { Album = "Aardvark", Artist = "Milli Vanilli", Title = "Aardvark" }, vm));
        vm.Tracks.Add(new LibraryTrackViewModel(
            new Track(TestPaths.Music("b.mp3"), "b.mp3") { Album = "Millitary Dance", Artist = "Thirtzy", Title = "Millitary Dance" }, vm));
        vm.Grid.SetViewportWidth(2000);
        vm.Grid.SetSortModeCommand.Execute(AlbumSortMode.Artist);

        vm.Search.Query = "milli";
        await vm.Search.DebounceTask;

        await Assert.That(vm.Grid.Rows.Sum(r => r.Tiles.Count)).IsEqualTo(2);
        await Assert.That(vm.Grid.Rows[0].Tiles[0].Title).IsEqualTo("Millitary Dance");
    }

    [Test]
    public async Task SortMode_DateAdded_OrdersAlbumsByTheMostRecentlyAddedTrackInEachAlbum()
    {
        var library = CreateLibraryViewModel();
        var now = DateTime.UtcNow;
        library.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("old-album", "a.mp3"), "a.mp3") { Album = "Old Album", DateAddedUtc = now.AddDays(-10) }, library));
        library.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("new-album", "b.mp3"), "b.mp3") { Album = "New Album", DateAddedUtc = now.AddDays(-1) }, library));
        library.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("old-album", "c.mp3"), "c.mp3") { Album = "Old Album", DateAddedUtc = now }, library));

        var grid = new AlbumGridViewModel(library, new SettingsStore(_temp.NewSettingsPath()), TestThemeLayout.Default, NullLogger.Instance, allowUserSort: false);
        grid.SetViewportWidth(2000);

        // "Old Album" has the most recently added track, so the whole album sorts first.
        await Assert.That(grid.Rows[0].Tiles[0].Title).IsEqualTo("Old Album");
        await Assert.That(grid.Rows[0].Tiles[1].Title).IsEqualTo("New Album");
    }

    [Test]
    public async Task Constructor_WithAllowUserSortFalse_NeverPersistsToSettings()
    {
        var settingsPath = _temp.NewSettingsPath();
        var library = CreateLibraryViewModel();

        _ = new AlbumGridViewModel(library, new SettingsStore(settingsPath), TestThemeLayout.Default, NullLogger.Instance, allowUserSort: false);

        var saved = new SettingsStore(settingsPath).Current;
        await Assert.That(saved.AlbumSortMode).IsEqualTo(AlbumSortMode.Title);
        await Assert.That(saved.AlbumSortDescending).IsFalse();
    }

    [Test]
    public async Task AllowUserSort_DefaultsToTrue_PreservingExistingBehavior()
    {
        var grid = new AlbumGridViewModel(CreateLibraryViewModel(), new SettingsStore(_temp.NewSettingsPath()), TestThemeLayout.Default, NullLogger.Instance);

        await Assert.That(grid.AllowUserSort).IsTrue();
    }
    [Test]
    public async Task RebuildIfStale_RebuildsOnlyTheMarkedAlbums_EvenWhenTheirTracksAreUnchanged()
    {
        var vm = CreateLibraryViewModel();
        AddTrack(vm, TestPaths.Music("a.mp3"), "A");
        AddTrack(vm, TestPaths.Music("b.mp3"), "B");
        var albumA = vm.Grid.Albums.Single(a => a.Title == "A");
        var albumB = vm.Grid.Albums.Single(a => a.Title == "B");

        vm.Grid.RebuildIfStale();
        await Assert.That(vm.Grid.Albums.Single(a => a.Title == "A")).IsSameReferenceAs(albumA);

        vm.Grid.MarkArtworkStale([albumA.UnderlyingTracks[0]]);
        vm.Grid.RebuildIfStale();

        await Assert.That(vm.Grid.Albums.Single(a => a.Title == "A")).IsNotSameReferenceAs(albumA);
        await Assert.That(vm.Grid.Albums.Single(a => a.Title == "B")).IsSameReferenceAs(albumB);
    }
    private LibraryViewModel CreateWithAlbums(params string[] albums)
    {
        var vm = CreateLibraryViewModel();
        foreach (var album in albums)
        {
            AddTrack(vm, TestPaths.Music($"{album}.mp3"), album);
        }

        return vm;
    }

    private static AlbumViewModel Album(LibraryViewModel vm, string title) => vm.Grid.Albums.Single(a => a.Title == title);

    private static string Selected(LibraryViewModel vm) => string.Join("|", vm.Grid.SelectedAlbums.Select(a => a.Title));

    [Test]
    public async Task Select_SelectsOnlyThatAlbum_AndMarksIt()
    {
        var vm = CreateWithAlbums("A", "B");
        vm.Grid.ToggleSelected(Album(vm, "A"));

        vm.Grid.Select(Album(vm, "B"));

        await Assert.That(Selected(vm)).IsEqualTo("B");
        await Assert.That(Album(vm, "A").IsSelected).IsFalse();
        await Assert.That(Album(vm, "B").IsSelected).IsTrue();
    }

    [Test]
    public async Task ToggleSelected_AddsAndRemoves_KeepingClickOrder()
    {
        var vm = CreateWithAlbums("A", "B", "C");

        vm.Grid.Select(Album(vm, "C"));
        vm.Grid.ToggleSelected(Album(vm, "A"));
        vm.Grid.ToggleSelected(Album(vm, "B"));
        vm.Grid.ToggleSelected(Album(vm, "C"));

        await Assert.That(Selected(vm)).IsEqualTo("A|B");
        await Assert.That(Album(vm, "C").IsSelected).IsFalse();
    }

    [Test]
    public async Task SelectRangeTo_SelectsTheShownAlbumsBetweenTheLastClickedOneAndThisOne()
    {
        var vm = CreateWithAlbums("A", "B", "C", "D");

        vm.Grid.Select(Album(vm, "D"));
        vm.Grid.SelectRangeTo(Album(vm, "B"));

        await Assert.That(Selected(vm)).IsEqualTo("B|C|D");
    }

    [Test]
    public async Task SelectRangeTo_WithNothingClickedYet_SelectsJustThatAlbum()
    {
        var vm = CreateWithAlbums("A", "B");

        vm.Grid.SelectRangeTo(Album(vm, "B"));

        await Assert.That(Selected(vm)).IsEqualTo("B");
    }

    [Test]
    public async Task ASearchThatHidesASelectedAlbum_DropsItFromTheSelection()
    {
        var vm = CreateWithAlbums("Alpha", "Beta");
        vm.Grid.Select(Album(vm, "Alpha"));
        vm.Grid.ToggleSelected(Album(vm, "Beta"));

        vm.Search.Query = "Beta";
        await vm.Search.DebounceTask;

        await Assert.That(Selected(vm)).IsEqualTo("Beta");
        await Assert.That(Album(vm, "Alpha").IsSelected).IsFalse();
    }

    [Test]
    public async Task ALibraryChange_KeepsTheSelection_OnTheRebuiltAlbumToo()
    {
        var vm = CreateWithAlbums("A", "B");
        vm.Grid.Select(Album(vm, "A"));
        vm.Grid.ToggleSelected(Album(vm, "B"));

        AddTrack(vm, TestPaths.Music("A2.mp3"), "A");

        await Assert.That(Selected(vm)).IsEqualTo("A|B");
        await Assert.That(Album(vm, "A").IsSelected).IsTrue();
        await Assert.That(Album(vm, "A").Tracks.Count).IsEqualTo(2);
    }
    [Test]
    [Arguments(347, 1)]
    [Arguments(348, 2)]
    public async Task ColumnCount_CountsEachTilesPaddingAndTheGapsBetweenThem(double viewportWidth, int columns)
    {
        // Two 160 px tiles, 6 px padding on each side, one 4 px gap: 2 * 172 + 4 = 348.
        var vm = CreateWithAlbums("A", "B", "C");

        vm.Grid.SetViewportWidth(viewportWidth);

        await Assert.That(vm.Grid.Rows[0].Tiles.Count).IsEqualTo(columns);
    }
    [Test]
    public async Task Album_Summary_ListsYearTrackCountAndTotalLength()
    {
        var vm = CreateLibraryViewModel();
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("1.mp3"), "1") { Album = "A", Year = 2024, Duration = TimeSpan.FromMinutes(3) }, vm));
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("2.mp3"), "2") { Album = "A", Duration = TimeSpan.FromSeconds(150) }, vm));

        var album = vm.Grid.Albums.Single();

        await Assert.That(album.Summary).IsEqualTo(string.Join(Strings.MetaSeparator, "2024", Strings.AlbumTrackCount(2), "5:30"));
    }

    [Test]
    public async Task Album_Summary_LeavesOutAMissingYear_AndSaysOneTrack()
    {
        var vm = CreateLibraryViewModel();
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("1.mp3"), "1") { Album = "A", Duration = TimeSpan.FromSeconds(59) }, vm));

        await Assert.That(vm.Grid.Albums.Single().Summary).IsEqualTo(string.Join(Strings.MetaSeparator, Strings.AlbumTrackCount(1), "0:59"));
    }

    [Test]
    public async Task TrackRows_WithoutATrackNumber_ShowTheirPositionInstead()
    {
        var vm = CreateLibraryViewModel();
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("a.mp3"), "a") { Album = "A", Title = "a" }, vm));
        vm.Tracks.Add(new LibraryTrackViewModel(new Track(TestPaths.Music("b.mp3"), "b") { Album = "A", Title = "b", TrackNumber = 7 }, vm));

        var rows = vm.Grid.Albums.Single().TrackRows;

        await Assert.That(rows.Select(r => r.NumberDisplay)).IsEquivalentTo(["7", "2"]);
    }

    [Test]
    public async Task ThePlayingTrack_IsMarkedInTheExpandedAlbum_AndFollowsPlayback()
    {
        var vm = CreateWithAlbums("A", "B");
        var albumA = Album(vm, "A");
        vm.Grid.SetPlayingTrack(TestPaths.Music("A.mp3"));

        vm.Grid.ExpandAlbum(albumA);
        await Assert.That(albumA.TrackRows.Single().IsPlaying).IsTrue();

        vm.Grid.SetPlayingTrack(TestPaths.Music("B.mp3"));
        await Assert.That(albumA.TrackRows.Single().IsPlaying).IsFalse();

        vm.Grid.ExpandAlbum(Album(vm, "B"));
        await Assert.That(Album(vm, "B").TrackRows.Single().IsPlaying).IsTrue();
    }
}
