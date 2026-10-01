using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class TrackPropertiesViewModelTests
{
    private static readonly DateTime DateAdded = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);

    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static TrackPropertiesViewModel CreateViewModel(
        string fixtureName,
        out string path,
        out DirectoryInfo dir,
        out IFilePickerService filePicker,
        out IFileManagerService fileManager)
    {
        dir = Directory.CreateTempSubdirectory("sharpselecta-track-properties-tests-");
        path = Path.Combine(dir.FullName, fixtureName);
        File.WriteAllBytes(path, Fixture(fixtureName));
        filePicker = Substitute.For<IFilePickerService>();
        fileManager = Substitute.For<IFileManagerService>();
        var track = MusicLibraryScanner.ReadTrackIfExists(path)! with { DateAddedUtc = DateAdded };
        return new TrackPropertiesViewModel(track, filePicker, fileManager, NullLogger.Instance);
    }

    [Test]
    public async Task Constructor_LoadsTheTracksFields()
    {
        var vm = CreateViewModel("tagged-track.mp3", out _, out var dir, out _, out _);
        try
        {
            await Assert.That(vm.Title).IsEqualTo("Test Song");
            await Assert.That(vm.YearText).IsNotEmpty();
            await Assert.That(vm.IsDirty).IsFalse();
            await Assert.That(vm.ApplyCommand.CanExecute(null)).IsFalse();
            await Assert.That(vm.OkCommand.CanExecute(null)).IsTrue();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task EditingAField_MakesItDirty_AndRevertingItClearsDirty()
    {
        var vm = CreateViewModel("tagged-track.mp3", out _, out var dir, out _, out _);
        try
        {
            var original = vm.Title;

            vm.Title = "Changed";
            await Assert.That(vm.IsDirty).IsTrue();
            await Assert.That(vm.ApplyCommand.CanExecute(null)).IsTrue();

            vm.Title = original;
            await Assert.That(vm.IsDirty).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task WhitespaceOnlyChanges_AreNotDirty()
    {
        var vm = CreateViewModel("untagged-track.mp3", out _, out var dir, out _, out _);
        try
        {
            vm.Genre = "   ";

            await Assert.That(vm.IsDirty).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    [Arguments("abc")]
    [Arguments("-5")]
    [Arguments("0")]
    [Arguments("10000")]
    public async Task InvalidYear_FlagsAnErrorAndDisablesApplyAndOk(string year)
    {
        var vm = CreateViewModel("untagged-track.mp3", out _, out var dir, out _, out _);
        try
        {
            vm.Title = "Changed";
            vm.YearText = year;

            await Assert.That(vm.HasYearError).IsTrue();
            await Assert.That(vm.IsValid).IsFalse();
            await Assert.That(vm.ApplyCommand.CanExecute(null)).IsFalse();
            await Assert.That(vm.OkCommand.CanExecute(null)).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task InvalidTrackNumber_FlagsAnError()
    {
        var vm = CreateViewModel("untagged-track.mp3", out _, out var dir, out _, out _);
        try
        {
            vm.TrackNumberText = "x";

            await Assert.That(vm.HasTrackNumberError).IsTrue();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Apply_WritesTheFile_RaisesTracksSavedAndClearsDirty()
    {
        var vm = CreateViewModel("untagged-track.mp3", out var path, out var dir, out _, out _);
        try
        {
            Track? saved = null;
            vm.TracksSaved += (_, tracks) => saved = tracks.Single();

            vm.Title = "New Title";
            vm.Artist = "New Artist";
            vm.Genre = "Jazz";
            vm.Comment = "A comment";
            vm.YearText = "1999";
            vm.TrackNumberText = "7";
            await vm.ApplyCommand.ExecuteAsync(null);

            var onDisk = MusicLibraryScanner.ReadTrackIfExists(path)!;
            await Assert.That(onDisk.Title).IsEqualTo("New Title");
            await Assert.That(onDisk.Genre).IsEqualTo("Jazz");
            await Assert.That(onDisk.Year).IsEqualTo(1999);
            await Assert.That(onDisk.TrackNumber).IsEqualTo(7);
            await Assert.That(saved).IsNotNull();
            await Assert.That(saved!.Title).IsEqualTo("New Title");
            await Assert.That(saved.DateAddedUtc).IsEqualTo(DateAdded);
            await Assert.That(vm.Track.Title).IsEqualTo("New Title");
            await Assert.That(vm.IsDirty).IsFalse();
            await Assert.That(vm.ErrorMessage).IsNull();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Apply_ClearingAField_RemovesItFromTheFile()
    {
        var vm = CreateViewModel("tagged-track.mp3", out var path, out var dir, out _, out _);
        try
        {
            vm.YearText = "";
            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(path)!.Year).IsNull();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Ok_AppliesThenRequestsClose()
    {
        var vm = CreateViewModel("untagged-track.mp3", out var path, out var dir, out _, out _);
        try
        {
            var closed = false;
            vm.CloseRequested += (_, _) => closed = true;

            vm.Title = "Via OK";
            await vm.OkCommand.ExecuteAsync(null);

            await Assert.That(closed).IsTrue();
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(path)!.Title).IsEqualTo("Via OK");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Ok_WhenNothingChanged_JustCloses()
    {
        var vm = CreateViewModel("untagged-track.mp3", out var path, out var dir, out _, out _);
        try
        {
            var before = File.GetLastWriteTimeUtc(path);
            var closed = false;
            vm.CloseRequested += (_, _) => closed = true;

            await vm.OkCommand.ExecuteAsync(null);

            await Assert.That(closed).IsTrue();
            await Assert.That(File.GetLastWriteTimeUtc(path)).IsEqualTo(before);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Ok_WhenSavingFails_KeepsTheWindowOpenAndReportsTheError()
    {
        var vm = CreateViewModel("untagged-track.mp3", out var path, out var dir, out _, out _);
        try
        {
            var closed = false;
            vm.CloseRequested += (_, _) => closed = true;
            vm.Title = "Doomed";
            File.Delete(path);

            await vm.OkCommand.ExecuteAsync(null);

            await Assert.That(closed).IsFalse();
            await Assert.That(vm.ErrorMessage).IsNotNull();
            await Assert.That(vm.IsDirty).IsTrue();
            await Assert.That(vm.OkCommand.CanExecute(null)).IsTrue(); // not stuck "applying"
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Cancel_RequestsCloseWithoutWriting()
    {
        var vm = CreateViewModel("untagged-track.mp3", out var path, out var dir, out _, out _);
        try
        {
            var before = File.ReadAllBytes(path);
            var closed = false;
            vm.CloseRequested += (_, _) => closed = true;

            vm.Title = "Discarded";
            vm.CancelCommand.Execute(null);

            await Assert.That(closed).IsTrue();
            await Assert.That(File.ReadAllBytes(path)).IsEquivalentTo(before);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task SetCoverFromFile_WithAnImage_ShowsItAndMakesTheViewModelDirty()
    {
        var vm = CreateViewModel("untagged-track.mp3", out _, out var dir, out _, out _);
        try
        {
            var imagePath = Path.Combine(dir.FullName, "picked.jpg");
            File.WriteAllBytes(imagePath, Fixture("cover-red.jpg"));

            await vm.SetCoverFromFileAsync(imagePath);

            await Assert.That(vm.CoverBytes).IsEquivalentTo(Fixture("cover-red.jpg"));
            await Assert.That(vm.HasCover).IsTrue();
            await Assert.That(vm.IsDirty).IsTrue();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task SetCoverFromFile_WithANonImage_ReportsAnErrorAndChangesNothing()
    {
        var vm = CreateViewModel("untagged-track.mp3", out _, out var dir, out _, out _);
        try
        {
            var textPath = Path.Combine(dir.FullName, "notes.jpg");
            File.WriteAllText(textPath, "not an image");

            await vm.SetCoverFromFileAsync(textPath);

            await Assert.That(vm.ErrorMessage).IsNotNull();
            await Assert.That(vm.HasCover).IsFalse();
            await Assert.That(vm.IsDirty).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ChangeCoverCommand_UsesThePickedFile_AndDoesNothingWhenThePickerIsCancelled()
    {
        var vm = CreateViewModel("untagged-track.mp3", out _, out var dir, out var filePicker, out _);
        try
        {
            filePicker.PickCoverImageAsync().Returns((string?)null);
            await vm.ChangeCoverCommand.ExecuteAsync(null);
            await Assert.That(vm.HasCover).IsFalse();

            var imagePath = Path.Combine(dir.FullName, "picked.png");
            File.WriteAllBytes(imagePath, Fixture("cover-blue.png"));
            filePicker.PickCoverImageAsync().Returns(imagePath);
            await vm.ChangeCoverCommand.ExecuteAsync(null);

            await Assert.That(vm.CoverBytes).IsEquivalentTo(Fixture("cover-blue.png"));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Apply_WithAPickedCoverEmbedded_EmbedsItInTheFile()
    {
        var vm = CreateViewModel("untagged-track.mp3", out var path, out var dir, out _, out _);
        try
        {
            var imagePath = Path.Combine(dir.FullName, "picked.jpg");
            File.WriteAllBytes(imagePath, Fixture("cover-red.jpg"));
            await vm.SetCoverFromFileAsync(imagePath);
            vm.SaveCoverAsSeparateFile = false;

            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(MusicLibraryScanner.LoadArtworkWithSource(path)!.IsSeparateFile).IsFalse();
            await Assert.That(MusicLibraryScanner.LoadArtwork(path)).IsEquivalentTo(Fixture("cover-red.jpg"));
            await Assert.That(vm.IsDirty).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Apply_WithAPickedCoverAsSeparateFile_WritesCoverJpg()
    {
        var vm = CreateViewModel("untagged-track.mp3", out var path, out var dir, out _, out _);
        try
        {
            var imagePath = Path.Combine(dir.FullName, "picked.jpg");
            File.WriteAllBytes(imagePath, Fixture("cover-red.jpg"));
            await vm.SetCoverFromFileAsync(imagePath);
            vm.SaveCoverAsSeparateFile = true;

            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(File.Exists(Path.Combine(dir.FullName, "cover.jpg"))).IsTrue();
            await Assert.That(MusicLibraryScanner.LoadArtworkWithSource(path)!.IsSeparateFile).IsTrue();
            await Assert.That(vm.HasFolderCoverFile).IsTrue();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task TogglingStorageOfAnExistingCover_IsDirty_AndMovesTheImageOnApply()
    {
        var vm = CreateViewModel("tagged-track-with-artwork.mp3", out var path, out var dir, out _, out _);
        try
        {
            await Assert.That(vm.HasCover).IsTrue();
            await Assert.That(vm.SaveCoverAsSeparateFile).IsFalse();

            vm.SaveCoverAsSeparateFile = true;
            await Assert.That(vm.IsDirty).IsTrue();
            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(new ATL.Track(path).EmbeddedPictures.Count).IsEqualTo(0);
            await Assert.That(MusicLibraryScanner.HasCoverFile(path)).IsTrue();
            await Assert.That(vm.IsDirty).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task RemoveCover_OnAnExistingCover_IsDirty_AndRemovesItOnApply()
    {
        var vm = CreateViewModel("tagged-track-with-artwork.mp3", out var path, out var dir, out _, out _);
        try
        {
            vm.RemoveCoverCommand.Execute(null);
            await Assert.That(vm.HasCover).IsFalse();
            await Assert.That(vm.IsDirty).IsTrue();

            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(MusicLibraryScanner.LoadArtwork(path)).IsNull();
            await Assert.That(vm.IsDirty).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task RemoveCover_OnACoverPickedThisSession_LeavesNothingToSave()
    {
        var vm = CreateViewModel("untagged-track.mp3", out _, out var dir, out _, out _);
        try
        {
            var imagePath = Path.Combine(dir.FullName, "picked.jpg");
            File.WriteAllBytes(imagePath, Fixture("cover-red.jpg"));
            await vm.SetCoverFromFileAsync(imagePath);

            vm.RemoveCoverCommand.Execute(null);

            await Assert.That(vm.IsDirty).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task RemoveCoverCommand_IsDisabledWhenThereIsNoCover()
    {
        var vm = CreateViewModel("untagged-track.mp3", out _, out var dir, out _, out _);
        try
        {
            await Assert.That(vm.RemoveCoverCommand.CanExecute(null)).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task HasFolderCoverFile_ReflectsACoverFileNextToTheTrack()
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-track-properties-tests-");
        try
        {
            var path = Path.Combine(dir.FullName, "untagged-track.mp3");
            File.WriteAllBytes(path, Fixture("untagged-track.mp3"));
            File.WriteAllBytes(Path.Combine(dir.FullName, "cover.jpg"), Fixture("cover-red.jpg"));
            var track = MusicLibraryScanner.ReadTrackIfExists(path)!;

            var vm = new TrackPropertiesViewModel(track, Substitute.For<IFilePickerService>(), Substitute.For<IFileManagerService>(), NullLogger.Instance);

            await Assert.That(vm.HasFolderCoverFile).IsTrue();
            await Assert.That(vm.SaveCoverAsSeparateFile).IsTrue();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ShowInFileManagerCommand_RevealsTheTracksFile()
    {
        var vm = CreateViewModel("untagged-track.mp3", out var path, out var dir, out _, out var fileManager);
        try
        {
            await vm.ShowInFileManagerCommand.ExecuteAsync(null);

            await fileManager.Received(1).RevealInFileManagerAsync(path);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task DiscNumber_IsLoaded_ValidatedLikeTheTrackNumber_AndSavedWithTheRest()
    {
        var vm = CreateViewModel("untagged-track.mp3", out var path, out var dir, out _, out _);
        try
        {
            await Assert.That(vm.DiscNumberText).IsEqualTo("");

            vm.DiscNumberText = "abc";
            await Assert.That(vm.HasDiscNumberError).IsTrue();
            await Assert.That(vm.IsValid).IsFalse();
            await Assert.That(vm.ApplyCommand.CanExecute(null)).IsFalse();

            vm.DiscNumberText = "2";
            await Assert.That(vm.HasDiscNumberError).IsFalse();
            await Assert.That(vm.IsDirty).IsTrue();

            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(path)!.DiscNumber).IsEqualTo(2);
            await Assert.That(vm.DiscNumberText).IsEqualTo("2");
            await Assert.That(vm.IsDirty).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
