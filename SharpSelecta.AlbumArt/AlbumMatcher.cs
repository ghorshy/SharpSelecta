using System.Globalization;
using System.Text;

using SharpSelecta.Core.AlbumArt;

namespace SharpSelecta.AlbumArt;

// Decides which of a service's search hits is the album that was asked for. A wrong cover is worse
// than none, so a hit needs the right artist AND a title that is the album (or an edition of it).
internal static class AlbumMatcher
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "";

        var builder = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    public static T? PickBest<T>(IEnumerable<T> hits, AlbumArtQuery query, Func<T, string?> title, Func<T, string?> artist)
        where T : class
    {
        var wantedAlbum = Normalize(query.Album);
        var wantedArtist = Normalize(query.Artist);

        return hits
            .Select((hit, index) => (Hit: hit, Index: index, Score: Score(Normalize(title(hit)), Normalize(artist(hit)), wantedAlbum, wantedArtist), TitleLength: Normalize(title(hit)).Length))
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.TitleLength) // among editions, the plainest title
            .ThenBy(x => x.Index)       // then the service's own relevance order
            .Select(x => x.Hit)
            .FirstOrDefault();
    }

    private static int Score(string title, string artist, string wantedAlbum, string wantedArtist)
    {
        if (wantedArtist.Length > 0 && artist.Length > 0 && !artist.Contains(wantedArtist) && !wantedArtist.Contains(artist))
            return 0;

        if (title == wantedAlbum)
            return 3;

        if (title.StartsWith(wantedAlbum + " ", StringComparison.Ordinal))
            return 2; // "Album (Deluxe Edition)" for "Album"

        if (wantedAlbum.StartsWith(title + " ", StringComparison.Ordinal))
            return 1; // wanted "Album (Deluxe)", the service lists plain "Album"

        return 0;
    }
}
