using Microsoft.Extensions.DependencyInjection;
using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.Integrations.AlbumArt;

public static class AlbumArtServiceCollectionExtensions
{
    // Keyed so the providers' client, with its MusicBrainz User-Agent, isn't handed to whoever asks for an HttpClient.
    private static readonly object HttpClientKey = new();

    extension(IServiceCollection services)
    {
        // The providers, in the order their tiles are shown. fanart.tv needs an API key the user supplies,
        // so where it's stored is up to the caller.
        public IServiceCollection AddAlbumArtProviders(Func<string?> loadFanartApiKey, Action<string> saveFanartApiKey)
            => services
                .AddKeyedSingleton(HttpClientKey, (_, _) => AlbumArtHttp.CreateClient())
                .AddSingleton(provider => new MusicBrainzClient(Http(provider)))
                .AddSingleton<IAlbumArtProvider>(provider => new AppleMusicArtProvider(Http(provider)))
                .AddSingleton<IAlbumArtProvider>(provider => new DeezerArtProvider(Http(provider)))
                .AddSingleton<IAlbumArtProvider>(provider => new CoverArtArchiveProvider(
                    Http(provider), provider.GetRequiredService<MusicBrainzClient>()))
                .AddSingleton<IAlbumArtProvider>(provider => new FanartTvArtProvider(
                    Http(provider), provider.GetRequiredService<MusicBrainzClient>(),
                    loadFanartApiKey, saveFanartApiKey));
    }

    private static HttpClient Http(IServiceProvider provider) => provider.GetRequiredKeyedService<HttpClient>(HttpClientKey);
}
