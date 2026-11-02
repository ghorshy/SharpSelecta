using System.Text.Json;

namespace SharpSelecta.Integrations.AlbumArt;

internal static class JsonText
{
    public static string StringOf(this JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? value.GetString() ?? "" : "";
}
