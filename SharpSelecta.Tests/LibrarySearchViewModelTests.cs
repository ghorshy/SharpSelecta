using SharpSelecta.App.ViewModels;

namespace SharpSelecta.Tests;

public class LibrarySearchViewModelTests
{
    [Test]
    public async Task TypingAQuery_AppliesItOnlyAfterTheDebounce()
    {
        var search = new LibrarySearchViewModel();
        var applied = new List<string>();
        search.Applied += (_, _) => applied.Add(search.Query);

        search.Query = "oasis";
        await Assert.That(applied).IsEmpty();

        await search.DebounceTask;
        await Assert.That(applied).IsEquivalentTo(["oasis"]);
    }

    [Test]
    public async Task FastTyping_AppliesOnceWithTheLastQuery()
    {
        var search = new LibrarySearchViewModel();
        var applied = new List<string>();
        search.Applied += (_, _) => applied.Add(search.Query);

        search.Query = "o";
        search.Query = "oa";
        search.Query = "oasis";
        await search.DebounceTask;

        await Assert.That(applied).IsEquivalentTo(["oasis"]);
    }

    [Test]
    public async Task ClearCommand_ResetsTheQueryAndAppliesAtOnce()
    {
        var search = new LibrarySearchViewModel { Query = "oasis" };
        var applied = new List<string>();
        search.Applied += (_, _) => applied.Add(search.Query);

        search.ClearCommand.Execute(null);

        await Assert.That(search.Query).IsEqualTo("");
        await Assert.That(applied).IsEquivalentTo([""]);
    }

    [Test]
    public async Task ClearingBeforeTheDebounce_CancelsThePendingQuery()
    {
        var search = new LibrarySearchViewModel();
        var applied = new List<string>();
        search.Applied += (_, _) => applied.Add(search.Query);

        search.Query = "oasis";
        var pending = search.DebounceTask;
        search.Query = "";
        await pending;

        await Assert.That(applied).IsEquivalentTo([""]);
    }

    [Test]
    public async Task FocusCommand_RaisesFocusRequested()
    {
        var search = new LibrarySearchViewModel();
        var raised = false;
        search.FocusRequested += (_, _) => raised = true;

        search.FocusCommand.Execute(null);

        await Assert.That(raised).IsTrue();
    }
}
