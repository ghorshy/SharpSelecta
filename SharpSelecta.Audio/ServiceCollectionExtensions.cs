using Microsoft.Extensions.DependencyInjection;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Audio;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAudioEngine()
            => services.AddSingleton<IAudioEngine, OwnAudioEngine>();
    }
}
