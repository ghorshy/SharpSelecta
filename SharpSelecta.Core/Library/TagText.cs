namespace SharpSelecta.Core.Library;

internal static class TagText
{
    public static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
