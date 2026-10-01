namespace SharpSelecta.Core.Library;

// A track can have several artists. ATL exposes them as one string joined with its display
// separator and splits it back into the format's native multiple values (e.g. several ID3v2 TPE1
// values, several Vorbis ARTIST fields) on save, so the list is just that string split/joined.
public static class ArtistList
{
    public static IReadOnlyList<string> Split(string? artist) =>
        string.IsNullOrWhiteSpace(artist)
            ? []
            : artist.Split(ATL.Settings.DisplayValueSeparator, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    // The artists every track of an album shares (tracks with no artist are ignored), in the order the
    // first such track lists them - what an album with guests on some tracks is "by".
    public static IReadOnlyList<string> Common(IEnumerable<string?> artistValues)
    {
        var lists = artistValues.Select(Split).Where(list => list.Count > 0).ToList();
        return lists.Count == 0
            ? []
            : lists[0].Where(artist => lists.Skip(1).All(list => list.Contains(artist, StringComparer.OrdinalIgnoreCase))).ToList();
    }

    // Null for an empty list, matching how the rest of the tag fields represent "no value".
    public static string? Join(IEnumerable<string> artists)
    {
        var normalized = Split(string.Join(ATL.Settings.DisplayValueSeparator, artists));
        return normalized.Count == 0 ? null : string.Join(ATL.Settings.DisplayValueSeparator, normalized);
    }
}
