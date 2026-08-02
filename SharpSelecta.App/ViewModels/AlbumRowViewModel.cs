using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SharpSelecta.App.ViewModels;

public sealed partial class AlbumRowViewModel(IReadOnlyList<AlbumViewModel> tiles) : ObservableObject
{
    public IReadOnlyList<AlbumViewModel> Tiles { get; } = tiles;

    [ObservableProperty]
    private AlbumViewModel? expandedAlbum;
}
