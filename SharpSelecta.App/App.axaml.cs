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

            // Disposes the singleton IAudioEngine (native mixer/source cleanup, temp transcode
            // file) on a normal exit, instead of relying entirely on process teardown.
            desktop.Exit += (_, _) => provider.Dispose();

            var audioEngine = provider.GetRequiredService<IAudioEngine>();

            var librarySettingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SharpSelecta", "library-settings.json");

            var mainWindowViewModel = new MainWindowViewModel(
                audioEngine,
                provider.GetRequiredService<IFilePickerService>(),
                librarySettingsFilePath,
                provider.GetRequiredService<ILogger<PlaybackControlsViewModel>>(),
                provider.GetRequiredService<ILogger<LibraryViewModel>>(),
                provider.GetRequiredService<ILogger<QueueViewModel>>());
            mainWindow.DataContext = mainWindowViewModel;
            desktop.MainWindow = mainWindow;

            // Task.Run escapes Avalonia's SynchronizationContext: at this point the classic desktop
            // lifetime hasn't started pumping its dispatcher loop yet, so blocking the UI thread here
            // while awaiting a continuation that expects that loop to be running would deadlock.
            // audioEngine.InitializeAsync() needs that isolation since native engine startup's
            // synchronous prefix is unpredictable. Library.InitializeAsync() doesn't (it's just
            // managed file I/O internally offloaded via its own Task.Run) — calling it directly
            // keeps its continuation on the UI thread, which it needs to safely mutate the
            // Tracks collection the DataGrid is bound to.
            Task.Run(() => audioEngine.InitializeAsync());
            _ = mainWindowViewModel.Library.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}