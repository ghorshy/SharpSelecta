using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using SharpSelecta.Integrations.AlbumArt;
using SharpSelecta.Integrations.Conversion;
using SharpSelecta.Integrations.Discord;
using SharpSelecta.Integrations.Updates;
using SharpSelecta.App.Services;
using SharpSelecta.App.Styles;
using SharpSelecta.App.Services.Mpris;
using SharpSelecta.App.Services.SingleInstance;
using SharpSelecta.App.ViewModels;
using SharpSelecta.App.Views;
using SharpSelecta.Audio;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Conversion;
using SharpSelecta.Core.Presence;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App;

public partial class App : Application
{
    public ActivationInbox? Activations { get; init; }

    public IReadOnlyList<string> OpenedPaths { get; init; } = [];

    private static readonly TimeSpan OpenedFilesBatchWindow = TimeSpan.FromMilliseconds(400);

    private Task _openingFiles = Task.CompletedTask;
    private volatile bool _filesRequested;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = new SettingsStore(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SharpSelecta", "library-settings.json"));

            ThemeService.Apply(
                settings.Current.Theme,
                settings.Current.CustomThemeFileName,
                ThemeService.GetThemesDirectory(settings.FilePath));

            var mainWindow = new MainWindow();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddSerilog(dispose: false));
            services.AddAudioEngine();
            services.AddDiscordRichPresence();
            services.AddAudioConverter();
            services.AddUpdates();
            services.AddSingleton<ITrashService, FileTrashService>();
            services.AddSingleton<TrackConversionService>();
            services.AddAlbumArtProviders(
                () => settings.Current.FanartApiKey,
                key => settings.Update(s => s with { FanartApiKey = key }));
            services.AddSingleton<IFilePickerService>(new AvaloniaFilePickerService(mainWindow));
            services.AddSingleton<IFileManagerShell, SystemFileManagerShell>();
            services.AddSingleton<IFileManagerService, FileManagerService>();
            services.AddViewModels(settings, ThemeLayout.From(this));
            var provider = services.BuildServiceProvider();

            var audioEngine = provider.GetRequiredService<IAudioEngine>();
            var mainWindowViewModel = provider.GetRequiredService<MainWindowViewModel>();
            mainWindow.DataContext = mainWindowViewModel;
            desktop.MainWindow = mainWindow;

            var mprisServiceTask = MprisService.TryStartAsync(
                mainWindowViewModel.PlaybackControls, provider.GetRequiredService<ILogger<MprisService>>());

            MprisService? Mpris() =>
                mprisServiceTask is { IsCompletedSuccessfully: true } ? mprisServiceTask.Result : null;

            mainWindow.Activated += (_, _) => Mpris()?.NudgePriority();

            provider.GetRequiredService<IOutputDeviceService>().ActiveDeviceLost += (_, _) =>
                Dispatcher.UIThread.Post(mainWindowViewModel.PlaybackControls.OnOutputDeviceLost);

            var richPresence = new RichPresenceCoordinator(
                mainWindowViewModel.PlaybackControls, provider.GetRequiredService<IRichPresence>(), mainWindowViewModel.Integrations);

            mainWindowViewModel.Updates.RestartingToUpdate += (_, _) =>
            {
                mainWindowViewModel.PersistQueueStateIfEnabled();
                mainWindowViewModel.PlaybackControls.Volume.Persist();
            };

            desktop.Exit += (_, _) =>
            {
                mainWindowViewModel.PersistQueueStateIfEnabled();
                mainWindowViewModel.PlaybackControls.Volume.Persist();
                mainWindowViewModel.Updates.InstallPendingUpdateOnExit();
                richPresence.Dispose();

                if (Mpris() is { } mprisService)
                {
                    _ = mprisService.DisposeAsync();
                }

                provider.Dispose();
            };

            var logger = provider.GetRequiredService<ILogger<App>>();
            _filesRequested = OpenedPaths.Count > 0;
            _openingFiles = InitializeAudioEngineAndRestoreStateAsync(audioEngine, mainWindowViewModel);
            var openedFiles = new OpenedFilesBatcher(OpenedFilesBatchWindow, TimeProvider.System,
                paths => Dispatcher.UIThread.Post(() => OpenFilesInOrder(mainWindowViewModel, paths, logger)));
            openedFiles.Add(OpenedPaths);

            Activations?.Attach(paths =>
            {
                _filesRequested = true;
                Dispatcher.UIThread.Post(() => BringToFront(mainWindow));
                openedFiles.Add(paths);
            });

            _ = mainWindowViewModel.Library.InitializeAsync();
            _ = mainWindowViewModel.Updates.CheckOnStartupAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    // Waits for the queue restore and for earlier requests, so opened files never get overwritten or reordered.
    private void OpenFilesInOrder(MainWindowViewModel mainWindowViewModel, IReadOnlyList<string> paths, ILogger<App> logger)
    {
        var previous = _openingFiles;
        _openingFiles = OpenAsync();

        async Task OpenAsync()
        {
            await previous.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
            try
            {
                await mainWindowViewModel.OpenFilesAsync(paths);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to open {Count} path(s) passed to the app", paths.Count);
            }
        }
    }

    private static void BringToFront(Window window)
    {
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Show();
        window.Activate();
    }

    private async Task InitializeAudioEngineAndRestoreStateAsync(IAudioEngine audioEngine, MainWindowViewModel mainWindowViewModel)
    {
        await Task.Run(() => audioEngine.InitializeAsync());
        await mainWindowViewModel.PlaybackSettings.ApplyPersistedOutputDeviceAsync();
        mainWindowViewModel.Equalizer.ApplyPersistedState();
        await mainWindowViewModel.RestoreQueueIfEnabledAsync();
        if (!_filesRequested)
        {
            mainWindowViewModel.ResumePlaybackIfEnabled();
        }
    }
}