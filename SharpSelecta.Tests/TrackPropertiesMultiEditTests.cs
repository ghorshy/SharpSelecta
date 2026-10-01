using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class TrackPropertiesMultiEditTests
{
    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    // Two tracks sharing an album and genre but with their own title/artist/year/track number.
    private static TrackPropertiesViewModel CreateViewModel(out string[] paths, out DirectoryInfo dir, out List<IReadOnlyList<Track>> saved)
    {
        dir = Directory.CreateTempSubdirectory("sharpselecta-multi-edit-tests-");
        paths = [Path.Combine(dir.FullName, "one.mp3"), Path.Combine(dir.FullName, "two.mp3")];
        File.WriteAllBytes(paths[0], Fixture("untagged-track.mp3"));
        File.WriteAllBytes(paths[1], Fixture("untagged-track.mp3"));
        TrackTagEditor.Write(paths[0], new TrackTagEdits("First", "Artist A", null, "Shared Album", "Rock", "c1", 2001, 1));
        TrackTagEditor.Write(paths[1], new TrackTagEdits("Second", "Artist B", null, "Shared Album", "Rock", "c2", 2001, 2));
        var tracks = paths.Select(p => MusicLibraryScanner.ReadTrackIfExists(p)!).ToList();
        var vm = new TrackPropertiesViewModel(tracks, Substitute.For<IFilePickerService>(), Substitute.For<IFileManagerService>(), NullLogger.Instance);
        var raised = new List<IReadOnlyList<Track>>();
        vm.TracksSaved += (_, updated) => raised.Add(updated);
        saved = raised;
        return vm;
    }

    [Test]
    public async Task Constructor_ShowsSharedValues_AndLeavesDifferingOnesEmptyWithAPlaceholder()
    {
        var vm = CreateViewModel(out _, out var dir, out _);
        try
        {
            await Assert.That(vm.IsMultiple).IsTrue();
            await Assert.That(vm.Album).IsEqualTo("Shared Album");
            await Assert.That(vm.Genre).IsEqualTo("Rock");
            await Assert.That(vm.YearText).IsEqualTo("2001");
            await Assert.That(vm.Title).IsNull();
            await Assert.That(vm.TitlePlaceholder).IsNotNull();
            await Assert.That(vm.Artist).IsNull();
            await Assert.That(vm.ArtistPlaceholder).IsNotNull();
            await Assert.That(vm.TrackNumberText).IsEqualTo("");
            await Assert.That(vm.TrackNumberPlaceholder).IsNotNull();
            await Assert.That(vm.AlbumPlaceholder).IsNull();
            await Assert.That(vm.IsDirty).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task EditingASharedField_WritesItToEveryTrack_AndKeepsTheirOwnTitles()
    {
        var vm = CreateViewModel(out var paths, out var dir, out var saved);
        try
        {
            vm.Genre = "Jazz";
            await vm.ApplyCommand.ExecuteAsync(null);

            var one = MusicLibraryScanner.ReadTrackIfExists(paths[0])!;
            var two = MusicLibraryScanner.ReadTrackIfExists(paths[1])!;
            await Assert.That(one.Genre).IsEqualTo("Jazz");
            await Assert.That(two.Genre).IsEqualTo("Jazz");
            await Assert.That(one.Title).IsEqualTo("First");
            await Assert.That(two.Title).IsEqualTo("Second");
            await Assert.That(one.TrackNumber).IsEqualTo(1);
            await Assert.That(two.Artist).IsEqualTo("Artist B");
            await Assert.That(saved.Count).IsEqualTo(1);
            await Assert.That(saved[0].Count).IsEqualTo(2);
            await Assert.That(vm.IsDirty).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task TypingIntoADifferingField_WritesItToEveryTrack()
    {
        var vm = CreateViewModel(out var paths, out var dir, out _);
        try
        {
            vm.Artist = "Everyone";
            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(paths[0])!.Artist).IsEqualTo("Everyone");
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(paths[1])!.Artist).IsEqualTo("Everyone");
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(paths[1])!.Title).IsEqualTo("Second");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ClearingASharedField_ClearsItOnEveryTrack()
    {
        var vm = CreateViewModel(out var paths, out var dir, out _);
        try
        {
            vm.Album = "";
            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(paths[0])!.Album).IsNull();
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(paths[1])!.Album).IsNull();
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(paths[0])!.Title).IsEqualTo("First");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task AfterApplying_ADifferingFieldStillShowsAsVarying()
    {
        var vm = CreateViewModel(out _, out var dir, out _);
        try
        {
            vm.Genre = "Jazz";
            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(vm.Title).IsNull();
            await Assert.That(vm.TitlePlaceholder).IsNotNull();
            await Assert.That(vm.Genre).IsEqualTo("Jazz");
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task SettingACover_EmbedsItInEveryTrack()
    {
        var vm = CreateViewModel(out var paths, out var dir, out _);
        try
        {
            var imagePath = Path.Combine(dir.FullName, "picked.jpg");
            File.WriteAllBytes(imagePath, Fixture("cover-red.jpg"));
            await vm.SetCoverFromFileAsync(imagePath);
            vm.SaveCoverAsSeparateFile = false;

            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(MusicLibraryScanner.LoadArtwork(paths[0])).IsEquivalentTo(Fixture("cover-red.jpg"));
            await Assert.That(MusicLibraryScanner.LoadArtwork(paths[1])).IsEquivalentTo(Fixture("cover-red.jpg"));
            await Assert.That(vm.CoverVaries).IsFalse();
            await Assert.That(vm.HasCover).IsTrue();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task SettingACoverAsSeparateFile_WritesOneCoverFileInTheFolder()
    {
        var vm = CreateViewModel(out var paths, out var dir, out _);
        try
        {
            var imagePath = Path.Combine(dir.FullName, "picked.jpg");
            File.WriteAllBytes(imagePath, Fixture("cover-red.jpg"));
            await vm.SetCoverFromFileAsync(imagePath);
            vm.SaveCoverAsSeparateFile = true;

            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(Directory.GetFiles(dir.FullName, "cover.*").Length).IsEqualTo(1);
            await Assert.That(MusicLibraryScanner.LoadArtworkWithSource(paths[1])!.IsSeparateFile).IsTrue();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task TracksWithDifferentCovers_ShowNoCoverButCanStillHaveItRemoved()
    {
        var vm = CreateViewModel(out var paths, out var dir, out _);
        try
        {
            TrackTagEditor.Write(paths[0], new TrackTagEdits("First", "Artist A", null, "Shared Album", "Rock", "c1", 2001, 1),
                new CoverArtEdit.Replace(Fixture("cover-red.jpg"), AsSeparateFile: false));
            var tracks = paths.Select(p => MusicLibraryScanner.ReadTrackIfExists(p)!).ToList();
            var mixed = new TrackPropertiesViewModel(tracks, Substitute.For<IFilePickerService>(), Substitute.For<IFileManagerService>(), NullLogger.Instance);

            await Assert.That(mixed.HasCover).IsFalse();
            await Assert.That(mixed.CoverVaries).IsTrue();
            await Assert.That(mixed.RemoveCoverCommand.CanExecute(null)).IsTrue();
            await Assert.That(mixed.IsDirty).IsFalse();

            mixed.RemoveCoverCommand.Execute(null);
            await Assert.That(mixed.IsDirty).IsTrue();
            await mixed.ApplyCommand.ExecuteAsync(null);

            await Assert.That(MusicLibraryScanner.LoadArtwork(paths[0])).IsNull();
            await Assert.That(mixed.CoverVaries).IsFalse();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task WhenOneTrackFailsToSave_TheOthersAreSavedAndTheFailureIsReported()
    {
        var vm = CreateViewModel(out var paths, out var dir, out var saved);
        try
        {
            vm.Genre = "Jazz";
            File.Delete(paths[1]);

            await vm.ApplyCommand.ExecuteAsync(null);

            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(paths[0])!.Genre).IsEqualTo("Jazz");
            await Assert.That(saved.Single().Select(t => t.FilePath)).IsEquivalentTo([paths[0]]);
            await Assert.That(vm.ErrorMessage).Contains("two.mp3");
            await Assert.That(vm.IsDirty).IsTrue();
            await Assert.That(vm.OkCommand.CanExecute(null)).IsTrue();
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task ReadOnlyInfo_ShowsTheSelectionSummaryCommonTypeAndFolder()
    {
        var vm = CreateViewModel(out var paths, out var dir, out _);
        try
        {
            var single = MusicLibraryScanner.ReadTrackIfExists(paths[0])!;

            await Assert.That(vm.SelectionSummary).IsNotEqualTo(single.DisplayName);
            await Assert.That(vm.FileTypeDisplay).IsEqualTo("MP3");
            await Assert.That(vm.Location).IsEqualTo(dir.FullName);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }

    [Test]
    public async Task TracksFromDifferentFolders_ShowAMultipleLocationsLabel()
    {
        var dirA = Directory.CreateTempSubdirectory("sharpselecta-multi-edit-tests-");
        var dirB = Directory.CreateTempSubdirectory("sharpselecta-multi-edit-tests-");
        try
        {
            var a = Path.Combine(dirA.FullName, "a.mp3");
            var b = Path.Combine(dirB.FullName, "b.flac");
            File.WriteAllBytes(a, Fixture("untagged-track.mp3"));
            File.WriteAllBytes(b, Fixture("untagged-track.flac"));
            var tracks = new[] { a, b }.Select(p => MusicLibraryScanner.ReadTrackIfExists(p)!).ToList();

            var vm = new TrackPropertiesViewModel(tracks, Substitute.For<IFilePickerService>(), Substitute.For<IFileManagerService>(), NullLogger.Instance);

            await Assert.That(vm.Location).IsNotEqualTo(dirA.FullName);
            await Assert.That(vm.Location).IsNotEqualTo(dirB.FullName);
            await Assert.That(vm.FileTypeDisplay).IsNotEqualTo("MP3");
            await Assert.That(vm.FileTypeDisplay).IsNotEqualTo("FLAC");
        }
        finally
        {
            dirA.Delete(recursive: true);
            dirB.Delete(recursive: true);
        }
    }

    [Test]
    public async Task Constructor_WithNoTracks_Throws()
    {
        await Assert.That(() => new TrackPropertiesViewModel(
            new List<Track>(), Substitute.For<IFilePickerService>(), Substitute.For<IFileManagerService>(), NullLogger.Instance))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task DiscNumber_DifferingAcrossTracks_StartsEmptyWithAPlaceholder_AndIsKeptPerTrackUnlessTyped()
    {
        var dir = Directory.CreateTempSubdirectory("sharpselecta-multi-edit-tests-");
        try
        {
            var paths = new[] { Path.Combine(dir.FullName, "one.mp3"), Path.Combine(dir.FullName, "two.mp3") };
            foreach (var (path, disc) in paths.Zip([1, 2]))
            {
                File.WriteAllBytes(path, Fixture("untagged-track.mp3"));
                TrackTagEditor.Write(path, new TrackTagEdits("T", "Same", null, "Album", null, null, 2001, 1, DiscNumber: disc));
            }

            var tracks = paths.Select(p => MusicLibraryScanner.ReadTrackIfExists(p)!).ToList();
            var vm = new TrackPropertiesViewModel(tracks, Substitute.For<IFilePickerService>(), Substitute.For<IFileManagerService>(), NullLogger.Instance);

            await Assert.That(vm.DiscNumberText).IsEqualTo("");
            await Assert.That(vm.DiscNumberPlaceholder).IsNotNull();

            vm.Genre = "Jazz"; // an unrelated edit must not flatten the discs
            await vm.ApplyCommand.ExecuteAsync(null);
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(paths[0])!.DiscNumber).IsEqualTo(1);
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(paths[1])!.DiscNumber).IsEqualTo(2);

            vm.DiscNumberText = "3";
            await vm.ApplyCommand.ExecuteAsync(null);
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(paths[0])!.DiscNumber).IsEqualTo(3);
            await Assert.That(MusicLibraryScanner.ReadTrackIfExists(paths[1])!.DiscNumber).IsEqualTo(3);
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
