namespace SharpSelecta.Core.Collections;

public abstract record ListEdit<T>(int Index)
{
    public sealed record Remove(int Index) : ListEdit<T>(Index);

    public sealed record Insert(int Index, T Item) : ListEdit<T>(Index);

    public sealed record Replace(int Index, T Item) : ListEdit<T>(Index);
}

public static class ListDiff
{
    // Edits are applied in order. Null when keys repeat or the kept items changed their order -
    // the caller rebuilds the whole list then.
    public static IReadOnlyList<ListEdit<T>>? Compute<T, TKey>(
        IReadOnlyList<T> current,
        IReadOnlyList<T> target,
        Func<T, TKey> keyOf,
        IEqualityComparer<TKey>? keyComparer = null,
        IEqualityComparer<T>? itemComparer = null)
        where TKey : notnull
    {
        itemComparer ??= EqualityComparer<T>.Default;

        var targetIndexByKey = new Dictionary<TKey, int>(target.Count, keyComparer);
        for (var i = 0; i < target.Count; i++)
        {
            if (!targetIndexByKey.TryAdd(keyOf(target[i]), i))
                return null;
        }

        var currentKeys = new HashSet<TKey>(current.Count, keyComparer);
        var edits = new List<ListEdit<T>>();
        var kept = new List<T>();
        for (var i = 0; i < current.Count; i++)
        {
            if (!currentKeys.Add(keyOf(current[i])))
                return null;

            if (targetIndexByKey.ContainsKey(keyOf(current[i])))
                kept.Add(current[i]);
        }

        for (var i = current.Count - 1; i >= 0; i--)
        {
            if (!targetIndexByKey.ContainsKey(keyOf(current[i])))
                edits.Add(new ListEdit<T>.Remove(i));
        }

        var previousTargetIndex = -1;
        foreach (var item in kept)
        {
            var targetIndex = targetIndexByKey[keyOf(item)];
            if (targetIndex < previousTargetIndex)
                return null;

            previousTargetIndex = targetIndex;
        }

        var keptIndex = 0;
        for (var i = 0; i < target.Count; i++)
        {
            if (keptIndex < kept.Count && targetIndexByKey[keyOf(kept[keptIndex])] == i)
            {
                if (!itemComparer.Equals(kept[keptIndex], target[i]))
                    edits.Add(new ListEdit<T>.Replace(i, target[i]));

                keptIndex++;
            }
            else
            {
                edits.Add(new ListEdit<T>.Insert(i, target[i]));
            }
        }

        return edits;
    }
}
