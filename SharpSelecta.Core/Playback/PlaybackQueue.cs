using System.Collections.ObjectModel;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Core.Playback;

public sealed class PlaybackQueue
{
    private readonly ObservableCollection<QueueEntry> _entries = [];

    public PlaybackQueue()
    {
        Entries = new ReadOnlyObservableCollection<QueueEntry>(_entries);
    }

    // Read-only from the outside — every mutation must go through a named method below (PlayNow,
    // AddToQueue, Move, RemoveAt, ...) so invariants like "never remove the currently playing
    // entry" can't be bypassed by a caller reaching in and mutating the collection directly.
    public ReadOnlyObservableCollection<QueueEntry> Entries { get; }

    public int CurrentIndex { get; private set; } = -1;

    public bool CanGoNext => CurrentIndex + 1 < _entries.Count;

    public bool CanGoPrevious => CurrentIndex > 0;

    public event EventHandler? CurrentIndexChanged;

    // Finds an entry by reference identity, not QueueEntry's record (structural) equality — the
    // same track can legitimately be queued more than once, and callers (Move/RemoveFromQueue/
    // PlayQueueEntryAsync) need to act on the exact row they were given, not "whichever occurrence
    // happens to compare equal first."
    public int IndexOf(QueueEntry entry)
    {
        for (var i = 0; i < _entries.Count; i++)
        {
            if (ReferenceEquals(_entries[i], entry))
            {
                return i;
            }
        }

        return -1;
    }

    // A track played "right now" is inserted right after wherever we currently are and becomes
    // the new current entry — anything already queued ahead stays queued, just pushed further out,
    // so nothing gets lost and the whole play history (including ad-hoc picks) stays browsable.
    public void PlayNow(Track track)
    {
        var insertIndex = CurrentIndex + 1;
        _entries.Insert(insertIndex, new QueueEntry(track, QueueEntrySource.Manual));
        SetCurrentIndex(insertIndex);
    }

    public void PlayNext(Track track) => _entries.Insert(CurrentIndex + 1, new QueueEntry(track, QueueEntrySource.Manual));

    // Manual additions go after any earlier manual entries but always ahead of the auto-DJ tail,
    // so the auto-DJ's random picks never bury something the user deliberately queued up.
    public void AddToQueue(Track track)
    {
        var insertIndex = _entries.Count;
        for (var i = CurrentIndex + 1; i < _entries.Count; i++)
        {
            if (_entries[i].Source != QueueEntrySource.AutoDj) continue;
            insertIndex = i;
            break;
        }

        _entries.Insert(insertIndex, new QueueEntry(track, QueueEntrySource.Manual));
    }

    // Seeds an auto-DJ tail entry. No auto-DJ feature exists yet to call this from production
    // code, but AddToQueue's "insert before the auto-DJ tail" logic needs a supported way to
    // create one now that Entries is read-only from the outside — this is that seam.
    public void AddAutoDjEntry(Track track) => _entries.Add(new QueueEntry(track, QueueEntrySource.AutoDj));

    // Drag-and-drop reordering in the Queue view. Keeps CurrentIndex pointing at the same entry
    // it did before the move, mirroring how a ListBox's SelectedIndex tracks an item across reorder.
    public void Move(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex || oldIndex < 0 || oldIndex >= _entries.Count || newIndex < 0 || newIndex >= _entries.Count)
        {
            return;
        }

        _entries.Move(oldIndex, newIndex);

        if (oldIndex == CurrentIndex)
        {
            SetCurrentIndex(newIndex);
        }
        else if (oldIndex < CurrentIndex && newIndex >= CurrentIndex)
        {
            SetCurrentIndex(CurrentIndex - 1);
        }
        else if (oldIndex > CurrentIndex && newIndex <= CurrentIndex)
        {
            SetCurrentIndex(CurrentIndex + 1);
        }
    }

    // "Remove from Queue" — refuses to remove the currently playing entry. The engine already has
    // that file loaded and playing; there's no queue position left to fall back to that wouldn't
    // either silently keep the old audio running under a stale CurrentIndex or require stopping
    // playback out from under the user, so the simplest correct rule is: it just can't be removed.
    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _entries.Count || index == CurrentIndex)
        {
            return;
        }

        _entries.RemoveAt(index);

        if (index < CurrentIndex)
        {
            SetCurrentIndex(CurrentIndex - 1);
        }
    }

    public Track? MoveNext()
    {
        if (!CanGoNext)
        {
            return null;
        }

        SetCurrentIndex(CurrentIndex + 1);
        return _entries[CurrentIndex].Track;
    }

    public Track? MovePrevious()
    {
        if (!CanGoPrevious)
        {
            return null;
        }

        SetCurrentIndex(CurrentIndex - 1);
        return _entries[CurrentIndex].Track;
    }

    // Used to wrap back around for RepeatMode.RepeatAll once the end of the queue is reached.
    public Track? MoveToStart() => JumpTo(0);

    // Jumps straight to an arbitrary entry — e.g. double-clicking a row in the Queue view.
    public Track? JumpTo(int index)
    {
        if (index < 0 || index >= _entries.Count)
        {
            return null;
        }

        SetCurrentIndex(index);
        return _entries[index].Track;
    }

    private void SetCurrentIndex(int value)
    {
        CurrentIndex = value;
        CurrentIndexChanged?.Invoke(this, EventArgs.Empty);
    }
}
