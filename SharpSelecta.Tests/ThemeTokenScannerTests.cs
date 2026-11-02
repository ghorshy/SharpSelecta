namespace SharpSelecta.Tests;

public class ThemeTokenScannerTests
{
    private static string View(string body) =>
        $"""
         <UserControl xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
         {body}
         </UserControl>
         """;

    [Test]
    public async Task LiteralColors_FlagsADoubleQuotedLiteral()
    {
        var violations = ThemeTokenScanner.LiteralColors(View("""<TextBlock Foreground="Red" />"""), "View.axaml");

        await Assert.That(violations).IsEquivalentTo([new ThemeTokenViolation(2, "Foreground = \"Red\"")]);
    }

    [Test]
    public async Task LiteralMetrics_FlagsASingleQuotedLiteral()
    {
        var violations = ThemeTokenScanner.LiteralMetrics(View("<TextBlock FontSize='12' />"), "View.axaml");

        await Assert.That(violations).IsEquivalentTo([new ThemeTokenViolation(2, "FontSize = \"12\"")]);
    }

    [Test]
    public async Task LiteralMetrics_FlagsASetterValueElement()
    {
        var axaml = View("""
                         <UserControl.Styles>
                           <Style Selector="TextBlock">
                             <Setter Property="FontSize">
                               <Setter.Value><x:Double>12</x:Double></Setter.Value>
                             </Setter>
                           </Style>
                         </UserControl.Styles>
                         """);

        var violations = ThemeTokenScanner.LiteralMetrics(axaml, "View.axaml");

        await Assert.That(violations).IsEquivalentTo([new ThemeTokenViolation(4, "FontSize = \"12\"")]);
    }

    [Test]
    public async Task Literals_FlagSetterContentWithoutASetterValueElement()
    {
        var axaml = View("""
                         <Setter Property="FontSize"><x:Double>12</x:Double></Setter>
                         <Setter Property="Background">Red</Setter>
                         """);

        var metrics = ThemeTokenScanner.LiteralMetrics(axaml, "View.axaml");
        var colors = ThemeTokenScanner.LiteralColors(axaml, "View.axaml");

        await Assert.That(metrics).IsEquivalentTo([new ThemeTokenViolation(2, "FontSize = \"12\"")]);
        await Assert.That(colors).IsEquivalentTo([new ThemeTokenViolation(3, "Background = \"Red\"")]);
    }

    [Test]
    public async Task LiteralColors_FlagsAPropertyElement()
    {
        var violations = ThemeTokenScanner.LiteralColors(View("<TextBlock><TextBlock.Foreground>Red</TextBlock.Foreground></TextBlock>"), "View.axaml");

        await Assert.That(violations).IsEquivalentTo([new ThemeTokenViolation(2, "Foreground = \"Red\"")]);
    }

    [Test]
    public async Task LiteralMetrics_FlagsFixedGridDefinitionsInASetter()
    {
        var violations = ThemeTokenScanner.LiteralMetrics(View("""<Setter Property="Grid.ColumnDefinitions" Value="200,Auto,*" />"""), "View.axaml");

        await Assert.That(violations).IsEquivalentTo([new ThemeTokenViolation(2, "grid definition \"200\"")]);
    }

    [Test]
    public async Task TokensStructuralValuesAndExemptGeometry_AreAllowed()
    {
        var axaml = View("""
                         <!-- <TextBlock Foreground="Red" /> -->
                         <Canvas Width="100" Height='100' Background="Transparent" Margin="0">
                           <TextBlock FontSize="{DynamicResource Font.Size.Body}" Foreground="{DynamicResource Brush.Text}" />
                           <Setter Property="Opacity"><Setter.Value><StaticResource ResourceKey="Opacity.Dim" /></Setter.Value></Setter>
                         </Canvas>
                         """);

        var colors = ThemeTokenScanner.LiteralColors(axaml, "AlbumCoverPlaceholderIcon.axaml");
        var metrics = ThemeTokenScanner.LiteralMetrics(axaml, "AlbumCoverPlaceholderIcon.axaml");

        await Assert.That(colors).IsEmpty();
        await Assert.That(metrics).IsEmpty();
    }
}
