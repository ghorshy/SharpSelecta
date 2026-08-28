using CommunityToolkit.Mvvm.ComponentModel;
using SharpSelecta.App.Resources;
using SharpSelecta.App.Shortcuts;

namespace SharpSelecta.App.ViewModels;

public sealed partial class ShortcutRowViewModel : ViewModelBase
{
    private readonly ShortcutDefinition _definition;

    public string Id => _definition.Id;

    public string Description => _definition.Description();

    public string DefaultGesture => _definition.DefaultGesture;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayGesture))]
    private string gesture;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayGesture))]
    private bool isRecording;

    // Updated live as modifiers are held during recording, so the button grows "Ctrl+Shift+..."
    // as you press keys, before the completing (non-modifier) key finalizes the gesture.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayGesture))]
    private string? recordingPreview;

    [ObservableProperty]
    private string? conflictWarning;

    public string DisplayGesture => IsRecording
        ? (RecordingPreview ?? Strings.PressAKeyCombination)
        : (string.IsNullOrEmpty(Gesture) ? Strings.ShortcutNotSet : Gesture);

    public ShortcutRowViewModel(ShortcutDefinition definition, string initialGesture)
    {
        _definition = definition;
        gesture = initialGesture;
    }
}
