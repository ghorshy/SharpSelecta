using SharpSelecta.Audio;

namespace SharpSelecta.Tests;

public class PactlJsonTests
{
    // Trimmed `pactl -f json list sinks` shape - captured against a real PipeWire session
    // (see PipeWireOutputDeviceService's comments for why pactl over wpctl/pw-dump).
    private const string SinksJson = """
        [
            {
                "name": "alsa_output.usb-FiiO_DigiHug_USB_Audio-01.analog-stereo",
                "description": "Fiio E10 Analog Stereo"
            },
            {
                "name": "alsa_output.pci-0000_28_00.1.hdmi-stereo-extra2",
                "description": "Navi 21/23 HDMI/DP Audio Controller Digital Stereo (HDMI 3)"
            }
        ]
        """;

    [Test]
    public async Task ParseSinks_ReturnsNameAndDescriptionForEachSink()
    {
        var sinks = PactlJson.ParseSinks(SinksJson);

        await Assert.That(sinks).IsEquivalentTo(
        [
            ("alsa_output.usb-FiiO_DigiHug_USB_Audio-01.analog-stereo", "Fiio E10 Analog Stereo"),
            ("alsa_output.pci-0000_28_00.1.hdmi-stereo-extra2", "Navi 21/23 HDMI/DP Audio Controller Digital Stereo (HDMI 3)"),
        ]);
    }

    [Test]
    public async Task ParseSinks_WithNoSinks_ReturnsEmpty()
    {
        var sinks = PactlJson.ParseSinks("[]");

        await Assert.That(sinks).IsEmpty();
    }

    // Real sink-input properties captured for SharpSelecta's own stream on a live session -
    // OwnAudioSharp's cpal/ALSA output surfaces through PipeWire's ALSA compatibility bridge, not
    // as a native pipewire-pulse client, so there is deliberately no "application.process.id"
    // here (unlike a browser tab's stream, which does carry one).
    private const string OwnSinkInputJson = """
        [
            {
                "index": 306,
                "properties": {
                    "application.name": "PipeWire ALSA [SharpSelecta.App]",
                    "node.name": "alsa_playback.SharpSelecta.App"
                }
            },
            {
                "index": 155,
                "properties": {
                    "application.name": "Zen",
                    "application.process.id": "114907",
                    "application.process.binary": "zen-bin"
                }
            }
        ]
        """;

    [Test]
    public async Task FindSinkInputIndexes_MatchesOwnProcessByApplicationNameBracketFormat()
    {
        var indexes = PactlJson.FindSinkInputIndexes(OwnSinkInputJson, "SharpSelecta.App");

        await Assert.That(indexes).IsEquivalentTo([306L]);
    }

    [Test]
    public async Task FindSinkInputIndexes_DoesNotMatchAnUnrelatedNativePulseClient()
    {
        var indexes = PactlJson.FindSinkInputIndexes(OwnSinkInputJson, "Zen");

        await Assert.That(indexes).IsEquivalentTo([155L]);
    }

    [Test]
    public async Task FindSinkInputIndexes_WhenNoStreamMatches_ReturnsEmpty()
    {
        var indexes = PactlJson.FindSinkInputIndexes(OwnSinkInputJson, "SomeOtherApp");

        await Assert.That(indexes).IsEmpty();
    }

    // node.name is the fallback match target if application.name ever stops carrying the
    // bracketed process name in a future pipewire-alsa version.
    [Test]
    public async Task FindSinkInputIndexes_FallsBackToNodeNameWhenApplicationNameDoesNotMatch()
    {
        const string json = """
            [
                {
                    "index": 42,
                    "properties": {
                        "application.name": "ALSA Playback",
                        "node.name": "alsa_playback.SharpSelecta.App"
                    }
                }
            ]
            """;

        var indexes = PactlJson.FindSinkInputIndexes(json, "SharpSelecta.App");

        await Assert.That(indexes).IsEquivalentTo([42L]);
    }
}
