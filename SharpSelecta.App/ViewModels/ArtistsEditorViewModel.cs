using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.ViewModels;

public sealed partial class ArtistEntryViewModel(string name) : ObservableObject
{
    [ObservableProperty]
    public partial string Name { get; set; } = name;
}

// Edits the list of artists of one track: rename in place, remove, add. Result is the normalized
// list (trimmed, no blanks, no case-insensitive duplicates), or null if the dialog was cancelled.
public sealed partial class ArtistsEditorViewModel : ViewModelBase
{
    public ArtistsEditorViewModel(IEnumerable<string> artists)
    {
        foreach (var artist in artists)
        {
            Artists.Add(new ArtistEntryViewModel(artist));
        }
    }

    public ObservableCollection<ArtistEntryViewModel> Artists { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddCommand))]
    public partial string NewArtistName { get; set; } = "";

    public IReadOnlyList<string> Result => ArtistList.Split(ArtistList.Join(Artists.Select(a => a.Name)));

    // Raised with the final list on OK and with null on Cancel.
    public event EventHandler<IReadOnlyList<string>?>? CloseRequested;

    private bool CanAdd() => !string.IsNullOrWhiteSpace(NewArtistName);

    [RelayCommand(CanExecute = nameof(CanAdd))]
    private void Add()
    {
        // A pasted "A;B" is two artists, same as it would be in the tag.
        foreach (var name in ArtistList.Split(NewArtistName))
        {
            if (!Artists.Any(a => string.Equals(a.Name.Trim(), name, StringComparison.OrdinalIgnoreCase)))
            {
                Artists.Add(new ArtistEntryViewModel(name));
            }
        }

        NewArtistName = "";
    }

    [RelayCommand]
    private void Remove(ArtistEntryViewModel entry) => Artists.Remove(entry);

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
