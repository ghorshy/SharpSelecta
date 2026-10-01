using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.AlbumArt;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class AlbumArtSearchViewModelTests
{
    private static readonly byte[] Jpeg16 = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "cover-red.jpg"));
    private static readonly byte[] Png16 = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "cover-blue.png"));
    private static readonly AlbumArtQuery Query = new("Daft Punk", "Random Access Memories");

    private sealed class FakeProvider(
        string name, Func<AlbumArtQuery, CancellationToken, Task<AlbumArtCandidate?>> find, bool configured = true) : IAlbumArtProvider
    {
        public string Name => name;

        public bool IsConfigured { get; private set; } = configured;

        public List<string> ConfiguredWith { get; } = [];

        public int Calls { get; private set; }

        public void Configure(string value)
        {
            ConfiguredWith.Add(value);
            IsConfigured = true;
        }

        public Task<AlbumArtCandidate?> FindAsync(AlbumArtQuery query, CancellationToken cancellationToken)
        {
            Calls++;
            return find(query, cancellationToken);
        }
    }

    private static FakeProvider Finds(string name, byte[] image, int size = 16) =>
        new(name, (_, _) => Task.FromResult<AlbumArtCandidate?>(new AlbumArtCandidate(image, size, size, "Random Access Memories")));

    private static FakeProvider FindsNothing(string name) => new(name, (_, _) => Task.FromResult<AlbumArtCandidate?>(null));

    private static FakeProvider Throws(string name) => new(name, (_, _) => throw new HttpRequestException("boom"));

    [Test]
    public async Task Search_FillsEachTileFromItsProvider_AndSelectsTheFirstCoverFound()
    {
        var vm = new AlbumArtSearchViewModel(Query, [FindsNothing("A"), Finds("B", Jpeg16, 1500), Finds("C", Png16)]);

        await vm.SearchAsync();

        await Assert.That(vm.Tiles.Select(t => t.State)).IsEquivalentTo([AlbumArtTileState.NotFound, AlbumArtTileState.Found, AlbumArtTileState.Found]);
        await Assert.That(vm.Tiles[1].SizeText).IsEqualTo("1500×1500");
        await Assert.That(vm.Tiles[1].Image).IsEquivalentTo(Jpeg16);
        await Assert.That(vm.SelectedTile).IsEqualTo(vm.Tiles[1]);
        await Assert.That(vm.Tiles[1].IsSelected).IsTrue();
        await Assert.That(vm.PreviewBytes).IsEquivalentTo(Jpeg16);
        await Assert.That(vm.OkCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task Search_ASearchThatThrows_MarksOnlyThatTileFailed()
    {
        var vm = new AlbumArtSearchViewModel(Query, [Throws("Broken"), Finds("Fine", Jpeg16)]);

        await vm.SearchAsync();

        await Assert.That(vm.Tiles[0].State).IsEqualTo(AlbumArtTileState.Failed);
        await Assert.That(vm.Tiles[1].State).IsEqualTo(AlbumArtTileState.Found);
        await Assert.That(vm.Tiles[0].StatusText).IsNotNull();
    }

    [Test]
    public async Task Search_AProviderThatStillNeedsConfiguring_IsNotCalled_AndAsksForSetup()
    {
        var needsKey = new FakeProvider("Keyed", (_, _) => Task.FromResult<AlbumArtCandidate?>(null), configured: false);
        var vm = new AlbumArtSearchViewModel(Query, [needsKey]);

        await vm.SearchAsync();

        await Assert.That(vm.Tiles[0].State).IsEqualTo(AlbumArtTileState.NeedsSetup);
        await Assert.That(vm.Tiles[0].NeedsSetup).IsTrue();
        await Assert.That(needsKey.Calls).IsEqualTo(0);
    }

    private sealed class ConfigurableFailingProvider : IAlbumArtProvider
    {
        public string Name => "Keyed";

        public bool CanBeConfigured => true;

        public bool KeyIsGood { get; private set; }

        public void Configure(string value) => KeyIsGood = value == "good";

        public Task<AlbumArtCandidate?> FindAsync(AlbumArtQuery query, CancellationToken cancellationToken) =>
            KeyIsGood
                ? Task.FromResult<AlbumArtCandidate?>(new AlbumArtCandidate(Jpeg16, 16, 16, "T"))
                : throw new HttpRequestException("401");
    }

    [Test]
    public async Task EnterKey_IsOfferedAgainAfterAConfigurableProviderFails_SoARejectedKeyCanBeReplaced()
    {
        var provider = new ConfigurableFailingProvider();
        var vm = new AlbumArtSearchViewModel(Query, [provider, Throws("Plain")]);
        await vm.SearchAsync();

        await Assert.That(vm.Tiles[0].State).IsEqualTo(AlbumArtTileState.Failed);
        await Assert.That(vm.Tiles[0].ShowEnterKey).IsTrue();
        await Assert.That(vm.Tiles[1].ShowEnterKey).IsFalse(); // nothing to configure on a plain provider

        await vm.ConfigureAsync(vm.Tiles[0], "good");

        await Assert.That(vm.Tiles[0].State).IsEqualTo(AlbumArtTileState.Found);
        await Assert.That(vm.Tiles[0].ShowEnterKey).IsFalse();
    }

    [Test]
    public async Task Configure_StoresTheKeyAndSearchesThatServiceAgain()
    {
        var keyed = new FakeProvider("Keyed", (_, _) => Task.FromResult<AlbumArtCandidate?>(new AlbumArtCandidate(Jpeg16, 16, 16, "T")), configured: false);
        var vm = new AlbumArtSearchViewModel(Query, [keyed]);
        await vm.SearchAsync();

        await vm.ConfigureAsync(vm.Tiles[0], "secret");

        await Assert.That(keyed.ConfiguredWith).IsEquivalentTo(["secret"]);
        await Assert.That(vm.Tiles[0].State).IsEqualTo(AlbumArtTileState.Found);
        await Assert.That(vm.SelectedTile).IsEqualTo(vm.Tiles[0]);
    }

    [Test]
    public async Task Search_RunsEveryProviderAtOnce()
    {
        var gate = new TaskCompletionSource();
        var started = 0;
        FakeProvider Slow(string name) => new(name, async (_, _) =>
        {
            Interlocked.Increment(ref started);
            await gate.Task;
            return null;
        });
        var vm = new AlbumArtSearchViewModel(Query, [Slow("A"), Slow("B"), Slow("C")]);

        var search = vm.SearchAsync();
        await Assert.That(started).IsEqualTo(3);
        await Assert.That(vm.Tiles.All(t => t.IsSearching)).IsTrue();

        gate.SetResult();
        await search;
    }

    [Test]
    public async Task Select_SwitchesThePreview_AndIgnoresATileWithNoCover()
    {
        var vm = new AlbumArtSearchViewModel(Query, [Finds("A", Jpeg16), Finds("B", Png16), FindsNothing("C")]);
        await vm.SearchAsync();

        vm.Select(vm.Tiles[1]);
        await Assert.That(vm.PreviewBytes).IsEquivalentTo(Png16);
        await Assert.That(vm.Tiles[0].IsSelected).IsFalse();
        await Assert.That(vm.Tiles[1].IsSelected).IsTrue();

        vm.Select(vm.Tiles[2]);
        await Assert.That(vm.SelectedTile).IsEqualTo(vm.Tiles[1]);
    }

    [Test]
    public async Task Ok_ReturnsTheSelectedCover_AndIsDisabledWithoutOne()
    {
        var vm = new AlbumArtSearchViewModel(Query, [FindsNothing("A"), Finds("B", Png16)]);
        await Assert.That(vm.OkCommand.CanExecute(null)).IsFalse();
        byte[]? closedWith = null;
        var closed = false;
        vm.CloseRequested += (_, result) => (closed, closedWith) = (true, result);
        await vm.SearchAsync();

        vm.OkCommand.Execute(null);

        await Assert.That(closed).IsTrue();
        await Assert.That(closedWith).IsEquivalentTo(Png16);
    }

    [Test]
    public async Task Cancel_ClosesWithNull()
    {
        var vm = new AlbumArtSearchViewModel(Query, [Finds("A", Jpeg16)]);
        await vm.SearchAsync();
        byte[]? closedWith = [1];
        vm.CloseRequested += (_, result) => closedWith = result;

        vm.CancelCommand.Execute(null);

        await Assert.That(closedWith).IsNull();
    }

    [Test]
    public async Task Dispose_AbandonsSearchesStillRunning_WithoutMarkingThemFailed()
    {
        var provider = new FakeProvider("Slow", async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return null;
        });
        var vm = new AlbumArtSearchViewModel(Query, [provider]);
        var search = vm.SearchAsync();

        vm.Dispose();
        await search;

        await Assert.That(vm.Tiles[0].State).IsEqualTo(AlbumArtTileState.Searching);
    }

    // --- TrackPropertiesViewModel integration ---

    private static TrackPropertiesViewModel Properties(Track track, params IAlbumArtProvider[] providers) =>
        new(track, Substitute.For<IFilePickerService>(), Substitute.For<IFileManagerService>(), NullLogger.Instance, providers);

    private static Track TrackWith(string? artist, string? album, string? albumArtist = null) =>
        new("/music/nonexistent.mp3", "nonexistent") { Artist = artist, Album = album, AlbumArtist = albumArtist };

    [Test]
    public async Task CanSearchCoverOnline_NeedsProvidersAnAlbumAndAnArtist()
    {
        var provider = FindsNothing("A");

        await Assert.That(Properties(TrackWith("Artist", "Album")).CanSearchCoverOnline).IsFalse();          // no providers
        await Assert.That(Properties(TrackWith("Artist", null), provider).CanSearchCoverOnline).IsFalse();    // no album
        await Assert.That(Properties(TrackWith(null, "Album"), provider).CanSearchCoverOnline).IsFalse();     // no artist
        await Assert.That(Properties(TrackWith("Artist", "Album"), provider).CanSearchCoverOnline).IsTrue();
    }

    [Test]
    public async Task CreateAlbumArtSearch_PrefersTheAlbumArtist_ElseTheFirstArtist()
    {
        var provider = FindsNothing("A");

        var withAlbumArtist = Properties(TrackWith("Guest;Other", "Album", "Band"), provider).CreateAlbumArtSearch();
        var withoutIt = Properties(TrackWith("Lead;Guest", "Album"), provider).CreateAlbumArtSearch();

        await Assert.That(withAlbumArtist.Query).IsEqualTo(new AlbumArtQuery("Band", "Album"));
        await Assert.That(withoutIt.Query).IsEqualTo(new AlbumArtQuery("Lead", "Album"));
    }

    [Test]
    public async Task CanSearchCoverOnline_FollowsTheFieldsAsTheyAreEdited()
    {
        var vm = Properties(TrackWith("Artist", "Album"), FindsNothing("A"));
        var changes = new List<string?>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        vm.Album = "";

        await Assert.That(vm.CanSearchCoverOnline).IsFalse();
        await Assert.That(changes).Contains(nameof(TrackPropertiesViewModel.CanSearchCoverOnline));
    }

    [Test]
    public async Task CanSearchCoverOnline_IsFalseWhenSeveralTracksDifferInAlbum()
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-album-art-tests-");
        try
        {
            string Make(string name, string album)
            {
                var path = Path.Combine(dir.FullName, name);
                File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "untagged-track.mp3"), path);
                TrackTagEditor.Write(path, new TrackTagEdits("T", "Same", null, album, null, null, null, null));
                return path;
            }

            var tracks = new[] { Make("a.mp3", "One"), Make("b.mp3", "Two") }.Select(p => MusicLibraryScanner.ReadTrackIfExists(p)!).ToList();
            var vm = new TrackPropertiesViewModel(tracks, Substitute.For<IFilePickerService>(), Substitute.For<IFileManagerService>(),
                NullLogger.Instance, [FindsNothing("A")]);

            await Assert.That(vm.CanSearchCoverOnline).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task SetCover_ShowsTheCoverAndMakesItDirty_ButRejectsANonImage()
    {
        var vm = Properties(TrackWith("Artist", "Album"));

        vm.SetCover([1, 2, 3]);
        await Assert.That(vm.HasCover).IsFalse();
        await Assert.That(vm.ErrorMessage).IsNotNull();

        vm.SetCover(Jpeg16);
        await Assert.That(vm.CoverBytes).IsEquivalentTo(Jpeg16);
        await Assert.That(vm.IsDirty).IsTrue();
        await Assert.That(vm.ErrorMessage).IsNull();
    }
}
