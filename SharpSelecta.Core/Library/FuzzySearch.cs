using System;

namespace SharpSelecta.Core.Library;

public static class FuzzySearch
{
    public static int? Score(string? candidate, string query)
    {
        if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(query))
            return null;

        var exactIndex = candidate.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (exactIndex >= 0)
        {
            var atWordStart = exactIndex == 0 || !char.IsLetterOrDigit(candidate[exactIndex - 1]);
            return 1000 - exactIndex + (atWordStart ? 100 : 0);
        }

        var qi = 0;
        var lastMatch = -1;
        var gapPenalty = 0;
        for (var ci = 0; ci < candidate.Length && qi < query.Length; ci++)
        {
            if (char.ToUpperInvariant(candidate[ci]) != char.ToUpperInvariant(query[qi]))
                continue;

            if (lastMatch >= 0)
                gapPenalty += ci - lastMatch - 1;

            lastMatch = ci;
            qi++;
        }

        return qi == query.Length ? 500 - gapPenalty : null;
    }

    public static int? Score(Track track, string query)
    {
        int? best = null;
        foreach (var candidate in new[] { track.Title ?? track.DisplayName, track.Artist, track.Album })
        {
            var score = Score(candidate, query);
            if (score is not null && (best is null || score > best))
                best = score;
        }

        return best;
    }
}
