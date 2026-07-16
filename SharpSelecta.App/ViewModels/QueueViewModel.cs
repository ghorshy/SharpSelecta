using System.Collections.ObjectModel;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed class QueueViewModel : ViewModelBase
{
    private readonly PlaybackQueue _queue;

    public QueueViewModel(PlaybackQueue queue)
    {
        _queue = queue;
        _queue.CurrentIndexChanged += (_, _) => OnPropertyChanged(nameof(CurrentIndex));
    }

    public ObservableCollection<QueueEntry> Entries => _queue.Entries;

    public int CurrentIndex => _queue.CurrentIndex;
}
