using System.Collections.Specialized;
using SharpSelecta.App.Collections;

namespace SharpSelecta.Tests;

public class BulkObservableCollectionTests
{
    [Test]
    public async Task ReplaceAll_EndsUpContainingExactlyTheNewItems()
    {
        var collection = new BulkObservableCollection<int> { 1, 2, 3 };

        collection.ReplaceAll([4, 5]);

        await Assert.That(collection).IsEquivalentTo([4, 5]);
    }

    [Test]
    public async Task ReplaceAll_RaisesExactlyOneResetNotification()
    {
        var collection = new BulkObservableCollection<int> { 1, 2, 3 };
        var raisedActions = new List<NotifyCollectionChangedAction>();
        collection.CollectionChanged += (_, e) => raisedActions.Add(e.Action);

        collection.ReplaceAll([4, 5, 6, 7]);

        await Assert.That(raisedActions).IsEquivalentTo([NotifyCollectionChangedAction.Reset]);
    }

    [Test]
    public async Task ReplaceAll_OnEmptyCollection_PopulatesItems()
    {
        var collection = new BulkObservableCollection<int>();

        collection.ReplaceAll([1, 2, 3]);

        await Assert.That(collection).IsEquivalentTo([1, 2, 3]);
    }
    private sealed class Row(string key, int version)
    {
        public string Key { get; } = key;

        public int Version { get; set; } = version;
    }

    private static void Sync(BulkObservableCollection<Row> rows, IReadOnlyList<(string Key, int Version)> target, int resetThreshold = 10) =>
        rows.SyncTo(target, r => (r.Key, r.Version), s => s.Key, s => new Row(s.Key, s.Version), (r, s) => r.Version = s.Version, resetThreshold: resetThreshold);

    [Test]
    public async Task SyncTo_RaisesItemNotificationsAndOneChanged_AndUpdatesChangedItemsInPlace()
    {
        var a = new Row("a", 0);
        var b = new Row("b", 0);
        var rows = new BulkObservableCollection<Row> { a, b, new Row("c", 0) };
        var actions = new List<NotifyCollectionChangedAction>();
        var changed = 0;
        rows.CollectionChanged += (_, e) => actions.Add(e.Action);
        rows.Changed += (_, _) => changed++;

        Sync(rows, [("a", 0), ("b", 1), ("d", 0)]);

        await Assert.That(rows.Select(r => r.Key)).IsEquivalentTo(["a", "b", "d"]);
        await Assert.That(rows[0]).IsSameReferenceAs(a);
        await Assert.That(rows[1]).IsSameReferenceAs(b);
        await Assert.That(b.Version).IsEqualTo(1);
        await Assert.That(actions).IsEquivalentTo([NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add]);
        await Assert.That(changed).IsEqualTo(1);
    }

    [Test]
    public async Task SyncTo_WithNothingChanged_RaisesNothing()
    {
        var rows = new BulkObservableCollection<Row> { new("a", 0) };
        var raised = 0;
        rows.CollectionChanged += (_, _) => raised++;
        rows.Changed += (_, _) => raised++;

        Sync(rows, [("a", 0)]);

        await Assert.That(raised).IsEqualTo(0);
    }

    [Test]
    public async Task SyncTo_PastTheThresholdOrOnAReorder_ResetsOnce_ButKeepsTheMatchingItems()
    {
        var a = new Row("a", 0);
        var rows = new BulkObservableCollection<Row> { a, new("b", 0) };
        var actions = new List<NotifyCollectionChangedAction>();
        rows.CollectionChanged += (_, e) => actions.Add(e.Action);

        Sync(rows, [("b", 0), ("a", 2)]);
        Sync(rows, [("b", 0), ("a", 2), ("x", 0), ("y", 0)], resetThreshold: 1);

        await Assert.That(actions).IsEquivalentTo([NotifyCollectionChangedAction.Reset, NotifyCollectionChangedAction.Reset]);
        await Assert.That(rows.Select(r => r.Key)).IsEquivalentTo(["b", "a", "x", "y"]);
        await Assert.That(rows[1]).IsSameReferenceAs(a);
        await Assert.That(a.Version).IsEqualTo(2);
    }

    [Test]
    public async Task Changed_IsRaisedForADirectAdd()
    {
        var rows = new BulkObservableCollection<int>();
        var changed = 0;
        rows.Changed += (_, _) => changed++;

        rows.Add(1);

        await Assert.That(changed).IsEqualTo(1);
    }
}
