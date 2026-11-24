using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using SharpSelecta.App.Services;

namespace SharpSelecta.Tests;

public class ThemeFileParserTests
{
    private const string Head = """
        <ResourceDictionary xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                            xmlns:sys="clr-namespace:System;assembly=System.Runtime">
        """;

    private static string Theme(string body) => $"{Head}\n{body}\n</ResourceDictionary>";

    // StreamGeometry needs a render platform, which tests don't start (for either loader), so it's left out.
    private static string StylesFile(string name) => string.Join('\n',
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SharpSelecta.App", "Styles", name))
            .Where(line => !line.TrimStart().StartsWith("<StreamGeometry", StringComparison.Ordinal)));

    private static ThemeFileException Fails(string text, Func<string, Type?>? existingType = null) =>
        Assert.Throws<ThemeFileException>(() => ThemeFileParser.Parse(text, null, existingType));

    // Our parser and Avalonia's XAML loader read every built-in token file to the same values.
    [Test]
    [Arguments("ThemeColors.axaml")]
    [Arguments("ThemeMetrics.axaml")]
    [Arguments("Spacing.axaml")]
    [Arguments("Icons.axaml")]
    public async Task BuiltInTokens_ReadTheSameAsAvaloniasLoader(string fileName)
    {
        var text = StylesFile(fileName);
        var reference = (ResourceDictionary)ReferenceXamlLoader.Load(text);

        using var parsed = ThemeFileParser.Parse(text, null, null);

        await AssertSameResources(parsed.Resources, reference);
        await Assert.That(parsed.Resources.ThemeDictionaries.Keys).IsEquivalentTo(reference.ThemeDictionaries.Keys);
        foreach (var (variant, dictionary) in reference.ThemeDictionaries)
        {
            await AssertSameResources((ResourceDictionary)parsed.Resources.ThemeDictionaries[variant], (ResourceDictionary)dictionary);
        }
    }

    private static async Task AssertSameResources(ResourceDictionary actual, ResourceDictionary expected)
    {
        await Assert.That(actual.Keys).IsEquivalentTo(expected.Keys);
        foreach (var key in expected.Keys)
        {
            var (want, got) = (expected[key], actual[key]);
            var same = (want, got) switch
            {
                // A brush that follows a resource has no color of its own until it's bound to a host.
                (ISolidColorBrush w, ISolidColorBrush g) => w.Opacity.Equals(g.Opacity) && (w.Color == g.Color || w.Color == default),
                (Geometry w, Geometry g) => w.Bounds == g.Bounds,
                (TransformOperations w, TransformOperations g) => w.Value == g.Value,
                _ => Equals(want, got),
            };
            await Assert.That(same).IsTrue().Because($"{key}: expected {want}, got {got}");
        }
    }

    [Test]
    public async Task ADynamicResourceColor_FollowsTheResource_UntilDisposed()
    {
        var host = new Border();
        host.Resources["SystemAccentColor"] = Colors.Red;
        var parsed = ThemeFileParser.Parse(
            Theme("""<SolidColorBrush x:Key="Brush.Accent" Color="{DynamicResource SystemAccentColor}" Opacity="0.5" />"""), host, null);
        var brush = (SolidColorBrush)parsed.Resources["Brush.Accent"]!;

        await Assert.That(brush.Color).IsEqualTo(Colors.Red);
        await Assert.That(brush.Opacity).IsEqualTo(0.5);

        host.Resources["SystemAccentColor"] = Colors.Blue;
        await Assert.That(brush.Color).IsEqualTo(Colors.Blue);

        parsed.Dispose();
        host.Resources["SystemAccentColor"] = Colors.Green;
        await Assert.That(brush.Color).IsEqualTo(Colors.Blue);
    }

    [Test]
    public async Task LightAndDarkDictionaries_GoToTheirVariants()
    {
        using var parsed = ThemeFileParser.Parse(Theme("""
            <ResourceDictionary.ThemeDictionaries>
                <ResourceDictionary x:Key="Light"><Color x:Key="Color.IconForeground">#FF111111</Color></ResourceDictionary>
                <ResourceDictionary x:Key="Dark"><Color x:Key="Color.IconForeground">#FFEEEEEE</Color></ResourceDictionary>
            </ResourceDictionary.ThemeDictionaries>
            """), null, null);

        var light = (ResourceDictionary)parsed.Resources.ThemeDictionaries[ThemeVariant.Light];
        var dark = (ResourceDictionary)parsed.Resources.ThemeDictionaries[ThemeVariant.Dark];
        await Assert.That(light["Color.IconForeground"]).IsEqualTo(Color.Parse("#FF111111"));
        await Assert.That(dark["Color.IconForeground"]).IsEqualTo(Color.Parse("#FFEEEEEE"));
    }

