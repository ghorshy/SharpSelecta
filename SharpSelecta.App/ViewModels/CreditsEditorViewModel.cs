using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

// A role as the dropdown shows it.
public sealed record RoleOption(CreditRole Role, string DisplayName);

public sealed partial class CreditEntryViewModel(string name, RoleOption role) : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = name;

    [ObservableProperty]
    public partial RoleOption Role { get; set; } = role;
}

// Edits the people credited on a track: each entry is a name plus a role, renamable and removable in
// place, with a name + role row to add more. Result is the normalized list (trimmed, no blanks, no
// case-insensitive duplicates within a role), or null if the dialog was cancelled.
public sealed partial class CreditsEditorViewModel : ViewModelBase
{
    public CreditsEditorViewModel(IEnumerable<CreditEntry> credits)
    {
        Roles =
        [
            new(CreditRole.Artist, Strings.ColumnArtist),
            new(CreditRole.Remixer, Strings.RoleRemixer),
            new(CreditRole.Composer, Strings.RoleComposer),
            new(CreditRole.Conductor, Strings.RoleConductor),
            new(CreditRole.Lyricist, Strings.RoleLyricist),
        ];
        NewCreditRole = Roles[0];

        foreach (var credit in credits)
        {
            Credits.Add(new CreditEntryViewModel(credit.Name, RoleOptionFor(credit.Role)));
        }
    }

    public IReadOnlyList<RoleOption> Roles { get; }

    public ObservableCollection<CreditEntryViewModel> Credits { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string NewCreditName { get; set; } = "";

    [ObservableProperty]
    public partial RoleOption NewCreditRole { get; set; }

    public IReadOnlyList<CreditEntry> Result =>
    [
        .. Enum.GetValues<CreditRole>().SelectMany(role =>
            ArtistList.Split(ArtistList.Join(Credits.Where(c => c.Role.Role == role).Select(c => c.Name)))
                .Select(name => new CreditEntry(role, name))),
    ];

    // Raised with the final list on OK and with null on Cancel.
    public event EventHandler<IReadOnlyList<CreditEntry>?>? CloseRequested;

    private RoleOption RoleOptionFor(CreditRole role) => Roles.First(option => option.Role == role);

    private bool CanAdd() => !string.IsNullOrWhiteSpace(NewCreditName);

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        // A pasted "A;B" is two people, same as it would be in the tag.
        foreach (var name in ArtistList.Split(NewCreditName))
        {
            var alreadyThere = Credits.Any(c => c.Role == NewCreditRole
                && string.Equals(c.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
            if (!alreadyThere)
            {
                Credits.Add(new CreditEntryViewModel(name, NewCreditRole));
            }
        }

        NewCreditName = "";
    }

    // Drag-reorder: the dragged entry lands just above the one it was dropped on (or just below it, for
    // a drop on its lower half), matching the line the window draws.
    public void Move(CreditEntryViewModel entry, CreditEntryViewModel target, bool insertAfter = false)
    {
        var from = Credits.IndexOf(entry);
        var targetIndex = Credits.IndexOf(target);
        if (from < 0 || targetIndex < 0 || entry == target)
            return;

        var slot = targetIndex + (insertAfter ? 1 : 0);
        var newIndex = from < slot ? slot - 1 : slot;
        if (newIndex != from)
        {
            Credits.Move(from, newIndex);
        }
    }

    [RelayCommand]
    private void Remove(CreditEntryViewModel entry) => Credits.Remove(entry);

    [RelayCommand]
    private void Ok()
    {
        // Whatever is still typed in the "add" box counts - clicking OK shouldn't silently drop it.
        if (CanAdd())
        {
            Add();
        }

        CloseRequested?.Invoke(this, Result);
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, null);
}
