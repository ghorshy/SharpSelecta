using System.Collections.ObjectModel;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Core.Playback;

public sealed class PlaybackQueue
{
    public ObservableCollection<QueueEntry> Entries { get; } = [];

    public int CurrentIndex { get; private set; } = -1;

    public bool CanGoNext => CurrentIndex + 1 < Entries.Count;

    public bool CanGoPrevious => CurrentIndex > 0;

    public event EventHandler? CurrentIndexChanged;

    // A track played "right now" is inserted right after wherever we currently are and becomes
    // the new current entry — anything already queued ahead stays queued, just pushed further out,
    // so nothing gets lost and the whole play history (including ad-hoc picks) stays browsable.
    public void PlayNow(Track track)
    {
        var insertIndex = CurrentIndex + 1;
        Entries.Insert(insertIndex, new QueueEntry(track, QueueEntrySource.Manual));
        SetCurrentIndex(insertIndex);
    }

    public void PlayNext(Track track) => Entries.Insert(CurrentIndex + 1, new QueueEntry(track, QueueEntrySource.Manual));

    // Manual additions go after any earlier manual entries but always ahead of the auto-DJ tail,
    // so the auto-DJ's random picks never bury something the user deliberately queued up.
    public void AddToQueue(Track track)
    {
        var insertIndex = Entries.Count;
        for (var i = CurrentIndex + 1; i < Entries.Count; i++)
        {
            if (Entries[i].Source != QueueEntrySource.AutoDj) continue;
            insertIndex = i;
            break;
        }

        Entries.Insert(insertIndex, new QueueEntry(track, QueueEntrySource.Manual));
    }

    public Track? MoveNext()
    {
        if (!CanGoNext)
        {
            return null;
        }

        SetCurrentIndex(CurrentIndex + 1);
        return Entries[CurrentIndex].Track;
    }

    public Track? MovePrevious()
    {
        if (!CanGoPrevious)
        {
            return null;
        }

        SetCurrentIndex(CurrentIndex - 1);
        return Entries[CurrentIndex].Track;
    }

    // Used to wrap back around for RepeatMode.RepeatAll once the end of the queue is reached.
    public Track? MoveToStart() => JumpTo(0);

    // Jumps straight to an arbitrary entry — e.g. double-clicking a row in the Queue view.
    public Track? JumpTo(int index)
    {
        if (index < 0 || index >= Entries.Count)
        {
            return null;
        }

        SetCurrentIndex(index);
        return Entries[index].Track;
    }

    private void SetCurrentIndex(int value)
    {
        CurrentIndex = value;
        CurrentIndexChanged?.Invoke(this, EventArgs.Empty);
    }
}
