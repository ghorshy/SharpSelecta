using System.Collections.ObjectModel;
using System.Linq;
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

    [ObservableProperty]
    private ShortcutRowViewModel? recordingRow;

    // Everything here is rebound and persisted immediately (like the other settings categories),
    // so Apply has nothing to stage - it's kept enabled rather than permanently greyed out.
    public bool HasPendingChanges => true;

    public ICommand ApplyCommand { get; } = new RelayCommand(() => { });

    public ICommand CancelCommand { get; } = new RelayCommand(() => { });

    public KeyboardShortcutsViewModel(ShortcutSettingsService shortcutSettings)
    {
        _shortcutSettings = shortcutSettings;
        Rows = new ObservableCollection<ShortcutRowViewModel>(ShortcutRegistry.All
            .Select(definition => new ShortcutRowViewModel(definition, shortcutSettings.GetEffectiveGesture(definition.Id))));
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

        var conflict = Rows.FirstOrDefault(r => r != row && r.Gesture == formattedGesture);
        row.ConflictWarning = conflict is { } c ? Strings.ShortcutConflict(c.Description) : null;

        _shortcutSettings.SetOverride(row.Id, formattedGesture);
    }

    public void ResetToDefault(ShortcutRowViewModel row)
    {
        _shortcutSettings.ResetOverride(row.Id);
        row.Gesture = row.DefaultGesture;
        row.ConflictWarning = null;
    }
}
