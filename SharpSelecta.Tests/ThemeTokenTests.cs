using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia.Markup.Xaml;

namespace SharpSelecta.Tests;

// Enforces the rule that every visual value (color, opacity, font, shape, size) comes from the theme
// tokens in Styles/ThemeColors.axaml, ThemeMetrics.axaml, Spacing.axaml and Icons.axaml - never a
// literal in a view, a style or code - so a custom theme can change any of them.
public partial class ThemeTokenTests
{
    private static readonly string[] TokenFileNames = ["ThemeColors.axaml", "ThemeMetrics.axaml", "Spacing.axaml", "Icons.axaml"];

    // Vector artwork coordinates (not style values): the shapes of the placeholder disc, same as
    // the path data inside an icon SVG. Its colors and stroke thickness are still tokens.
    private static readonly Dictionary<string, string[]> GeometryExemptions = new()
    {
        ["AlbumCoverPlaceholderIcon.axaml"] = ["Width", "Height"],
    };

    private static readonly string[] ColorAttributes =
        ["Foreground", "Background", "BorderBrush", "Fill", "Stroke", "Color", "CurrentColor", "SelectionBrush", "CaretBrush"];

    private static readonly string[] MetricAttributes =
    [
        "Opacity", "FontSize", "FontWeight", "FontStyle", "FontFamily", "CornerRadius", "BorderThickness", "StrokeThickness",
        "Width", "Height", "MinWidth", "MinHeight", "MaxWidth", "MaxHeight", "Padding", "Margin", "Spacing", "RowSpacing", "ColumnSpacing",
    ];

