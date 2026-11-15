using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using SharpSelecta.Core.Collections;

namespace SharpSelecta.App.Collections;

public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    // Past this many edits one Reset is cheaper for a bound DataGrid than item-by-item notifications.
    public const int DefaultResetThreshold = 200;

    private bool _syncing;

    // Once per change: after a whole ReplaceAll or SyncTo, or after a single Add/Remove/set made directly.
    public event EventHandler? Changed;

    protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        base.OnCollectionChanged(e);
        if (!_syncing)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void ReplaceAll(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    // Keyed by reference: items both lists share stay, the rest are removed or inserted.
    public void SyncTo(IReadOnlyList<T> target, int resetThreshold = DefaultResetThreshold) =>
        SyncTo<T, object>(target, item => item, item => item!, item => item, (_, _) => { }, ReferenceEqualityComparer.Instance, resetThreshold);

    // Items are matched to target sources by key; a matched item whose source changed is updated in place.
    public void SyncTo<TSource, TKey>(
        IReadOnlyList<TSource> target,
        Func<T, TSource> sourceOf,
        Func<TSource, TKey> keyOf,
        Func<TSource, T> create,
        Action<T, TSource> update,
        IEqualityComparer<TKey>? keyComparer = null,
        int resetThreshold = DefaultResetThreshold)
        where TKey : notnull
    {
        var current = Items.Select(sourceOf).ToList();
        var edits = ListDiff.Compute(current, target, keyOf, keyComparer);
        if (edits is null || edits.Count > resetThreshold)
        {
            var existing = new Dictionary<TKey, T>(keyComparer);
            foreach (var item in Items)
            {
                existing.TryAdd(keyOf(sourceOf(item)), item);
            }

            ReplaceAll(target.Select(source => Reuse(existing, source, sourceOf, keyOf, create, update)).ToList());
            return;
        }

        if (edits.Count == 0)
            return;

        _syncing = true;
        try
        {
            foreach (var edit in edits)
            {
                switch (edit)
                {
                    case ListEdit<TSource>.Remove remove:
                        RemoveAt(remove.Index);
                        break;
                    case ListEdit<TSource>.Insert insert:
                        Insert(insert.Index, create(insert.Item));
                        break;
                    case ListEdit<TSource>.Replace replace:
                        update(this[replace.Index], replace.Item);
                        break;
                }
            }
        }
        finally
        {
            _syncing = false;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static T Reuse<TSource, TKey>(
        Dictionary<TKey, T> existing, TSource source, Func<T, TSource> sourceOf, Func<TSource, TKey> keyOf,
        Func<TSource, T> create, Action<T, TSource> update)
        where TKey : notnull
    {
        if (!existing.Remove(keyOf(source), out var item))
            return create(source);

        if (!EqualityComparer<TSource>.Default.Equals(sourceOf(item), source))
            update(item, source);

        return item;
    }
}
