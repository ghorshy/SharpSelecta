using Microsoft.Extensions.DependencyInjection;
using SharpSelecta.Core.Presence;

namespace SharpSelecta.Integrations.Discord;

public static class DiscordServiceCollectionExtensions
{
    // The SharpSelecta application registered in the Discord developer portal. A client id is a public
    // identifier (it appears in every status), not a secret.
    private const string ApplicationId = "1555248853324664934";

    private static readonly TimeSpan MinimumUpdateInterval = TimeSpan.FromSeconds(4);

    extension(IServiceCollection services)
    {
        public IServiceCollection AddDiscordRichPresence()
            => services.AddSingleton<IRichPresence>(_ =>
                new ThrottledRichPresence(new DiscordPresenceService(ApplicationId), MinimumUpdateInterval, TimeProvider.System));
    }
}
