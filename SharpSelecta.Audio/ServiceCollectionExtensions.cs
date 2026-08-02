using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.Audio;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAudioEngine()
            => services
                .AddSingleton<IAudioEngine, OwnAudioEngine>()
                .AddSingleton<IOutputDeviceService>(provider =>
                    OperatingSystem.IsLinux() && PipeWireOutputDeviceService.IsAvailable()
                        ? new PipeWireOutputDeviceService(provider.GetRequiredService<ILogger<PipeWireOutputDeviceService>>())
                        : new EngineOutputDeviceService(provider.GetRequiredService<IAudioEngine>()));
    }
}
