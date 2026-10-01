using Microsoft.Extensions.DependencyInjection;
using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.Integrations.AlbumArt;

public static class AlbumArtServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        // The providers, in the order their tiles are shown. fanart.tv needs an API key the user supplies,
        // so where it's stored is up to the caller.
        public IServiceCollection AddAlbumArtProviders(Func<string?> loadFanartApiKey, Action<string> saveFanartApiKey)
            => services
                .AddSingleton(_ => AlbumArtHttp.CreateClient())
                .AddSingleton(provider => new MusicBrainzClient(provider.GetRequiredService<HttpClient>()))
                .AddSingleton<IAlbumArtProvider>(provider => new AppleMusicArtProvider(provider.GetRequiredService<HttpClient>()))
                .AddSingleton<IAlbumArtProvider>(provider => new DeezerArtProvider(provider.GetRequiredService<HttpClient>()))
                .AddSingleton<IAlbumArtProvider>(provider => new CoverArtArchiveProvider(
                    provider.GetRequiredService<HttpClient>(), provider.GetRequiredService<MusicBrainzClient>()))
                .AddSingleton<IAlbumArtProvider>(provider => new FanartTvArtProvider(
                    provider.GetRequiredService<HttpClient>(), provider.GetRequiredService<MusicBrainzClient>(),
                    loadFanartApiKey, saveFanartApiKey));
    }
}
