using SharpSelecta.Core.Collections;

namespace SharpSelecta.Tests;

public class ListDiffTests
{
    private sealed record Item(string Key, int Version = 0);

    private static IReadOnlyList<ListEdit<Item>>? Diff(IReadOnlyList<Item> current, IReadOnlyList<Item> target) =>
        ListDiff.Compute(current, target, item => item.Key);

    private static List<Item> Apply(IReadOnlyList<Item> current, IReadOnlyList<ListEdit<Item>> edits)
    {
        var list = current.ToList();
        foreach (var edit in edits)
        {
            switch (edit)
            {
                case ListEdit<Item>.Remove remove:
                    list.RemoveAt(remove.Index);
                    break;
                case ListEdit<Item>.Insert insert:
                    list.Insert(insert.Index, insert.Item);
                    break;
                case ListEdit<Item>.Replace replace:
                    list[replace.Index] = replace.Item;
                    break;
            }
        }

        return list;
    }

    private static Item[] Items(string keys) => keys.Select(k => new Item(k.ToString())).ToArray();

    [Test]
    public async Task SameLists_NeedNoEdits()
    {
        await Assert.That(Diff(Items("abc"), Items("abc"))).IsEmpty();
    }

    [Test]
    [Arguments("abc", "")]
    [Arguments("", "abc")]
    [Arguments("abcde", "ace")]
    [Arguments("ace", "abcde")]
    [Arguments("abcd", "xbdy")]
    [Arguments("ab", "xyab")]
    public async Task Edits_TurnTheCurrentListIntoTheTarget(string current, string target)
    {
        var edits = Diff(Items(current), Items(target));

        await Assert.That(Apply(Items(current), edits!).Select(i => i.Key)).IsEquivalentTo(Items(target).Select(i => i.Key), TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task OnlyTheChangedItems_AreTouched()
    {
        var edits = Diff(Items("abcd"), [new("a"), new("c"), new("d", Version: 1), new("e")]);

        await Assert.That(edits!).IsEquivalentTo(new ListEdit<Item>[]
        {
            new ListEdit<Item>.Remove(1),
            new ListEdit<Item>.Replace(2, new("d", Version: 1)),
            new ListEdit<Item>.Insert(3, new("e")),
        }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    [Test]
    public async Task ItemsKeptInADifferentOrder_GiveNoEdits()
    {
        await Assert.That(Diff(Items("abc"), Items("cab"))).IsNull();
    }

    [Test]
    public async Task DuplicateKeys_GiveNoEdits()
    {
        await Assert.That(Diff(Items("aa"), Items("a"))).IsNull();
        await Assert.That(Diff(Items("a"), Items("aa"))).IsNull();
    }

    [Test]
    public async Task ItemComparer_DecidesWhatCountsAsChanged()
    {
        var current = new[] { new Item("a") };
        var target = new[] { new Item("a", Version: 1) };

        var edits = ListDiff.Compute(current, target, i => i.Key, itemComparer: EqualityComparer<Item>.Create((x, y) => x!.Key == y!.Key));

        await Assert.That(edits).IsEmpty();
    }
}
