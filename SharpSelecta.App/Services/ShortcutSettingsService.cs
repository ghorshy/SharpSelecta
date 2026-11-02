using SharpSelecta.App.Shortcuts;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services;

public sealed class ShortcutSettingsService
{
    private readonly SettingsStore _settings;
    private readonly Dictionary<string, string> _overrides;

    public event EventHandler? ShortcutsChanged;

    public ShortcutSettingsService(SettingsStore settings)
    {
        _settings = settings;
        _overrides = new Dictionary<string, string>(settings.Current.ShortcutOverrides ?? new Dictionary<string, string>());
    }

    public string GetEffectiveGesture(string shortcutId) =>
        _overrides.TryGetValue(shortcutId, out var gesture)
            ? gesture
            : ShortcutRegistry.All.First(s => s.Id == shortcutId).DefaultGesture;

    public void SetOverride(string shortcutId, string gesture)
    {
        _overrides[shortcutId] = gesture;
        Persist();
    }

    public void ResetOverride(string shortcutId)
    {
        if (_overrides.Remove(shortcutId))
            Persist();
    }

    private void Persist()
    {
        var snapshot = new Dictionary<string, string>(_overrides);
        _settings.Update(s => s with { ShortcutOverrides = snapshot });
        ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }
}
