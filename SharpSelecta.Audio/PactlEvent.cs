using System.Text.RegularExpressions;

namespace SharpSelecta.Audio;

public sealed partial record PactlEvent(string Kind, string Facility, long Index)
{
    public static PactlEvent? Parse(string line)
    {
        var match = EventLine().Match(line);
        return match.Success
            ? new PactlEvent(match.Groups[1].Value, match.Groups[2].Value, long.Parse(match.Groups[3].Value))
            : null;
    }

    [GeneratedRegex(@"^Event '([a-z]+)' on ([a-z-]+) #(\d+)$")]
    private static partial Regex EventLine();
}
