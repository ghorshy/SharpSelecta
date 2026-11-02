using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Services;
using SharpSelecta.App.Styles;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.AlbumArt;
using SharpSelecta.Core.Conversion;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;
using SharpSelecta.Core.Updates;

namespace SharpSelecta.App;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddViewModels(SettingsStore settings, ThemeLayout layout)
            => services
                .AddSingleton(settings)
                .AddSingleton(layout)
                .AddSingleton<PlaybackQueue>()
                .AddSingleton<PlaybackControlsViewModel>()
                .AddSingleton(provider => new LibraryViewModel(
                    provider.GetRequiredService<IFilePickerService>(),
                    provider.GetRequiredService<PlaybackControlsViewModel>(),
                    provider.GetRequiredService<IFileManagerService>(),
                    settings,
                    layout,
                    provider.GetRequiredService<ILogger<LibraryViewModel>>(),
                    provider.GetServices<IAlbumArtProvider>().ToList(),
                    provider.GetService<TrackConversionService>()))
                .AddSingleton<QueueViewModel>()
                .AddSingleton<PlaybackSettingsViewModel>()
                .AddSingleton<InterfaceSettingsViewModel>()
                .AddSingleton<IntegrationsSettingsViewModel>()
                .AddSingleton(provider => new UpdatesViewModel(
                    settings,
                    provider.GetService<IUpdateService>(),
                    provider.GetRequiredService<ILogger<UpdatesViewModel>>()))
                .AddSingleton<ShortcutSettingsService>()
                .AddSingleton<EqualizerViewModel>()
                .AddSingleton<MainWindowViewModel>();
    }
}
