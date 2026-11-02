namespace SharpSelecta.Core.Library;

// Next to the target, so the final rename over it stays on the same filesystem.
internal static class HiddenTempFile
{
    public static string PathNextTo(string filePath, string extension) =>
        Path.Combine(
            Path.GetDirectoryName(filePath) ?? "",
            $".{Path.GetFileNameWithoutExtension(filePath)}.{Guid.NewGuid():N}.tmp{extension}");
}
