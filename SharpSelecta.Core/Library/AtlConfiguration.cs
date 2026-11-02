namespace SharpSelecta.Core.Library;

public static class AtlConfiguration
{
    public static void Ensure()
    {
        // Otherwise ATL reports the file name as Title for untitled files, which the tag editor
        // would then show (and write back) as if it were a real tag.
        ATL.Settings.UseFileNameWhenNoTitle = false;
        ATL.Settings.OutputStacktracesToConsole = false;
    }
}