    private static readonly string[] StructuralValues = ["0", "Auto", "*", "NaN", "Transparent"];

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "SharpSelecta.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("SharpSelecta.slnx not found above the test binaries.");
    }

    private static string AppDir() => Path.Combine(RepoRoot(), "SharpSelecta.App");

    private static IEnumerable<string> SourceFiles(string pattern) =>
        Directory.EnumerateFiles(AppDir(), pattern, SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static IEnumerable<string> NonTokenAxamlFiles() =>
        SourceFiles("*.axaml").Where(path => !TokenFileNames.Contains(Path.GetFileName(path)));

    private static string ReadWithoutComments(string path) => CommentRegex().Replace(File.ReadAllText(path), match =>
        new string(match.Value.Select(c => c == '\n' ? '\n' : ' ').ToArray()));

    private static string Where(string path, string text, int index) =>
        $"{Path.GetRelativePath(RepoRoot(), path)}:{text.AsSpan(0, index).Count('\n') + 1}";

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex CommentRegex();

    [GeneratedRegex("""(?<=["'>])#[0-9A-Fa-f]{6,8}\b""")]
    private static partial Regex HexColorRegex();

    [GeneratedRegex(@"\s([A-Za-z][\w.]*)=""([^""]*)""")]
    private static partial Regex AttributeRegex();

    [GeneratedRegex(@"<Setter\s+Property=""([\w.:]+)""\s+Value=""([^""]*)""")]
    private static partial Regex SetterRegex();

    [GeneratedRegex(@"\b(?:Column|Row)Definitions=""([^""]*)""")]
    private static partial Regex DefinitionsRegex();

    [GeneratedRegex(@"\{(?:DynamicResource|StaticResource)\s+([^\s}]+)\}")]
    private static partial Regex ResourceReferenceRegex();

    [GeneratedRegex(@"TryFindResource\(\s*""([^""]+)""")]
    private static partial Regex CodeResourceReferenceRegex();

    private static bool IsAllowed(string attribute, string value, string file)
    {
        if (value.StartsWith('{') || StructuralValues.Contains(value))
            return true;

        return GeometryExemptions.TryGetValue(Path.GetFileName(file), out var exempt) && exempt.Contains(attribute);
    }

    private static string LastSegment(string name) => name[(name.LastIndexOfAny(['.', ':']) + 1)..];

    private static HashSet<string> DefinedTokens()
    {
        var keys = new HashSet<string>();
        foreach (var name in TokenFileNames)
        {
            var path = Path.Combine(AppDir(), "Styles", name);
            foreach (var element in XDocument.Load(path).Descendants())
            {
                var key = element.Attribute(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;
                if (key is not null && element.Parent?.Name.LocalName.EndsWith("ThemeDictionaries") != true)
                {
                    keys.Add(key);
                }
            }
        }

        return keys;
    }

    [Test]
    public async Task Axaml_ContainsNoLiteralColors()
    {
        var violations = new List<string>();
        foreach (var path in NonTokenAxamlFiles())
        {
            var text = ReadWithoutComments(path);
            violations.AddRange(HexColorRegex().Matches(text).Select(m => $"{Where(path, text, m.Index)} hex color {m.Value}"));

            foreach (Match match in AttributeRegex().Matches(text))
            {
                var (name, value) = (match.Groups[1].Value, match.Groups[2].Value);
                if (ColorAttributes.Contains(name) && !IsAllowed(name, value, path))
                    violations.Add($"{Where(path, text, match.Index)} {name}=\"{value}\"");
            }

            foreach (Match match in SetterRegex().Matches(text))
            {
                var (name, value) = (LastSegment(match.Groups[1].Value), match.Groups[2].Value);
                if (ColorAttributes.Contains(name) && !IsAllowed(name, value, path))
                    violations.Add($"{Where(path, text, match.Index)} Setter {name} = \"{value}\"");
            }
        }

        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task Axaml_ContainsNoLiteralOpacitiesFontsShapesOrSizes()
    {
        var violations = new List<string>();
        foreach (var path in NonTokenAxamlFiles())
        {
            var text = ReadWithoutComments(path);

            foreach (Match match in AttributeRegex().Matches(text))
            {
                var (name, value) = (match.Groups[1].Value, match.Groups[2].Value);
                if (MetricAttributes.Contains(name) && !IsAllowed(name, value, path))
                    violations.Add($"{Where(path, text, match.Index)} {name}=\"{value}\"");
            }

            foreach (Match match in SetterRegex().Matches(text))
            {
                var (name, value) = (LastSegment(match.Groups[1].Value), match.Groups[2].Value);
                if (MetricAttributes.Contains(name) && !IsAllowed(name, value, path))
                    violations.Add($"{Where(path, text, match.Index)} Setter {name} = \"{value}\"");
            }

            foreach (Match match in DefinitionsRegex().Matches(text))
            {
                foreach (var part in match.Groups[1].Value.Split(',').Select(p => p.Trim()))
                {
                    if (!Regex.IsMatch(part, @"^(Auto|\d*\*)$"))
                        violations.Add($"{Where(path, text, match.Index)} grid definition \"{part}\"");
                }
            }
        }

        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task Code_DoesNotCreateColorsBrushesOrShapesFromLiterals()
    {
        var forbidden = new Regex(@"Color\.From|Color\.Parse|Colors\.(?!Transparent)|Brushes\.(?!Transparent)|new SolidColorBrush\(|new Thickness\(|new CornerRadius\(");
        var violations = new List<string>();
        foreach (var path in SourceFiles("*.cs").Where(p => !p.Contains($"{Path.DirectorySeparatorChar}Styles{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(path);
            violations.AddRange(forbidden.Matches(text).Select(m => $"{Where(path, text, m.Index)} {m.Value}"));
        }

        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task EveryReferencedResourceKey_IsAnAppThemeToken()
    {
        var defined = DefinedTokens();
        var violations = new List<string>();

        foreach (var path in NonTokenAxamlFiles())
        {
            var text = ReadWithoutComments(path);
            foreach (Match match in ResourceReferenceRegex().Matches(text))
            {
                if (!defined.Contains(match.Groups[1].Value))
                    violations.Add($"{Where(path, text, match.Index)} {match.Groups[1].Value}");
            }
        }

        foreach (var path in SourceFiles("*.cs"))
        {
            var text = File.ReadAllText(path);
            foreach (Match match in CodeResourceReferenceRegex().Matches(text))
            {
                if (!defined.Contains(match.Groups[1].Value))
                    violations.Add($"{Where(path, text, match.Index)} {match.Groups[1].Value}");
            }
        }

        await Assert.That(violations).IsEmpty();
    }

    [Test]
    public async Task ThemeColors_DefinesTheSameKeysForLightAndDark()
    {
        var document = XDocument.Load(Path.Combine(AppDir(), "Styles", "ThemeColors.axaml"));
        var xKey = XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
        var variants = document.Descendants()
            .Where(e => e.Parent?.Name.LocalName.EndsWith("ThemeDictionaries") == true)
            .ToDictionary(e => e.Attribute(xKey)!.Value, e => e.Elements().Select(c => c.Attribute(xKey)!.Value).Order().ToList());

        await Assert.That(variants.Keys).IsEquivalentTo(["Light", "Dark"]);
        await Assert.That(variants["Dark"]).IsEquivalentTo(variants["Light"]);
    }

    [Test]
    [Arguments("ThemeColors.axaml")]
    [Arguments("ThemeMetrics.axaml")]
    public async Task ThemeDictionaries_LoadAsResourceDictionaries(string fileName)
    {
        var xaml = File.ReadAllText(Path.Combine(AppDir(), "Styles", fileName));

        var loaded = AvaloniaRuntimeXamlLoader.Load(xaml);

        await Assert.That(loaded).IsNotNull();
    }

    [Test]
    public async Task ThemeLayout_ReadsEveryValueFromTheBuiltInThemeTokens()
    {
        var host = new Avalonia.Controls.Border();
        foreach (var fileName in new[] { "ThemeMetrics.axaml", "Spacing.axaml" })
        {
            var dictionary = (Avalonia.Controls.ResourceDictionary)AvaloniaRuntimeXamlLoader.Load(File.ReadAllText(Path.Combine(AppDir(), "Styles", fileName)));
            host.Resources.MergedDictionaries.Add(dictionary);
        }

        var layout = SharpSelecta.App.Styles.ThemeLayout.From(host);

        await Assert.That(layout.TileSizeMin).IsLessThanOrEqualTo(layout.TileSizeDefault);
        await Assert.That(layout.TileSizeDefault).IsLessThanOrEqualTo(layout.TileSizeMax);
        await Assert.That(layout.TileSizeStep).IsGreaterThan(0);
        await Assert.That(layout.TileSpacing).IsEqualTo(16); // Spacing.L
        await Assert.That(layout.RightColumnWidth).IsGreaterThan(0);
    }

    [Test]
    public async Task ThemeLayout_WhenATokenIsMissing_FailsLoudlyNamingIt()
    {
        var host = new Avalonia.Controls.Border();

        await Assert.That(() => SharpSelecta.App.Styles.ThemeLayout.From(host)).Throws<InvalidOperationException>();
    }
}
