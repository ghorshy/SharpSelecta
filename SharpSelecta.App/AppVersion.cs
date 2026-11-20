using System.Reflection;

namespace SharpSelecta.App;

public static class AppVersion
{
    public static string Current { get; } = Format(
        typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(AppVersion).Assembly.GetName().Version?.ToString(3)
        ?? string.Empty);

    // The SDK appends the commit to the informational version ("1.0.0+5712d41a..."); a short hash is enough.
    public static string Format(string informationalVersion)
    {
        var plus = informationalVersion.IndexOf('+');
        if (plus < 0)
            return informationalVersion;

        var version = informationalVersion[..plus];
        var commit = informationalVersion[(plus + 1)..];
        return commit.Length == 0 ? version : $"{version} ({commit[..Math.Min(7, commit.Length)]})";
    }
}
