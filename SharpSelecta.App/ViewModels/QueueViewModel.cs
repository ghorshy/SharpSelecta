using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.App.ViewModels;

public partial class QueueViewModel : ViewModelBase
{
    private readonly PlaybackControlsViewModel _playbackControls;
    private readonly ILogger<QueueViewModel> _logger;

    // Mirrors _playbackControls.QueueEntries item-for-item (see OnQueueEntriesChanged), wrapping
    // each QueueEntry so the view's DataTemplate/ContextMenu can reach back to this view model's
    // commands via QueueEntryViewModel.Queue instead of an untyped $parent/RelativeSource lookup.
    public ObservableCollection<QueueEntryViewModel> Entries { get; } = [];

    public QueueViewModel(PlaybackControlsViewModel playbackControls, ILogger<QueueViewModel> logger)
    {
        _playbackControls = playbackControls;
        _logger = logger;

        foreach (var entry in _playbackControls.QueueEntries)
        {
            Entries.Add(new QueueEntryViewModel(entry, this));
        }

        // ReadOnlyObservableCollection implements CollectionChanged explicitly, hence the cast.
        ((INotifyCollectionChanged)_playbackControls.QueueEntries).CollectionChanged += OnQueueEntriesChanged;
        _playbackControls.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaybackControlsViewModel.QueueCurrentIndex))
            {
                OnPropertyChanged(nameof(CurrentIndex));
                RemoveFromQueueCommand.NotifyCanExecuteChanged();
            }
        };
    }

    private void OnQueueEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                for (var i = 0; i < e.NewItems!.Count; i++)
                {
                    Entries.Insert(e.NewStartingIndex + i, new QueueEntryViewModel((QueueEntry)e.NewItems[i]!, this));
                }

                break;

            case NotifyCollectionChangedAction.Remove:
                for (var i = e.OldItems!.Count - 1; i >= 0; i--)
                {
                    Entries.RemoveAt(e.OldStartingIndex + i);
                }

                break;

            case NotifyCollectionChangedAction.Move:
                Entries.Move(e.OldStartingIndex, e.NewStartingIndex);
                break;

            case NotifyCollectionChangedAction.Replace:
                for (var i = 0; i < e.NewItems!.Count; i++)
                {
                    Entries[e.OldStartingIndex + i] = new QueueEntryViewModel((QueueEntry)e.NewItems[i]!, this);
                }

                break;

            case NotifyCollectionChangedAction.Reset:
                Entries.Clear();
                foreach (var entry in _playbackControls.QueueEntries)
                {
                    Entries.Add(new QueueEntryViewModel(entry, this));
                }

                break;
        }

        RemoveFromQueueCommand.NotifyCanExecuteChanged();
    }

    public int CurrentIndex => _playbackControls.QueueCurrentIndex;

    [RelayCommand]
    private Task PlayEntryAsync(QueueEntryViewModel entry) => _playbackControls.PlayQueueEntryAsync(entry.Entry);

    [RelayCommand(CanExecute = nameof(CanRemoveFromQueue))]
    private void RemoveFromQueue(QueueEntryViewModel entry) => _playbackControls.RemoveFromQueue(entry.Entry);

    // The currently playing entry can't be removed — see PlaybackQueue.RemoveAt.
    private bool CanRemoveFromQueue(QueueEntryViewModel entry) => Entries.IndexOf(entry) != CurrentIndex;

    // Called from the Queue view's drag-and-drop reorder handling in code-behind.
    public void MoveEntry(QueueEntryViewModel entry, QueueEntryViewModel? targetEntry) =>
        _playbackControls.MoveQueueEntry(entry.Entry, targetEntry?.Entry);

    // Called from the Queue view's code-behind if DragDrop.DoDragDropAsync throws — a platform-level
    // drag/drop failure (more plausible than usual since this app runs on the still-experimental
    // native Wayland backend) shouldn't crash the app, just fail that one drag silently past this log.
    public void ReportDragReorderFailure(Exception exception) =>
        _logger.LogError(exception, "Queue drag-and-drop reorder failed");
}
