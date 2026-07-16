using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.Core.Playback;

namespace SharpSelecta.App.ViewModels;

public partial class QueueViewModel : ViewModelBase
{
    private readonly PlaybackControlsViewModel _playbackControls;

    public QueueViewModel(PlaybackControlsViewModel playbackControls)
    {
        _playbackControls = playbackControls;
        _playbackControls.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(PlaybackControlsViewModel.QueueCurrentIndex))
            {
                OnPropertyChanged(nameof(CurrentIndex));
            }
        };
    }

    public ObservableCollection<QueueEntry> Entries => _playbackControls.QueueEntries;

    public int CurrentIndex => _playbackControls.QueueCurrentIndex;

    [RelayCommand]
    private Task PlayEntryAsync(QueueEntry entry) => _playbackControls.PlayQueueEntryAsync(entry);
}
