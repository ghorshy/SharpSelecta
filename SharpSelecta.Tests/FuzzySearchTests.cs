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
}