    [Test]
    public async Task SystemNamespaceTypes_AreReadLikeXTypes()
    {
        using var parsed = ThemeFileParser.Parse(Theme("""<sys:Double x:Key="Spacing.M">9.5</sys:Double>"""), null, null);

        await Assert.That(parsed.Resources["Spacing.M"]).IsEqualTo(9.5);
    }

    [Test]
    public async Task KnownKeys_OfTheRightType_AreAccepted()
    {
        Type? Existing(string key) => key switch { "Spacing.M" => typeof(double), "Brush.Accent" => typeof(SolidColorBrush), _ => null };

        using var parsed = ThemeFileParser.Parse(Theme("""
            <x:Double x:Key="Spacing.M">10</x:Double>
            <SolidColorBrush x:Key="Brush.Accent" Color="#FF00FF00" />
            """), null, Existing);

        await Assert.That(parsed.Resources["Spacing.M"]).IsEqualTo(10.0);
    }

    [Test]
    public async Task AnUnknownKey_IsReportedWithItsLine()
    {
        var error = Fails(Theme("""
            <x:Double x:Key="Spacing.M">10</x:Double>
            <x:Double x:Key="Spacing.Typo">10</x:Double>
            """), key => key == "Spacing.M" ? typeof(double) : null);

        await Assert.That(error.Line).IsEqualTo(4);
        await Assert.That(error.Message).Contains("Spacing.Typo");
    }

    [Test]
    public async Task ADynamicResourceToAnUnknownKey_IsReported()
    {
        var error = Fails(Theme("""<SolidColorBrush x:Key="Brush.Accent" Color="{DynamicResource NoSuchColor}" />"""),
            key => key == "Brush.Accent" ? typeof(SolidColorBrush) : null);

        await Assert.That(error.Message).Contains("NoSuchColor");
    }

    [Test]
    public async Task TheWrongType_ForAKnownKey_IsReported()
    {
        var error = Fails(Theme("""<Thickness x:Key="Spacing.M">4</Thickness>"""), key => key == "Spacing.M" ? typeof(double) : null);

        await Assert.That(error.Message).Contains("Spacing.M");
        await Assert.That(error.Message).Contains("Double");
    }

    [Test]
    [Arguments("""<x:Double x:Key="Spacing.M">wide</x:Double>""", "wide")]
    [Arguments("""<Color x:Key="Color.IconForeground">#XYZ</Color>""", "#XYZ")]
    [Arguments("""<SolidColorBrush x:Key="Brush.Accent" Color="#FF00FF00" Opacity="half" />""", "half")]
    [Arguments("""<Thickness x:Key="Border.All">thick</Thickness>""", "thick")]
    public async Task AMalformedValue_IsReportedWithItsLine(string element, string value)
    {
        var error = Fails(Theme(element));

        await Assert.That(error.Line).IsEqualTo(3);
        await Assert.That(error.Message).Contains(value);
    }

    [Test]
    public async Task AnythingButAResourceValue_IsRefused()
    {
        var error = Fails(Theme("""<Button x:Key="Brush.Accent" Content="hi" />"""));

        await Assert.That(error.Message).Contains("Button");
    }

    [Test]
    [Arguments("not xml at all {{{")]
    [Arguments("""<Styles xmlns="https://github.com/avaloniaui" />""")]
    public async Task ANonThemeFile_IsRefused(string text)
    {
        var error = Fails(text);

        await Assert.That(error.Line).IsEqualTo(1);
    }

    [Test]
    public async Task AMissingOrRepeatedKey_IsRefused()
    {
        var missing = Fails(Theme("""<x:Double>4</x:Double>"""));
        var repeated = Fails(Theme("""
            <x:Double x:Key="Spacing.M">4</x:Double>
            <x:Double x:Key="Spacing.M">5</x:Double>
            """));

        await Assert.That(missing.Message).Contains("x:Key");
        await Assert.That(repeated.Message).Contains("Spacing.M");
    }

    [Test]
    public async Task AThemeVariantOtherThanLightOrDark_IsRefused()
    {
        var error = Fails(Theme("""
            <ResourceDictionary.ThemeDictionaries>
                <ResourceDictionary x:Key="Sepia" />
            </ResourceDictionary.ThemeDictionaries>
            """));

        await Assert.That(error.Line).IsEqualTo(4);
    }

