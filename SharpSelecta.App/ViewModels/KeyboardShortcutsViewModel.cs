using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Services;
using SharpSelecta.App.Shortcuts;

namespace SharpSelecta.App.ViewModels;

public sealed partial class KeyboardShortcutsViewModel : ViewModelBase, ISettingsCategoryViewModel
{
    private readonly ShortcutSettingsService _shortcutSettings;

    public ObservableCollection<ShortcutRowViewModel> Rows { get; }

    public IReadOnlyList<ShortcutSectionViewModel> Sections { get; }

    [ObservableProperty] public partial ShortcutRowViewModel? RecordingRow { get; set; }

    public string DisplayName => Strings.SettingsCategoryKeyboardShortcuts;

    // Everything here is rebound and persisted immediately (like the other settings categories),
    // so Apply has nothing to stage - it's kept enabled rather than permanently greyed out.
    public bool HasPendingChanges => true;

    public ICommand ApplyCommand { get; } = new RelayCommand(() => { });

    public ICommand CancelCommand { get; } = new RelayCommand(() => { });

    public KeyboardShortcutsViewModel(ShortcutSettingsService shortcutSettings)
    {
        _shortcutSettings = shortcutSettings;
        Rows =
        [
            .. ShortcutRegistry.All
                .Select(definition =>
                    new ShortcutRowViewModel(definition, shortcutSettings.GetEffectiveGesture(definition.Id)))
        ];
        RefreshConflictWarnings();
        Sections =
        [
            .. Rows.GroupBy(row => row.Section)
                .Select(group => new ShortcutSectionViewModel(group.Key.Title(), [.. group]))
        ];
    }

    public void StartRecording(ShortcutRowViewModel row)
    {
        if (RecordingRow is { } previous)
            previous.IsRecording = false;

        row.RecordingPreview = null;
        RecordingRow = row;
        row.IsRecording = true;
    }

    public void CancelRecording()
    {
        if (RecordingRow is { } row)
            row.IsRecording = false;

        RecordingRow = null;
    }

    public void CompleteRecording(string formattedGesture)
    {
        if (RecordingRow is not { } row)
            return;

        row.Gesture = formattedGesture;
        row.IsRecording = false;
        RecordingRow = null;

        _shortcutSettings.SetOverride(row.Id, formattedGesture);
        RefreshConflictWarnings();
    }

    public void ResetToDefault(ShortcutRowViewModel row)
    {
        if (RecordingRow == row)
            CancelRecording();

        _shortcutSettings.ResetOverride(row.Id);
        row.Gesture = row.DefaultGesture;
        RefreshConflictWarnings();
    }

    public void ClearShortcut(ShortcutRowViewModel row)
    {
        if (RecordingRow == row)
            CancelRecording();

        _shortcutSettings.SetOverride(row.Id, string.Empty);
        row.Gesture = string.Empty;
        RefreshConflictWarnings();
    }

    // Recomputed for every row, since rebinding one side of a conflict also resolves it for the other.
    private void RefreshConflictWarnings()
    {
        foreach (var row in Rows)
        {
            var conflict = row.Gesture.Length == 0
                ? null
                : Rows.FirstOrDefault(r => r != row && r.Gesture == row.Gesture && r.Scope.Overlaps(row.Scope));
            row.ConflictWarning = conflict != null ? Strings.ShortcutConflict(conflict.Description) : null;
        }
    }
}