namespace SharpSelecta.Core.Library;

public enum AppTheme
{
    System,
    Light,
    Dark,

    /// <summary>A user-supplied theme file (see <see cref="SettingsStore.LoadCustomThemeFileName(string)"/>).</summary>
    Custom,
}
