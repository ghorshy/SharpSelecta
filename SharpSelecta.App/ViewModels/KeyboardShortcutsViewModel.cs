using System.Collections.Generic;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;

namespace SharpSelecta.App.ViewModels;

public sealed record ShortcutEntry(string Shortcut, string Description);

public sealed class KeyboardShortcutsViewModel : ISettingsCategoryViewModel
{
    public IReadOnlyList<ShortcutEntry> Shortcuts { get; } =
    [
        new("Ctrl+F", Strings.ShortcutSearchLibrary),
        new("Ctrl+Scroll", Strings.ShortcutResizeAlbumTiles),
        new("←/→", Strings.ShortcutSeek),
    ];

    public bool HasPendingChanges => false;

    public ICommand ApplyCommand { get; } = new RelayCommand(() => { });

    public ICommand CancelCommand { get; } = new RelayCommand(() => { });
}
