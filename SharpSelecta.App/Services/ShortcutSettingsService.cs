using System;
using System.Collections.Generic;
using System.Linq;
using SharpSelecta.App.Shortcuts;
using SharpSelecta.Core.Library;

namespace SharpSelecta.App.Services;

// Fast, deterministic JSON-file wrapper (SettingsStore) - no interface needed for testing, per the mock-vs-real rule.
public sealed class ShortcutSettingsService
{
    private readonly string _settingsFilePath;
    private readonly Dictionary<string, string> _overrides;

    public event EventHandler? ShortcutsChanged;

    public ShortcutSettingsService(string settingsFilePath)
    {
        _settingsFilePath = settingsFilePath;
        _overrides = new Dictionary<string, string>(SettingsStore.LoadShortcutOverrides(settingsFilePath) ?? new Dictionary<string, string>());
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
        SettingsStore.SaveShortcutOverrides(_settingsFilePath, _overrides);
        ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }
}
