using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class ArtistsEditorViewModelTests
{
    private static string Names(ArtistsEditorViewModel vm) => string.Join("|", vm.Artists.Select(a => a.Name));

    [Test]
    public async Task Constructor_ListsTheGivenArtistsInOrder()
    {
        var vm = new ArtistsEditorViewModel(["A", "B"]);

        await Assert.That(Names(vm)).IsEqualTo("A|B");
    }

    [Test]
    public async Task Add_AppendsTheTrimmedNameAndClearsTheBox()
    {
        var vm = new ArtistsEditorViewModel(["A"]);
        vm.NewArtistName = "  B  ";

        vm.AddCommand.Execute(null);

        await Assert.That(Names(vm)).IsEqualTo("A|B");
        await Assert.That(vm.NewArtistName).IsEqualTo("");
    }

    [Test]
    public async Task Add_IsDisabledForABlankName()
    {
        var vm = new ArtistsEditorViewModel([]);

        await Assert.That(vm.AddCommand.CanExecute(null)).IsFalse();

        vm.NewArtistName = "   ";
        await Assert.That(vm.AddCommand.CanExecute(null)).IsFalse();

        vm.NewArtistName = "X";
        await Assert.That(vm.AddCommand.CanExecute(null)).IsTrue();
    }

    [Test]
    public async Task Add_IgnoresAnArtistAlreadyInTheList_ComparingCaseInsensitively()
    {
        var vm = new ArtistsEditorViewModel(["Moby"]);
        vm.NewArtistName = "moby";

        vm.AddCommand.Execute(null);

        await Assert.That(Names(vm)).IsEqualTo("Moby");
        await Assert.That(vm.NewArtistName).IsEqualTo("");
    }

    [Test]
    public async Task Add_SplitsANameWithTheSeparatorIntoSeveralArtists()
    {
        var vm = new ArtistsEditorViewModel([]);
        vm.NewArtistName = "A;B";

        vm.AddCommand.Execute(null);

        await Assert.That(Names(vm)).IsEqualTo("A|B");
    }

    [Test]
    public async Task Remove_DropsThatEntry()
    {
        var vm = new ArtistsEditorViewModel(["A", "B", "C"]);

        vm.RemoveCommand.Execute(vm.Artists[1]);

        await Assert.That(Names(vm)).IsEqualTo("A|C");
    }

    [Test]
    public async Task Result_ReflectsRenamesAndDropsBlanksAndDuplicates()
    {
        var vm = new ArtistsEditorViewModel(["A", "B", "C"]);
        vm.Artists[0].Name = "  Alpha ";
        vm.Artists[1].Name = "";
        vm.Artists[2].Name = "alpha";

        await Assert.That(string.Join("|", vm.Result)).IsEqualTo("Alpha");
    }

    [Test]
    public async Task Ok_KeepsWhatIsStillTypedInTheAddBox_AndReturnsTheList()
    {
        var vm = new ArtistsEditorViewModel(["A"]);
        vm.NewArtistName = "B";
        IReadOnlyList<string>? closedWith = null;
        var closed = false;
        vm.CloseRequested += (_, result) => (closed, closedWith) = (true, result);

        vm.OkCommand.Execute(null);

        await Assert.That(closed).IsTrue();
        await Assert.That(string.Join("|", closedWith!)).IsEqualTo("A|B");
    }

    [Test]
    public async Task Cancel_ClosesWithNull()
    {
        var vm = new ArtistsEditorViewModel(["A"]);
        IReadOnlyList<string>? closedWith = ["sentinel"];
        var closed = false;
        vm.CloseRequested += (_, result) => (closed, closedWith) = (true, result);

        vm.CancelCommand.Execute(null);

        await Assert.That(closed).IsTrue();
        await Assert.That(closedWith).IsNull();
    }

    private static TrackPropertiesViewModel CreateProperties(out string path, out DirectoryInfo dir)
    {
        dir = Directory.CreateTempSubdirectory("sharpselecta-artists-editor-tests-");
        path = Path.Combine(dir.FullName, "untagged-track.mp3");
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "untagged-track.mp3"), path);
        TrackTagEditor.Write(path, new TrackTagEdits("T", "First;Second", null, null, null, null, null, null));
        var track = MusicLibraryScanner.ReadTrackIfExists(path)!;
        return new TrackPropertiesViewModel(track, Substitute.For<IFilePickerService>(), Substitute.For<IFileManagerService>(), NullLogger.Instance);
    }

    [Test]
    public async Task TrackProperties_CreateArtistsEditor_StartsFromTheArtistField()
    {
        var vm = CreateProperties(out _, out var dir);
        try
        {
            await Assert.That(Names(vm.CreateArtistsEditor())).IsEqualTo("First|Second");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task TrackProperties_ApplyArtists_UpdatesTheFieldAndMakesItDirty_AndSavesAllArtists()
    {
        var vm = CreateProperties(out var path, out var dir);
        try
        {
            vm.ApplyArtists(["First", "Second", "Third"]);

            await Assert.That(vm.Artist).IsEqualTo("First;Second;Third");
            await Assert.That(vm.IsDirty).IsTrue();

            await vm.ApplyCommand.ExecuteAsync(null);

            var saved = ArtistList.Split(MusicLibraryScanner.ReadTrackIfExists(path)!.Artist);
            await Assert.That(string.Join("|", saved)).IsEqualTo("First|Second|Third");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task TrackProperties_ApplyingAnEmptyArtistList_ClearsTheField()
    {
        var vm = CreateProperties(out _, out var dir);
        try
        {
            vm.ApplyArtists([]);

            await Assert.That(vm.Artist).IsNull();
            await Assert.That(vm.IsDirty).IsTrue();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
