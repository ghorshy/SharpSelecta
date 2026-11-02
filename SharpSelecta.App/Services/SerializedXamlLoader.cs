using Avalonia.Markup.Xaml;

namespace SharpSelecta.App.Services;

// AvaloniaRuntimeXamlLoader isn't thread-safe: concurrent loads collide in its emitted assembly.
public static class SerializedXamlLoader
{
    private static readonly object Gate = new();

    public static object Load(string xaml)
    {
        lock (Gate)
        {
            return AvaloniaRuntimeXamlLoader.Load(xaml);
        }
    }
}
