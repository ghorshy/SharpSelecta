using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class FuzzySearchTests
{
    [Test]
    public async Task Score_ExactSubstringMatch_ReturnsAPositiveScore()
    {
        var score = FuzzySearch.Score("Wonderwall", "wonder");

        await Assert.That(score).IsNotNull();
    }

    [Test]
    public async Task Score_IsCaseInsensitive()
    {
        var score = FuzzySearch.Score("Wonderwall", "WONDER");

        await Assert.That(score).IsNotNull();
    }

    [Test]
    public async Task Score_ContiguousSubstringMatch_ScoresHigherThanSubsequenceMatch()
    {
        var substringScore = FuzzySearch.Score("Wonderwall", "wonder");
        var subsequenceScore = FuzzySearch.Score("Wonderwall", "wowl");

        await Assert.That(substringScore).IsNotNull();
        await Assert.That(subsequenceScore).IsNotNull();
        await Assert.That(substringScore!.Value).IsGreaterThan(subsequenceScore!.Value);
    }

    [Test]
    public async Task Score_MatchAtWordStart_ScoresHigherThanMatchMidWord()
    {
        var atStart = FuzzySearch.Score("Wonderful", "wonder");
        var midWord = FuzzySearch.Score("Superwonder", "wonder");

        await Assert.That(atStart!.Value).IsGreaterThan(midWord!.Value);
    }

    [Test]
    public async Task Score_SubsequenceWithGaps_ScoresLowerThanTighterSubsequence()
    {
        var tight = FuzzySearch.Score("aXbXc", "abc");
        var loose = FuzzySearch.Score("aXXXbXXXc", "abc");

        await Assert.That(tight!.Value).IsGreaterThan(loose!.Value);
    }

    [Test]
    public async Task Score_WhenNotAllQueryCharactersAppearInOrder_ReturnsNull()
    {
        var score = FuzzySearch.Score("Wonderwall", "xyz");

        await Assert.That(score).IsNull();
    }

    [Test]
    public async Task Score_WithEmptyOrNullCandidate_ReturnsNull()
    {
        await Assert.That(FuzzySearch.Score((string?)null, "wonder")).IsNull();
        await Assert.That(FuzzySearch.Score("", "wonder")).IsNull();
    }

    [Test]
    public async Task Score_WithEmptyQuery_ReturnsNull()
    {
        var score = FuzzySearch.Score("Wonderwall", "");

        await Assert.That(score).IsNull();
    }

    [Test]
    public async Task Score_Track_MatchesOnTitleArtistOrAlbum()
    {
        var track = new Track("/music/wonderwall.mp3", "wonderwall.mp3")
        {
            Title = "Wonderwall",
            Artist = "Oasis",
            Album = "(What's the Story) Morning Glory?",
        };

        await Assert.That(FuzzySearch.Score(track, "wonder")).IsNotNull();
        await Assert.That(FuzzySearch.Score(track, "oasis")).IsNotNull();
        await Assert.That(FuzzySearch.Score(track, "morning glory")).IsNotNull();
        await Assert.That(FuzzySearch.Score(track, "zzz")).IsNull();
    }

    [Test]
    public async Task Score_Track_WithNoTitle_FallsBackToDisplayName()
    {
        var track = new Track("/music/untagged.mp3", "untagged.mp3");

        await Assert.That(FuzzySearch.Score(track, "untagged")).IsNotNull();
    }

    [Test]
    public async Task Score_Track_PrefersATitleMatchOverAnEquallyStrongArtistOrAlbumMatch()
    {
        var titleMatch = new Track("/music/a.mp3", "a.mp3") { Title = "Wonder", Artist = "Unrelated", Album = "Unrelated" };
        var artistMatch = new Track("/music/b.mp3", "b.mp3") { Title = "Unrelated", Artist = "Wonder", Album = "Unrelated" };
        var albumMatch = new Track("/music/c.mp3", "c.mp3") { Title = "Unrelated", Artist = "Unrelated", Album = "Wonder" };

        var titleScore = FuzzySearch.Score(titleMatch, "wonder");
        var artistScore = FuzzySearch.Score(artistMatch, "wonder");
        var albumScore = FuzzySearch.Score(albumMatch, "wonder");

        await Assert.That(titleScore!.Value).IsGreaterThan(artistScore!.Value);
        await Assert.That(artistScore!.Value).IsGreaterThan(albumScore!.Value);
    }

    [Test]
    public async Task Score_Track_TitleMatchOutranksAnArtistMatchThatWouldOtherwiseTie()
    {
        // Both fields match "milli" as an exact, word-start substring - same raw Score(string?,
        // string) result - so without field weighting these would tie and fall back to whatever
        // order the tracks happened to be in.
        var militaryDance = new Track("/music/a.mp3", "a.mp3") { Title = "Millitary Dance", Artist = "Some Artist" };
        var milliVanilli = new Track("/music/b.mp3", "b.mp3") { Title = "Girl You Know It's True", Artist = "Milli Vanilli" };

        var militaryDanceScore = FuzzySearch.Score(militaryDance, "milli");
        var milliVanilliScore = FuzzySearch.Score(milliVanilli, "milli");

        await Assert.That(militaryDanceScore!.Value).IsGreaterThan(milliVanilliScore!.Value);
    }
}
