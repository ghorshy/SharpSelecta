using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using SharpSelecta.App.Services;
using SharpSelecta.App.Services.Mpris;
using SharpSelecta.App.ViewModels;
using SharpSelecta.App.Views;
using SharpSelecta.Audio;
using SharpSelecta.Core.Audio;

namespace SharpSelecta.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddSerilog(dispose: false));
            services.AddAudioEngine();
            services.AddSingleton<IFilePickerService>(new AvaloniaFilePickerService(mainWindow));
            var provider = services.BuildServiceProvider();

            var audioEngine = provider.GetRequiredService<IAudioEngine>();

            var settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SharpSelecta", "library-settings.json");

            var mainWindowViewModel = new MainWindowViewModel(
                audioEngine,
                provider.GetRequiredService<IOutputDeviceService>(),
                provider.GetRequiredService<IFilePickerService>(),
                settingsFilePath,
                provider.GetRequiredService<ILogger<PlaybackControlsViewModel>>(),
                provider.GetRequiredService<ILogger<LibraryViewModel>>(),
                provider.GetRequiredService<ILogger<QueueViewModel>>());
            mainWindow.DataContext = mainWindowViewModel;
            desktop.MainWindow = mainWindow;

            var mprisServiceTask = MprisService.TryStartAsync(
                mainWindowViewModel.PlaybackControls, provider.GetRequiredService<ILogger<MprisService>>());

            MprisService? Mpris() =>
                mprisServiceTask is { IsCompletedSuccessfully: true } ? mprisServiceTask.Result : null;

            mainWindow.Activated += (_, _) => Mpris()?.NudgePriority();

            desktop.Exit += (_, _) =>
            {
                mainWindowViewModel.PersistQueueStateIfEnabled();
                mainWindowViewModel.PersistVolume();

                if (Mpris() is { } mprisService)
                {
                    _ = mprisService.DisposeAsync();
                }

                provider.Dispose();
            };

            _ = InitializeAudioEngineAndRestoreStateAsync(audioEngine, mainWindowViewModel);
            _ = mainWindowViewModel.Library.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static async Task InitializeAudioEngineAndRestoreStateAsync(IAudioEngine audioEngine, MainWindowViewModel mainWindowViewModel)
    {
        await Task.Run(() => audioEngine.InitializeAsync());
        await mainWindowViewModel.PlaybackSettings.ApplyPersistedOutputDeviceAsync();
        await mainWindowViewModel.RestoreQueueIfEnabledAsync();
    }
}