    private const string Gradients = """
        <LinearGradientBrush x:Key="Brush.Linear" StartPoint="0%,0%" EndPoint="100%,50%" SpreadMethod="Reflect" Opacity="0.9">
            <GradientStop Color="#FF102030" Offset="0" />
            <GradientStop Color="#80FFFFFF" Offset="0.75" />
        </LinearGradientBrush>
        <RadialGradientBrush x:Key="Brush.Radial" Center="50%,40%" GradientOrigin="25%,25%" RadiusX="60%" RadiusY="30%">
            <RadialGradientBrush.GradientStops>
                <GradientStop Color="Red" Offset="0.2" />
                <GradientStop Color="#00000000" Offset="1" />
            </RadialGradientBrush.GradientStops>
        </RadialGradientBrush>
        <ConicGradientBrush x:Key="Brush.Conic" Center="10,20" Angle="45">
            <ConicGradientBrush.GradientStops>
                <GradientStops>
                    <GradientStop Color="#FF00FF00" Offset="0.5" />
                </GradientStops>
            </ConicGradientBrush.GradientStops>
        </ConicGradientBrush>
        """;

    [Test]
    public async Task GradientBrushes_ReadTheSameAsAvaloniasLoader()
    {
        var text = Theme(Gradients);
        var reference = (ResourceDictionary)ReferenceXamlLoader.Load(text);

        using var parsed = ThemeFileParser.Parse(text, null, null);

        foreach (var key in new[] { "Brush.Linear", "Brush.Radial", "Brush.Conic" })
        {
            var want = (GradientBrush)reference[key]!;
            var got = (GradientBrush)parsed.Resources[key]!;
            await Assert.That(got.GetType()).IsEqualTo(want.GetType());
            await Assert.That(got.Opacity).IsEqualTo(want.Opacity);
            await Assert.That(got.SpreadMethod).IsEqualTo(want.SpreadMethod);
            await Assert.That(got.GradientStops.Select(s => (s.Color, s.Offset)))
                .IsEquivalentTo(want.GradientStops.Select(s => (s.Color, s.Offset)), TUnit.Assertions.Enums.CollectionOrdering.Matching);
            var same = (want, got) switch
            {
                (LinearGradientBrush w, LinearGradientBrush g) => w.StartPoint == g.StartPoint && w.EndPoint == g.EndPoint,
                (RadialGradientBrush w, RadialGradientBrush g) => w.Center == g.Center && w.GradientOrigin == g.GradientOrigin
                                                                   && w.RadiusX == g.RadiusX && w.RadiusY == g.RadiusY,
                (ConicGradientBrush w, ConicGradientBrush g) => w.Center == g.Center && w.Angle.Equals(g.Angle),
                _ => false,
            };
            await Assert.That(same).IsTrue().Because(key);
        }
    }

    [Test]
    public async Task AGradientStop_CanFollowAResource()
    {
        var host = new Border();
        host.Resources["SystemAccentColor"] = Colors.Red;
        using var parsed = ThemeFileParser.Parse(Theme("""
            <LinearGradientBrush x:Key="Brush.Surface">
                <GradientStop Color="{DynamicResource SystemAccentColor}" Offset="0" />
                <GradientStop Color="Transparent" Offset="1" />
            </LinearGradientBrush>
            """), host, null);
        var brush = (LinearGradientBrush)parsed.Resources["Brush.Surface"]!;

        host.Resources["SystemAccentColor"] = Colors.Blue;

        await Assert.That(brush.GradientStops[0].Color).IsEqualTo(Colors.Blue);
    }

    [Test]
    public async Task AGradient_CanReplaceASolidBrush()
    {
        using var parsed = ThemeFileParser.Parse(Theme("""
            <LinearGradientBrush x:Key="Brush.Surface"><GradientStop Color="Black" Offset="0" /></LinearGradientBrush>
            """), null, key => key == "Brush.Surface" ? typeof(SolidColorBrush) : null);

        await Assert.That(parsed.Resources["Brush.Surface"]).IsTypeOf<LinearGradientBrush>();
    }

    [Test]
    [Arguments("""<LinearGradientBrush x:Key="Brush.Surface" Angle="45" />""", "Angle")]
    [Arguments("""<LinearGradientBrush x:Key="Brush.Surface" StartPoint="left" />""", "left")]
    [Arguments("""<LinearGradientBrush x:Key="Brush.Surface"><GradientStop Color="#nope" /></LinearGradientBrush>""", "#nope")]
    [Arguments("""<LinearGradientBrush x:Key="Brush.Surface"><Border /></LinearGradientBrush>""", "Border")]
    public async Task AMistakeInAGradient_IsReported(string element, string mentioned)
    {
        var error = Fails(Theme(element));

        await Assert.That(error.Line).IsEqualTo(3);
        await Assert.That(error.Message).Contains(mentioned);
    }
}
