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

    public ObservableCollection<QueueEntryViewModel> Entries { get; } = [];

    public QueueViewModel(PlaybackControlsViewModel playbackControls, ILogger<QueueViewModel> logger)
    {
        _playbackControls = playbackControls;
        _logger = logger;

        foreach (var entry in _playbackControls.QueueEntries)
        {
            Entries.Add(new QueueEntryViewModel(entry, this));
        }

        ((INotifyCollectionChanged)_playbackControls.QueueEntries).CollectionChanged += OnQueueEntriesChanged;
        _playbackControls.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaybackControlsViewModel.QueueCurrentIndex))
            {
                OnPropertyChanged(nameof(CurrentIndex));
                RemoveFromQueueCommand.NotifyCanExecuteChanged();
                RefreshIsCurrent();
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
        ClearQueueCommand.NotifyCanExecuteChanged();
        RefreshIsCurrent();
    }

    private void RefreshIsCurrent()
    {
        foreach (var entry in Entries)
        {
            entry.NotifyIsCurrentChanged();
        }
    }

    public int CurrentIndex => _playbackControls.QueueCurrentIndex;

    [RelayCommand]
    private Task PlayEntryAsync(QueueEntryViewModel entry) => _playbackControls.PlayQueueEntryAsync(entry.Entry);

    [RelayCommand(CanExecute = nameof(CanRemoveFromQueue))]
    private void RemoveFromQueue(QueueEntryViewModel entry) => _playbackControls.RemoveFromQueue(entry.Entry);

    private bool CanRemoveFromQueue(QueueEntryViewModel entry) => Entries.IndexOf(entry) != CurrentIndex;

    [RelayCommand(CanExecute = nameof(CanClearQueue))]
    private void ClearQueue() => _playbackControls.ClearQueueExceptCurrent();

    private bool CanClearQueue() => Entries.Count > 1;

    public void MoveEntry(QueueEntryViewModel entry, QueueEntryViewModel? targetEntry) =>
        _playbackControls.MoveQueueEntry(entry.Entry, targetEntry?.Entry);

    public void ReportDragReorderFailure(Exception exception) =>
        _logger.LogError(exception, "Queue drag-and-drop reorder failed");
}
