using SharpSelecta.Audio;

namespace SharpSelecta.Tests;

public class PactlEventTests
{
    [Test]
    public async Task Parse_ReadsKindFacilityAndIndex()
    {
        var parsed = PactlEvent.Parse("Event 'remove' on sink #57");

        await Assert.That(parsed).IsEqualTo(new PactlEvent("remove", "sink", 57));
    }

    [Test]
    public async Task Parse_KeepsHyphenatedFacilities()
    {
        var parsed = PactlEvent.Parse("Event 'change' on sink-input #608");

        await Assert.That(parsed).IsEqualTo(new PactlEvent("change", "sink-input", 608));
    }

    [Test]
    [Arguments("")]
    [Arguments("Got SIGINT, exiting.")]
    [Arguments("Event 'change' on server")]
    public async Task Parse_ReturnsNullForLinesThatAreNotIndexedEvents(string line)
    {
        await Assert.That(PactlEvent.Parse(line)).IsNull();
    }
}
