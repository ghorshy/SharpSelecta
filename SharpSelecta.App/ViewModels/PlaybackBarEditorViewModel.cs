using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

// The bar's tiles and the Hidden pool in Settings → Interface. Every change is handed to Interface
// settings as a new layout, which the main window's bar previews.
public sealed partial class PlaybackBarEditorViewModel : ViewModelBase
{
    private readonly Action<PlaybackBarLayout> _changed;
    private readonly Dictionary<PlaybackBarComponent, PlaybackBarTile> _tiles =
        PlaybackBarLayout.AllComponents.ToDictionary(c => c, PlaybackBarTile.For);

    // Set while the controls are loaded from a layout (the constructor included); that isn't an edit.
    private bool _loading = true;

    public ObservableCollection<PlaybackBarTile> BarTiles { get; } = [];

    public ObservableCollection<PlaybackBarTile> HiddenTiles { get; } = [];

    public IReadOnlyList<SeekBarWidthOption> SeekBarWidthOptions { get; } =
    [
        new(SeekBarWidth.Fill, Strings.SeekBarWidthFill),
        new(SeekBarWidth.Fixed, Strings.SeekBarWidthFixed),
    ];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSeekBarFixed))]
    public partial SeekBarWidthOption SelectedSeekBarWidthOption { get; set; }

    public bool IsSeekBarFixed => SelectedSeekBarWidthOption.Width == SeekBarWidth.Fixed;

    public double MinSeekBarPercent => PlaybackBarLayout.MinSeekBarPercent;

    public double MaxSeekBarPercent => PlaybackBarLayout.MaxSeekBarPercent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SeekBarPercentText))]
    public partial double SeekBarPercent { get; set; }

    public string SeekBarPercentText => Strings.Percent((int)Math.Round(SeekBarPercent));

    public IReadOnlyList<PlaybackBarAlignmentOption> AlignmentOptions { get; } =
    [
        new(PlaybackBarAlignment.Left, Strings.AlignLeft),
        new(PlaybackBarAlignment.Center, Strings.AlignCenter),
        new(PlaybackBarAlignment.Right, Strings.AlignRight),
    ];

    [ObservableProperty]
    public partial PlaybackBarAlignmentOption SelectedAlignmentOption { get; set; }

    [ObservableProperty]
    public partial bool ShowTitle { get; set; }

    public IReadOnlyList<TitleAlignmentOption> TitleAlignmentOptions { get; } =
    [
        new(TitleAlignment.Left, Strings.AlignLeft),
        new(TitleAlignment.Center, Strings.AlignCenter),
    ];

    [ObservableProperty]
    public partial TitleAlignmentOption SelectedTitleAlignmentOption { get; set; }

    public PlaybackBarLayout Layout => new()
    {
        Components = BarTiles.Select(t => t.Component).ToList(),
        SeekBarWidth = SelectedSeekBarWidthOption.Width,
        SeekBarPercent = SeekBarPercent,
        Alignment = SelectedAlignmentOption.Alignment,
        ShowTitle = ShowTitle,
        TitleAlignment = SelectedTitleAlignmentOption.Alignment,
    };

    public PlaybackBarEditorViewModel(PlaybackBarLayout layout, Action<PlaybackBarLayout> changed)
    {
        _changed = changed;
        SelectedSeekBarWidthOption = SeekBarWidthOptions[0];
        SelectedAlignmentOption = AlignmentOptions[1];
        SelectedTitleAlignmentOption = TitleAlignmentOptions[0];
        Load(layout);
    }

    public void Load(PlaybackBarLayout layout)
    {
        _loading = true;
        try
        {
            BarTiles.Clear();
            foreach (var component in layout.Components)
            {
                BarTiles.Add(_tiles[component]);
            }

            HiddenTiles.Clear();
            foreach (var component in layout.HiddenComponents)
            {
                HiddenTiles.Add(_tiles[component]);
            }

            SelectedSeekBarWidthOption = SeekBarWidthOptions.First(o => o.Width == layout.SeekBarWidth);
            SeekBarPercent = layout.SeekBarPercent;
            SelectedAlignmentOption = AlignmentOptions.First(o => o.Alignment == layout.Alignment);
            ShowTitle = layout.ShowTitle;
            SelectedTitleAlignmentOption = TitleAlignmentOptions.First(o => o.Alignment == layout.TitleAlignment);
        }
        finally
        {
            _loading = false;
        }

        RestoreDefaultCommand.NotifyCanExecuteChanged();
    }

    // Puts the component on the bar before the tile now at `index` (BarTiles.Count = at the end),
    // taking it from wherever it is.
    public bool Place(PlaybackBarComponent component, int index)
    {
        var tile = _tiles[component];
        var from = BarTiles.IndexOf(tile);
        if (from >= 0)
        {
            var to = Math.Clamp(from < index ? index - 1 : index, 0, BarTiles.Count - 1);
            if (to == from)
                return false;

            BarTiles.Move(from, to);
        }
        else
        {
            HiddenTiles.Remove(tile);
            BarTiles.Insert(Math.Clamp(index, 0, BarTiles.Count), tile);
        }

        Changed();
        return true;
    }

    // Ctrl+←/→: one place left or right, nothing at the edges.
    public bool MoveBy(PlaybackBarComponent component, int offset)
    {
        var from = BarTiles.IndexOf(_tiles[component]);
        var to = from + offset;
        if (from < 0 || to < 0 || to >= BarTiles.Count)
            return false;

        BarTiles.Move(from, to);
        Changed();
        return true;
    }

    public bool Hide(PlaybackBarComponent component)
    {
        var tile = _tiles[component];
        if (tile.IsLocked || !BarTiles.Remove(tile))
            return false;

        // The pool keeps the components in their default order.
        var index = HiddenTiles.Count(t => t.Component < component);
        HiddenTiles.Insert(index, tile);
        Changed();
        return true;
    }

    // Insert: back on the bar, at the end.
    public bool Show(PlaybackBarComponent component) =>
        HiddenTiles.Contains(_tiles[component]) && Place(component, BarTiles.Count);

    [RelayCommand(CanExecute = nameof(CanRestoreDefault))]
    private void RestoreDefault()
    {
        Load(PlaybackBarLayout.Default);
        Changed();
    }

    private bool CanRestoreDefault() => Layout != PlaybackBarLayout.Default;

    partial void OnSelectedSeekBarWidthOptionChanged(SeekBarWidthOption value) => Changed();

    partial void OnSeekBarPercentChanged(double value) => Changed();

    partial void OnSelectedAlignmentOptionChanged(PlaybackBarAlignmentOption value) => Changed();

    partial void OnShowTitleChanged(bool value) => Changed();

    partial void OnSelectedTitleAlignmentOptionChanged(TitleAlignmentOption value) => Changed();

    private void Changed()
    {
        if (_loading)
            return;

        _changed(Layout);
        RestoreDefaultCommand.NotifyCanExecuteChanged();
    }
}

