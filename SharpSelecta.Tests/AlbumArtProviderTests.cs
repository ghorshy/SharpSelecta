using System.Net;
using System.Text;
using SharpSelecta.AlbumArt;
using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.Tests;

public class AlbumArtProviderTests
{
    private static readonly byte[] Jpeg16 = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "cover-red.jpg"));
    private static readonly byte[] Png16 = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "cover-blue.png"));
    private static readonly AlbumArtQuery Ram = new("Daft Punk", "Random Access Memories");

    // Answers by URL prefix; records every request.
    private sealed class StubHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        public List<(string Prefix, Func<HttpResponseMessage> Respond)> Routes { get; } = [];

        public StubHandler Json(string urlPrefix, string json) => Add(urlPrefix, () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });

        public StubHandler Image(string urlPrefix, byte[] bytes) => Add(urlPrefix, () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes),
        });

        public StubHandler Status(string urlPrefix, HttpStatusCode status) => Add(urlPrefix, () => new HttpResponseMessage(status));

        public StubHandler Throw(string urlPrefix, Exception exception) => Add(urlPrefix, () => throw exception);

        private StubHandler Add(string prefix, Func<HttpResponseMessage> respond)
        {
            Routes.Add((prefix, respond));
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            var url = request.RequestUri!.ToString();
            var route = Routes.FirstOrDefault(r => url.StartsWith(r.Prefix, StringComparison.Ordinal));
            return Task.FromResult(route.Respond is null ? new HttpResponseMessage(HttpStatusCode.NotFound) : route.Respond());
        }
    }

    private static HttpClient Client(StubHandler handler)
    {
        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SharpSelecta-tests/1");
        return client;
    }

    private static MusicBrainzClient FastMusicBrainz(StubHandler handler) => new(Client(handler), TimeSpan.Zero);

    private const string AppleHits = """
        {"resultCount":3,"results":[
          {"collectionName":"Random Access Memories (10th Anniversary Edition)","artistName":"Daft Punk","artworkUrl100":"https://img/anniv/100x100bb.jpg"},
          {"collectionName":"Random Access Memories","artistName":"Daft Punk","artworkUrl100":"https://img/plain/100x100bb.jpg"},
          {"collectionName":"Random Access Memories","artistName":"Some Cover Band","artworkUrl100":"https://img/cover-band/100x100bb.jpg"}]}
        """;

    // --- Apple Music ---

    [Test]
    public async Task Apple_PicksTheExactAlbumByTheRightArtist_AndAsksForTheLargestSize()
    {
        var handler = new StubHandler().Json("https://itunes.apple.com/search", AppleHits).Image("https://img/plain/3000x3000bb.jpg", Jpeg16);

        var found = await new AppleMusicArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None);

        await Assert.That(found).IsNotNull();
        await Assert.That(found!.Width).IsEqualTo(16);
        await Assert.That(found.Height).IsEqualTo(16);
        await Assert.That(found.MatchedTitle).IsEqualTo("Random Access Memories");
        await Assert.That(found.Image).IsEquivalentTo(Jpeg16);
        await Assert.That(handler.Requests.Select(r => r.RequestUri!.ToString()))
            .Contains("https://img/plain/3000x3000bb.jpg");
    }

    [Test]
    public async Task Apple_PrefersThePlainTitleOverAnEditionOfIt()
    {
        var hits = """
            {"results":[
              {"collectionName":"Random Access Memories (Deluxe)","artistName":"Daft Punk","artworkUrl100":"https://img/deluxe/100x100bb.jpg"},
              {"collectionName":"Random Access Memories","artistName":"Daft Punk","artworkUrl100":"https://img/plain/100x100bb.jpg"}]}
            """;
        var handler = new StubHandler().Json("https://itunes.apple.com/search", hits).Image("https://img/", Jpeg16);

        var found = await new AppleMusicArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None);

        await Assert.That(found!.MatchedTitle).IsEqualTo("Random Access Memories");
    }

    [Test]
    public async Task Apple_UsesAnEditionWhenThePlainAlbumIsNotListed()
    {
        var hits = """{"results":[{"collectionName":"Random Access Memories (10th Anniversary Edition)","artistName":"Daft Punk","artworkUrl100":"https://img/anniv/100x100bb.jpg"}]}""";
        var handler = new StubHandler().Json("https://itunes.apple.com/search", hits).Image("https://img/", Jpeg16);

        var found = await new AppleMusicArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None);

        await Assert.That(found!.MatchedTitle).IsEqualTo("Random Access Memories (10th Anniversary Edition)");
    }

    [Test]
    public async Task Apple_RejectsAWrongArtistOrAnUnrelatedAlbum()
    {
        var hits = """
            {"results":[
              {"collectionName":"Random Access Memories","artistName":"Some Cover Band","artworkUrl100":"https://img/a/100x100bb.jpg"},
              {"collectionName":"Discovery","artistName":"Daft Punk","artworkUrl100":"https://img/b/100x100bb.jpg"}]}
            """;
        var handler = new StubHandler().Json("https://itunes.apple.com/search", hits).Image("https://img/", Jpeg16);

        await Assert.That(await new AppleMusicArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None)).IsNull();
    }

    [Test]
    public async Task Apple_MatchesAcrossAccentsAndPunctuation()
    {
        var hits = """{"results":[{"collectionName":"I AM...SASHA FIERCE","artistName":"Beyoncé","artworkUrl100":"https://img/x/100x100bb.jpg"}]}""";
        var handler = new StubHandler().Json("https://itunes.apple.com/search", hits).Image("https://img/", Jpeg16);

        var found = await new AppleMusicArtProvider(Client(handler)).FindAsync(new AlbumArtQuery("Beyonce", "I Am Sasha Fierce"), CancellationToken.None);

        await Assert.That(found).IsNotNull();
    }

    [Test]
    public async Task Apple_WithNoResults_ReturnsNull()
    {
        var handler = new StubHandler().Json("https://itunes.apple.com/search", """{"resultCount":0,"results":[]}""");

        await Assert.That(await new AppleMusicArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None)).IsNull();
    }

    [Test]
    public async Task Apple_WhenTheServiceFails_Throws()
    {
        var handler = new StubHandler().Status("https://itunes.apple.com/search", HttpStatusCode.Forbidden);

        await Assert.That(async () => await new AppleMusicArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None))
            .Throws<HttpRequestException>();
    }

    [Test]
    public async Task Apple_WhenTheDownloadIsNotAnImage_ReturnsNull()
    {
        var handler = new StubHandler().Json("https://itunes.apple.com/search", AppleHits).Image("https://img/", Encoding.UTF8.GetBytes("<html>nope</html>"));

        await Assert.That(await new AppleMusicArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None)).IsNull();
    }

    [Test]
    public async Task Apple_FallsBackToASmallerSizeWhenTheLargestIsRefused()
    {
        var handler = new StubHandler().Json("https://itunes.apple.com/search", AppleHits)
            .Status("https://img/plain/3000x3000bb.jpg", HttpStatusCode.NotFound)
            .Image("https://img/plain/1400x1400bb.jpg", Jpeg16);

        var found = await new AppleMusicArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None);

        await Assert.That(found!.Image).IsEquivalentTo(Jpeg16);
    }

    [Test]
    public async Task Apple_ReadsPngDimensionsToo()
    {
        var handler = new StubHandler().Json("https://itunes.apple.com/search", AppleHits).Image("https://img/", Png16);

        var found = await new AppleMusicArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None);

        await Assert.That((found!.Width, found.Height)).IsEqualTo((16, 16));
    }

    // --- Deezer ---

    private const string DeezerHits = """
        {"total":2,"data":[
          {"title":"Random Access Memories","artist":{"name":"Daft Punk"},"cover_xl":"https://cdn/cover/abc/1000x1000-000000-80-0-0.jpg"}]}
        """;

    [Test]
    public async Task Deezer_TriesTheLargerSizeFirst()
    {
        // The test image is 16px, so "larger" doesn't qualify and the standard size is used - but the
        // larger URL must have been tried.
        var handler = new StubHandler().Json("https://api.deezer.com/search/album", DeezerHits).Image("https://cdn/", Jpeg16);

        var found = await new DeezerArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None);

        await Assert.That(found).IsNotNull();
        var urls = handler.Requests.Select(r => r.RequestUri!.ToString()).ToList();
        await Assert.That(urls).Contains("https://cdn/cover/abc/1400x1400-000000-80-0-0.jpg");
        await Assert.That(urls.Last()).IsEqualTo("https://cdn/cover/abc/1000x1000-000000-80-0-0.jpg");
    }

    [Test]
    public async Task Deezer_FallsBackToTheStandardSizeWhenTheLargerOneIsRefused()
    {
        var handler = new StubHandler().Json("https://api.deezer.com/search/album", DeezerHits)
            .Status("https://cdn/cover/abc/1400x1400", HttpStatusCode.Forbidden)
            .Image("https://cdn/cover/abc/1000x1000", Jpeg16);

        var found = await new DeezerArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None);

        await Assert.That(found!.Image).IsEquivalentTo(Jpeg16);
    }

    [Test]
    public async Task Deezer_FallsBackToTheStandardSizeWhenTheLargerOneTimesOut()
    {
        var handler = new StubHandler().Json("https://api.deezer.com/search/album", DeezerHits)
            .Throw("https://cdn/cover/abc/1400x1400", new TaskCanceledException("The request timed out."))
            .Image("https://cdn/cover/abc/1000x1000", Jpeg16);

        var found = await new DeezerArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None);

        await Assert.That(found!.Image).IsEquivalentTo(Jpeg16);
    }

    [Test]
    public async Task Deezer_StillStopsWhenTheSearchItselfIsCancelled()
    {
        var handler = new StubHandler().Json("https://api.deezer.com/search/album", DeezerHits)
            .Throw("https://cdn/cover/abc/1400x1400", new TaskCanceledException());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.That(async () => await new DeezerArtProvider(Client(handler)).FindAsync(Ram, cancellation.Token))
            .Throws<OperationCanceledException>();
    }

    [Test]
    public async Task Deezer_WithAnUnrelatedResult_ReturnsNull()
    {
        var hits = """{"data":[{"title":"Discovery","artist":{"name":"Daft Punk"},"cover_xl":"https://cdn/x/1000x1000.jpg"}]}""";
        var handler = new StubHandler().Json("https://api.deezer.com/search/album", hits).Image("https://cdn/", Jpeg16);

        await Assert.That(await new DeezerArtProvider(Client(handler)).FindAsync(Ram, CancellationToken.None)).IsNull();
    }

    // --- MusicBrainz / Cover Art Archive ---

    private const string MusicBrainzHits = """
        {"release-groups":[
          {"id":"aaaa-1111","title":"Random Access Memories: The Collaborators","artist-credit":[{"name":"Daft Punk"}]},
          {"id":"bbbb-2222","title":"Random Access Memories","artist-credit":[{"name":"Daft Punk"}]}]}
        """;

    [Test]
    public async Task CoverArtArchive_FindsTheReleaseGroupThenItsFrontCover()
    {
        var handler = new StubHandler()
            .Json("https://musicbrainz.org/ws/2/release-group/", MusicBrainzHits)
            .Image("https://coverartarchive.org/release-group/bbbb-2222/front-1200", Jpeg16);

        var found = await new CoverArtArchiveProvider(Client(handler), FastMusicBrainz(handler)).FindAsync(Ram, CancellationToken.None);

        await Assert.That(found).IsNotNull();
        await Assert.That(found!.MatchedTitle).IsEqualTo("Random Access Memories");
        await Assert.That(handler.Requests.All(r => r.Headers.UserAgent.ToString().Contains("SharpSelecta"))).IsTrue();
    }

    [Test]
    public async Task CoverArtArchive_WithNoCoverForTheAlbum_ReturnsNull()
    {
        var handler = new StubHandler()
            .Json("https://musicbrainz.org/ws/2/release-group/", MusicBrainzHits)
            .Status("https://coverartarchive.org/", HttpStatusCode.NotFound);

        await Assert.That(await new CoverArtArchiveProvider(Client(handler), FastMusicBrainz(handler)).FindAsync(Ram, CancellationToken.None)).IsNull();
    }

    [Test]
    public async Task CoverArtArchive_WhenMusicBrainzKnowsNoSuchAlbum_ReturnsNull()
    {
        var handler = new StubHandler().Json("https://musicbrainz.org/ws/2/release-group/", """{"release-groups":[]}""");

        await Assert.That(await new CoverArtArchiveProvider(Client(handler), FastMusicBrainz(handler)).FindAsync(Ram, CancellationToken.None)).IsNull();
    }

    [Test]
    public async Task MusicBrainz_AnswersTheSameQueryOnceForBothProviders()
    {
        var handler = new StubHandler()
            .Json("https://musicbrainz.org/ws/2/release-group/", MusicBrainzHits)
            .Image("https://coverartarchive.org/", Jpeg16)
            .Json("https://webservice.fanart.tv/", """{"albums":{"bbbb-2222":{"albumcover":[{"id":"1","url":"https://assets/a.jpg","likes":"0"}]}}}""")
            .Image("https://assets/", Jpeg16);
        var musicBrainz = FastMusicBrainz(handler);
        var archive = new CoverArtArchiveProvider(Client(handler), musicBrainz);
        var fanart = new FanartTvArtProvider(Client(handler), musicBrainz, () => "key", _ => { });

        await Task.WhenAll(archive.FindAsync(Ram, CancellationToken.None), fanart.FindAsync(Ram, CancellationToken.None));

        await Assert.That(handler.Requests.Count(r => r.RequestUri!.Host == "musicbrainz.org")).IsEqualTo(1);
    }

    [Test]
    public async Task MusicBrainz_SpacesItsRequestsApart()
    {
        var handler = new StubHandler().Json("https://musicbrainz.org/ws/2/release-group/", MusicBrainzHits);
        var musicBrainz = new MusicBrainzClient(Client(handler), TimeSpan.FromMilliseconds(300));

        var started = DateTime.UtcNow;
        await musicBrainz.FindReleaseGroupAsync(new AlbumArtQuery("A", "One"), CancellationToken.None);
        await musicBrainz.FindReleaseGroupAsync(new AlbumArtQuery("B", "Two"), CancellationToken.None);

        await Assert.That(DateTime.UtcNow - started).IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(280));
    }

    // --- fanart.tv ---

    private static FanartTvArtProvider Fanart(StubHandler handler, string? key, List<string>? saved = null) =>
        new(Client(handler), FastMusicBrainz(handler), () => key, value => saved?.Add(value));

    [Test]
    public async Task Fanart_WithoutAKey_IsNotConfigured_AndFindsNothingWithoutCallingAnything()
    {
        var handler = new StubHandler();
        var provider = Fanart(handler, null);

        await Assert.That(provider.IsConfigured).IsFalse();
        await Assert.That(await provider.FindAsync(Ram, CancellationToken.None)).IsNull();
        await Assert.That(handler.Requests).IsEmpty();
    }

    [Test]
    public async Task Fanart_CanBeConfigured_ButAPlainProviderCannot()
    {
        await Assert.That(Fanart(new StubHandler(), null).CanBeConfigured).IsTrue();
        await Assert.That(((IAlbumArtProvider)new DeezerArtProvider(Client(new StubHandler()))).CanBeConfigured).IsFalse();
    }

    [Test]
    public async Task Fanart_Configure_SavesTheTrimmedKey()
    {
        var saved = new List<string>();

        Fanart(new StubHandler(), null, saved).Configure("  abc123  ");

        await Assert.That(saved).IsEquivalentTo(["abc123"]);
    }

    [Test]
    public async Task Fanart_PicksTheMostLikedCover_OverHttps()
    {
        var handler = new StubHandler()
            .Json("https://musicbrainz.org/ws/2/release-group/", MusicBrainzHits)
            .Json("https://webservice.fanart.tv/v3/music/albums/bbbb-2222?api_key=key", """
                {"name":"Daft Punk","albums":{"bbbb-2222":{"albumcover":[
                  {"id":"1","url":"http://assets/low.jpg","likes":"1"},
                  {"id":"2","url":"http://assets/high.jpg","likes":"7"}]}}}
                """)
            .Image("https://assets/high.jpg", Jpeg16);

        var found = await Fanart(handler, "key").FindAsync(Ram, CancellationToken.None);

        await Assert.That(found).IsNotNull();
        await Assert.That(handler.Requests.Select(r => r.RequestUri!.ToString())).Contains("https://assets/high.jpg");
    }

    [Test]
    public async Task Fanart_WhenTheAlbumHasNoCovers_ReturnsNull()
    {
        var handler = new StubHandler()
            .Json("https://musicbrainz.org/ws/2/release-group/", MusicBrainzHits)
            .Json("https://webservice.fanart.tv/", """{"albums":{"bbbb-2222":{"cdart":[]}}}""");

        await Assert.That(await Fanart(handler, "key").FindAsync(Ram, CancellationToken.None)).IsNull();
    }

    [Test]
    public async Task Fanart_WithARejectedKey_Throws()
    {
        var handler = new StubHandler()
            .Json("https://musicbrainz.org/ws/2/release-group/", MusicBrainzHits)
            .Status("https://webservice.fanart.tv/", HttpStatusCode.Unauthorized);

        await Assert.That(async () => await Fanart(handler, "bad").FindAsync(Ram, CancellationToken.None)).Throws<HttpRequestException>();
    }

    [Test]
    public async Task Fanart_WhenFanartHasNothingForTheAlbum_ReturnsNull()
    {
        var handler = new StubHandler()
            .Json("https://musicbrainz.org/ws/2/release-group/", MusicBrainzHits)
            .Status("https://webservice.fanart.tv/", HttpStatusCode.NotFound);

        await Assert.That(await Fanart(handler, "key").FindAsync(Ram, CancellationToken.None)).IsNull();
    }
}
