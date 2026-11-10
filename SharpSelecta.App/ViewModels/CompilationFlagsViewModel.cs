using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed partial class CompilationFlagsViewModel(IReadOnlyList<Track> tracks, ILogger logger) : ViewModelBase
{
    private bool _hasLoaded;

    public ObservableCollection<InconsistentAlbumViewModel> Albums { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowNoneFound))]
    [NotifyCanExecuteChangedFor(nameof(FixSelectedCommand))]
    public partial bool IsBusy { get; private set; }

    [ObservableProperty]
    public partial bool HasChecked { get; private set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; private set; }

    public bool ShowNoneFound => !IsBusy && _hasLoaded && ErrorMessage is null && Albums.Count == 0;

    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var found = await Task.Run(() => CompilationFlagConsistency.FindInconsistent(
                tracks.Where(t => File.Exists(t.FilePath)), IsSetOrLogged));
            foreach (var album in found
                         .OrderBy(a => a.Album, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(a => a.AlbumArtist, StringComparer.OrdinalIgnoreCase))
            {
                var row = new InconsistentAlbumViewModel(album);
                foreach (var file in row.FlaggedFiles)
                {
                    file.PropertyChanged += (_, _) => RefreshChecked();
                }

                Albums.Add(row);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to check compilation flags");
            ErrorMessage = Strings.FailedToCheckCompilationFlags(ex.Message);
        }
        finally
        {
            _hasLoaded = true;
            IsBusy = false;
            RefreshChecked();
        }
    }

    // One unreadable file shouldn't hide every other album's result.
    private bool IsSetOrLogged(string filePath)
    {
        try
        {
            return CompilationFlag.IsSet(filePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not read the compilation flag of {FilePath}", filePath);
            return false;
        }
    }

    private bool CanFixSelected() => !IsBusy && HasChecked;

    [RelayCommand(CanExecute = nameof(CanFixSelected))]
    private async Task FixSelectedAsync()
    {
        var selected = Albums.SelectMany(a => a.FlaggedFiles).Where(f => f.IsChecked).ToList();
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var failures = await Task.Run(() =>
            {
                var failed = new List<FlaggedFileViewModel>();
                foreach (var file in selected)
                {
                    try
                    {
                        TrackTagEditor.WriteCompilationFlag(file.Track.FilePath, false);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to clear the compilation flag of {FilePath}", file.Track.FilePath);
                        failed.Add(file);
                    }
                }

                return failed;
            });

            var cleared = selected.Except(failures).ToHashSet();
            foreach (var album in Albums.ToList())
            {
                foreach (var file in album.FlaggedFiles.Where(cleared.Contains).ToList())
                {
                    album.FlaggedFiles.Remove(file);
                }

                if (album.FlaggedFiles.Count == 0)
                {
                    Albums.Remove(album);
                }
            }

            if (failures.Count > 0)
            {
                ErrorMessage = Strings.FailedToClearCompilationFlag(string.Join(", ", failures.Select(f => f.FileName)));
            }
        }
        finally
        {
            IsBusy = false;
            RefreshChecked();
        }
    }

    private void RefreshChecked()
    {
        HasChecked = Albums.SelectMany(a => a.FlaggedFiles).Any(f => f.IsChecked);
        OnPropertyChanged(nameof(ShowNoneFound));
        FixSelectedCommand.NotifyCanExecuteChanged();
    }
}

public sealed class InconsistentAlbumViewModel(InconsistentCompilationAlbum album)
{
    public string Heading { get; } =
        Strings.CompilationFlagsAlbum(album.Album.Length > 0 ? album.Album : Strings.UnknownAlbum, album.AlbumArtist);

    public ObservableCollection<FlaggedFileViewModel> FlaggedFiles { get; } =
        [.. album.Flagged.OrderBy(t => t.FilePath, StringComparer.OrdinalIgnoreCase).Select(t => new FlaggedFileViewModel(t))];

    public IReadOnlyList<string> UnflaggedFileNames { get; } =
        [.. album.Unflagged.Select(t => Path.GetFileName(t.FilePath)).Order(StringComparer.OrdinalIgnoreCase)];
}

public sealed partial class FlaggedFileViewModel(Track track) : ObservableObject
{
    public Track Track { get; } = track;

    public string FileName { get; } = Path.GetFileName(track.FilePath);

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = true;
}