public sealed record PlaybackBarTile(PlaybackBarComponent Component, string Name, string? Label, string? IconKey)
{
    public bool IsLocked => !PlaybackBarLayout.CanHide(Component);

    public bool IsSeekBar => Component == PlaybackBarComponent.SeekBar;

    public string ToolTip => IsLocked ? Strings.PlaybackBarLockedTile(Name) : Name;

    // Icon tiles for the components that are icons on the bar, text tiles for the rest.
    public static PlaybackBarTile For(PlaybackBarComponent component) => component switch
    {
        PlaybackBarComponent.Previous => new(component, Strings.Previous, null, "Icon.PlayerTrackPrev"),
        PlaybackBarComponent.PlayPause => new(component, Strings.PlayPause, null, "Icon.PlayerPlay"),
        PlaybackBarComponent.Next => new(component, Strings.Next, null, "Icon.PlayerTrackNext"),
        PlaybackBarComponent.Elapsed => new(component, Strings.ElapsedTime, Strings.ElapsedTileLabel, null),
        PlaybackBarComponent.SeekBar => new(component, Strings.SeekBar, Strings.SeekBar, null),
        PlaybackBarComponent.Length => new(component, Strings.TrackLength, Strings.LengthTileLabel, null),
        PlaybackBarComponent.Volume => new(component, Strings.Volume, Strings.Volume, null),
        PlaybackBarComponent.Repeat => new(component, Strings.Repeat, null, "Icon.Repeat"),
        PlaybackBarComponent.Equalizer => new(component, Strings.Equalizer, null, "Icon.Equalizer"),
        PlaybackBarComponent.Settings => new(component, Strings.Settings, null, "Icon.Settings"),
        _ => throw new ArgumentOutOfRangeException(nameof(component), component, null),
    };
}

public sealed record SeekBarWidthOption(SeekBarWidth Width, string DisplayName);

public sealed record PlaybackBarAlignmentOption(PlaybackBarAlignment Alignment, string DisplayName);

public sealed record TitleAlignmentOption(TitleAlignment Alignment, string DisplayName);
