using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.App.ViewModels;

public enum AlbumArtTileState
{
    Searching,
    Found,
    NotFound,
    Failed,
    NeedsSetup,
}

// One service's tile: what it found for the album, or why it found nothing.
public sealed partial class AlbumArtTileViewModel(IAlbumArtProvider provider) : ObservableObject
{
    public IAlbumArtProvider Provider { get; } = provider;

    public string Name => Provider.Name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSearching), nameof(HasImage), nameof(NeedsSetup), nameof(ShowEnterKey), nameof(StatusText))]
    public partial AlbumArtTileState State { get; set; } = AlbumArtTileState.Searching;

    [ObservableProperty]
    public partial byte[]? Image { get; set; }

    // e.g. "1500×1500" once found.
    [ObservableProperty]
    public partial string? SizeText { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    public bool IsSearching => State == AlbumArtTileState.Searching;

    public bool HasImage => State == AlbumArtTileState.Found;

    public bool NeedsSetup => State == AlbumArtTileState.NeedsSetup;

    // The "Enter key..." button: while a key is missing, and again after a failed search if the key can be
    // changed (a rejected key makes the search fail).
    public bool ShowEnterKey => NeedsSetup || (State == AlbumArtTileState.Failed && Provider.CanBeConfigured);

    // Shown instead of an image: why there isn't one (null once found - the size is shown then).
    public string? StatusText => State switch
    {
        AlbumArtTileState.Searching => Strings.AlbumArtSearching,
        AlbumArtTileState.NotFound => Strings.AlbumArtNotFound,
        AlbumArtTileState.Failed => Strings.AlbumArtFailed,
        AlbumArtTileState.NeedsSetup => Strings.AlbumArtNeedsKey,
        _ => null,
    };
}

// Searches every provider for one album at once and lets the user pick which result to use.
public sealed partial class AlbumArtSearchViewModel : ViewModelBase, IDisposable
{
    private readonly CancellationTokenSource _cancellation = new();

    public AlbumArtSearchViewModel(AlbumArtQuery query, IEnumerable<IAlbumArtProvider> providers)
    {
        Query = query;
        Tiles = [.. providers.Select(provider => new AlbumArtTileViewModel(provider))];
    }

    public AlbumArtQuery Query { get; }

    public string QueryText => Strings.AlbumArtQuery(Query.Artist, Query.Album);

    public IReadOnlyList<AlbumArtTileViewModel> Tiles { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewBytes), nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(OkCommand))]
    public partial AlbumArtTileViewModel? SelectedTile { get; set; }

    public byte[]? PreviewBytes => SelectedTile?.Image;

    public bool HasSelection => SelectedTile is not null;

    // Raised with the chosen cover on OK and with null on Cancel.
    public event EventHandler<byte[]?>? CloseRequested;

    public async Task SearchAsync() => await Task.WhenAll(Tiles.Select(SearchTileAsync));

    public void Select(AlbumArtTileViewModel tile)
    {
        if (!tile.HasImage)
            return;

        foreach (var other in Tiles)
        {
            other.IsSelected = other == tile;
        }

        SelectedTile = tile;
    }

    // Used by a tile that needs an API key: store it and search that service again.
    public async Task ConfigureAsync(AlbumArtTileViewModel tile, string value)
    {
        tile.Provider.Configure(value);
        await SearchTileAsync(tile);
    }

    private async Task SearchTileAsync(AlbumArtTileViewModel tile)
    {
        if (!tile.Provider.IsConfigured)
        {
            tile.State = AlbumArtTileState.NeedsSetup;
            return;
        }

        tile.State = AlbumArtTileState.Searching;
        try
        {
            var found = await tile.Provider.FindAsync(Query, _cancellation.Token);
            if (found is null)
            {
                tile.State = AlbumArtTileState.NotFound;
                return;
            }

            tile.Image = found.Image;
            tile.SizeText = $"{found.Width}×{found.Height}";
            tile.State = AlbumArtTileState.Found;

            // The first cover to arrive is selected so OK works right away; clicking another tile overrides it.
            if (SelectedTile is null)
            {
                Select(tile);
            }
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
            // The window was closed.
        }
        catch (Exception)
        {
            tile.State = AlbumArtTileState.Failed;
        }
    }

    private bool CanOk() => SelectedTile?.Image is not null;

    [RelayCommand(CanExecute = nameof(CanOk))]
    private void Ok() => CloseRequested?.Invoke(this, SelectedTile?.Image);

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, null);

    public void Dispose()
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
    }
}
