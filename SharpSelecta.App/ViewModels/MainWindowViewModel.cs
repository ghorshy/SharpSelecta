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

    [ObservableProperty]
    private GridLength rightColumnWidth;

    public MainWindowViewModel(
        IAudioEngine audioEngine,
        IOutputDeviceService outputDeviceService,
        IFilePickerService filePickerService,
        string settingsFilePath,
        ILogger<PlaybackControlsViewModel> playbackControlsLogger,
        ILogger<LibraryViewModel> libraryLogger,
        ILogger<QueueViewModel> queueLogger)
    {
        _settingsFilePath = settingsFilePath;

        var queue = new PlaybackQueue();
        PlaybackControls = new PlaybackControlsViewModel(audioEngine, queue, playbackControlsLogger);
        Library = new LibraryViewModel(filePickerService, PlaybackControls, settingsFilePath, libraryLogger);
        Queue = new QueueViewModel(PlaybackControls, queueLogger);
        // Constructed after PlaybackControls so it can hydrate the saved volume curve and volume
        // into it immediately (see PlaybackSettingsViewModel's constructor).
        PlaybackSettings = new PlaybackSettingsViewModel(settingsFilePath, outputDeviceService, PlaybackControls);

        // Assigning the backing field directly (not the generated property) so loading the saved
        // width on startup doesn't immediately re-save the same value it was just loaded from.
        rightColumnWidth = new GridLength(SettingsStore.LoadRightColumnWidth(_settingsFilePath) ?? DefaultRightColumnWidth);
    }

    public void PersistRightColumnWidth() =>
        SettingsStore.SaveRightColumnWidth(_settingsFilePath, RightColumnWidth.Value);

    // Called on the volume slider's PointerReleased (not on every OnVolumeChanged tick) to avoid
    // writing the settings file dozens of times during a single drag - same debounce-on-commit
    // pattern as PersistRightColumnWidth (GridSplitter DragCompleted) and column widths (DataGrid
    // PointerReleased).
    public void PersistVolume() =>
        SettingsStore.SaveVolume(_settingsFilePath, PlaybackControls.Volume);

    // Called once on startup, after the audio engine has finished initializing (Load() throws
    // until then). Reads tracks directly off disk rather than through the Library's own scan, since
    // a saved queue entry doesn't have to live under a currently configured library folder, and
    // restoring shouldn't have to wait on a full folder rescan to finish first.
    public async Task RestoreQueueIfEnabledAsync()
    {
        if (!PlaybackSettings.RestoreQueueOnStartup)
            return;

        var state = QueueStateStore.Load(_settingsFilePath);
        if (state is null)
            return;

        // ReadTrackIfExists re-parses each entry's tags from disk - pushed off the UI thread so a
        // long saved queue can't stall first paint (this runs during startup, on the dispatcher).
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

        // restoredCurrentIndex may still be -1 here (the previously-current track went missing
        // since it was saved) - the queue's own Restore clamp resolves that to the first
        // still-available entry, so it isn't patched up separately here.
        await PlaybackControls.RestoreQueueAsync(restoredEntries, restoredCurrentIndex, state.PositionSeconds);
    }

    // Called on app exit. Always overwrites the saved queue state (even with an empty queue) so a
    // session that ends with nothing queued doesn't leave a stale queue for RestoreQueueIfEnabledAsync
    // to resurrect next launch.
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
