using Microsoft.Extensions.DependencyInjection;
using SharpSelecta.Core.Conversion;

namespace SharpSelecta.Integrations.Conversion;

public static class ConversionServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddAudioConverter()
            => services.AddSingleton<IAudioConverter, FfmpegAudioConverter>();
    }
}
