using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SharpSelecta.App.Resources;
using SharpSelecta.App.ViewModels;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Views;

public partial class TrackPropertiesWindow : Window
{
    public TrackPropertiesWindow()
    {
        InitializeComponent();
        DragDrop.AddDragOverHandler(CoverBox, OnCoverDragOver);
        DragDrop.AddDropHandler(CoverBox, OnCoverDrop);
        DataContextChanged += (_, _) =>
        {
            if (DataContext is TrackPropertiesViewModel vm)
            {
                vm.CloseRequested += (_, _) => Close();
            }
        };
        // Closing the window stops a conversion that is still running.
        Closing += (_, _) => (DataContext as TrackPropertiesViewModel)?.Conversion?.CancelConversion();
    }

    public static Task ShowAsync(Control anchor, LibraryViewModel library, Track track) =>
        ShowAsync(anchor, library, [track]);

    public static Task ShowAsync(Control anchor, LibraryViewModel library, IReadOnlyList<Track> tracks)
    {
        // A missing-file placeholder (e.g. in a playlist) has nothing on disk to edit.
        var editable = tracks.Where(track => File.Exists(track.FilePath)).ToList();
        if (editable.Count == 0 || TopLevel.GetTopLevel(anchor) is not Window owner)
            return Task.CompletedTask;

        var window = new TrackPropertiesWindow { DataContext = library.CreateTrackProperties(editable) };
        return window.ShowDialog(owner);
    }

    private void OnCoverDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;

    private async void OnCoverDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not TrackPropertiesViewModel vm)
            return;

        var path = e.DataTransfer.TryGetFiles()?.Select(file => file.TryGetLocalPath()).FirstOrDefault(p => p is not null);
        if (path is not null)
        {
            await vm.SetCoverFromFileAsync(path);
        }
    }

    private async void OnEditCreditsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TrackPropertiesViewModel vm)
            return;

        if (await CreditsEditorWindow.ShowAsync(this, vm.CreateCreditsEditor()) is { } credits)
        {
            vm.ApplyCredits(credits);
        }
    }

    private async void OnFindCoverOnlineClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TrackPropertiesViewModel { CanSearchCoverOnline: true } vm)
            return;

        if (await AlbumArtSearchWindow.ShowAsync(this, vm.CreateAlbumArtSearch()) is { } cover)
        {
            vm.SetCover(cover);
        }
    }

    private async void OnRemoveCoverClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TrackPropertiesViewModel vm)
            return;

        // Removing also deletes the folder's cover file, which every track in the folder shares.
        if (vm.HasFolderCoverFile &&
            !await ConfirmationWindow.ShowAsync(this, Strings.RemoveCoverConfirmTitle, Strings.RemoveCoverConfirmMessage))
            return;

        vm.RemoveCoverCommand.Execute(null);
    }
}
