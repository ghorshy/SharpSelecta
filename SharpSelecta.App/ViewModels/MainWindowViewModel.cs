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
        IFilePickerService filePickerService,
        string librarySettingsFilePath,
        ILogger<PlaybackControlsViewModel> playbackControlsLogger,
        ILogger<LibraryViewModel> libraryLogger,
        ILogger<QueueViewModel> queueLogger)
    {
        _settingsFilePath = librarySettingsFilePath;

        var queue = new PlaybackQueue();
        PlaybackControls = new PlaybackControlsViewModel(audioEngine, queue, playbackControlsLogger);
        Library = new LibraryViewModel(filePickerService, PlaybackControls, librarySettingsFilePath, libraryLogger);
        Queue = new QueueViewModel(PlaybackControls, queueLogger);
        PlaybackSettings = new PlaybackSettingsViewModel(librarySettingsFilePath, audioEngine);

        // Assigning the backing field directly (not the generated property) so loading the saved
        // width on startup doesn't immediately re-save the same value it was just loaded from.
        rightColumnWidth = new GridLength(LibrarySettingsStore.LoadRightColumnWidth(_settingsFilePath) ?? DefaultRightColumnWidth);
    }

    public void PersistRightColumnWidth() =>
        LibrarySettingsStore.SaveRightColumnWidth(_settingsFilePath, RightColumnWidth.Value);

    // Called once on startup, after the audio engine has finished initializing (Load() throws
    // until then). Reads tracks directly off disk rather than through the Library's own scan, since
    // a saved queue entry doesn't have to live under a currently configured library folder, and
    // restoring shouldn't have to wait on a full folder rescan to finish first.
    public async Task RestoreQueueIfEnabledAsync()
    {
        if (!PlaybackSettings.RestoreQueueOnStartup)
            return;

        var state = LibrarySettingsStore.LoadQueueState(_settingsFilePath);
        if (state is null)
            return;

        var restoredEntries = new List<QueueEntry>();
        var restoredCurrentIndex = -1;
        for (var i = 0; i < state.Entries.Count; i++)
        {
            var savedEntry = state.Entries[i];
            var track = MusicLibraryScanner.ReadTrackIfExists(savedEntry.FilePath);
            if (track is null)
                continue;

            if (i == state.CurrentIndex)
                restoredCurrentIndex = restoredEntries.Count;

            restoredEntries.Add(new QueueEntry(track, savedEntry.Source));
        }

        if (restoredEntries.Count == 0)
            return;

        // The previously-current track itself went missing since it was saved (moved/deleted) —
        // fall back to the first still-available entry rather than restoring with nothing current.
        if (restoredCurrentIndex < 0)
            restoredCurrentIndex = 0;

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
            .Select(e => new LibrarySettingsStore.QueueEntryData(e.Track.FilePath, e.Source))
            .ToList();

        LibrarySettingsStore.SaveQueueState(_settingsFilePath, entries, PlaybackControls.QueueCurrentIndex, PlaybackControls.PositionSeconds);
    }
}
