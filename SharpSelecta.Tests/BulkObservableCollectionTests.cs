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
}
