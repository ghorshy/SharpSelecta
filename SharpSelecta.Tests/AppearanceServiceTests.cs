using Avalonia.Media;
using SharpSelecta.App.Services;
using SharpSelecta.Core.Library;

namespace SharpSelecta.Tests;

public class AppearanceServiceTests
{
    private static readonly Dictionary<string, object> Theme = new()
    {
        ["FontSize.Content"] = 14.0,
        ["FontSize.Small"] = 11.0,
        ["ControlContentThemeFontSize"] = 14.0,
        ["Size.Icon.M"] = 18.0,
    };

    private static IReadOnlyDictionary<string, object> Compute(AppSettings settings) =>
        AppearanceService.Compute(settings, key => Theme.GetValueOrDefault(key));

    [Test]
    public async Task Compute_WithDefaultSettings_OverridesNothing()
    {
        await Assert.That(Compute(new AppSettings())).IsEmpty();
    }

    [Test]
    public async Task Compute_SetsTheContentTextSize_AndScalesTheOtherTextAndIconsInProportion()
    {
        var overrides = Compute(new AppSettings { FontSize = 21 });

        await Assert.That(overrides["FontSize.Content"]).IsEqualTo(21.0);
        await Assert.That(overrides["FontSize.Small"]).IsEqualTo(17.0);
        await Assert.That(overrides["ControlContentThemeFontSize"]).IsEqualTo(21.0);
        await Assert.That(overrides["Size.Icon.M"]).IsEqualTo(27.0);
    }

    [Test]
    public async Task Compute_WhenTheSizeIsTheThemesOwn_OverridesNothing()
    {
        await Assert.That(Compute(new AppSettings { FontSize = 14 })).IsEmpty();
    }

    [Test]
    public async Task Compute_WithAnAccentColor_OverridesTheAccentAndItsShades()
    {
        var overrides = Compute(new AppSettings { AccentColor = "#D13438" });

        await Assert.That(overrides["SystemAccentColor"]).IsEqualTo(AppearanceService.ParseAccent("#D13438")!.Value);
        await Assert.That(overrides.Keys.Count(key => key.StartsWith("SystemAccentColor"))).IsEqualTo(7);
        await Assert.That(overrides["SystemAccentColorLight1"]).IsNotEqualTo(overrides["SystemAccentColor"]);
    }

    [Test]
    [Arguments("D13438")]
    [Arguments("#D1343")]
    [Arguments("#FFD13438")]
    [Arguments("#GGGGGG")]
    [Arguments("red")]
    public async Task ParseAccent_AcceptsOnlyHashAndSixHexDigits(string text)
    {
        await Assert.That(AppearanceService.ParseAccent(text)).IsNull();
    }

    [Test]
    public async Task FormatAccent_RoundTripsThroughParseAccent()
    {
        var color = AppearanceService.ParseAccent("#0a7Bc4")!.Value;

        await Assert.That(AppearanceService.FormatAccent(color)).IsEqualTo("#0A7BC4");
    }
}
