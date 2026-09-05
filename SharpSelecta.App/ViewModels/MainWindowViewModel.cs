using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Audio;
using SharpSelecta.Core.Library;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const double DefaultRightColumnWidth = 220;

    private readonly string _settingsFilePath;

    public PlaybackControlsViewModel PlaybackControls { get; }

    public LibraryViewModel Library { get; }

    public QueueViewModel Queue { get; }

    public PlaybackSettingsViewModel PlaybackSettings { get; }

    public InterfaceSettingsViewModel InterfaceSettings { get; }

    public ShortcutSettingsService ShortcutSettings { get; }

    [ObservableProperty]
    private GridLength rightColumnWidth;

    public MainWindowViewModel(
        IAudioEngine audioEngine,
        IOutputDeviceService outputDeviceService,
        IFilePickerService filePickerService,
        IFileManagerService fileManagerService,
        string settingsFilePath,
        ILogger<PlaybackControlsViewModel> playbackControlsLogger,
        ILogger<LibraryViewModel> libraryLogger,
        ILogger<QueueViewModel> queueLogger)
    {
        _settingsFilePath = settingsFilePath;

        var queue = new PlaybackQueue();
        PlaybackControls = new PlaybackControlsViewModel(audioEngine, queue, playbackControlsLogger);
        Library = new LibraryViewModel(filePickerService, PlaybackControls, fileManagerService, settingsFilePath, libraryLogger);
        Queue = new QueueViewModel(PlaybackControls, queueLogger);
        PlaybackSettings = new PlaybackSettingsViewModel(settingsFilePath, outputDeviceService, PlaybackControls);
        InterfaceSettings = new InterfaceSettingsViewModel(settingsFilePath, filePickerService);
        ShortcutSettings = new ShortcutSettingsService(settingsFilePath);

        rightColumnWidth = new GridLength(SettingsStore.LoadRightColumnWidth(_settingsFilePath) ?? DefaultRightColumnWidth);
    }

    public void PersistRightColumnWidth() =>
        SettingsStore.SaveRightColumnWidth(_settingsFilePath, RightColumnWidth.Value);

    public void PersistVolume() =>
        SettingsStore.SaveVolume(_settingsFilePath, PlaybackControls.Volume);

    public async Task RestoreQueueIfEnabledAsync()
    {
        if (!PlaybackSettings.RestoreQueueOnStartup)
            return;

        var state = QueueStateStore.Load(_settingsFilePath);
        if (state is null)
            return;

        var (restoredEntries, restoredCurrentIndex) = await Task.Run(() =>
        {
            var entries = new List<QueueEntry>();
            var currentIndex = -1;
            for (var i = 0; i < state.Entries.Count; i++)
            {
                var savedEntry = state.Entries[i];
                var track = MusicLibraryScanner.ReadTrackIfExists(savedEntry.FilePath);
                if (track is null)
                    continue;

                if (i == state.CurrentIndex)
                    currentIndex = entries.Count;

                entries.Add(new QueueEntry(track, savedEntry.Source));
            }

            return (entries, currentIndex);
        });

        if (restoredEntries.Count == 0)
            return;

        await PlaybackControls.RestoreQueueAsync(restoredEntries, restoredCurrentIndex, state.PositionSeconds);
    }

    public void PersistQueueStateIfEnabled()
    {
        if (!PlaybackSettings.RestoreQueueOnStartup)
            return;

        var entries = PlaybackControls.QueueEntries
            .Select(e => new QueueStateStore.QueueEntryData(e.Track.FilePath, e.Source))
            .ToList();

        QueueStateStore.Save(_settingsFilePath, entries, PlaybackControls.QueueCurrentIndex, PlaybackControls.PositionSeconds);
    }
}
