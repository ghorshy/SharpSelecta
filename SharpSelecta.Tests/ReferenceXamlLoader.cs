using Avalonia.Markup.Xaml;

namespace SharpSelecta.Tests;

// Avalonia's own runtime XAML loader, kept for tests only: the yardstick for ThemeFileParser.
// It isn't thread-safe: concurrent loads collide in its emitted assembly.
public static class ReferenceXamlLoader
{
    private static readonly Lock Gate = new();

    public static object Load(string xaml)
    {
        lock (Gate)
        {
            return AvaloniaRuntimeXamlLoader.Load(xaml);
        }
    }
}
