using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class CreditsEditorViewModelTests
{
    private static string Names(CreditsEditorViewModel vm) =>
        string.Join("|", vm.Credits.Select(c => $"{c.Name}:{c.Role.Role}"));

    private static string ResultText(IEnumerable<CreditEntry> result) => string.Join("|", result.Select(c => $"{c.Name}:{c.Role}"));

    private static CreditsEditorViewModel Editor(params (string Name, CreditRole Role)[] credits) =>
        new(credits.Select(c => new CreditEntry(c.Role, c.Name)));

    [Test]
    public async Task Constructor_ListsTheGivenCreditsInOrderWithTheirRoles()
    {
        var vm = Editor(("A", CreditRole.Artist), ("B", CreditRole.Remixer));

        await Assert.That(Names(vm)).IsEqualTo("A:Artist|B:Remixer");
    }

    [Test]
    public async Task Roles_OffersEveryRole_AndTheAddRowStartsOnArtist()
    {
        var vm = Editor();

        await Assert.That(vm.Roles.Select(r => r.Role)).IsEquivalentTo(Enum.GetValues<CreditRole>());
        await Assert.That(vm.NewCreditRole.Role).IsEqualTo(CreditRole.Artist);
    }

    [Test]
    public async Task Add_AppendsTheTrimmedNameWithTheChosenRoleAndClearsTheBox()
    {
        var vm = Editor(("A", CreditRole.Artist));
        vm.NewCreditName = "  Bob  ";
        vm.NewCreditRole = vm.Roles.First(r => r.Role == CreditRole.Composer);

        vm.AddCommand.Execute(null);

        await Assert.That(Names(vm)).IsEqualTo("A:Artist|Bob:Composer");
        await Assert.That(vm.NewCreditName).IsEqualTo("");
    }

    [Test]
    public async Task Add_IsDisabledForABlankName()
    {
        var vm = Editor();

        await Assert.That(vm.AddCommand.CanExecute(null)).IsFalse();

        vm.NewCreditName = "   ";
        await Assert.That(vm.AddCommand.CanExecute(null)).IsFalse();

        vm.NewCreditName = "X";
        await Assert.That(vm.AddCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task Add_IgnoresADuplicateInTheSameRole_ButAllowsTheSameNameInAnotherRole()
    {
        var vm = Editor(("Moby", CreditRole.Artist));

        vm.NewCreditName = "moby";
        vm.AddCommand.Execute(null);
        await Assert.That(Names(vm)).IsEqualTo("Moby:Artist");

        vm.NewCreditRole = vm.Roles.First(r => r.Role == CreditRole.Remixer);
        vm.NewCreditName = "Moby";
        vm.AddCommand.Execute(null);
        await Assert.That(Names(vm)).IsEqualTo("Moby:Artist|Moby:Remixer");
    }

    [Test]
    public async Task Add_SplitsANameWithTheSeparatorIntoSeveralPeople()
    {
        var vm = Editor();
        vm.NewCreditName = "A;B";

        vm.AddCommand.Execute(null);

        await Assert.That(Names(vm)).IsEqualTo("A:Artist|B:Artist");
    }

    [Test]
    public async Task Remove_DropsThatEntry()
    {
        var vm = Editor(("A", CreditRole.Artist), ("B", CreditRole.Artist), ("C", CreditRole.Lyricist));

        vm.RemoveCommand.Execute(vm.Credits[1]);

        await Assert.That(Names(vm)).IsEqualTo("A:Artist|C:Lyricist");
    }

    [Test]
    public async Task Result_GroupsByRole_AndReflectsRenamesRoleChangesAndDropsBlanksAndDuplicates()
    {
        var vm = Editor(("A", CreditRole.Composer), ("B", CreditRole.Artist), ("C", CreditRole.Artist), ("D", CreditRole.Artist));
        vm.Credits[0].Name = "  Alpha ";
        vm.Credits[2].Name = "";
        vm.Credits[3].Role = vm.Roles.First(r => r.Role == CreditRole.Conductor);
        vm.Credits[3].Name = "b";

        await Assert.That(ResultText(vm.Result)).IsEqualTo("B:Artist|Alpha:Composer|b:Conductor");
    }

    [Test]
    public async Task Ok_KeepsWhatIsStillTypedInTheAddBox_AndReturnsTheList()
    {
        var vm = Editor(("A", CreditRole.Artist));
        vm.NewCreditName = "B";
        vm.NewCreditRole = vm.Roles.First(r => r.Role == CreditRole.Lyricist);
        IReadOnlyList<CreditEntry>? closedWith = null;
        var closed = false;
        vm.CloseRequested += (_, result) => (closed, closedWith) = (true, result);

        vm.OkCommand.Execute(null);

        await Assert.That(closed).IsTrue();
        await Assert.That(ResultText(closedWith!)).IsEqualTo("A:Artist|B:Lyricist");
    }

    [Test]
    public async Task Cancel_ClosesWithNull()
    {
        var vm = Editor(("A", CreditRole.Artist));
        IReadOnlyList<CreditEntry>? closedWith = [new CreditEntry(CreditRole.Artist, "sentinel")];
        var closed = false;
        vm.CloseRequested += (_, result) => (closed, closedWith) = (true, result);

        vm.CancelCommand.Execute(null);

        await Assert.That(closed).IsTrue();
        await Assert.That(closedWith).IsNull();
    }

    // --- TrackPropertiesViewModel integration ---

    private static string NewTrack(DirectoryInfo dir, string name, TrackTagEdits edits, TrackCredits credits)
    {
        var path = Path.Combine(dir.FullName, name);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "untagged-track.mp3"), path);
        TrackTagEditor.Write(path, edits, credits: credits);
        return path;
    }

    private static TrackPropertiesViewModel Properties(params string[] paths) => new(
        paths.Select(p => MusicLibraryScanner.ReadTrackIfExists(p)!).ToList(),
        Substitute.For<IFilePickerService>(), Substitute.For<IFileManagerService>(), NullLogger.Instance);

    private static TrackTagEdits Edits(string title, string? artist = null) => new(title, artist, null, null, null, null, null, null);

    [Test]
    public async Task TrackProperties_CreateCreditsEditor_ListsArtistsAndEveryCreditReadFromTheFile()
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-credits-vm-tests-");
        try
        {
            var path = NewTrack(dir, "a.mp3", Edits("T", "First;Second"), new TrackCredits("Rem", "Comp1;Comp2", "Cond", "Lyr"));

            var vm = Properties(path);

            await Assert.That(Names(vm.CreateCreditsEditor()))
                .IsEqualTo("First:Artist|Second:Artist|Rem:Remixer|Comp1:Composer|Comp2:Composer|Cond:Conductor|Lyr:Lyricist");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task TrackProperties_ApplyCredits_UpdatesTheFieldsMakesThemDirtyAndSavesEverything()
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-credits-vm-tests-");
        try
        {
            var path = NewTrack(dir, "a.mp3", Edits("T", "First"), TrackCredits.None);
            var vm = Properties(path);
            await Assert.That(vm.IsDirty).IsFalse();

            vm.ApplyCredits(
            [
                new(CreditRole.Artist, "First"), new(CreditRole.Artist, "Second"),
                new(CreditRole.Remixer, "Rem"), new(CreditRole.Composer, "Comp"),
                new(CreditRole.Conductor, "Cond"), new(CreditRole.Lyricist, "Lyr"),
            ]);

            await Assert.That(vm.Artist).IsEqualTo("First;Second");
            await Assert.That(vm.Remixer).IsEqualTo("Rem");
            await Assert.That(vm.IsDirty).IsTrue();

            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(path)!.Artist).IsEqualTo("First;Second");
            await Assert.That(TrackCredits.Read(path)).IsEqualTo(new TrackCredits("Rem", "Comp", "Cond", "Lyr"));
            await Assert.That(vm.IsDirty).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task TrackProperties_ApplyingAnEmptyList_ClearsEveryCredit()
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-credits-vm-tests-");
        try
        {
            var path = NewTrack(dir, "a.mp3", Edits("T", "First"), new TrackCredits("Rem", "Comp", "Cond", "Lyr"));
            var vm = Properties(path);

            vm.ApplyCredits([]);
            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(path)!.Artist).IsNull();
            await Assert.That(TrackCredits.Read(path)).IsEqualTo(TrackCredits.None);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task TrackProperties_EditingOnlyTheTitle_DoesNotRewriteCredits()
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-credits-vm-tests-");
        try
        {
            var path = Path.Combine(dir.FullName, "a.flac");
            File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "untagged-track.flac"), path);
            // A remixer under the other format's name: if Apply rewrote credits it would be moved to REMIXER.
            var atl = new ATL.Track(path);
            atl.AdditionalFields["TPE4"] = "Foreign Remixer";
            atl.Save();
            var vm = Properties(path);

            vm.Title = "Changed";
            await vm.ApplyCommand.ExecuteAsync(null);

            var keys = new ATL.Track(path).AdditionalFields.Keys;
            await Assert.That(keys.Contains("TPE4")).IsTrue();
            await Assert.That(keys.Any(k => k.Equals("REMIXER", StringComparison.OrdinalIgnoreCase))).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task MultiEdit_ARoleThatDiffersStartsEmpty_AndIsLeftAloneUnlessCreditsAreAddedToIt()
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-credits-vm-tests-");
        try
        {
            var one = NewTrack(dir, "one.mp3", Edits("One", "Same"), new TrackCredits("RemOne", "CompOne", "SharedCond", null));
            var two = NewTrack(dir, "two.mp3", Edits("Two", "Same"), new TrackCredits("RemTwo", "CompTwo", "SharedCond", null));
            var vm = Properties(one, two);

            await Assert.That(Names(vm.CreateCreditsEditor())).IsEqualTo("Same:Artist|SharedCond:Conductor");

            // Add a lyricist to everyone; leave the differing remixer and composer untouched.
            vm.ApplyCredits([new(CreditRole.Artist, "Same"), new(CreditRole.Conductor, "SharedCond"), new(CreditRole.Lyricist, "Poet")]);
            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(TrackCredits.Read(one)).IsEqualTo(new TrackCredits("RemOne", "CompOne", "SharedCond", "Poet"));
            await Assert.That(TrackCredits.Read(two)).IsEqualTo(new TrackCredits("RemTwo", "CompTwo", "SharedCond", "Poet"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task MultiEdit_AddingToADifferingRole_WritesItToEveryTrack()
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-credits-vm-tests-");
        try
        {
            var one = NewTrack(dir, "one.mp3", Edits("One", "Same"), new TrackCredits(null, "CompOne", null, null));
            var two = NewTrack(dir, "two.mp3", Edits("Two", "Same"), new TrackCredits(null, "CompTwo", null, null));
            var vm = Properties(one, two);

            vm.ApplyCredits([new(CreditRole.Artist, "Same"), new(CreditRole.Composer, "Everyone")]);
            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(TrackCredits.Read(one).Composer).IsEqualTo("Everyone");
            await Assert.That(TrackCredits.Read(two).Composer).IsEqualTo("Everyone");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task MultiEdit_RemovingEveryoneFromASharedRole_ClearsItOnEveryTrack()
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-credits-vm-tests-");
        try
        {
            var one = NewTrack(dir, "one.mp3", Edits("One", "Same"), new TrackCredits(null, null, "SharedCond", null));
            var two = NewTrack(dir, "two.mp3", Edits("Two", "Same"), new TrackCredits(null, null, "SharedCond", null));
            var vm = Properties(one, two);

            vm.ApplyCredits([new(CreditRole.Artist, "Same")]);
            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(TrackCredits.Read(one).Conductor).IsNull();
            await Assert.That(TrackCredits.Read(two).Conductor).IsNull();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
