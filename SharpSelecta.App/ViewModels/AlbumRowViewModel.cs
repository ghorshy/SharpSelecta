using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SharpSelecta.App.ViewModels;

// One visual row of the album grid: the tiles that belong on it, plus which one (if any) is
// currently expanded. Rows are computed data, not something WrapPanel can produce on its own —
// see AlbumGridViewModel for why "expand a row in place" needs rows to be modeled explicitly.
public sealed partial class AlbumRowViewModel(IReadOnlyList<AlbumViewModel> tiles) : ObservableObject
{
    public IReadOnlyList<AlbumViewModel> Tiles { get; } = tiles;

    [ObservableProperty]
    private AlbumViewModel? expandedAlbum;
}
