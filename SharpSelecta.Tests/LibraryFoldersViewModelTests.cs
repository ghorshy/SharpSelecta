using NSubstitute;
using SharpSelecta.App.Services;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class LibraryFoldersViewModelTests
{
    private sealed class Harness : IDisposable
    {
        private readonly TempDirectory _temp = new();

        public IFilePickerService FilePicker { get; } = Substitute.For<IFilePickerService>();

        public SettingsStore Settings { get; }

        public int RescanCount { get; private set; }

        public LibraryFoldersViewModel Folders { get; }

        public Harness()
        {
            Settings = new SettingsStore(_temp.NewSettingsPath());
            Folders = new LibraryFoldersViewModel(FilePicker, Settings, () =>
            {
                RescanCount++;
                return Task.CompletedTask;
            });
        }

        public void Dispose() => _temp.Dispose();
    }

    [Test]
    public async Task AddFolderCommand_AddsSavesAndReconciles()
    {
        using var h = new Harness();
        h.FilePicker.PickLibraryFolderAsync().Returns("/music");

        await h.Folders.AddFolderCommand.ExecuteAsync(null);

        await Assert.That(h.Folders.Paths).IsEquivalentTo(["/music"]);
        await Assert.That(h.Folders.HasFolders).IsTrue();
        await Assert.That(h.Settings.Current.LibraryFolderPaths!).IsEquivalentTo(["/music"]);
        await Assert.That(h.RescanCount).IsEqualTo(1);
    }

    [Test]
    public async Task AddFolderCommand_WhenAlreadyAddedOrCancelled_ChangesNothing()
    {
        using var h = new Harness();
        h.Folders.Restore(["/music"]);

        h.FilePicker.PickLibraryFolderAsync().Returns("/music");
        await h.Folders.AddFolderCommand.ExecuteAsync(null);
        h.FilePicker.PickLibraryFolderAsync().Returns((string?)null);
        await h.Folders.AddFolderCommand.ExecuteAsync(null);

        await Assert.That(h.Folders.Paths).IsEquivalentTo(["/music"]);
        await Assert.That(h.RescanCount).IsEqualTo(0);
    }

    [Test]
    public async Task Restore_FillsPathsAndPendingPaths_WithoutSavingOrReconciling()
    {
        using var h = new Harness();

        h.Folders.Restore(["/a", "/b"]);

        await Assert.That(h.Folders.Paths).IsEquivalentTo(["/a", "/b"]);
        await Assert.That(h.Folders.PendingPaths).IsEquivalentTo(["/a", "/b"]);
        await Assert.That(h.Folders.HasPendingChanges).IsFalse();
        await Assert.That(h.Settings.Current.LibraryFolderPaths).IsNull();
        await Assert.That(h.RescanCount).IsEqualTo(0);
    }

    [Test]
    public async Task PendingChanges_AreAppliedOnlyOnApply()
    {
        using var h = new Harness();
        h.Folders.Restore(["/a", "/b"]);
        h.FilePicker.PickLibraryFolderAsync().Returns("/c");

        h.Folders.RemovePendingFolderCommand.Execute("/a");
        await h.Folders.AddPendingFolderCommand.ExecuteAsync(null);

        await Assert.That(h.Folders.Paths).IsEquivalentTo(["/a", "/b"]);
        await Assert.That(h.Folders.HasPendingChanges).IsTrue();

        await h.Folders.ApplyPendingChangesCommand.ExecuteAsync(null);

        await Assert.That(h.Folders.Paths).IsEquivalentTo(["/b", "/c"]);
        await Assert.That(h.Folders.HasPendingChanges).IsFalse();
        await Assert.That(h.Settings.Current.LibraryFolderPaths!).IsEquivalentTo(["/b", "/c"]);
        await Assert.That(h.RescanCount).IsEqualTo(1);
    }

    [Test]
    public async Task ApplyPendingChangesCommand_WithNoChanges_DoesNotReconcile()
    {
        using var h = new Harness();
        h.Folders.Restore(["/a"]);

        await h.Folders.ApplyPendingChangesCommand.ExecuteAsync(null);

        await Assert.That(h.RescanCount).IsEqualTo(0);
    }

    [Test]
    public async Task CancelPendingChangesCommand_DiscardsPendingChanges()
    {
        using var h = new Harness();
        h.Folders.Restore(["/a"]);
        h.Folders.RemovePendingFolderCommand.Execute("/a");
        await Assert.That(h.Folders.HasPendingFolders).IsFalse();

        h.Folders.CancelPendingChangesCommand.Execute(null);

        await Assert.That(h.Folders.PendingPaths).IsEquivalentTo(["/a"]);
        await Assert.That(h.Folders.HasPendingChanges).IsFalse();
    }

    [Test]
    public async Task HasPendingChanges_RaisesPropertyChanged()
    {
        using var h = new Harness();
        h.Folders.Restore(["/a"]);
        var raised = new List<string?>();
        h.Folders.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        h.Folders.RemovePendingFolderCommand.Execute("/a");

        await Assert.That(raised).Contains(nameof(LibraryFoldersViewModel.HasPendingChanges));
    }
}
