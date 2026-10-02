using Microsoft.Extensions.DependencyInjection;
using SharpSelecta.Core.Updates;

namespace SharpSelecta.Integrations.Updates;

public static class UpdatesServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUpdates()
            => services.AddSingleton<IUpdateService, VelopackUpdateService>();
    }
}
