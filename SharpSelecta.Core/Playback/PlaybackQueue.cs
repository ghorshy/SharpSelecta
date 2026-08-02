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

    public ReadOnlyObservableCollection<QueueEntry> Entries { get; }

    public int CurrentIndex { get; private set; } = -1;

    public bool CanGoNext => CurrentIndex + 1 < _entries.Count;

    public bool CanGoPrevious => CurrentIndex > 0;

    public event EventHandler? CurrentIndexChanged;

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

    public void PlayNow(Track track) => PlayNow([track]);

    public void PlayNow(IReadOnlyList<Track> tracks)
    {
        if (tracks.Count == 0)
            return;

        var insertIndex = CurrentIndex + 1;
        var firstInsertedIndex = insertIndex;
        foreach (var track in tracks)
        {
            _entries.Insert(insertIndex++, new QueueEntry(track, QueueEntrySource.Manual));
        }

        SetCurrentIndex(firstInsertedIndex);
    }

    public void PlayNext(Track track) => PlayNext([track]);

    public void PlayNext(IReadOnlyList<Track> tracks)
    {
        var insertIndex = CurrentIndex + 1;
        foreach (var track in tracks)
        {
            _entries.Insert(insertIndex++, new QueueEntry(track, QueueEntrySource.Manual));
        }
    }

    public void AddToQueue(Track track) => AddToQueue([track]);

    public void AddToQueue(IReadOnlyList<Track> tracks)
    {
        var insertIndex = _entries.Count;
        for (var i = CurrentIndex + 1; i < _entries.Count; i++)
        {
            if (_entries[i].Source != QueueEntrySource.AutoDj) continue;
            insertIndex = i;
            break;
        }

        foreach (var track in tracks)
        {
            _entries.Insert(insertIndex++, new QueueEntry(track, QueueEntrySource.Manual));
        }
    }

    public void AddAutoDjEntry(Track track) => _entries.Add(new QueueEntry(track, QueueEntrySource.AutoDj));

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

    public Track? MoveToStart() => JumpTo(0);

    public Track? JumpTo(int index)
    {
        if (index < 0 || index >= _entries.Count)
        {
            return null;
        }

        SetCurrentIndex(index);
        return _entries[index].Track;
    }

    public void Restore(IReadOnlyList<QueueEntry> entries, int currentIndex)
    {
        _entries.Clear();
        foreach (var entry in entries)
        {
            _entries.Add(entry);
        }

        SetCurrentIndex(_entries.Count == 0 ? -1 : Math.Clamp(currentIndex, 0, _entries.Count - 1));
    }

    private void SetCurrentIndex(int value)
    {
        CurrentIndex = value;
        CurrentIndexChanged?.Invoke(this, EventArgs.Empty);
    }
}
