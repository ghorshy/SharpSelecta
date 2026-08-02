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

            // Linux-only (MprisService.TryStartAsync no-ops elsewhere) - lets playerctl and
            // Hyprland/GNOME/KDE's global media-key bindings control playback. Not awaited here
            // (D-Bus connection setup shouldn't hold up showing the window); the Exit handler below
            // only disposes it if it actually finished starting in time.
            var mprisServiceTask = MprisService.TryStartAsync(
                mainWindowViewModel.PlaybackControls, provider.GetRequiredService<ILogger<MprisService>>());

            // Focusing the window claims media-key priority (playerctld ranks players by most
            // recent PropertiesChanged activity) - without this, only play/pause/track changes
            // bump SharpSelecta above e.g. a browser tab that also registered an MPRIS player.
            mainWindow.Activated += (_, _) =>
            {
                if (mprisServiceTask.IsCompletedSuccessfully && mprisServiceTask.Result is { } mpris)
                {
                    mpris.NudgePriority();
                }
            };

            // Disposes the singleton IAudioEngine (native mixer/source cleanup) on a normal exit,
            // instead of relying entirely on process teardown — but only after the current queue
            // state has been saved off (needs the engine's cached playback position, still alive).
            desktop.Exit += (_, _) =>
            {
                mainWindowViewModel.PersistQueueStateIfEnabled();

                // Not awaited (Exit isn't async-friendly) - a best-effort release of the D-Bus name;
                // if the process exits before this completes, the bus daemon reclaims it anyway once
                // the connection closes.
                if (mprisServiceTask.IsCompletedSuccessfully && mprisServiceTask.Result is { } mprisService)
                {
                    _ = mprisService.DisposeAsync();
                }

                provider.Dispose();
            };

            // Task.Run escapes Avalonia's SynchronizationContext: at this point the classic desktop
            // lifetime hasn't started pumping its dispatcher loop yet, so blocking the UI thread here
            // while awaiting a continuation that expects that loop to be running would deadlock.
            // audioEngine.InitializeAsync() needs that isolation since native engine startup's
            // synchronous prefix is unpredictable. Library.InitializeAsync() doesn't (it's just
            // managed file I/O internally offloaded via its own Task.Run) — calling it directly
            // keeps its continuation on the UI thread, which it needs to safely mutate the
            // Tracks collection the DataGrid is bound to.
            _ = InitializeAudioEngineAndRestoreStateAsync(audioEngine, mainWindowViewModel);
            _ = mainWindowViewModel.Library.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Everything chained after the engine init call — device selection, then queue restore — needs
    // IAudioEngine to be fully initialized first (Load()/GetOutputDevices()/SetOutputDevice() all
    // throw or no-op until then), hence sequencing rather than running in parallel with it. The
    // awaits themselves don't need Task.Run's isolation the way the engine init call does: awaiting
    // (rather than blocking on) a continuation is safe even before the dispatcher loop has started.
    private static async Task InitializeAudioEngineAndRestoreStateAsync(IAudioEngine audioEngine, MainWindowViewModel mainWindowViewModel)
    {
        await Task.Run(() => audioEngine.InitializeAsync());
        await mainWindowViewModel.PlaybackSettings.ApplyPersistedOutputDeviceAsync();
        await mainWindowViewModel.RestoreQueueIfEnabledAsync();
    }
}